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

        private static readonly MemberTypes[] DefaultMemberTypes =
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
        private MethodInfo[] memberMethods;
        private MemberCache[] pks;
        private MemberCache[] properties;
        private MemberCache[] publicBasicEnumerableProperties;
        private MemberCache[] publicBasicProperties;
        private MemberCache[] publicEnumerableProperties;
        private MemberCache[] publicFields;
        private MemberCache[] publicProperties;
        private MemberCache[] tables;
        private FieldInfo propertyBackingField;
        private bool? propertyBackingFieldExists;

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

            Parent = parent;
            typeCache = memberInfo as MemberCache;
            if (typeCache != null)
            {
                memberInfo = typeCache.MemberInfo;
            }

            MemberInfo = memberInfo;

            var t = MemberInfo as Type;
            var pi = MemberInfo as PropertyInfo;
            var fi = MemberInfo as FieldInfo;
            var mi = MemberInfo as MethodInfo;
            var ci = MemberInfo as ConstructorInfo;
            var mx = MemberInfo as MemberCache;
            var e = MemberInfo as EventInfo;

            if (t != null)
            {
                type = t;
            }

            if (pi != null)
            {
                type = pi.PropertyType;
            }

            if (fi != null)
            {
                type = fi.FieldType;
            }

            if (mi != null)
            {
                type = mi.ReturnType;
            }

            if (ci != null)
            {
                type = ci.DeclaringType;
            }

            if (mx != null)
            {
                type = mx.Type;
            }

            if (e != null)
            {
                type = e.EventHandlerType;
            }

            IsDictionary = typeCache?.IsDictionary ?? Obj.IsDictionary(type);
            IsDelegate = typeCache?.IsDelegate ?? Obj.IsDelegate(type);
            IsFloat = typeCache?.IsFloat ?? Obj.IsFloat(type);
            IsNullable = typeCache?.IsNullable ?? Obj.IsNullable(type);
            IsNumeric = typeCache?.IsNumeric ?? Obj.IsNumeric(type);
            IsBoolean = typeCache?.IsBoolean ?? Obj.IsBoolean(type);
            IsBasic = typeCache?.IsBasic ?? Obj.IsBasic(type);
            IsEnum = typeCache?.IsEnum ?? type?.IsEnum ?? false;
            IsConst = typeCache?.IsConst ?? (fi != null && fi.IsLiteral && !fi.IsInitOnly);
            IsObject = typeCache?.IsObject ?? type == typeof(object);
            IsTuple = typeCache?.IsTuple ?? Obj.IsTuple(type);
            IsProperty = pi != null;
            IsEvent = e != null;
            IsField = fi != null;
            IsType = t != null;
            IsMethod = mi != null;
            IsConstructor = ci != null;
            IsPublic = typeCache?.IsPublic ?? Obj.IsPublic(MemberInfo);
            IsPrivate = typeCache?.IsPrivate ?? Obj.IsPrivate(MemberInfo);
            IsCollection = typeCache?.IsCollection ?? Obj.IsCollection(type);
            ElementType = IsCollection ? typeCache?.ElementType ?? Obj.GetCollectionItemType(Type) : null;
            IsBasicCollection = typeCache?.IsBasicCollection ?? (IsCollection && Obj.IsBasic(ElementType));
            CanWrite = pi != null ? pi.CanWrite : fi != null;
            CanRead = pi != null ? pi.CanRead : fi != null;
            Name = typeCache?.Name ?? MemberInfo.Name.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries)
                .LastOrDefault() ?? string.Empty;

            Description = typeCache?.Description ??
                               MemberInfo.GetCustomAttributes<DescriptionAttribute>().FirstOrDefault()?.Description;
            DisplayName = typeCache?.DisplayName ?? MemberInfo.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName;

            var da = GetAttribute("DisplayAttribute");
            if (da != null)
            {
                if (string.IsNullOrEmpty(DisplayName))
                    DisplayName = da?.GetType().GetMethod("GetName")?.Invoke(da, null)?.ToString();
                if (string.IsNullOrEmpty(Description))
                    Description = da?.GetType().GetMethod("GetDescription")?.Invoke(da, null)?.ToString();
                GroupName = da?.GetType().GetMethod("GetGroupName")?.Invoke(da, null)?.ToString();
                ShortName = da?.GetType().GetMethod("GetShortName")?.Invoke(da, null)?.ToString();
                Prompt = da?.GetType().GetProperty("GetPrompt")?.GetValue(da)?.ToString();
                Order = (int?)da?.GetType().GetMethod("GetOrder")?.Invoke(da, null);
                AutoGenerateFilter = (bool?)da?.GetType().GetMethod("GetAutoGenerateFilter")?.Invoke(da, null);
                AutoGenerateField = (bool?)da?.GetType().GetMethod("GetAutoGenerateField")?.Invoke(da, null);
            }

            if (IsType)
            {
                if (IsBasic) return;
                DefaultConstructor = typeCache?.DefaultConstructor ?? CreateConstructorDelegate(t);

                if (typeCache == null)
                {
                    var tblAttr = GetAttribute("TableAttribute");
                    if (tblAttr != null)
                    {
                        var tblNameProperty = tblAttr.GetType().GetProperty("Name");
                        var tblSchemaProperty = tblAttr.GetType().GetProperty("Schema");
                        TableName = tblNameProperty?.GetValue(tblAttr)?.ToString();
                        SchemaName = tblSchemaProperty?.GetValue(tblAttr)?.ToString();
                    }
                    else
                    {
                        TableName = Name;
                    }
                }
                else
                {
                    TableName = typeCache.TableName;
                    SchemaName = typeCache.SchemaName;
                }
            }
            else
            {
                if (pi != null)
                {
                    PropertyType = pi.PropertyType;
                    IsSetterPublic = pi.GetSetMethod()?.IsPublic == true;
                    IsSetterPrivate = pi.GetSetMethod() == null || pi.GetSetMethod()?.IsPrivate == true;
                    IsGetterPublic = pi.GetGetMethod()?.IsPublic == true;
                    IsGetterPrivate = pi.GetGetMethod() == null || pi.GetGetMethod()?.IsPrivate == true;
                    TableName = Parent.TableName;
                    SchemaName = Parent.SchemaName;

                    if (typeCache == null)
                    {
                        var keyAttr = GetAttribute("KeyAttribute");
                        var colAttr = GetAttribute("ColumnAttribute");
                        var fkAttr = GetAttribute("ForeignKeyAttribute");
                        IsPrimaryKey = keyAttr != null || string.Equals(Name, "id", StringComparison.OrdinalIgnoreCase);
                        IsForeignKey = fkAttr != null;
                        IsColumn = HasAnyAttributeOfType("ColumnAttribute", "KeyAttribute") || (IsBasic && HasAnyAttributeOfType("ForeignKeyAttribute"));

                        try
                        {
                            Setter = Obj.GetMemberSetter(pi);

                            if (Setter == null && PropertyBackingField != null)
                            {
                                Setter = Obj.GetMemberSetter(PropertyBackingField);
                            }
                        }
                        catch (Exception)
                        {
                            Setter = (o, v) => pi.SetValue(o, v);
                        }

                        try
                        {
                            Getter = Obj.GetMemberGetter(pi);
                        }
                        catch (Exception)
                        {
                            Getter = o => pi.GetValue(o);
                        }

                        TableName = Parent.TableName;
                        ColumnName = colAttr != null
                            ? colAttr.GetType().GetProperty("Name")?.GetValue(colAttr)?.ToString() ?? Name
                            : Name;

                        ForeignKeyName = fkAttr?.GetType().GetProperty("Name")?.GetValue(fkAttr)?.ToString() ??
                                              string.Empty;
                    }
                    else
                    {
                        Setter = typeCache.Setter;
                        Getter = typeCache.Getter;
                        ColumnName = typeCache.ColumnName;
                        ForeignKeyName = typeCache.ForeignKeyName;
                        IsPrimaryKey = typeCache.IsPrimaryKey;
                        IsForeignKey = typeCache.IsForeignKey;
                    }
                }
                else
                {
                    if (fi == null) return;
                    IsSetterPublic = true;
                    IsSetterPrivate = false;
                    IsGetterPublic = true;
                    IsGetterPrivate = false;
                    FieldType = fi.FieldType;
                    try
                    {
                        Setter = typeCache?.Setter ?? Obj.GetMemberSetter(fi);
                    }
                    catch
                    {
                        Setter = (obj, value) => fi.SetValue(obj, value);
                    }

                    try
                    {
                        Getter = typeCache?.Getter ?? Obj.GetMemberGetter(fi);
                    }
                    catch (Exception)
                    {
                        Getter = x => fi.GetValue(x);
                    }
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
        /// Количество закешированных членов типа (базовые типы, свойства, поля, методы, события, атрибуты, конструкторы).
        /// </summary>
        public int CachedMembersCount =>
            (baseTypes?.Length ?? 0) +
            (properties?.Length ?? 0) +
            (fields?.Length ?? 0) +
            (memberMethods?.Length ?? 0) +
            (memberEvents?.Length ?? 0) +
            (memberAttributes?.Length ?? 0) +
            (memberConstructors?.Length ?? 0);

        /// <summary>
        /// Получает все базовые типы и интерфейсы для текущего типа.
        /// </summary>
        public Type[] BaseTypes
        {
            get
            {
                if (baseTypes != null)
                {
                    return baseTypes;
                }

                baseTypes = Obj.GetBaseTypes(type, getInterfaces: true);
                return baseTypes;
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

                columns = typeCache?.ColumnProperties ?? PublicBasicProperties.Where(x =>
                        !x.IsPrimaryKey
                        && x.IsColumn
                        && x.GetAttribute("NotMappedAttribute") == null)
                    .ToArray();

                if (columns.Length == 0)
                {
                    columns = PublicBasicProperties.Where(x => !x.IsPrimaryKey)
                        .ToArray();
                }

                return columns;
            }
        }

        /// <summary>
        /// Получает все конструкторы для текущего типа.
        /// </summary>
        public ConstructorInfo[] Constructors => GetConstructors();

        /// <summary>
        /// Получает тип, который объявляет этот член.
        /// </summary>
        public override Type DeclaringType => MemberInfo.DeclaringType;

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
        /// Получает значение свойства ShortName из атрибута DisplayAttribute.
        /// </summary>
        public string ShortName { get; }

        /// <summary>
        /// Получает значение свойства Prompt из атрибута DisplayAttribute.
        /// </summary>
        public string Prompt { get; }

        /// <summary>
        /// Получает значение свойства AutoGenerateField из атрибута DisplayAttribute.
        /// </summary>
        public bool? AutoGenerateField { get; }

        /// <summary>
        /// Получает значение свойства AutoGenerateFilter из атрибута DisplayAttribute.
        /// </summary>
        public bool? AutoGenerateFilter { get; }

        /// <summary>
        /// Получает значение свойства Order из атрибута DisplayAttribute.
        /// </summary>
        public int? Order { get; }

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
                if (fields != null)
                {
                    return fields;
                }

                fields = GetFields().Select(x => new MemberCache(x, this)).ToArray();
                memberFieldsMap.Init(memberFields, x => x.Name);
                return fields;
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

                fks = typeCache?.ForeignKeys ?? PublicBasicProperties
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
        public bool IsClass => Type.IsClass;

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
        public bool IsIdentity => IsPrimaryKey && (Obj.IsNumeric(Type, false) || Type == typeof(Guid));

        /// <summary>
        /// Получает значение, указывающее, является ли тип интерфейсом.
        /// </summary>
        public bool IsInterface => Type.IsInterface;

        /// <summary>
        /// Получает значение, указывающее, является ли член методом.
        /// </summary>
        public bool IsMethod { get; }

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
        public bool IsPrimaryKey { get; }

        /// <summary>
        /// Получает значение, указывающее, является ли член колонкой в базе данных.
        /// </summary>
        public bool IsColumn { get; }

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
        public bool IsValueType => type.IsValueType;

        /// <summary>
        /// Получает имя для сериализации JSON из атрибутов JsonProperty, JsonPropertyName и т.д.
        /// </summary>
        public string JsonName
        {
            get
            {
                if (jsonName != null)
                {
                    return jsonName;
                }

                if (typeCache == null)
                {
                    jsonName = string.Empty;
                    var jsonAttr = GetAttributes().FirstOrDefault(x => x.GetType().Name.StartsWith("Json"));
                    if (jsonAttr == null)
                    {
                        return jsonName;
                    }

                    var propName = jsonAttr.GetType().GetProperties().FirstOrDefault(p => p.Name.EndsWith("Name"));
                    if (propName != null)
                    {
                        jsonName = propName.GetValue(jsonAttr)?.ToString();
                    }
                }
                else
                {
                    jsonName = typeCache.jsonName;
                }

                return jsonName;
            }
        }

        /// <summary>
        /// Получает тип члена (свойство, метод, поле и т.д.).
        /// </summary>
        public override MemberTypes MemberType => MemberInfo.MemberType;

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

                pks = typeCache?.PrimaryKeys ??
                      PublicBasicProperties.Where(x => x.GetAttribute("KeyAttribute") != null).ToArray();

                if (pks.Length == 0)
                {
                    var p =
                        PublicBasicProperties.FirstOrDefault(x =>
                            x.Name.Equals("id", StringComparison.OrdinalIgnoreCase)) ??
                        PublicBasicProperties.FirstOrDefault(x =>
                            x.Name.Equals(TableName + "id", StringComparison.OrdinalIgnoreCase));
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
                if (properties != null)
                {
                    return properties;
                }

                properties = GetProperties().Select(x => new MemberCache(x, this)).ToArray();
                memberPropertiesMap.Init(memberProperties, x => x.Name);
                return properties;
            }
        }

        /// <summary>
        /// Получает поле, которое является backing-полем для автосвойства.
        /// </summary>
        public FieldInfo PropertyBackingField
        {
            get
            {
                if (propertyBackingField != null || (propertyBackingFieldExists.HasValue && !propertyBackingFieldExists.Value))
                    return propertyBackingField;
                try
                {
                    propertyBackingField = Obj.GetFieldInfoFromGetAccessor(AsPropertyInfo().GetGetMethod(true));
                }
                catch
                {
                    propertyBackingFieldExists = false;
                    return null;
                }

                propertyBackingFieldExists = true;
                return propertyBackingField;
            }
        }

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
                if (publicBasicProperties != null)
                {
                    return publicBasicProperties;
                }

                publicBasicProperties = PublicProperties.Where(x => x.IsBasic).ToArray();
                return publicBasicProperties;
            }
        }

        /// <summary>
        /// Получает массив публичных свойств, которые являются коллекциями.
        /// </summary>
        public MemberCache[] PublicEnumerableProperties
        {
            get
            {
                if (publicEnumerableProperties != null)
                {
                    return publicEnumerableProperties;
                }

                publicEnumerableProperties = PublicProperties.Where(x => x.IsCollection).ToArray();
                return publicEnumerableProperties;
            }
        }

        /// <summary>
        /// Получает массив публичных полей.
        /// </summary>
        public MemberCache[] PublicFields
        {
            get
            {
                if (publicFields != null)
                {
                    return publicFields;
                }

                publicFields = Fields.Where(x => x.IsPublic).ToArray();
                return publicFields;
            }
        }

        /// <summary>
        /// Получает массив публичных свойств.
        /// </summary>
        public MemberCache[] PublicProperties
        {
            get
            {
                if (publicProperties != null)
                {
                    return publicProperties;
                }

                publicProperties = Properties.Where(x => x.IsPublic).ToArray();
                return publicProperties;
            }
        }

        /// <summary>
        /// Получает тип объекта, отраженного этим экземпляром.
        /// </summary>
        public override Type ReflectedType => MemberInfo.ReflectedType;

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
        public Type Type => type;

        /// <summary>
        /// Получает имя атрибута XML для сериализации из атрибутов XmlAttribute, XmlAttributeAttribute и т.д.
        /// </summary>
        public string XmlAttributeName
        {
            get
            {
                if (xmlAttr != null)
                {
                    return xmlAttr;
                }

                if (typeCache == null)
                {
                    var xmlAttrs = GetAttributes().Where(x => x.GetType().Name.StartsWith("Xml")).ToArray();
                    if (xmlAttrs.Any())
                    {
                        foreach (var xa in xmlAttrs)
                        {
                            var propName = xa.GetType().GetProperties().FirstOrDefault(p => p.Name.EndsWith("Name"));
                            switch (propName?.Name)
                            {
                                case "ElementName":
                                    xmlElem = propName.GetValue(xa)?.ToString();
                                    break;

                                case "AttributeName":
                                    xmlAttr = propName.GetValue(xa)?.ToString();
                                    break;
                            }
                        }
                    }
                }
                else
                {
                    xmlAttr = typeCache.xmlAttr;
                }

                return xmlAttr ?? (xmlAttr = string.Empty);
            }
        }

        /// <summary>
        /// Получает имя элемента XML для сериализации из атрибутов XmlElement, XmlElementAttribute и т.д.
        /// </summary>
        public string XmlElementName
        {
            get
            {
                if (xmlElem != null)
                {
                    return xmlElem;
                }

                if (typeCache == null)
                {
                    var xmlAttrs = GetAttributes().Where(x => x.GetType().Name.StartsWith("Xml")).ToArray();
                    if (xmlAttrs.Any())
                    {
                        foreach (var xa in xmlAttrs)
                        {
                            var propName = xa.GetType().GetProperties().FirstOrDefault(p => p.Name.EndsWith("Name"));
                            switch (propName?.Name)
                            {
                                case "ElementName":
                                    xmlElem = propName.GetValue(xa)?.ToString();
                                    break;

                                case "AttributeName":
                                    xmlAttr = propName.GetValue(xa)?.ToString();
                                    break;
                            }
                        }
                    }

                    if (xmlElem == null)
                    {
                        xmlElem = string.Empty;
                    }
                }
                else
                {
                    xmlElem = typeCache.xmlElem;
                }

                return xmlElem;
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
            get => GetMember(memberName, MemberTypes.Property, MemberTypes.Field)?.GetValue(source);

            set => GetMember(memberName, MemberTypes.Property, MemberTypes.Field)?.SetValue(source, value);
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
            if (memberInfo == null)
            {
                throw new ArgumentNullException(nameof(memberInfo));
            }

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
        public ConstructorInfo AsConstructorInfo() => MemberInfo as ConstructorInfo;

        /// <summary>
        /// Преобразует текущий MemberCache в EventInfo.
        /// </summary>
        /// <returns>EventInfo или null, если текущий член не является событием.</returns>
        public EventInfo AsEventInfo() => MemberInfo as EventInfo;

        /// <summary>
        /// Преобразует текущий MemberCache в FieldInfo.
        /// </summary>
        /// <returns>FieldInfo или null, если текущий член не является полем.</returns>
        public FieldInfo AsFieldInfo() => MemberInfo as FieldInfo;

        /// <summary>
        /// Преобразует текущий MemberCache в MethodInfo.
        /// </summary>
        /// <returns>MethodInfo или null, если текущий член не является методом.</returns>
        public MethodInfo AsMethodInfo() => MemberInfo as MethodInfo;

        /// <summary>
        /// Преобразует текущий MemberCache в PropertyInfo.
        /// </summary>
        /// <returns>PropertyInfo или null, если текущий член не является свойством.</returns>
        public PropertyInfo AsPropertyInfo() => MemberInfo as PropertyInfo;

        /// <summary>
        /// Преобразует текущий MemberCache в Type.
        /// </summary>
        /// <returns>Type или null, если текущий член не является типом.</returns>
        public Type AsType() => MemberInfo as Type;

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
        /// Получает метод по имени.
        /// </summary>
        /// <param name="methodName">Имя метода.</param>
        /// <returns>MethodInfo или null, если метод не найден.</returns>
        public MethodInfo GetMethod(string methodName)
        {
            if (string.IsNullOrWhiteSpace(methodName))
                return null;

            return memberMethodsMap.GetOrAdd(
                methodName,
                n => GetMethods().FirstOrDefault(x => x.Name.Equals(n)) ?? GetMethods().FirstOrDefault(x => x.Name.Equals(n, StringComparison.OrdinalIgnoreCase)),
                a => a.Name);
        }

        /// <summary>
        /// Получает все атрибуты, примененные к текущему члену, включая атрибуты базовых типов.
        /// </summary>
        /// <returns>Массив атрибутов.</returns>
        public Attribute[] GetAttributes()
        {
            if (memberAttributes != null)
            {
                return memberAttributes;
            }

            memberAttributes = MemberInfo
                .GetCustomAttributes()
                .Concat(BaseTypes.SelectMany(x => x.GetCustomAttributes()))
                .Distinct()
                .ToArray();

            memberAttributesMap.Init(memberAttributes, x => x.GetType().Name);

            return memberAttributes;
        }

        /// <summary>
        /// Получает массив членов, которые представляют столбцы в базе данных.
        /// </summary>
        /// <returns>Массив MemberCache, представляющих столбцы.</returns>
        public MemberCache[] GetColumns()
        {
            if (columns != null)
            {
                return columns;
            }

            columns = GetProperties()
                .Select(pi => this[pi])
                .Where(x => x.IsProperty &&
                            x.IsPublic &&
                            x.IsBasic &&
                            !x.IsCollection &&
                            x.HasAnyAttributeOfType("ColumnAttribute", "KeyAttribute", "ForeignKeyAttribute")
                            && !x.HasAnyAttributeOfType("NotMappedAttribute"))
                .ToArray();

            return columns;
        }

        /// <summary>
        /// Находит конструктор, соответствующий предоставленным аргументам.
        /// </summary>
        /// <param name="ctorArgs">Аргументы конструктора (могут быть изменены при наличии параметров со значениями по умолчанию).</param>
        /// <returns>ConstructorInfo или null, если подходящий конструктор не найден.</returns>
        public ConstructorInfo GetConstructorByArgs(ref object[] ctorArgs)
        {
            var args = ctorArgs;
            foreach (var c in GetConstructors())
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

            var ctor = Constructors.FirstOrDefault(x => x.GetParameters().Length == args.Length);
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
            if (memberConstructors != null)
            {
                return memberConstructors;
            }

            memberConstructors = type.GetConstructors(DefaultBindingFlags)
                .Concat(BaseTypes.Where(x => !x.IsInterface)
                    .SelectMany(x => x.GetConstructors(DefaultBindingFlags)))
                .OrderBy(c => c.GetParameters().Length)
                .Distinct()
                .ToArray();
            return memberConstructors;
        }

        /// <summary>
        /// Возвращает массив всех настраиваемых атрибутов, примененных к этому члену.
        /// </summary>
        /// <param name="inherit">true для поиска цепочки наследования этого члена для поиска атрибутов; в противном случае — false.</param>
        /// <returns>Массив настраиваемых атрибутов.</returns>
        public override object[] GetCustomAttributes(bool inherit) => MemberInfo.GetCustomAttributes(inherit);

        /// <summary>
        /// Возвращает массив настраиваемых атрибутов, примененных к этому члену и идентифицируемых типом <see cref="Type"/>.
        /// </summary>
        /// <param name="attributeType">Тип атрибута для поиска.</param>
        /// <param name="inherit">true для поиска цепочки наследования этого члена для поиска атрибутов; в противном случае — false.</param>
        /// <returns>Массив настраиваемых атрибутов.</returns>
        public override object[] GetCustomAttributes(Type attributeType, bool inherit) => MemberInfo.GetCustomAttributes(attributeType, inherit);

        /// <summary>
        /// Получает все события текущего типа.
        /// </summary>
        /// <returns>Массив событий.</returns>
        public EventInfo[] GetEvents()
        {
            if (memberEvents != null)
            {
                return memberEvents;
            }

            memberEvents = type.GetEvents(DefaultBindingFlags)
                .Concat(BaseTypes.Where(x => !x.IsInterface)
                    .SelectMany(x => x.GetEvents(DefaultBindingFlags)))
                .Distinct()
                .ToArray();
            return memberEvents;
        }

        /// <summary>
        /// Получает MemberCache для поля с указанным именем.
        /// </summary>
        /// <param name="fieldName">Имя поля.</param>
        /// <returns>MemberCache для поля или null, если поле не найдено.</returns>
        public MemberCache GetField(string fieldName) => GetMember(fieldName, MemberTypes.Field);

        /// <summary>
        /// Получает все поля текущего типа.
        /// </summary>
        /// <returns>Массив полей.</returns>
        public FieldInfo[] GetFields()
        {
            if (memberFields != null)
            {
                return memberFields;
            }

            memberFields = type.GetFields(DefaultBindingFlags)
                .Concat(BaseTypes.Where(x => !x.IsInterface)
                    .SelectMany(x => x.GetFields(DefaultBindingFlags)))
                .Distinct()
                .ToArray();

            memberFieldsMap.Init(memberFields, x => x.Name);

            return memberFields;
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
                return nav?.PropertyType == Type;
            });
        }

        /// <summary>
        /// Получает все внешние ключи текущего типа.
        /// </summary>
        /// <returns>Массив внешних ключей.</returns>
        public MemberCache[] GetForeignKeys()
        {
            if (fks != null)
            {
                return fks;
            }

            fks = GetColumns()
                .Where(x => x.IsForeignKey)
                .ToArray();

            return fks;
        }

        /// <summary>
        /// Получает полное имя столбца в формате [Схема].[Таблица].[Столбец] с квадратными скобками.
        /// </summary>
        /// <returns>Полное имя столбца.</returns>
        public string GetFullColumnName() => GetFullColumnName("[", "]");

        /// <summary>
        /// Получает полное имя столбца с указанными префиксом и суффиксом для имен.
        /// </summary>
        /// <param name="namePrefix">Префикс для имен (например, "[").</param>
        /// <param name="nameSuffix">Суффикс для имен (например, "]").</param>
        /// <param name="defaultSchemaName">Имя схемы по умолчанию, если SchemaName не задан.</param>
        /// <returns>Полное имя столбца.</returns>
        public string GetFullColumnName(string namePrefix, string nameSuffix, string defaultSchemaName = null) => GetFullTableName(namePrefix, nameSuffix, defaultSchemaName) +
                                                                                                                  $".{namePrefix}{ColumnName}{nameSuffix}";

        /// <summary>
        /// Получает полное имя таблицы в формате [Схема].[Таблица] с квадратными скобками.
        /// </summary>
        /// <returns>Полное имя таблицы.</returns>
        public string GetFullTableName() => GetFullTableName("[", "]");

        /// <summary>
        /// Получает полное имя таблицы с указанными префиксом и суффиксом для имен.
        /// </summary>
        /// <param name="namePrefix">Префикс для имен (например, "[").</param>
        /// <param name="nameSuffix">Суффикс для имен (например, "]").</param>
        /// <param name="defaultSchemaName">Имя схемы по умолчанию, если SchemaName не задан.</param>
        /// <returns>Полное имя таблицы.</returns>
        public string GetFullTableName(string namePrefix, string nameSuffix, string defaultSchemaName = null)
        {
            var schema = string.IsNullOrWhiteSpace(SchemaName) ? defaultSchemaName : SchemaName;
            var fullTableName = $"{namePrefix}{TableName}{nameSuffix}";
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
            var memberInfo = GetMember(memberName, memberTypes)?.MemberInfo;
            if (!(memberInfo is TMember info))
            {
                return null;
            }

            return info;
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
            try
            {
                if (quickCache.TryGetValue(memberName, out var mc))
                    return mc;
                if (memberTypes == null || memberTypes.Length == 0)
                {
                    memberTypes = DefaultMemberTypes;
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
                            var eventInfo = memberEventsMap.GetOrAdd(memberName, x => type.GetEvent(x, DefaultBindingFlags) ?? type.GetEvent(x, DefaultIgnoreCaseBindingFlags), e => e.Name);
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
            catch (Exception ex)
            {
                throw new NotSupportedException($"Ошибка получения члена {memberName} в {this}: {ex}", ex);
            }
        }

        /// <summary>
        /// Получает все методы текущего типа.
        /// </summary>
        /// <returns>Массив методов.</returns>
        public MethodInfo[] GetMethods()
        {
            if (memberMethods != null)
            {
                return memberMethods;
            }

            memberMethods = type.GetMethods(DefaultBindingFlags)
                .Concat(BaseTypes.Where(x => !x.IsInterface)
                    .SelectMany(x => x.GetMethods(DefaultBindingFlags)))
                .Distinct()
                .ToArray();

            return memberMethods;
        }

        /// <summary>
        /// Получает все первичные ключи текущего типа.
        /// </summary>
        /// <returns>Массив первичных ключей.</returns>
        public MemberCache[] GetPrimaryKeys()
        {
            if (pks != null)
            {
                return pks;
            }

            pks = GetColumns()
                .Where(x => x.IsPrimaryKey)
                .ToArray();

            return pks;
        }

        /// <summary>
        /// Получает все свойства текущего типа.
        /// </summary>
        /// <returns>Массив свойств.</returns>
        public PropertyInfo[] GetProperties()
        {
            if (memberProperties != null)
            {
                return memberProperties;
            }

            var props = type.GetProperties(DefaultBindingFlags)
                .Concat(
                    BaseTypes
                        .Where(x => !x.IsInterface)
                        .SelectMany(x => x.GetProperties(DefaultBindingFlags)))
                .ToList();

            var seen = new HashSet<string>();

            memberProperties = props
                .Where(p => seen.Add(p.Name))
                .ToArray();

            return memberProperties;
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
            if (tables != null)
            {
                return tables;
            }

            tables = Properties.Where(x =>
                x.IsProperty &&
                x.IsPublic &&
                ((x.IsCollection &&
                !x.IsBasicCollection) || !x.IsBasic) &&
                !x.HasAnyAttributeOfType("ColumnAttribute", "NotMappedAttribute", "Key"))
                .ToArray();

            return tables;
        }

        /// <summary>
        /// Получает значение члена для указанного экземпляра.
        /// </summary>
        /// <param name="instance">Экземпляр объекта.</param>
        /// <returns>Значение члена.</returns>
        public object GetValue(object instance) => Getter(instance);

        /// <summary>
        /// Получает значение члена для указанного экземпляра и преобразует его к указанному типу.
        /// </summary>
        /// <typeparam name="T">Тип, к которому преобразуется значение.</typeparam>
        /// <param name="instance">Экземпляр объекта.</param>
        /// <returns>Значение члена, преобразованное к типу T.</returns>
        public T GetValue<T>(object instance) => Obj.ChangeType<T>(Getter(instance));

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
        public override bool IsDefined(Type attributeType, bool inherit) => MemberInfo.IsDefined(attributeType, inherit);

        /// <summary>
        /// Устанавливает значение члена для указанного экземпляра.
        /// </summary>
        /// <param name="source">Экземпляр объекта.</param>
        /// <param name="value">Значение для установки.</param>
        /// <param name="valueConverter">Конвертер значения (необязательный).</param>
        public virtual void SetValue(object source, object value, Func<object, object> valueConverter = null) => Setter(source, valueConverter == null ? Obj.ChangeType(value, Type) : valueConverter(value));

        /// <summary>
        /// Преобразует экземпляр объекта в словарь имен и значений свойств.
        /// </summary>
        /// <typeparam name="T">Тип объекта.</typeparam>
        /// <param name="instance">Экземпляр объекта.</param>
        /// <param name="propertyFilter">Фильтр свойств для включения (если не указаны, включаются все публичные свойства).</param>
        /// <returns>Словарь имен и значений свойств.</returns>
        public Dictionary<string, object> ToDictionary<T>(T instance, Func<MemberCache, bool> propertyFilter = null)
            where T : class
        {
            var dic = new Dictionary<string, object>();

            ToDictionary(instance, dic, propertyFilter);

            return dic;
        }

        /// <summary>
        /// Преобразует экземпляр объекта в словарь имен и значений свойств и добавляет их в указанный словарь.
        /// </summary>
        /// <typeparam name="T">Тип объекта.</typeparam>
        /// <param name="instance">Экземпляр объекта.</param>
        /// <param name="dictionary">Словарь, в который добавляются пары имя-значение.</param>
        /// <param name="propertyFilter">Фильтр свойств для включения (если не указаны, включаются все публичные свойства).</param>
        public void ToDictionary<T>(T instance, Dictionary<string, object> dictionary, Func<MemberCache, bool> propertyFilter = null)
            where T : class
        {
            if (propertyFilter == null)
                propertyFilter = x => x.IsPublic;

            var props = Properties.Where(propertyFilter).ToArray();

            foreach (var mi in props)
            {
                dictionary[mi.Name] = mi.GetValue(instance);
            }
        }

        /// <summary>
        /// Создать внутренний кеш для всех дочерних членов. В обычном режиме кеш создается по мере обращения.
        /// </summary>
        public void CreateInternalCaches()
        {
            _ = BaseTypes;
            _ = Properties;
            _ = PublicProperties;
            _ = PublicBasicEnumerableProperties;
            _ = PublicBasicProperties;
            _ = PublicEnumerableProperties;
            _ = ColumnProperties;
            _ = PrimaryKeys;
            _ = ForeignKeys;
            _ = Fields;
            _ = PublicFields;
            _ = GetMethods();
            _ = GetEvents();
            _ = GetConstructors();
            _ = JsonName;
            _ = XmlAttributeName;
            _ = XmlElementName;
        }

        /// <summary>
        /// Возвращает строковое представление текущего члена в формате "DeclaringType.Name.Name(Type.Name)".
        /// </summary>
        /// <returns>Строковое представление члена.</returns>
        public override string ToString()
        {
            if (!IsType)
                return $"{(IsPublic ? "public" : "private")} {Type.Name} [{DeclaringType?.Name}].[{Name}] {{{(IsGetterPublic ? " get;" : string.Empty)}{(IsSetterPublic ? " set;" : string.Empty)} }}";

            return $"{(IsPublic ? "public" : "private")} {Type.FullName}";
        }

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
            private readonly ConcurrentDictionary<string, TValue> exactMap =
                new ConcurrentDictionary<string, TValue>(StringComparer.Ordinal);

            private readonly ConcurrentDictionary<string, TValue> ignoreCaseMap =
                new ConcurrentDictionary<string, TValue>(StringComparer.OrdinalIgnoreCase);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public TValue GetOrAdd(string key, Func<string, TValue> valueFactory, Func<TValue, string> keySelector, bool ignoreCase = true)
            {
                if (key == null)
                    return null;
                if (valueFactory == null)
                    throw new ArgumentNullException(nameof(valueFactory));

                if (exactMap.TryGetValue(key, out var value))
                    return value;

                if (ignoreCase && ignoreCaseMap.TryGetValue(key, out value))
                    return value;

                value = valueFactory(key);

                if (value != null)
                {
                    exactMap[keySelector(value)] = value;
                    ignoreCaseMap[key] = value;
                }
                else
                {
                    exactMap[key] = null;
                    ignoreCaseMap[key] = null;
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

                    exactMap.TryAdd(key, item);

                    ignoreCaseMap.TryAdd(key, item);
                }
            }
        }
    }
}