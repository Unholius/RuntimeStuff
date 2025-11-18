using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace RuntimeStuff.Helpers
{
    /// <summary>
    ///     v.2025.10.05<br />
    ///     Вспомогательный класс для быстрого доступа к свойствам объектов с помощью скомпилированных делегатов.<br />
    ///     Позволяет получать и изменять значения свойств по имени без постоянного использования Reflection.<br />
    ///     Особенности:
    ///     <list type="bullet">
    ///         <item>
    ///             Создает делегаты-геттеры (<see cref="Func{T,Object}" />) и сеттеры (<see cref="Action{T, Object}" />)
    ///             для указанных свойств.
    ///         </item>
    ///         <item>
    ///             Использует кеширование для повторного использования скомпилированных выражений, что обеспечивает высокую
    ///             производительность.
    ///         </item>
    ///         <item>
    ///             Поддерживает работу как со ссылочными, так и со значимыми типами свойств (boxing выполняется
    ///             автоматически).
    ///         </item>
    ///     </list>
    ///     Пример:
    ///     <code>
    /// var getter = PropertyHelper.Getter&lt;Person&gt;("Name");
    /// var setter = PropertyHelper.Setter&lt;Person&gt;("Name");
    /// 
    /// var p = new Person { Name = "Alice" };
    /// Console.WriteLine(getter(p)); // Alice
    /// setter(p, "Bob");
    /// Console.WriteLine(getter(p)); // Bob
    /// </code>
    /// </summary>
    public static class TypeHelper
    {
        private static readonly string[] DateFormats =
        {
            // --- Только дата ---
            "yyyy-MM-dd",
            "dd.MM.yyyy",
            "MM/dd/yyyy",
            "yyyy/MM/dd",
            "dd-MM-yyyy",
            "yyyyMMdd",
            "dd MMM yyyy",
            "dd MMMM yyyy",
            "M/d/yyyy",
            "d/M/yyyy",
            "dd/MM/yyyy",
            "MM-dd-yyyy",
            "dd/MM/yy",
            "MM/dd/yy",

            // --- Дата + время ---
            "yyyy-MM-dd HH:mm:ss",
            "dd.MM.yyyy HH:mm:ss",
            "MM/dd/yyyy HH:mm:ss",
            "yyyy/MM/dd HH:mm:ss",
            "dd-MM-yyyy HH:mm:ss",

            // --- Дата + время + миллисекунды ---
            "yyyy-MM-dd HH:mm:ss.fff",
            "dd.MM.yyyy HH:mm:ss.fff",
            "MM/dd/yyyy HH:mm:ss.fff",

            // --- ISO и с часовым поясом ---
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-ddTHH:mm:ssZ",
            "yyyy-MM-ddTHH:mm:ss.fffZ",
            "yyyy-MM-ddTHH:mm:sszzz",
            "yyyy-MM-ddTHH:mm:ss.fffzzz",
            "o", // ISO 8601 Round-trip
            "s", // Sortable

            // --- Только время ---
            "HH:mm",
            "HH:mm:ss",
            "HH:mm:ss.fff"
        };

        private static readonly ConcurrentDictionary<string, object> SetterCache =
            new ConcurrentDictionary<string, object>();

        /// <summary>
        ///     Универсальный конвертер строки в DateTime?, не зависящий от региональных настроек.
        ///     Пытается распарсить дату из строки, используя набор фиксированных форматов. Если не получается, то пытается угадать
        ///     формат.
        /// </summary>
        private static readonly Converter<string, DateTime?> StringToDateTimeConverter = s =>
        {
            if (string.IsNullOrWhiteSpace(s))
                return null;

            const DateTimeStyles styles = DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal;

            if (DateTime.TryParseExact(s.Trim(), DateFormats, CultureInfo.InvariantCulture, styles, out var result))
                return result;

            // Пробуем угадать формат:

            if (DateTime.TryParse(s, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out var d))
                return d;

            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out d))
                return d;

            var dateTimeParts = s.Split(new[] { ' ', 'T' }, StringSplitOptions.RemoveEmptyEntries);
            var dateParts = dateTimeParts[0].Split(new[] { '.', '\\', '/', '-' }, StringSplitOptions.RemoveEmptyEntries);
            var yearIndex = dateParts.IndexOf((x, _) => x.Length == 4);
            var dayForSureIndex = dateParts.IndexOf((x, _) =>
                x.Length <= 2 && (int)Convert.ChangeType(x, typeof(int)) > 12 &&
                (int)Convert.ChangeType(x, typeof(int)) <= 31);
            var dayPossibleIndex = dateParts.IndexOf((x, i) =>
                x.Length <= 2 && (int)Convert.ChangeType(x, typeof(int)) > 0 &&
                (int)Convert.ChangeType(x, typeof(int)) <= 31 && i != dayForSureIndex);
            var dayIndex = dayForSureIndex >= 0 ? dayForSureIndex : dayPossibleIndex;
            var monthIndex = dateParts.IndexOf((x, i) =>
                x.Length <= 2 && (int)Convert.ChangeType(x, typeof(int)) > 0 &&
                (int)Convert.ChangeType(x, typeof(int)) <= 12 && i != dayIndex);

            var year = yearIndex >= 0 && yearIndex < dateParts.Length
                ? Convert.ChangeType(dateParts[yearIndex], typeof(int))
                : null;
            var month = monthIndex >= 0 && monthIndex < dateParts.Length
                ? Convert.ChangeType(dateParts[monthIndex], typeof(int))
                : null;
            var day = dayIndex >= 0 && dayIndex < dateParts.Length
                ? Convert.ChangeType(dateParts[dayIndex], typeof(int))
                : null;

            if (year != null && month != null && day != null)
                return new DateTime((int)year, (int)month, (int)day);

            if (dateTimeParts[0].Length == 8)
                return new DateTime((int)Convert.ChangeType(s.Substring(0, 4), typeof(int)),
                    (int)Convert.ChangeType(s.Substring(4, 2), typeof(int)),
                    (int)Convert.ChangeType(s.Substring(6, 2), typeof(int)));

            return null;
        };

        // Потокобезопасный кэш найденных типов
        private static readonly ConcurrentDictionary<string, Type> TypeCache =
            new ConcurrentDictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        static TypeHelper()
        {
            NullValues = new object[] { null, DBNull.Value, double.NaN, float.NaN };

            IntNumberTypes = new[]
            {
                typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(short), typeof(ushort), typeof(byte),
                typeof(sbyte),
                typeof(int?), typeof(uint?), typeof(long?), typeof(ulong?), typeof(short?), typeof(ushort?), typeof(byte?),
                typeof(sbyte?)
            };

            FloatNumberTypes = new[]
            {
                typeof(float), typeof(double), typeof(decimal),
                typeof(float?), typeof(double?), typeof(decimal?)
            };

            BoolTypes = new[]
            {
                typeof(bool),
                typeof(bool?),
                typeof(SqlBoolean),
                typeof(SqlBoolean?)
            };

            DateTypes = new[]
            {
                typeof(DateTime), typeof(DateTime?)
            };

            NumberTypes = IntNumberTypes.Concat(FloatNumberTypes).ToArray();

            BasicTypes =
                NumberTypes
                    .Concat(BoolTypes)
                    .Concat(new[]
                    {
                        typeof(string), typeof(DateTime), typeof(DateTime?), typeof(TimeSpan), typeof(Guid), typeof(Guid?),
                        typeof(char), typeof(char?), typeof(Enum)
                    })
                    .ToArray();
        }

        public static ConcurrentDictionary<string, object> Cache { get; } = new ConcurrentDictionary<string, object>();

        /// <summary>
        ///     Набор основных типов: числа, логические, строки, даты, Guid, Enum и др.
        /// </summary>
        public static Type[] BasicTypes { get; }

        /// <summary>
        ///     Типы, представляющие логические значения.
        /// </summary>
        public static Type[] BoolTypes { get; }

        /// <summary>
        ///     Типы, представляющие дату и время.
        /// </summary>
        public static Type[] DateTypes { get; }

        /// <summary>
        ///     Типы с плавающей запятой (float, double, decimal).
        /// </summary>
        public static Type[] FloatNumberTypes { get; }

        /// <summary>
        ///     Целочисленные типы (byte, int, long и т.д. с nullable и без).
        /// </summary>
        public static Type[] IntNumberTypes { get; }

        /// <summary>
        ///     Значения, трактуемые как null (null, DBNull, NaN).
        /// </summary>
        public static object[] NullValues { get; }

        /// <summary>
        ///     Все числовые типы: целочисленные и с плавающей точкой.
        /// </summary>
        public static Type[] NumberTypes { get; }

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
            if (value == null || (value.Equals(DBNull.Value) && IsNullable(toType)))
                return null;

            toType = Nullable.GetUnderlyingType(toType) ?? toType;
            var fromType = value.GetType();

            if (fromType == toType || toType.IsAssignableFrom(fromType))
                return value;

            if (formatProvider == null)
                formatProvider = CultureInfo.InvariantCulture;

            if (toType == typeof(string))
                return string.Format(formatProvider, "{0}", value);

            var isValueNumeric = IsNumeric(fromType);

            // Обработка преобразования в перечисление
            if (toType.IsEnum)
            {
                if (isValueNumeric)
                    return Enum.ToObject(toType,
                        ChangeType(value, typeof(int), formatProvider) ??
                        throw new NullReferenceException("ChangeType: Enum.ToObject"));
                if (fromType == typeof(bool))
                    return Enum.ToObject(toType,
                        ChangeType(Convert.ChangeType(value, typeof(int)), typeof(int), formatProvider) ??
                        throw new NullReferenceException("ChangeType: Enum.ToObject"));
                if (fromType == typeof(string))
                    return Enum.Parse(toType, $"{value}");
            }

            // Обработка строковых значений
            if (value is string s)
            {
                if (string.IsNullOrWhiteSpace(s) && IsNullable(toType))
                    return Default(toType);
                if (toType.IsEnum)
                    return Enum.Parse(toType, s, true);
                if (toType == typeof(DateTime))
                    return StringToDateTimeConverter(s);
                if (IsNumeric(toType))
                {
                    s = s.Replace(",", ".");
                    return Convert.ChangeType(s, toType, CultureInfo.InvariantCulture);
                }
            }

            if (fromType == typeof(bool) && toType == typeof(SqlBoolean))
                return new SqlBoolean((bool)value);

            // Стандартное преобразование
            return Convert.ChangeType(value, toType, CultureInfo.InvariantCulture);
        }

        /// <summary>
        ///     Преобразует значение к указанному типу.
        /// </summary>
        /// <typeparam name="T">Тип, в который нужно преобразовать.</typeparam>
        /// <param name="value">Значение для преобразования.</param>
        /// <param name="formatProvider">Провайдер формата (по умолчанию <see cref="CultureInfo.InvariantCulture" />).</param>
        /// <returns>Преобразованное значение.</returns>
        public static T ChangeType<T>(object value, IFormatProvider formatProvider = null)
        {
            return (T)ChangeType(value, typeof(T), formatProvider);
        }

        /// <summary>
        ///     Возвращает значение по умолчанию для указанного типа.
        /// </summary>
        /// <param name="type">Тип, для которого нужно получить значение по умолчанию.</param>
        /// <returns>Значение по умолчанию для указанного типа.</returns>
        public static object Default(Type type)
        {
            return type?.IsValueType == true ? Activator.CreateInstance(type) : null;
        }

        /// <summary>
        ///     Получает цепочку базовых типов и/или интерфейсов.
        /// </summary>
        /// <param name="type">Тип, для которого нужно получить базовые типы.</param>
        /// <param name="includeThis">Включать ли текущий тип в результат.</param>
        /// <param name="getInterfaces">Включать ли интерфейсы в результат.</param>
        /// <returns>Массив базовых типов и/или интерфейсов.</returns>
        public static Type[] GetBaseTypes(Type type, bool includeThis = false, bool getInterfaces = false)
        {
            var baseTypes = new List<Type>();
            var baseType = type;
            while (baseType.BaseType != null && baseType.BaseType != typeof(object))
            {
                baseType = baseType.BaseType;
                baseTypes.Add(baseType);
            }

            if (includeThis)
                baseTypes.Add(type);
            if (getInterfaces)
                baseTypes.AddRange(type.GetInterfaces());
            return baseTypes.ToArray();
        }

        /// <summary>
        ///     Возвращает тип элементов коллекции.
        /// </summary>
        /// <param name="type">Тип коллекции.</param>
        /// <returns>Тип элементов коллекции или null, если тип не является коллекцией.</returns>
        public static Type GetCollectionItemType(Type type)
        {
            if (type == null)
                return null;
            var isDic = typeof(IDictionary).IsAssignableFrom(type);
            var ga = type.GetGenericArguments();
            return type.IsArray
                ? type.GetElementType()
                : isDic && ga.Length > 1
                    ? ga[1]
                    : ga.FirstOrDefault();
        }

        /// <summary>
        ///     Ищет и возвращает первый пользовательский атрибут по имени типа на указанном <see cref="MemberInfo" />.
        ///     Метод сравнивает имя типа атрибута с заданным значением <paramref name="attributeName" /> с использованием
        ///     указанного <paramref name="stringComparison" />.
        ///     Удобен для случаев, когда тип атрибута известен только по имени (например, при работе с внешними библиотеками или
        ///     динамическими сценариями).
        /// </summary>
        /// <param name="member">Член, на котором производится поиск атрибута.</param>
        /// <param name="attributeName">Имя типа атрибута для поиска (например, "KeyAttribute").</param>
        /// <param name="stringComparison">
        ///     Способ сравнения строк для имени атрибута. По умолчанию
        ///     <see cref="StringComparison.OrdinalIgnoreCase" />.
        /// </param>
        /// <returns>Первый найденный экземпляр <see cref="Attribute" />, либо <c>null</c>, если атрибут не найден.</returns>
        public static Attribute GetCustomAttribute(MemberInfo member, string attributeName,
            StringComparison stringComparison = StringComparison.OrdinalIgnoreCase)
        {
            var trimAttributeName = !attributeName.ToLower().EndsWith("attribute");
            var memberAttributes = member.GetCustomAttributes();
            foreach (var a in memberAttributes)
            {
                var aName = a.GetType().Name;
                if (trimAttributeName)
                    aName = aName.Substring(0, aName.Length - 9);
                if (attributeName.Equals(aName, stringComparison))
                    return a;
            }

            return null;
        }

        /// <summary>
        ///     Возвращает поле по условию фильтрации.
        /// </summary>
        /// <param name="type">Тип, в котором нужно найти поле.</param>
        /// <param name="matchCriteria">Условие фильтрации полей.</param>
        /// <returns>Найденное поле или null, если поле не найдено.</returns>
        public static FieldInfo GetField(Type type, Func<FieldInfo, bool> matchCriteria)
        {
            return type.GetTypeInfo().DeclaredFields.FirstOrDefault(matchCriteria);
        }

        /// <summary>
        ///     Возвращает все типы из указанной сборки (или из сборки вызывающего кода),
        ///     которые реализуют интерфейс или наследуются от указанного базового типа.
        /// </summary>
        /// <param name="baseType">Базовый тип или интерфейс для поиска реализаций.</param>
        /// <param name="fromAssembly">
        ///     Сборка для поиска типов. Если не указана, используется сборка вызывающего кода.
        /// </param>
        /// <returns>Массив типов, удовлетворяющих условию.</returns>
        public static Type[] GetImplementationsOf(Type baseType, Assembly fromAssembly)
        {
            var assembly = fromAssembly ?? Assembly.GetCallingAssembly();
            return assembly
                .GetTypes()
                .Where(x => IsImplements(x, baseType) && x != baseType)
                .ToArray();
        }

        /// <summary>
        ///     Возвращает все типы из всех загруженных в домен приложений сборок,
        ///     которые реализуют интерфейс или наследуются от указанного базового типа.
        /// </summary>
        /// <param name="baseType">Базовый тип или интерфейс для поиска реализаций.</param>
        /// <returns>Массив типов, удовлетворяющих условию.</returns>
        public static Type[] GetImplementationsOf(Type baseType)
        {
            return AppDomain.CurrentDomain
                .GetAssemblies()
                .SelectMany(a =>
                {
                    try
                    {
                        return a.GetTypes();
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        // Если часть типов не загружается, используем только доступные
                        return ex.Types.Where(t => t != null);
                    }
                })
                .Where(x => IsImplements(x, baseType) && x != baseType)
                .ToArray();
        }

        /// <summary>
        ///     Получает событие с наименьшего уровня иерархии.
        /// </summary>
        /// <param name="type">Тип, с которого начинается поиск.</param>
        /// <param name="name">Имя события.</param>
        /// <returns>Найденное событие или null, если событие не найдено.</returns>
        public static EventInfo GetLowestEvent(Type type, string name)
        {
            while (type != null)
            {
                var member = type.GetEvent(name, BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance);
                if (member != null)
                    return member;
                type = type.BaseType;
            }

            return null;
        }

        /// <summary>
        ///     Получает поле с наименьшего уровня иерархии.
        /// </summary>
        /// <param name="type">Тип, с которого начинается поиск.</param>
        /// <param name="name">Имя поля.</param>
        /// <returns>Найденное поле или null, если поле не найдено.</returns>
        public static FieldInfo GetLowestField(Type type, string name)
        {
            while (type != null)
            {
                var member = type.GetField(name, BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance);
                if (member != null)
                    return member;
                type = type.BaseType;
            }

            return null;
        }

        /// <summary>
        ///     Получает метод с наименьшего уровня иерархии.
        /// </summary>
        /// <param name="type">Тип, с которого начинается поиск.</param>
        /// <param name="name">Имя метода.</param>
        /// <returns>Найденный метод или null, если метод не найден.</returns>
        public static MethodInfo GetLowestMethod(Type type, string name)
        {
            while (type != null)
            {
                var member = type.GetMethod(name, BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance);
                if (member != null)
                    return member;
                type = type.BaseType;
            }

            return null;
        }

        /// <summary>
        ///     Получает свойство с наименьшего уровня иерархии.
        /// </summary>
        /// <param name="type">Тип, с которого начинается поиск.</param>
        /// <param name="name">Имя свойства.</param>
        /// <returns>Найденное свойство или null, если свойство не найдено.</returns>
        public static PropertyInfo GetLowestProperty(Type type, string name)
        {
            while (type != null)
            {
                var member = type.GetProperty(name,
                    BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance);
                if (member != null)
                    return member;
                type = type.BaseType;
            }

            return null;
        }

        /// <summary>
        ///     Получить свойство указанное в выражении
        /// </summary>
        /// <param name="expr"></param>
        /// <returns></returns>
        public static MemberInfo GetMemberInfo(Expression expr)
        {
            if (expr == null)
                return null;
            switch (expr)
            {
                case LambdaExpression le: return GetMemberInfoFromLambda(le);
                case BinaryExpression be: return GetMemberInfo(be.Left);
                case MemberExpression me: return me.Member;
                case UnaryExpression ue: return GetMemberInfo(ue.Operand);
                case MethodCallExpression mc: return GetMemberInfoFromMethodCall(mc);
                case ConditionalExpression ce: return GetMemberInfo(ce.IfTrue) ?? GetMemberInfo(ce.IfFalse);
                default: return null;
            }
        }

        /// <summary>
        ///     Получает все публичные свойства типа <typeparamref name="T" />.
        /// </summary>
        /// <typeparam name="T">Тип, для которого нужно получить свойства.</typeparam>
        /// <returns>Массив <see cref="PropertyInfo" /> всех публичных свойств.</returns>
        public static PropertyInfo[] GetProperties<T>() where T : class
        {
            return GetProperties(typeof(T));
        }

        /// <summary>
        ///     Получает все публичные свойства указанного типа.
        ///     Использует внутренний кеш для ускорения повторных вызовов.
        /// </summary>
        /// <param name="type">Тип, для которого нужно получить свойства.</param>
        /// <returns>Массив <see cref="PropertyInfo" /> всех публичных свойств.</returns>
        public static PropertyInfo[] GetProperties(Type type)
        {
            var key = $"{type.FullName}.properties.{type.FullName}:object";

            if (Cache.TryGetValue(key, out var cached))
                return ((Dictionary<string, PropertyInfo>)cached).Values.ToArray();

            var typeProperties = type.GetProperties();
            var dic = new Dictionary<string, PropertyInfo>();
            foreach (var prop in typeProperties)
                dic[prop.Name] = prop;
            Cache[key] = dic;
            return typeProperties;
        }

        /// <summary>
        ///     Получить свойство по его имени
        /// </summary>
        /// <param name="type">Тип в котором искать свойство</param>
        /// <param name="propertyName">Имя свойства</param>
        /// <param name="stringComparison">Сравнение имен</param>
        /// <returns></returns>
        public static PropertyInfo GetProperty(Type type, string propertyName,
            StringComparison stringComparison = StringComparison.OrdinalIgnoreCase)
        {
            return GetProperties(type).FirstOrDefault(x => x.Name.Equals(propertyName, stringComparison));
        }

        /// <summary>
        ///     Получить свойство указанное в выражении
        /// </summary>
        /// <param name="expr"></param>
        /// <returns></returns>
        public static PropertyInfo GetProperty(Expression expr)
        {
            return GetMemberInfo(expr) as PropertyInfo;
        }

        /// <summary>
        ///     Получает имена всех публичных свойств типа <typeparamref name="T" />.
        /// </summary>
        /// <typeparam name="T">Тип, для которого нужно получить имена свойств.</typeparam>
        /// <returns>Массив имен свойств.</returns>
        public static string[] GetPropertyNames<T>()
        {
            return GetPropertyNames(typeof(T));
        }

        /// <summary>
        ///     Получает имена всех публичных свойств указанного типа.
        ///     Использует внутренний кеш для ускорения повторных вызовов.
        /// </summary>
        /// <param name="type">Тип, для которого нужно получить имена свойств.</param>
        /// <returns>Массив имен свойств.</returns>
        public static string[] GetPropertyNames(Type type)
        {
            var key = $"{type.FullName}.propertynames.{type.FullName}:object";

            if (Cache.TryGetValue(key, out var cached))
                return (string[])cached;

            var typePropertyNames = GetProperties(type).Select(x => x.Name).ToArray();
            Cache[key] = typePropertyNames;
            return typePropertyNames;
        }

        /// <summary>
        ///     Получает значение указанного свойства объекта по имени с помощью кэшируемого делегата.
        ///     Позволяет быстро извлекать значения свойств без постоянного использования Reflection.
        ///     Особенности:
        ///     - Автоматически создает и кэширует делегат-геттер для типа и имени свойства.
        ///     - Поддерживает как ссылочные, так и значимые типы свойств (boxing выполняется автоматически).
        ///     - При повторных вызовах для того же типа и свойства используется уже скомпилированный делегат.
        ///     Пример:
        ///     <code>
        /// var person = new Person { Name = "Alice" };
        /// var value = PropertyHelper.GetPropertyValue(person, "Name"); // "Alice"
        /// </code>
        /// </summary>
        public static object GetPropertyValue(object source, string propertyName)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var type = source.GetType();
            var key = $"{type.FullName}.get.{propertyName}:object";

            if (Cache.TryGetValue(key, out var cached))
                return ((Func<object, object>)cached)(source);

            var param = Expression.Parameter(typeof(object), "x");
            var convertedParam = Expression.Convert(param, type);
            var prop = Expression.PropertyOrField(convertedParam, propertyName);
            var convertedProp = Expression.Convert(prop, typeof(object));

            var lambda = Expression.Lambda<Func<object, object>>(convertedProp, param).Compile();

            Cache[key] = lambda;
            return lambda(source);
        }

        /// <summary>
        ///     Возвращает значение свойства с типизацией результата.
        ///     Создает и кэширует делегат для быстрого доступа к свойству по имени.
        ///     Особенности:
        ///     - Позволяет получить значение свойства с приведением к нужному типу.
        ///     - Использует кэширование делегатов для повышения производительности.
        ///     - Генерирует исключение, если свойство не найдено или несовместимо по типу.
        ///     Пример:
        ///     <code>
        /// var person = new Person { Age = 42 };
        /// int age = PropertyHelper.GetPropertyValue&lt;Person, int&gt;(person, "Age"); // 42
        /// </code>
        /// </summary>
        public static TReturn GetPropertyValue<T, TReturn>(T source, string propertyName) where T : class
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var getter = Getter<T, TReturn>(propertyName);
            return getter(source);
        }

        /// <summary>
        ///     Установить значение свойству объекта по имени.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">Объект</param>
        /// <param name="propertyName">Имя свойства</param>
        /// <param name="value">Значение свойства</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool SetPropertyValue<T>(T source, string propertyName, object value) where T : class
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var setter = Setter<T>(propertyName);
            if (setter == null)
                return false;
            setter(source, value);
            return true;
        }

        /// <summary>
        ///     Получает значения свойств объекта в указанном порядке
        /// </summary>
        /// <typeparam name="TObject"></typeparam>
        /// <param name="source">Исходный объект</param>
        /// <param name="propertyNames">Имена свойств объекта с учетом регистра</param>
        /// <returns></returns>
        public static object[] GetPropertyValues<TObject>(TObject source, params string[] propertyNames)
            where TObject : class
        {
            var values = new List<object>();
            foreach (var property in propertyNames)
            {
                var getter = Getter<TObject>(property);
                if (getter == null)
                    continue;
                values.Add(getter(source));
            }

            return values.ToArray();
        }

        /// <summary>
        ///     Получает значения свойств объекта в указанном порядке и преобразует в указанный тип через
        ///     <see cref="TypeHelper.ChangeType{T}(object, IFormatProvider)" />
        /// </summary>
        /// <typeparam name="TObject"></typeparam>
        /// <param name="source">Исходный объект</param>
        /// <param name="propertyNames">Имена свойств объекта с учетом регистра</param>
        /// <returns></returns>
        public static TValue[] GetPropertyValues<TObject, TValue>(TObject source, params string[] propertyNames)
            where TObject : class
        {
            return GetPropertyValues(source, propertyNames).Select(x => ChangeType<TValue>(x)).ToArray();
        }

        /// <summary>
        ///     Создает и кэширует делегат-геттер для указанного свойства типа <typeparamref name="T" />.
        ///     Делегат возвращает значение свойства в виде <see cref="object" />
        ///     (значимые типы будут упакованы).
        /// </summary>
        /// <typeparam name="T">Тип объекта, содержащего свойство.</typeparam>
        /// <param name="propertyName">Имя свойства для доступа.</param>
        /// <returns>
        ///     Функция <see cref="Func{T, Object}" />, возвращающая значение указанного свойства.
        /// </returns>
        /// <exception cref="ArgumentException">
        ///     Выбрасывается, если свойство с указанным именем отсутствует.
        /// </exception>
        /// <example>
        ///     <code>
        /// class Person
        /// {
        ///     public int Id { get; set; }
        ///     public string Name { get; set; }
        /// }
        /// 
        /// var getId = PropertyHelper.Getter&lt;Person&gt;("Id");
        /// var getName = PropertyHelper.Getter&lt;Person&gt;("Name");
        /// 
        /// var p = new Person { Id = 42, Name = "Alice" };
        /// Console.WriteLine(getId(p));   // 42 (упаковано в object)
        /// Console.WriteLine(getName(p)); // Alice
        /// </code>
        /// </example>
        public static Func<T, object> Getter<T>(string propertyName)
        {
            var key = $"{typeof(T).FullName}.get.{propertyName}";
            if (Cache.TryGetValue(key, out var cached))
                return (Func<T, object>)cached;

            var type = typeof(T);
            var flags1 = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            var flags2 = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                         BindingFlags.Static;

            // --- Ищем Property ---
            PropertyInfo prop = null;
            try
            {
                prop = type.GetProperty(propertyName, flags1);
            }
            catch
            {
                try
                {
                    prop = type.GetProperty(propertyName, flags2);
                }
                catch
                {
                    // ignored
                }
            }

            if (prop != null)
            {
                var getMethod = prop.GetGetMethod(true) ??
                                throw new InvalidOperationException($"Свойство '{propertyName}' не имеет геттера.");
                Func<T, object> lambda;

                if (getMethod.IsStatic)
                {
                    var call = Expression.Call(getMethod);
                    var converted = Expression.Convert(call, typeof(object));
                    lambda = Expression.Lambda<Func<T, object>>(converted, Expression.Parameter(typeof(T), "x")).Compile();
                }
                else
                {
                    var param = Expression.Parameter(typeof(T), "x");
                    var call = Expression.Call(param, getMethod);
                    var converted = Expression.Convert(call, typeof(object));
                    lambda = Expression.Lambda<Func<T, object>>(converted, param).Compile();
                }

                Cache[key] = lambda;
                return lambda;
            }

            // --- Ищем Field ---
            var field = type.GetField(propertyName, flags1);
            if (field != null)
            {
                Func<T, object> lambda;

                if (field.IsStatic)
                {
                    var fieldExp = Expression.Field(null, field);
                    var converted = Expression.Convert(fieldExp, typeof(object));
                    lambda = Expression.Lambda<Func<T, object>>(converted, Expression.Parameter(typeof(T), "x")).Compile();
                }
                else
                {
                    var param = Expression.Parameter(typeof(T), "x");
                    var fieldExp = Expression.Field(param, field);
                    var converted = Expression.Convert(fieldExp, typeof(object));
                    lambda = Expression.Lambda<Func<T, object>>(converted, param).Compile();
                }

                Cache[key] = lambda;
                return lambda;
            }

            return null;
            throw new ArgumentException($"Свойство или поле '{propertyName}' не найдено в типе {type.FullName}.");
        }

        /// <summary>
        ///     Создает и кэширует делегат-геттер для указанного свойства типа <typeparamref name="T" />.
        ///     Делегат возвращает значение свойства без упаковки (если <typeparamref name="TReturn" /> совпадает с типом
        ///     свойства).
        /// </summary>
        /// <typeparam name="T">Тип объекта, содержащего свойство.</typeparam>
        /// <typeparam name="TReturn">Тип возвращаемого значения свойства.</typeparam>
        /// <param name="propertyName">Имя свойства для доступа.</param>
        /// <returns>Функция <see cref="Func{T, TReturn}" />, возвращающая значение указанного свойства.</returns>
        /// <exception cref="InvalidOperationException">
        ///     Выбрасывается, если свойство не может быть приведено к типу <typeparamref name="TReturn" />.
        /// </exception>
        /// <example>
        ///     <code>
        /// class Person
        /// {
        ///     public int Id { get; set; }
        ///     public string Name { get; set; }
        /// }
        /// 
        /// var getId = PropertyHelper.Getter&lt;Person, int&gt;("Id");
        /// var getName = PropertyHelper.Getter&lt;Person, string&gt;("Name");
        /// 
        /// var p = new Person { Id = 42, Name = "Alice" };
        /// Console.WriteLine(getId(p));   // 42
        /// Console.WriteLine(getName(p)); // Alice
        /// </code>
        /// </example>
        public static Func<T, TReturn> Getter<T, TReturn>(string propertyName)
        {
            var key = $"{typeof(T).FullName}.get.{propertyName}:{typeof(TReturn).FullName}";
            if (Cache.TryGetValue(key, out var cached))
                return (Func<T, TReturn>)cached;

            var param = Expression.Parameter(typeof(T), "x");
            var prop = Expression.PropertyOrField(param, propertyName);

            // проверим совместимость типов
            if (!typeof(TReturn).IsAssignableFrom(prop.Type))
                throw new InvalidOperationException(
                    $"Свойство '{propertyName}' типа {prop.Type.FullName} " +
                    $"нельзя преобразовать к {typeof(TReturn).FullName}");

            var lambda = Expression.Lambda<Func<T, TReturn>>(prop, param).Compile();

            Cache[key] = lambda;
            return lambda;
        }

        /// <summary>
        ///     Создает и кэширует делегат для получения значения свойства на основе лямбда-выражения.
        ///     Позволяет быстро и безопасно извлекать значение свойства по имени без постоянного использования Reflection.
        ///     Особенности:
        ///     - Принимает лямбда-выражение, указывающее на нужное свойство, и возвращает делегат-геттер.
        ///     - Автоматически извлекает имя свойства из выражения и возвращает его через out-параметр.
        ///     - Использует кэширование делегатов для повышения производительности при повторных вызовах.
        ///     - Генерирует исключение, если выражение не указывает на свойство.
        ///     Пример:
        ///     <code>
        /// var getter = PropertyHelper.Getter&lt;Person, string&gt;(x =&gt; x.Name, out var propName);
        /// var value = getter(person); // "Alice"
        /// </code>
        /// </summary>
        public static Func<TSource, object> Getter<TSource, TSourceProp>(
            this Expression<Func<TSource, TSourceProp>> propSelector, out string propertyName)
        {
            if (!(propSelector.Body is MemberExpression member))
                throw new ArgumentException("Выражение должно указывать на свойство.", nameof(propSelector));

            propertyName = member.Member.Name;
            return Getter<TSource>(propertyName);
        }

        /// <summary>
        ///     Проверяет, является ли тип простым (базовым).
        /// </summary>
        /// <param name="t">Тип для проверки.</param>
        /// <returns>True, если тип является базовым, иначе False.</returns>
        public static bool IsBasic(Type t)
        {
            return t != null && (t.IsEnum || BasicTypes.Contains(t));
        }

        /// <summary>
        ///     Проверяет, является ли тип логическим.
        /// </summary>
        /// <param name="t">Тип для проверки.</param>
        /// <returns>True, если тип является логическим, иначе False.</returns>
        public static bool IsBoolean(Type t)
        {
            return BoolTypes.Contains(t);
        }

        /// <summary>
        ///     Проверяет, является ли тип коллекцией.
        /// </summary>
        /// <param name="t">Тип для проверки.</param>
        /// <returns>True, если тип является коллекцией, иначе False.</returns>
        public static bool IsCollection(Type t)
        {
            if (t.IsArray)
                return true;
            if (t == typeof(string))
                return false;
            var hasGenericType = t.GenericTypeArguments.Length > 0;
            return (typeof(IList).IsAssignableFrom(t) || typeof(ICollection).IsAssignableFrom(t) ||
                    typeof(IEnumerable).IsAssignableFrom(t)) && hasGenericType;
        }

        /// <summary>
        ///     Проверяет, является ли тип датой/временем.
        /// </summary>
        /// <param name="t">Тип для проверки.</param>
        /// <returns>True, если тип представляет дату/время, иначе False.</returns>
        public static bool IsDate(Type t)
        {
            return DateTypes.Contains(t);
        }

        /// <summary>
        ///     Проверяет, является ли тип делегатом.
        /// </summary>
        /// <param name="type">Тип для проверки.</param>
        /// <returns>True, если тип является делегатом, иначе False.</returns>
        public static bool IsDelegate(Type type)
        {
            return typeof(MulticastDelegate).IsAssignableFrom(type.BaseType);
        }

        /// <summary>
        ///     Проверяет, является ли тип словарём.
        /// </summary>
        /// <param name="type">Тип для проверки.</param>
        /// <returns>True, если тип является словарём, иначе False.</returns>
        public static bool IsDictionary(Type type)
        {
            return IsImplements<IDictionary>(type) ||
                   (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>)) || type.GetInterfaces()
                       .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));
        }

        /// <summary>
        ///     Проверяет, является ли тип числом с плавающей точкой.
        /// </summary>
        /// <param name="t">Тип для проверки.</param>
        /// <returns>True, если тип является числом с плавающей точкой, иначе False.</returns>
        public static bool IsFloat(Type t)
        {
            return FloatNumberTypes.Contains(t);
        }

        /// <summary>
        ///     Проверяет, реализует ли тип заданный интерфейс.
        /// </summary>
        /// <param name="t">Тип для проверки.</param>
        /// <param name="implementType">Интерфейс, который нужно проверить.</param>
        /// <returns>True, если тип реализует указанный интерфейс, иначе False.</returns>
        public static bool IsImplements(Type t, Type implementType)
        {
            return implementType.IsAssignableFrom(t);
        }

        /// <summary>
        ///     Проверяет, реализует ли тип заданный интерфейс (generic).
        /// </summary>
        /// <typeparam name="T">Интерфейс, который нужно проверить.</typeparam>
        /// <param name="t"> Тип для проверки.</param>
        /// <returns>True, если тип реализует указанный интерфейс, иначе False.</returns>
        public static bool IsImplements<T>(Type t)
        {
            return typeof(T).IsAssignableFrom(t);
        }

        /// <summary>
        ///     Проверяет, является ли тип nullable.
        /// </summary>
        /// <param name="t">Тип для проверки.</param>
        /// <returns>True, если тип является nullable, иначе False.</returns>
        public static bool IsNullable(Type t)
        {
            return !t.IsValueType || Nullable.GetUnderlyingType(t) != null || t == typeof(object);
        }

        /// <summary>
        ///     Проверяет, является ли тип числовым.
        /// </summary>
        /// <param name="t">Тип для проверки.</param>
        /// <param name="includeFloatTypes">Включать ли типы с плавающей точкой.</param>
        /// <returns>True, если тип является числовым, иначе False.</returns>
        public static bool IsNumeric(Type t, bool includeFloatTypes = true)
        {
            return includeFloatTypes ? NumberTypes.Contains(t) : IntNumberTypes.Contains(t);
        }

        /// <summary>
        ///     Проверяет, является ли тип кортежем (ValueTuple/Tuple).
        /// </summary>
        /// <param name="type">Тип для проверки.</param>
        /// <returns>True, если тип является кортежем, иначе False.</returns>
        public static bool IsTuple(Type type)
        {
            var baseTypes = GetBaseTypes(type, true, true);
            return baseTypes.Any(x =>
                x.FullName?.StartsWith("System.ValueTuple") == true || x.FullName?.StartsWith("System.Tuple") == true ||
                x.Name.Equals("ITuple"));
        }

        /// <summary>
        ///     Создает и кэширует делегат для установки значения свойства по имени.
        ///     Позволяет быстро и безопасно изменять значение свойства без постоянного использования Reflection.
        ///     Особенности:
        ///     - Автоматически создает и кэширует делегат-сеттер для типа и имени свойства.
        ///     - Поддерживает как ссылочные, так и значимые типы свойств (boxing выполняется автоматически).
        ///     - При повторных вызовах для того же типа и свойства используется уже скомпилированный делегат.
        ///     - Генерирует исключение, если свойство не найдено или несовместимо по типу.
        ///     Пример:
        ///     <code>
        /// var setter = PropertyHelper.Setter&lt;Person&gt;("Name");
        /// setter(person, "Bob");
        /// </code>
        /// </summary>
        public static Action<T, object> Setter<T>(string propertyName)
        {
            var key = $"{typeof(T).FullName}.set.{propertyName}";
            if (SetterCache.TryGetValue(key, out var cached))
                return (Action<T, object>)cached;

            var type = typeof(T);
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

            // --- Ищем Property ---
            var prop = type.GetProperty(propertyName, flags);
            if (prop != null)
            {
                var setMethod = prop.GetSetMethod(true) ??
                                throw new InvalidOperationException($"Свойство '{propertyName}' не имеет сеттера.");
                Action<T, object> lambda;

                if (setMethod.IsStatic)
                {
                    var valueExp = Expression.Parameter(typeof(object), "value");
                    var convertedValue = Expression.Convert(valueExp, prop.PropertyType);
                    var call = Expression.Call(setMethod, convertedValue);
                    lambda = Expression.Lambda<Action<T, object>>(call, Expression.Parameter(typeof(T), "x"), valueExp)
                        .Compile();
                }
                else
                {
                    var targetExp = Expression.Parameter(typeof(T), "x");
                    var valueExp = Expression.Parameter(typeof(object), "value");
                    var convertedValue = Expression.Convert(valueExp, prop.PropertyType);
                    var call = Expression.Call(targetExp, setMethod, convertedValue);
                    lambda = Expression.Lambda<Action<T, object>>(call, targetExp, valueExp).Compile();
                }

                SetterCache[key] = lambda;
                return lambda;
            }

            // --- Ищем Field ---
            var field = type.GetField(propertyName, flags);
            if (field != null)
            {
                Action<T, object> lambda;

                if (field.IsStatic)
                {
                    var valueExp = Expression.Parameter(typeof(object), "value");
                    var convertedValue = Expression.Convert(valueExp, field.FieldType);
                    var assign = Expression.Assign(Expression.Field(null, field), convertedValue);
                    lambda = Expression.Lambda<Action<T, object>>(assign, Expression.Parameter(typeof(T), "x"), valueExp)
                        .Compile();
                }
                else
                {
                    var targetExp = Expression.Parameter(typeof(T), "x");
                    var valueExp = Expression.Parameter(typeof(object), "value");
                    var convertedValue = Expression.Convert(valueExp, field.FieldType);
                    var assign = Expression.Assign(Expression.Field(targetExp, field), convertedValue);
                    lambda = Expression.Lambda<Action<T, object>>(assign, targetExp, valueExp).Compile();
                }

                SetterCache[key] = lambda;
                return lambda;
            }

            throw new ArgumentException($"Свойство или поле '{propertyName}' не найдено в типе {type.FullName}.");
        }

        /// <summary>
        ///     Создает и кэширует делегат для установки значения свойства на основе лямбда-выражения.
        ///     Позволяет быстро и безопасно изменять значение свойства по имени без постоянного использования Reflection.
        ///     Особенности:
        ///     - Принимает лямбда-выражение, указывающее на нужное свойство, и возвращает делегат-сеттер.
        ///     - Автоматически извлекает имя свойства из выражения и возвращает его через out-параметр.
        ///     - Использует кэширование делегатов для повышения производительности при повторных вызовах.
        ///     - Генерирует исключение, если выражение не указывает на свойство.
        ///     Пример:
        ///     <code>
        /// var setter = PropertyHelper.Setter&lt;Person, string&gt;(x =&gt; x.Name, out var propName);
        /// setter(person, "Bob");
        /// </code>
        /// </summary>
        public static Action<TSource, object> Setter<TSource, TSourceProp>(
            this Expression<Func<TSource, TSourceProp>> propSelector, out string propertyName)
        {
            if (!(propSelector.Body is MemberExpression member))
                throw new ArgumentException("Выражение должно указывать на свойство.", nameof(propSelector));

            propertyName = member.Member.Name;
            return Setter<TSource>(propertyName);
        }

        private static MemberInfo GetMemberInfoFromLambda(LambdaExpression le)
        {
            var propDeclaringType = le.Type.GenericTypeArguments.FirstOrDefault();
            var pi = GetMemberInfo(le.Body);
            pi = GetProperties(propDeclaringType)
                .FirstOrDefault(x => string.Compare(x.Name, pi?.Name, StringComparison.Ordinal) == 0) ?? pi;
            return pi;
        }

        private static MemberInfo GetMemberInfoFromMethodCall(MethodCallExpression mce)
        {
            var pi = GetMemberInfo(mce.Arguments[0]);
            return pi;
        }

        /// <summary>
        ///     Ищет тип или интерфейс по указанному имени во всех сборках, загруженных в текущий <see cref="AppDomain" />.
        ///     Результаты поиска кэшируются для ускорения последующих вызовов.
        /// </summary>
        /// <param name="typeOrInterfaceName">
        ///     Полное или короткое имя типа (например, <c>"System.String"</c> или <c>"String"</c>).
        /// </param>
        /// <returns>
        ///     Объект <see cref="Type" />, если тип найден; в противном случае <see langword="null" />.
        /// </returns>
        /// <remarks>
        ///     Поиск выполняется без учёта регистра, сравниваются <see cref="Type.FullName" /> и <see cref="Type.Name" />.
        ///     При первом вызове метод перебирает все загруженные сборки, затем кэширует результат.
        /// </remarks>
        /// <exception cref="ArgumentException">
        ///     Выбрасывается, если параметр <paramref name="typeOrInterfaceName" /> равен <see langword="null" /> или пуст.
        /// </exception>
        /// <example>
        ///     Пример использования:
        ///     <code language="csharp">
        /// var type1 = TypeHelper.GetTypeByName("System.String");
        /// Console.WriteLine(type1); // Вывод: System.String
        /// 
        /// var type2 = TypeHelper.GetTypeByName("String");
        /// Console.WriteLine(type2); // Вывод: System.String
        /// 
        /// var type3 = TypeHelper.GetTypeByName("IEnumerable");
        /// Console.WriteLine(type3); // Вывод: System.Collections.IEnumerable
        /// 
        /// // Повторный вызов — берётся из кэша, без обхода сборок
        /// var cached = TypeHelper.GetTypeByName("System.String");
        /// 
        /// Console.WriteLine(ReferenceEquals(type1, cached)); // True
        /// </code>
        /// </example>
        public static Type GetTypeByName(string typeOrInterfaceName)
        {
            if (string.IsNullOrWhiteSpace(typeOrInterfaceName))
                throw new ArgumentException("Type name cannot be null or empty.", nameof(typeOrInterfaceName));

            // Проверяем кэш
            if (TypeCache.TryGetValue(typeOrInterfaceName, out var cachedType))
                return cachedType;

            Type foundType = null;
            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in loadedAssemblies)
                try
                {
                    var type = assembly.GetTypes()
                        .FirstOrDefault(t =>
                            string.Equals(t.FullName, typeOrInterfaceName, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(t.Name, typeOrInterfaceName, StringComparison.OrdinalIgnoreCase));

                    if (type != null)
                    {
                        foundType = type;
                        break;
                    }
                }
                catch (ReflectionTypeLoadException ex)
                {
                    var type = ex.Types
                        .FirstOrDefault(t =>
                            t != null &&
                            (string.Equals(t.FullName, typeOrInterfaceName, StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(t.Name, typeOrInterfaceName, StringComparison.OrdinalIgnoreCase)));

                    if (type != null)
                    {
                        foundType = type;
                        break;
                    }
                }

            // Кэшируем результат (в том числе null, чтобы избежать повторных обходов)
            TypeCache[typeOrInterfaceName] = foundType;

            return foundType;
        }
    }
}