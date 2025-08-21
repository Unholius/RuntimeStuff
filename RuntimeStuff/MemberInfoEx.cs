using RuntimeStuff.Extensions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;

namespace RuntimeStuff
{
    /// <summary>
    /// Представляет расширенную обёртку над <see cref="MemberInfo"/>, предоставляющую унифицированный доступ к дополнительной информации и операциям для членов типа .NET (свойств, методов, полей, событий, конструкторов и самих типов).
    /// Класс предназначен для использования в сценариях динамического анализа типов, построения универсальных сериализаторов, ORM, генераторов кода, UI-редакторов и других задач, где требуется расширенная работа с метаданными .NET.
    /// <para>
    /// Класс <c>MemberInfoEx</c> позволяет:
    /// <list type="bullet">
    /// <item>— Получать расширенные сведения о членах типа, включая их атрибуты, типы, модификаторы доступа, связи с базовыми типами и интерфейсами.</item>
    /// <item>— Быстро и кэшированно получать члены по имени, включая поиск по альтернативным именам (отображаемое имя, JSON-имя, имя колонки и др.).</item>
    /// <item>— Определять семантику члена: является ли он свойством, методом, полем, событием, конструктором, типом и т.д.</item>
    /// <item>— Получать и устанавливать значения свойств и полей через делегаты, а также вызывать методы по отражению.</item>
    /// <item>— Работать с атрибутами, включая стандартные и пользовательские, а также поддерживать работу с атрибутами сериализации (JSON, XML, DataAnnotations).</item>
    /// <item>— Определять особенности члена: является ли он коллекцией, словарём, делегатом, nullable, числовым, булевым, кортежем, простым типом и др.</item>
    /// <item>— Получать информацию о первичных и внешних ключах, колонках, таблицах и схемах для интеграции с ORM и сериализаторами.</item>
    /// <item>— Кэшировать результаты для повышения производительности при повторных обращениях.</item>
    /// </list>
    /// </para>
    /// </summary>
    public class MemberInfoEx : MemberInfo
    {
        internal readonly MemberInfo MemberInfo;

        // Кэш для расширенной информации о членах класса
        private static readonly ConcurrentDictionary<MemberInfo, MemberInfoEx> _cache = new ConcurrentDictionary<MemberInfo, MemberInfoEx>();

        // Кэш делегатов для создания экземпляров типов
        private static readonly ConcurrentDictionary<string, Func<object>> _ctors = new ConcurrentDictionary<string, Func<object>>();

        // Кэш делегатов для получения значения свойства
        private static readonly ConcurrentDictionary<PropertyInfo, Func<object, object>> _propertyGetters = new ConcurrentDictionary<PropertyInfo, Func<object, object>>();

        // Кэш делегатов для установки значения свойства
        private static readonly ConcurrentDictionary<PropertyInfo, Action<object, object>> _propertySetters = new ConcurrentDictionary<PropertyInfo, Action<object, object>>();

        // Кэш делегатов для установки значения поля
        private static readonly ConcurrentDictionary<FieldInfo, Action<object, object>> _fieldSetters = new ConcurrentDictionary<FieldInfo, Action<object, object>>();

        // Кэш делегатов для получения значения поля
        private static readonly ConcurrentDictionary<FieldInfo, Func<object, object>> _fieldGetters = new ConcurrentDictionary<FieldInfo, Func<object, object>>();

        // Кэш для расширенной информации о членах класса по имени
        private readonly ConcurrentDictionary<string, MemberInfoEx> _memberCache = new ConcurrentDictionary<string, MemberInfoEx>();

        private readonly string _name;
        private readonly Type _type;
        private static readonly OpCode[] s_oneByte = new OpCode[256];
        private static readonly OpCode[] s_twoByte = new OpCode[256];
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

        static MemberInfoEx()
        {
            // Построим таблицу опкодов для дешифровки IL
            foreach (var fi in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                var op = (OpCode)fi.GetValue(null);
                ushort v = (ushort)op.Value;
                if (v < 0x100) s_oneByte[v] = op;
                else s_twoByte[v & 0xFF] = op;
            }
        }

        /// <summary>
        /// Конструктор для создания расширенной информации о члене класса
        /// </summary>
        /// <param name="memberInfo">Информация о члене класса</param>
        /// <param name="recursive">Рекурсивно обрабатывать все члены</param>
        internal MemberInfoEx(MemberInfo memberInfo, bool recursive = false)
        {
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
            Type = _type ?? throw new NotSupportedException($"{nameof(MemberInfoEx)}: ({memberInfo.GetType().Name}) not supported!");
            IsDictionary = _type.IsDictionary();
            IsDelegate = _type.IsDelegate();
            IsFloat = _type.IsFloat();
            IsNullable = _type.IsNullable();
            IsNumeric = _type.IsNumeric();
            IsBoolean = _type.IsBoolean();
            IsCollection = _type.IsCollection();
            ElementType = IsCollection ? Type.GetCollectionItemType() : null;
            IsBasic = _type.IsBasic();
            IsBasicCollection = IsCollection && ElementType.IsBasic();
            IsObject = _type == typeof(object);
            IsTuple = _type.IsTuple();
            IsProperty = pi != null;
            IsEvent = e != null;
            IsField = fi != null;
            IsType = t != null;
            IsMethod = mi != null;
            IsConstructor = ci != null;
            CanWrite = pi != null ? pi.CanWrite : fi != null;
            CanRead = pi != null ? pi.CanRead : fi != null;
            IsPublic = IsProperty ? AsPropertyInfo().GetAccessors().Any(m => m.IsPublic) : IsField ? AsFieldInfo().IsPublic : IsMethod ? AsMethodInfo().IsPublic : IsConstructor && AsConstructorInfo().IsPublic;
            IsPrivate = IsProperty ? AsPropertyInfo().GetAccessors().Any(m => m.IsPrivate) : IsField ? AsFieldInfo().IsPrivate : IsMethod ? AsMethodInfo().IsPrivate : IsConstructor && AsConstructorInfo().IsPrivate;

            // Дополнительная обработка для типов
            if (IsType)
            {
                DefaultConstructor = CreateConstructorDelegate(t);
                PrimaryKeys = Properties.Where(x => x.GetCustomAttribute<KeyAttribute>() != null).ToArray();
                ForeignKeys = Properties.Where(x => x.GetCustomAttribute<ForeignKeyAttribute>() != null).ToArray();
                var tblAttr = MemberInfo.GetCustomAttribute<TableAttribute>();
                TableName = tblAttr != null ? tblAttr.Name ?? Name : null;
                SchemaName = tblAttr?.Schema;
                Columns = Properties.Where(x => x.GetCustomAttribute<ColumnAttribute>() != null && x.GetCustomAttribute<NotMappedAttribute>() == null).ToArray();
            }

            // Дополнительная обработка для свойств
            if (pi != null)
            {
                var keyAttr = pi.GetCustomAttribute<KeyAttribute>();
                var colAttr = pi.GetCustomAttribute<ColumnAttribute>();
                var fkAttr = pi.GetCustomAttribute<ForeignKeyAttribute>();
                Setter = GetPropertySetter(pi);
                Getter = GetPropertyGetter(pi);
                PropertyBackingField = Fields.FirstOrDefault(x => x.Name == $"<{Name}>k__BackingField") ?? GetFieldInfoFromGetAccessor(pi.GetGetMethod(true));
                ColumnName = colAttr != null ? colAttr.Name ?? Name : (IsPrimaryKey ? Name : null);
                ForeignColumnName = fkAttr?.Name;
                IsPrimaryKey = keyAttr != null;
                IsForeignKey = fkAttr != null;
            }

            if (fi != null)
            {
                Setter = GetFieldSetter(fi);
                Getter = GetFieldGetter(fi);
            }

            // Обработка имени
            _name = MemberInfo.Name.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries)
                .LastOrDefault() ?? string.Empty;

            // Получение атрибутов
            Description = MemberInfo.GetCustomAttributes<DescriptionAttribute>().FirstOrDefault()?.Description;
            DisplayName = MemberInfo.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName;
            GroupName = MemberInfo.GetCustomAttribute<DisplayAttribute>()?.GroupName;

            // Рекурсивная загрузка членов класса
            if (recursive)
            {
                _members = GetMembersInternal();
            }
        }

        #region Статические свойства

        /// <summary>
        /// Флаги для поиска членов класса по умолчанию
        /// </summary>
        public static BindingFlags DefaultBindingFlags { get; set; } = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;

        #endregion

        #region Основные свойства

        /// <summary>
        /// Атрибуты члена класса
        /// </summary>
        public Attribute[] Attributes => _attributes ?? GetAttributes();

        /// <summary>
        /// Базовые типы и интерфейсы
        /// </summary>
        public Type[] BaseTypes
        {
            get
            {
                if (_baseTypes != null)
                    return _baseTypes;

                _baseTypes = _type.GetBaseTypes(getInterfaces: true);
                return _baseTypes;
            }
        }

        /// <summary>
        /// Можно ли читать значение (для свойств и полей)
        /// </summary>
        public bool CanRead { get; }

        /// <summary>
        /// Можно ли записывать значение (для свойств и полей)
        /// </summary>
        public bool CanWrite { get; }

        /// <summary>
        /// Имя колонки (из атрибута ColumnAttribute)
        /// </summary>
        public string ColumnName { get; }

        /// <summary>
        /// Колонки (свойства с атрибутом ColumnAttribute)
        /// </summary>
        public PropertyInfo[] Columns { get; } = Array.Empty<PropertyInfo>();

        /// <summary>
        /// Конструкторы типа
        /// </summary>
        public ConstructorInfo[] Constructors => _constructors ?? GetConstructors();

        /// <summary>
        /// Тип, объявивший этот член
        /// </summary>
        public override Type DeclaringType => MemberInfo.DeclaringType;

        /// <summary>
        /// Делегат конструктора по умолчанию
        /// </summary>
        public Func<object> DefaultConstructor { get; }

        /// <summary>
        /// Описание (из атрибута DescriptionAttribute)
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Отображаемое имя (из атрибута DisplayNameAttribute)
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Тип элемента коллекции (если текущий тип является коллекцией)
        /// </summary>
        public Type ElementType { get; }

        /// <summary>
        /// События типа
        /// </summary>
        public EventInfo[] Events => _events ?? GetEvents();

        /// <summary>
        /// Поля типа
        /// </summary>
        public FieldInfo[] Fields => _fields ?? GetFields();

        /// <summary>
        /// Имя внешнего ключа (из атрибута ForeignKeyAttribute)
        /// </summary>
        public string ForeignColumnName { get; }

        /// <summary>
        /// Свойства, помеченные как внешние ключи
        /// </summary>
        public PropertyInfo[] ForeignKeys { get; }

        /// <summary>
        /// Имя группы (из атрибута DisplayAttribute)
        /// </summary>
        public string GroupName { get; }

        /// <summary>
        /// Является ли тип простым (примитивным или строкой)
        /// </summary>
        public bool IsBasic { get; }

        /// <summary>
        /// Является ли коллекция коллекцией простых типов
        /// </summary>
        public bool IsBasicCollection { get; }

        /// <summary>
        /// Является ли тип булевым
        /// </summary>
        public bool IsBoolean { get; }

        /// <summary>
        /// Является ли тип коллекцией
        /// </summary>
        public bool IsCollection { get; }

        /// <summary>
        /// Является ли член конструктором
        /// </summary>
        public bool IsConstructor { get; set; }

        /// <summary>
        /// Является ли тип делегатом
        /// </summary>
        public bool IsDelegate { get; }

        /// <summary>
        /// Является ли тип словарем
        /// </summary>
        public bool IsDictionary { get; }

        /// <summary>
        /// Является ли член событием
        /// </summary>
        public bool IsEvent { get; set; }

        /// <summary>
        /// Является ли член полем
        /// </summary>
        public bool IsField { get; }

        /// <summary>
        /// Является ли тип числом с плавающей точкой
        /// </summary>
        public bool IsFloat { get; }

        /// <summary>
        /// Является ли свойство внешним ключом
        /// </summary>
        public bool IsForeignKey { get; set; }

        /// <summary>
        /// Является ли первичный ключ автоинкрементным (число или Guid)
        /// </summary>
        public bool IsIdentity => IsPrimaryKey && (Type.IsNumeric(false) || Type == typeof(Guid));

        /// <summary>
        /// Является ли тип интерфейсом
        /// </summary>
        public bool IsInterface => Type.IsInterface;

        /// <summary>
        /// Является ли член методом
        /// </summary>
        public bool IsMethod { get; set; }

        /// <summary>
        /// Является ли тип nullable
        /// </summary>
        public bool IsNullable { get; }

        /// <summary>
        /// Является ли тип числовым
        /// </summary>
        public bool IsNumeric { get; }

        /// <summary>
        /// Является ли тип object
        /// </summary>
        public bool IsObject { get; }

        /// <summary>
        /// Является ли свойство первичным ключом
        /// </summary>
        public bool IsPrimaryKey { get; set; }

        /// <summary>
        /// Является ли член приватным
        /// </summary>
        public bool IsPrivate { get; }

        /// <summary>
        /// Является ли член свойством
        /// </summary>
        public bool IsProperty { get; }

        /// <summary>
        /// Является ли член публичным
        /// </summary>
        public bool IsPublic { get; }

        /// <summary>
        /// Является ли тип кортежем
        /// </summary>
        public bool IsTuple { get; }

        /// <summary>
        /// Является ли член типом
        /// </summary>
        public bool IsType { get; }

        /// <summary>
        /// Является ли тип значимым типом
        /// </summary>
        public bool IsValueType => _type.IsValueType;

        /// <summary>
        /// Имя в JSON (из атрибутов JsonPropertyNameAttribute или JsonPropertyAttribute)
        /// </summary>
        public string JsonName
        {
            get
            {
                if (_jsonName != null)
                    return _jsonName;
                var jsonAttr = Attributes.FirstOrDefault(x => x.GetType().Name.StartsWith("Json"));
                if (jsonAttr != null)
                {
                    var propName = jsonAttr.GetType().GetProperties().FirstOrDefault(p => p.Name.EndsWith("Name"));
                    if (propName != null)
                        _jsonName = propName.GetValue(jsonAttr)?.ToString();
                }
                return _jsonName;
            }
        }

        /// <summary>
        /// Все члены типа (свойства, поля, методы и т.д.)
        /// </summary>
        public MemberInfoEx[] Members
        {
            get
            {
                if (_members != null)
                    return _members;
                _members = GetMembersInternal();
                return _members;
            }
        }

        /// <summary>
        /// Тип члена (свойство, метод, поле и т.д.)
        /// </summary>
        public override MemberTypes MemberType => MemberInfo.MemberType;

        /// <summary>
        /// Методы типа
        /// </summary>
        public MethodInfo[] Methods => _methods ?? GetMethods();

        /// <summary>
        /// Имя члена
        /// </summary>
        public sealed override string Name => _name;

        /// <summary>
        /// Родительский член (для вложенных членов)
        /// </summary>
        public MemberInfoEx Parent
        {
            get
            {
                if (_parent != null)
                    return _parent;
                if (MemberInfo.DeclaringType != null)
                    _parent = Create(MemberInfo.DeclaringType);

                return _parent;
            }
        }

        /// <summary>
        /// Свойства, помеченные как первичные ключи
        /// </summary>
        public PropertyInfo[] PrimaryKeys { get; }

        /// <summary>
        /// Свойства типа
        /// </summary>
        public PropertyInfo[] Properties => _properties ?? GetProperties();

        /// <summary>
        /// Поле, хранящее значение свойства (для автоматически реализуемых свойств)
        /// </summary>
        public FieldInfo PropertyBackingField { get; }

        /// <summary>
        /// Тип, через который был получен этот член
        /// </summary>
        public override Type ReflectedType => MemberInfo.ReflectedType;

        /// <summary>
        /// Имя схемы (из атрибута TableAttribute.Schema)
        /// </summary>
        public string SchemaName { get; }

        /// <summary>
        /// Имя таблицы (из атрибута TableAttribute.Name)
        /// </summary>
        public string TableName { get; }

        /// <summary>
        /// Тип члена
        /// </summary>
        public Type Type { get; }

        /// <summary>
        /// Имя XML атрибута (из XmlAttributeAttribute)
        /// </summary>
        public string XmlAttributeName
        {
            get
            {
                if (_xmlAttr != null)
                    return _xmlAttr;
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

                return _xmlAttr ?? (_xmlAttr = "");
            }
        }

        /// <summary>
        /// Имя XML элемента (из XmlElementAttribute)
        /// </summary>
        public string XmlElementName
        {
            get
            {
                if (_xmlElem != null)
                    return _xmlElem;

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
                    _xmlElem = "";

                return _xmlElem;
            }
        }

        /// <summary>
        /// Имя в XML (элемент или атрибут)
        /// </summary>
        public string XmlName { get; } = null;

        /// <summary>
        /// Делегат для получения значения свойства
        /// </summary>
        public Func<object, object> Getter { get; }

        /// <summary>
        /// Делегат для установки значения свойства
        /// </summary>
        public Action<object, object> Setter { get; }

        #endregion

        #region Индексаторы

        /// <summary>
        /// Получить член по имени
        /// </summary>
        /// <param name="memberName">Имя члена для поиска</param>
        /// <returns>Найденный член или null, если не найден</returns>
        public MemberInfoEx this[string memberName] => this[memberName, MemberNameType.Any];

        /// <summary>
        /// Получить член по имени с фильтрацией
        /// </summary>
        /// <param name="memberName">Имя члена для поиска</param>
        /// <param name="memberNameType">Тип имени члена для поиска</param>
        /// <param name="memberFilter">Фильтр для отбора членов</param>
        /// <returns>Найденный член или null, если не найден</returns>
        public MemberInfoEx this[string memberName, MemberNameType memberNameType = MemberNameType.Any, Func<MemberInfoEx, bool> memberFilter = null] => GetMember(memberName, memberNameType, memberFilter);

        #endregion

        #region Статические методы

        /// <summary>
        /// Создать расширенную информацию о члене класса (с кэшированием)
        /// </summary>
        /// <param name="memberInfo">Информация о члене класса</param>
        /// <returns>Расширенная информация о члене класса</returns>
        public static MemberInfoEx Create(MemberInfo memberInfo)
        {
            return memberInfo == null ? null : _cache.GetOrAdd(memberInfo, x => new MemberInfoEx(x, x is Type));
        }

        /// <summary>
        /// Создаёт делегат для конструктора по умолчанию указанного типа.
        /// Используется для быстрой активации объектов без вызова <see cref="Activator.CreateInstance(Type)"/>.
        /// </summary>
        /// <param name="type">Тип, для которого создаётся делегат конструктора.</param>
        /// <returns>
        /// Делегат <see cref="Func{Object}"/>, который создаёт экземпляр типа, или <c>null</c>, если конструктор по умолчанию отсутствует.
        /// </returns>
        public static Func<object> CreateConstructorDelegate(Type type)
        {
            if (type == null)
                return null;

            if (_ctors.TryGetValue(type.FullName ?? type.Name, out var ctor))
                return ctor;

            var constructorInfo = type.GetConstructor(Type.EmptyTypes);
            if (constructorInfo == null)
            {
                _ctors[type.FullName ?? type.Name] = null;
                return null;
            }

            ctor = type.IsGenericTypeDefinition ? () => Activator.CreateInstance(type) : Expression.Lambda<Func<object>>(Expression.Convert(Expression.New(constructorInfo), typeof(object))).Compile();
            _ctors[type.FullName ?? type.Name] = ctor;
            return ctor;
        }

        /// <summary>
        /// Получает поле, связанное с методом получения свойства (геттером).
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
        /// Возвращает делегат для установки значения указанного свойства.<br/>
        /// Для повышения производительности используется кэш компилированных выражений.
        /// </summary>
        /// <param name="property">Свойство, для которого создаётся setter.</param>
        /// <returns>
        /// Делегат <see cref="Action{Object,Object}"/>, который принимает объект-владельца (или <c>null</c> для static свойств)
        /// и новое значение. Если свойство read-only, возвращается <c>null</c>.
        /// </returns>
        public static Action<object, object> GetPropertySetter(PropertyInfo property)
        {
            return _propertySetters.GetOrAdd(property, CreatePropertySetter);
        }

        /// <summary>
        /// Создаёт делегат для установки значения свойства.
        /// </summary>
        /// <param name="property">Свойство, для которого создаётся setter.</param>
        /// <returns>Делегат для установки значения свойства или null, если свойство read-only</returns>
        private static Action<object, object> CreatePropertySetter(PropertyInfo property)
        {
            //var setMethod = property.GetSetMethod(nonPublic: true);
            //if (setMethod == null)
            //{
            //    var backingField = GetPropertyBackingField(property);
            //    return backingField == null ? null : GetFieldSetter(backingField);
            //}

            //var method = new DynamicMethod(
            //    $"Set_{property.Name}",
            //    typeof(void),
            //    new[] { typeof(object), typeof(object) },
            //    property.DeclaringType,
            //    true);

            //var il = method.GetILGenerator();

            //if (!setMethod.IsStatic)
            //{
            //    il.Emit(OpCodes.Ldarg_0);
            //    il.Emit(OpCodes.Castclass, property.DeclaringType);
            //}

            //il.Emit(OpCodes.Ldarg_1);
            //il.Emit(OpCodes.Unbox_Any, property.PropertyType);

            //il.Emit(setMethod.IsStatic ? OpCodes.Call : OpCodes.Callvirt, setMethod);
            //il.Emit(OpCodes.Ret);

            //return (Action<object, object>)method.CreateDelegate(typeof(Action<object, object>));

            var setMethod = property.GetSetMethod(nonPublic: true);
            if (setMethod == null)
            {
                var backingField = GetPropertyBackingField(property);
                return backingField == null ? null : GetFieldSetter(backingField);
            }

            var objParam = Expression.Parameter(typeof(object), "obj");
            var valueParam = Expression.Parameter(typeof(object), "value");
            var valueCast = Expression.Convert(valueParam, property.PropertyType);

            Expression body;

            if (setMethod.IsStatic)
            {
                // static: Class.Property = (T)value
                body = Expression.Assign(Expression.Property(null, property), valueCast);
            }
            else
            {
                // instance: ((TDeclaring)obj).Property = (T)value
                var instanceCast = Expression.Convert(objParam, property.DeclaringType);
                body = Expression.Assign(Expression.Property(instanceCast, property), valueCast);
            }

            return Expression.Lambda<Action<object, object>>(body, objParam, valueParam).Compile();
        }

        /// <summary>
        /// Возвращает делегат для получения значения указанного свойства.<br/>
        /// Для повышения производительности используется кэш компилированных выражений.
        /// </summary>
        /// <param name="property">Свойство, для которого создаётся getter.</param>
        /// <returns>
        /// Делегат <see cref="Func{Object,Object}"/>, который принимает объект-владельца (или <c>null</c> для static свойств)
        /// и возвращает текущее значение свойства.
        /// </returns>
        public static Func<object, object> GetPropertyGetter(PropertyInfo property)
        {
            return _propertyGetters.GetOrAdd(property, CreatePropertyGetter);
        }

        /// <summary>
        /// Создаёт делегат для получения значения свойства.
        /// </summary>
        /// <param name="property">Свойство, для которого создаётся getter.</param>
        /// <returns>Делегат для получения значения свойства</returns>
        private static Func<object, object> CreatePropertyGetter(PropertyInfo property)
        {
            var getMethod = property.GetGetMethod(nonPublic: true) ?? throw new InvalidOperationException($"Property {property.Name} has no getter.");
            var objParam = Expression.Parameter(typeof(object), "obj");

            Expression body;
            if (getMethod.IsStatic)
            {
                // static: (object)Class.Property
                body = Expression.Convert(Expression.Property(null, property), typeof(object));
            }
            else
            {
                // instance: (object)((TDeclaring)obj).Property
                var instanceCast = Expression.Convert(objParam, property.DeclaringType);
                body = Expression.Convert(Expression.Property(instanceCast, property), typeof(object));
            }

            return Expression.Lambda<Func<object, object>>(body, objParam).Compile();
        }

        /// <summary>
        /// Пытается определить поле, используемое для хранения значения свойства.<br/>
        /// Работает как для автоматически реализованных свойств (backing field вида <c>&lt;Имя&gt;k__BackingField</c>),
        /// так и для свойств с ручной реализацией, если их геттер/сеттер явно обращается к полю.
        /// </summary>
        /// <param name="propertyInfo">Свойство, для которого ищется поле.</param>
        /// <returns>
        /// Экземпляр <see cref="FieldInfo"/>, если удалось найти поле, или <c>null</c>,
        /// если backing field отсутствует (например, свойство вычисляемое).
        /// </returns>
        /// <exception cref="ArgumentNullException">Если <paramref name="propertyInfo"/> равен <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException">Если свойство не имеет типа-владельца.</exception>
        public static FieldInfo GetPropertyBackingField(PropertyInfo propertyInfo)
        {
            if (propertyInfo == null) throw new ArgumentNullException(nameof(propertyInfo));
            var declType = propertyInfo.DeclaringType
                ?? throw new InvalidOperationException("Property has no declaring type.");

            // 1) Попытка: авто-свойство
            string autoName = $"<{propertyInfo.Name}>k__BackingField";
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
            var field = declType.GetField(autoName, flags);
            if (field != null) return field;

            // 2) Попытка: достать поле из IL геттера/сеттера
            return TryGetFieldFromAccessor(propertyInfo.GetGetMethod(true))
                 ?? TryGetFieldFromAccessor(propertyInfo.GetSetMethod(true));
        }

        /// <summary>
        /// Пытается определить поле из IL кода метода доступа (геттера/сеттера).
        /// </summary>
        /// <param name="accessor">Метод доступа (геттер или сеттер свойства)</param>
        /// <returns>Найденное поле или null, если не удалось определить</returns>
        private static FieldInfo TryGetFieldFromAccessor(MethodInfo accessor)
        {
            if (accessor == null) return null;
            var body = accessor.GetMethodBody();
            if (body == null) return null;

            var il = body.GetILAsByteArray();
            if (il == null || il.Length == 0) return null;

            int i = 0;
            var module = accessor.Module;
            var typeArgs = accessor.DeclaringType?.GetGenericArguments();
            var methodArgs = accessor.GetGenericArguments();

            while (i < il.Length)
            {
                OpCode op;
                byte code = il[i++];

                if (code != 0xFE)
                {
                    op = s_oneByte[code];
                }
                else
                {
                    byte b2 = il[i++];
                    op = s_twoByte[b2];
                }

                switch (op.OperandType)
                {
                    case OperandType.InlineNone:
                        break;

                    case OperandType.ShortInlineI:
                    case OperandType.ShortInlineVar:
                    case OperandType.ShortInlineBrTarget:
                        i ++;
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
                        int count = BitConverter.ToInt32(il, i);
                        i += 4 + (4 * count);
                        break;

                    case OperandType.InlineField:
                        // Вот он — операнд поля у ldfld/ldsfld/stfld/stsfld/ldflda
                        int token = BitConverter.ToInt32(il, i);
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

                    default:
                        // На всякий случай сместим хотя бы на 0, но сюда обычно не попадём
                        break;
                }
            }

            return null;
        }

        /// <summary>
        /// Возвращает делегат для установки значения указанного поля.<br/>
        /// Для повышения производительности используется кэш компилированных выражений.
        /// </summary>
        /// <param name="field">Поле, для которого создаётся setter.</param>
        /// <returns>
        /// Делегат <see cref="Action{Object,Object}"/>, который принимает объект-владельца (или <c>null</c> для static полей)
        /// и новое значение. Если поле <c>readonly</c> или <c>const</c>, возвращается <c>null</c>.
        /// </returns>
        public static Action<object, object> GetFieldSetter(FieldInfo field)
            => _fieldSetters.GetOrAdd(field, CreateFieldSetter);

        /// <summary>
        /// Создаёт делегат для установки значения поля.
        /// </summary>
        /// <param name="field">Поле, для которого создаётся setter.</param>
        /// <returns>Делегат для установки значения поля или null, если поле read-only</returns>
        private static Action<object, object> CreateFieldSetter(FieldInfo field)
        {
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
                    var instanceCast = Expression.Convert(objParam, field.DeclaringType);
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
        /// Возвращает делегат для получения значения указанного поля.<br/>
        /// Поддерживаются instance и static поля, включая приватные.
        /// </summary>
        /// <param name="field">Поле, для которого создаётся getter.</param>
        /// <returns>
        /// Делегат <see cref="Func{Object,Object}"/>, который принимает объект-владельца (или <c>null</c> для static полей)
        /// и возвращает текущее значение поля.
        /// </returns>
        public static Func<object, object> GetFieldGetter(FieldInfo field)
            => _fieldGetters.GetOrAdd(field, CreateFieldGetter);

        /// <summary>
        /// Создаёт делегат для получения значения поля.
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
                var instanceCast = Expression.Convert(objParam, field.DeclaringType);
                body = Expression.Convert(Expression.Field(instanceCast, field), typeof(object));
            }

            return Expression.Lambda<Func<object, object>>(body, objParam).Compile();
        }

        #endregion

        #region Методы преобразования

        /// <summary>
        /// Возвращает <see cref="ConstructorInfo"/> для текущего члена, если он является конструктором.
        /// </summary>
        /// <returns>Экземпляр <see cref="ConstructorInfo"/>, либо <c>null</c>.</returns>
        public ConstructorInfo AsConstructorInfo()
        {
            return MemberInfo as ConstructorInfo;
        }

        /// <summary>
        /// Возвращает <see cref="EventInfo"/> для текущего члена, если он является событием.
        /// </summary>
        /// <returns>Экземпляр <see cref="EventInfo"/>, либо <c>null</c>.</returns>
        public EventInfo AsEventInfo()
        {
            return MemberInfo as EventInfo;
        }

        /// <summary>
        /// Возвращает <see cref="FieldInfo"/> для текущего члена, если он является полем.
        /// </summary>
        /// <returns>Экземпляр <see cref="FieldInfo"/>, либо <c>null</c>.</returns>
        public FieldInfo AsFieldInfo()
        {
            return MemberInfo as FieldInfo;
        }

        /// <summary>
        /// Возвращает <see cref="MethodInfo"/> для текущего члена, если он является методом.
        /// </summary>
        /// <returns>Экземпляр <see cref="MethodInfo"/>, либо <c>null</c>.</returns>
        public MethodInfo AsMethodInfo()
        {
            return MemberInfo as MethodInfo;
        }

        /// <summary>
        /// Возвращает <see cref="PropertyInfo"/> для текущего члена, если он является свойством.
        /// </summary>
        /// <returns>Экземпляр <see cref="PropertyInfo"/>, либо <c>null</c>.</returns>
        public PropertyInfo AsPropertyInfo()
        {
            return MemberInfo as PropertyInfo;
        }

        /// <summary>
        /// Возвращает <see cref="Type"/> для текущего члена, если он является типом.
        /// </summary>
        /// <returns>Экземпляр <see cref="Type"/>, либо <c>null</c>.</returns>
        public Type AsType()
        {
            return MemberInfo as Type;
        }

        #endregion

        #region Методы поиска членов

        /// <summary>
        /// Получает член по имени с возможностью фильтрации.
        /// </summary>
        /// <param name="name">Имя члена.</param>
        /// <param name="memberNamesType">Тип имен по которым вести поиск</param>
        /// <param name="membersFilter">Фильтр для отбора членов (опционально).</param>
        /// <param name="nameComparison">Сравнение имен</param>
        /// <returns>Экземпляр <see cref="MemberInfoEx"/>, либо <c>null</c>, если член не найден.</returns>
        public MemberInfoEx GetMember(string name, MemberNameType memberNamesType = MemberNameType.Any, Func<MemberInfoEx, bool> membersFilter = null, StringComparison nameComparison = StringComparison.OrdinalIgnoreCase)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            if (_memberCache.TryGetValue(name, out var mx))
                return mx;

            if (memberNamesType == MemberNameType.Any || memberNamesType.HasFlag(MemberNameType.Name))
            {
                // Быстрый поиск свойства
                var quickProp = Type.GetLowestProperty(name);
                if (quickProp != null)
                {
                    mx = new MemberInfoEx(quickProp);
                    _memberCache[name] = mx;
                    if (membersFilter == null || membersFilter(mx))
                        return mx;
                }

                // Быстрый поиск поля
                var quickField = Type.GetLowestField(name);
                if (quickField != null)
                {
                    mx = new MemberInfoEx(quickField);
                    _memberCache[name] = mx;
                    if (membersFilter == null || membersFilter(mx))
                        return mx;
                }

                // Быстрый поиск метода
                var quickMethod = Type.GetLowestMethod(name);
                if (quickMethod != null)
                {
                    mx = new MemberInfoEx(quickMethod);
                    _memberCache[name] = mx;
                    if (membersFilter == null || membersFilter(mx))
                        return mx;
                }

                // Быстрый поиск события
                var quickEvent = Type.GetLowestEvent(name);
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
                (x => x.Name,        MemberNameType.Name),
                (x => x.DisplayName, MemberNameType.DisplayName),
                (x => x.JsonName,    MemberNameType.JsonName),
                (x => x.XmlName,     MemberNameType.XmlName),
                (x => x.ColumnName,  MemberNameType.ColumnName),
                (x => x.TableName,   MemberNameType.TableName),
                (x => x.SchemaName,  MemberNameType.SchemaName),
            };

            var searchIndex = 0;
            for (var idx=0; idx < searchNames.Length; idx++)
            {
                var (f, flag) = searchNames[idx];

                // Если явно указан memberNamesType и он не включает этот тип имени — пропускаем
                if (memberNamesType != MemberNameType.Any && (memberNamesType & flag) == 0)
                    continue;

                // Ищем по совпадению имени
                if (mx == null)
                {
                    mx = Members.FirstOrDefault(x =>
                        f(x)?.Equals(name, nameComparison) == true &&
                        (membersFilter == null || membersFilter(x)));
                }

                // Ищем по совпадению с удалением специальных символов
                if (mx == null)
                {
                    mx = Members.FirstOrDefault(x =>
                        Regex.Replace($"{f(x)}", "[ \\-_\\.]", "").Equals(Regex.Replace(name, "[ \\-_\\.]", ""),
                            nameComparison) && (membersFilter == null || membersFilter(x)));
                }

                if (mx != null)
                {
                    _memberCache[name] = mx;
                    return mx;
                }

                searchIndex++;
            }

            return null;
        }

        /// <summary>
        /// Получает конструктор по имени.
        /// </summary>
        /// <param name="methodName">Имя конструктора.</param>
        /// <returns>Экземпляр <see cref="ConstructorInfo"/>, либо <c>null</c>.</returns>
        public ConstructorInfo GetConstructor(string methodName) => GetMember(methodName)?.AsConstructorInfo();

        /// <summary>
        /// Получает конструктор, подходящий для указанных аргументов.
        /// </summary>
        /// <param name="ctorArgs">Аргументы конструктора. Может быть изменён для добавления значений по умолчанию.</param>
        /// <returns>Экземпляр <see cref="ConstructorInfo"/>, либо <c>null</c>, если подходящий конструктор не найден.</returns>
        public ConstructorInfo GetConstructorByArgs(ref object[] ctorArgs)
        {
            var args = ctorArgs;
            foreach (var c in Constructors)
            {
                var pAll = c.GetParameters();
                if (pAll.Length == ctorArgs.Length && ctorArgs.All((_, i) => args[i]?.GetType().IsImplements(pAll[i].ParameterType) == true))
                    return c;
                var pNoDef = c.GetParameters().Where(p => !p.HasDefaultValue).ToArray();
                //var pDef = c.GetParameters().Where(p => p.HasDefaultValue).ToArray();
                if (pNoDef.Length == ctorArgs.Length && ctorArgs.All((_, i) => args[i]?.GetType().IsImplements(pNoDef[i].ParameterType) == true))
                {
                    Array.Resize(ref ctorArgs, pAll.Length);
                    for (int i = pNoDef.Length; i < pAll.Length; i++)
                        ctorArgs[i] = pAll[i].DefaultValue;
                    return c;
                }
            }

            return null;
        }

        /// <summary>
        /// Получает событие по имени.
        /// </summary>
        /// <param name="eventName">Имя события.</param>
        /// <returns>Экземпляр <see cref="EventInfo"/>, либо <c>null</c>.</returns>
        public EventInfo GetEvent(string eventName) => GetMember(eventName)?.AsEventInfo();

        /// <summary>
        /// Получает поле по имени.
        /// </summary>
        /// <param name="fieldName">Имя поля.</param>
        /// <returns>Экземпляр <see cref="FieldInfo"/>, либо <c>null</c>.</returns>
        public FieldInfo GetField(string fieldName) => GetMember(fieldName)?.AsFieldInfo();

        /// <summary>
        /// Получает метод по имени.
        /// </summary>
        /// <param name="methodName">Имя метода.</param>
        /// <returns>Экземпляр <see cref="MethodInfo"/>, либо <c>null</c>.</returns>
        public MethodInfo GetMethod(string methodName) => GetMember(methodName)?.AsMethodInfo();

        /// <summary>
        /// Получает свойство по имени.
        /// </summary>
        /// <param name="propertyName">Имя свойства.</param>
        /// <returns>Экземпляр <see cref="PropertyInfo"/>, либо <c>null</c>.</returns>
        public PropertyInfo GetProperty(string propertyName) => GetMember(propertyName)?.AsPropertyInfo();

        #endregion

        #region Методы работы со значениями

        /// <summary>
        /// Получает значение члена (свойства, поля или метода) для указанного экземпляра объекта.<br/>
        /// Использует кешированный делегат <see cref="Getter"/> для получения значения, если он доступен.<br/>
        /// Не использует преобразование типов, если тип значения не совпадает с типом свойства, то выдается исключение.<br/>
        /// </summary>
        /// <param name="objectInstance">Экземпляр объекта, из которого извлекается значение.</param>
        /// <param name="args">Аргументы для метода, если член является методом.</param>
        /// <returns>Значение члена, либо <c>null</c> в случае ошибки.</returns>
        public object GetValue(object objectInstance, params object[] args)
        {
            if (IsProperty && Getter != null)
            {
                return Getter(objectInstance);
            }

            if (IsField && Getter != null)
            {
                return Getter(objectInstance);
            }

            if (IsMethod)
            {
                return AsMethodInfo()?.Invoke(objectInstance, args);
            }

            return null;
        }

        /// <summary>
        /// Устанавливает значение свойства или поля для указанного экземпляра объекта.<br/>
        /// Использует кешированный делегат <see cref="Setter"/> для установки значения, если он доступен.<br/>
        /// Не использует преобразование типов, если тип значения не совпадает с типом свойства, то выдается исключение.<br/>
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

        #endregion

        #region Методы работы с атрибутами

        /// <summary>
        /// Получает все атрибуты члена.
        /// </summary>
        /// <param name="inherit">Учитывать ли атрибуты из цепочки наследования.</param>
        /// <returns>Массив атрибутов.</returns>
        public override object[] GetCustomAttributes(bool inherit)
        {
            return MemberInfo.GetCustomAttributes(inherit);
        }

        /// <summary>
        /// Получает атрибуты указанного типа.
        /// </summary>
        /// <param name="attributeType">Тип атрибута.</param>
        /// <param name="inherit">Учитывать ли атрибуты из цепочки наследования.</param>
        /// <returns>Массив атрибутов указанного типа.</returns>
        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            return MemberInfo.GetCustomAttributes(attributeType, inherit);
        }

        /// <summary>
        /// Проверяет, определён ли атрибут указанного типа.
        /// </summary>
        /// <param name="attributeType">Тип атрибута.</param>
        /// <param name="inherit">Учитывать ли атрибуты из цепочки наследования.</param>
        /// <returns><c>true</c>, если атрибут определён; иначе <c>false</c>.</returns>
        public override bool IsDefined(Type attributeType, bool inherit)
        {
            return MemberInfo.IsDefined(attributeType, inherit);
        }

        /// <summary>
        /// Проверяет наличие атрибута указанного типа.
        /// </summary>
        /// <typeparam name="T">Тип атрибута.</typeparam>
        /// <param name="filter">Фильтр для отбора атрибутов (опционально).</param>
        /// <returns><c>true</c>, если атрибут найден; иначе <c>false</c>.</returns>
        public bool HasAttribute<T>(Func<T, bool> filter = null) where T : Attribute
        {
            if (filter == null)
                filter = (_) => true;
            return Attributes.OfType<T>().Any(filter);
        }

        /// <summary>
        /// Проверяет наличие атрибута указанного типа по имени.
        /// </summary>
        /// <param name="typeNames">Имя типа</param>
        public bool HasAttributeOfType(params string[] typeNames)
        {
            return Attributes.Any(a => a.GetType().Name.In(typeNames));
        }

        #endregion

        #region Методы работы с ORM

        private MemberInfoEx[] _columns = null;

        /// <summary>
        /// Получает коллекцию простых публичных свойств, которые подходят для ORM колонок.<br/>
        /// </summary>
        /// <returns>Массив <see cref="MemberInfoEx"/> для колонок.</returns>
        public MemberInfoEx[] GetColumns()
        {
            if (_columns != null)
                return _columns;

            _columns = Members.Where(x => x.HasAttributeOfType("ColumnAttribute", "KeyAttribute", "ForeignKeyAttribute")).ToArray();

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
        /// Получает коллекцию свойств, представляющих таблицы (коллекции сложных типов без атрибута NotMapped).
        /// </summary>
        /// <returns>Массив <see cref="MemberInfoEx"/> для таблиц.</returns>
        public MemberInfoEx[] GetTables()
        {
            return Members.Where(x =>
                x.IsProperty &&
                x.IsPublic &&
                x.IsCollection &&
                !x.IsBasicCollection &&
                x.Attributes.All(a => a.GetType().Name != "NotMappedAttribute")).ToArray();
        }

        #endregion

        #region Вспомогательные методы

        /// <summary>
        /// Возвращает строковое представление члена в формате "Имя (Тип)".
        /// </summary>
        /// <returns>Строка с именем и типом члена.</returns>
        public override string ToString()
        {
            return $"\"{Name}\" ({Type.Name})";
        }

        #endregion

        #region Внутренние методы

        /// <summary>
        /// Получить все члены типа (свойства, поля, методы и т.д.)
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
        /// Получить атрибуты члена и его базовых типов
        /// </summary>
        /// <returns>Массив атрибутов</returns>
        private Attribute[] GetAttributes()
        {
            _attributes = MemberInfo.GetCustomAttributes().Concat(BaseTypes.SelectMany(x => x.GetCustomAttributes())).ToArray();
            return _attributes;
        }

        /// <summary>
        /// Получить конструкторы типа
        /// </summary>
        /// <returns>Массив информации о конструкторах</returns>
        private ConstructorInfo[] GetConstructors()
        {
            _constructors = _type.GetConstructors(DefaultBindingFlags);
            return _constructors;
        }

        /// <summary>
        /// Получить события типа
        /// </summary>
        /// <returns>Массив информации о событиях</returns>
        private EventInfo[] GetEvents()
        {
            _events = _type.GetEvents(DefaultBindingFlags);
            return _events;
        }

        /// <summary>
        /// Получить поля типа и его базовых типов
        /// </summary>
        /// <returns>Массив информации о полях</returns>
        private FieldInfo[] GetFields()
        {
            _fields = _type.GetFields(DefaultBindingFlags).Concat(BaseTypes.SelectMany(x => x.GetFields(DefaultBindingFlags))).ToArray();
            return _fields;
        }

        /// <summary>
        /// Получить методы типа
        /// </summary>
        /// <returns>Массив информации о методах</returns>
        private MethodInfo[] GetMethods()
        {
            _methods = _type.GetMethods(DefaultBindingFlags);
            return _methods;
        }

        /// <summary>
        /// Получить свойства типа и его базовых типов (кроме интерфейсов)
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
            {
                if (!l.ContainsKey(p.Name))
                    l.Add(p.Name, p);
            }

            _properties = l.Values.ToArray();
            return _properties;
        }

        #endregion
    }

    [Flags]
    public enum MemberNameType
    {
        Any = 0,
        Name = 1,
        DisplayName = 2,
        JsonName = 4,
        XmlName = 8,
        ColumnName = 16,
        TableName = 32,
        SchemaName = 64,
    }

    public enum MemberNameComparison
    {
        Exact,
        IgnoreCase,
        IgnoreSpecialChars,
    }

    #region Методы расширения

    /// <summary>
    /// Методы расширения для MemberInfo
    /// </summary>
    public static class MemberInfoExtensions
    {
        /// <summary>
        /// Получить расширенную информацию о члене класса
        /// </summary>
        /// <param name="memberInfo">Информация о члене класса</param>
        /// <returns>Расширенная информация о члене класса</returns>
        public static MemberInfoEx GetMemberInfoEx(this MemberInfo memberInfo)
        {
            if (memberInfo is MemberInfoEx miex)
                return miex;

            return MemberInfoEx.Create(memberInfo);
        }
    }

    #endregion
}