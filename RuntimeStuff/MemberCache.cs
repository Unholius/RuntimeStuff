// <copyright file="MemberCache.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace RuntimeStuff
{
    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using RuntimeStuff.Helpers;

    /// <summary>
    /// Предоставляет кэшированную информацию о членах типа (класса, структуры, интерфейса) и их метаданных.
    /// Обеспечивает высокопроизводительный доступ к свойствам, полям, методам, атрибутам и другой рефлексионной информации.
    /// Поддерживает ORM-специфичные метаданные (таблицы, столбцы, ключи) и сериализационные атрибуты (JSON, XML).
    /// </summary>
    /// <remarks>
    /// Класс является оберткой над System.Reflection.MemberInfo с расширенными возможностями и кэшированием.
    /// Все экземпляры кэшируются для избежания накладных расходов на рефлексию.
    /// </remarks>
    public class MemberCache : MemberInfo
    {
        /// <summary>
        /// Статический кэш экземпляров MemberCache для типов.
        /// </summary>
        protected static readonly ConcurrentDictionary<Type, MemberCache> TypeCache = new ConcurrentDictionary<Type, MemberCache>();

        private static MemberTypes[] defaultMemberTypes = new MemberTypes[2]
        {
            MemberTypes.Property, MemberTypes.Field,
        };

        private readonly CasePriorityDictionary<Attribute> memberAttributesMap = new CasePriorityDictionary<Attribute>();
        private readonly ConcurrentDictionary<MemberInfo, MemberCache> memberCacheMap = new ConcurrentDictionary<MemberInfo, MemberCache>();
        private readonly CasePriorityDictionary<EventInfo> memberEventsMap = new CasePriorityDictionary<EventInfo>();
        private readonly CasePriorityDictionary<FieldInfo> memberFieldsMap = new CasePriorityDictionary<FieldInfo>();
        private readonly CasePriorityDictionary<MethodInfo> memberMethodsMap = new CasePriorityDictionary<MethodInfo>();
        private readonly CasePriorityDictionary<PropertyInfo> memberPropertiesMap = new CasePriorityDictionary<PropertyInfo>();
        private readonly ConcurrentDictionary<string, MemberCache> quickCache = new ConcurrentDictionary<string, MemberCache>();
        private readonly Type type;
        private readonly MemberCache typeCache;
        private Type[] baseTypes;
        private MemberCache[] columns;
        private MemberCache[] fields;
        private MemberCache[] fks;
        private string jsonName;
        private Attribute[] memberAttributes;
        private ConstructorInfo[] memberConstructors;
        private EventInfo[] memberEvents;
        private FieldInfo[] memberFields;
        private PropertyInfo[] memberProperties;
        private MethodInfo[] methods;
        private MemberCache[] pks;
        private MemberCache[] properties;
        private MemberCache[] publicBasicEnumerableProperties;
        private MemberCache[] publicBasicProperties;
        private MemberCache[] publicEnumerableProperties;
        private MemberCache[] publicFields;
        private MemberCache[] publicProperties;
        private MemberCache[] tables;

        private string xmlAttr;

        private string xmlElem;

        /// <summary>
        /// Initializes a new instance of the <see cref="MemberCache"/> class.
        /// Инициализирует новый экземпляр класса <see cref="MemberCache"/> для указанного члена типа.
        /// </summary>
        /// <param name="memberInfo">Информация о члене типа (свойство, поле, метод, тип и т.д.).</param>
        public MemberCache(MemberInfo memberInfo)
            : this(memberInfo, null)
        {
        }

        private MemberCache(MemberInfo memberInfo, MemberCache parent)
        {
            if (memberInfo == null)
            {
                throw new ArgumentNullException(nameof(memberInfo));
            }

            this.Parent = parent;
            this.typeCache = memberInfo as MemberCache;
            if (this.typeCache != null)
            {
                memberInfo = this.typeCache.MemberInfo;
            }

            this.MemberInfo = memberInfo;

            var t = this.MemberInfo as Type;
            var pi = this.MemberInfo as PropertyInfo;
            var fi = this.MemberInfo as FieldInfo;
            var mi = this.MemberInfo as MethodInfo;
            var ci = this.MemberInfo as ConstructorInfo;
            var mx = this.MemberInfo as MemberCache;
            var e = this.MemberInfo as EventInfo;

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

            this.IsDictionary = this.typeCache?.IsDictionary ?? Obj.IsDictionary(this.type);
            this.IsDelegate = this.typeCache?.IsDelegate ?? Obj.IsDelegate(this.type);
            this.IsFloat = this.typeCache?.IsFloat ?? Obj.IsFloat(this.type);
            this.IsNullable = this.typeCache?.IsNullable ?? Obj.IsNullable(this.type);
            this.IsNumeric = this.typeCache?.IsNumeric ?? Obj.IsNumeric(this.type);
            this.IsBoolean = this.typeCache?.IsBoolean ?? Obj.IsBoolean(this.type);
            this.IsBasic = this.typeCache?.IsBasic ?? Obj.IsBasic(this.type);
            this.IsEnum = this.typeCache?.IsEnum ?? type?.IsEnum ?? false;
            this.IsConst = this.typeCache?.IsConst ?? (fi != null && fi.IsLiteral && !fi.IsInitOnly);
            this.IsObject = this.typeCache?.IsObject ?? this.type == typeof(object);
            this.IsTuple = this.typeCache?.IsTuple ?? Obj.IsTuple(this.type);
            this.IsProperty = pi != null;
            this.IsEvent = e != null;
            this.IsField = fi != null;
            this.IsType = t != null;
            this.IsMethod = mi != null;
            this.IsConstructor = ci != null;
            this.IsPublic = this.typeCache?.IsPublic ?? Obj.IsPublic(this.MemberInfo);
            this.IsPrivate = this.typeCache?.IsPrivate ?? Obj.IsPrivate(this.MemberInfo);
            this.IsCollection = this.typeCache?.IsCollection ?? Obj.IsCollection(this.type);
            this.ElementType = this.IsCollection ? this.typeCache?.ElementType ?? Obj.GetCollectionItemType(this.Type) : null;
            this.IsBasicCollection = this.typeCache?.IsBasicCollection ?? (this.IsCollection && Obj.IsBasic(this.ElementType));
            this.CanWrite = pi != null ? pi.CanWrite : fi != null;
            this.CanRead = pi != null ? pi.CanRead : fi != null;
            this.Name = this.typeCache?.Name ?? this.MemberInfo.Name.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries)
                .LastOrDefault() ?? string.Empty;

            this.Description = this.typeCache?.Description ??
                               this.MemberInfo.GetCustomAttributes<DescriptionAttribute>().FirstOrDefault()?.Description;
            this.DisplayName = this.typeCache?.DisplayName ?? this.MemberInfo.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName;

            if (this.DisplayName == null)
            {
                var da = GetAttribute("DisplayAttribute");
                this.DisplayName = da?.GetType().GetProperty("Name")?.GetValue(da)?.ToString();
                this.GroupName = da?.GetType().GetProperty("GroupName")?.GetValue(da)?.ToString();
            }

            if (this.IsType && !this.IsBasic)
            {
                this.DefaultConstructor = this.typeCache?.DefaultConstructor ?? CreateConstructorDelegate(t);

                if (this.typeCache == null)
                {
                    var tblAttr = this.GetAttribute("TableAttribute");
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
            }

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
                    var keyAttr = this.GetAttribute("KeyAttribute");
                    var colAttr = this.GetAttribute("ColumnAttribute");
                    var fkAttr = this.GetAttribute("ForeignKeyAttribute");
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
        }

        /// <summary>
        /// Флаги привязки по умолчанию, используемые для поиска членов в типе.
        /// </summary>
#pragma warning disable S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields
        public static BindingFlags DefaultBindingFlags { get; } = BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic;

        /// <summary>
        /// Флаги привязки по умолчанию c IgnoreCase, используемые для поиска членов в типе.
        /// </summary>
        public static BindingFlags DefaultIgnoreCaseBindingFlags { get; } = BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.IgnoreCase;
#pragma warning restore S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields

        /// <summary>
        /// Сопоставление интерфейсов с конкретными реализациями, используемыми при создании экземпляров.
        /// </summary>
        public static Dictionary<Type, Type> InterfaceToInstanceMap { get; } = new Dictionary<Type, Type>
        {
            { typeof(IEnumerable), typeof(List<object>) },
            { typeof(IEnumerable<>), typeof(List<>) },
            { typeof(ICollection), typeof(ObservableCollection<object>) },
            { typeof(ICollection<>), typeof(ObservableCollection<>) },
            { typeof(IDictionary<,>), typeof(Dictionary<,>) },
        };

        /// <summary>
        /// Получает все базовые типы и интерфейсы для текущего типа.
        /// </summary>
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
        /// Получает значение, указывающее, можно ли читать значение члена (свойство или поле).
        /// </summary>
        public bool CanRead { get; }

        /// <summary>
        /// Получает значение, указывающее, можно ли записывать значение члена (свойство или поле).
        /// </summary>
        public bool CanWrite { get; }

        /// <summary>
        /// Получает имя столбца в базе данных, соответствующее этому члену.
        /// </summary>
        public string ColumnName { get; }

        /// <summary>
        /// Получает массив свойств, которые представляют столбцы в таблице базы данных.
        /// </summary>
        public MemberCache[] ColumnProperties
        {
            get
            {
                if (columns != null)
                    return columns;

                columns = this.typeCache?.ColumnProperties ?? this.PublicBasicProperties.Where(x =>
                        !x.IsPrimaryKey
                        && x.GetAttribute("ColumnAttribute") != null
                        && x.GetAttribute("NotMappedAttribute") == null)
                    .ToArray();

                if (columns.Length == 0)
                {
                    columns = this.PublicBasicProperties.Where(x => !x.IsPrimaryKey)
                        .ToArray();
                }

                return columns;
            }
        }

        /// <summary>
        /// Получает все конструкторы для текущего типа.
        /// </summary>
        public ConstructorInfo[] Constructors => this.GetConstructors();

        /// <summary>
        /// Получает тип, который объявляет этот член.
        /// </summary>
        public override Type DeclaringType => this.MemberInfo.DeclaringType;

        /// <summary>
        /// Получает делегат для вызова конструктора по умолчанию (без параметров) текущего типа.
        /// </summary>
        public Func<object> DefaultConstructor { get; }

        /// <summary>
        /// Получает описание члена из атрибута <see cref="DescriptionAttribute"/>.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Получает отображаемое имя члена из атрибута <see cref="DisplayNameAttribute"/> или DisplayAttribute.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Получает тип элементов коллекции, если текущий член является коллекцией.
        /// </summary>
        public Type ElementType { get; }

        /// <summary>
        /// Получает все поля (включая непубличные) текущего типа.
        /// </summary>
        public MemberCache[] Fields
        {
            get
            {
                if (this.fields != null)
                {
                    return this.fields;
                }

                fields = GetFields().Select(x => new MemberCache(x, this)).ToArray();
                memberFieldsMap.Init(memberFields, x => x.Name);
                return this.fields;
            }
        }

        /// <summary>
        /// Получает тип поля, если текущий член является полем.
        /// </summary>
        public Type FieldType { get; }

        /// <summary>
        /// Получает имя внешнего ключа в базе данных, если член помечен атрибутом ForeignKeyAttribute.
        /// </summary>
        public string ForeignKeyName { get; }

        /// <summary>
        /// Получает массив свойств, которые являются внешними ключами.
        /// </summary>
        public MemberCache[] ForeignKeys
        {
            get
            {
                if (fks != null)
                    return fks;

                fks = this.typeCache?.ForeignKeys ?? this.PublicBasicProperties
                    .Where(x => x.GetAttribute("ForeignKeyAttribute") != null)
                    .ToArray();

                return fks;
            }
        }

        /// <summary>
        /// Получает делегат для чтения значения члена.
        /// </summary>
        public Func<object, object> Getter { get; }

        /// <summary>
        /// Получает имя группы из атрибута DisplayAttribute.
        /// </summary>
        public string GroupName { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли тип базовым (примитивным, строкой, DateTime, Decimal, Guid, Enum).
        /// </summary>
        public bool IsBasic { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли член коллекцией базовых типов.
        /// </summary>
        public bool IsBasicCollection { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли тип логическим (bool).
        /// </summary>
        public bool IsBoolean { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли тип классом.
        /// </summary>
        public bool IsClass => this.Type.IsClass;

        /// <summary>
        /// Получает значение, указывающее, является ли член коллекцией.
        /// </summary>
        public bool IsCollection { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли член константой.
        /// </summary>
        public bool IsConst { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли член конструктором.
        /// </summary>
        public bool IsConstructor { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли тип делегатом.
        /// </summary>
        public bool IsDelegate { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли тип словарем.
        /// </summary>
        public bool IsDictionary { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли тип перечислением (enum).
        /// </summary>
        public bool IsEnum { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли член событием.
        /// </summary>
        public bool IsEvent { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли член полем.
        /// </summary>
        public bool IsField { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли тип числом с плавающей запятой (float, double, decimal).
        /// </summary>
        public bool IsFloat { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли член внешним ключом в базе данных.
        /// </summary>
        public bool IsForeignKey { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли геттер свойства приватным.
        /// </summary>
        public bool IsGetterPrivate { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли геттер свойства публичным.
        /// </summary>
        public bool IsGetterPublic { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли первичный ключ идентификатором (автоинкрементным числом или GUID).
        /// </summary>
        public bool IsIdentity => this.IsPrimaryKey && (Obj.IsNumeric(this.Type, false) || this.Type == typeof(Guid));

        /// <summary>
        /// Получает значение, указывающее, является ли тип интерфейсом.
        /// </summary>
        public bool IsInterface => this.Type.IsInterface;

        /// <summary>
        /// Получает значение, указывающее, является ли член методом.
        /// </summary>
        public bool IsMethod { get; set; }

        /// <summary>
        /// Получает значение, указывающее, является ли тип nullable (Nullable&lt;T&gt;).
        /// </summary>
        public bool IsNullable { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли тип числовым.
        /// </summary>
        public bool IsNumeric { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли тип System.Object.
        /// </summary>
        public bool IsObject { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли член первичным ключом в базе данных.
        /// </summary>
        public bool IsPrimaryKey { get; set; }

        /// <summary>
        /// Получает значение, указывающее, является ли член приватным.
        /// </summary>
        public bool IsPrivate { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли член свойством.
        /// </summary>
        public bool IsProperty { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли член публичным.
        /// </summary>
        public bool IsPublic { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли сеттер свойства приватным.
        /// </summary>
        public bool IsSetterPrivate { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли сеттер свойства публичным.
        /// </summary>
        public bool IsSetterPublic { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли тип кортежем (Tuple).
        /// </summary>
        public bool IsTuple { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли член типом (System.Type).
        /// </summary>
        public bool IsType { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли тип значимым типом (value type).
        /// </summary>
        public bool IsValueType => this.type.IsValueType;

        /// <summary>
        /// Получает имя для сериализации JSON из атрибутов JsonProperty, JsonPropertyName и т.д.
        /// </summary>
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
        /// Получает тип члена (свойство, метод, поле и т.д.).
        /// </summary>
        public override MemberTypes MemberType => this.MemberInfo.MemberType;

        /// <summary>
        /// Получает имя члена.
        /// </summary>
        public override sealed string Name { get; }

        /// <summary>
        /// Получает родительский MemberCache (для вложенных членов).
        /// </summary>
        public MemberCache Parent { get; private set; }

        /// <summary>
        /// Получает массив свойств, которые являются первичными ключами.
        /// </summary>
        public MemberCache[] PrimaryKeys
        {
            get
            {
                if (pks != null)
                    return pks;

                pks = this.typeCache?.PrimaryKeys ??
                      this.PublicBasicProperties.Where(x => x.GetAttribute("KeyAttribute") != null).ToArray();

                if (pks.Length == 0)
                {
                    var p =
                        this.PublicBasicProperties.FirstOrDefault(x =>
                            x.Name.Equals("id", StringComparison.OrdinalIgnoreCase)) ??
                        this.PublicBasicProperties.FirstOrDefault(x =>
                            x.Name.Equals(this.TableName + "id", StringComparison.OrdinalIgnoreCase));
                    if (p != null)
                    {
                        pks = new[] { p };
                    }
                }

                return pks;
            }
        }

        /// <summary>
        /// Получает все свойства (включая непубличные) текущего типа.
        /// </summary>
        public MemberCache[] Properties
        {
            get
            {
                if (this.properties != null)
                {
                    return this.properties;
                }

                properties = GetProperties().Select(x => new MemberCache(x, this)).ToArray();
                memberPropertiesMap.Init(memberProperties, x => x.Name);
                return this.properties;
            }
        }

        /// <summary>
        /// Получает поле, которое является backing-полем для автосвойства.
        /// </summary>
        public FieldInfo PropertyBackingField { get; }

        /// <summary>
        /// Получает тип свойства, если текущий член является свойством.
        /// </summary>
        public Type PropertyType { get; }

        /// <summary>
        /// Получает массив публичных свойств, которые являются коллекциями базовых типов.
        /// </summary>
        public MemberCache[] PublicBasicEnumerableProperties
        {
            get
            {
                if (publicBasicEnumerableProperties != null)
                    return publicBasicEnumerableProperties;

                publicBasicEnumerableProperties = PublicProperties.Where(x => x.IsBasicCollection).ToArray();
                return publicBasicEnumerableProperties;
            }
        }

        /// <summary>
        /// Получает массив публичных свойств базовых типов.
        /// </summary>
        public MemberCache[] PublicBasicProperties
        {
            get
            {
                if (this.publicBasicProperties != null)
                {
                    return this.publicBasicProperties;
                }

                this.publicBasicProperties = this.PublicProperties.Where(x => x.IsBasic).ToArray();
                return this.publicBasicProperties;
            }
        }

        /// <summary>
        /// Получает массив публичных свойств, которые являются коллекциями.
        /// </summary>
        public MemberCache[] PublicEnumerableProperties
        {
            get
            {
                if (this.publicEnumerableProperties != null)
                {
                    return this.publicEnumerableProperties;
                }

                this.publicEnumerableProperties = this.PublicProperties.Where(x => x.IsCollection).ToArray();
                return this.publicEnumerableProperties;
            }
        }

        /// <summary>
        /// Получает массив публичных полей.
        /// </summary>
        public MemberCache[] PublicFields
        {
            get
            {
                if (this.publicFields != null)
                {
                    return this.publicFields;
                }

                this.publicFields = this.Fields.Where(x => x.IsPublic).ToArray();
                return this.publicFields;
            }
        }

        /// <summary>
        /// Получает массив публичных свойств.
        /// </summary>
        public MemberCache[] PublicProperties
        {
            get
            {
                if (this.publicProperties != null)
                {
                    return this.publicProperties;
                }

                this.publicProperties = this.Properties.Where(x => x.IsPublic).ToArray();
                return this.publicProperties;
            }
        }

        /// <summary>
        /// Получает тип объекта, отраженного этим экземпляром.
        /// </summary>
        public override Type ReflectedType => this.MemberInfo.ReflectedType;

        /// <summary>
        /// Получает имя схемы базы данных для таблицы.
        /// </summary>
        public string SchemaName { get; }

        /// <summary>
        /// Получает делегат для записи значения члена.
        /// </summary>
        public Action<object, object> Setter { get; }

        /// <summary>
        /// Получает имя таблицы в базе данных.
        /// </summary>
        public string TableName { get; }

        /// <summary>
        /// Получает тип, связанный с этим членом.
        /// </summary>
        /// <remarks>
        /// Для свойства возвращает PropertyType, для поля - FieldType, для метода - ReturnType, для типа - сам тип.
        /// </remarks>
        public Type Type => this.type;

        /// <summary>
        /// Получает имя атрибута XML для сериализации из атрибутов XmlAttribute, XmlAttributeAttribute и т.д.
        /// </summary>
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
                    var xmlAttrs = this.GetAttributes().Where(x => x.GetType().Name.StartsWith("Xml")).ToArray();
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
        /// Получает имя элемента XML для сериализации из атрибутов XmlElement, XmlElementAttribute и т.д.
        /// </summary>
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
                    var xmlAttrs = this.GetAttributes().Where(x => x.GetType().Name.StartsWith("Xml")).ToArray();
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
        /// Получает имя для сериализации XML (устаревшее, используйте XmlElementName или XmlAttributeName).
        /// </summary>
        public string XmlName { get; } = null;

        /// <summary>
        /// Внутренний объект MemberInfo, который кэшируется этим экземпляром.
        /// </summary>
        internal MemberInfo MemberInfo { get; }

        /// <summary>
        /// Получает или задает значение члена по имени для указанного исходного объекта.
        /// </summary>
        /// <param name="source">Исходный объект.</param>
        /// <param name="memberName">Имя члена (свойства, поля).</param>
        /// <returns>Значение члена.</returns>
        public object this[object source, string memberName]
        {
            get => this.GetMember(memberName, MemberTypes.Property, MemberTypes.Field)?.GetValue(source);

            set => this.GetMember(memberName, MemberTypes.Property, MemberTypes.Field)?.SetValue(source, value);
        }

        /// <summary>
        /// Получает MemberCache для члена с указанным именем и типом.
        /// </summary>
        /// <param name="memberName">Имя члена.</param>
        /// <param name="memberTypes">Тип члена (по умолчанию Property).</param>
        /// <returns>Кэшированная информация о члене или null, если член не найден.</returns>
        public MemberCache this[string memberName, params MemberTypes[] memberTypes] => GetMember(memberName, memberTypes);

        /// <summary>
        /// Получает MemberCache для указанного объекта MemberInfo в контексте текущего типа.
        /// </summary>
        /// <param name="memberInfo">Информация о члене.</param>
        /// <returns>Кэшированная информация о члене.</returns>
        public MemberCache this[MemberInfo memberInfo]
        {
            get
            {
                return memberCacheMap.GetOrAdd(memberInfo, x => new MemberCache(x, this));
            }
        }

        /// <summary>
        /// Неявное преобразование MemberCache в ConstructorInfo.
        /// </summary>
        /// <param name="mc">Экземпляр MemberCache.</param>
        /// <exception cref="InvalidCastException">Выбрасывается, если MemberCache не является конструктором.</exception>
        public static implicit operator ConstructorInfo(MemberCache mc)
        {
            var constructorInfo = mc.AsConstructorInfo();
            return constructorInfo ?? throw new InvalidCastException(
                $"Cannot cast MemberCache of type '{mc.MemberType}' to ConstructorInfo. Member is a {mc.MemberType}.");
        }

        /// <summary>
        /// Неявное преобразование MemberCache в EventInfo.
        /// </summary>
        /// <param name="mc">Экземпляр MemberCache.</param>
        /// <exception cref="InvalidCastException">Выбрасывается, если MemberCache не является событием.</exception>
        public static implicit operator EventInfo(MemberCache mc)
        {
            var eventInfo = mc.AsEventInfo();
            return eventInfo ?? throw new InvalidCastException(
                $"Cannot cast MemberCache of type '{mc.MemberType}' to EventInfo. Member is a {mc.MemberType}.");
        }

        /// <summary>
        /// Неявное преобразование MemberCache в FieldInfo.
        /// </summary>
        /// <param name="mc">Экземпляр MemberCache.</param>
        /// <exception cref="InvalidCastException">Выбрасывается, если MemberCache не является полем.</exception>
        public static implicit operator FieldInfo(MemberCache mc)
        {
            var fieldInfo = mc.AsFieldInfo();
            return fieldInfo ?? throw new InvalidCastException(
                $"Cannot cast MemberCache of type '{mc.MemberType}' to FieldInfo. Member is a {mc.MemberType}.");
        }

        /// <summary>
        /// Неявное преобразование PropertyInfo в MemberCache.
        /// </summary>
        /// <param name="memberInfo">Информация о свойстве.</param>
        /// <exception cref="ArgumentNullException">Выбрасывается, если memberInfo равен null.</exception>
        public static implicit operator MemberCache(PropertyInfo memberInfo)
        {
            return memberInfo == null ? throw new ArgumentNullException(nameof(memberInfo)) : Create(memberInfo);
        }

        /// <summary>
        /// Неявное преобразование FieldInfo в MemberCache.
        /// </summary>
        /// <param name="memberInfo">Информация о поле.</param>
        /// <exception cref="ArgumentNullException">Выбрасывается, если memberInfo равен null.</exception>
        public static implicit operator MemberCache(FieldInfo memberInfo)
        {
            return memberInfo == null ? throw new ArgumentNullException(nameof(memberInfo)) : Create(memberInfo);
        }

        /// <summary>
        /// Неявное преобразование MethodInfo в MemberCache.
        /// </summary>
        /// <param name="memberInfo">Информация о методе.</param>
        /// <exception cref="ArgumentNullException">Выбрасывается, если memberInfo равен null.</exception>
        public static implicit operator MemberCache(MethodInfo memberInfo)
        {
            return memberInfo == null ? throw new ArgumentNullException(nameof(memberInfo)) : Create(memberInfo);
        }

        /// <summary>
        /// Неявное преобразование EventInfo в MemberCache.
        /// </summary>
        /// <param name="memberInfo">Информация о событии.</param>
        /// <exception cref="ArgumentNullException">Выбрасывается, если memberInfo равен null.</exception>
        public static implicit operator MemberCache(EventInfo memberInfo)
        {
            return memberInfo == null ? throw new ArgumentNullException(nameof(memberInfo)) : Create(memberInfo);
        }

        /// <summary>
        /// Неявное преобразование ConstructorInfo в MemberCache.
        /// </summary>
        /// <param name="memberInfo">Информация о конструкторе.</param>
        /// <exception cref="ArgumentNullException">Выбрасывается, если memberInfo равен null.</exception>
        public static implicit operator MemberCache(ConstructorInfo memberInfo)
        {
            return memberInfo == null ? throw new ArgumentNullException(nameof(memberInfo)) : Create(memberInfo);
        }

        /// <summary>
        /// Неявное преобразование Type в MemberCache.
        /// </summary>
        /// <param name="memberInfo">Тип.</param>
        /// <exception cref="ArgumentNullException">Выбрасывается, если memberInfo равен null.</exception>
        public static implicit operator MemberCache(Type memberInfo)
        {
            return memberInfo == null ? throw new ArgumentNullException(nameof(memberInfo)) : Create(memberInfo);
        }

        /// <summary>
        /// Неявное преобразование MemberCache в MethodInfo.
        /// </summary>
        /// <param name="mc">Экземпляр MemberCache.</param>
        /// <exception cref="InvalidCastException">Выбрасывается, если MemberCache не является методом.</exception>
        public static implicit operator MethodInfo(MemberCache mc)
        {
            var methodInfo = mc.AsMethodInfo();
            return methodInfo ?? throw new InvalidCastException(
                $"Cannot cast MemberCache of type '{mc.MemberType}' to MethodInfo. Member is a {mc.MemberType}.");
        }

        /// <summary>
        /// Неявное преобразование MemberCache в PropertyInfo.
        /// </summary>
        /// <param name="mc">Экземпляр MemberCache.</param>
        /// <exception cref="InvalidCastException">Выбрасывается, если MemberCache не является свойством.</exception>
        public static implicit operator PropertyInfo(MemberCache mc)
        {
            var propertyInfo = mc.AsPropertyInfo();
            return propertyInfo ?? throw new InvalidCastException(
                $"Cannot cast MemberCache of type '{mc.MemberType}' to PropertyInfo. Member is a {mc.MemberType}.");
        }

        /// <summary>
        /// Неявное преобразование MemberCache в Type.
        /// </summary>
        /// <param name="mc">Экземпляр MemberCache.</param>
        public static implicit operator Type(MemberCache mc)
        {
            return mc.Type;
        }

        /// <summary>
        /// Создает или получает из кэша экземпляр MemberCache для указанного MemberInfo.
        /// </summary>
        /// <param name="memberInfo">Информация о члене типа.</param>
        /// <returns>Кэшированная информация о члене.</returns>
        /// <exception cref="InvalidOperationException">Выбрасывается, если DeclaringType равен null.</exception>
        public static MemberCache Create(MemberInfo memberInfo)
        {
            switch (memberInfo)
            {
                case MemberCache me:
                    return me;

                case Type t:
                    return TypeCache.GetOrAdd(t, x => new MemberCache(x, null));

                default:
                {
                    var declaringTypeCache = TypeCache.GetOrAdd(memberInfo.DeclaringType ?? throw new InvalidOperationException(), x => new MemberCache(x, null));
                    return declaringTypeCache[memberInfo];
                }
            }
        }

        /// <summary>
        /// Создает делегат для вызова конструктора по умолчанию указанного типа.
        /// </summary>
        /// <param name="type">Тип, для которого создается делегат конструктора.</param>
        /// <returns>Делегат, создающий экземпляр типа, или null, если тип не имеет конструктора по умолчанию.</returns>
        public static Func<object> CreateConstructorDelegate(Type type)
        {
            if (type == null)
            {
                return null;
            }

            var constructorInfo = type.GetConstructor(Type.EmptyTypes);
            if (constructorInfo == null)
                return null;

            var ctor = type.IsGenericTypeDefinition
                ? () => Activator.CreateInstance(type)
                : Expression.Lambda<Func<object>>(Expression.Convert(Expression.New(constructorInfo), typeof(object)))
                    .Compile();

            return ctor;
        }

        /// <summary>
        /// Создает новый экземпляр указанного типа с использованием предоставленных аргументов конструктора.
        /// </summary>
        /// <param name="type">Тип создаваемого экземпляра.</param>
        /// <param name="ctorArgs">Аргументы конструктора.</param>
        /// <returns>Новый экземпляр типа.</returns>
        /// <exception cref="ArgumentNullException">Выбрасывается, если type равен null.</exception>
        /// <exception cref="InvalidOperationException">Выбрасывается, если не найден подходящий конструктор.</exception>
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
        /// Преобразует текущий MemberCache в ConstructorInfo.
        /// </summary>
        /// <returns>ConstructorInfo или null, если текущий член не является конструктором.</returns>
        public ConstructorInfo AsConstructorInfo() => this.MemberInfo as ConstructorInfo;

        /// <summary>
        /// Преобразует текущий MemberCache в EventInfo.
        /// </summary>
        /// <returns>EventInfo или null, если текущий член не является событием.</returns>
        public EventInfo AsEventInfo() => this.MemberInfo as EventInfo;

        /// <summary>
        /// Преобразует текущий MemberCache в FieldInfo.
        /// </summary>
        /// <returns>FieldInfo или null, если текущий член не является полем.</returns>
        public FieldInfo AsFieldInfo() => this.MemberInfo as FieldInfo;

        /// <summary>
        /// Преобразует текущий MemberCache в MethodInfo.
        /// </summary>
        /// <returns>MethodInfo или null, если текущий член не является методом.</returns>
        public MethodInfo AsMethodInfo() => this.MemberInfo as MethodInfo;

        /// <summary>
        /// Преобразует текущий MemberCache в PropertyInfo.
        /// </summary>
        /// <returns>PropertyInfo или null, если текущий член не является свойством.</returns>
        public PropertyInfo AsPropertyInfo() => this.MemberInfo as PropertyInfo;

        /// <summary>
        /// Преобразует текущий MemberCache в Type.
        /// </summary>
        /// <returns>Type или null, если текущий член не является типом.</returns>
        public Type AsType() => this.MemberInfo as Type;

        /// <summary>
        /// Получает атрибут указанного типа по имени типа атрибута.
        /// </summary>
        /// <typeparam name="TAttribute">Тип атрибута (должен быть производным от Attribute).</typeparam>
        /// <param name="attributeTypeName">Имя типа атрибута (с суффиксом Attribute или без).</param>
        /// <returns>Экземпляр атрибута или null, если атрибут не найден.</returns>
        public TAttribute GetAttribute<TAttribute>(string attributeTypeName)
            where TAttribute : Attribute
            => GetAttribute(attributeTypeName) as TAttribute;

        /// <summary>
        /// Получает атрибут по имени типа атрибута.
        /// </summary>
        /// <param name="attributeTypeName">Имя типа атрибута (с суффиксом Attribute или без).</param>
        /// <returns>Экземпляр атрибута или null, если атрибут не найден.</returns>
        public Attribute GetAttribute(string attributeTypeName)
        {
            if (string.IsNullOrWhiteSpace(attributeTypeName))
                return null;
            if (!attributeTypeName.EndsWith(nameof(Attribute)))
                attributeTypeName += nameof(Attribute);

            return memberAttributesMap.GetOrAdd(
                attributeTypeName,
                n => GetAttributes().FirstOrDefault(x => x.GetType().Name.Equals(n)) ?? GetAttributes().FirstOrDefault(x => x.GetType().Name.Equals(n, StringComparison.OrdinalIgnoreCase)),
                a => a.GetType().Name);
        }

        /// <summary>
        /// Получает все атрибуты, примененные к текущему члену, включая атрибуты базовых типов.
        /// </summary>
        /// <returns>Массив атрибутов.</returns>
        public Attribute[] GetAttributes()
        {
            if (this.memberAttributes != null)
            {
                return this.memberAttributes;
            }

            this.memberAttributes = this.MemberInfo
                .GetCustomAttributes()
                .Concat(this.BaseTypes.SelectMany(x => x.GetCustomAttributes()))
                .Distinct()
                .ToArray();

            memberAttributesMap.Init(memberAttributes, x => x.GetType().Name);

            return this.memberAttributes;
        }

        /// <summary>
        /// Получает массив членов, которые представляют столбцы в базе данных.
        /// </summary>
        /// <returns>Массив MemberCache, представляющих столбцы.</returns>
        public MemberCache[] GetColumns()
        {
            if (this.columns != null)
            {
                return this.columns;
            }

            this.columns = this.GetProperties()
                .Select(pi => this[pi])
                .Where(x => x.IsProperty &&
                            x.IsPublic &&
                            x.IsBasic &&
                            !x.IsCollection &&
                            x.HasAnyAttributeOfType("ColumnAttribute", "KeyAttribute", "ForeignKeyAttribute")
                            && !x.HasAnyAttributeOfType("NotMappedAttribute"))
                .ToArray();

            return this.columns;
        }

        /// <summary>
        /// Находит конструктор, соответствующий предоставленным аргументам.
        /// </summary>
        /// <param name="ctorArgs">Аргументы конструктора (могут быть изменены при наличии параметров со значениями по умолчанию).</param>
        /// <returns>ConstructorInfo или null, если подходящий конструктор не найден.</returns>
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
        /// Получает все конструкторы текущего типа.
        /// </summary>
        /// <returns>Массив конструкторов.</returns>
        public ConstructorInfo[] GetConstructors()
        {
            if (this.memberConstructors != null)
            {
                return this.memberConstructors;
            }

            this.memberConstructors = this.type.GetConstructors(DefaultBindingFlags)
                .Concat(this.BaseTypes.Where(x => !x.IsInterface)
                    .SelectMany(x => x.GetConstructors(DefaultBindingFlags)))
                .OrderBy(c => c.GetParameters().Length)
                .Distinct()
                .ToArray();
            return this.memberConstructors;
        }

        /// <summary>
        /// Возвращает массив всех настраиваемых атрибутов, примененных к этому члену.
        /// </summary>
        /// <param name="inherit">true для поиска цепочки наследования этого члена для поиска атрибутов; в противном случае — false.</param>
        /// <returns>Массив настраиваемых атрибутов.</returns>
        public override object[] GetCustomAttributes(bool inherit) => this.MemberInfo.GetCustomAttributes(inherit);

        /// <summary>
        /// Возвращает массив настраиваемых атрибутов, примененных к этому члену и идентифицируемых типом <see cref="Type"/>.
        /// </summary>
        /// <param name="attributeType">Тип атрибута для поиска.</param>
        /// <param name="inherit">true для поиска цепочки наследования этого члена для поиска атрибутов; в противном случае — false.</param>
        /// <returns>Массив настраиваемых атрибутов.</returns>
        public override object[] GetCustomAttributes(Type attributeType, bool inherit) => this.MemberInfo.GetCustomAttributes(attributeType, inherit);

        /// <summary>
        /// Получает все события текущего типа.
        /// </summary>
        /// <returns>Массив событий.</returns>
        public EventInfo[] GetEvents()
        {
            if (this.memberEvents != null)
            {
                return this.memberEvents;
            }

            this.memberEvents = this.type.GetEvents(DefaultBindingFlags)
                .Concat(this.BaseTypes.Where(x => !x.IsInterface)
                    .SelectMany(x => x.GetEvents(DefaultBindingFlags)))
                .Distinct()
                .ToArray();
            return this.memberEvents;
        }

        /// <summary>
        /// Получает MemberCache для поля с указанным именем.
        /// </summary>
        /// <param name="fieldName">Имя поля.</param>
        /// <param name="ignoreCase">true для игнорирования регистра при поиске; в противном случае — false.</param>
        /// <returns>MemberCache для поля или null, если поле не найдено.</returns>
        public MemberCache GetField(string fieldName, bool ignoreCase = true) => GetMember(fieldName, MemberTypes.Field);

        /// <summary>
        /// Получает все поля текущего типа.
        /// </summary>
        /// <returns>Массив полей.</returns>
        public FieldInfo[] GetFields()
        {
            if (this.memberFields != null)
            {
                return this.memberFields;
            }

            this.memberFields = this.type.GetFields(DefaultBindingFlags)
                .Concat(this.BaseTypes.Where(x => !x.IsInterface)
                    .SelectMany(x => x.GetFields(DefaultBindingFlags)))
                .Distinct()
                .ToArray();

            this.memberFieldsMap.Init(memberFields, x => x.Name);

            return this.memberFields;
        }

        /// <summary>
        /// Находит внешний ключ, который ссылается на текущий тип из указанного типа-потомка.
        /// </summary>
        /// <param name="children">Тип-потомок, содержащий внешний ключ.</param>
        /// <returns>MemberCache внешнего ключа или null, если не найден.</returns>
        public MemberCache GetForeignKey(Type children)
        {
            var childrenCache = Create(children);
            return childrenCache.ForeignKeys.FirstOrDefault(fk =>
            {
                var nav = childrenCache.GetProperty(fk.ForeignKeyName);
                return nav?.PropertyType == this.Type;
            });
        }

        /// <summary>
        /// Получает все внешние ключи текущего типа.
        /// </summary>
        /// <returns>Массив внешних ключей.</returns>
        public MemberCache[] GetForeignKeys()
        {
            if (this.fks != null)
            {
                return this.fks;
            }

            this.fks = this.GetColumns()
                .Where(x => x.IsForeignKey)
                .ToArray();

            return this.fks;
        }

        /// <summary>
        /// Получает полное имя столбца в формате [Схема].[Таблица].[Столбец] с квадратными скобками.
        /// </summary>
        /// <returns>Полное имя столбца.</returns>
        public string GetFullColumnName() => this.GetFullColumnName("[", "]");

        /// <summary>
        /// Получает полное имя столбца с указанными префиксом и суффиксом для имен.
        /// </summary>
        /// <param name="namePrefix">Префикс для имен (например, "[").</param>
        /// <param name="nameSuffix">Суффикс для имен (например, "]").</param>
        /// <param name="defaultSchemaName">Имя схемы по умолчанию, если SchemaName не задан.</param>
        /// <returns>Полное имя столбца.</returns>
        public string GetFullColumnName(string namePrefix, string nameSuffix, string defaultSchemaName = null) => this.GetFullTableName(namePrefix, nameSuffix, defaultSchemaName) +
                                                                                                                  $".{namePrefix}{this.ColumnName}{nameSuffix}";

        /// <summary>
        /// Получает полное имя таблицы в формате [Схема].[Таблица] с квадратными скобками.
        /// </summary>
        /// <returns>Полное имя таблицы.</returns>
        public string GetFullTableName() => this.GetFullTableName("[", "]");

        /// <summary>
        /// Получает полное имя таблицы с указанными префиксом и суффиксом для имен.
        /// </summary>
        /// <param name="namePrefix">Префикс для имен (например, "[").</param>
        /// <param name="nameSuffix">Суффикс для имен (например, "]").</param>
        /// <param name="defaultSchemaName">Имя схемы по умолчанию, если SchemaName не задан.</param>
        /// <returns>Полное имя таблицы.</returns>
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
        /// Получает информацию о члене указанного типа по имени.
        /// </summary>
        /// <typeparam name="TMember">Тип члена (PropertyInfo, FieldInfo, MethodInfo, EventInfo).</typeparam>
        /// <param name="memberName">Имя члена.</param>
        /// <param name="memberTypes">Тип члена.</param>
        /// <returns>Информация о члене или null, если член не найден.</returns>
        public TMember GetMember<TMember>(string memberName, params MemberTypes[] memberTypes)
            where TMember : MemberInfo
        {
            return (TMember)GetMember(memberName, memberTypes).MemberInfo;
        }

        /// <summary>
        /// Получает MemberCache для члена с указанным именем и типом.
        /// </summary>
        /// <param name="memberName">Имя члена.</param>
        /// <param name="memberTypes">Тип члена.</param>
        /// <returns>MemberCache для члена или null, если член не найден.</returns>
        /// <exception cref="NotSupportedException">Выбрасывается для неподдерживаемых типов членов.</exception>
        public MemberCache GetMember(string memberName, params MemberTypes[] memberTypes)
        {
            if (quickCache.TryGetValue(memberName, out var mc))
                return mc;
            if (memberTypes == null || memberTypes.Length == 0)
            {
                memberTypes = defaultMemberTypes;
            }

            foreach (var mt in memberTypes)
            {
                switch (mt)
                {
                    case MemberTypes.Property:
                        var propInfo = memberPropertiesMap.GetOrAdd(memberName, x => type.GetProperty(x, DefaultBindingFlags) ?? type.GetProperty(x, DefaultIgnoreCaseBindingFlags), p => p.Name);
                        if (propInfo != null)
                        {
                            var propCache = memberCacheMap.GetOrAdd(propInfo, x => new MemberCache(x, this));
                            quickCache[memberName] = propCache;
                            return propCache;
                        }

                        break;

                    case MemberTypes.Field:
                        var fieldInfo = memberFieldsMap.GetOrAdd(memberName, x => type.GetField(x, DefaultBindingFlags) ?? type.GetField(x, DefaultIgnoreCaseBindingFlags), f => f.Name);
                        if (fieldInfo != null)
                        {
                            var fieldCache = memberCacheMap.GetOrAdd(fieldInfo, x => new MemberCache(x, this));
                            quickCache[memberName] = fieldCache;
                            return fieldCache;
                        }

                        break;

                    case MemberTypes.Method:
                        var methodInfo = memberMethodsMap.GetOrAdd(memberName, x => type.GetMethod(x, DefaultBindingFlags) ?? type.GetMethod(x, DefaultIgnoreCaseBindingFlags), m => m.Name);
                        if (methodInfo != null)
                        {
                            var methodCache = memberCacheMap.GetOrAdd(methodInfo, x => new MemberCache(x, this));
                            quickCache[memberName] = methodCache;
                            return methodCache;
                        }

                        break;

                    case MemberTypes.Event:
                        var eventInfo = memberEventsMap.GetOrAdd(memberName, x => type.GetEvent(x, DefaultBindingFlags) ?? type.GetEvent(x, DefaultBindingFlags), e => e.Name);
                        if (eventInfo != null)
                        {
                            var eventCache = memberCacheMap.GetOrAdd(eventInfo, x => new MemberCache(x, this));
                            quickCache[memberName] = eventCache;
                            return eventCache;
                        }

                        break;

                    case MemberTypes.All:
                        return GetMember(memberName, MemberTypes.Property) ??
                               GetMember(memberName, MemberTypes.Field) ??
                               GetMember(memberName, MemberTypes.Method) ??
                               GetMember(memberName, MemberTypes.Event);

                    case MemberTypes.Constructor:
                    case MemberTypes.Custom:
                    case MemberTypes.NestedType:
                    case MemberTypes.TypeInfo:
                    default:
                        throw new NotSupportedException(nameof(mt));
                }
            }

            return null;
        }

        /// <summary>
        /// Получает все методы текущего типа.
        /// </summary>
        /// <returns>Массив методов.</returns>
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
        /// Получает все первичные ключи текущего типа.
        /// </summary>
        /// <returns>Массив первичных ключей.</returns>
        public MemberCache[] GetPrimaryKeys()
        {
            if (this.pks != null)
            {
                return this.pks;
            }

            this.pks = this.GetColumns()
                .Where(x => x.IsPrimaryKey)
                .ToArray();

            return this.pks;
        }

        /// <summary>
        /// Получает все свойства текущего типа.
        /// </summary>
        /// <returns>Массив свойств.</returns>
        public PropertyInfo[] GetProperties()
        {
            if (this.memberProperties != null)
            {
                return this.memberProperties;
            }

            var props = this.type.GetProperties(DefaultBindingFlags)
                .Concat(
                    this.BaseTypes
                        .Where(x => !x.IsInterface)
                        .SelectMany(x => x.GetProperties(DefaultBindingFlags)))
                .ToList();

            var seen = new HashSet<string>();

            this.memberProperties = props
                .Where(p => seen.Add(p.Name))
                .ToArray();

            return this.memberProperties;
        }

        /// <summary>
        /// Получает PropertyInfo для свойства с указанным именем.
        /// </summary>
        /// <param name="propertyName">Имя свойства.</param>
        /// <returns>PropertyInfo или null, если свойство не найдено.</returns>
        public PropertyInfo GetProperty(string propertyName) => this[propertyName]?.AsPropertyInfo();

        /// <summary>
        /// Получает все навигационные свойства (таблицы) текущего типа.
        /// </summary>
        /// <returns>Массив навигационных свойств.</returns>
        public MemberCache[] GetTables()
        {
            if (this.tables != null)
            {
                return this.tables;
            }

            this.tables = this.Properties.Where(x =>
                x.IsProperty &&
                x.IsPublic &&
                ((x.IsCollection &&
                !x.IsBasicCollection) || !x.IsBasic) &&
                !x.HasAnyAttributeOfType("ColumnAttribute", "NotMappedAttribute", "Key"))
                .ToArray();

            return this.tables;
        }

        /// <summary>
        /// Получает значение члена для указанного экземпляра.
        /// </summary>
        /// <param name="instance">Экземпляр объекта.</param>
        /// <returns>Значение члена.</returns>
        public object GetValue(object instance) => this.Getter(instance);

        /// <summary>
        /// Получает значение члена для указанного экземпляра и преобразует его к указанному типу.
        /// </summary>
        /// <typeparam name="T">Тип, к которому преобразуется значение.</typeparam>
        /// <param name="instance">Экземпляр объекта.</param>
        /// <returns>Значение члена, преобразованное к типу T.</returns>
        public T GetValue<T>(object instance) => Obj.ChangeType<T>(this.Getter(instance));

        /// <summary>
        /// Проверяет, содержит ли член все указанные атрибуты.
        /// </summary>
        /// <param name="attributeTypeNames">Имена типов атрибутов.</param>
        /// <returns>true, если член содержит все указанные атрибуты; в противном случае — false.</returns>
        public bool HasAllAttributeOfType(params string[] attributeTypeNames)
        {
            return attributeTypeNames.All(x => GetAttribute(x) != null);
        }

        /// <summary>
        /// Проверяет, содержит ли член любой из указанных атрибутов.
        /// </summary>
        /// <param name="attributeTypeNames">Имена типов атрибутов.</param>
        /// <returns>true, если член содержит хотя бы один из указанных атрибутов; в противном случае — false.</returns>
        public bool HasAnyAttributeOfType(params string[] attributeTypeNames)
        {
            return attributeTypeNames.Any(x => GetAttribute(x) != null);
        }

        /// <summary>
        /// Определяет, применен ли к этому члену один или несколько атрибутов, идентифицируемых типом <see cref="Type"/>.
        /// </summary>
        /// <param name="attributeType">Тип атрибута для поиска.</param>
        /// <param name="inherit">true для поиска цепочки наследования этого члена для поиска атрибутов; в противном случае — false.</param>
        /// <returns>true, если к этому члену применен один или несколько экземпляров атрибута; в противном случае — false.</returns>
        public override bool IsDefined(Type attributeType, bool inherit) => this.MemberInfo.IsDefined(attributeType, inherit);

        /// <summary>
        /// Устанавливает значение члена для указанного экземпляра.
        /// </summary>
        /// <param name="source">Экземпляр объекта.</param>
        /// <param name="value">Значение для установки.</param>
        /// <param name="valueConverter">Конвертер значения (необязательный).</param>
        public virtual void SetValue(object source, object value, Func<object, object> valueConverter = null) => this.Setter(source, valueConverter == null ? Obj.ChangeType(value, this.Type) : valueConverter(value));

        /// <summary>
        /// Преобразует экземпляр объекта в словарь имен и значений свойств.
        /// </summary>
        /// <typeparam name="T">Тип объекта.</typeparam>
        /// <param name="instance">Экземпляр объекта.</param>
        /// <param name="propertyNames">Имена свойств для включения (если не указаны, включаются все публичные базовые свойства).</param>
        /// <returns>Словарь имен и значений свойств.</returns>
        public Dictionary<string, object> ToDictionary<T>(T instance, params string[] propertyNames)
            where T : class
        {
            var dic = new Dictionary<string, object>();

            this.ToDictionary(instance, dic, propertyNames);

            return dic;
        }

        /// <summary>
        /// Преобразует экземпляр объекта в словарь имен и значений свойств и добавляет их в указанный словарь.
        /// </summary>
        /// <typeparam name="T">Тип объекта.</typeparam>
        /// <param name="instance">Экземпляр объекта.</param>
        /// <param name="dictionary">Словарь, в который добавляются пары имя-значение.</param>
        /// <param name="propertyNames">Имена свойств для включения (если не указаны, включаются все публичные базовые свойства).</param>
        public void ToDictionary<T>(T instance, Dictionary<string, object> dictionary, params string[] propertyNames)
            where T : class
        {
            var props = propertyNames.Any()
                ? this.PublicBasicProperties.Where(x => propertyNames.Contains(x.Name)).ToArray()
                : this.PublicBasicProperties;

            foreach (var mi in props)
            {
                dictionary[mi.Name] = mi.GetValue(instance);
            }
        }

        /// <summary>
        /// Возвращает строковое представление текущего члена в формате "DeclaringType.Name.Name(Type.Name)".
        /// </summary>
        /// <returns>Строковое представление члена.</returns>
        public override string ToString() => $"{this.DeclaringType?.Name}{this.Name}({this.Type.Name})";

        /// <summary>
        /// Проверяет, удовлетворяют ли все элементы последовательности условию.
        /// </summary>
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
        /// Словарь с приоритетом регистра для эффективного поиска значений по ключу-строке.
        /// </summary>
        /// <typeparam name="TValue">Тип значения.</typeparam>
        private sealed class CasePriorityDictionary<TValue>
            where TValue : class
        {
            private readonly Dictionary<string, TValue> exactMap =
                new Dictionary<string, TValue>(StringComparer.Ordinal);

            private readonly Dictionary<string, TValue> ignoreCaseMap =
                new Dictionary<string, TValue>(StringComparer.OrdinalIgnoreCase);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public TValue GetOrAdd(string key, Func<string, TValue> valueFactory, Func<TValue, string> keySelector, bool ignoreCase = true)
            {
                if (key == null)
                    return null;
                if (valueFactory == null)
                    throw new ArgumentNullException(nameof(valueFactory));

                if (this.exactMap.TryGetValue(key, out var value))
                    return value;

                if (ignoreCase && this.ignoreCaseMap.TryGetValue(key, out value))
                    return value;

                value = valueFactory(key);

                if (value != null)
                {
                    this.exactMap[keySelector(value)] = value;
                    this.ignoreCaseMap[key] = value;
                }
                else
                {
                    this.exactMap[key] = null;
                    this.ignoreCaseMap[key] = null;
                }

                return value;
            }

            internal void Init(IEnumerable<TValue> source, Func<TValue, string> keySelector)
            {
                if (source is null)
                {
                    throw new ArgumentNullException(nameof(source));
                }

                if (keySelector is null)
                {
                    throw new ArgumentNullException(nameof(keySelector));
                }

                foreach (var item in source)
                {
                    if (item is null)
                        continue;

                    var key = keySelector(item);
                    if (key == null)
                        continue;

                    if (!this.exactMap.ContainsKey(key))
                        this.exactMap[key] = item;

                    if (!this.ignoreCaseMap.ContainsKey(key))
                        this.ignoreCaseMap[key] = item;
                }
            }
        }
    }
}