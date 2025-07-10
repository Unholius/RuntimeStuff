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
using System.Text.RegularExpressions;

namespace RuntimeStuff
{
    /// <summary>
    /// Расширенная информация о члене класса (свойстве, методе, поле и т.д.)
    /// </summary>
    public class MemberInfoEx : MemberInfo
    {
        internal readonly MemberInfo MemberInfo;
        private static readonly ConcurrentDictionary<MemberInfo, MemberInfoEx> _cache = new ConcurrentDictionary<MemberInfo, MemberInfoEx>();
        private static readonly Dictionary<string, Func<object>> _ctors = new Dictionary<string, Func<object>>();
        private static readonly Dictionary<string, Func<object, object>> _getters = new Dictionary<string, Func<object, object>>();
        private static readonly Dictionary<string, Action<object, object>> _setters = new Dictionary<string, Action<object, object>>();
        private readonly ConcurrentDictionary<string, MemberInfoEx> _memberCache = new ConcurrentDictionary<string, MemberInfoEx>();
        private readonly string _name;
        private readonly Type _type;

        // Кэшированные данные
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
            IsBasicCollection = IsCollection && ElementType.IsBasic() == true;
            IsObject = _type == typeof(object);
            IsTuple = _type.IsTuple();
            IsProperty = pi != null;
            IsEvent = e != null;
            IsField = fi != null;
            IsType = t != null;
            IsMethod = mi != null;
            IsConstructor = ci != null;
            CanWrite = pi != null ? pi.CanWrite : fi != null;
            CanRead = pi != null || fi != null ? pi?.CanRead ?? true : Type != null;
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
                SetMethodDelegate = CreatePropertySetter(pi.DeclaringType, pi.Name);
                GetMethodDelegate = CreatePropertyAccessor(pi.DeclaringType, pi.Name);
                PropertyBackingField = Fields.FirstOrDefault(x => x.Name == $"<{Name}>k__BackingField") ?? GetFieldInfoFromGetAccessor(pi.GetGetMethod(true));
                ColumnName = colAttr != null ? colAttr.Name ?? Name : (IsPrimaryKey ? Name : null);
                ForeignColumnName = fkAttr?.Name;
                IsPrimaryKey = keyAttr != null;
                IsForeignKey = fkAttr != null;
            }

            // Обработка имени
            _name = MemberInfo.Name.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries)
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

        // Свойства класса

        /// <summary>
        /// Флаги для поиска членов класса по умолчанию
        /// </summary>
        public static BindingFlags DefaultBindingFlags { get; set; } = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;

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
        public override string Name => _name;

        /// <summary>
        /// Родительский член (для вложенных членов)
        /// </summary>
        public MemberInfoEx Parent
        {
            get
            {
                if (_parent != null)
                    return _parent;
                if (MemberInfo is Type && MemberInfo.DeclaringType != null)
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

                if (_xmlAttr == null)
                    _xmlAttr = "";

                return _xmlAttr;
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
        public string XmlName { get; }

        /// <summary>
        /// Делегат для получения значения свойства
        /// </summary>
        private Func<object, object> GetMethodDelegate { get; }

        /// <summary>
        /// Делегат для установки значения свойства
        /// </summary>
        private Action<object, object> SetMethodDelegate { get; }

        // Индексаторы

        /// <summary>
        /// Получить член по имени
        /// </summary>
        public MemberInfoEx this[string memberName] => this[memberName, null];

        /// <summary>
        /// Получить член по имени с фильтрацией
        /// </summary>
        public MemberInfoEx this[string memberName, Func<MemberInfoEx, bool> memberFilter] => GetMember(memberName, memberFilter);

        // Методы класса

        /// <summary>
        /// Создать расширенную информацию о члене класса (с кэшированием)
        /// </summary>
        /// <param name="memberInfo">Информация о члене класса</param>
        /// <returns>Расширенная информация о члене класса</returns>
        public static MemberInfoEx Create(MemberInfo memberInfo)
        {
            if (memberInfo == null)
                return null;
            if (_cache.TryGetValue(memberInfo, out var ex))
                return ex;

            var result = new MemberInfoEx(memberInfo);
            _cache[memberInfo] = result;
            return result;
        }

        /// <summary>
        /// Создать делегат конструктора по умолчанию для типа
        /// </summary>
        /// <param name="type">Тип</param>
        /// <returns>Делегат конструктора</returns>
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
        /// Создать делегат для получения значения свойства
        /// </summary>
        /// <param name="type">Тип, содержащий свойство</param>
        /// <param name="propertyName">Имя свойства</param>
        /// <returns>Делегат для получения значения</returns>
        public static Func<object, object> CreatePropertyAccessor(Type type, string propertyName)
        {
            _getters.TryGetValue($"{type.FullName}.{propertyName}", out var getter);
            if (getter != null)
                return getter;
            var propertyInfo = type.GetProperties(DefaultBindingFlags)
                .FirstOrDefault(p => p.Name == propertyName && p.DeclaringType == type);

            if (propertyInfo == null)
            {
                var fieldInfo = type.GetField(propertyName, DefaultBindingFlags);
                if (fieldInfo == null)
                {
                    throw new ArgumentException($"Property or field '{propertyName}' not found on type '{type.FullName}'.");
                }

                getter = CreateFieldAccessor(fieldInfo);
            }
            else
            {
                getter = CreatePropertyAccessor(propertyInfo);
            }
            _getters[$"{type.FullName}.{propertyName}"] = getter;
            return getter;
        }

        /// <summary>
        /// Создать делегат для установки значения свойства
        /// </summary>
        /// <param name="type">Тип, содержащий свойство</param>
        /// <param name="propertyName">Имя свойства</param>
        /// <returns>Делегат для установки значения</returns>
        public static Action<object, object> CreatePropertySetter(Type type, string propertyName)
        {
            _setters.TryGetValue($"{type.FullName}.{propertyName}", out var setter);
            if (setter != null)
                return setter;
            var propertyInfo = type
                .GetProperties(DefaultBindingFlags).FirstOrDefault(x =>
                    x.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase));
            if (propertyInfo == null)
            {
                return null;
            }

            var instance = Expression.Parameter(typeof(object), "instance");
            var value = Expression.Parameter(typeof(object), "value");

            var instanceCast = Expression.Convert(instance, type);
            var valueCast = Expression.Convert(value, propertyInfo.PropertyType);
            if (propertyInfo.GetSetMethod(true) == null)
                return null;

            try
            {
                var propertySet = Expression.Call(instanceCast, propertyInfo.GetSetMethod(true), valueCast);

                setter = Expression.Lambda<Action<object, object>>(propertySet, instance, value).Compile();
                _setters[$"{type.FullName}.{propertyName}"] = setter;
                return setter;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Получить поле, связанное с методом получения свойства
        /// </summary>
        /// <param name="getMethod">Метод получения свойства</param>
        /// <returns>Информация о поле</returns>
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
        /// Получить информацию о конструкторе
        /// </summary>
        /// <returns>Информация о конструкторе</returns>
        public ConstructorInfo AsConstructorInfo()
        {
            return MemberInfo as ConstructorInfo;
        }

        /// <summary>
        /// Получить информацию о событии
        /// </summary>
        /// <returns>Информация о событии</returns>
        public EventInfo AsEventInfo()
        {
            return MemberInfo as EventInfo;
        }

        /// <summary>
        /// Получить информацию о поле
        /// </summary>
        /// <returns>Информация о поле</returns>
        public FieldInfo AsFieldInfo()
        {
            return MemberInfo as FieldInfo;
        }

        /// <summary>
        /// Получить информацию о методе
        /// </summary>
        /// <returns>Информация о методе</returns>
        public MethodInfo AsMethodInfo()
        {
            return MemberInfo as MethodInfo;
        }

        /// <summary>
        /// Получить информацию о свойстве
        /// </summary>
        /// <returns>Информация о свойстве</returns>
        public PropertyInfo AsPropertyInfo()
        {
            return MemberInfo as PropertyInfo;
        }

        /// <summary>
        /// Получить информацию о типе
        /// </summary>
        /// <returns>Информация о типе</returns>
        public Type AsType()
        {
            return MemberInfo as Type;
        }

        /// <summary>
        /// Получить колонки (простые публичные свойства без атрибута NotMapped)
        /// </summary>
        /// <returns>Массив информации о членах</returns>
        public MemberInfoEx[] GetColumns()
        {
            return Members.Where(x => x.IsProperty && x.IsPublic && x.IsBasic && !x.IsCollection && x.Attributes.All(a => a.GetType().Name != "NotMappedAttribute")).ToArray();
        }

        /// <summary>
        /// Получить конструктор по имени
        /// </summary>
        /// <param name="methodName">Имя конструктора</param>
        /// <returns>Информация о конструкторе</returns>
        public ConstructorInfo GetConstructor(string methodName) => GetMember(methodName)?.AsConstructorInfo();

        /// <summary>
        /// Получить конструктор, подходящий для указанных аргументов
        /// </summary>
        /// <param name="ctorArgs">Аргументы конструктора</param>
        /// <returns>Информация о конструкторе</returns>
        public ConstructorInfo GetConstructorByArgs(ref object[] ctorArgs)
        {
            var args = ctorArgs;
            foreach (var c in Constructors)
            {
                var pAll = c.GetParameters();
                if (pAll.Length == ctorArgs.Length && ctorArgs.All((x, i) => args[i]?.GetType()?.IsImplements(pAll[i].ParameterType) == true))
                    return c;
                var pNoDef = c.GetParameters().Where(p => !p.HasDefaultValue).ToArray();
                var pDef = c.GetParameters().Where(p => p.HasDefaultValue).ToArray();
                if (pNoDef.Length == ctorArgs.Length && ctorArgs.All((x, i) => args[i]?.GetType()?.IsImplements(pNoDef[i].ParameterType) == true))
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
        /// Получить атрибуты члена
        /// </summary>
        /// <param name="inherit">Искать в цепочке наследования</param>
        /// <returns>Массив атрибутов</returns>
        public override object[] GetCustomAttributes(bool inherit)
        {
            return MemberInfo.GetCustomAttributes(inherit);
        }

        /// <summary>
        /// Получить атрибуты указанного типа
        /// </summary>
        /// <param name="attributeType">Тип атрибута</param>
        /// <param name="inherit">Искать в цепочке наследования</param>
        /// <returns>Массив атрибутов</returns>
        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            return MemberInfo.GetCustomAttributes(attributeType, inherit);
        }

        /// <summary>
        /// Получить событие по имени
        /// </summary>
        /// <param name="eventName">Имя события</param>
        /// <returns>Информация о событии</returns>
        public EventInfo GetEvent(string eventName) => GetMember(eventName)?.AsEventInfo();

        /// <summary>
        /// Получить поле по имени
        /// </summary>
        /// <param name="fieldName">Имя поля</param>
        /// <returns>Информация о поле</returns>
        public FieldInfo GetField(string fieldName) => GetMember(fieldName)?.AsFieldInfo();

        /// <summary>
        /// Получить член по имени с возможностью фильтрации
        /// </summary>
        /// <param name="name">Имя члена</param>
        /// <param name="membersFilter">Фильтр членов</param>
        /// <returns>Информация о члене</returns>
        public MemberInfoEx GetMember(string name, Func<MemberInfoEx, bool> membersFilter = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            if (_memberCache.TryGetValue(name, out var mx))
                return mx;

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

            // Поиск по различным именам (основное имя, отображаемое имя, JSON имя и т.д.)
            var searchNames = new Func<MemberInfoEx, string>[]
            {
                x => x.Name,
                x => x.DisplayName,
                x => x.JsonName,
                x => x.XmlName,
                x => x.ColumnName,
                x => x.TableName,
                x => x.SchemaName,
            };

            foreach (var f in searchNames)
            {
                mx = Members.FirstOrDefault(x =>
                    f(x)?.Equals(f) == true && (membersFilter == null || membersFilter(x)));
                if (mx == null)
                    mx = Members.FirstOrDefault(x =>
                        f(x)?.Equals(name, StringComparison.OrdinalIgnoreCase) == true &&
                        (membersFilter == null || membersFilter(x)));
                if (mx == null)
                    mx = Members.FirstOrDefault(x =>
                        Regex.Replace($"{f(x)}", "[^a-zA-Z0-9]*", "").Equals(Regex.Replace(name, "[^a-zA-Z0-9]*", ""),
                            StringComparison.OrdinalIgnoreCase) && (membersFilter == null || membersFilter(x)));
                if (mx != null)
                {
                    _memberCache[name] = mx;
                    return mx;
                }
            }

            return null;
        }

        /// <summary>
        /// Получить метод по имени
        /// </summary>
        /// <param name="methodName">Имя метода</param>
        /// <returns>Информация о методе</returns>
        public MethodInfo GetMethod(string methodName) => GetMember(methodName)?.AsMethodInfo();

        /// <summary>
        /// Получить свойство по имени
        /// </summary>
        /// <param name="propertyName">Имя свойства</param>
        /// <returns>Информация о свойстве</returns>
        public PropertyInfo GetProperty(string propertyName) => GetMember(propertyName)?.AsPropertyInfo();

        /// <summary>
        /// Получить таблицы (коллекции не простых типов без атрибута NotMapped)
        /// </summary>
        /// <returns>Массив информации о членах</returns>
        public MemberInfoEx[] GetTables()
        {
            return Members.Where(x => x.IsProperty && x.IsPublic && x.IsCollection && !x.IsBasicCollection && x.Attributes.All(a => a.GetType().Name != "NotMappedAttribute")).ToArray();
        }

        /// <summary>
        /// Получить значение члена
        /// </summary>
        /// <param name="objectInstance">Экземпляр объекта</param>
        /// <param name="args">Аргументы для метода (если член является методом)</param>
        /// <returns>Значение члена</returns>
        public object GetValue(object objectInstance, params object[] args)
        {
            if (IsProperty && GetMethodDelegate != null)
            {
                try
                {
                    return GetMethodDelegate(objectInstance);
                }
                catch
                {
                    return null;
                }
            }

            if (IsField && GetMethodDelegate != null)
            {
                try
                {
                    return GetMethodDelegate(objectInstance);
                }
                catch
                {
                    return null;
                }
            }

            if (IsMethod)
            {
                try
                {
                    return AsMethodInfo()?.Invoke(objectInstance, args);
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }

        /// <summary>
        /// Проверить наличие атрибута
        /// </summary>
        /// <typeparam name="T">Тип атрибута</typeparam>
        /// <param name="filter">Фильтр атрибутов</param>
        /// <returns>Есть ли атрибут</returns>
        public bool HasAttribute<T>(Func<T, bool> filter = null) where T : Attribute
        {
            if (filter == null)
                filter = (x) => true;
            return Attributes.OfType<T>().Any(filter);
        }

        /// <summary>
        /// Проверить наличие атрибута указанного типа
        /// </summary>
        /// <param name="attributeType">Тип атрибута</param>
        /// <param name="inherit">Искать в цепочке наследования</param>
        /// <returns>Есть ли атрибут</returns>
        public override bool IsDefined(Type attributeType, bool inherit)
        {
            return MemberInfo.IsDefined(attributeType, inherit);
        }

        /// <summary>
        /// Установить значение члену
        /// </summary>
        /// <param name="objectInstance">Экземпляр объекта</param>
        /// <param name="value">Значение</param>
        /// <returns>Успешно ли установлено значение</returns>
        public bool SetValue(object objectInstance, object value)
        {
            if (IsProperty && SetMethodDelegate != null)
            {
                try
                {
                    SetMethodDelegate(objectInstance, value);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            if (IsField && SetMethodDelegate != null)
            {
                try
                {
                    SetMethodDelegate(objectInstance, value);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Строковое представление члена
        /// </summary>
        /// <returns>Строка с именем и типом члена</returns>
        public override string ToString()
        {
            return $"\"{Name}\" ({Type.Name})";
        }

        // Внутренние методы

        /// <summary>
        /// Получить все члены типа (свойства, поля, методы и т.д.)
        /// </summary>
        /// <returns>Массив информации о членах</returns>
        internal MemberInfoEx[] GetMembersInternal()
        {
            return Properties.Select(p => new MemberInfoEx(p, false)).Concat(
                   Fields.Select(x => new MemberInfoEx(x, false))).Concat(
                   Methods.Select(x => new MemberInfoEx(x, false))).Concat(
                   Constructors.Select(x => new MemberInfoEx(x, false))).Concat(
                   Events.Select(x => new MemberInfoEx(x, false))).ToArray();
        }

        /// <summary>
        /// Создать делегат для получения значения поля
        /// </summary>
        /// <param name="fieldInfo">Информация о поле</param>
        /// <returns>Делегат для получения значения</returns>
        private static Func<object, object> CreateFieldAccessor(FieldInfo fieldInfo)
        {
            var parameter = Expression.Parameter(typeof(object), "obj");

            // Проверка, является ли поле статическим
            Expression field;
            if (fieldInfo.IsStatic)
            {
                field = Expression.Field(null, fieldInfo);
            }
            else
            {
                var castedParameter = Expression.Convert(parameter, fieldInfo.DeclaringType);
                field = Expression.Field(castedParameter, fieldInfo);
            }

            var castedField = Expression.Convert(field, typeof(object));
            return Expression.Lambda<Func<object, object>>(castedField, parameter).Compile();
        }

        /// <summary>
        /// Создать делегат для получения значения свойства
        /// </summary>
        /// <param name="propertyInfo">Информация о свойстве</param>
        /// <returns>Делегат для получения значения</returns>
        private static Func<object, object> CreatePropertyAccessor(PropertyInfo propertyInfo)
        {
            try
            {
                var parameter = Expression.Parameter(typeof(object), "obj");
                // Проверка, является ли свойство статическим
                Expression property;
                if (propertyInfo.GetGetMethod(true)?.IsStatic == true)
                {
                    property = Expression.Property(null, propertyInfo);
                }
                else
                {
                    var castedParameter = Expression.Convert(parameter, propertyInfo.DeclaringType);
                    property = Expression.Property(castedParameter, propertyInfo);
                }

                var castedProperty = Expression.Convert(property, typeof(object));

                return Expression.Lambda<Func<object, object>>(castedProperty, parameter).Compile();
            }
            catch
            {
                return null;
            }
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
                if (!l.ContainsKey(p.Name))
                    l.Add(p.Name, p);
            _properties = l.Values.ToArray();
            return _properties;
        }
    }

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
            return MemberInfoEx.Create(memberInfo);
        }
    }
}