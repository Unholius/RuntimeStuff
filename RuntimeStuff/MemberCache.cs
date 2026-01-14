// ***********************************************************************
// Assembly         : RuntimeStuff
// Author           : Rudnev Sergey
// Created          : 01-06-2026
//
// Last Modified By : Rudnev Sergey
// Last Modified On : 01-07-2026
// ***********************************************************************
// <copyright file="MemberCache.cs" company="Rudnev Sergey">
//     Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************

namespace RuntimeStuff
{
    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel;

#if DEBUG
    using System.IO;
#endif

    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Text.RegularExpressions;
    using RuntimeStuff.Extensions;
    using RuntimeStuff.Helpers;

    /// <summary>
    /// Определяет тип имени, используемый для члена, включая основное имя, отображаемое имя, имена для JSON, XML, а также
    /// имена для базы данных и схемы. Позволяет комбинировать несколько типов с помощью битовой маски.
    /// </summary>
    /// <remarks>Перечисление поддерживает флаги, что позволяет указывать сразу несколько типов имен для одного члена.
    /// Используйте для выбора или фильтрации нужных представлений имени в различных сценариях, например при сериализации,
    /// отображении или работе с базой данных.</remarks>
    [Flags]
    public enum MemberNameType
    {
        /// <summary>
        /// Любой тип имени (основное, отображаемое, JSON, XML и др.).
        /// </summary>
        None = 0,

        /// <summary>
        /// Основное имя члена (Name).
        /// </summary>
        Name = 1,

        /// <summary>
        /// Отображаемое имя (DisplayName).
        /// </summary>
        DisplayName = 2,

        /// <summary>
        /// Имя в JSON (JsonName).
        /// </summary>
        JsonName = 4,

        /// <summary>
        /// Имя в XML (XmlName).
        /// </summary>
        XmlName = 8,

        /// <summary>
        /// Имя колонки (ColumnName).
        /// </summary>
        ColumnName = 16,

        /// <summary>
        /// Имя таблицы (TableName).
        /// </summary>
        TableName = 32,

        /// <summary>
        /// Имя схемы (SchemaName).
        /// </summary>
        SchemaName = 64,
    }

    /// <summary>
    /// v.2026.01.07 (RS) <br />
    /// Представляет расширенную обёртку над <see cref="MemberInfo" />, предоставляющую унифицированный доступ к
    /// дополнительной информации и операциям для членов типа .NET<br />
    /// (свойств, методов, полей, событий, конструкторов и самих типов).
    /// Класс предназначен для использования в сценариях динамического анализа типов, построения универсальных
    /// сериализаторов, ORM, генераторов кода, UI-редакторов и других задач,<br />
    /// где требуется расширенная работа с метаданными .NET.
    /// <para>
    /// Класс <c>TypeCache</c> позволяет:
    /// <list type="bullet"><item>
    /// Получать расширенные сведения о членах типа, включая их атрибуты, типы, модификаторы доступа, связи
    /// с базовыми типами и интерфейсами.
    /// </item><item>
    /// Быстро и кэшированно получать члены по имени, включая поиск по альтернативным именам (отображаемое
    /// имя, JSON-имя, имя колонки и др.).
    /// </item><item>
    /// Определять семантику члена: является ли он свойством, методом, полем, событием, конструктором, типом
    /// и т.д.
    /// </item><item>
    /// Получать и устанавливать значения свойств и полей через делегаты, а также вызывать методы по
    /// отражению.
    /// </item><item>
    /// Работать с атрибутами, включая стандартные и пользовательские, а также поддерживать работу с
    /// атрибутами сериализации (JSON, XML, DataAnnotations).
    /// </item><item>
    /// Определять особенности члена: является ли он коллекцией, словарём, делегатом, nullable, числовым,
    /// булевым, кортежем, простым типом и др.
    /// </item><item>
    /// Получать информацию о первичных и внешних ключах, колонках, таблицах и схемах для интеграции с ORM и
    /// сериализаторами.
    /// </item><item> Кэшировать результаты для повышения производительности при повторных обращениях.</item></list></para>
    /// </summary>
    public class MemberCache : MemberInfo
    {
        /// <summary>
        /// The member information cache.
        /// </summary>
        protected static readonly ConcurrentDictionary<MemberInfo, MemberCache> MemberInfoCache =
            new ConcurrentDictionary<MemberInfo, MemberCache>();

#if DEBUG
        private static readonly object Lock = new object();
#endif

        /// <summary>
        /// The constructors cache.
        /// </summary>
        private static readonly ConcurrentDictionary<string, Func<object>> ConstructorsCache =
            new ConcurrentDictionary<string, Func<object>>();

        /// <summary>
        /// The getters.
        /// </summary>
        private readonly ConcurrentDictionary<string, Func<object, object>[]> getters =
            new ConcurrentDictionary<string, Func<object, object>[]>(
                StringComparison.OrdinalIgnoreCase.ToStringComparer());

        /// <summary>
        /// The member cache.
        /// </summary>
        private readonly ConcurrentDictionary<string, MemberCache> memberCache =
            new ConcurrentDictionary<string, MemberCache>();

        /// <summary>
        /// The setters.
        /// </summary>
        private readonly ConcurrentDictionary<string, Action<object, object>[]> setters =
            new ConcurrentDictionary<string, Action<object, object>[]>(StringComparison.OrdinalIgnoreCase
                .ToStringComparer());

        /// <summary>
        /// The type.
        /// </summary>
        private readonly Type type;

        /// <summary>
        /// The type cache.
        /// </summary>
        private readonly MemberCache typeCache;

        /// <summary>
        /// The attributes.
        /// </summary>
        private Attribute[] attributes;

        /// <summary>
        /// The base types.
        /// </summary>
        private Type[] baseTypes;

        /// <summary>
        /// The columns.
        /// </summary>
        private MemberCache[] columns;

        /// <summary>
        /// The constructors.
        /// </summary>
        private ConstructorInfo[] constructors;

        /// <summary>
        /// The events.
        /// </summary>
        private EventInfo[] events;

        /// <summary>
        /// The fields.
        /// </summary>
        private FieldInfo[] fields;

        /// <summary>
        /// The FKS.
        /// </summary>
        private MemberCache[] fks;

        /// <summary>
        /// The json name.
        /// </summary>
        private string jsonName;

        /// <summary>
        /// The members.
        /// </summary>
        private Dictionary<MemberInfo, MemberCache> members;

        /// <summary>
        /// The methods.
        /// </summary>
        private MethodInfo[] methods;

        /// <summary>
        /// The PKS.
        /// </summary>
        private MemberCache[] pks;

        /// <summary>
        /// The properties.
        /// </summary>
        private PropertyInfo[] properties;

        /// <summary>
        /// The tables.
        /// </summary>
        private MemberCache[] tables;

        /// <summary>
        /// The XML attribute.
        /// </summary>
        private string xmlAttr;

        /// <summary>
        /// The XML elem.
        /// </summary>
        private string xmlElem;

        /// <summary>
        /// Initializes a new instance of the <see cref="MemberCache" /> class.
        /// </summary>
        /// <param name="memberInfo">The member information.</param>
        public MemberCache(MemberInfo memberInfo)
            : this(memberInfo, memberInfo is Type)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MemberCache" /> class.
        /// Конструктор для создания расширенной информации о члене класса.
        /// </summary>
        /// <param name="memberInfo">Информация о члене класса.</param>
        /// <param name="getMembers">Получить информацию о дочерних членах: свойства, поля, методы и т.п.</param>
        /// <param name="parent">The parent.</param>
        private MemberCache(MemberInfo memberInfo, bool getMembers, MemberCache parent = null)
        {
#if DEBUG
            var beginTime = DateTime.Now.ExactNow();
#endif
            this.Parent = parent;

            this.typeCache = memberInfo as MemberCache;
            if (this.typeCache != null)
            {
                memberInfo = this.typeCache.MemberInfo;
            }

            this.MemberInfo = memberInfo;

            // Определяем тип члена класса
            var t = this.MemberInfo as Type;
            var pi = this.MemberInfo as PropertyInfo;
            var fi = this.MemberInfo as FieldInfo;
            var mi = this.MemberInfo as MethodInfo;
            var ci = this.MemberInfo as ConstructorInfo;
            var mx = this.MemberInfo as MemberCache;
            var e = this.MemberInfo as EventInfo;

            // Устанавливаем тип в зависимости от вида члена класса
            if (t != null)
            {
                this.type = t;
            }

            if (pi != null)
            {
                this.type = pi.PropertyType;
            }

            if (fi != null)
            {
                this.type = fi.FieldType;
            }

            if (mi != null)
            {
                this.type = mi.ReturnType;
            }

            if (ci != null)
            {
                this.type = ci.DeclaringType;
            }

            if (mx != null)
            {
                this.type = mx.Type;
            }

            if (e != null)
            {
                this.type = e.EventHandlerType;
            }

            // Устанавливаем свойства типа
            this.Type = this.type ??
                   throw new NotSupportedException(
                       $"{nameof(MemberCache)}: ({memberInfo.GetType().Name}) not supported!");
            this.IsDictionary = this.typeCache?.IsDictionary ?? Obj.IsDictionary(this.type);
            this.IsDelegate = this.typeCache?.IsDelegate ?? Obj.IsDelegate(this.type);
            this.IsFloat = this.typeCache?.IsFloat ?? Obj.IsFloat(this.type);
            this.IsNullable = this.typeCache?.IsNullable ?? Obj.IsNullable(this.type);
            if (e != null)
            {
                this.type = e.EventHandlerType;
            }

            this.IsNumeric = this.typeCache?.IsNumeric ?? Obj.IsNumeric(this.type);
            this.IsBoolean = this.typeCache?.IsBoolean ?? Obj.IsBoolean(this.type);
            this.IsCollection = this.typeCache?.IsCollection ?? Obj.IsCollection(this.type);
            this.ElementType = this.IsCollection ? this.typeCache?.ElementType ?? Obj.GetCollectionItemType(this.Type) : null;
            this.IsBasic = this.typeCache?.IsBasic ?? Obj.IsBasic(this.type);
            this.IsEnum = this.typeCache?.IsEnum ?? this.type.IsEnum;
            this.IsConst = this.typeCache?.IsConst ?? (fi != null && fi.IsLiteral && !fi.IsInitOnly);
            this.IsBasicCollection = this.typeCache?.IsBasicCollection ?? (this.IsCollection && Obj.IsBasic(this.ElementType));
            this.IsObject = this.typeCache?.IsObject ?? this.type == typeof(object);
            this.IsTuple = this.typeCache?.IsTuple ?? Obj.IsTuple(this.type);
            this.IsProperty = pi != null;
            this.IsEvent = e != null;
            this.IsField = fi != null;
            this.IsType = t != null;
            this.IsMethod = mi != null;
            this.IsConstructor = ci != null;
            this.CanWrite = pi != null ? pi.CanWrite : fi != null;
            this.CanRead = pi != null ? pi.CanRead : fi != null;
            this.IsPublic = this.typeCache?.IsPublic ?? Obj.IsPublic(this.MemberInfo);
            this.IsPrivate = this.typeCache?.IsPrivate ?? Obj.IsPrivate(this.MemberInfo);
            this.Attributes = this.GetAttributes().ToDictionaryDistinct(x => x.GetType().Name);
            this.Events = this.GetEvents().ToDictionaryDistinct(x => x.Name);

            // Обработка имени
            this.Name = this.typeCache?.Name ?? this.MemberInfo.Name.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries)
                .LastOrDefault() ?? string.Empty;

            // Получение атрибутов
            this.Description = this.typeCache?.Description ??
                          this.MemberInfo.GetCustomAttributes<DescriptionAttribute>().FirstOrDefault()?.Description;
            this.DisplayName = this.typeCache?.DisplayName ?? this.MemberInfo.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName;

            if (this.DisplayName == null && this.Attributes.TryGetValue("DisplayAttribute", out var da))
            {
                this.DisplayName = da.GetType().GetProperty("Name")?.GetValue(da)?.ToString();
            }

            // Дополнительная обработка для типов
            if (this.IsType && !this.IsBasic)
            {
                this.DefaultConstructor = this.typeCache?.DefaultConstructor ?? CreateConstructorDelegate(t);

                if (this.typeCache == null)
                {
                    var tblAttr = this.Attributes.GetValueOrDefault("TableAttribute");
                    if (tblAttr != null)
                    {
                        var tblNameProperty = tblAttr.GetType().GetProperty("Name");
                        var tblSchemaProperty = tblAttr.GetType().GetProperty("Schema");
                        this.TableName = tblNameProperty?.GetValue(tblAttr)?.ToString();
                        this.SchemaName = tblSchemaProperty?.GetValue(tblAttr)?.ToString();
                    }
                    else
                    {
                        this.TableName = this.Name;
                    }
                }
                else
                {
                    this.TableName = this.typeCache.TableName;
                    this.SchemaName = this.typeCache.SchemaName;
                }

                this.Properties = this.typeCache?.Properties ??
                             this.Members.Where(x => x.Value.IsProperty).ToDictionaryDistinct(x => x.Value.Name, y => y.Value);
                this.PublicProperties = this.typeCache?.PublicProperties ??
                                   this.Properties.Values.Where(x => x.IsPublic).ToDictionary(x => x.Name);
                this.PrivateProperties = this.typeCache?.PrivateProperties ??
                                    this.Properties.Values.Where(x => x.IsPrivate).ToDictionary(x => x.Name);
                this.PublicBasicProperties = this.typeCache?.PublicBasicProperties ??
                                        this.PublicProperties.Values.Where(x => x.IsBasic).ToDictionary(x => x.Name);
                this.PublicBasicEnumerableProperties = this.typeCache?.PublicBasicEnumerableProperties ??
                                                  this.PublicProperties.Values.Where(x => x.IsBasicCollection)
                                                      .ToDictionary(x => x.Name);
                this.PublicEnumerableProperties = this.typeCache?.PublicEnumerableProperties ?? this.PublicProperties.Values
                    .Where(x => x.IsCollection && !x.IsBasicCollection).ToDictionary(x => x.Name);

                this.Fields = this.typeCache?.Fields ?? this.Members.Where(x => x.Value.IsField).ToDictionary(x => x.Value.Name, y => y.Value);
                this.PublicFields = this.typeCache?.PublicFields ??
                               this.Fields.Values.Where(x => x.IsPublic).ToDictionary(x => x.Name);
                this.PrivateFields = this.typeCache?.PrivateFields ??
                                this.Fields.Values.Where(x => x.IsPrivate).ToDictionary(x => x.Name);

                this.PrimaryKeys = this.typeCache?.PrimaryKeys ??
                              this.PublicBasicProperties.Where(x => x.Value.Attributes.ContainsKey("KeyAttribute"))
                                  .Select(x => x.Value)
                                  .ToDictionary(x => x.Name);

                if (this.PrimaryKeys.Count == 0)
                {
                    var p =
                        this.PublicBasicProperties.GetValueOrDefault("id", StringComparison.OrdinalIgnoreCase.ToStringComparer()) ??
                        this.PublicBasicProperties.GetValueOrDefault(this.TableName + "id", StringComparer.OrdinalIgnoreCase);
                    if (p != null)
                    {
                        this.PrimaryKeys = new Dictionary<string, MemberCache>
                            { { p.Name, this.PublicBasicProperties[p.Name] } };
                    }
                }

                this.ForeignKeys = this.typeCache?.ForeignKeys ?? this.PublicBasicProperties
                    .Where(x => x.Value.Attributes.ContainsKey("ForeignKeyAttribute"))
                    .Select(x => x.Value)
                    .ToDictionary(x => x.Name);

                this.ColumnProperties = this.typeCache?.ColumnProperties ?? this.PublicBasicProperties.Where(x =>
                        !x.Value.IsPrimaryKey
                        && x.Value.Attributes.ContainsKey("ColumnAttribute")
                        && !x.Value.Attributes.ContainsKey("NotMappedAttribute")).Select(x => x.Value)
                    .ToDictionary(x => x.Name);

                if (this.ColumnProperties.Count == 0)
                {
                    this.ColumnProperties = this.PublicBasicProperties.Where(x => !x.Value.IsPrimaryKey)
                        .Select(x => x.Value)
                        .ToDictionary(x => x.Name);
                }

                var propsAndFields = this.Members.Where(x => x.Value.IsProperty || x.Value.IsField)
                    .GroupBy(x => x.Value.Name, StringComparison.OrdinalIgnoreCase.ToStringComparer());
                foreach (var pf in propsAndFields)
                {
                    this.setters[pf.Key] = pf.Select(x => x.Value.Setter).ToArray();
                    this.getters[pf.Key] = pf.Select(x => x.Value.Getter).ToArray();
                }

                // Рекурсивная загрузка членов класса
                if (getMembers)
                {
                    this.members = this.typeCache?.members ?? this.GetChildMembersInternal();
                }
            }

            // Дополнительная обработка для свойств
            if (pi != null)
            {
                if (this.Parent == null && pi?.DeclaringType != null)
                {
                    this.Parent = Create(pi.DeclaringType);
                }

                this.PropertyType = pi.PropertyType;
                this.IsSetterPublic = pi.GetSetMethod()?.IsPublic == true;
                this.IsSetterPrivate = pi.GetSetMethod()?.IsPrivate == true;
                this.IsGetterPublic = pi.GetGetMethod()?.IsPublic == true;
                this.IsGetterPrivate = pi.GetGetMethod()?.IsPrivate == true;
                this.TableName = this.Parent?.TableName;
                this.SchemaName = this.Parent?.SchemaName;

                if (this.typeCache == null)
                {
                    var keyAttr = this.Attributes.GetValueOrDefault("KeyAttribute");
                    var colAttr = this.Attributes.GetValueOrDefault("ColumnAttribute");
                    var fkAttr = this.Attributes.GetValueOrDefault("ForeignKeyAttribute");
                    this.IsPrimaryKey = keyAttr != null || string.Equals(this.Name, "id", StringComparison.OrdinalIgnoreCase);
                    this.IsForeignKey = fkAttr != null;
                    try
                    {
                        this.PropertyBackingField = this.Parent.GetFields().FirstOrDefault(x => x.Name == $"<{this.Name}>k__BackingField") ??
                                               Obj.GetFieldInfoFromGetAccessor(pi.GetGetMethod(true));
                    }
                    catch
                    {
                        // ignore
                    }

                    try
                    {
                        this.Setter = Obj.PropertySetterCache.Get(pi);

                        if (this.Setter == null && this.PropertyBackingField != null)
                        {
                            this.Setter = Obj.FieldSetterCache.Get(this.PropertyBackingField);
                        }
                    }
                    catch (Exception)
                    {
                        this.Setter = (o, v) => pi.SetValue(o, v);
                    }

                    try
                    {
                        this.Getter = Obj.PropertyGetterCache.Get(pi);
                    }
                    catch (Exception)
                    {
                        this.Getter = o => pi.GetValue(o);
                    }

                    this.TableName = this.Parent?.TableName;
                    this.ColumnName = colAttr != null
                        ? colAttr.GetType().GetProperty("Name")?.GetValue(colAttr)?.ToString() ?? this.Name
                        : this.Name;

                    this.ForeignKeyName = fkAttr?.GetType().GetProperty("Name")?.GetValue(fkAttr)?.ToString() ??
                                        string.Empty;
                }
                else
                {
                    this.Setter = this.typeCache.Setter;
                    this.Getter = this.typeCache.Getter;
                    this.PropertyBackingField = this.typeCache.PropertyBackingField;
                    this.ColumnName = this.typeCache.ColumnName;
                    this.ForeignKeyName = this.typeCache.ForeignKeyName;
                    this.IsPrimaryKey = this.typeCache.IsPrimaryKey;
                    this.IsForeignKey = this.typeCache.IsForeignKey;
                }
            }

            if (fi != null)
            {
                this.IsSetterPublic = true;
                this.IsSetterPrivate = false;
                this.IsGetterPublic = true;
                this.IsGetterPrivate = false;
                this.FieldType = fi.FieldType;
                try
                {
                    this.Setter = this.typeCache?.Setter ?? Obj.FieldSetterCache.Get(fi);
                }
                catch
                {
                    this.Setter = (obj, value) => fi.SetValue(obj, value);
                }

                try
                {
                    this.Getter = this.typeCache?.Getter ?? Obj.FieldGetterCache.Get(fi);
                }
                catch (Exception)
                {
                    this.Getter = x => fi.GetValue(x);
                }
            }

            if (this.typeCache == null)
            {
                var displayAttr = this.Attributes.GetValueOrDefault("DisplayAttribute");
                if (displayAttr == null)
                {
                    return;
                }

                var groupNameProp = displayAttr.GetType().GetProperty("GroupName");
                if (groupNameProp != null)
                {
                    this.GroupName = groupNameProp.GetValue(displayAttr)?.ToString() ?? this.DisplayName;
                }
            }
            else
            {
                this.GroupName = this.typeCache.GroupName;
            }
#if DEBUG
            lock (Lock)
            {
                File.AppendAllText(
                    "MemberCache.log",
                    $@"{DateTime.Now:O} - Created MemberCache for {this.MemberInfo.MemberType} '{this.MemberInfo.DeclaringType?.FullName}.{this.MemberInfo.Name}' Elapsed ms: {(DateTime.Now.ExactNow() - beginTime).TotalMilliseconds}" + Environment.NewLine);
            }
#endif
        }

        /// <summary>
        /// Gets or sets флаги для поиска членов класса по умолчанию.
        /// </summary>
        /// <value>The default binding flags.</value>
#pragma warning disable S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields
        public static BindingFlags DefaultBindingFlags { get; set; } = BindingFlags.Instance | BindingFlags.NonPublic |
#pragma warning restore S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields
                                                                       BindingFlags.Public | BindingFlags.Static;

        /// <summary>
        /// Gets карта интерфейсов коллекций к конкретным типам реализаций.
        /// </summary>
        /// <value>The interface to instance map.</value>
        public static Dictionary<Type, Type> InterfaceToInstanceMap { get; } = new Dictionary<Type, Type>
        {
            { typeof(IEnumerable), typeof(List<object>) },
            { typeof(IEnumerable<>), typeof(List<>) },
            { typeof(ICollection), typeof(ObservableCollection<object>) },
            { typeof(ICollection<>), typeof(ObservableCollection<>) },
            { typeof(IDictionary<,>), typeof(Dictionary<,>) },
        };

        /// <summary>
        /// Gets атрибуты члена класса.
        /// </summary>
        /// <value>The attributes.</value>
        public Dictionary<string, Attribute> Attributes { get; }

        /// <summary>
        /// Gets базовые типы и интерфейсы.
        /// </summary>
        /// <value>The base types.</value>
        public Type[] BaseTypes
        {
            get
            {
                if (this.baseTypes != null)
                {
                    return this.baseTypes;
                }

                this.baseTypes = Obj.GetBaseTypes(this.type, getInterfaces: true);
                return this.baseTypes;
            }
        }

        /// <summary>
        /// Gets a value indicating whether можно ли читать значение (для свойств и полей).
        /// </summary>
        /// <value><c>true</c> if this instance can read; otherwise, <c>false</c>.</value>
        public bool CanRead { get; }

        /// <summary>
        /// Gets a value indicating whether можно ли записывать значение (для свойств и полей).
        /// </summary>
        /// <value><c>true</c> if this instance can write; otherwise, <c>false</c>.</value>
        public bool CanWrite { get; }

        /// <summary>
        /// Gets имя колонки (из атрибута ColumnAttribute), если такого атрибута нет - то имя свойства.
        /// </summary>
        /// <value>The name of the column.</value>
        public string ColumnName { get; }

        /// <summary>
        /// Gets словарь свойств по имени колонки у которых есть один из атрибутов Column, Foreign и нет NotMapped и Key. Если таких
        /// нет, то все простые публичные свойства кроме первичных ключей.
        /// </summary>
        /// <value>The column properties.</value>
        public Dictionary<string, MemberCache> ColumnProperties { get; }

        /// <summary>
        /// Gets все доступные конструкторы.
        /// </summary>
        /// <value>The constructors.</value>
        public ConstructorInfo[] Constructors => this.GetConstructors();

        /// <summary>
        /// Gets тип, объявивший этот член.
        /// </summary>
        /// <value>The type of the declaring.</value>
        public override Type DeclaringType => this.MemberInfo.DeclaringType;

        /// <summary>
        /// Gets делегат конструктора по умолчанию.
        /// </summary>
        /// <value>The default constructor.</value>
        public Func<object> DefaultConstructor { get; }

        /// <summary>
        /// Gets описание (из атрибута DescriptionAttribute).
        /// </summary>
        /// <value>The description.</value>
        public string Description { get; }

        /// <summary>
        /// Gets отображаемое имя (из атрибута DisplayNameAttribute).
        /// </summary>
        /// <value>The display name.</value>
        public string DisplayName { get; }

        /// <summary>
        /// Gets тип элемента коллекции (если текущий тип является коллекцией).
        /// </summary>
        /// <value>The type of the element.</value>
        public Type ElementType { get; }

        /// <summary>
        /// Gets словарь событий типа.
        /// </summary>
        /// <value>The events.</value>
        public Dictionary<string, EventInfo> Events { get; }

        /// <summary>
        /// Gets the fields.
        /// </summary>
        /// <value>The fields.</value>
        public Dictionary<string, MemberCache> Fields { get; }

        /// <summary>
        /// Gets the type of the field.
        /// </summary>
        /// <value>The type of the field.</value>
        public Type FieldType { get; }

        /// <summary>
        /// Gets имя внешнего ключа (из атрибута ForeignKeyAttribute).
        /// </summary>
        /// <value>The name of the foreign key.</value>
        public string ForeignKeyName { get; }

        /// <summary>
        /// Gets свойства, помеченные атрибутом ForeignKeyAttribute.
        /// </summary>
        /// <value>The foreign keys.</value>
        public Dictionary<string, MemberCache> ForeignKeys { get; }

        /// <summary>
        /// Gets имя группы (из атрибута DisplayAttribute).
        /// </summary>
        /// <value>The name of the group.</value>
        public string GroupName { get; }

        /// <summary>
        /// Gets a value indicating whether type.IsClass.
        /// </summary>
        public bool IsClass => this.Type.IsClass;

        /// <summary>
        /// Gets a value indicating whether является ли тип простым (примитивным или строкой).
        /// </summary>
        /// <value><c>true</c> if this instance is basic; otherwise, <c>false</c>.</value>
        public bool IsBasic { get; }

        /// <summary>
        /// Gets a value indicating whether является ли коллекция коллекцией простых типов.
        /// </summary>
        /// <value><c>true</c> if this instance is basic collection; otherwise, <c>false</c>.</value>
        public bool IsBasicCollection { get; }

        /// <summary>
        /// Gets a value indicating whether является ли тип булевым.
        /// </summary>
        /// <value><c>true</c> if this instance is boolean; otherwise, <c>false</c>.</value>
        public bool IsBoolean { get; }

        /// <summary>
        /// Gets a value indicating whether является ли тип коллекцией.
        /// </summary>
        /// <value><c>true</c> if this instance is collection; otherwise, <c>false</c>.</value>
        public bool IsCollection { get; }

        /// <summary>
        /// Gets a value indicating whether this instance is constant.
        /// </summary>
        /// <value><c>true</c> if this instance is constant; otherwise, <c>false</c>.</value>
        public bool IsConst { get; }

        /// <summary>
        /// Gets or sets a value indicating whether является ли член конструктором.
        /// </summary>
        /// <value><c>true</c> if this instance is constructor; otherwise, <c>false</c>.</value>
        public bool IsConstructor { get; set; }

        /// <summary>
        /// Gets a value indicating whether является ли тип делегатом.
        /// </summary>
        /// <value><c>true</c> if this instance is delegate; otherwise, <c>false</c>.</value>
        public bool IsDelegate { get; }

        /// <summary>
        /// Gets a value indicating whether является ли тип словарем.
        /// </summary>
        /// <value><c>true</c> if this instance is dictionary; otherwise, <c>false</c>.</value>
        public bool IsDictionary { get; }

        /// <summary>
        /// Gets a value indicating whether this instance is enum.
        /// </summary>
        /// <value><c>true</c> if this instance is enum; otherwise, <c>false</c>.</value>
        public bool IsEnum { get; }

        /// <summary>
        /// Gets or sets a value indicating whether является ли член событием.
        /// </summary>
        /// <value><c>true</c> if this instance is event; otherwise, <c>false</c>.</value>
        public bool IsEvent { get; set; }

        /// <summary>
        /// Gets a value indicating whether является ли член полем.
        /// </summary>
        /// <value><c>true</c> if this instance is field; otherwise, <c>false</c>.</value>
        public bool IsField { get; }

        /// <summary>
        /// Gets a value indicating whether является ли тип числом с плавающей точкой.
        /// </summary>
        /// <value><c>true</c> if this instance is float; otherwise, <c>false</c>.</value>
        public bool IsFloat { get; }

        /// <summary>
        /// Gets or sets a value indicating whether является ли свойство внешним ключом.
        /// </summary>
        /// <value><c>true</c> if this instance is foreign key; otherwise, <c>false</c>.</value>
        public bool IsForeignKey { get; set; }

        /// <summary>
        /// Gets a value indicating whether this instance is getter private.
        /// </summary>
        /// <value><c>true</c> if this instance is getter private; otherwise, <c>false</c>.</value>
        public bool IsGetterPrivate { get; }

        /// <summary>
        /// Gets a value indicating whether this instance is getter public.
        /// </summary>
        /// <value><c>true</c> if this instance is getter public; otherwise, <c>false</c>.</value>
        public bool IsGetterPublic { get; }

        /// <summary>
        /// Gets a value indicating whether является ли первичный ключ автоинкрементным (число или Guid).
        /// </summary>
        /// <value><c>true</c> if this instance is identity; otherwise, <c>false</c>.</value>
        public bool IsIdentity => this.IsPrimaryKey && (Obj.IsNumeric(this.Type, false) || this.Type == typeof(Guid));

        /// <summary>
        /// Gets a value indicating whether является ли тип интерфейсом.
        /// </summary>
        /// <value><c>true</c> if this instance is interface; otherwise, <c>false</c>.</value>
        public bool IsInterface => this.Type.IsInterface;

        /// <summary>
        /// Gets or sets a value indicating whether является ли член методом.
        /// </summary>
        /// <value><c>true</c> if this instance is method; otherwise, <c>false</c>.</value>
        public bool IsMethod { get; set; }

        /// <summary>
        /// Gets a value indicating whether является ли тип nullable.
        /// </summary>
        /// <value><c>true</c> if this instance is nullable; otherwise, <c>false</c>.</value>
        public bool IsNullable { get; }

        /// <summary>
        /// Gets a value indicating whether является ли тип числовым.
        /// </summary>
        /// <value><c>true</c> if this instance is numeric; otherwise, <c>false</c>.</value>
        public bool IsNumeric { get; }

        /// <summary>
        /// Gets a value indicating whether является ли тип object.
        /// </summary>
        /// <value><c>true</c> if this instance is object; otherwise, <c>false</c>.</value>
        public bool IsObject { get; }

        /// <summary>
        /// Gets or sets a value indicating whether является ли свойство первичным ключом.
        /// </summary>
        /// <value><c>true</c> if this instance is primary key; otherwise, <c>false</c>.</value>
        public bool IsPrimaryKey { get; set; }

        /// <summary>
        /// Gets a value indicating whether является ли член приватным.
        /// </summary>
        /// <value><c>true</c> if this instance is private; otherwise, <c>false</c>.</value>
        public bool IsPrivate { get; }

        /// <summary>
        /// Gets a value indicating whether является ли член свойством.
        /// </summary>
        /// <value><c>true</c> if this instance is property; otherwise, <c>false</c>.</value>
        public bool IsProperty { get; }

        /// <summary>
        /// Gets a value indicating whether является ли член публичным.
        /// </summary>
        /// <value><c>true</c> if this instance is public; otherwise, <c>false</c>.</value>
        public bool IsPublic { get; }

        /// <summary>
        /// Gets a value indicating whether this instance is setter private.
        /// </summary>
        /// <value><c>true</c> if this instance is setter private; otherwise, <c>false</c>.</value>
        public bool IsSetterPrivate { get; }

        /// <summary>
        /// Gets a value indicating whether this instance is setter public.
        /// </summary>
        /// <value><c>true</c> if this instance is setter public; otherwise, <c>false</c>.</value>
        public bool IsSetterPublic { get; }

        /// <summary>
        /// Gets a value indicating whether является ли тип кортежем.
        /// </summary>
        /// <value><c>true</c> if this instance is tuple; otherwise, <c>false</c>.</value>
        public bool IsTuple { get; }

        /// <summary>
        /// Gets a value indicating whether является ли член типом.
        /// </summary>
        /// <value><c>true</c> if this instance is type; otherwise, <c>false</c>.</value>
        public bool IsType { get; }

        /// <summary>
        /// Gets a value indicating whether является ли тип значимым типом.
        /// </summary>
        /// <value><c>true</c> if this instance is value type; otherwise, <c>false</c>.</value>
        public bool IsValueType => this.type.IsValueType;

        /// <summary>
        /// Gets имя в JSON (из атрибутов JsonPropertyNameAttribute или JsonPropertyAttribute).
        /// </summary>
        /// <value>The name of the json.</value>
        public string JsonName
        {
            get
            {
                if (this.jsonName != null)
                {
                    return this.jsonName;
                }

                if (this.typeCache == null)
                {
                    this.jsonName = string.Empty;
                    var jsonAttr = this.GetAttributes().FirstOrDefault(x => x.GetType().Name.StartsWith("Json"));
                    if (jsonAttr == null)
                    {
                        return this.jsonName;
                    }

                    var propName = jsonAttr.GetType().GetProperties().FirstOrDefault(p => p.Name.EndsWith("Name"));
                    if (propName != null)
                    {
                        this.jsonName = propName.GetValue(jsonAttr)?.ToString();
                    }
                }
                else
                {
                    this.jsonName = this.typeCache.jsonName;
                }

                return this.jsonName;
            }
        }

        /// <summary>
        /// Gets все члены типа (свойства, поля, методы, события).
        /// </summary>
        /// <value>The members.</value>
        public Dictionary<MemberInfo, MemberCache> Members
        {
            get
            {
                if (this.members != null)
                {
                    return this.members;
                }

                this.members = this.typeCache?.members ?? this.GetChildMembersInternal();
                return this.members;
            }
        }

        /// <summary>
        /// Gets тип члена (свойство, метод, поле и т.д.)
        /// </summary>
        /// <value>The type of the member.</value>
        public override MemberTypes MemberType => this.MemberInfo.MemberType;

        /// <summary>
        /// Gets имя члена.
        /// </summary>
        /// <value>The name.</value>
        public sealed override string Name { get; }

        /// <summary>
        /// Gets родительский член (для вложенных членов).
        /// </summary>
        /// <value>The parent.</value>
        public MemberCache Parent { get; private set; }

        /// <summary>
        /// Gets свойства, помеченные атрибутом KeyAttribute. Если таких нет, то ищется сначала "EventId", потом ИмяТаблицыId.
        /// </summary>
        /// <value>The primary keys.</value>
        public Dictionary<string, MemberCache> PrimaryKeys { get; }

        /// <summary>
        /// Gets the private fields.
        /// </summary>
        /// <value>The private fields.</value>
        public Dictionary<string, MemberCache> PrivateFields { get; }

        /// <summary>
        /// Gets the private properties.
        /// </summary>
        /// <value>The private properties.</value>
        public Dictionary<string, MemberCache> PrivateProperties { get; }

        /// <summary>
        /// Gets словарь всех свойств типа (IsProperty).
        /// </summary>
        /// <value>The properties.</value>
        public Dictionary<string, MemberCache> Properties { get; }

        /// <summary>
        /// Gets поле, хранящее значение свойства (для автоматически реализуемых свойств).
        /// </summary>
        /// <value>The property backing field.</value>
        public FieldInfo PropertyBackingField { get; }

        /// <summary>
        /// Gets the type of the property.
        /// </summary>
        /// <value>The type of the property.</value>
        public Type PropertyType { get; }

        /// <summary>
        ///     Gets словарь публичных свойств-коллекций с элементом списка {T}, где T - простой тип <see cref="BaseTypes" /> (IsPublic
        ///     and IsProperty and IsBasicCollection).
        /// </summary>
        public Dictionary<string, MemberCache> PublicBasicEnumerableProperties { get; }

        /// <summary>
        ///     Gets словарь простых (<see cref="Obj.BasicTypes" />) публичных свойств типа (IsPublic и IsProperty и IsBasic).
        /// </summary>
        public Dictionary<string, MemberCache> PublicBasicProperties { get; }

        /// <summary>
        ///     Gets словарь публичных свойств-коллекций с элементом списка {T}, где T : class (IsPublic и IsProperty и IsCollection и !IsBasicCollection).
        /// </summary>
        public Dictionary<string, MemberCache> PublicEnumerableProperties { get; }

        /// <summary>
        /// Gets the public fields.
        /// </summary>
        /// <value>The public fields.</value>
        public Dictionary<string, MemberCache> PublicFields { get; }

        /// <summary>
        ///     Gets словарь публичных свойств типа (IsPublic и IsProperty).
        /// </summary>
        public Dictionary<string, MemberCache> PublicProperties { get; }

        /// <summary>
        /// Gets тип, через который был получен этот член.
        /// </summary>
        /// <value>The type of the reflected.</value>
        public override Type ReflectedType => this.MemberInfo.ReflectedType;

        /// <summary>
        /// Gets имя схемы (из атрибута TableAttribute.Schema).
        /// </summary>
        /// <value>The name of the schema.</value>
        public string SchemaName { get; }

        /// <summary>
        /// Gets делегат для установки значения свойства.
        /// </summary>
        /// <value>The setter.</value>
        public Action<object, object> Setter { get; }

        /// <summary>
        /// Gets имя таблицы (из атрибута TableAttribute.Name).
        /// </summary>
        /// <value>The name of the table.</value>
        public string TableName { get; }

        /// <summary>
        /// Gets тип члена.
        /// </summary>
        /// <value>The type.</value>
        public Type Type { get; }

        /// <summary>
        /// Gets имя XML атрибута (из XmlAttributeAttribute).
        /// </summary>
        /// <value>The name of the XML attribute.</value>
        public string XmlAttributeName
        {
            get
            {
                if (this.xmlAttr != null)
                {
                    return this.xmlAttr;
                }

                if (this.typeCache == null)
                {
                    var xmlAttrs = this.Attributes.Where(x => x.GetType().Name.StartsWith("Xml")).ToArray();
                    if (xmlAttrs.Any())
                    {
                        foreach (var xa in xmlAttrs)
                        {
                            var propName = xa.GetType().GetProperties().FirstOrDefault(p => p.Name.EndsWith("Name"));
                            switch (propName?.Name)
                            {
                                case "ElementName":
                                    this.xmlElem = propName.GetValue(xa)?.ToString();
                                    break;

                                case "AttributeName":
                                    this.xmlAttr = propName.GetValue(xa)?.ToString();
                                    break;
                            }
                        }
                    }
                }
                else
                {
                    this.xmlAttr = this.typeCache.xmlAttr;
                }

                return this.xmlAttr ?? (this.xmlAttr = string.Empty);
            }
        }

        /// <summary>
        /// Gets имя XML элемента (из XmlElementAttribute).
        /// </summary>
        /// <value>The name of the XML element.</value>
        public string XmlElementName
        {
            get
            {
                if (this.xmlElem != null)
                {
                    return this.xmlElem;
                }

                if (this.typeCache == null)
                {
                    var xmlAttrs = this.Attributes.Where(x => x.GetType().Name.StartsWith("Xml")).ToArray();
                    if (xmlAttrs.Any())
                    {
                        foreach (var xa in xmlAttrs)
                        {
                            var propName = xa.GetType().GetProperties().FirstOrDefault(p => p.Name.EndsWith("Name"));
                            switch (propName?.Name)
                            {
                                case "ElementName":
                                    this.xmlElem = propName.GetValue(xa)?.ToString();
                                    break;

                                case "AttributeName":
                                    this.xmlAttr = propName.GetValue(xa)?.ToString();
                                    break;
                            }
                        }
                    }

                    if (this.xmlElem == null)
                    {
                        this.xmlElem = string.Empty;
                    }
                }
                else
                {
                    this.xmlElem = this.typeCache.xmlElem;
                }

                return this.xmlElem;
            }
        }

        /// <summary>
        /// Gets имя в XML (элемент или атрибут).
        /// </summary>
        /// <value>The name of the XML.</value>
        public string XmlName { get; } = null;

        /// <summary>
        /// Gets the member information.
        /// </summary>
        /// <value>The member information.</value>
        internal MemberInfo MemberInfo { get; }

        /// <summary>
        /// Gets делегат для получения значения свойства.
        /// </summary>
        /// <value>The getter.</value>
        private Func<object, object> Getter { get; }

        /// <summary>
        /// Установить или получить значение свойства или поля по имени.
        /// </summary>
        /// <param name="source">Объект.</param>
        /// <param name="memberName">Имя свойства или поля.</param>
        /// <returns>System.Object.</returns>
        public object this[object source, string memberName]
        {
            get => this.GetValue(source, memberName);

            set => this.SetMemberValue(source, memberName, value);
        }

        /// <summary>
        /// Получить MemberCache по MemberInfo.
        /// </summary>
        /// <param name="memberInfo">Имя свойства или поля.</param>
        /// <returns>MemberCache.</returns>
        public MemberCache this[MemberInfo memberInfo] => this.Members[memberInfo];

        /// <summary>
        /// Получить член по имени.
        /// </summary>
        /// <param name="memberName">Имя члена для поиска.</param>
        /// <returns>Найденный член или null, если не найден.</returns>
        public MemberCache this[string memberName] => this[memberName, MemberNameType.None];

        /// <summary>
        /// Получить член по имени с фильтрацией.
        /// </summary>
        /// <param name="memberName">Имя члена для поиска.</param>
        /// <param name="memberNameType">Тип имени члена для поиска.</param>
        /// <param name="memberFilter">Фильтр для отбора членов.</param>
        /// <returns>Найденный член или null, если не найден.</returns>
        public MemberCache this[string memberName, MemberNameType memberNameType = MemberNameType.None, Func<MemberCache, bool> memberFilter = null] => this.GetMember(memberName, memberNameType, memberFilter);

        /// <summary>
        /// Performs an implicit conversion from <see cref="MemberCache" /> to <see cref="ConstructorInfo" />.
        /// </summary>
        /// <param name="mc">The mc.</param>
        /// <returns>The result of the conversion.</returns>
        /// <exception cref="System.ArgumentNullException">mc.</exception>
        /// <exception cref="System.InvalidCastException">Cannot cast MemberCache of type '{mc.MemberType}' to ConstructorInfo. Member is a {mc.MemberType}.</exception>
        public static implicit operator ConstructorInfo(MemberCache mc)
        {
            var constructorInfo = mc.AsConstructorInfo();
            return constructorInfo ?? throw new InvalidCastException(
                $"Cannot cast MemberCache of type '{mc.MemberType}' to ConstructorInfo. Member is a {mc.MemberType}.");
        }

        /// <summary>
        /// Performs an implicit conversion from <see cref="MemberCache" /> to <see cref="EventInfo" />.
        /// </summary>
        /// <param name="mc">The mc.</param>
        /// <returns>The result of the conversion.</returns>
        /// <exception cref="System.ArgumentNullException">mc.</exception>
        /// <exception cref="System.InvalidCastException">Cannot cast MemberCache of type '{mc.MemberType}' to EventInfo. Member is a {mc.MemberType}.</exception>
        public static implicit operator EventInfo(MemberCache mc)
        {
            var eventInfo = mc.AsEventInfo();
            return eventInfo ?? throw new InvalidCastException(
                $"Cannot cast MemberCache of type '{mc.MemberType}' to EventInfo. Member is a {mc.MemberType}.");
        }

        /// <summary>
        /// Performs an implicit conversion from <see cref="MemberCache" /> to <see cref="FieldInfo" />.
        /// </summary>
        /// <param name="mc">The mc.</param>
        /// <returns>The result of the conversion.</returns>
        /// <exception cref="System.ArgumentNullException">mc.</exception>
        /// <exception cref="System.InvalidCastException">Cannot cast MemberCache of type '{mc.MemberType}' to FieldInfo. Member is a {mc.MemberType}.</exception>
        public static implicit operator FieldInfo(MemberCache mc)
        {
            var fieldInfo = mc.AsFieldInfo();
            return fieldInfo ?? throw new InvalidCastException(
                $"Cannot cast MemberCache of type '{mc.MemberType}' to FieldInfo. Member is a {mc.MemberType}.");
        }

        /// <summary>
        /// Performs an implicit conversion from <see cref="PropertyInfo" /> to <see cref="MemberCache" />.
        /// </summary>
        /// <param name="memberInfo">The member information.</param>
        /// <returns>The result of the conversion.</returns>
        /// <exception cref="System.ArgumentNullException">memberInfo.</exception>
        public static implicit operator MemberCache(PropertyInfo memberInfo)
        {
            return memberInfo == null ? throw new ArgumentNullException(nameof(memberInfo)) : Create(memberInfo);
        }

        /// <summary>
        /// Performs an implicit conversion from <see cref="FieldInfo" /> to <see cref="MemberCache" />.
        /// </summary>
        /// <param name="memberInfo">The member information.</param>
        /// <returns>The result of the conversion.</returns>
        /// <exception cref="System.ArgumentNullException">memberInfo.</exception>
        public static implicit operator MemberCache(FieldInfo memberInfo)
        {
            return memberInfo == null ? throw new ArgumentNullException(nameof(memberInfo)) : Create(memberInfo);
        }

        /// <summary>
        /// Performs an implicit conversion from <see cref="MethodInfo" /> to <see cref="MemberCache" />.
        /// </summary>
        /// <param name="memberInfo">The member information.</param>
        /// <returns>The result of the conversion.</returns>
        /// <exception cref="System.ArgumentNullException">memberInfo.</exception>
        public static implicit operator MemberCache(MethodInfo memberInfo)
        {
            return memberInfo == null ? throw new ArgumentNullException(nameof(memberInfo)) : Create(memberInfo);
        }

        /// <summary>
        /// Performs an implicit conversion from <see cref="EventInfo" /> to <see cref="MemberCache" />.
        /// </summary>
        /// <param name="memberInfo">The member information.</param>
        /// <returns>The result of the conversion.</returns>
        /// <exception cref="System.ArgumentNullException">memberInfo.</exception>
        public static implicit operator MemberCache(EventInfo memberInfo)
        {
            return memberInfo == null ? throw new ArgumentNullException(nameof(memberInfo)) : Create(memberInfo);
        }

        /// <summary>
        /// Performs an implicit conversion from <see cref="ConstructorInfo" /> to <see cref="MemberCache" />.
        /// </summary>
        /// <param name="memberInfo">The member information.</param>
        /// <returns>The result of the conversion.</returns>
        /// <exception cref="System.ArgumentNullException">memberInfo.</exception>
        public static implicit operator MemberCache(ConstructorInfo memberInfo)
        {
            return memberInfo == null ? throw new ArgumentNullException(nameof(memberInfo)) : Create(memberInfo);
        }

        /// <summary>
        /// Performs an implicit conversion from <see cref="Type" /> to <see cref="MemberCache" />.
        /// </summary>
        /// <param name="memberInfo">The member information.</param>
        /// <returns>The result of the conversion.</returns>
        /// <exception cref="System.ArgumentNullException">memberInfo.</exception>
        public static implicit operator MemberCache(Type memberInfo)
        {
            return memberInfo == null ? throw new ArgumentNullException(nameof(memberInfo)) : Create(memberInfo);
        }

        /// <summary>
        /// Performs an implicit conversion from <see cref="MemberCache" /> to <see cref="MethodInfo" />.
        /// </summary>
        /// <param name="mc">The mc.</param>
        /// <returns>The result of the conversion.</returns>
        /// <exception cref="System.ArgumentNullException">mc.</exception>
        /// <exception cref="System.InvalidCastException">Cannot cast MemberCache of type '{mc.MemberType}' to MethodInfo. Member is a {mc.MemberType}.</exception>
        public static implicit operator MethodInfo(MemberCache mc)
        {
            var methodInfo = mc.AsMethodInfo();
            return methodInfo ?? throw new InvalidCastException(
                $"Cannot cast MemberCache of type '{mc.MemberType}' to MethodInfo. Member is a {mc.MemberType}.");
        }

        /// <summary>
        /// Performs an implicit conversion from <see cref="MemberCache" /> to <see cref="PropertyInfo" />.
        /// </summary>
        /// <param name="mc">The mc.</param>
        /// <returns>The result of the conversion.</returns>
        /// <exception cref="System.ArgumentNullException">mc.</exception>
        /// <exception cref="System.InvalidCastException">Cannot cast MemberCache of type '{mc.MemberType}' to PropertyInfo. Member is a {mc.MemberType}.</exception>
        public static implicit operator PropertyInfo(MemberCache mc)
        {
            var propertyInfo = mc.AsPropertyInfo();
            return propertyInfo ?? throw new InvalidCastException(
                $"Cannot cast MemberCache of type '{mc.MemberType}' to PropertyInfo. Member is a {mc.MemberType}.");
        }

        /// <summary>
        /// Performs an implicit conversion from <see cref="MemberCache" /> to <see cref="Type" />.
        /// </summary>
        /// <param name="mc">The mc.</param>
        /// <returns>The result of the conversion.</returns>
        /// <exception cref="System.ArgumentNullException">mc.</exception>
        /// <exception cref="System.InvalidCastException">Cannot cast MemberCache of type '{mc.MemberType}' to Type. Member is a {mc.MemberType}.</exception>
        public static implicit operator Type(MemberCache mc)
        {
            return mc.Type;
        }

        /// <summary>
        /// Создать расширенную информацию о члене класса (с кэшированием).
        /// </summary>
        /// <param name="memberInfo">Информация о члене класса.</param>
        /// <returns>Расширенная информация о члене класса.</returns>
        public static MemberCache Create(MemberInfo memberInfo)
        {
            switch (memberInfo)
            {
                case MemberCache me:
                    return me;

                case Type t:
                    return MemberInfoCache.GetOrAdd(t, x => new MemberCache(x, true));

                default:
                    {
                        MemberCache mc = MemberInfoCache.GetOrAdd(memberInfo.DeclaringType ?? throw new InvalidOperationException(), x => new MemberCache(x, false));
                        return mc[memberInfo];
                    }
            }
        }

        /// <summary>
        /// Создаёт делегат для конструктора по умолчанию указанного типа.
        /// Используется для быстрой активации объектов без вызова Activator.New(Type).
        /// </summary>
        /// <param name="type">Тип, для которого создаётся делегат конструктора.</param>
        /// <returns>Делегат <see cref="Func{Object}" />, который создаёт экземпляр типа, или <c>null</c>, если конструктор по умолчанию
        /// отсутствует.</returns>
        public static Func<object> CreateConstructorDelegate(Type type)
        {
            if (type == null)
            {
                return null;
            }

            if (ConstructorsCache.TryGetValue(type.FullName ?? type.Name, out var ctor))
            {
                return ctor;
            }

            var constructorInfo = type.GetConstructor(Type.EmptyTypes);
            if (constructorInfo == null)
            {
                ConstructorsCache[type.FullName ?? type.Name] = null;
                return null;
            }

            ctor = type.IsGenericTypeDefinition
                ? () => Activator.CreateInstance(type)
                : Expression.Lambda<Func<object>>(Expression.Convert(Expression.New(constructorInfo), typeof(object)))
                    .Compile();
            ConstructorsCache[type.FullName ?? type.Name] = ctor;
            return ctor;
        }

        /// <summary>
        /// Создаёт экземпляр указанного типа с возможностью передачи аргументов конструктора.
        /// </summary>
        /// <param name="type">Тип, экземпляр которого нужно создать.</param>
        /// <param name="ctorArgs">Аргументы конструктора.</param>
        /// <returns>Созданный экземпляр указанного типа.</returns>
        /// <exception cref="System.NullReferenceException">Create.</exception>
        /// <exception cref="System.InvalidOperationException">Не найден конструктор для типа '{type}' с аргументами '{string.Join(",", ctorArgs.Select(arg => arg?.GetType()))}'.</exception>
        public static object New(Type type, params object[] ctorArgs)
        {
            if (ctorArgs == null)
            {
                ctorArgs = Array.Empty<object>();
            }

            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            var typeInfo = Create(type);
            if (typeInfo.DefaultConstructor != null && ctorArgs.Length == 0)
            {
                return typeInfo.DefaultConstructor();
            }

            if (typeInfo.IsDelegate)
            {
                return null;
            }

            if (type.IsInterface)
            {
                if (typeInfo.IsCollection)
                {
                    if (!InterfaceToInstanceMap.TryGetValue(type, out var lstType))
                    {
                        InterfaceToInstanceMap.TryGetValue(type.GetGenericTypeDefinition(), out lstType);
                    }

                    var genericArgs = type.GetGenericArguments();
                    if (genericArgs.Length == 0)
                    {
                        genericArgs = new[] { typeof(object) };
                    }

                    if (lstType != null && lstType.IsGenericTypeDefinition)
                    {
                        lstType = lstType.MakeGenericType(genericArgs);
                    }

                    if (lstType != null)
                    {
                        return Activator.CreateInstance(lstType);
                    }
                }

                throw new NotImplementedException();
            }

            if (type.IsArray)
            {
                if (ctorArgs.Length == 0)
                {
                    return Activator.CreateInstance(type, 0);
                }

                if (ctorArgs.Length == 1 && ctorArgs[0] is int)
                {
                    return Activator.CreateInstance(type, ctorArgs[0]);
                }

                return Activator.CreateInstance(type, ctorArgs.Length);
            }

            if (type.IsEnum)
            {
                return ctorArgs.FirstOrDefault(x => x?.GetType() == type) ?? Obj.Default(type);
            }

            if (type == typeof(string) && ctorArgs.Length == 0)
            {
                return string.Empty;
            }

            var defaultCtor = typeInfo.DefaultConstructor;
            if (defaultCtor != null && ctorArgs.Length == 0)
            {
                try
                {
                    return defaultCtor();
                }
                catch
                {
                    return Obj.Default(type);
                }
            }

            var ctor = typeInfo.GetConstructorByArgs(ref ctorArgs);

            if (ctor == null && type.IsValueType)
            {
                return Obj.Default(type);
            }

            if (ctor == null)
            {
                throw new InvalidOperationException(
                    $"Не найден конструктор для типа '{type}' с аргументами '{string.Join(",", ctorArgs.Select(arg => arg?.GetType()))}'");
            }

            return ctor.Invoke(ctorArgs);
        }

        /// <summary>
        /// Возвращает <see cref="ConstructorInfo" /> для текущего члена, если он является конструктором.
        /// </summary>
        /// <returns>Экземпляр <see cref="ConstructorInfo" />, либо <c>null</c>.</returns>
        public ConstructorInfo AsConstructorInfo() => this.MemberInfo as ConstructorInfo;

        /// <summary>
        /// Возвращает <see cref="EventInfo" /> для текущего члена, если он является событием.
        /// </summary>
        /// <returns>Экземпляр <see cref="EventInfo" />, либо <c>null</c>.</returns>
        public EventInfo AsEventInfo() => this.MemberInfo as EventInfo;

        /// <summary>
        /// Возвращает <see cref="FieldInfo" /> для текущего члена, если он является полем.
        /// </summary>
        /// <returns>Экземпляр <see cref="FieldInfo" />, либо <c>null</c>.</returns>
        public FieldInfo AsFieldInfo() => this.MemberInfo as FieldInfo;

        /// <summary>
        /// Возвращает <see cref="MethodInfo" /> для текущего члена, если он является методом.
        /// </summary>
        /// <returns>Экземпляр <see cref="MethodInfo" />, либо <c>null</c>.</returns>
        public MethodInfo AsMethodInfo() => this.MemberInfo as MethodInfo;

        /// <summary>
        /// Возвращает <see cref="PropertyInfo" /> для текущего члена, если он является свойством.
        /// </summary>
        /// <returns>Экземпляр <see cref="PropertyInfo" />, либо <c>null</c>.</returns>
        public PropertyInfo AsPropertyInfo() => this.MemberInfo as PropertyInfo;

        /// <summary>
        /// Возвращает <see cref="Type" /> для текущего члена, если он является типом.
        /// </summary>
        /// <returns>Экземпляр <see cref="Type" />, либо <c>null</c>.</returns>
        public Type AsType() => this.MemberInfo as Type;

        /// <summary>
        /// Получить атрибуты члена и его базовых типов.
        /// </summary>
        /// <returns>Массив атрибутов.</returns>
        public Attribute[] GetAttributes()
        {
            if (this.attributes != null)
            {
                return this.attributes;
            }

            this.attributes = this.MemberInfo
                .GetCustomAttributes()
                .Concat(this.BaseTypes.SelectMany(x => x.GetCustomAttributes()))
                .Distinct()
                .DistinctBy(x => x.GetType().Name)
                .ToArray();

            return this.attributes;
        }

        /// <summary>
        /// Получает коллекцию простых публичных свойств типа, которые могут использоваться как колонки ORM.
        /// </summary>
        /// <returns>Массив <see cref="MemberCache" />, представляющий свойства, подходящие для колонок базы данных.</returns>
        /// <remarks>Метод сначала ищет свойства, помеченные атрибутами <c>ColumnAttribute</c>, <c>KeyAttribute</c> или
        /// <c>ForeignKeyAttribute</c>.
        /// Если такие свойства не найдены, возвращаются все публичные простые свойства, не являющиеся коллекциями и не
        /// помеченные как <c>NotMappedAttribute</c>.
        /// Результат кэшируется в поле <c>_columns</c> для повторного использования.</remarks>
        public MemberCache[] GetColumns()
        {
            if (this.columns != null)
            {
                return this.columns;
            }

            this.columns = this.Members.Where(x =>
                    x.Value.IsProperty &&
                    x.Value.IsPublic &&
                    x.Value.IsBasic &&
                    !x.Value.IsCollection &&
                    x.Value.HasAttributeOfType("ColumnAttribute", "KeyAttribute", "ForeignKeyAttribute")
                    && !x.Value.HasAttributeOfType("NotMappedAttribute"))
                .Select(x => x.Value)
                .ToArray();

            return this.columns;
        }

        /// <summary>
        /// Получает конструктор по имени.
        /// </summary>
        /// <param name="methodName">Имя конструктора.</param>
        /// <returns>Экземпляр <see cref="ConstructorInfo" />, либо <c>null</c>.</returns>
        public ConstructorInfo GetConstructor(string methodName) => this.GetMember(methodName, MemberNameType.Name)?.AsConstructorInfo();

        /// <summary>
        /// Получает конструктор, подходящий для указанных аргументов.
        /// </summary>
        /// <param name="ctorArgs">Аргументы конструктора. Может быть изменён для добавления значений по умолчанию.</param>
        /// <returns>Экземпляр <see cref="ConstructorInfo" />, либо <c>null</c>, если подходящий конструктор не найден.</returns>
        public ConstructorInfo GetConstructorByArgs(ref object[] ctorArgs)
        {
            var args = ctorArgs;
            foreach (var c in this.GetConstructors())
            {
                var pAll = c.GetParameters();
                if (pAll.Length == ctorArgs.Length && All(ctorArgs, (_, i) =>
                        Obj.IsImplements(args[i]?.GetType(), pAll[i].ParameterType)))
                {
                    return c;
                }

                var pNoDef = c.GetParameters().Where(p => !p.HasDefaultValue).ToArray();

                if (pNoDef.Length == ctorArgs.Length && All(ctorArgs, (_, i) => Obj.IsImplements(args[i]?.GetType(), pNoDef[i].ParameterType)))
                {
                    Array.Resize(ref ctorArgs, pAll.Length);
                    for (var i = pNoDef.Length; i < pAll.Length; i++)
                    {
                        ctorArgs[i] = pAll[i].DefaultValue;
                    }

                    return c;
                }
            }

            var ctor = this.Constructors.FirstOrDefault(x => x.GetParameters().Length == args.Length);
            if (ctor != null)
            {
                var ctorParameters = ctor.GetParameters();
                ctorArgs = ctorParameters.Select((x, i) => Obj.ChangeType(args[i], x.ParameterType)).ToArray();
                return ctor;
            }

            return null;
        }

        /// <summary>
        /// Получить конструкторы типа и его базовых типов.
        /// </summary>
        /// <returns>Массив информации о конструкторах.</returns>
        public ConstructorInfo[] GetConstructors()
        {
            if (this.constructors != null)
            {
                return this.constructors;
            }

            this.constructors = this.type.GetConstructors(DefaultBindingFlags)
                .Concat(this.BaseTypes.Where(x => !x.IsInterface)
                .SelectMany(x => x.GetConstructors(DefaultBindingFlags)))
                .OrderBy(c => c.GetParameters().Length)
                .Distinct()
                .ToArray();
            return this.constructors;
        }

        /// <summary>
        /// Получает все атрибуты члена.
        /// </summary>
        /// <param name="inherit">Учитывать ли атрибуты из цепочки наследования.</param>
        /// <returns>Массив атрибутов.</returns>
        public override object[] GetCustomAttributes(bool inherit) => this.MemberInfo.GetCustomAttributes(inherit);

        /// <summary>
        /// Получает атрибуты указанного типа.
        /// </summary>
        /// <param name="attributeType">Тип атрибута.</param>
        /// <param name="inherit">Учитывать ли атрибуты из цепочки наследования.</param>
        /// <returns>Массив атрибутов указанного типа.</returns>
        public override object[] GetCustomAttributes(Type attributeType, bool inherit) => this.MemberInfo.GetCustomAttributes(attributeType, inherit);

        /// <summary>
        /// Получает событие по имени.
        /// </summary>
        /// <param name="eventName">Имя события.</param>
        /// <returns>Экземпляр <see cref="EventInfo" />, либо <c>null</c>.</returns>
        public EventInfo GetEvent(string eventName) => this.GetMember(eventName)?.AsEventInfo();

        /// <summary>
        /// Получить события типа и его базовых типов.
        /// </summary>
        /// <returns>Массив информации о событиях.</returns>
        public EventInfo[] GetEvents()
        {
            if (this.events != null)
            {
                return this.events;
            }

            this.events = this.type.GetEvents(DefaultBindingFlags)
                .Concat(this.BaseTypes.Where(x => !x.IsInterface)
                .SelectMany(x => x.GetEvents(DefaultBindingFlags)))
                .Distinct()
                .ToArray();
            return this.events;
        }

        /// <summary>
        /// Получает поле по имени.
        /// </summary>
        /// <param name="fieldName">Имя поля.</param>
        /// <returns>Экземпляр <see cref="FieldInfo" />, либо <c>null</c>.</returns>
        public FieldInfo GetField(string fieldName) => this.GetMember(fieldName)?.AsFieldInfo();

        /// <summary>
        /// Получить поля типа и его базовых типов.
        /// </summary>
        /// <returns>Массив информации о полях.</returns>
        public FieldInfo[] GetFields()
        {
            if (this.fields != null)
            {
                return this.fields;
            }

            this.fields = this.type.GetFields(DefaultBindingFlags)
                .Concat(this.BaseTypes.Where(x => !x.IsInterface)
                    .SelectMany(x => x.GetFields(DefaultBindingFlags)))
                .Distinct()
                .ToArray();

            return this.fields;
        }

        /// <summary>
        /// Gets the foreign key.
        /// </summary>
        /// <param name="children">The children.</param>
        /// <returns>MemberCache.</returns>
        public MemberCache GetForeignKey(Type children)
        {
            var childrenCache = Create(children);
            return childrenCache.ForeignKeys.FirstOrDefault(fk =>
            {
                var nav = childrenCache.GetProperty(fk.Value.ForeignKeyName);
                return nav?.PropertyType == this.Type;
            }).Value;
        }

        /// <summary>
        /// Получает коллекцию свойств, которые являются внешними ключами.
        /// </summary>
        /// <returns>Массив <see cref="MemberCache" />, представляющий внешние ключи.</returns>
        /// <remarks>Результат кэшируется в поле <c>_fks</c> для повторного использования.</remarks>
        public MemberCache[] GetForeignKeys()
        {
            if (this.fks != null)
            {
                return this.fks;
            }

            this.fks = this.Members
                .Where(x => x.Value.IsForeignKey)
                .Select(x => x.Value)
                .ToArray();

            return this.fks;
        }

        /// <summary>
        /// Получить полное имя колонки с именем схемы, таблицы и экранированием имен.
        /// </summary>
        /// <returns>System.String.</returns>
        public string GetFullColumnName() => this.GetFullColumnName("[", "]");

        /// <summary>
        /// Получить полное имя колонки с именем схемы, таблицы и экранированием имен.
        /// </summary>
        /// <param name="namePrefix">The name prefix.</param>
        /// <param name="nameSuffix">The name suffix.</param>
        /// <param name="defaultSchemaName">Default name of the schema.</param>
        /// <returns>System.String.</returns>
        public string GetFullColumnName(string namePrefix, string nameSuffix, string defaultSchemaName = null) => this.GetFullTableName(namePrefix, nameSuffix, defaultSchemaName) +
                   $".{namePrefix}{this.ColumnName}{nameSuffix}";

        /// <summary>
        /// Получает полное имя таблицы вместе именем схемы и экранированием имен.
        /// </summary>
        /// <returns>System.String.</returns>
        public string GetFullTableName() => this.GetFullTableName("[", "]");

        /// <summary>
        /// Получает полное имя таблицы вместе именем схемы и экранированием имен.
        /// </summary>
        /// <param name="namePrefix">The name prefix.</param>
        /// <param name="nameSuffix">The name suffix.</param>
        /// <param name="defaultSchemaName">Default name of the schema.</param>
        /// <returns>System.String.</returns>
        public string GetFullTableName(string namePrefix, string nameSuffix, string defaultSchemaName = null)
        {
            var schema = string.IsNullOrWhiteSpace(this.SchemaName) ? defaultSchemaName : this.SchemaName;
            var fullTableName = $"{namePrefix}{this.TableName}{nameSuffix}";
            if (!string.IsNullOrWhiteSpace(schema))
            {
                fullTableName = $"{namePrefix}{schema}{nameSuffix}." + fullTableName;
            }

            return fullTableName;
        }

        /// <summary>
        /// Получает член по имени с возможностью фильтрации.
        /// </summary>
        /// <param name="name">Имя члена.</param>
        /// <param name="memberNamesType">Тип имен по которым вести поиск.</param>
        /// <param name="membersFilter">Фильтр для отбора членов (опционально).</param>
        /// <param name="nameComparison">Сравнение имен.</param>
        /// <returns>Экземпляр <see cref="MemberCache" />, либо <c>null</c>, если член не найден.</returns>
        public MemberCache GetMember(string name, MemberNameType memberNamesType = MemberNameType.None, Func<MemberCache, bool> membersFilter = null, StringComparison nameComparison = StringComparison.OrdinalIgnoreCase)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            if (this.memberCache.TryGetValue(name, out var mx))
            {
                return mx;
            }

            if (memberNamesType == MemberNameType.None || memberNamesType.HasFlag(MemberNameType.Name))
            {
                // Быстрый поиск свойства
                var quickProp = Obj.GetLowestProperty(this.Type, name);
                if (quickProp != null)
                {
                    mx = new MemberCache(quickProp);
                    this.memberCache[name] = mx;
                    if (membersFilter == null || membersFilter(mx))
                    {
                        return mx;
                    }
                }

                // Быстрый поиск поля
                var quickField = Obj.GetLowestField(this.Type, name);
                if (quickField != null)
                {
                    mx = new MemberCache(quickField);
                    this.memberCache[name] = mx;
                    if (membersFilter == null || membersFilter(mx))
                    {
                        return mx;
                    }
                }

                // Быстрый поиск метода
                var quickMethod = Obj.GetLowestMethod(this.Type, name);
                if (quickMethod != null)
                {
                    mx = new MemberCache(quickMethod);
                    this.memberCache[name] = mx;
                    if (membersFilter == null || membersFilter(mx))
                    {
                        return mx;
                    }
                }

                // Быстрый поиск события
                var quickEvent = Obj.GetLowestEvent(this.Type, name);
                if (quickEvent != null)
                {
                    mx = new MemberCache(quickEvent);
                    this.memberCache[name] = mx;
                    if (membersFilter == null || membersFilter(mx))
                    {
                        return mx;
                    }
                }
            }

            // Поиск по различным именам (основное имя, отображаемое имя, JSON имя и т.д.)
            var searchNames = new (Func<MemberCache, string> getter, MemberNameType flag)[]
            {
                (x => x.Name, MemberNameType.Name),
                (x => x.DisplayName, MemberNameType.DisplayName),
                (x => x.JsonName, MemberNameType.JsonName),
                (x => x.XmlName, MemberNameType.XmlName),
                (x => x.ColumnName, MemberNameType.ColumnName),
                (x => x.TableName, MemberNameType.TableName),
                (x => x.SchemaName, MemberNameType.SchemaName),
            };

            foreach (var (f, flag) in searchNames)
            {
                if (memberNamesType != MemberNameType.None && (memberNamesType & flag) == 0)
                {
                    continue;
                }

                // Ищем по совпадению имени
                if (mx == null)
                {
                    mx = this.Members.FirstOrDefault(x =>
                        f(x.Value)?.Equals(name, nameComparison) == true &&
                        (membersFilter == null || membersFilter(x.Value))).Value;
                }

                // Ищем по совпадению с удалением специальных символов
                if (mx == null)
                {
                    mx = this.Members.FirstOrDefault(x =>
                        Regex.Replace($"{f(x.Value)}", "[ \\-_\\.]", string.Empty).Equals(
                            Regex.Replace(name, "[ \\-_\\.]", string.Empty),
                            nameComparison) && (membersFilter == null || membersFilter(x.Value))).Value;
                }

                if (mx != null)
                {
                    this.memberCache[name] = mx;
                    return mx;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the member value.
        /// </summary>
        /// <typeparam name="T">Type.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="memberName">Name of the member.</param>
        /// <returns>T.</returns>
        public virtual T GetMemberValue<T>(object source, string memberName) => Obj.ChangeType<T>(this.GetValue(source, memberName));

        /// <summary>
        /// Получает метод по имени.
        /// </summary>
        /// <param name="methodName">Имя метода.</param>
        /// <returns>Экземпляр <see cref="MethodInfo" />, либо <c>null</c>.</returns>
        public MethodInfo GetMethod(string methodName) => this.GetMember(methodName)?.AsMethodInfo();

        /// <summary>
        /// Получить методы типа и его базовых типов.
        /// </summary>
        /// <returns>Массив информации о методах.</returns>
        public MethodInfo[] GetMethods()
        {
            if (this.methods != null)
            {
                return this.methods;
            }

            this.methods = this.type.GetMethods(DefaultBindingFlags)
                .Concat(this.BaseTypes.Where(x => !x.IsInterface)
                    .SelectMany(x => x.GetMethods(DefaultBindingFlags)))
                .Distinct()
                .ToArray();

            return this.methods;
        }

        /// <summary>
        /// Получает коллекцию свойств, которые являются первичными ключами.
        /// </summary>
        /// <returns>Массив <see cref="MemberCache" />, представляющий первичные ключи.</returns>
        /// <remarks>Результат кэшируется в поле <c>_pks</c> для повторного использования.</remarks>
        public MemberCache[] GetPrimaryKeys()
        {
            if (this.pks != null)
            {
                return this.pks;
            }

            this.pks = this.Members
                .Where(x => x.Value.IsPrimaryKey)
                .Select(x => x.Value)
                .ToArray();

            return this.pks;
        }

        /// <summary>
        /// Получить свойства типа и его базовых типов (кроме интерфейсов).
        /// </summary>
        /// <returns>Массив информации о свойствах.</returns>
        public PropertyInfo[] GetProperties()
        {
            if (this.properties != null)
            {
                return this.properties;
            }

            var props = this.type.GetProperties(DefaultBindingFlags)
                .Concat(
                    this.BaseTypes
                        .Where(x => !x.IsInterface)
                        .SelectMany(x => x.GetProperties(DefaultBindingFlags)))
                .ToList();

            var seen = new HashSet<string>();

            this.properties = props
                .Where(p => seen.Add(p.Name))
                .ToArray();

            return this.properties;
        }

        /// <summary>
        /// Получает свойство по имени.
        /// </summary>
        /// <param name="propertyName">Имя свойства.</param>
        /// <returns>Экземпляр <see cref="PropertyInfo" />, либо <c>null</c>.</returns>
        public PropertyInfo GetProperty(string propertyName) => this.GetMember(propertyName, MemberNameType.Name)?.AsPropertyInfo();

        /// <summary>
        /// Получает коллекцию свойств, представляющих таблицы (коллекции сложных типов без атрибута NotMapped).
        /// </summary>
        /// <returns>Массив <see cref="MemberCache" /> для таблиц.</returns>
        public MemberCache[] GetTables()
        {
            if (this.tables != null)
            {
                return this.tables;
            }

            this.tables = this.Members.Where(x =>
                x.Value.IsProperty &&
                x.Value.IsPublic &&
                ((x.Value.IsCollection &&
                !x.Value.IsBasicCollection) || !x.Value.IsBasic) &&
                !x.Value.HasAttributeOfType("ColumnAttribute", "NotMappedAttribute", "Key"))
                .Select(x => x.Value)
                .ToArray();

            return this.tables;
        }

        /// <summary>
        /// Извлекает значения указанного члена из заданного объекта источника.
        /// </summary>
        /// <param name="source">Объект, из которого требуется получить значения члена. Не может быть равен null.</param>
        /// <param name="memberName">Имя члена, значения которого необходимо получить. Чувствительно к регистру.</param>
        /// <returns>Массив объектов, содержащий значения указанного члена. Возвращает null, если член с заданным именем не
        /// найден.</returns>
        /// <remarks>Если для указанного имени члена определено несколько геттеров, возвращаются значения
        /// всех соответствующих членов. Если член не найден, возвращается null.</remarks>
        public virtual object GetValue(object source, string memberName)
        {
            if (!this.getters.TryGetValue(memberName, out var memberGetters))
            {
                return null;
            }

            var values = new List<object>();
            foreach (var g in memberGetters)
            {
                values.Add(g(source));
            }

            return values.Count == 1 ? values[0] : values.ToArray();
        }

        /// <summary>
        /// Получает значение члена указанного объекта.
        /// </summary>
        /// <param name="instance">Объект, значение которого нужно получить.</param>
        /// <returns>Значение члена.</returns>
        public object GetValue(object instance) => this.Getter(instance);

        /// <summary>
        /// Получает значение члена указанного объекта и приводит его к типу <typeparamref name="T" />.
        /// </summary>
        /// <typeparam name="T">Тип, к которому нужно привести значение.</typeparam>
        /// <param name="instance">Объект, значение которого нужно получить.</param>
        /// <returns>Значение члена, приведённое к типу <typeparamref name="T" />.</returns>
        public T GetValue<T>(object instance) => Obj.ChangeType<T>(this.Getter(instance));

        /// <summary>
        /// Проверяет наличие атрибута указанного типа.
        /// </summary>
        /// <typeparam name="T">Type.</typeparam>
        /// <returns><c>true</c> if [has attribute of type]; otherwise, <c>false</c>.</returns>
        public bool HasAttributeOfType<T>() => this.HasAttributeOfType(typeof(T));

        /// <summary>
        /// Проверяет наличие атрибута любого из указанных типов.
        /// </summary>
        /// <param name="types">The types.</param>
        /// <returns><c>true</c> if [has attribute of type] [the specified types]; otherwise, <c>false</c>.</returns>
        public bool HasAttributeOfType(params Type[] types) => this.Attributes.Any(a => types.Contains(a.Value.GetType()));

        /// <summary>
        /// Проверяет наличие атрибута любого из указанных имен типа.
        /// </summary>
        /// <param name="typeNames">Имя типа.</param>
        /// <returns><c>true</c> if [has attribute of type] [the specified type names]; otherwise, <c>false</c>.</returns>
        public bool HasAttributeOfType(params string[] typeNames) => this.Attributes.Any(a => typeNames.Contains(a.Key));

        /// <summary>
        /// Проверяет, определён ли атрибут указанного типа.
        /// </summary>
        /// <param name="attributeType">Тип атрибута.</param>
        /// <param name="inherit">Учитывать ли атрибуты из цепочки наследования.</param>
        /// <returns><c>true</c>, если атрибут определён; иначе <c>false</c>.</returns>
        public override bool IsDefined(Type attributeType, bool inherit) => this.MemberInfo.IsDefined(attributeType, inherit);

        /// <summary>
        /// Устанавливает значение свойства или поля для указанного объекта по имени. Для прямого доступа используйте
        /// <see cref="Setter" />.
        /// </summary>
        /// <param name="source">Объект, для которого устанавливается значение.</param>
        /// <param name="memberName">Name of the member.</param>
        /// <param name="value">Значение, которое нужно установить.</param>
        /// <param name="valueConverter">Конвертор значения в тип свойства, если не указан, то пытаемся установить как есть/&gt;.</param>
        public virtual void SetMemberValue(object source, string memberName, object value, Func<object, object> valueConverter = null)
        {
            if (!this.setters.TryGetValue(memberName, out var memberSetters))
            {
                return;
            }

            foreach (var s in memberSetters)
            {
                s(source, valueConverter != null ? valueConverter(value) : value);
            }
        }

        /// <summary>
        /// Устанавливает значение члена для указанного объекта. Если необходимо, выполняется преобразование типа значения.
        /// </summary>
        /// <param name="source">Объект, для которого устанавливается значение.</param>
        /// <param name="value">Значение, которое нужно установить.</param>
        /// <param name="valueConverter">Конвертор значения в тип свойства, если не указан, то используется
        /// <see cref="Obj.ChangeType" />.</param>
        public virtual void SetValue(object source, object value, Func<object, object> valueConverter = null) => this.Setter(source, valueConverter == null ? Obj.ChangeType(value, this.Type) : valueConverter(value));

        /// <summary>
        /// Создать словарь из имен свойств и их значений.
        /// </summary>
        /// <typeparam name="T">Type.</typeparam>
        /// <param name="instance">The instance.</param>
        /// <param name="propertyNames">Имена свойств, если не указаны, то берутся <see cref="PublicProperties" />.</param>
        /// <returns>Dictionary&lt;System.String, System.Object&gt;.</returns>
        public Dictionary<string, object> ToDictionary<T>(T instance, params string[] propertyNames)
            where T : class
        {
            var dic = new Dictionary<string, object>();

            this.ToDictionary(instance, dic, propertyNames);

            return dic;
        }

        /// <summary>
        /// Записать в словарь значения свойств объекта.
        /// </summary>
        /// <typeparam name="T">Type.</typeparam>
        /// <param name="instance">The instance.</param>
        /// <param name="dictionary">The dictionary.</param>
        /// <param name="propertyNames">Имена свойств, если не указаны, то берутся <see cref="PublicProperties" />.</param>
        public void ToDictionary<T>(T instance, Dictionary<string, object> dictionary, params string[] propertyNames)
            where T : class
        {
            var props = propertyNames.Any()
                ? this.PublicBasicProperties.Where(x => propertyNames.Contains(x.Key)).Select(x => x.Value).ToArray()
                : this.PublicBasicProperties.Select(x => x.Value).ToArray();

            foreach (var mi in props)
            {
                dictionary[mi.Name] = mi.GetValue(instance);
            }
        }

        /// <summary>
        /// Возвращает строковое представление члена в формате "Имя (Тип)".
        /// </summary>
        /// <returns>Строка с именем и типом члена.</returns>
        public override string ToString() => $"{this.DeclaringType?.Name}{this.Name}({this.Type.Name})";

        /// <summary>
        /// Получить все члены типа (свойства, поля, события).
        /// </summary>
        /// <returns>Массив информации о членах.</returns>
        internal Dictionary<MemberInfo, MemberCache> GetChildMembersInternal()
        {
            var allMembers =
            this.GetProperties().Cast<MemberInfo>()
            .Concat(this.GetFields())
            .Concat(this.GetEvents())
            .Concat(this.GetConstructors())
            .Concat(this.GetMethods()).Distinct();

            return allMembers.ToDictionary(key => key, val => Create(val, this));
        }

        /// <summary>
        /// Alls the specified source.
        /// </summary>
        /// <typeparam name="TSource">The type of the t source.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="predicate">The predicate.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        /// <exception cref="System.NullReferenceException">source.</exception>
        /// <exception cref="System.NullReferenceException">predicate.</exception>
        private static bool All<TSource>(IEnumerable<TSource> source, Func<TSource, int, bool> predicate)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            var i = 0;
            foreach (var item in source)
            {
                if (!predicate(item, i))
                {
                    return false;
                }

                i++;
            }

            return true;
        }

        /// <summary>
        /// Creates the specified member information.
        /// </summary>
        /// <param name="memberInfo">The member information.</param>
        /// <param name="parent">The parent.</param>
        /// <returns>MemberCache.</returns>
        private static MemberCache Create(MemberInfo memberInfo, MemberCache parent)
        {
            if (memberInfo is MemberCache me)
            {
                return me;
            }

            var result = memberInfo == null
                ? null
                : MemberInfoCache.GetOrAdd(memberInfo, x => new MemberCache(x, x is Type, parent));

            return result;
        }
    }
}