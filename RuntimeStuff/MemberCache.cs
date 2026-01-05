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
    ///     v.2026.01.05 (RS) <br />
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
    public class MemberCache : MemberInfo
    {
        // Кэш для расширенной информации о членах класса
        protected static readonly ConcurrentDictionary<MemberInfo, MemberCache> MemberInfoCache =
            new ConcurrentDictionary<MemberInfo, MemberCache>();

        // Кэш делегатов для создания экземпляров типов
        private static readonly ConcurrentDictionary<string, Func<object>> ConstructorsCache =
            new ConcurrentDictionary<string, Func<object>>();

        // Кэш для делегатов получения значений членов
        private readonly ConcurrentDictionary<string, Func<object, object>[]> _getters =
            new ConcurrentDictionary<string, Func<object, object>[]>(
                StringComparison.OrdinalIgnoreCase.ToStringComparer());

        // Кэш для расширенной информации о членах класса по имени
        private readonly ConcurrentDictionary<string, MemberCache> _memberCache =
            new ConcurrentDictionary<string, MemberCache>();

        // Кэш для делегатов установки и получения значений членов
        private readonly ConcurrentDictionary<string, Action<object, object>[]> _setters =
            new ConcurrentDictionary<string, Action<object, object>[]>(StringComparison.OrdinalIgnoreCase
                .ToStringComparer());

        private readonly Type _type;

        private readonly MemberCache _typeCache;

        internal readonly MemberInfo MemberInfo;

        private Attribute[] _attributes;

        private Type[] _baseTypes;

        private ConstructorInfo[] _constructors;

        private EventInfo[] _events;

        private FieldInfo[] _fields;

        private string _jsonName;

        private Dictionary<MemberInfo, MemberCache> _members;

        private MethodInfo[] _methods;

        private PropertyInfo[] _properties;

        private string _xmlAttr;

        private string _xmlElem;

        public MemberCache(MemberInfo memberInfo) : this(memberInfo, memberInfo is Type)
        {
        }

        /// <summary>
        ///     Конструктор для создания расширенной информации о члене класса
        /// </summary>
        /// <param name="memberInfo">Информация о члене класса</param>
        /// <param name="getMembers">Получить информацию о дочерних членах: свойства, поля, методы и т.п.</param>
        /// <param name="parent"></param>
        private MemberCache(MemberInfo memberInfo, bool getMembers = false, MemberCache parent = null)
        {
            Parent = parent;

            _typeCache = memberInfo as MemberCache;
            if (_typeCache != null)
                memberInfo = _typeCache.MemberInfo;

            MemberInfo = memberInfo;

            // Определяем тип члена класса
            var t = MemberInfo as Type;
            var pi = MemberInfo as PropertyInfo;
            var fi = MemberInfo as FieldInfo;
            var mi = MemberInfo as MethodInfo;
            var ci = MemberInfo as ConstructorInfo;
            var mx = MemberInfo as MemberCache;
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
                   throw new NotSupportedException(
                       $"{nameof(MemberCache)}: ({memberInfo.GetType().Name}) not supported!");
            IsDictionary = _typeCache?.IsDictionary ?? Obj.IsDictionary(_type);
            IsDelegate = _typeCache?.IsDelegate ?? Obj.IsDelegate(_type);
            IsFloat = _typeCache?.IsFloat ?? Obj.IsFloat(_type);
            IsNullable = _typeCache?.IsNullable ?? Obj.IsNullable(_type);
            if (e != null)
                _type = e.EventHandlerType;

            IsNumeric = _typeCache?.IsNumeric ?? Obj.IsNumeric(_type);
            IsBoolean = _typeCache?.IsBoolean ?? Obj.IsBoolean(_type);
            IsCollection = _typeCache?.IsCollection ?? Obj.IsCollection(_type);
            ElementType = IsCollection ? _typeCache?.ElementType ?? Obj.GetCollectionItemType(Type) : null;
            IsBasic = _typeCache?.IsBasic ?? Obj.IsBasic(_type);
            IsEnum = _typeCache?.IsEnum ?? _type.IsEnum;
            IsConst = _typeCache?.IsConst ?? (fi != null && fi.IsLiteral && !fi.IsInitOnly);
            IsBasicCollection = _typeCache?.IsBasicCollection ?? (IsCollection && Obj.IsBasic(ElementType));
            IsObject = _typeCache?.IsObject ?? _type == typeof(object);
            IsTuple = _typeCache?.IsTuple ?? Obj.IsTuple(_type);
            IsProperty = pi != null;
            IsEvent = e != null;
            IsField = fi != null;
            IsType = t != null;
            IsMethod = mi != null;
            IsConstructor = ci != null;
            CanWrite = pi != null ? pi.CanWrite : fi != null;
            CanRead = pi != null ? pi.CanRead : fi != null;
            IsPublic = _typeCache?.IsPublic ??
                       IsProperty ? AsPropertyInfo()?.GetAccessors().Any(m => m.IsPublic) == true :
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

            // Получение атрибутов
            Description = _typeCache?.Description ??
                          MemberInfo.GetCustomAttributes<DescriptionAttribute>().FirstOrDefault()?.Description;
            DisplayName = _typeCache?.DisplayName ?? MemberInfo.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName;

            if (DisplayName == null && Attributes.TryGetValue("DisplayAttribute", out var da)) DisplayName = da.GetType().GetProperty("Name")?.GetValue(da)?.ToString();

            // Дополнительная обработка для типов
            if (IsType)
            {
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

                Properties = _typeCache?.Properties ??
                             Members.Where(x => x.Value.IsProperty).ToDictionaryDistinct(x => x.Value.Name, y => y.Value);
                PublicProperties = _typeCache?.PublicProperties ??
                                   Properties.Values.Where(x => x.IsPublic).ToDictionary(x => x.Name);
                PrivateProperties = _typeCache?.PrivateProperties ??
                                    Properties.Values.Where(x => x.IsPrivate).ToDictionary(x => x.Name);
                PublicBasicProperties = _typeCache?.PublicBasicProperties ??
                                        PublicProperties.Values.Where(x => x.IsBasic).ToDictionary(x => x.Name);
                PublicBasicEnumerableProperties = _typeCache?.PublicBasicEnumerableProperties ??
                                                  PublicProperties.Values.Where(x => x.IsBasicCollection)
                                                      .ToDictionary(x => x.Name);
                PublicEnumerableProperties = _typeCache?.PublicEnumerableProperties ?? PublicProperties.Values
                    .Where(x => x.IsCollection && !x.IsBasicCollection).ToDictionary(x => x.Name);

                Fields = _typeCache?.Fields ?? Members.Where(x => x.Value.IsField).ToDictionary(x => x.Value.Name, y => y.Value);
                PublicFields = _typeCache?.PublicFields ??
                               Fields.Values.Where(x => x.IsPublic).ToDictionary(x => x.Name);
                PrivateFields = _typeCache?.PrivateFields ??
                                Fields.Values.Where(x => x.IsPrivate).ToDictionary(x => x.Name);

                PrimaryKeys = _typeCache?.PrimaryKeys ??
                              PublicBasicProperties.Where(x => x.Value.Attributes.ContainsKey("KeyAttribute"))
                                  .Select(x => x.Value)
                                  .ToDictionary(x => x.Name);

                if (PrimaryKeys.Count == 0)
                {
                    var p =
                        PublicBasicProperties.GetValueOrDefault("id",
                            StringComparison.OrdinalIgnoreCase.ToStringComparer()) ??
                        PublicBasicProperties.GetValueOrDefault(TableName + "id", StringComparer.OrdinalIgnoreCase);
                    if (p != null)
                        PrimaryKeys = new Dictionary<string, MemberCache>
                            { { p.Name, PublicBasicProperties[p.Name] } };
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

                var propsAndFields = Members.Where(x => x.Value.IsProperty || x.Value.IsField)
                    .GroupBy(x => x.Value.Name, StringComparison.OrdinalIgnoreCase.ToStringComparer());
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
                if (Parent == null && pi?.DeclaringType != null) Parent = Create(pi.DeclaringType);

                PropertyType = pi.PropertyType;
                IsSetterPublic = pi.GetSetMethod()?.IsPublic == true;
                IsSetterPrivate = pi.GetSetMethod()?.IsPrivate == true;
                IsGetterPublic = pi.GetGetMethod()?.IsPublic == true;
                IsGetterPrivate = pi.GetGetMethod()?.IsPrivate == true;
                TableName = Parent?.TableName;
                SchemaName = Parent?.SchemaName;

                if (_typeCache == null)
                {
                    var keyAttr = Attributes.GetValueOrDefault("KeyAttribute");
                    var colAttr = Attributes.GetValueOrDefault("ColumnAttribute");
                    var fkAttr = Attributes.GetValueOrDefault("ForeignKeyAttribute");
                    IsPrimaryKey = keyAttr != null || string.Equals(Name, "id", StringComparison.OrdinalIgnoreCase);
                    IsForeignKey = fkAttr != null;
                    try
                    {
                        PropertyBackingField = Parent.GetFields().FirstOrDefault(x => x.Name == $"<{Name}>k__BackingField") ??
                                               Obj.GetFieldInfoFromGetAccessor(pi.GetGetMethod(true));
                    }
                    catch
                    {
                        //ignore
                    }

                    try
                    {
                        Setter = Obj.PropertySetterCache.Get(pi);

                        if (Setter == null && PropertyBackingField != null)
                            Setter = Obj.FieldSetterCache.Get(PropertyBackingField);
                    }
                    catch (Exception)
                    {
                        Setter = (o, v) => pi.SetValue(o, v);
                    }

                    try
                    {
                        Getter = Obj.PropertyGetterCache.Get(pi);
                    }
                    catch (Exception)
                    {
                        Getter = o => pi.GetValue(o);
                    }

                    TableName = Parent?.TableName;
                    ColumnName = colAttr != null
                        ? colAttr.GetType().GetProperty("Name")?.GetValue(colAttr)?.ToString() ?? Name
                        : Name;

                    ForeignKeyName = fkAttr?.GetType().GetProperty("Name")?.GetValue(fkAttr)?.ToString() ??
                                        string.Empty;
                }
                else
                {
                    Setter = _typeCache.Setter;
                    Getter = _typeCache.Getter;
                    PropertyBackingField = _typeCache.PropertyBackingField;
                    ColumnName = _typeCache.ColumnName;
                    ForeignKeyName = _typeCache.ForeignKeyName;
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
                FieldType = fi.FieldType;
                try
                {
                    Setter = _typeCache?.Setter ?? Obj.FieldSetterCache.Get(fi);
                }
                catch
                {
                    Setter = (obj, value) => fi.SetValue(obj, value);
                }

                try
                {
                    Getter = _typeCache?.Getter ?? Obj.FieldGetterCache.Get(fi);
                }
                catch (Exception)
                {
                    Getter = x => fi.GetValue(x);
                }
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

        public Dictionary<string, MemberCache> PrivateFields { get; }

        public Dictionary<string, MemberCache> PublicFields { get; }

        public Dictionary<string, MemberCache> PrivateProperties { get; }

        public Dictionary<string, MemberCache> Fields { get; }

        public Type PropertyType { get; }
        public Type FieldType { get; }

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
            return $"[{(IsProperty ? "P" : IsField ? "F" : IsType ? "T" : IsMethod ? "M" : IsConstructor ? "C" : IsEvent ? "E" : "?")}] {(DeclaringType == null ? "" : $"{DeclaringType.Name}.")}{Name} ({Type.Name})";
        }

        #endregion Вспомогательные методы

        /// <summary>
        ///     Получает полное имя таблицы вместе именем схемы и экранированием имен
        /// </summary>
        /// <returns></returns>
        public string GetFullTableName()
        {
            return GetFullTableName("[", "]");
        }

        /// <summary>
        ///     Получает полное имя таблицы вместе именем схемы и экранированием имен
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
        ///     Получить полное имя колонки с именем схемы, таблицы и экранированием имен
        /// </summary>
        /// <returns></returns>
        public string GetFullColumnName()
        {
            return GetFullColumnName("[", "]");
        }

        /// <summary>
        ///     Получить полное имя колонки с именем схемы, таблицы и экранированием имен
        /// </summary>
        /// <param name="namePrefix"></param>
        /// <param name="nameSuffix"></param>
        /// <param name="defaultSchemaName"></param>
        /// <returns></returns>
        public string GetFullColumnName(string namePrefix, string nameSuffix, string defaultSchemaName = null)
        {
            return GetFullTableName(namePrefix, nameSuffix, defaultSchemaName) +
                   $".{namePrefix}{ColumnName}{nameSuffix}";
        }

        public static implicit operator PropertyInfo(MemberCache mc)
        {
            if (mc == null)
                throw new ArgumentNullException(nameof(mc));

            var propertyInfo = mc.AsPropertyInfo();
            return propertyInfo ?? throw new InvalidCastException(
                $"Cannot cast MemberCache of type '{mc.MemberType}' to PropertyInfo. Member is a {mc.MemberType}.");
        }

        public static implicit operator FieldInfo(MemberCache mc)
        {
            if (mc == null)
                throw new ArgumentNullException(nameof(mc));

            var fieldInfo = mc.AsFieldInfo();
            return fieldInfo ?? throw new InvalidCastException(
                $"Cannot cast MemberCache of type '{mc.MemberType}' to FieldInfo. Member is a {mc.MemberType}.");
        }

        public static implicit operator MethodInfo(MemberCache mc)
        {
            if (mc == null)
                throw new ArgumentNullException(nameof(mc));

            var methodInfo = mc.AsMethodInfo();
            return methodInfo ?? throw new InvalidCastException(
                $"Cannot cast MemberCache of type '{mc.MemberType}' to MethodInfo. Member is a {mc.MemberType}.");
        }

        public static implicit operator EventInfo(MemberCache mc)
        {
            if (mc == null)
                throw new ArgumentNullException(nameof(mc));

            var eventInfo = mc.AsEventInfo();
            return eventInfo ?? throw new InvalidCastException(
                $"Cannot cast MemberCache of type '{mc.MemberType}' to EventInfo. Member is a {mc.MemberType}.");
        }

        public static implicit operator ConstructorInfo(MemberCache mc)
        {
            if (mc == null)
                throw new ArgumentNullException(nameof(mc));

            var constructorInfo = mc.AsConstructorInfo();
            return constructorInfo ?? throw new InvalidCastException(
                $"Cannot cast MemberCache of type '{mc.MemberType}' to ConstructorInfo. Member is a {mc.MemberType}.");
        }

        public static implicit operator Type(MemberCache mc)
        {
            if (mc == null)
                throw new ArgumentNullException(nameof(mc));

            if (!mc.IsType)
                throw new InvalidCastException(
                    $"Cannot cast MemberCache of type '{mc.MemberType}' to Type. Member is a {mc.MemberType}.");

            return mc.Type;
        }

        public static implicit operator MemberCache(PropertyInfo memberInfo)
        {
            return memberInfo == null ? throw new ArgumentNullException(nameof(memberInfo)) : Create(memberInfo);
        }

        public static implicit operator MemberCache(FieldInfo memberInfo)
        {
            return memberInfo == null ? throw new ArgumentNullException(nameof(memberInfo)) : Create(memberInfo);
        }

        public static implicit operator MemberCache(MethodInfo memberInfo)
        {
            return memberInfo == null ? throw new ArgumentNullException(nameof(memberInfo)) : Create(memberInfo);
        }

        public static implicit operator MemberCache(EventInfo memberInfo)
        {
            return memberInfo == null ? throw new ArgumentNullException(nameof(memberInfo)) : Create(memberInfo);
        }

        public static implicit operator MemberCache(ConstructorInfo memberInfo)
        {
            return memberInfo == null ? throw new ArgumentNullException(nameof(memberInfo)) : Create(memberInfo);
        }

        public static implicit operator MemberCache(Type memberInfo)
        {
            return memberInfo == null ? throw new ArgumentNullException(nameof(memberInfo)) : Create(memberInfo);
        }

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

                _baseTypes = Obj.GetBaseTypes(_type, getInterfaces: true);
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
        ///     Имя колонки (из атрибута ColumnAttribute), если такого атрибута нет - то имя свойства
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
        public string ForeignKeyName { get; }

        /// <summary>
        ///     Свойства, помеченные атрибутом ForeignKeyAttribute
        /// </summary>
        public Dictionary<string, MemberCache> ForeignKeys { get; }

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

        public bool IsEnum { get; }

        public bool IsConst { get; }

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
        public bool IsIdentity => IsPrimaryKey && (Obj.IsNumeric(Type, false) || Type == typeof(Guid));

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
        public Dictionary<MemberInfo, MemberCache> Members
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
        ///     Словарь событий типа
        /// </summary>
        public Dictionary<string, EventInfo> Events { get; }

        /// <summary>
        ///     Имя члена
        /// </summary>
        public sealed override string Name { get; }

        /// <summary>
        ///     Родительский член (для вложенных членов)
        /// </summary>
        public MemberCache Parent { get; private set; }

        /// <summary>
        ///     Свойства, помеченные атрибутом KeyAttribute. Если таких нет, то ищется сначала "EventId", потом ИмяТаблицыId
        /// </summary>
        public Dictionary<string, MemberCache> PrimaryKeys { get; }

        /// <summary>
        ///     Поле, хранящее значение свойства (для автоматически реализуемых свойств)
        /// </summary>
        public FieldInfo PropertyBackingField { get; }

        /// <summary>
        ///     Словарь публичных свойств-коллекций с элементом списка {T}, где T - простой тип <see cref="BaseTypes" /> (IsPublic
        ///     && IsProperty && IsBasicCollection)
        /// </summary>
        public Dictionary<string, MemberCache> PublicBasicEnumerableProperties { get; }

        /// <summary>
        ///     Словарь публичных свойств-коллекций с элементом списка {T}, где T : class (IsPublic && IsProperty && IsCollection &
        ///     & !IsBasicCollection)
        /// </summary>
        public Dictionary<string, MemberCache> PublicEnumerableProperties { get; }

        /// <summary>
        ///     Словарь простых (<see cref="Obj.BasicTypes" />) публичных свойств типа (IsPublic && IsProperty && IsBasic)
        /// </summary>
        public Dictionary<string, MemberCache> PublicBasicProperties { get; }

        /// <summary>
        ///     Словарь свойств по имени колонки у которых есть один из атрибутов Column, Foreign и нет NotMapped и Key. Если таких
        ///     нет, то все простые публичные свойства кроме первичных ключей
        /// </summary>
        public Dictionary<string, MemberCache> ColumnProperties { get; }

        /// <summary>
        ///     Словарь публичных свойств типа (IsPublic && IsProperty)
        /// </summary>
        public Dictionary<string, MemberCache> PublicProperties { get; }

        /// <summary>
        ///     Словарь всех свойств типа (IsProperty)
        /// </summary>
        public Dictionary<string, MemberCache> Properties { get; }

        /// <summary>
        ///     Все доступные конструкторы
        /// </summary>
        public ConstructorInfo[] Constructors => GetConstructors();

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

            set => SetMemberValue(source, memberName, value);
        }

        /// <summary>
        ///     Получить MemberCache по MemberInfo
        /// </summary>
        /// <param name="source">Объект</param>
        /// <param name="memberName">Имя свойства или поля</param>
        /// <returns></returns>
        public MemberCache this[MemberInfo memberInfo]
        {
            get => Members[memberInfo];
        }

        /// <summary>
        ///     Получить член по имени
        /// </summary>
        /// <param name="memberName">Имя члена для поиска</param>
        /// <returns>Найденный член или null, если не найден</returns>
        public MemberCache this[string memberName] => this[memberName, MemberNameType.Any];

        /// <summary>
        ///     Получить член по имени с фильтрацией
        /// </summary>
        /// <param name="memberName">Имя члена для поиска</param>
        /// <param name="memberNameType">Тип имени члена для поиска</param>
        /// <param name="memberFilter">Фильтр для отбора членов</param>
        /// <returns>Найденный член или null, если не найден</returns>
        public MemberCache this[string memberName, MemberNameType memberNameType = MemberNameType.Any,
            Func<MemberCache, bool> memberFilter = null] => GetMember(memberName, memberNameType, memberFilter);

        #endregion Индексаторы

        #region Статические методы

        /// <summary>
        ///     Создать расширенную информацию о члене класса (с кэшированием)
        /// </summary>
        /// <param name="memberInfo">Информация о члене класса</param>
        /// <returns>Расширенная информация о члене класса</returns>
        public static MemberCache Create(MemberInfo memberInfo)
        {
            if (memberInfo is MemberCache me)
                return me;

            if (memberInfo is Type t)
                return MemberInfoCache.GetOrAdd(t, x => new MemberCache(x, true));

            var mc = MemberInfoCache.GetOrAdd(memberInfo.DeclaringType, x => new MemberCache(x, false));
            return mc[memberInfo];

            //var result = memberInfo == null
            //    ? null
            //    : MemberInfoCache.GetOrAdd(memberInfo, x => new MemberCache(x, x is Type));

            //return result;
        }

        private static MemberCache Create(MemberInfo memberInfo, MemberCache parent)
        {
            if (memberInfo is MemberCache me)
                return me;

            var result = memberInfo == null
                ? null
                : MemberInfoCache.GetOrAdd(memberInfo, x => new MemberCache(x, x is Type, parent));

            return result;
        }

        /// <summary>
        ///     Создаёт делегат для конструктора по умолчанию указанного типа.
        ///     Используется для быстрой активации объектов без вызова Activator.New(Type)
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
                        Obj.IsImplements(args[i]?.GetType(), pAll[i].ParameterType)))
                    return c;
                var pNoDef = c.GetParameters().Where(p => !p.HasDefaultValue).ToArray();

                if (pNoDef.Length == ctorArgs.Length && All(ctorArgs, (_, i) => Obj.IsImplements(args[i]?.GetType(), pNoDef[i].ParameterType)))
                {
                    Array.Resize(ref ctorArgs, pAll.Length);
                    for (var i = pNoDef.Length; i < pAll.Length; i++)
                        ctorArgs[i] = pAll[i].DefaultValue;
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
        /// <returns>Созданный экземпляр указанного типа.</returns>
        public T New<T>() where T : class
        {
            return DefaultConstructor() as T;
        }

        /// <summary>
        ///     Создаёт экземпляр указанного типа с возможностью передачи аргументов конструктора.
        /// </summary>
        /// <returns>Созданный экземпляр указанного типа.</returns>
        public object New()
        {
            return IsBasic ? Obj.Default(Type) : DefaultConstructor();
        }

        /// <summary>
        ///     Создаёт экземпляр указанного типа с возможностью передачи аргументов конструктора.
        /// </summary>
        /// <param name="ctorArgs">Аргументы конструктора.</param>
        /// <returns>Созданный экземпляр указанного типа.</returns>
        public object New(params object[] ctorArgs)
        {
            return New(typeof(Type), ctorArgs);
        }

        /// <summary>
        ///     Создаёт экземпляр указанного типа с возможностью передачи аргументов конструктора.
        /// </summary>
        /// <param name="ctorArgs">Аргументы конструктора.</param>
        /// <returns>Созданный экземпляр указанного типа.</returns>
        public T New<T>(params object[] ctorArgs)
        {
            return Obj.ChangeType<T>(New(Type, ctorArgs));
        }

        /// <summary>
        ///     Создаёт экземпляр указанного типа с возможностью передачи аргументов конструктора.
        /// </summary>
        /// <param name="type">Тип, экземпляр которого нужно создать.</param>
        /// <param name="ctorArgs">Аргументы конструктора.</param>
        /// <returns>Созданный экземпляр указанного типа.</returns>
        public static object New(Type type, params object[] ctorArgs)
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

            if (type.IsEnum) return ctorArgs.FirstOrDefault(x => x?.GetType() == type) ?? Obj.Default(type);

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
                    return Obj.Default(type);
                }

            var ctor = typeInfo.GetConstructorByArgs(ref ctorArgs);


            if (ctor == null && type.IsValueType)
                return Obj.Default(type);

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
        /// <returns>Экземпляр <see cref="MemberCache" />, либо <c>null</c>, если член не найден.</returns>
        public MemberCache GetMember(string name, MemberNameType memberNamesType = MemberNameType.Any,
            Func<MemberCache, bool> membersFilter = null,
            StringComparison nameComparison = StringComparison.OrdinalIgnoreCase)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            if (_memberCache.TryGetValue(name, out var mx))
                return mx;

            if (memberNamesType == MemberNameType.Any || memberNamesType.HasFlag(MemberNameType.Name))
            {
                // Быстрый поиск свойства
                var quickProp = Obj.GetLowestProperty(Type, name);
                if (quickProp != null)
                {
                    mx = new MemberCache(quickProp);
                    _memberCache[name] = mx;
                    if (membersFilter == null || membersFilter(mx))
                        return mx;
                }

                // Быстрый поиск поля
                var quickField = Obj.GetLowestField(Type, name);
                if (quickField != null)
                {
                    mx = new MemberCache(quickField);
                    _memberCache[name] = mx;
                    if (membersFilter == null || membersFilter(mx))
                        return mx;
                }

                // Быстрый поиск метода
                var quickMethod = Obj.GetLowestMethod(Type, name);
                if (quickMethod != null)
                {
                    mx = new MemberCache(quickMethod);
                    _memberCache[name] = mx;
                    if (membersFilter == null || membersFilter(mx))
                        return mx;
                }

                // Быстрый поиск события
                var quickEvent = Obj.GetLowestEvent(Type, name);
                if (quickEvent != null)
                {
                    mx = new MemberCache(quickEvent);
                    _memberCache[name] = mx;
                    if (membersFilter == null || membersFilter(mx))
                        return mx;
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
                (x => x.SchemaName, MemberNameType.SchemaName)
            };

            foreach (var (f, flag) in searchNames)
            {
                if (memberNamesType != MemberNameType.Any && (memberNamesType & flag) == 0)
                    continue;

                // Ищем по совпадению имени
                if (mx == null)
                    mx = Members.FirstOrDefault(x =>
                        f(x.Value)?.Equals(name, nameComparison) == true &&
                        (membersFilter == null || membersFilter(x.Value))).Value;

                // Ищем по совпадению с удалением специальных символов
                if (mx == null)
                    mx = Members.FirstOrDefault(x =>
                        Regex.Replace($"{f(x.Value)}", "[ \\-_\\.]", string.Empty).Equals(
                            Regex.Replace(name, "[ \\-_\\.]", string.Empty),
                            nameComparison) && (membersFilter == null || membersFilter(x.Value))).Value;

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
            return GetMember(propertyName, MemberNameType.Name)?.AsPropertyInfo();
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
        ///     Проверяет наличие атрибута указанного типа.
        /// </summary>
        /// <param name="typeNames">Имя типа</param>
        public bool HasAttributeOfType<T>()
        {
            return HasAttributeOfType(typeof(T));
        }

        /// <summary>
        ///     Проверяет наличие атрибута любого из указанных типов.
        /// </summary>
        /// <param name="typeNames">Имя типа</param>
        public bool HasAttributeOfType(params Type[] types)
        {
            return Attributes.Any(a => types.Contains(a.Value.GetType()));
        }

        /// <summary>
        ///     Проверяет наличие атрибута любого из указанных имен типа.
        /// </summary>
        /// <param name="typeNames">Имя типа</param>
        public bool HasAttributeOfType(params string[] typeNames)
        {
            return Attributes.Any(a => typeNames.Contains(a.Key));
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

        private MemberCache[] _columns;
        private MemberCache[] _tables;
        private MemberCache[] _pks;
        private MemberCache[] _fks;

        /// <summary>
        ///     Получает коллекцию простых публичных свойств типа, которые могут использоваться как колонки ORM.
        /// </summary>
        /// <returns>Массив <see cref="MemberCache" />, представляющий свойства, подходящие для колонок базы данных.</returns>
        /// <remarks>
        ///     Метод сначала ищет свойства, помеченные атрибутами <c>ColumnAttribute</c>, <c>KeyAttribute</c> или
        ///     <c>ForeignKeyAttribute</c>.
        ///     Если такие свойства не найдены, возвращаются все публичные простые свойства, не являющиеся коллекциями и не
        ///     помеченные как <c>NotMappedAttribute</c>.
        ///     Результат кэшируется в поле <c>_columns</c> для повторного использования.
        /// </remarks>
        public MemberCache[] GetColumns()
        {
            if (_columns != null)
                return _columns;

            _columns = Members.Where(x =>
                    x.Value.IsProperty &&
                    x.Value.IsPublic &&
                    x.Value.IsBasic &&
                    !x.Value.IsCollection &&
                    x.Value.HasAttributeOfType("ColumnAttribute", "KeyAttribute", "ForeignKeyAttribute")
                    && !x.Value.HasAttributeOfType("NotMappedAttribute")
                )
                .Select(x => x.Value)
                .ToArray();

            return _columns;
        }

        /// <summary>
        ///     Получает коллекцию свойств, которые являются первичными ключами.
        /// </summary>
        /// <returns>Массив <see cref="MemberCache" />, представляющий первичные ключи.</returns>
        /// <remarks>
        ///     Результат кэшируется в поле <c>_pks</c> для повторного использования.
        /// </remarks>
        public MemberCache[] GetPrimaryKeys()
        {
            if (_pks != null)
                return _pks;

            _pks = Members
                .Where(x => x.Value.IsPrimaryKey)
                .Select(x => x.Value)
                .ToArray();

            return _pks;
        }

        /// <summary>
        ///     Получает коллекцию свойств, которые являются внешними ключами.
        /// </summary>
        /// <returns>Массив <see cref="MemberCache" />, представляющий внешние ключи.</returns>
        /// <remarks>
        ///     Результат кэшируется в поле <c>_fks</c> для повторного использования.
        /// </remarks>
        public MemberCache[] GetForeignKeys()
        {
            if (_fks != null)
                return _fks;

            _fks = Members
                .Where(x => x.Value.IsForeignKey)
                .Select(x => x.Value)
                .ToArray();

            return _fks;
        }

        /// <summary>
        ///     Получает коллекцию свойств, представляющих таблицы (коллекции сложных типов без атрибута NotMapped).
        /// </summary>
        /// <returns>Массив <see cref="MemberCache" /> для таблиц.</returns>
        public MemberCache[] GetTables()
        {
            if (_tables != null)
                return _tables;

            _tables = Members.Where(x =>
                x.Value.IsProperty &&
                x.Value.IsPublic &&
                (x.Value.IsCollection &&
                !x.Value.IsBasicCollection || !x.Value.IsBasic) &&
                !x.Value.HasAttributeOfType("ColumnAttribute", "NotMappedAttribute", "Key")
                )
                .Select(x => x.Value)
                .ToArray();

            return _tables;
        }

        #endregion Методы работы с ORM

        #region Внутренние методы

        /// <summary>
        ///     Получить все члены типа (свойства, поля, события)
        /// </summary>
        /// <returns>Массив информации о членах</returns>
        internal Dictionary<MemberInfo, MemberCache> GetChildMembersInternal()
        {
            var members =
            GetProperties().Cast<MemberInfo>()
            .Concat(GetFields())
            .Concat(GetEvents())
            .Concat(GetConstructors())
            .Concat(GetMethods());

            return members.ToDictionary(key => key, val => Create(val, this));
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
        ///     Устанавливает значение члена для указанного объекта. Если необходимо, выполняется преобразование типа значения.
        /// </summary>
        /// <param name="source">Объект, для которого устанавливается значение.</param>
        /// <param name="value">Значение, которое нужно установить.</param>
        /// <param name="valueConverter">
        ///     Конвертор значения в тип свойства, если не указан, то используется
        ///     <see cref="Obj.ChangeType" />
        /// </param>
        public virtual void SetValue(object source, object value, Func<object, object> valueConverter = null)
        {
            Setter(source, valueConverter == null ? Obj.ChangeType(value, Type) : valueConverter(value));
        }

        /// <summary>
        ///     Устанавливает значение свойства или поля для указанного объекта по имени. Для прямого доступа используйте
        ///     <see cref="Setter" />.
        /// </summary>
        /// <param name="source">Объект, для которого устанавливается значение.</param>
        /// <param name="memberName"></param>
        /// <param name="value">Значение, которое нужно установить.</param>
        /// <param name="valueConverter">Конвертор значения в тип свойства, если не указан, то пытаемся установить как есть/></param>
        public virtual void SetMemberValue(object source, string memberName, object value, Func<object, object> valueConverter = null)
        {
            if (!_setters.TryGetValue(memberName, out var setters))
                return;
            foreach (var s in setters) s(source, valueConverter != null ? valueConverter(value) : value);
        }

        /// <summary>
        ///     Извлекает значения указанного члена из заданного объекта источника.
        /// </summary>
        /// <remarks>
        ///     Если для указанного имени члена определено несколько геттеров, возвращаются значения
        ///     всех соответствующих членов. Если член не найден, возвращается null.
        /// </remarks>
        /// <param name="source">Объект, из которого требуется получить значения члена. Не может быть равен null.</param>
        /// <param name="memberName">Имя члена, значения которого необходимо получить. Чувствительно к регистру.</param>
        /// <returns>
        ///     Массив объектов, содержащий значения указанного члена. Возвращает null, если член с заданным именем не
        ///     найден.
        /// </returns>
        public virtual object GetValue(object source, string memberName)
        {
            if (!_getters.TryGetValue(memberName, out var getters))
                return null;

            var values = new List<object>();
            foreach (var g in getters) values.Add(g(source));

            return values.Count == 1 ? values[0] : values.ToArray();
        }

        public virtual T GetMemberValue<T>(object source, string memberName)
        {
            return Obj.ChangeType<T>(GetValue(source, memberName));
        }

        /// <summary>
        ///     Получает значение члена указанного объекта.
        /// </summary>
        /// <param name="instance">Объект, значение которого нужно получить.</param>
        /// <returns>Значение члена.</returns>
        public object GetValue(object instance)
        {
            return Getter(instance);
        }

        /// <summary>
        ///     Получает значение члена указанного объекта и приводит его к типу <typeparamref name="T" />.
        /// </summary>
        /// <typeparam name="T">Тип, к которому нужно привести значение.</typeparam>
        /// <param name="instance">Объект, значение которого нужно получить.</param>
        /// <returns>Значение члена, приведённое к типу <typeparamref name="T" />.</returns>
        public T GetValue<T>(object instance)
        {
            return Obj.ChangeType<T>(Getter(instance));
        }

        /// <summary>
        ///     Создать словарь из имен свойств и их значений.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="instance"></param>
        /// <param name="propertyNames">Имена свойств, если не указаны, то берутся <see cref="PublicProperties" /></param>
        /// <returns></returns>
        public Dictionary<string, object> ToDictionary<T>(T instance, params string[] propertyNames) where T : class
        {
            var dic = new Dictionary<string, object>();

            ToDictionary(instance, dic, propertyNames);

            return dic;
        }

        /// <summary>
        ///     Записать в словарь значения свойств объекта.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="instance"></param>
        /// <param name="dictionary"></param>
        /// <param name="propertyNames">Имена свойств, если не указаны, то берутся <see cref="PublicProperties" /></param>
        /// <returns></returns>
        public void ToDictionary<T>(T instance, Dictionary<string, object> dictionary, params string[] propertyNames)
            where T : class
        {
            var props = propertyNames.Any()
                ? PublicBasicProperties.Where(x => propertyNames.Contains(x.Key)).Select(x => x.Value).ToArray()
                : PublicBasicProperties.Select(x => x.Value).ToArray();

            foreach (var mi in props) dictionary[mi.Name] = mi.GetValue(instance);
        }

        /// <summary>
        ///     Получить конструкторы типа и его базовых типов
        /// </summary>
        /// <returns>Массив информации о конструкторах</returns>
        public ConstructorInfo[] GetConstructors()
        {
            if (_constructors != null)
                return _constructors;

            _constructors = _type.GetConstructors(DefaultBindingFlags)
                .Concat(BaseTypes.Where(x => !x.IsInterface)
                .SelectMany(x => x.GetConstructors(DefaultBindingFlags)))
                .OrderBy(c => c.GetParameters().Length)
                .Distinct()
                .ToArray();
            return _constructors;
        }

        /// <summary>
        ///     Получить события типа и его базовых типов
        /// </summary>
        /// <returns>Массив информации о событиях</returns>
        public EventInfo[] GetEvents()
        {
            if (_events != null) return _events;
            _events = _type.GetEvents(DefaultBindingFlags)
                .Concat(BaseTypes.Where(x => !x.IsInterface)
                .SelectMany(x => x.GetEvents(DefaultBindingFlags)))
                .Distinct()
                .ToArray();
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
                .Concat(BaseTypes.Where(x => !x.IsInterface)
                    .SelectMany(x => x.GetFields(DefaultBindingFlags)))
                .Distinct()
                .ToArray();

            return _fields;
        }

        /// <summary>
        ///     Получить методы типа и его базовых типов
        /// </summary>
        /// <returns>Массив информации о методах</returns>
        public MethodInfo[] GetMethods()
        {
            if (_methods != null) return _methods;
            _methods = _type.GetMethods(DefaultBindingFlags)
                .Concat(BaseTypes.Where(x => !x.IsInterface)
                    .SelectMany(x => x.GetMethods(DefaultBindingFlags)))
                .Distinct()
                .ToArray();

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

        public MemberCache GetForeignKey(Type children)
        {
            var childrenCache = Create(children);
            return childrenCache.ForeignKeys.FirstOrDefault(fk =>
            {
                var nav = childrenCache.GetProperty(fk.Value.ForeignKeyName);
                return nav?.PropertyType == Type;
            }).Value;
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
        public static MemberCache GetMemberCache(this MemberInfo memberInfo)
        {
            return MemberCache.Create(memberInfo);
        }

        internal static IEnumerable<TSource> DistinctBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

            var seenKeys = new HashSet<TKey>();

            foreach (var element in source)
                if (seenKeys.Add(keySelector(element)))
                    yield return element;
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

    public class MemberCache<T> : MemberCache
    {
        protected static readonly ConcurrentDictionary<Type, MemberCache<T>> MemberInfoCacheT =
            new ConcurrentDictionary<Type, MemberCache<T>>();

        public MemberCache(T defaultValue) : base(typeof(T))
        {
            DefaultValue = defaultValue;
            DefaultConstructor = Expression.Lambda<Func<T>>(Expression.New(typeof(T))).Compile();
        }

        public MemberCache(MemberInfo memberInfo, T defaultValue) : base(memberInfo)
        {
            DefaultValue = defaultValue;
            DefaultConstructor = Expression.Lambda<Func<T>>(Expression.New(typeof(T))).Compile();
        }

        private T DefaultValue { get; }
        public new Func<T> DefaultConstructor { get; }

        public static MemberCache<T> Create()
        {
            var memberCache = MemberInfoCache.GetOrAdd(typeof(T), x => new MemberCache(typeof(T)));
            var result = MemberInfoCacheT.GetOrAdd(typeof(T), x => new MemberCache<T>(memberCache, default));

            return result;
        }
    }
}