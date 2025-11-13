using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;

namespace RuntimeStuff
{
    /// <summary>
    ///     Представляет расширенную обёртку над <see cref="MemberInfo" />, предоставляющую унифицированный доступ к
    ///     дополнительной информации и операциям для членов типа .NET<br />
    ///     (свойств, методов, полей, событий, конструкторов и самих типов).
    ///     Класс предназначен для использования в сценариях динамического анализа типов, построения универсальных
    ///     сериализаторов, ORM, генераторов кода, UI-редакторов и других задач,<br />
    ///     где требуется расширенная работа с метаданными .NET.
    ///     <para>
    ///         Класс <c>MemberInfoEx</c> позволяет:
    ///         <list type="bullet">
    ///             <item>
    ///                 Получать расширенные сведения о членах типа, включая их атрибуты, типы, модификаторы доступа, связи
    ///                 с базовыми типами и интерфейсами.
    ///             </item>
    ///             <item>
    ///                 Быстро и кэшированно получать члены по имени, включая поиск по альтернативным именам (отображаемое
    ///                 имя, JSON-имя, имя колонки и др.).
    ///             </item>
    ///             <item>
    ///                 Определять семантику члена: является ли он свойством, методом, полем, событием, конструктором, типом
    ///                 и т.д.
    ///             </item>
    ///             <item>
    ///                 Получать и устанавливать значения свойств и полей через делегаты, а также вызывать методы по
    ///                 отражению.
    ///             </item>
    ///             <item>
    ///                 Работать с атрибутами, включая стандартные и пользовательские, а также поддерживать работу с
    ///                 атрибутами сериализации (JSON, XML, DataAnnotations).
    ///             </item>
    ///             <item>
    ///                 Определять особенности члена: является ли он коллекцией, словарём, делегатом, nullable, числовым,
    ///                 булевым, кортежем, простым типом и др.
    ///             </item>
    ///             <item>
    ///                 Получать информацию о первичных и внешних ключах, колонках, таблицах и схемах для интеграции с ORM и
    ///                 сериализаторами.
    ///             </item>
    ///             <item> Кэшировать результаты для повышения производительности при повторных обращениях.</item>
    ///         </list>
    ///     </para>
    /// </summary>
    public class MemberInfoEx : MemberInfo
    {
        // Кэш для расширенной информации о членах класса
        private static readonly ConcurrentDictionary<MemberInfo, MemberInfoEx> MemberInfoCache =
            new ConcurrentDictionary<MemberInfo, MemberInfoEx>();

        // Кэш делегатов для создания экземпляров типов
        private static readonly ConcurrentDictionary<string, Func<object>> ConstructorsCache =
            new ConcurrentDictionary<string, Func<object>>();

        // Кэш делегатов для получения значения поля
        private static readonly ConcurrentDictionary<FieldInfo, Func<object, object>> FieldGettersCache =
            new ConcurrentDictionary<FieldInfo, Func<object, object>>();

        // Кэш делегатов для установки значения поля
        private static readonly ConcurrentDictionary<FieldInfo, Action<object, object>> FieldSettersCache =
            new ConcurrentDictionary<FieldInfo, Action<object, object>>();

        // Кэш делегатов для получения значения свойства
        private static readonly ConcurrentDictionary<PropertyInfo, Func<object, object>> PropertyGettersCache =
            new ConcurrentDictionary<PropertyInfo, Func<object, object>>();

        // Кэш делегатов для установки значения свойства
        private static readonly ConcurrentDictionary<PropertyInfo, Action<object, object>> PropertySettersCache =
            new ConcurrentDictionary<PropertyInfo, Action<object, object>>();

        private static readonly OpCode[] SOneByte = new OpCode[256];

        private static readonly OpCode[] STwoByte = new OpCode[256];

        // Кэш для делегатов получения значений членов
        private readonly ConcurrentDictionary<string, Func<object, object>> _getters =
            new ConcurrentDictionary<string, Func<object, object>>();

        // Кэш для расширенной информации о членах класса по имени
        private readonly ConcurrentDictionary<string, MemberInfoEx> _memberCache =
            new ConcurrentDictionary<string, MemberInfoEx>();

        // Кэш для делегатов установки и получения значений членов
        private readonly ConcurrentDictionary<string, Action<object, object>> _setters =
            new ConcurrentDictionary<string, Action<object, object>>();

        private readonly Type _type;

        internal readonly MemberInfo MemberInfo;

        private Attribute[] _attributes;

        private Type[] _baseTypes;

        private ConstructorInfo[] _constructors;

        private EventInfo[] _events;

        private FieldInfo[] _fields;

        private string _jsonName;

        private MemberInfoEx[] _members;

        private MethodInfo[] _methods;

        private MemberInfoEx _parent;

        private PropertyInfo[] _properties;

        private string _xmlAttr;

        private string _xmlElem;

        private readonly MemberInfoEx _memberInfoEx;

        /// <summary>
        ///     Конструктор для создания расширенной информации о члене класса
        /// </summary>
        /// <param name="memberInfo">Информация о члене класса</param>
        /// <param name="getMembers">Получить информацию о дочерних членах: свойства, поля, методы и т.п.</param>
        public MemberInfoEx(MemberInfo memberInfo, bool getMembers = false)
        {
            _memberInfoEx = memberInfo as MemberInfoEx;
            if (_memberInfoEx != null)
                memberInfo = _memberInfoEx.MemberInfo;

            MemberInfo = memberInfo;

            // Определяем тип члена класса
            var t = MemberInfo as Type;
            var pi = MemberInfo as PropertyInfo;
            var fi = MemberInfo as FieldInfo;
            var mi = MemberInfo as MethodInfo;
            var ci = MemberInfo as ConstructorInfo;
            var mx = MemberInfo as MemberInfoEx;
            var e = MemberInfo as EventInfo;

            // Устанавливаем тип в зависимости от вида члена класса
            if (t != null)
                _type = t;
            if (pi != null)
                _type = pi.PropertyType;
            if (fi != null)
                _type = fi.FieldType;
            if (mi != null)
                _type = mi.ReturnType;
            if (ci != null)
                _type = ci.DeclaringType;
            if (mx != null)
                _type = mx.Type;
            if (e != null)
                _type = e.EventHandlerType;

            // Устанавливаем свойства типа
            Type = _type ??
                   throw new NotSupportedException($"{nameof(MemberInfoEx)}: ({memberInfo.GetType().Name}) not supported!");
            IsDictionary = _memberInfoEx?.IsDictionary ?? TypeHelper.IsDictionary(_type);
            IsDelegate = _memberInfoEx?.IsDelegate ?? TypeHelper.IsDelegate(_type);
            IsFloat = _memberInfoEx?.IsFloat ?? TypeHelper.IsFloat(_type);
            IsNullable = _memberInfoEx?.IsNullable ?? TypeHelper.IsNullable(_type);
            IsNumeric = _memberInfoEx?.IsNumeric ?? TypeHelper.IsNumeric(_type);
            IsBoolean = _memberInfoEx?.IsBoolean ?? TypeHelper.IsBoolean(_type);
            IsCollection = _memberInfoEx?.IsCollection ?? TypeHelper.IsCollection(_type);
            ElementType = IsCollection ? _memberInfoEx?.ElementType ?? TypeHelper.GetCollectionItemType(Type) : null;
            IsBasic = _memberInfoEx?.IsBasic ?? TypeHelper.IsBasic(_type);
            IsBasicCollection = _memberInfoEx?.IsBasicCollection ?? (IsCollection && TypeHelper.IsBasic(ElementType));
            IsObject = _memberInfoEx?.IsObject ?? _type == typeof(object);
            IsTuple = _memberInfoEx?.IsTuple ?? TypeHelper.IsTuple(_type);
            IsProperty = pi != null;
            IsEvent = e != null;
            IsField = fi != null;
            IsType = t != null;
            IsMethod = mi != null;
            IsConstructor = ci != null;
            CanWrite = pi != null ? pi.CanWrite : fi != null;
            CanRead = pi != null ? pi.CanRead : fi != null;
            IsPublic = _memberInfoEx?.IsPublic ?? IsProperty ? AsPropertyInfo().GetAccessors().Any(m => m.IsPublic) :
                IsField ? AsFieldInfo().IsPublic :
                IsMethod ? AsMethodInfo().IsPublic : IsConstructor && AsConstructorInfo().IsPublic;
            IsPrivate = _memberInfoEx?.IsPrivate ?? IsProperty ? AsPropertyInfo().GetAccessors().Any(m => m.IsPrivate) :
                IsField ? AsFieldInfo().IsPrivate :
                IsMethod ? AsMethodInfo().IsPrivate : IsConstructor && AsConstructorInfo().IsPrivate;

            // Дополнительная обработка для типов
            if (IsType)
            {
                DefaultConstructor = _memberInfoEx?.DefaultConstructor ?? CreateConstructorDelegate(t);
                PrimaryKeys = _memberInfoEx?.PrimaryKeys ??
                              Properties.Where(x => TypeHelper.GetCustomAttribute(x, "KeyAttribute") != null).ToArray();
                ForeignKeys = _memberInfoEx?.ForeignKeys ?? Properties
                    .Where(x => TypeHelper.GetCustomAttribute(x, "ForeignKeyAttribute") != null).ToArray();

                if (_memberInfoEx == null)
                {
                    var tblAttr = TypeHelper.GetCustomAttribute(MemberInfo, "TableAttribute");
                    if (tblAttr != null)
                    {
                        var tblNameProperty = tblAttr.GetType().GetProperty("Name");
                        var tblSchemaProperty = tblAttr.GetType().GetProperty("Schema");
                        TableName = tblNameProperty?.GetValue(tblAttr)?.ToString() ?? Name;
                        SchemaName = tblSchemaProperty?.GetValue(tblAttr)?.ToString();
                    }
                }
                else
                {
                    TableName = _memberInfoEx.TableName;
                    SchemaName = _memberInfoEx.SchemaName;
                }

                Columns = _memberInfoEx?.Columns ?? Properties.Where(x =>
                    TypeHelper.GetCustomAttribute(x, "ColumnAttribute") != null &&
                    TypeHelper.GetCustomAttribute(x, "NotMappedAttribute") == null).ToArray();
            }

            // Дополнительная обработка для свойств
            if (pi != null)
            {
                if (_memberInfoEx == null)
                {
                    var keyAttr = TypeHelper.GetCustomAttribute(pi, "KeyAttribute");
                    var colAttr = TypeHelper.GetCustomAttribute(pi, "ColumnAttribute");
                    var fkAttr = TypeHelper.GetCustomAttribute(pi, "ForeignKeyAttribute");
                    Setter = GetPropertySetter(pi);
                    Getter = GetPropertyGetter(pi);
                    PropertyBackingField = Fields.FirstOrDefault(x => x.Name == $"<{Name}>k__BackingField") ??
                                           GetFieldInfoFromGetAccessor(pi.GetGetMethod(true));
                    ColumnName = colAttr != null
                        ? colAttr.GetType().GetProperty("Name")?.GetValue(colAttr)?.ToString() ?? Name
                        : IsPrimaryKey
                            ? Name
                            : null;
                    ForeignColumnName = fkAttr?.GetType().GetProperty("Name")?.GetValue(fkAttr)?.ToString();
                    IsPrimaryKey = keyAttr != null;
                    IsForeignKey = fkAttr != null;
                }
                else
                {
                    Setter = _memberInfoEx.Setter;
                    Getter = _memberInfoEx.Getter;
                    PropertyBackingField = _memberInfoEx.PropertyBackingField;
                    ColumnName = _memberInfoEx.ColumnName;
                    ForeignColumnName = _memberInfoEx.ForeignColumnName;
                    IsPrimaryKey = _memberInfoEx.IsPrimaryKey;
                    IsForeignKey = _memberInfoEx.IsForeignKey;
                }
            }

            if (fi != null)
            {
                Setter = _memberInfoEx?.Setter ?? GetFieldSetter(fi);
                Getter = _memberInfoEx?.Getter ?? GetFieldGetter(fi);
            }

            // Обработка имени
            Name = _memberInfoEx?.Name ?? MemberInfo.Name.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries)
                .LastOrDefault() ?? string.Empty;

            // Получение атрибутов
            Description = _memberInfoEx?.Description ??
                          MemberInfo.GetCustomAttributes<DescriptionAttribute>().FirstOrDefault()?.Description;
            DisplayName = _memberInfoEx?.DisplayName ?? MemberInfo.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName;

            if (_memberInfoEx == null)
            {
                var displayAttr = TypeHelper.GetCustomAttribute(MemberInfo, "DisplayAttribute");
                if (displayAttr != null)
                {
                    var dispGroupNameProp = displayAttr.GetType().GetProperty("GroupName");
                    if (dispGroupNameProp != null)
                        GroupName = dispGroupNameProp.GetValue(displayAttr)?.ToString() ?? DisplayName;
                }
            }
            else
            {
                GroupName = _memberInfoEx.GroupName;
            }

            // Рекурсивная загрузка членов класса
            if (getMembers) _members = _memberInfoEx?._members ?? GetMembersInternal();
        }

        /// <summary>
        ///     Карта интерфейсов коллекций к конкретным типам реализаций.
        /// </summary>
        public static Dictionary<Type, Type> InterfaceToInstanceMap { get; } = new Dictionary<Type, Type>
        {
            { typeof(IEnumerable), typeof(List<object>) },
            { typeof(IEnumerable<>), typeof(List<>) },
            { typeof(ICollection), typeof(ObservableCollection<object>) },
            { typeof(ICollection<>), typeof(ObservableCollection<>) },
            { typeof(IDictionary<,>), typeof(Dictionary<,>) }
        };

        #region Статические свойства

        /// <summary>
        ///     Флаги для поиска членов класса по умолчанию
        /// </summary>
        public static BindingFlags DefaultBindingFlags { get; set; } = BindingFlags.Instance | BindingFlags.NonPublic |
                                                                       BindingFlags.Public | BindingFlags.Static;

        #endregion Статические свойства

        #region Вспомогательные методы

        /// <summary>
        ///     Возвращает строковое представление члена в формате "Имя (Тип)".
        /// </summary>
        /// <returns>Строка с именем и типом члена.</returns>
        public override string ToString()
        {
            return $"\"{Name}\" ({Type.Name})";
        }

        #endregion Вспомогательные методы

        #region Основные свойства

        private Dictionary<string, MemberInfoEx> _publicBasicEnumerableProperties;

        private Dictionary<string, MemberInfoEx> _publicBasicProperties;

        private Dictionary<string, MemberInfoEx> _publicEnumerableProperties;

        private Dictionary<string, MemberInfoEx> _publicProperties;

        /// <summary>
        ///     Атрибуты члена класса
        /// </summary>
        public Attribute[] Attributes => _attributes ?? GetAttributes();

        /// <summary>
        ///     Базовые типы и интерфейсы
        /// </summary>
        public Type[] BaseTypes
        {
            get
            {
                if (_baseTypes != null)
                    return _baseTypes;

                _baseTypes = TypeHelper.GetBaseTypes(_type, getInterfaces: true);
                return _baseTypes;
            }
        }

        /// <summary>
        ///     Можно ли читать значение (для свойств и полей)
        /// </summary>
        public bool CanRead { get; }

        /// <summary>
        ///     Можно ли записывать значение (для свойств и полей)
        /// </summary>
        public bool CanWrite { get; }

        /// <summary>
        ///     Имя колонки (из атрибута ColumnAttribute)
        /// </summary>
        public string ColumnName { get; }

        /// <summary>
        ///     Колонки (свойства с атрибутом ColumnAttribute)
        /// </summary>
        public PropertyInfo[] Columns { get; } = Array.Empty<PropertyInfo>();

        /// <summary>
        ///     Конструкторы типа
        /// </summary>
        public ConstructorInfo[] Constructors => _constructors ?? GetConstructors();

        /// <summary>
        ///     Тип, объявивший этот член
        /// </summary>
        public override Type DeclaringType => MemberInfo.DeclaringType;

        /// <summary>
        ///     Делегат конструктора по умолчанию
        /// </summary>
        public Func<object> DefaultConstructor { get; }

        /// <summary>
        ///     Описание (из атрибута DescriptionAttribute)
        /// </summary>
        public string Description { get; }

        /// <summary>
        ///     Отображаемое имя (из атрибута DisplayNameAttribute)
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        ///     Тип элемента коллекции (если текущий тип является коллекцией)
        /// </summary>
        public Type ElementType { get; }

        /// <summary>
        ///     События типа
        /// </summary>
        public EventInfo[] Events => _events ?? GetEvents();

        /// <summary>
        ///     Поля типа
        /// </summary>
        public FieldInfo[] Fields => _fields ?? GetFields();

        /// <summary>
        ///     Имя внешнего ключа (из атрибута ForeignKeyAttribute)
        /// </summary>
        public string ForeignColumnName { get; }

        /// <summary>
        ///     Свойства, помеченные как внешние ключи
        /// </summary>
        public PropertyInfo[] ForeignKeys { get; }

        /// <summary>
        ///     Делегат для получения значения свойства
        /// </summary>
        public Func<object, object> Getter { get; }

        /// <summary>
        ///     Имя группы (из атрибута DisplayAttribute)
        /// </summary>
        public string GroupName { get; }

        /// <summary>
        ///     Является ли тип простым (примитивным или строкой)
        /// </summary>
        public bool IsBasic { get; }

        /// <summary>
        ///     Является ли коллекция коллекцией простых типов
        /// </summary>
        public bool IsBasicCollection { get; }

        /// <summary>
        ///     Является ли тип булевым
        /// </summary>
        public bool IsBoolean { get; }

        /// <summary>
        ///     Является ли тип коллекцией
        /// </summary>
        public bool IsCollection { get; }

        /// <summary>
        ///     Является ли член конструктором
        /// </summary>
        public bool IsConstructor { get; set; }

        /// <summary>
        ///     Является ли тип делегатом
        /// </summary>
        public bool IsDelegate { get; }

        /// <summary>
        ///     Является ли тип словарем
        /// </summary>
        public bool IsDictionary { get; }

        /// <summary>
        ///     Является ли член событием
        /// </summary>
        public bool IsEvent { get; set; }

        /// <summary>
        ///     Является ли член полем
        /// </summary>
        public bool IsField { get; }

        /// <summary>
        ///     Является ли тип числом с плавающей точкой
        /// </summary>
        public bool IsFloat { get; }

        /// <summary>
        ///     Является ли свойство внешним ключом
        /// </summary>
        public bool IsForeignKey { get; set; }

        /// <summary>
        ///     Является ли первичный ключ автоинкрементным (число или Guid)
        /// </summary>
        public bool IsIdentity => IsPrimaryKey && (TypeHelper.IsNumeric(Type, false) || Type == typeof(Guid));

        /// <summary>
        ///     Является ли тип интерфейсом
        /// </summary>
        public bool IsInterface => Type.IsInterface;

        /// <summary>
        ///     Является ли член методом
        /// </summary>
        public bool IsMethod { get; set; }

        /// <summary>
        ///     Является ли тип nullable
        /// </summary>
        public bool IsNullable { get; }

        /// <summary>
        ///     Является ли тип числовым
        /// </summary>
        public bool IsNumeric { get; }

        /// <summary>
        ///     Является ли тип object
        /// </summary>
        public bool IsObject { get; }

        /// <summary>
        ///     Является ли свойство первичным ключом
        /// </summary>
        public bool IsPrimaryKey { get; set; }

        /// <summary>
        ///     Является ли член приватным
        /// </summary>
        public bool IsPrivate { get; }

        /// <summary>
        ///     Является ли член свойством
        /// </summary>
        public bool IsProperty { get; }

        /// <summary>
        ///     Является ли член публичным
        /// </summary>
        public bool IsPublic { get; }

        /// <summary>
        ///     Является ли тип кортежем
        /// </summary>
        public bool IsTuple { get; }

        /// <summary>
        ///     Является ли член типом
        /// </summary>
        public bool IsType { get; }

        /// <summary>
        ///     Является ли тип значимым типом
        /// </summary>
        public bool IsValueType => _type.IsValueType;

        /// <summary>
        ///     Имя в JSON (из атрибутов JsonPropertyNameAttribute или JsonPropertyAttribute)
        /// </summary>
        public string JsonName
        {
            get
            {
                if (_jsonName != null)
                    return _jsonName;

                if (_memberInfoEx == null)
                {
                    _jsonName = "";
                    var jsonAttr = Attributes.FirstOrDefault(x => x.GetType().Name.StartsWith("Json"));
                    if (jsonAttr == null) return _jsonName;
                    var propName = jsonAttr.GetType().GetProperties().FirstOrDefault(p => p.Name.EndsWith("Name"));
                    if (propName != null)
                        _jsonName = propName.GetValue(jsonAttr)?.ToString();
                }
                else
                {
                    _jsonName = _memberInfoEx._jsonName;
                }

                return _jsonName;
            }
        }

        /// <summary>
        ///     Все члены типа (свойства, поля, методы и т.д.)
        /// </summary>
        public MemberInfoEx[] Members
        {
            get
            {
                if (_members != null)
                    return _members;
                _members = _memberInfoEx?._members ?? GetMembersInternal();
                return _members;
            }
        }

        /// <summary>
        ///     Тип члена (свойство, метод, поле и т.д.)
        /// </summary>
        public override MemberTypes MemberType => MemberInfo.MemberType;

        /// <summary>
        ///     Методы типа
        /// </summary>
        public MethodInfo[] Methods => _methods ?? _memberInfoEx?._methods ?? GetMethods();

        /// <summary>
        ///     Имя члена
        /// </summary>
        public sealed override string Name { get; }

        /// <summary>
        ///     Родительский член (для вложенных членов)
        /// </summary>
        public MemberInfoEx Parent
        {
            get
            {
                if (_parent != null)
                    return _parent;

                if (MemberInfo.DeclaringType != null)
                    _parent = _memberInfoEx?._parent ?? Create(MemberInfo.DeclaringType);

                return _parent;
            }
        }

        /// <summary>
        ///     Свойства, помеченные как первичные ключи
        /// </summary>
        public PropertyInfo[] PrimaryKeys { get; }

        /// <summary>
        ///     Свойства типа
        /// </summary>
        public PropertyInfo[] Properties => _properties ?? GetProperties();

        /// <summary>
        ///     Поле, хранящее значение свойства (для автоматически реализуемых свойств)
        /// </summary>
        public FieldInfo PropertyBackingField { get; }

        /// <summary>
        ///     Словарь публичных свойств-коллекций с элементом списка {T}, где T - простой тип <see cref="BaseTypes" />
        /// </summary>
        public Dictionary<string, MemberInfoEx> PublicBasicEnumerableProperties
        {
            get
            {
                if (_publicBasicEnumerableProperties != null)
                    return _publicBasicEnumerableProperties;

                _publicBasicEnumerableProperties = _memberInfoEx?._publicBasicEnumerableProperties ?? Members.Where(x => x.IsPublic && x.IsProperty && x.IsBasicCollection)
                    .ToDictionary(x => x.Name);

                return _publicBasicEnumerableProperties;
            }
        }

        /// <summary>
        ///     Словарь простых (<see cref="TypeHelper.BasicTypes" />) публичных свойств типа
        /// </summary>
        public Dictionary<string, MemberInfoEx> PublicBasicProperties
        {
            get
            {
                if (_publicBasicProperties != null)
                    return _publicBasicProperties;

                _publicBasicProperties = _memberInfoEx?._publicBasicProperties ??
                                         Members.Where(x => x.IsPublic && x.IsProperty && x.IsBasic).ToDictionary(x => x.Name);

                return _publicBasicProperties;
            }
        }

        /// <summary>
        ///     Словарь публичных свойств-коллекций с элементом списка {T}, где T : class
        /// </summary>
        public Dictionary<string, MemberInfoEx> PublicEnumerableProperties
        {
            get
            {
                if (_publicEnumerableProperties != null)
                    return _publicEnumerableProperties;

                _publicEnumerableProperties = _memberInfoEx?._publicEnumerableProperties ??
                                              Members
                                                  .Where(x => x.IsPublic && x.IsProperty && x.IsCollection && !x.IsBasicCollection)
                                                  .ToDictionary(x => x.Name);

                return _publicEnumerableProperties;
            }
        }

        /// <summary>
        ///     Словарь публичных свойств типа
        /// </summary>
        public Dictionary<string, MemberInfoEx> PublicProperties
        {
            get
            {
                if (_publicProperties != null)
                    return _publicProperties;

                _publicProperties = _memberInfoEx?._publicProperties ?? Members.Where(x => x.IsPublic && x.IsProperty).ToDictionary(x => x.Name);

                return _publicProperties;
            }
        }

        /// <summary>
        ///     Тип, через который был получен этот член
        /// </summary>
        public override Type ReflectedType => MemberInfo.ReflectedType;

        /// <summary>
        ///     Имя схемы (из атрибута TableAttribute.Schema)
        /// </summary>
        public string SchemaName { get; }

        /// <summary>
        ///     Делегат для установки значения свойства
        /// </summary>
        public Action<object, object> Setter { get; }

        /// <summary>
        ///     Имя таблицы (из атрибута TableAttribute.Name)
        /// </summary>
        public string TableName { get; }

        /// <summary>
        ///     Тип члена
        /// </summary>
        public Type Type { get; }

        /// <summary>
        ///     Имя XML атрибута (из XmlAttributeAttribute)
        /// </summary>
        public string XmlAttributeName
        {
            get
            {
                if (_xmlAttr != null)
                    return _xmlAttr;

                if (_memberInfoEx == null)
                {
                    var xmlAttrs = Attributes.Where(x => x.GetType().Name.StartsWith("Xml")).ToArray();
                    if (xmlAttrs.Any())
                    {
                        foreach (var xa in xmlAttrs)
                        {
                            var propName = xa.GetType().GetProperties().FirstOrDefault(p => p.Name.EndsWith("Name"));
                            switch (propName?.Name)
                            {
                                case "ElementName":
                                    _xmlElem = propName.GetValue(xa)?.ToString();
                                    break;

                                case "AttributeName":
                                    _xmlAttr = propName.GetValue(xa)?.ToString();
                                    break;
                            }
                        }
                    }
                } else
                {
                    _xmlAttr = _memberInfoEx._xmlAttr;
                }

                return _xmlAttr ?? (_xmlAttr = string.Empty);
            }
        }

        /// <summary>
        ///     Имя XML элемента (из XmlElementAttribute)
        /// </summary>
        public string XmlElementName
        {
            get
            {
                if (_xmlElem != null)
                    return _xmlElem;

                if (_memberInfoEx == null)
                {
                    var xmlAttrs = Attributes.Where(x => x.GetType().Name.StartsWith("Xml")).ToArray();
                    if (xmlAttrs.Any())
                    {
                        foreach (var xa in xmlAttrs)
                        {
                            var propName = xa.GetType().GetProperties().FirstOrDefault(p => p.Name.EndsWith("Name"));
                            switch (propName?.Name)
                            {
                                case "ElementName":
                                    _xmlElem = propName.GetValue(xa)?.ToString();
                                    break;

                                case "AttributeName":
                                    _xmlAttr = propName.GetValue(xa)?.ToString();
                                    break;
                            }
                        }
                    }

                    if (_xmlElem == null)
                        _xmlElem = string.Empty;
                } else
                {
                    _xmlElem = _memberInfoEx._xmlElem;
                }

                return _xmlElem;
            }
        }

        /// <summary>
        ///     Имя в XML (элемент или атрибут)
        /// </summary>
        public string XmlName { get; } = null;

        #endregion Основные свойства

        #region Индексаторы

        /// <summary>
        ///     Установить или получить значение свойства или поля по имени
        /// </summary>
        /// <param name="source">Объект</param>
        /// <param name="memberName">Имя свойства или поля</param>
        /// <returns></returns>
        public object this[object source, string memberName]
        {
            get => _getters[memberName](source);

            set => _setters[memberName](source, value);
        }

        /// <summary>
        ///     Получить член по имени
        /// </summary>
        /// <param name="memberName">Имя члена для поиска</param>
        /// <returns>Найденный член или null, если не найден</returns>
        public MemberInfoEx this[string memberName] => this[memberName, MemberNameType.Any];

        /// <summary>
        ///     Получить член по имени с фильтрацией
        /// </summary>
        /// <param name="memberName">Имя члена для поиска</param>
        /// <param name="memberNameType">Тип имени члена для поиска</param>
        /// <param name="memberFilter">Фильтр для отбора членов</param>
        /// <returns>Найденный член или null, если не найден</returns>
        public MemberInfoEx this[string memberName, MemberNameType memberNameType = MemberNameType.Any,
            Func<MemberInfoEx, bool> memberFilter = null] => GetMember(memberName, memberNameType, memberFilter);

        #endregion Индексаторы

        #region Статические методы

        /// <summary>
        ///     Преобразует значение к указанному типу.
        /// </summary>
        /// <param name="value">Значение для преобразования.</param>
        /// <param name="toType">Тип, в который нужно преобразовать.</param>
        /// <param name="formatProvider">Провайдер формата (по умолчанию <see cref="CultureInfo.InvariantCulture" />).</param>
        /// <returns>Преобразованное значение.</returns>
        /// <exception cref="InvalidCastException">Если преобразование невозможно.</exception>
        /// <exception cref="FormatException">Если формат значения некорректен.</exception>
        /// <exception cref="ArgumentNullException">Если <paramref name="toType" /> равен null.</exception>
        public static object ChangeType(object value, Type toType, IFormatProvider formatProvider = null)
        {
            return TypeHelper.ChangeType(value, toType, formatProvider);
        }

        /// <summary>
        ///     Преобразует значение к указанному типу.
        /// </summary>
        /// <param name="value">Значение для преобразования.</param>
        /// <param name="formatProvider">Провайдер формата (по умолчанию <see cref="CultureInfo.InvariantCulture" />).</param>
        /// <returns>Преобразованное значение.</returns>
        /// <exception cref="InvalidCastException">Если преобразование невозможно.</exception>
        /// <exception cref="FormatException">Если формат значения некорректен.</exception>
        public static T ChangeType<T>(object value, IFormatProvider formatProvider = null)
        {
            return TypeHelper.ChangeType<T>(value, formatProvider);
        }

        /// <summary>
        ///     Создать расширенную информацию о члене класса (с кэшированием)
        /// </summary>
        /// <param name="memberInfo">Информация о члене класса</param>
        /// <returns>Расширенная информация о члене класса</returns>
        public static MemberInfoEx Create(MemberInfo memberInfo)
        {
            if (memberInfo is MemberInfoEx me)
                return me;

            var result = memberInfo == null ? null : MemberInfoCache.GetOrAdd(memberInfo, x => new MemberInfoEx(x, x is Type));
            if (!(memberInfo is Type)) return result;
            foreach (var m in result.Members)
            {
                result._setters.TryAdd(m.Name, m.Setter);
                result._getters.TryAdd(m.Name, m.Getter);
            }

            return result;
        }

        /// <summary>
        ///     Создаёт делегат для конструктора по умолчанию указанного типа.
        ///     Используется для быстрой активации объектов без вызова Activator.CreateInstance(Type)
        /// </summary>
        /// <param name="type">Тип, для которого создаётся делегат конструктора.</param>
        /// <returns>
        ///     Делегат <see cref="Func{Object}" />, который создаёт экземпляр типа, или <c>null</c>, если конструктор по умолчанию
        ///     отсутствует.
        /// </returns>
        public static Func<object> CreateConstructorDelegate(Type type)
        {
            if (type == null)
                return null;

            if (ConstructorsCache.TryGetValue(type.FullName ?? type.Name, out var ctor))
                return ctor;

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
        ///     Возвращает делегат для получения значения указанного поля.<br />
        ///     Поддерживаются instance и static поля, включая приватные.
        /// </summary>
        /// <param name="field">Поле, для которого создаётся getter.</param>
        /// <returns>
        ///     Делегат <see cref="Func{Object,Object}" />, который принимает объект-владельца (или <c>null</c> для static полей)
        ///     и возвращает текущее значение поля.
        /// </returns>
        public static Func<object, object> GetFieldGetter(FieldInfo field)
        {
            return FieldGettersCache.GetOrAdd(field, CreateFieldGetter);
        }

        /// <summary>
        ///     Получает поле, связанное с методом получения свойства (геттером).
        /// </summary>
        /// <param name="getMethod">Метод получения свойства.</param>
        /// <returns>Информация о поле, либо <c>null</c>, если не удалось определить поле.</returns>
        public static FieldInfo GetFieldInfoFromGetAccessor(MethodInfo getMethod)
        {
            try
            {
                var getMethodBody = getMethod?.GetMethodBody();
                if (getMethodBody == null)
                    return null;
                var body = getMethodBody.GetILAsByteArray();
                if (body[0] != 0x02 || body[1] != 0x7B) return null;
                var fieldToken = BitConverter.ToInt32(body, 2);
                return getMethod.DeclaringType?.Module.ResolveField(fieldToken);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                return null;
            }
        }

        /// <summary>
        ///     Возвращает делегат для установки значения указанного поля.<br />
        ///     Для повышения производительности используется кэш компилированных выражений.
        /// </summary>
        /// <param name="field">Поле, для которого создаётся setter.</param>
        /// <returns>
        ///     Делегат <see cref="Action{Object,Object}" />, который принимает объект-владельца (или <c>null</c> для static полей)
        ///     и новое значение. Если поле <c>readonly</c> или <c>const</c>, возвращается <c>null</c>.
        /// </returns>
        public static Action<object, object> GetFieldSetter(FieldInfo field)
        {
            return FieldSettersCache.GetOrAdd(field, CreateFieldSetter);
        }

        /// <summary>
        ///     Пытается определить поле, используемое для хранения значения свойства.<br />
        ///     Работает как для автоматически реализованных свойств (backing field вида <c>&lt;Имя&gt;k__BackingField</c>),
        ///     так и для свойств с ручной реализацией, если их геттер/сеттер явно обращается к полю.
        /// </summary>
        /// <param name="propertyInfo">Свойство, для которого ищется поле.</param>
        /// <returns>
        ///     Экземпляр <see cref="FieldInfo" />, если удалось найти поле, или <c>null</c>,
        ///     если backing field отсутствует (например, свойство вычисляемое).
        /// </returns>
        /// <exception cref="ArgumentNullException">Если <paramref name="propertyInfo" /> равен <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException">Если свойство не имеет типа-владельца.</exception>
        public static FieldInfo GetPropertyBackingField(PropertyInfo propertyInfo)
        {
            if (propertyInfo == null) throw new ArgumentNullException(nameof(propertyInfo));
            var declType = propertyInfo.DeclaringType
                           ?? throw new InvalidOperationException("Property has no declaring type.");

            // 1) Попытка: авто-свойство
            var autoName = $"<{propertyInfo.Name}>k__BackingField";
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic |
                                       BindingFlags.Public;
            var field = declType.GetField(autoName, flags);
            if (field != null) return field;

            // 2) Попытка: достать поле из IL геттера/сеттера
            return TryGetFieldFromAccessor(propertyInfo.GetGetMethod(true))
                   ?? TryGetFieldFromAccessor(propertyInfo.GetSetMethod(true));
        }

        /// <summary>
        ///     Возвращает делегат для получения значения указанного свойства.<br />
        ///     Для повышения производительности используется кэш компилированных выражений.
        /// </summary>
        /// <param name="property">Свойство, для которого создаётся getter.</param>
        /// <returns>
        ///     Делегат <see cref="Func{Object,Object}" />, который принимает объект-владельца (или <c>null</c> для static свойств)
        ///     и возвращает текущее значение свойства.
        /// </returns>
        public static Func<object, object> GetPropertyGetter(PropertyInfo property)
        {
            return PropertyGettersCache.GetOrAdd(property, CreatePropertyGetter);
        }

        /// <summary>
        ///     Возвращает делегат для установки значения указанного свойства.<br />
        ///     Для повышения производительности используется кэш компилированных выражений.
        /// </summary>
        /// <param name="property">Свойство, для которого создаётся setter.</param>
        /// <returns>
        ///     Делегат <see cref="Action{Object,Object}" />, который принимает объект-владельца (или <c>null</c> для static
        ///     свойств)
        ///     и новое значение. Если свойство read-only, возвращается <c>null</c>.
        /// </returns>
        public static Action<object, object> GetPropertySetter(PropertyInfo property)
        {
            return PropertySettersCache.GetOrAdd(property, CreatePropertySetter);
        }

        /// <summary>
        ///     Создаёт делегат для получения значения поля.
        /// </summary>
        /// <param name="field">Поле, для которого создаётся getter.</param>
        /// <returns>Делегат для получения значения поля</returns>
        private static Func<object, object> CreateFieldGetter(FieldInfo field)
        {
            var objParam = Expression.Parameter(typeof(object), "obj");

            Expression body;
            if (field.IsStatic)
            {
                body = Expression.Convert(Expression.Field(null, field), typeof(object));
            }
            else
            {
                var instanceCast = Expression.Convert(objParam, field.DeclaringType ?? typeof(object));
                body = Expression.Convert(Expression.Field(instanceCast, field), typeof(object));
            }

            return Expression.Lambda<Func<object, object>>(body, objParam).Compile();
        }

        /// <summary>
        ///     Создаёт делегат для установки значения поля.
        /// </summary>
        /// <param name="field">Поле, для которого создаётся setter.</param>
        /// <returns>Делегат для установки значения поля или null, если поле read-only</returns>
        private static Action<object, object> CreateFieldSetter(FieldInfo field)
        {
            if (field == null)
                return null;

            if (field.IsInitOnly || field.IsLiteral)
                return null;

            try
            {
                var objParam = Expression.Parameter(typeof(object), "obj");
                var valueParam = Expression.Parameter(typeof(object), "value");
                var valueCast = Expression.Convert(valueParam, field.FieldType);

                Expression body;
                if (field.IsStatic)
                {
                    body = Expression.Assign(Expression.Field(null, field), valueCast);
                }
                else
                {
                    var instanceCast = Expression.Convert(objParam, field.DeclaringType ?? typeof(object));
                    body = Expression.Assign(Expression.Field(instanceCast, field), valueCast);
                }

                return Expression.Lambda<Action<object, object>>(body, objParam, valueParam).Compile();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating field setter for {field.Name}: {ex}");
                return null;
            }
        }

        /// <summary>
        ///     Создаёт делегат для получения значения свойства.
        /// </summary>
        /// <param name="property">Свойство, для которого создаётся getter.</param>
        /// <returns>Делегат для получения значения свойства</returns>
        private static Func<object, object> CreatePropertyGetter(PropertyInfo property)
        {
            try
            {
                var objParam = Expression.Parameter(typeof(object), "obj");

                Expression body;
                //if (getMethod.IsStatic)
                //{
                //    // static: (object)Class.Property
                //    body = Expression.Convert(Expression.Property(null, property), typeof(object));
                //}
                //else
                {
                    // instance: (object)((TDeclaring)obj).Property
                    var instanceCast = Expression.Convert(objParam, property.DeclaringType ?? typeof(object));
                    body = Expression.Convert(Expression.Property(instanceCast, property), typeof(object));
                }

                return Expression.Lambda<Func<object, object>>(body, objParam).Compile();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        ///     Создаёт делегат для установки значения свойства.
        /// </summary>
        /// <param name="property">Свойство, для которого создаётся setter.</param>
        /// <returns>Делегат для установки значения свойства или null, если свойство read-only</returns>
        private static Action<object, object> CreatePropertySetter(PropertyInfo property)
        {
            try
            {
                var setMethod = property.GetSetMethod(true);
                if (setMethod == null)
                {
                    var backingField = GetPropertyBackingField(property);
                    return backingField == null ? null : GetFieldSetter(backingField);
                }

                var objParam = Expression.Parameter(typeof(object), "obj");
                var valueParam = Expression.Parameter(typeof(object), "value");
                var valueCast = Expression.Convert(valueParam, property.PropertyType);

                Expression body;

                //if (setMethod.IsStatic)
                //{
                //    // static: Class.Property = (T)value
                //    body = Expression.Assign(Expression.Property(null, property), valueCast);
                //}
                //else
                {
                    // instance: ((TDeclaring)obj).Property = (T)value
                    var instanceCast = Expression.Convert(objParam, property.DeclaringType ?? typeof(object));
                    body = Expression.Assign(Expression.Property(instanceCast, property), valueCast);
                }

                return Expression.Lambda<Action<object, object>>(body, objParam, valueParam).Compile();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        ///     Пытается определить поле из IL кода метода доступа (геттера/сеттера).
        /// </summary>
        /// <param name="accessor">Метод доступа (геттер или сеттер свойства)</param>
        /// <returns>Найденное поле или null, если не удалось определить</returns>
        private static FieldInfo TryGetFieldFromAccessor(MethodInfo accessor)
        {
            if (accessor == null) return null;
            var body = accessor.GetMethodBody();
            if (body == null) return null;

            var il = body.GetILAsByteArray();
            if (il.Length == 0) return null;

            var i = 0;
            var module = accessor.Module;
            var typeArgs = accessor.DeclaringType?.GetGenericArguments();
            var methodArgs = accessor.GetGenericArguments();

            while (i < il.Length)
            {
                OpCode op;
                var code = il[i++];

                if (code != 0xFE)
                {
                    op = SOneByte[code];
                }
                else
                {
                    var b2 = il[i++];
                    op = STwoByte[b2];
                }

                switch (op.OperandType)
                {
                    case OperandType.InlineNone:
                        break;

                    case OperandType.ShortInlineI:
                    case OperandType.ShortInlineVar:
                    case OperandType.ShortInlineBrTarget:
                        i++;
                        break;

                    case OperandType.InlineVar:
                        i += 2;
                        break;

                    case OperandType.InlineI:
                    case OperandType.InlineBrTarget:
                    case OperandType.InlineString:
                    case OperandType.InlineSig:
                    case OperandType.InlineMethod:
                    case OperandType.InlineType:
                    case OperandType.InlineTok:
                    case OperandType.ShortInlineR:
                        i += 4;
                        break;

                    case OperandType.InlineI8:
                    case OperandType.InlineR:
                        i += 8;
                        break;

                    case OperandType.InlineSwitch:
                        var count = BitConverter.ToInt32(il, i);
                        i += 4 + 4 * count;
                        break;

                    case OperandType.InlineField:
                        // Вот он — операнд поля у ldfld/ldsfld/stfld/stsfld/ldflda
                        var token = BitConverter.ToInt32(il, i);
                        //i += 4;

                        try
                        {
                            var fi = module.ResolveField(token, typeArgs, methodArgs);
                            return fi;
                        }
                        catch
                        {
                            return null;
                        }
                }
            }

            return null;
        }

        #endregion Статические методы

        #region Методы преобразования

        /// <summary>
        ///     Возвращает <see cref="ConstructorInfo" /> для текущего члена, если он является конструктором.
        /// </summary>
        /// <returns>Экземпляр <see cref="ConstructorInfo" />, либо <c>null</c>.</returns>
        public ConstructorInfo AsConstructorInfo()
        {
            return MemberInfo as ConstructorInfo;
        }

        /// <summary>
        ///     Возвращает <see cref="EventInfo" /> для текущего члена, если он является событием.
        /// </summary>
        /// <returns>Экземпляр <see cref="EventInfo" />, либо <c>null</c>.</returns>
        public EventInfo AsEventInfo()
        {
            return MemberInfo as EventInfo;
        }

        /// <summary>
        ///     Возвращает <see cref="FieldInfo" /> для текущего члена, если он является полем.
        /// </summary>
        /// <returns>Экземпляр <see cref="FieldInfo" />, либо <c>null</c>.</returns>
        public FieldInfo AsFieldInfo()
        {
            return MemberInfo as FieldInfo;
        }

        /// <summary>
        ///     Возвращает <see cref="MethodInfo" /> для текущего члена, если он является методом.
        /// </summary>
        /// <returns>Экземпляр <see cref="MethodInfo" />, либо <c>null</c>.</returns>
        public MethodInfo AsMethodInfo()
        {
            return MemberInfo as MethodInfo;
        }

        /// <summary>
        ///     Возвращает <see cref="PropertyInfo" /> для текущего члена, если он является свойством.
        /// </summary>
        /// <returns>Экземпляр <see cref="PropertyInfo" />, либо <c>null</c>.</returns>
        public PropertyInfo AsPropertyInfo()
        {
            return MemberInfo as PropertyInfo;
        }

        /// <summary>
        ///     Возвращает <see cref="Type" /> для текущего члена, если он является типом.
        /// </summary>
        /// <returns>Экземпляр <see cref="Type" />, либо <c>null</c>.</returns>
        public Type AsType()
        {
            return MemberInfo as Type;
        }

        #endregion Методы преобразования

        #region Методы поиска членов

        /// <summary>
        ///     Получает конструктор по имени.
        /// </summary>
        /// <param name="methodName">Имя конструктора.</param>
        /// <returns>Экземпляр <see cref="ConstructorInfo" />, либо <c>null</c>.</returns>
        public ConstructorInfo GetConstructor(string methodName)
        {
            return GetMember(methodName, MemberNameType.Name)?.AsConstructorInfo();
        }

        /// <summary>
        ///     Получает конструктор, подходящий для указанных аргументов.
        /// </summary>
        /// <param name="ctorArgs">Аргументы конструктора. Может быть изменён для добавления значений по умолчанию.</param>
        /// <returns>Экземпляр <see cref="ConstructorInfo" />, либо <c>null</c>, если подходящий конструктор не найден.</returns>
        public ConstructorInfo GetConstructorByArgs(ref object[] ctorArgs)
        {
            var args = ctorArgs;
            foreach (var c in Constructors)
            {
                var pAll = c.GetParameters();
                if (pAll.Length == ctorArgs.Length && ctorArgs.All((_, i) =>
                        TypeHelper.IsImplements(args[i]?.GetType(), pAll[i].ParameterType)))
                    return c;
                var pNoDef = c.GetParameters().Where(p => !p.HasDefaultValue).ToArray();

                if (pNoDef.Length == ctorArgs.Length && ctorArgs.All((_, i) =>
                        TypeHelper.IsImplements(args[i]?.GetType(), pNoDef[i].ParameterType)))
                {
                    Array.Resize(ref ctorArgs, pAll.Length);
                    for (var i = pNoDef.Length; i < pAll.Length; i++)
                        ctorArgs[i] = pAll[i].DefaultValue;
                    return c;
                }
            }

            return null;
        }

        /// <summary>
        ///     Получает событие по имени.
        /// </summary>
        /// <param name="eventName">Имя события.</param>
        /// <returns>Экземпляр <see cref="EventInfo" />, либо <c>null</c>.</returns>
        public EventInfo GetEvent(string eventName)
        {
            return GetMember(eventName)?.AsEventInfo();
        }

        /// <summary>
        ///     Получает поле по имени.
        /// </summary>
        /// <param name="fieldName">Имя поля.</param>
        /// <returns>Экземпляр <see cref="FieldInfo" />, либо <c>null</c>.</returns>
        public FieldInfo GetField(string fieldName)
        {
            return GetMember(fieldName)?.AsFieldInfo();
        }

        /// <summary>
        ///     Создаёт экземпляр указанного типа с возможностью передачи аргументов конструктора.
        /// </summary>
        /// <param name="type">Тип, экземпляр которого нужно создать.</param>
        /// <param name="ctorArgs">Аргументы конструктора.</param>
        /// <returns>Созданный экземпляр указанного типа.</returns>
        public static T CreateInstance<T>(Type type, params object[] ctorArgs)
        {
            return (T)CreateInstance(type, ctorArgs);
        }

        /// <summary>
        ///     Создаёт экземпляр указанного типа с возможностью передачи аргументов конструктора.
        /// </summary>
        /// <param name="type">Тип, экземпляр которого нужно создать.</param>
        /// <param name="ctorArgs">Аргументы конструктора.</param>
        /// <returns>Созданный экземпляр указанного типа.</returns>
        public static object CreateInstance(Type type, params object[] ctorArgs)
        {
            if (ctorArgs == null)
                ctorArgs = Array.Empty<object>();

            var typeInfo = Create(type) ?? throw new NullReferenceException(nameof(Create) + ": type is null!");
            if (typeInfo.DefaultConstructor != null && ctorArgs.Length == 0)
                return typeInfo.DefaultConstructor();

            if (typeInfo.IsDelegate)
                return null;

            if (type.IsInterface)
            {
                if (typeInfo.IsCollection)
                {
                    if (!InterfaceToInstanceMap.TryGetValue(type, out var lstType))
                        InterfaceToInstanceMap.TryGetValue(type.GetGenericTypeDefinition(), out lstType);

                    var genericArgs = type.GetGenericArguments();
                    if (genericArgs.Length == 0)
                        genericArgs = new[] { typeof(object) };
                    if (lstType != null && lstType.IsGenericTypeDefinition)
                        lstType = lstType.MakeGenericType(genericArgs);
                    if (lstType != null) return Activator.CreateInstance(lstType);
                }

                throw new NotImplementedException();
            }

            if (type.IsArray)
            {
                if (ctorArgs.Length == 0)
                    return Activator.CreateInstance(type, 0);
                if (ctorArgs.Length == 1 && ctorArgs[0] is int)
                    return Activator.CreateInstance(type, ctorArgs[0]);
                return Activator.CreateInstance(type, ctorArgs.Length);
            }

            if (type.IsEnum) return ctorArgs.FirstOrDefault(x => x?.GetType() == type) ?? TypeHelper.Default(type);

            if (type == typeof(string) && ctorArgs.Length == 0)
                return string.Empty;

            var defaultCtor = typeInfo.DefaultConstructor;
            if (defaultCtor != null && ctorArgs.Length == 0)
                try
                {
                    return defaultCtor();
                }
                catch
                {
                    return TypeHelper.Default(type);
                }

            var ctor = typeInfo.GetConstructorByArgs(ref ctorArgs);

            if (ctor == null && type.IsValueType)
                return TypeHelper.Default(type);

            if (ctor == null)
                throw new InvalidOperationException(
                    $"Не найден конструктор для типа '{type}' с аргументами '{string.Join(",", ctorArgs.Select(arg => arg?.GetType()))}'");

            return ctor.Invoke(ctorArgs);
        }

        /// <summary>
        ///     Получает член по имени с возможностью фильтрации.
        /// </summary>
        /// <param name="name">Имя члена.</param>
        /// <param name="memberNamesType">Тип имен по которым вести поиск</param>
        /// <param name="membersFilter">Фильтр для отбора членов (опционально).</param>
        /// <param name="nameComparison">Сравнение имен</param>
        /// <returns>Экземпляр <see cref="MemberInfoEx" />, либо <c>null</c>, если член не найден.</returns>
        public MemberInfoEx GetMember(string name, MemberNameType memberNamesType = MemberNameType.Any,
            Func<MemberInfoEx, bool> membersFilter = null,
            StringComparison nameComparison = StringComparison.OrdinalIgnoreCase)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            if (_memberCache.TryGetValue(name, out var mx))
                return mx;

            if (memberNamesType == MemberNameType.Any || memberNamesType.HasFlag(MemberNameType.Name))
            {
                // Быстрый поиск свойства
                var quickProp = TypeHelper.GetLowestProperty(Type, name);
                if (quickProp != null)
                {
                    mx = new MemberInfoEx(quickProp);
                    _memberCache[name] = mx;
                    if (membersFilter == null || membersFilter(mx))
                        return mx;
                }

                // Быстрый поиск поля
                var quickField = TypeHelper.GetLowestField(Type, name);
                if (quickField != null)
                {
                    mx = new MemberInfoEx(quickField);
                    _memberCache[name] = mx;
                    if (membersFilter == null || membersFilter(mx))
                        return mx;
                }

                // Быстрый поиск метода
                var quickMethod = TypeHelper.GetLowestMethod(Type, name);
                if (quickMethod != null)
                {
                    mx = new MemberInfoEx(quickMethod);
                    _memberCache[name] = mx;
                    if (membersFilter == null || membersFilter(mx))
                        return mx;
                }

                // Быстрый поиск события
                var quickEvent = TypeHelper.GetLowestEvent(Type, name);
                if (quickEvent != null)
                {
                    mx = new MemberInfoEx(quickEvent);
                    _memberCache[name] = mx;
                    if (membersFilter == null || membersFilter(mx))
                        return mx;
                }
            }

            // Поиск по различным именам (основное имя, отображаемое имя, JSON имя и т.д.)
            var searchNames = new (Func<MemberInfoEx, string> getter, MemberNameType flag)[]
            {
                (x => x.Name, MemberNameType.Name),
                (x => x.DisplayName, MemberNameType.DisplayName),
                (x => x.JsonName, MemberNameType.JsonName),
                (x => x.XmlName, MemberNameType.XmlName),
                (x => x.ColumnName, MemberNameType.ColumnName),
                (x => x.TableName, MemberNameType.TableName),
                (x => x.SchemaName, MemberNameType.SchemaName)
            };

            foreach (var (f, flag) in searchNames)
            {
                if (memberNamesType != MemberNameType.Any && (memberNamesType & flag) == 0)
                    continue;

                // Ищем по совпадению имени
                if (mx == null)
                    mx = Members.FirstOrDefault(x =>
                        f(x)?.Equals(name, nameComparison) == true &&
                        (membersFilter == null || membersFilter(x)));

                // Ищем по совпадению с удалением специальных символов
                if (mx == null)
                    mx = Members.FirstOrDefault(x =>
                        Regex.Replace($"{f(x)}", "[ \\-_\\.]", string.Empty).Equals(
                            Regex.Replace(name, "[ \\-_\\.]", string.Empty),
                            nameComparison) && (membersFilter == null || membersFilter(x)));

                if (mx != null)
                {
                    _memberCache[name] = mx;
                    return mx;
                }
            }

            return null;
        }

        /// <summary>
        ///     Получает метод по имени.
        /// </summary>
        /// <param name="methodName">Имя метода.</param>
        /// <returns>Экземпляр <see cref="MethodInfo" />, либо <c>null</c>.</returns>
        public MethodInfo GetMethod(string methodName)
        {
            return GetMember(methodName)?.AsMethodInfo();
        }

        /// <summary>
        ///     Получает свойство по имени.
        /// </summary>
        /// <param name="propertyName">Имя свойства.</param>
        /// <returns>Экземпляр <see cref="PropertyInfo" />, либо <c>null</c>.</returns>
        public PropertyInfo GetProperty(string propertyName)
        {
            return GetMember(propertyName)?.AsPropertyInfo();
        }

        #endregion Методы поиска членов

        #region Методы работы со значениями

        /// <summary>
        ///     Получает значение члена (свойства, поля или метода) для указанного экземпляра объекта.<br />
        ///     Использует кешированный делегат <see cref="Getter" /> для получения значения, если он доступен.<br />
        ///     Не использует преобразование типов, если тип значения не совпадает с типом свойства, то выдается исключение.<br />
        /// </summary>
        /// <param name="objectInstance">Экземпляр объекта, из которого извлекается значение.</param>
        /// <param name="args">Аргументы для метода, если член является методом.</param>
        /// <returns>Значение члена, либо <c>null</c> в случае ошибки.</returns>
        public object GetValue(object objectInstance, params object[] args)
        {
            if (IsProperty && Getter != null) return Getter(objectInstance);

            if (IsField && Getter != null) return Getter(objectInstance);

            if (IsMethod) return AsMethodInfo()?.Invoke(objectInstance, args);

            return null;
        }

        /// <summary>
        ///     Устанавливает значение свойства или поля для указанного экземпляра объекта.<br />
        ///     Использует кешированный делегат <see cref="Setter" /> для установки значения, если он доступен.<br />
        ///     Не использует преобразование типов, если тип значения не совпадает с типом свойства, то выдается исключение.<br />
        /// </summary>
        /// <param name="objectInstance">Экземпляр объекта, для которого устанавливается значение.</param>
        /// <param name="value">Значение для установки.</param>
        /// <returns><c>true</c>, если значение успешно установлено; иначе <c>false</c>.</returns>
        public bool SetValue(object objectInstance, object value)
        {
            if (IsProperty && Setter != null)
            {
                Setter(objectInstance, value);
                return true;
            }

            if (IsField)
            {
                if (Setter != null)
                    Setter(objectInstance, value);
                else
                    AsFieldInfo().SetValue(objectInstance, value);
                return true;
            }

            return false;
        }

        #endregion Методы работы со значениями

        #region Методы работы с атрибутами

        /// <summary>
        ///     Получает все атрибуты члена.
        /// </summary>
        /// <param name="inherit">Учитывать ли атрибуты из цепочки наследования.</param>
        /// <returns>Массив атрибутов.</returns>
        public override object[] GetCustomAttributes(bool inherit)
        {
            return MemberInfo.GetCustomAttributes(inherit);
        }

        /// <summary>
        ///     Получает атрибуты указанного типа.
        /// </summary>
        /// <param name="attributeType">Тип атрибута.</param>
        /// <param name="inherit">Учитывать ли атрибуты из цепочки наследования.</param>
        /// <returns>Массив атрибутов указанного типа.</returns>
        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            return MemberInfo.GetCustomAttributes(attributeType, inherit);
        }

        /// <summary>
        ///     Проверяет наличие атрибута указанного типа.
        /// </summary>
        /// <typeparam name="T">Тип атрибута.</typeparam>
        /// <param name="filter">Фильтр для отбора атрибутов (опционально).</param>
        /// <returns><c>true</c>, если атрибут найден; иначе <c>false</c>.</returns>
        public bool HasAttribute<T>(Func<T, bool> filter = null) where T : Attribute
        {
            if (filter == null)
                filter = _ => true;
            return Attributes.OfType<T>().Any(filter);
        }

        /// <summary>
        ///     Проверяет наличие атрибута указанного типа по имени.
        /// </summary>
        /// <param name="typeNames">Имя типа</param>
        public bool HasAttributeOfType(params string[] typeNames)
        {
            return Attributes.Any(a => typeNames.Contains(a.GetType().Name));
        }

        /// <summary>
        ///     Проверяет, определён ли атрибут указанного типа.
        /// </summary>
        /// <param name="attributeType">Тип атрибута.</param>
        /// <param name="inherit">Учитывать ли атрибуты из цепочки наследования.</param>
        /// <returns><c>true</c>, если атрибут определён; иначе <c>false</c>.</returns>
        public override bool IsDefined(Type attributeType, bool inherit)
        {
            return MemberInfo.IsDefined(attributeType, inherit);
        }

        #endregion Методы работы с атрибутами

        #region Методы работы с ORM

        private MemberInfoEx[] _columns;

        /// <summary>
        ///     Получает коллекцию простых публичных свойств, которые подходят для ORM колонок.<br />
        /// </summary>
        /// <returns>Массив <see cref="MemberInfoEx" /> для колонок.</returns>
        public MemberInfoEx[] GetColumns()
        {
            if (_columns != null)
                return _columns;

            _columns = Members.Where(x => x.HasAttributeOfType("ColumnAttribute", "KeyAttribute", "ForeignKeyAttribute"))
                .ToArray();

            if (_columns.Length > 0)
                return _columns;

            _columns = Members.Where(x =>
                    x.IsProperty &&
                    x.IsPublic &&
                    x.IsBasic &&
                    !x.IsCollection &&
                    x.Attributes.All(a => a.GetType().Name != "NotMappedAttribute"))
                .ToArray();

            return _columns;
        }

        /// <summary>
        ///     Получает коллекцию свойств, представляющих таблицы (коллекции сложных типов без атрибута NotMapped).
        /// </summary>
        /// <returns>Массив <see cref="MemberInfoEx" /> для таблиц.</returns>
        public MemberInfoEx[] GetTables()
        {
            return Members.Where(x =>
                x.IsProperty &&
                x.IsPublic &&
                x.IsCollection &&
                !x.IsBasicCollection &&
                x.Attributes.All(a => a.GetType().Name != "NotMappedAttribute")).ToArray();
        }

        #endregion Методы работы с ORM

        #region Внутренние методы

        /// <summary>
        ///     Получить все члены типа (свойства, поля, методы и т.д.)
        /// </summary>
        /// <returns>Массив информации о членах</returns>
        internal MemberInfoEx[] GetMembersInternal()
        {
            return Properties.Select(p => new MemberInfoEx(p)).Concat(
                Fields.Select(x => new MemberInfoEx(x))).Concat(
                Methods.Select(x => new MemberInfoEx(x))).Concat(
                Constructors.Select(x => new MemberInfoEx(x))).Concat(
                Events.Select(x => new MemberInfoEx(x))).ToArray();
        }

        /// <summary>
        ///     Получить атрибуты члена и его базовых типов
        /// </summary>
        /// <returns>Массив атрибутов</returns>
        private Attribute[] GetAttributes()
        {
            _attributes = MemberInfo.GetCustomAttributes().Concat(BaseTypes.SelectMany(x => x.GetCustomAttributes()))
                .ToArray();
            return _attributes;
        }

        /// <summary>
        ///     Получить конструкторы типа
        /// </summary>
        /// <returns>Массив информации о конструкторах</returns>
        private ConstructorInfo[] GetConstructors()
        {
            _constructors = _type.GetConstructors(DefaultBindingFlags);
            return _constructors;
        }

        /// <summary>
        ///     Получить события типа
        /// </summary>
        /// <returns>Массив информации о событиях</returns>
        private EventInfo[] GetEvents()
        {
            _events = _type.GetEvents(DefaultBindingFlags);
            return _events;
        }

        /// <summary>
        ///     Получить поля типа и его базовых типов
        /// </summary>
        /// <returns>Массив информации о полях</returns>
        private FieldInfo[] GetFields()
        {
            _fields = _type.GetFields(DefaultBindingFlags)
                .Concat(BaseTypes.SelectMany(x => x.GetFields(DefaultBindingFlags))).ToArray();
            return _fields;
        }

        /// <summary>
        ///     Получить методы типа
        /// </summary>
        /// <returns>Массив информации о методах</returns>
        private MethodInfo[] GetMethods()
        {
            _methods = _type.GetMethods(DefaultBindingFlags);
            return _methods;
        }

        /// <summary>
        ///     Получить свойства типа и его базовых типов (кроме интерфейсов)
        /// </summary>
        /// <returns>Массив информации о свойствах</returns>
        private PropertyInfo[] GetProperties()
        {
            var props = _type.GetProperties(DefaultBindingFlags)
                .Concat(BaseTypes.Where(x => !x.IsInterface)
                    .SelectMany(x => x.GetProperties(DefaultBindingFlags)))
                .ToList();
            var l = new Dictionary<string, PropertyInfo>();
            foreach (var p in props)
                if (!l.ContainsKey(p.Name))
                    l.Add(p.Name, p);

            _properties = l.Values.ToArray();
            return _properties;
        }

        #endregion Внутренние методы
    }

    public static class MemberInfoExtensions
    {
        /// <summary>
        ///     Получить расширенную информацию о члене класса
        /// </summary>
        /// <param name="memberInfo">Информация о члене класса</param>
        /// <returns>Расширенная информация о члене класса</returns>
        public static MemberInfoEx GetMemberInfoEx(this MemberInfo memberInfo)
        {
            return MemberInfoEx.Create(memberInfo);
        }
    }

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
        ///     Любой тип имени (основное, отображаемое, JSON, XML и др.).
        /// </summary>
        Any = 0,

        /// <summary>
        ///     Основное имя члена (Name).
        /// </summary>
        Name = 1,

        /// <summary>
        ///     Отображаемое имя (DisplayName).
        /// </summary>
        DisplayName = 2,

        /// <summary>
        ///     Имя в JSON (JsonName).
        /// </summary>
        JsonName = 4,

        /// <summary>
        ///     Имя в XML (XmlName).
        /// </summary>
        XmlName = 8,

        /// <summary>
        ///     Имя колонки (ColumnName).
        /// </summary>
        ColumnName = 16,

        /// <summary>
        ///     Имя таблицы (TableName).
        /// </summary>
        TableName = 32,

        /// <summary>
        ///     Имя схемы (SchemaName).
        /// </summary>
        SchemaName = 64
    }
}