using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using RuntimeStuff.Extensions;
using RuntimeStuff.Helpers;

namespace RuntimeStuff
{
    /// <summary>
    ///     v.2025.12.18 (RS) <br/>
    ///     Представляет расширенную обёртку над <see cref="MemberInfo" />, предоставляющую унифицированный доступ к
    ///     дополнительной информации и операциям для членов типа .NET<br />
    ///     (свойств, методов, полей, событий, конструкторов и самих типов).
    ///     Класс предназначен для использования в сценариях динамического анализа типов, построения универсальных
    ///     сериализаторов, ORM, генераторов кода, UI-редакторов и других задач,<br />
    ///     где требуется расширенная работа с метаданными .NET.
    ///     <para>
    ///         Класс <c>TypeCache</c> позволяет:
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
    public class TypeCache : MemberInfo
    {
        // Кэш для расширенной информации о членах класса
        private static readonly ConcurrentDictionary<MemberInfo, TypeCache> MemberInfoCache =
            new ConcurrentDictionary<MemberInfo, TypeCache>();

        // Кэш делегатов для создания экземпляров типов
        private static readonly ConcurrentDictionary<string, Func<object>> ConstructorsCache =
            new ConcurrentDictionary<string, Func<object>>();

        // Кэш для расширенной информации о членах класса по имени
        private readonly ConcurrentDictionary<string, TypeCache> _memberCache =
            new ConcurrentDictionary<string, TypeCache>();

        private readonly TypeCache _typeCache;

        // Кэш для делегатов установки и получения значений членов
        private readonly ConcurrentDictionary<string, Action<object, object>[]> _setters = new ConcurrentDictionary<string, Action<object, object>[]>(StringComparison.OrdinalIgnoreCase.ToStringComparer());

        // Кэш для делегатов получения значений членов
        private readonly ConcurrentDictionary<string, Func<object, object>[]> _getters = new ConcurrentDictionary<string, Func<object, object>[]>(StringComparison.OrdinalIgnoreCase.ToStringComparer());

        private readonly Type _type;

        internal readonly MemberInfo MemberInfo;

        private Attribute[] _attributes;

        private Type[] _baseTypes;

        private ConstructorInfo[] _constructors;

        private EventInfo[] _events;

        private FieldInfo[] _fields;

        private string _jsonName;

        private Dictionary<string, TypeCache> _members;

        private MethodInfo[] _methods;

        private TypeCache _parent;

        private PropertyInfo[] _properties;

        private string _xmlAttr;

        private string _xmlElem;

        public TypeCache(MemberInfo memberInfo) : this(memberInfo, getMembers: memberInfo is Type)
        {
        }

        /// <summary>
        ///     Конструктор для создания расширенной информации о члене класса
        /// </summary>
        /// <param name="memberInfo">Информация о члене класса</param>
        /// <param name="getMembers">Получить информацию о дочерних членах: свойства, поля, методы и т.п.</param>
        private TypeCache(MemberInfo memberInfo, bool getMembers = false, TypeCache parent = null)
        {
            Parent = parent;

            _typeCache = memberInfo as TypeCache;
            if (_typeCache != null)
                memberInfo = _typeCache.MemberInfo;

            MemberInfo = memberInfo;

            // Определяем тип члена класса
            var t = MemberInfo as Type;
            var pi = MemberInfo as PropertyInfo;
            var fi = MemberInfo as FieldInfo;
            var mi = MemberInfo as MethodInfo;
            var ci = MemberInfo as ConstructorInfo;
            var mx = MemberInfo as TypeCache;
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
                   throw new NotSupportedException($"{nameof(TypeCache)}: ({memberInfo.GetType().Name}) not supported!");
            IsDictionary = _typeCache?.IsDictionary ?? TypeHelper.IsDictionary(_type);
            IsDelegate = _typeCache?.IsDelegate ?? TypeHelper.IsDelegate(_type);
            IsFloat = _typeCache?.IsFloat ?? TypeHelper.IsFloat(_type);
            IsNullable = _typeCache?.IsNullable ?? TypeHelper.IsNullable(_type);
            IsNumeric = _typeCache?.IsNumeric ?? TypeHelper.IsNumeric(_type);
            IsBoolean = _typeCache?.IsBoolean ?? TypeHelper.IsBoolean(_type);
            IsCollection = _typeCache?.IsCollection ?? TypeHelper.IsCollection(_type);
            ElementType = IsCollection ? _typeCache?.ElementType ?? TypeHelper.GetCollectionItemType(Type) : null;
            IsBasic = _typeCache?.IsBasic ?? TypeHelper.IsBasic(_type);
            IsBasicCollection = _typeCache?.IsBasicCollection ?? (IsCollection && TypeHelper.IsBasic(ElementType));
            IsObject = _typeCache?.IsObject ?? _type == typeof(object);
            IsTuple = _typeCache?.IsTuple ?? TypeHelper.IsTuple(_type);
            IsProperty = pi != null;
            IsEvent = e != null;
            IsField = fi != null;
            IsType = t != null;
            IsMethod = mi != null;
            IsConstructor = ci != null;
            CanWrite = pi != null ? pi.CanWrite : fi != null;
            CanRead = pi != null ? pi.CanRead : fi != null;
            IsPublic = _typeCache?.IsPublic ?? 
                        IsProperty ? AsPropertyInfo().GetAccessors().Any(m => m.IsPublic) :
                        IsField ? AsFieldInfo().IsPublic :
                        IsMethod ? AsMethodInfo().IsPublic : 
                        IsConstructor ? AsConstructorInfo().IsPublic : 
                        IsEvent || Type.IsPublic;
            IsPrivate = _typeCache?.IsPrivate ?? 
                        IsProperty ? AsPropertyInfo().GetAccessors().Any(m => m.IsPrivate) :
                        IsField ? AsFieldInfo().IsPrivate :
                        IsMethod ? AsMethodInfo().IsPrivate : 
                        IsConstructor ? AsConstructorInfo().IsPrivate :
                        IsType ? !Type.IsPublic :
                        !IsEvent
                        ;

            Attributes = GetAttributes().ToDictionary(x => x.GetType().Name);
            Events = GetEvents().ToDictionary(x => x.Name);

            // Обработка имени
            Name = _typeCache?.Name ?? MemberInfo.Name.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries)
                .LastOrDefault() ?? string.Empty;

            // Дополнительная обработка для типов
            if (IsType)
            {
                // Получение атрибутов
                Description = _typeCache?.Description ?? MemberInfo.GetCustomAttributes<DescriptionAttribute>().FirstOrDefault()?.Description;
                DisplayName = _typeCache?.DisplayName ?? MemberInfo.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName;

                DefaultConstructor = _typeCache?.DefaultConstructor ?? CreateConstructorDelegate(t);

                if (_typeCache == null)
                {
                    var tblAttr = Attributes.GetValueOrDefault("TableAttribute");
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
                    TableName = _typeCache.TableName;
                    SchemaName = _typeCache.SchemaName;
                }

                Properties = _typeCache?.Properties ?? Members.Values.Where(x => x.IsProperty).ToDictionary(x => x.Name);
                PublicProperties = _typeCache?.PublicProperties ?? Properties.Values.Where(x => x.IsPublic).ToDictionary(x => x.Name);
                PrivateProperties = _typeCache?.PrivateProperties ?? Properties.Values.Where(x => x.IsPrivate).ToDictionary(x => x.Name);
                PublicBasicProperties = _typeCache?.PublicBasicProperties ?? PublicProperties.Values.Where(x => x.IsBasic).ToDictionary(x => x.Name);
                PublicBasicEnumerableProperties = _typeCache?.PublicBasicEnumerableProperties ?? PublicProperties.Values.Where(x => x.IsBasicCollection).ToDictionary(x => x.Name);
                PublicEnumerableProperties = _typeCache?.PublicEnumerableProperties ?? PublicProperties.Values.Where(x => x.IsCollection && !x.IsBasicCollection).ToDictionary(x => x.Name);

                Fields = _typeCache?.Fields ?? Members.Values.Where(x => x.IsField).ToDictionary(x => x.Name);
                PublicFields = _typeCache?.PublicFields ?? Fields.Values.Where(x => x.IsPublic).ToDictionary(x => x.Name);
                PrivateFields = _typeCache?.PrivateFields ?? Fields.Values.Where(x => x.IsPrivate).ToDictionary(x => x.Name);

                PrimaryKeys = _typeCache?.PrimaryKeys ??
                              PublicBasicProperties.Where(x => x.Value.Attributes.ContainsKey("KeyAttribute"))
                                  .Select(x => x.Value)
                                  .ToDictionary(x => x.Name);

                if (PrimaryKeys.Count == 0)
                {
                    var p = PublicBasicProperties.GetValueOrDefault("id", StringComparison.OrdinalIgnoreCase.ToStringComparer()) ?? PublicBasicProperties.GetValueOrDefault(TableName + "id", StringComparer.OrdinalIgnoreCase);
                    if (p != null)
                        PrimaryKeys = new Dictionary<string, TypeCache>() { { p.Name, PublicBasicProperties[p.Name] } };
                }

                ForeignKeys = _typeCache?.ForeignKeys ?? PublicBasicProperties
                    .Where(x => x.Value.Attributes.ContainsKey("ForeignKeyAttribute"))
                    .Select(x => x.Value)
                    .ToDictionary(x => x.Name);

                ColumnProperties = _typeCache?.ColumnProperties ?? PublicBasicProperties.Where(x =>
                        !x.Value.IsPrimaryKey
                        && x.Value.Attributes.ContainsKey("ColumnAttribute")
                        && !x.Value.Attributes.ContainsKey("NotMappedAttribute")
                    ).Select(x => x.Value)
                    .ToDictionary(x => x.Name);

                if (ColumnProperties.Count == 0)
                    ColumnProperties = PublicBasicProperties.Where(x => !x.Value.IsPrimaryKey)
                        .Select(x => x.Value)
                        .ToDictionary(x => x.Name);

                var propsAndFields = Members.Where(x => x.Value.IsProperty || x.Value.IsField).GroupBy(x => x.Key, StringComparison.OrdinalIgnoreCase.ToStringComparer());
                foreach (var pf in propsAndFields)
                {
                    _setters[pf.Key] = pf.Select(x => x.Value.Setter).ToArray();
                    _getters[pf.Key] = pf.Select(x => x.Value.Getter).ToArray();
                }

                // Рекурсивная загрузка членов класса
                if (getMembers) _members = _typeCache?._members ?? GetChildMembersInternal();
            }

            // Дополнительная обработка для свойств
            if (pi != null)
            {
                PropertyType = pi.PropertyType;
                IsSetterPublic = pi.GetSetMethod()?.IsPublic == true;
                IsSetterPrivate = pi.GetSetMethod()?.IsPrivate == true;
                IsGetterPublic = pi.GetGetMethod()?.IsPublic == true;
                IsGetterPrivate = pi.GetGetMethod()?.IsPrivate == true;

                if (_typeCache == null)
                {
                    var keyAttr = Attributes.GetValueOrDefault("KeyAttribute");
                    var colAttr = Attributes.GetValueOrDefault("ColumnAttribute");
                    var fkAttr = Attributes.GetValueOrDefault("ForeignKeyAttribute");
                    IsPrimaryKey = keyAttr != null || string.Equals(Name, "id", StringComparison.OrdinalIgnoreCase);
                    IsForeignKey = fkAttr != null;

                    Setter = TypeHelper.GetMemberSetter(pi.Name, pi.DeclaringType);
                    Getter = TypeHelper.GetMemberGetter(pi.Name, pi.DeclaringType);
                    PropertyBackingField = GetFields().FirstOrDefault(x => x.Name == $"<{Name}>k__BackingField") ??
                                           TypeHelper.GetFieldInfoFromGetAccessor(pi.GetGetMethod(true));

                    //TableName = Parent.TableName;
                    ColumnName = colAttr != null
                        ? colAttr.GetType().GetProperty("Name")?.GetValue(colAttr)?.ToString() ?? Name
                        : Name;

                    ForeignColumnName = fkAttr?.GetType().GetProperty("Name")?.GetValue(fkAttr)?.ToString() ?? string.Empty;
                }
                else
                {
                    Setter = _typeCache.Setter;
                    Getter = _typeCache.Getter;
                    PropertyBackingField = _typeCache.PropertyBackingField;
                    ColumnName = _typeCache.ColumnName;
                    ForeignColumnName = _typeCache.ForeignColumnName;
                    IsPrimaryKey = _typeCache.IsPrimaryKey;
                    IsForeignKey = _typeCache.IsForeignKey;
                }
            }

            if (fi != null)
            {
                IsSetterPublic = true;
                IsSetterPrivate = false;
                IsGetterPublic = true;
                IsGetterPrivate = false;

                Setter = _typeCache?.Setter ?? TypeHelper.GetMemberSetter(fi.Name, fi.DeclaringType);
                Getter = _typeCache?.Getter ?? TypeHelper.GetMemberGetter(fi.Name, fi.DeclaringType);
            }

            if (_typeCache == null)
            {
                var displayAttr = Attributes.GetValueOrDefault("DisplayAttribute");
                if (displayAttr == null) return;
                var groupNameProp = displayAttr.GetType().GetProperty("GroupName");
                if (groupNameProp != null)
                    GroupName = groupNameProp.GetValue(displayAttr)?.ToString() ?? DisplayName;
            }
            else
            {
                GroupName = _typeCache.GroupName;
            }
        }

        public bool IsGetterPrivate { get; }

        public bool IsGetterPublic { get; }

        public bool IsSetterPrivate { get; }

        public bool IsSetterPublic { get; }

        public Dictionary<string, TypeCache> PrivateFields { get; }

        public Dictionary<string, TypeCache> PublicFields { get; }

        public Dictionary<string, TypeCache> PrivateProperties { get; }

        public Dictionary<string, TypeCache> Fields { get; }

        public Type PropertyType { get; }

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
            return $"{(DeclaringType == null ? "" : $"{DeclaringType.Name}.")}{Name} ({Type.Name})";
        }

        #endregion Вспомогательные методы

        #region Основные свойства

        /// <summary>
        ///     Атрибуты члена класса
        /// </summary>
        public Dictionary<string, Attribute> Attributes { get; }

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
        ///     Имя внешнего ключа (из атрибута ForeignKeyAttribute)
        /// </summary>
        public string ForeignColumnName { get; }

        /// <summary>
        ///     Свойства, помеченные атрибутом ForeignKeyAttribute
        /// </summary>
        public Dictionary<string, TypeCache> ForeignKeys { get; }

        /// <summary>
        ///     Делегат для получения значения свойства
        /// </summary>
        private Func<object, object> Getter { get; }

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

                if (_typeCache == null)
                {
                    _jsonName = "";
                    var jsonAttr = GetAttributes().FirstOrDefault(x => x.GetType().Name.StartsWith("Json"));
                    if (jsonAttr == null) return _jsonName;
                    var propName = jsonAttr.GetType().GetProperties().FirstOrDefault(p => p.Name.EndsWith("Name"));
                    if (propName != null)
                        _jsonName = propName.GetValue(jsonAttr)?.ToString();
                }
                else
                {
                    _jsonName = _typeCache._jsonName;
                }

                return _jsonName;
            }
        }

        /// <summary>
        ///     Все члены типа (свойства, поля, методы, события)
        /// </summary>
        public Dictionary<string, TypeCache> Members
        {
            get
            {
                if (_members != null)
                    return _members;
                _members = _typeCache?._members ?? GetChildMembersInternal();
                return _members;
            }
        }

        /// <summary>
        ///     Тип члена (свойство, метод, поле и т.д.)
        /// </summary>
        public override MemberTypes MemberType => MemberInfo.MemberType;

        /// <summary>
        /// Словарь событий типа
        /// </summary>
        public Dictionary<string, EventInfo> Events { get; }

        /// <summary>
        ///     Имя члена
        /// </summary>
        public sealed override string Name { get; }

        /// <summary>
        ///     Родительский член (для вложенных членов)
        /// </summary>
        public TypeCache Parent
        {
            get
            {
                //if (_parent != null)
                //    return _parent;

                //if (MemberInfo.DeclaringType != null)
                //    _parent = _typeCache?._parent ?? Create(MemberInfo.DeclaringType);

                return _parent;
            }

            private set  => _parent = value;
        }

        /// <summary>
        ///     Свойства, помеченные атрибутом KeyAttribute. Если таких нет, то ищется сначала "EventId", потом ИмяТаблицыId
        /// </summary>
        public Dictionary<string, TypeCache> PrimaryKeys { get; }

        /// <summary>
        ///     Поле, хранящее значение свойства (для автоматически реализуемых свойств)
        /// </summary>
        public FieldInfo PropertyBackingField { get; }

        /// <summary>
        ///     Словарь публичных свойств-коллекций с элементом списка {T}, где T - простой тип <see cref="BaseTypes" /> (IsPublic && IsProperty && IsBasicCollection)
        /// </summary>
        public Dictionary<string, TypeCache> PublicBasicEnumerableProperties
        {
            get;
        }

        /// <summary>
        ///     Словарь публичных свойств-коллекций с элементом списка {T}, где T : class (IsPublic && IsProperty && IsCollection && !IsBasicCollection)
        /// </summary>
        public Dictionary<string, TypeCache> PublicEnumerableProperties
        {
            get;
        }

        /// <summary>
        ///     Словарь простых (<see cref="TypeHelper.BasicTypes" />) публичных свойств типа (IsPublic && IsProperty && IsBasic)
        /// </summary>
        public Dictionary<string, TypeCache> PublicBasicProperties
        {
            get;
        }

        /// <summary>
        ///     Словарь свойств по имени колонки у которых есть один из атрибутов Column, Foreign и нет NotMapped и Key. Если таких нет, то все простые публичные свойства кроме первичных ключей
        /// </summary>
        public Dictionary<string, TypeCache> ColumnProperties
        {
            get;
        }

        /// <summary>
        ///     Словарь публичных свойств типа (IsPublic && IsProperty)
        /// </summary>
        public Dictionary<string, TypeCache> PublicProperties
        {
            get;
            private set;
        }

        /// <summary>
        ///     Словарь всех свойств типа (IsProperty)
        /// </summary>
        public Dictionary<string, TypeCache> Properties
        {
            get;
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
        private Action<object, object> Setter { get; }

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

                if (_typeCache == null)
                {
                    var xmlAttrs = Attributes.Where(x => x.GetType().Name.StartsWith("Xml")).ToArray();
                    if (xmlAttrs.Any())
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
                else
                {
                    _xmlAttr = _typeCache._xmlAttr;
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

                if (_typeCache == null)
                {
                    var xmlAttrs = Attributes.Where(x => x.GetType().Name.StartsWith("Xml")).ToArray();
                    if (xmlAttrs.Any())
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

                    if (_xmlElem == null)
                        _xmlElem = string.Empty;
                }
                else
                {
                    _xmlElem = _typeCache._xmlElem;
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
            get => GetValue(source, memberName);

            set => SetValue(source, memberName, value);
        }

        /// <summary>
        ///     Получить член по имени
        /// </summary>
        /// <param name="memberName">Имя члена для поиска</param>
        /// <returns>Найденный член или null, если не найден</returns>
        public TypeCache this[string memberName] => this[memberName, MemberNameType.Any];

        /// <summary>
        ///     Получить член по имени с фильтрацией
        /// </summary>
        /// <param name="memberName">Имя члена для поиска</param>
        /// <param name="memberNameType">Тип имени члена для поиска</param>
        /// <param name="memberFilter">Фильтр для отбора членов</param>
        /// <returns>Найденный член или null, если не найден</returns>
        public TypeCache this[string memberName, MemberNameType memberNameType = MemberNameType.Any,
            Func<TypeCache, bool> memberFilter = null] => GetMember(memberName, memberNameType, memberFilter);

        #endregion Индексаторы

        #region Статические методы

        /// <summary>
        ///     Создать расширенную информацию о члене класса (с кэшированием)
        /// </summary>
        /// <returns>Расширенная информация о члене класса</returns>
        public static TypeCache Create<T>()
        {
            return Create(typeof(T));
        }

        /// <summary>
        ///     Создать расширенную информацию о члене класса (с кэшированием)
        /// </summary>
        /// <param name="memberInfo">Информация о члене класса</param>
        /// <returns>Расширенная информация о члене класса</returns>
        public static TypeCache Create(MemberInfo memberInfo)
        {
            if (memberInfo is TypeCache me)
                return me;

            var result = memberInfo == null ? null : MemberInfoCache.GetOrAdd(memberInfo, x => new TypeCache(x, x is Type));

            return result;
        }

        private static TypeCache Create(MemberInfo memberInfo, TypeCache parent)
        {
            if (memberInfo is TypeCache me)
                return me;

            var result = memberInfo == null ? null : MemberInfoCache.GetOrAdd(memberInfo, x => new TypeCache(x, x is Type, parent));

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
            foreach (var c in GetConstructors())
            {
                var pAll = c.GetParameters();
                if (pAll.Length == ctorArgs.Length && All(ctorArgs, (_, i) =>
                        TypeHelper.IsImplements(args[i]?.GetType(), pAll[i].ParameterType)))
                    return c;
                var pNoDef = c.GetParameters().Where(p => !p.HasDefaultValue).ToArray();

                if (pNoDef.Length == ctorArgs.Length && All(ctorArgs, (_, i) =>
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
        /// <returns>Экземпляр <see cref="TypeCache" />, либо <c>null</c>, если член не найден.</returns>
        public TypeCache GetMember(string name, MemberNameType memberNamesType = MemberNameType.Any,
            Func<TypeCache, bool> membersFilter = null,
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
                    mx = new TypeCache(quickProp);
                    _memberCache[name] = mx;
                    if (membersFilter == null || membersFilter(mx))
                        return mx;
                }

                // Быстрый поиск поля
                var quickField = TypeHelper.GetLowestField(Type, name);
                if (quickField != null)
                {
                    mx = new TypeCache(quickField);
                    _memberCache[name] = mx;
                    if (membersFilter == null || membersFilter(mx))
                        return mx;
                }

                // Быстрый поиск метода
                var quickMethod = TypeHelper.GetLowestMethod(Type, name);
                if (quickMethod != null)
                {
                    mx = new TypeCache(quickMethod);
                    _memberCache[name] = mx;
                    if (membersFilter == null || membersFilter(mx))
                        return mx;
                }

                // Быстрый поиск события
                var quickEvent = TypeHelper.GetLowestEvent(Type, name);
                if (quickEvent != null)
                {
                    mx = new TypeCache(quickEvent);
                    _memberCache[name] = mx;
                    if (membersFilter == null || membersFilter(mx))
                        return mx;
                }
            }

            // Поиск по различным именам (основное имя, отображаемое имя, JSON имя и т.д.)
            var searchNames = new (Func<TypeCache, string> getter, MemberNameType flag)[]
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
                    mx = Members.Values.FirstOrDefault(x =>
                        f(x)?.Equals(name, nameComparison) == true &&
                        (membersFilter == null || membersFilter(x)));

                // Ищем по совпадению с удалением специальных символов
                if (mx == null)
                    mx = Members.Values.FirstOrDefault(x =>
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

        private TypeCache[] _columns;
        private TypeCache[] _tables;
        private TypeCache[] _pks;
        private TypeCache[] _fks;

        /// <summary>
        /// Получает коллекцию простых публичных свойств типа, которые могут использоваться как колонки ORM.
        /// </summary>
        /// <returns>Массив <see cref="TypeCache"/>, представляющий свойства, подходящие для колонок базы данных.</returns>
        /// <remarks>
        /// Метод сначала ищет свойства, помеченные атрибутами <c>ColumnAttribute</c>, <c>KeyAttribute</c> или <c>ForeignKeyAttribute</c>.
        /// Если такие свойства не найдены, возвращаются все публичные простые свойства, не являющиеся коллекциями и не помеченные как <c>NotMappedAttribute</c>.
        /// Результат кэшируется в поле <c>_columns</c> для повторного использования.
        /// </remarks>
        public TypeCache[] GetColumns()
        {
            if (_columns != null)
                return _columns;

            _columns = Members.Values.Where(x => x.HasAttributeOfType("ColumnAttribute", "KeyAttribute", "ForeignKeyAttribute"))
                .ToArray();

            if (_columns.Length > 0)
                return _columns;

            _columns = Members.Values.Where(x =>
                    x.IsProperty &&
                    x.IsPublic &&
                    x.IsBasic &&
                    !x.IsCollection &&
                    x.Attributes.All(a => a.GetType().Name != "NotMappedAttribute"))
                .ToArray();

            return _columns;
        }

        /// <summary>
        /// Получает коллекцию свойств, которые являются первичными ключами.
        /// </summary>
        /// <returns>Массив <see cref="TypeCache"/>, представляющий первичные ключи.</returns>
        /// <remarks>
        /// Результат кэшируется в поле <c>_pks</c> для повторного использования.
        /// </remarks>
        public TypeCache[] GetPrimaryKeys()
        {
            if (_pks != null)
                return _pks;

            _pks = Members.Values.Where(x => x.IsPrimaryKey)
                .ToArray();

            return _pks;
        }

        /// <summary>
        /// Получает коллекцию свойств, которые являются внешними ключами.
        /// </summary>
        /// <returns>Массив <see cref="TypeCache"/>, представляющий внешние ключи.</returns>
        /// <remarks>
        /// Результат кэшируется в поле <c>_fks</c> для повторного использования.
        /// </remarks>
        public TypeCache[] GetForeignKeys()
        {
            if (_fks != null)
                return _fks;

            _fks = Members.Values.Where(x => x.IsForeignKey)
                .ToArray();

            return _fks;
        }

        /// <summary>
        ///     Получает коллекцию свойств, представляющих таблицы (коллекции сложных типов без атрибута NotMapped).
        /// </summary>
        /// <returns>Массив <see cref="TypeCache" /> для таблиц.</returns>
        public TypeCache[] GetTables()
        {
            if (_tables != null)
                return _tables;

            _tables = Members.Values.Where(x =>
                x.IsProperty &&
                x.IsPublic &&
                x.IsCollection &&
                !x.IsBasicCollection &&
                x.Attributes.All(a => a.GetType().Name != "NotMappedAttribute")).ToArray();

            return _tables;
        }

        #endregion Методы работы с ORM

        #region Внутренние методы

        /// <summary>
        ///     Получить все члены типа (свойства, поля, события)
        /// </summary>
        /// <returns>Массив информации о членах</returns>
        internal Dictionary<string, TypeCache> GetChildMembersInternal()
        {
            var members =
                GetProperties().Concat(
                    GetFields().Concat(
                        GetEvents().OfType<MemberInfo>()))
                    .Select(m => Create(m, this))
                    .ToArray();

            return members.ToDictionary(x => x.Name);
        }

        /// <summary>
        ///     Получить атрибуты члена и его базовых типов
        /// </summary>
        /// <returns>Массив атрибутов</returns>
        public Attribute[] GetAttributes()
        {
            if (_attributes != null)
                return _attributes;

            _attributes = MemberInfo
                .GetCustomAttributes()
                .Concat(BaseTypes.SelectMany(x => x.GetCustomAttributes()))
                .Distinct()
                .DistinctBy(x => x.GetType().Name)
                .ToArray();

            return _attributes;
        }

        /// <summary>
        /// Устанавливает значение члена для указанного объекта.
        /// </summary>
        /// <param name="source">Объект, для которого устанавливается значение.</param>
        /// <param name="value">Значение, которое нужно установить.</param>
        public virtual void SetValue(object source, object value)
        {
            Setter(source, value);
        }

        /// <summary>
        /// Устанавливает значение члена для указанного объекта.
        /// </summary>
        /// <param name="source">Объект, для которого устанавливается значение.</param>
        /// <param name="memberName"></param>
        /// <param name="value">Значение, которое нужно установить.</param>
        public virtual void SetValue(object source, string memberName, object value)
        {
            if (!_setters.TryGetValue(memberName, out var setters))
                return;
            foreach (var s in setters)
            {
                s(source, value);
            }
        }

        /// <summary>
        /// Извлекает значения указанного члена из заданного объекта источника.
        /// </summary>
        /// <remarks>Если для указанного имени члена определено несколько геттеров, возвращаются значения
        /// всех соответствующих членов. Если член не найден, возвращается null.</remarks>
        /// <param name="source">Объект, из которого требуется получить значения члена. Не может быть равен null.</param>
        /// <param name="memberName">Имя члена, значения которого необходимо получить. Чувствительно к регистру.</param>
        /// <returns>Массив объектов, содержащий значения указанного члена. Возвращает null, если член с заданным именем не
        /// найден.</returns>
        public virtual object GetValue(object source, string memberName)
        {
            if (!_getters.TryGetValue(memberName, out var getters))
                return null;

            var values = new List<object>();
            foreach (var g in getters)
            {
                values.Add(g(source));
            }
            return values.ToArray();
        }

        private object _typedSetter1;

        /// <summary>
        /// Устанавливает значение члена для объекта заданного типа <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Тип объекта, для которого создается сеттер.</typeparam>
        /// <param name="instance">Объект, для которого устанавливается значение.</param>
        /// <param name="value">Значение, которое нужно установить.</param>
        public virtual void SetValue<T>(T instance, object value)
        {
            if (_typedSetter1 == null)
                _typedSetter1 = TypeHelper.GetMemberSetter<T>(Name);

            ((Action<T, object>)_typedSetter1)(instance, value);
        }

        /// <summary>
        /// Получает значение члена указанного объекта.
        /// </summary>
        /// <param name="instance">Объект, значение которого нужно получить.</param>
        /// <returns>Значение члена.</returns>
        public object GetValue(object instance)
        {
            return Getter(instance);
        }

        /// <summary>
        /// Получает значение члена указанного объекта и приводит его к типу <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Тип, к которому нужно привести значение.</typeparam>
        /// <param name="instance">Объект, значение которого нужно получить.</param>
        /// <returns>Значение члена, приведённое к типу <typeparamref name="T"/>.</returns>
        public T GetValue<T>(object instance)
        {
            return TypeHelper.ChangeType<T>(Getter(instance));
        }

        /// <summary>
        /// Создать словарь из имен свойств и их значений.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="instance"></param>
        /// <param name="propertyNames">Имена свойств, если не указаны, то берутся <see cref="PublicProperties"/></param>
        /// <returns></returns>
        public Dictionary<string, object> ToDictionary<T>(T instance, params string[] propertyNames) where T : class
        {
            var dic = new Dictionary<string, object>();

            ToDictionary(instance, dic, propertyNames);

            return dic;
        }

        /// <summary>
        /// Записать в словарь значения свойств объекта.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="instance"></param>
        /// <param name="propertyNames">Имена свойств, если не указаны, то берутся <see cref="PublicProperties"/></param>
        /// <returns></returns>
        public void ToDictionary<T>(T instance, Dictionary<string, object> dictionary, params string[] propertyNames) where T : class
        {
            var props = propertyNames.Any()
                ? PublicBasicProperties.Where(x => propertyNames.Contains(x.Key)).Select(x => x.Value).ToArray()
                : PublicBasicProperties.Select(x => x.Value).ToArray();

            foreach (var mi in props)
            {
                dictionary[mi.Name] = mi.GetValue(instance);
            }
        }

        /// <summary>
        ///     Получить конструкторы типа
        /// </summary>
        /// <returns>Массив информации о конструкторах</returns>
        public ConstructorInfo[] GetConstructors()
        {
            if (_constructors != null)
                return _constructors;

            _constructors = _type.GetConstructors(DefaultBindingFlags);
            return _constructors;
        }

        /// <summary>
        ///     Получить события типа
        /// </summary>
        /// <returns>Массив информации о событиях</returns>
        public EventInfo[] GetEvents()
        {
            if (_events != null) return _events;
            _events = _type.GetEvents(DefaultBindingFlags);
            return _events;
        }

        /// <summary>
        ///     Получить поля типа и его базовых типов
        /// </summary>
        /// <returns>Массив информации о полях</returns>
        public FieldInfo[] GetFields()
        {
            if (_fields != null) return _fields;
            _fields = _type.GetFields(DefaultBindingFlags)
                .Concat(BaseTypes.SelectMany(x => x.GetFields(DefaultBindingFlags))).ToArray();
            return _fields;
        }

        /// <summary>
        ///     Получить методы типа
        /// </summary>
        /// <returns>Массив информации о методах</returns>
        public MethodInfo[] GetMethods()
        {
            if (_methods != null) return _methods;
            _methods = _type.GetMethods(DefaultBindingFlags);
            return _methods;
        }

        /// <summary>
        ///     Получить свойства типа и его базовых типов (кроме интерфейсов)
        /// </summary>
        /// <returns>Массив информации о свойствах</returns>
        public PropertyInfo[] GetProperties()
        {
            if (_properties != null) return _properties;
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

        private static bool All<TSource>(IEnumerable<TSource> source, Func<TSource, int, bool> predicate)
        {
            if (source == null) throw new NullReferenceException("source");

            if (predicate == null) throw new NullReferenceException("predicate");

            var i = 0;
            foreach (var item in source)
            {
                if (!predicate(item, i)) return false;
                i++;
            }

            return true;
        }

        #endregion Внутренние методы

        /// <summary>
        /// Получает полное имя таблицы вместе именем схемы и экранированием имен
        /// </summary>
        /// <returns></returns>
        public string GetFullTableName()
        {
            return GetFullTableName("[", "]");
        }

        /// <summary>
        /// Получает полное имя таблицы вместе именем схемы и экранированием имен
        /// </summary>
        /// <param name="namePrefix"></param>
        /// <param name="nameSuffix"></param>
        /// <param name="defaultSchemaName"></param>
        /// <returns></returns>
        public string GetFullTableName(string namePrefix, string nameSuffix, string defaultSchemaName = null)
        {
            var schema = string.IsNullOrWhiteSpace(SchemaName) ? defaultSchemaName : SchemaName;
            var fullTableName = $"{namePrefix}{TableName}{nameSuffix}";
            if (!string.IsNullOrWhiteSpace(schema))
                fullTableName = $"{namePrefix}{schema}{nameSuffix}." + fullTableName;
            return fullTableName;
        }

        /// <summary>
        /// Получить полное имя колонки с именем схемы, таблицы и экранированием имен
        /// </summary>
        /// <returns></returns>
        public string GetFullColumnName()
        {
            return GetFullColumnName("[", "]");
        }

        /// <summary>
        /// Получить полное имя колонки с именем схемы, таблицы и экранированием имен
        /// </summary>
        /// <param name="namePrefix"></param>
        /// <param name="nameSuffix"></param>
        /// <param name="defaultSchemaName"></param>
        /// <returns></returns>
        public string GetFullColumnName(string namePrefix, string nameSuffix, string defaultSchemaName = null)
        {
            return GetFullTableName(namePrefix, nameSuffix, defaultSchemaName) + $".{namePrefix}{ColumnName}{nameSuffix}";
        }
    }

    public static class MemberInfoExtensions
    {
        /// <summary>
        ///     Получить расширенную информацию о члене класса
        /// </summary>
        /// <param name="memberInfo">Информация о члене класса</param>
        /// <returns>Расширенная информация о члене класса</returns>
        public static TypeCache GetMemberInfoEx(this MemberInfo memberInfo)
        {
            return TypeCache.Create(memberInfo);
        }

        internal static IEnumerable<TSource> DistinctBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

            var seenKeys = new HashSet<TKey>();

            foreach (var element in source)
            {
                if (seenKeys.Add(keySelector(element)))
                    yield return element;
            }
        }
    }

    /// <summary>
    ///     Определяет тип имени, используемый для члена, включая основное имя, отображаемое имя, имена для JSON, XML, а также
    ///     имена для базы данных и схемы. Позволяет комбинировать несколько типов с помощью битовой маски.
    /// </summary>
    /// <remarks>
    ///     Перечисление поддерживает флаги, что позволяет указывать сразу несколько типов имен для одного члена.
    ///     Используйте для выбора или фильтрации нужных представлений имени в различных сценариях, например при сериализации,
    ///     отображении или работе с базой данных.
    /// </remarks>
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