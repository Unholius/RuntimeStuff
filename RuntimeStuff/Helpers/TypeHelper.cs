using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;

namespace RuntimeStuff.Helpers
{
    /// <summary>
    ///     v.2025.12.18 (RS)<br/>
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

        private static readonly ConcurrentDictionary<Type, Dictionary<string, FieldInfo>> FieldsCache = new ConcurrentDictionary<Type, Dictionary<string, FieldInfo>>();

        private static readonly ConcurrentDictionary<CacheKey, Delegate> GettersCache = new ConcurrentDictionary<CacheKey, Delegate>();

        private static readonly ConcurrentDictionary<Type, Dictionary<string, PropertyInfo>> PropertiesCache = new ConcurrentDictionary<Type, Dictionary<string, PropertyInfo>>();

        private static readonly ConcurrentDictionary<CacheKey, Delegate> SettersCache = new ConcurrentDictionary<CacheKey, Delegate>();

        // Порог для переключения на SortedDictionary
        private static readonly OpCode[] SOneByte = new OpCode[256];

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
            var dateParts = dateTimeParts[0]
                .Split(new[] { '.', '\\', '/', '-' }, StringSplitOptions.RemoveEmptyEntries);
            var yearIndex = IndexOf(dateParts, (x, _) => x.Length == 4);
            var dayForSureIndex = IndexOf(dateParts, (x, _) =>
                x.Length <= 2 && (int)Convert.ChangeType(x, typeof(int)) > 12 &&
                (int)Convert.ChangeType(x, typeof(int)) <= 31);
            var dayPossibleIndex = IndexOf(dateParts, (x, i) =>
                x.Length <= 2 && (int)Convert.ChangeType(x, typeof(int)) > 0 &&
                (int)Convert.ChangeType(x, typeof(int)) <= 31 && i != dayForSureIndex);
            var dayIndex = dayForSureIndex >= 0 ? dayForSureIndex : dayPossibleIndex;
            var monthIndex = IndexOf(dateParts, (x, i) =>
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

        private static readonly OpCode[] STwoByte = new OpCode[256];
        private static readonly StringComparer OrdinalIgnoreCaseComparer = StringComparer.OrdinalIgnoreCase;
        private static readonly ConcurrentDictionary<string, Type> TypeCache = new ConcurrentDictionary<string, Type>(OrdinalIgnoreCaseComparer);

        static TypeHelper()
        {
            NullValues = new object[] { null, DBNull.Value, double.NaN, float.NaN };

            IntNumberTypes = new HashSet<Type>
            {
                typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(short), typeof(ushort), typeof(byte),
                typeof(sbyte),
                typeof(int?), typeof(uint?), typeof(long?), typeof(ulong?), typeof(short?), typeof(ushort?),
                typeof(byte?),
                typeof(sbyte?)
            };

            FloatNumberTypes = new HashSet<Type>
            {
                typeof(float), typeof(double), typeof(decimal),
                typeof(float?), typeof(double?), typeof(decimal?)
            };

            BoolTypes = new HashSet<Type>
            {
                typeof(bool),
                typeof(bool?)
            };

            DateTypes = new HashSet<Type>
            {
                typeof(DateTime), typeof(DateTime?)
            };

            NumberTypes = new HashSet<Type>(IntNumberTypes.Concat(FloatNumberTypes));

            BasicTypes =
                NumberTypes
                    .Concat(BoolTypes)
                    .Concat(new[]
                    {
                        typeof(string), typeof(DateTime), typeof(DateTime?), typeof(TimeSpan), typeof(Guid),
                        typeof(Guid?),
                        typeof(char), typeof(char?), typeof(Enum)
                    })
                    .ToArray();
        }

        /// <summary>
        ///     Набор основных типов: числа, логические, строки, даты, Guid, Enum и др.
        /// </summary>
        public static Type[] BasicTypes { get; }

        /// <summary>
        ///     Типы, представляющие логические значения.
        /// </summary>
        public static HashSet<Type> BoolTypes { get; }

        /// <summary>
        ///     Типы, представляющие дату и время.
        /// </summary>
        public static HashSet<Type> DateTypes { get; }

        /// <summary>
        ///     Флаги для поиска членов класса по умолчанию
        /// </summary>
        public static BindingFlags DefaultBindingFlags { get; } = BindingFlags.Instance | BindingFlags.NonPublic |
                                                                  BindingFlags.Public | BindingFlags.Static;

        /// <summary>
        ///     Типы с плавающей запятой (float, double, decimal).
        /// </summary>
        public static HashSet<Type> FloatNumberTypes { get; }

        /// <summary>
        ///     Целочисленные типы (byte, int, long и т.д. с nullable и без).
        /// </summary>
        public static HashSet<Type> IntNumberTypes { get; }

        /// <summary>
        ///     Значения, трактуемые как null (null, DBNull, NaN).
        /// </summary>
        public static object[] NullValues { get; }

        /// <summary>
        ///     Все числовые типы: целочисленные и с плавающей точкой.
        /// </summary>
        public static HashSet<Type> NumberTypes { get; }

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

            if (toType == typeof(object))
                return value;

            if (formatProvider == null)
                formatProvider = CultureInfo.InvariantCulture;
            toType = Nullable.GetUnderlyingType(toType) ?? toType;

            var fromType = value.GetType();

            // Быстрый возврат
            if (fromType == toType || toType.IsAssignableFrom(fromType))
                return value;

            // Преобразование в строку
            if (toType == typeof(string))
                return string.Format(formatProvider, "{0}", value);

            // ENUM
            if (toType.IsEnum)
            {
                if (value is string es)
                    return Enum.Parse(toType, es, true);

                if (value is bool b)
                    return Enum.ToObject(toType, b ? 1 : 0);

                if (IsNumeric(fromType))
                    return Enum.ToObject(toType, Convert.ToInt32(value, CultureInfo.InvariantCulture));
            }

            // Преобразование строк
            if (value is string s)
            {
                if (string.IsNullOrWhiteSpace(s) && IsNullable(toType))
                    return Default(toType);

                if (toType == typeof(DateTime))
                    return StringToDateTimeConverter(s);

                if (IsNumeric(toType))
                {
                    // сначала пытаемся корректный parse
                    if (decimal.TryParse(s, NumberStyles.Any, formatProvider, out var dec))
                        return Convert.ChangeType(dec, toType, CultureInfo.InvariantCulture);

                    // fallback на замену, если формат "1,23"
                    s = s.Replace(",", ".");
                    return Convert.ChangeType(s, toType, CultureInfo.InvariantCulture);
                }
            }

            // SQL Boolean
            if (fromType == typeof(bool) && toType.Name == "SqlBoolean")
                return Activator.CreateInstance(toType, (bool)value);

            // Универсальное приведение
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
        ///     Вычисляет комбинированный хеш-код для четырёх объектов.
        /// </summary>
        /// <typeparam name="T1">Тип первого объекта.</typeparam>
        /// <typeparam name="T2">Тип второго объекта.</typeparam>
        /// <typeparam name="T3">Тип третьего объекта.</typeparam>
        /// <typeparam name="T4">Тип четвёртого объекта.</typeparam>
        /// <param name="obj1">Первый объект. Может быть <c>null</c>.</param>
        /// <param name="obj2">Второй объект. Может быть <c>null</c>.</param>
        /// <param name="obj3">Третий объект. Может быть <c>null</c>.</param>
        /// <param name="obj4">Четвёртый объект. Может быть <c>null</c>.</param>
        /// <returns>
        ///     Целочисленный хеш-код, вычисленный комбинацией хешей всех переданных объектов.
        ///     Если объект равен <c>null</c>, используется значение 0.
        /// </returns>
        /// <remarks>
        ///     Используется стандартная формула комбинирования хешей:
        ///     <c>h = h * 31 + hash</c>. Переполнения не приводят к ошибке благодаря <c>unchecked</c>.
        /// </remarks>
        public static int ComputeHash<T1, T2, T3, T4>(T1 obj1, T2 obj2, T3 obj3, T4 obj4)
        {
            unchecked
            {
                var h = 17;
                h = h * 31 + (obj1?.GetHashCode() ?? 0);
                h = h * 31 + (obj2?.GetHashCode() ?? 0);
                h = h * 31 + (obj3?.GetHashCode() ?? 0);
                h = h * 31 + (obj4?.GetHashCode() ?? 0);
                return h;
            }
        }

        /// <summary>
        ///     Вычисляет комбинированный хеш-код для трех объектов.
        /// </summary>
        /// <typeparam name="T1">Тип первого объекта.</typeparam>
        /// <typeparam name="T2">Тип второго объекта.</typeparam>
        /// <typeparam name="T3">Тип третьего объекта.</typeparam>
        /// <param name="obj1">Первый объект. Может быть <c>null</c>.</param>
        /// <param name="obj2">Второй объект. Может быть <c>null</c>.</param>
        /// <param name="obj3">Третий объект. Может быть <c>null</c>.</param>
        /// <returns>
        ///     Целочисленный хеш-код, вычисленный комбинацией хешей всех переданных объектов.
        ///     Если объект равен <c>null</c>, используется значение 0.
        /// </returns>
        /// <remarks>
        ///     Используется стандартная формула комбинирования хешей:
        ///     <c>h = h * 31 + hash</c>. Переполнения не приводят к ошибке благодаря <c>unchecked</c>.
        /// </remarks>
        public static int ComputeHash<T1, T2, T3>(T1 obj1, T2 obj2, T3 obj3)
        {
            unchecked
            {
                var h = 17;
                h = h * 31 + (obj1?.GetHashCode() ?? 0);
                h = h * 31 + (obj2?.GetHashCode() ?? 0);
                h = h * 31 + (obj3?.GetHashCode() ?? 0);
                return h;
            }
        }

        /// <summary>
        ///     Вычисляет комбинированный хеш-код для двух объектов.
        /// </summary>
        /// <typeparam name="T1">Тип первого объекта.</typeparam>
        /// <typeparam name="T2">Тип второго объекта.</typeparam>
        /// <param name="obj1">Первый объект. Может быть <c>null</c>.</param>
        /// <param name="obj2">Второй объект. Может быть <c>null</c>.</param>
        /// <returns>
        ///     Целочисленный хеш-код, вычисленный комбинацией хешей всех переданных объектов.
        ///     Если объект равен <c>null</c>, используется значение 0.
        /// </returns>
        /// <remarks>
        ///     Используется стандартная формула комбинирования хешей:
        ///     <c>h = h * 31 + hash</c>. Переполнения не приводят к ошибке благодаря <c>unchecked</c>.
        /// </remarks>
        public static int ComputeHash<T1, T2>(T1 obj1, T2 obj2)
        {
            unchecked
            {
                var h = 17;
                h = h * 31 + (obj1?.GetHashCode() ?? 0);
                h = h * 31 + (obj2?.GetHashCode() ?? 0);
                return h;
            }
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
        ///     Ищет член типа (свойство, поле или метод) по его имени,
        ///     включая проверку в базовых типах и реализованных интерфейсах.
        /// </summary>
        /// <param name="type">Тип, в котором выполняется поиск.</param>
        /// <param name="name">Имя члена, который необходимо найти.</param>
        /// <param name="ignoreCase">
        ///     Если <c>true</c>, поиск выполняется без учета регистра букв.
        /// </param>
        /// <param name="bindingFlags">
        ///     Набор флагов <see cref="BindingFlags" />, определяющих стратегию поиска.
        ///     Если не указан, используется значение <c>DefaultBindingFlags</c>.
        /// </param>
        /// <returns>
        ///     Объект <see cref="MemberInfo" />, соответствующий найденному члену,
        ///     либо <c>null</c>, если подходящий член не найден.
        /// </returns>
        /// <remarks>
        ///     Метод выполняет поиск в следующем порядке:
        ///     <list type="number">
        ///         <item>
        ///             <description>Свойства типа;</description>
        ///         </item>
        ///         <item>
        ///             <description>Поля типа;</description>
        ///         </item>
        ///         <item>
        ///             <description>Свойства интерфейсов, реализованных данным типом;</description>
        ///         </item>
        ///         <item>
        ///             <description>Методы типа;</description>
        ///         </item>
        ///         <item>
        ///             <description>Рекурсивный поиск в базовом типе.</description>
        ///         </item>
        ///     </list>
        /// </remarks>
        public static MemberInfo FindMember(Type type, string name, bool ignoreCase = false, BindingFlags? bindingFlags = null)
        {
            var flags = bindingFlags ?? DefaultBindingFlags;
            if (ignoreCase)
                flags |= BindingFlags.IgnoreCase;

            // 1. Property
            var prop = type.GetProperty(name, flags);
            if (prop != null)
                return prop;

            // 2. Field
            var field = type.GetField(name, flags);
            if (field != null)
                return field;

            // 3. Interface properties
            foreach (var it in type.GetInterfaces())
            {
                var iprop = it.GetProperty(name, flags);
                if (iprop != null)
                    return iprop;
            }

            // 4. Method
            var method = type.GetMethod(name, flags);
            if (method != null)
                return method;

            // 5. Base types (итерация вместо рекурсии)
            var bt = type.BaseType;
            while (bt != null)
            {
                var m = FindMember(bt, name, ignoreCase, bindingFlags);
                if (m != null)
                    return m;
                bt = bt.BaseType;
            }

            return null;
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
        public static Attribute GetCustomAttribute(MemberInfo member, string attributeName, StringComparison stringComparison = StringComparison.OrdinalIgnoreCase)
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
            var fieldMap = GetFieldsMap(type);
            return fieldMap.Values.FirstOrDefault(matchCriteria);
        }

        /// <summary>
        ///     Пытается определить поле из IL кода метода доступа (геттера/сеттера).
        /// </summary>
        /// <param name="accessor">Метод доступа (геттер или сеттер свойства)</param>
        /// <returns>Найденное поле или null, если не удалось определить</returns>
        public static FieldInfo GetFieldInfoFromGetAccessor(MethodInfo accessor)
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

        /// <summary>
        ///     Возвращает отображение имён полей типа на объекты <see cref="FieldInfo" />.
        /// </summary>
        /// <typeparam name="T">Тип, поля которого требуется получить.</typeparam>
        /// <returns>Словарь «имя поля → FieldInfo».</returns>
        public static Dictionary<string, FieldInfo> GetFieldsMap<T>()
        {
            return GetFieldsMap(typeof(T));
        }

        /// <summary>
        ///     Возвращает отображение имён полей указанного типа на объекты <see cref="FieldInfo" />.
        /// </summary>
        /// <param name="type">Тип, поля которого требуется получить.</param>
        /// <returns>Словарь «имя поля → FieldInfo».</returns>
        public static Dictionary<string, FieldInfo> GetFieldsMap(Type type)
        {
            if (FieldsCache.TryGetValue(type, out var cached))
                return cached;

            var typeFields = type.GetFields(DefaultBindingFlags);
            var dic = new Dictionary<string, FieldInfo>();
            foreach (var field in typeFields)
                dic[field.Name] = field;

            FieldsCache[type] = dic;
            return dic;
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
                var member = type.GetEvent(name, DefaultBindingFlags);
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
                var member = type.GetField(name, DefaultBindingFlags);
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
                var member = type.GetMethod(name, DefaultBindingFlags);
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
                var member = type.GetProperty(name, DefaultBindingFlags);
                if (member != null)
                    return member;
                type = type.BaseType;
            }

            return null;
        }

        /// <summary>
        ///     Создаёт делегат для получения значения указанного члена типа
        ///     (свойства, поля или метода-геттера).
        /// </summary>
        /// <typeparam name="T">Тип объекта, из которого будет извлекаться значение.</typeparam>
        /// <typeparam name="TResult">Тип возвращаемого значения.</typeparam>
        /// <param name="memberName">Имя свойства, поля или метода.</param>
        /// <param name="sourceType">
        ///     Тип, в котором искать член.
        ///     Если не указан, используется тип <typeparamref name="T" />.
        /// </param>
        /// <returns>
        ///     Делегат <see cref="Func{T, TResult}" />, который извлекает значение соответствующего члена.
        /// </returns>
        /// <exception cref="NotSupportedException">
        ///     Выбрасывается, если найденный член имеет тип, для которого не может быть создан геттер.
        /// </exception>
        /// <remarks>
        ///     Метод кеширует созданные делегаты на основе хэша комбинации:
        ///     <typeparamref name="T" />, <paramref name="sourceType" /> и имени члена.
        ///     Поддерживаются следующие типы членов:
        ///     <list type="bullet">
        ///         <item>
        ///             <description>Свойства (<see cref="PropertyInfo" />)</description>
        ///         </item>
        ///         <item>
        ///             <description>Поля (<see cref="FieldInfo" />)</description>
        ///         </item>
        ///         <item>
        ///             <description>Методы без параметров (<see cref="MethodInfo" />)</description>
        ///         </item>
        ///     </list>
        /// </remarks>
        public static Func<T, TResult> GetMemberGetter<T, TResult>(string memberName, Type sourceType = null)
        {
            var actualSourceType = sourceType ?? typeof(T);

            var key = new CacheKey(
                typeof(T),
                actualSourceType,
                typeof(TResult),
                memberName
            );

            if (GettersCache.TryGetValue(key, out var del))
                return (Func<T, TResult>)del;

            var bindingFlags =
                BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static;

            var memberInfo = (FindMember(actualSourceType, memberName)
                              ?? FindMember(actualSourceType, memberName, true, bindingFlags));

            if (memberInfo == null)
                return null;

            Func<T, TResult> func;

            switch (memberInfo)
            {
                case PropertyInfo pi:
                    func = CreatePropertyGetter<T, TResult>(pi);
                    break;

                case FieldInfo fi:
                    func = CreateFieldGetter<T, TResult>(fi);
                    break;

                case MethodInfo mi:
                    func = CreateMethodGetter<T, TResult>(mi);
                    break;

                default:
                    throw new NotSupportedException($"Тип члена не поддерживается: {memberInfo.MemberType}");
            }

            GettersCache[key] = func;
            return func;
        }

        /// <summary>
        ///     Создаёт делегат для получения значения указанного члена типа,
        ///     возвращающий результат в виде <see cref="object" />.
        /// </summary>
        /// <typeparam name="T">Тип объекта, из которого извлекается значение.</typeparam>
        /// <param name="memberName">Имя свойства, поля или метода.</param>
        /// <param name="sourceType">
        ///     Тип, в котором выполнять поиск.
        ///     Если не указан, используется тип <typeparamref name="T" />.
        /// </param>
        /// <returns>
        ///     Делегат <see cref="Func{T, Object}" />, возвращающий значение указанного члена.
        /// </returns>
        public static Func<T, object> GetMemberGetter<T>(string memberName, Type sourceType = null)
        {
            return GetMemberGetter<T, object>(memberName, sourceType);
        }

        /// <summary>
        ///     Создаёт делегат для получения значения указанного члена типа,
        ///     принимая объект в виде <see cref="object" /> и возвращая результат как <see cref="object" />.
        /// </summary>
        /// <param name="memberName">Имя свойства, поля или метода.</param>
        /// <param name="sourceType">
        ///     Тип, в котором выполняется поиск.
        ///     Если не указан, используется <see cref="object" />.
        /// </param>
        /// <returns>
        ///     Делегат <see cref="Func{Object, Object}" />, возвращающий значение указанного члена.
        /// </returns>
        /// <remarks>
        ///     Это универсальная версия метода <see cref="GetMemberGetter{T, TResult}" />,
        ///     которая позволяет работать с объектами и членами без знания их типов во время компиляции.
        /// </remarks>
        public static Func<object, object> GetMemberGetter(string memberName, Type sourceType = null)
        {
            return GetMemberGetter<object, object>(memberName, sourceType);
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
        ///     Создаёт делегат для установки значения указанного члена типа,
        ///     принимая объект и значение в виде <see cref="object" />.
        /// </summary>
        /// <param name="memberName">Имя свойства или поля.</param>
        /// <param name="sourceType">
        ///     Тип, в котором выполняется поиск.
        ///     Если не указан, используется <see cref="object" />.
        /// </param>
        /// <returns>
        ///     Делегат <see cref="Action{Object, Object}" />, который устанавливает значение указанного члена.
        /// </returns>
        public static Action<object, object> GetMemberSetter(string memberName, Type sourceType)
        {
            return GetMemberSetter<object, object>(memberName, sourceType);
        }

        /// <summary>
        ///     Создаёт делегат для установки значения указанного члена типа <typeparamref name="T" />,
        ///     принимая значение в виде <see cref="object" />.
        /// </summary>
        /// <typeparam name="T">Тип объекта, для которого создается сеттер.</typeparam>
        /// <param name="memberName">Имя свойства или поля.</param>
        /// <param name="sourceType">
        ///     Тип, в котором выполняется поиск.
        ///     Если не указан, используется тип <typeparamref name="T" />.
        /// </param>
        /// <returns>
        ///     Делегат <see cref="Action{T, Object}" />, который устанавливает значение указанного члена.
        /// </returns>
        public static Action<T, object> GetMemberSetter<T>(string memberName, Type sourceType = null)
        {
            return GetMemberSetter<T, object>(memberName, sourceType);
        }

        /// <summary>
        ///     Создаёт делегат для установки значения указанного члена типа <typeparamref name="T" />,
        ///     с типом значения <typeparamref name="TResult" />.
        /// </summary>
        /// <typeparam name="T">Тип объекта, для которого создается сеттер.</typeparam>
        /// <typeparam name="TResult">Тип значения, которое устанавливается.</typeparam>
        /// <param name="memberName">Имя свойства или поля.</param>
        /// <param name="sourceType">
        ///     Тип, в котором выполняется поиск.
        ///     Если не указан, используется тип <typeparamref name="T" />.
        /// </param>
        /// <returns>
        ///     Делегат <see cref="Action{T, TValue}" />, который устанавливает значение указанного члена.
        /// </returns>
        /// <exception cref="NotSupportedException">
        ///     Выбрасывается, если найденный член не является свойством или полем, для которого можно создать сеттер.
        /// </exception>
        /// <remarks>
        ///     Для автосвойств используется поле компилятора (<c>BackingField</c>) для установки значения.
        /// </remarks>
        public static Action<T, TResult> GetMemberSetter<T, TResult>(string memberName, Type sourceType = null)
        {
            var actualSourceType = sourceType ?? typeof(T);

            var key = new CacheKey(
                typeof(T),
                actualSourceType,
                typeof(TResult),
                memberName
            );

            if (SettersCache.TryGetValue(key, out var del))
                return (Action<T, TResult>)del;

            var bindingFlags =
                BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static;

            var memberInfo = (FindMember(actualSourceType, memberName)
                              ?? FindMember(actualSourceType, memberName, true, bindingFlags));

            if (memberInfo == null)
                return null;

            Action<T, TResult> action;

            switch (memberInfo)
            {
                case PropertyInfo pi:
                    action = !pi.CanWrite
                        ? (GetPropertyBackingFieldInfo(pi, out var pfi) ? CreateFieldSetter<T, TResult>(pfi) : null)
                        : CreatePropertySetter<T, TResult>(pi);
                    break;

                case FieldInfo fi:
                    action = CreateFieldSetter<T, TResult>(fi);
                    break;

                default:
                    throw new NotSupportedException($"Setter не поддерживается для члена: {memberInfo.MemberType}");
            }

            SettersCache[key] = action;
            return action;
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
        public static Action<TSource, object> GetMemberSetter<TSource, TSourceProp>(this Expression<Func<TSource, TSourceProp>> propSelector, out string propertyName)
        {
            if (!(propSelector.Body is MemberExpression member))
                throw new ArgumentException(@"Выражение должно указывать на свойство.", nameof(propSelector));

            propertyName = member.Member.Name;
            return GetMemberSetter<TSource>(propertyName);
        }

        /// <summary>
        ///     Получает значения всех полей и свойств типа T из объекта TClass.
        /// </summary>
        /// <typeparam name="T">Тип члена, который ищем.</typeparam>
        /// <typeparam name="TClass">Тип объекта.</typeparam>
        /// <param name="obj">Объект, из которого извлекаем значения.</param>
        /// <param name="memberFilter">Опциональный фильтр значений.</param>
        /// <param name="recursive">Если true, рекурсивно обходит вложенные объекты.</param>
        /// <param name="searchInCollections">Если true, рекурсивно ищет элементы типа T в коллекциях.</param>
        public static IEnumerable<T> GetMembersOfType<TClass, T>(this TClass obj, Func<T, bool> memberFilter = null, bool recursive = false, bool searchInCollections = false) where TClass : class
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            var visited = new HashSet<object>();
            return GetMembersInternal(obj, memberFilter, recursive, searchInCollections, visited);
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
        /// var value = PropertyHelper.GetMemberValue(person, "Name"); // "Alice"
        /// </code>
        /// </summary>
        public static object GetMemberValue(this object source, string propertyName, Type tryConvertToType = null, bool throwIfSourceIsNull = true)
        {
            if (source == null)
                if (throwIfSourceIsNull)
                    throw new ArgumentNullException(nameof(source));
                else
                    return null;

            var getter = GetMemberGetter(propertyName, source.GetType());
            return tryConvertToType != null
                ? ChangeType(getter(source), tryConvertToType)
                : getter(source);
        }

        /// <summary>
        ///     Возвращает значение свойства с типизацией результата.<br />
        ///     Создает и кэширует делегат для быстрого доступа к свойству по имени.<br />
        ///     Особенности:<br />
        ///     - Имя свойства реегистронезависимо.<br />
        ///     - Позволяет получить значение свойства с приведением к нужному типу (Тип должен быть совместимым! Автоматической
        ///     конвертации типов не происходит!).<br />
        ///     - Использует кэширование делегатов для повышения производительности.<br />
        ///     - Генерирует исключение, если свойство не найдено или несовместимо по типу.<br />
        ///     Пример:
        ///     <code>
        /// var person = new Person { Age = 42 };
        /// int age = PropertyHelper.GetMemberValue&lt;Person, int&gt;(person, "Age"); // 42
        /// </code>
        /// </summary>
        public static TValue GetMemberValue<TValue>(this object source, string propertyName, bool throwIfSourceIsNull = true)
        {
            if (source == null)
                if (throwIfSourceIsNull)
                    throw new ArgumentNullException(nameof(source));
                else
                    return default;

            var getter = GetMemberGetter<object, object>(propertyName, source.GetType());
            return ChangeType<TValue>(getter(source));
        }

        /// <summary>
        ///     Возвращает значение по ключу из словаря или добавляет его, если ключ отсутствует.
        /// </summary>
        /// <typeparam name="TKey">Тип ключа словаря.</typeparam>
        /// <typeparam name="TValue">Тип значения словаря.</typeparam>
        /// <param name="dic">Словарь, в котором выполняется поиск или добавление.</param>
        /// <param name="key">Ключ для поиска или добавления значения.</param>
        /// <param name="valueFactory">Функция, создающая значение, если ключ отсутствует.</param>
        /// <returns>Значение, соответствующее ключу.</returns>
        /// <exception cref="ArgumentNullException">Выбрасывается, если <paramref name="dic" /> равен <c>null</c>.</exception>
        public static TValue GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> dic, TKey key, Func<TValue> valueFactory)
        {
            if (dic.TryGetValue(key, out var val))
                return val;

            val = valueFactory();
            dic[key] = val;
            return val;
        }

        /// <summary>
        ///     Получает все публичные свойства типа <typeparamref name="T" />.
        ///     Использует внутренний кеш для ускорения повторных вызовов.
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
            return GetPropertiesMap(type).Values.ToArray();
        }

        /// <summary>
        ///     Возвращает отображение имён свойств типа на объекты <see cref="PropertyInfo" />.
        /// </summary>
        /// <typeparam name="T">Тип, свойства которого требуется получить.</typeparam>
        /// <returns>Словарь «имя свойства → PropertyInfo».</returns>
        public static Dictionary<string, PropertyInfo> GetPropertiesMap<T>()
        {
            return GetPropertiesMap(typeof(T));
        }

        /// <summary>
        ///     Возвращает отображение имён свойств указанного типа на объекты <see cref="PropertyInfo" />.
        /// </summary>
        /// <param name="type">Тип, свойства которого требуется получить.</param>
        /// <returns>Словарь «имя свойства → PropertyInfo».</returns>
        public static Dictionary<string, PropertyInfo> GetPropertiesMap(Type type)
        {
            if (PropertiesCache.TryGetValue(type, out var cached))
                return cached;

            var typeProperties = type.GetProperties(DefaultBindingFlags);
            var dic = new Dictionary<string, PropertyInfo>();
            foreach (var prop in typeProperties)
                dic[prop.Name] = prop;

            PropertiesCache[type] = dic;
            return dic;
        }

        /// <summary>
        ///     Получить свойство по его имени
        /// </summary>
        /// <param name="type">Тип в котором искать свойство</param>
        /// <param name="propertyName">Имя свойства</param>
        /// <param name="stringComparison">Сравнение имен</param>
        /// <returns></returns>
        public static PropertyInfo GetProperty(Type type, string propertyName, StringComparison stringComparison = StringComparison.OrdinalIgnoreCase)
        {
            if (stringComparison == StringComparison.OrdinalIgnoreCase) return GetPropertiesMap(type).Values.FirstOrDefault(x => string.Compare(x.Name, propertyName, stringComparison) == 0);
            return GetPropertiesMap(type).TryGetValue(propertyName, out var pi) ? pi : null;
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
        ///     Возвращает поле, соответствующее автосвойству (<c>BackingField</c>) или полю,
        ///     доступному через геттер свойства.
        /// </summary>
        /// <param name="pi">Свойство, для которого нужно получить поле.</param>
        /// <param name="fieldInfo"></param>
        /// <returns>
        ///     <see cref="FieldInfo" /> — поле, соответствующее свойству.
        /// </returns>
        /// <remarks>
        ///     Для автосвойств компилятор создаёт скрытое поле с именем вида
        ///     <c>&lt;PropertyName&gt;k__BackingField</c>.
        ///     Если поле не найдено напрямую, выполняется попытка получить его из метода-геттера.
        /// </remarks>
        public static bool GetPropertyBackingFieldInfo(PropertyInfo pi, out FieldInfo fieldInfo)
        {
            // Для автосвойств компилятор создает поле с именем <PropertyName>k__BackingField
            var backingFieldName = $"<{pi.Name}>k__BackingField";
            var fi = pi.DeclaringType?.GetField(backingFieldName, DefaultBindingFlags) ??
                     GetFieldInfoFromGetAccessor(pi.GetGetMethod(true));
            fieldInfo = fi;
            return fieldInfo != null;
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
            return GetPropertiesMap(type).Keys.ToArray();
        }

        /// <summary>
        ///     Получает значения свойств объекта в указанном порядке
        /// </summary>
        /// <typeparam name="TObject"></typeparam>
        /// <param name="source">Исходный объект</param>
        /// <param name="propertyNames">Имена свойств объекта с учетом регистра</param>
        /// <returns></returns>
        public static object[] GetPropertyValues<TObject>(TObject source, params string[] propertyNames) where TObject : class
        {
            var values = new List<object>();
            foreach (var property in propertyNames)
            {
                var getter = GetMemberGetter<TObject>(property, source.GetType());
                values.Add(getter?.Invoke(source));
            }

            return values.ToArray();
        }

        /// <summary>
        ///     Получает значения свойств объекта в указанном порядке и преобразует в указанный тип через
        ///     <see cref="TypeHelper.ChangeType{T}(object, IFormatProvider)" />
        /// </summary>
        /// <typeparam name="TObject"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="source">Исходный объект</param>
        /// <param name="propertyNames">Имена свойств объекта с учетом регистра</param>
        /// <returns></returns>
        public static TValue[] GetPropertyValues<TObject, TValue>(TObject source, params string[] propertyNames) where TObject : class
        {
            return GetPropertyValues(source, propertyNames).Select(x => ChangeType<TValue>(x)).ToArray();
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
                throw new ArgumentException(@"Type name cannot be null or empty.", nameof(typeOrInterfaceName));

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

        /// <summary>
        ///     Возвращает значение по ключу из коллекции пар ключ-значение или значение по умолчанию, если ключ отсутствует.
        /// </summary>
        /// <typeparam name="TKey">Тип ключа.</typeparam>
        /// <typeparam name="TValue">Тип значения.</typeparam>
        /// <param name="dic">Коллекция пар ключ-значение.</param>
        /// <param name="key">Ключ для поиска.</param>
        /// <param name="comparer">
        ///     Компаратор для сравнения ключей.
        ///     Если <c>null</c>, используется стандартное сравнение по <see cref="EqualityComparer{TKey}.Default" />.
        /// </param>
        /// <returns>
        ///     Значение, соответствующее ключу, или значение по умолчанию для <typeparamref name="TValue" />, если ключ не найден.
        /// </returns>
        /// <exception cref="ArgumentNullException">Выбрасывается, если <paramref name="dic" /> равен <c>null</c>.</exception>
        /// <remarks>
        ///     Метод выбирает стратегию поиска в зависимости от наличия компаратора и размера коллекции:
        ///     <list type="number">
        ///         <item>
        ///             <description>Если компаратор не задан — выполняется линейный поиск.</description>
        ///         </item>
        ///         <item>
        ///             <description>Если коллекция небольшая (<c>Count &lt; ThresholdForSorted</c>) — также линейный поиск.</description>
        ///         </item>
        ///         <item>
        ///             <description>
        ///                 Если коллекция большая — создаётся кэшированный <see cref="SortedDictionary{TKey, TValue}" />
        ///                 для быстрого поиска.
        ///             </description>
        ///         </item>
        ///     </list>
        /// </remarks>
        public static TValue GetValueOrDefault<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> dic, TKey key, IComparer<TKey> comparer = null)
        {
            if (dic == null)
                throw new ArgumentNullException(nameof(dic));

            // 1️ Компаратор не задан — быстрый линейный поиск
            if (comparer == null)
            {
                foreach (var kv in dic)
                    if (EqualityComparer<TKey>.Default.Equals(kv.Key, key))
                        return kv.Value;

                return default;
            }

            // 2️ Компаратор задан
            // Считаем элементы, чтобы определить способ поиска
            // Маленькая коллекция — линейный поиск
            foreach (var kv in dic)
                if (comparer.Compare(kv.Key, key) == 0)
                    return kv.Value;

            return default;
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
                   (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>)) || type
                       .GetInterfaces()
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
        ///     Создаёт новый экземпляр типа <typeparamref name="T" />
        ///     с использованием заранее сгенерированного делегата конструктора.
        /// </summary>
        /// <typeparam name="T">
        ///     Тип создаваемого объекта.
        ///     Требует наличия конструктора без параметров.
        /// </typeparam>
        /// <returns>
        ///     Новый экземпляр типа <typeparamref name="T" />.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        ///     Выбрасывается, если тип не имеет конструктора по умолчанию.
        /// </exception>
        /// <remarks>
        ///     Метод является быстрым способом создания объектов, так как использует
        ///     предварительно скомпилированный делегат конструктора, полученный через IL-генерацию.
        /// </remarks>
        public static T New<T>(params object[] args)
        {
            return (T)New(typeof(T), args);
        }

        /// <summary>
        /// Находит конструктор указанного типа, параметры которого совместимы
        /// с переданным набором аргументов.
        /// </summary>
        /// <param name="type">
        /// Тип, в котором требуется найти подходящий конструктор.
        /// </param>
        /// <param name="args">
        /// Массив аргументов, по типам которых выполняется поиск конструктора.
        /// Если элемент массива равен <c>null</c>, считается, что его тип — <see cref="object"/>.
        /// </param>
        /// <returns>
        /// Экземпляр <see cref="ConstructorInfo"/>, представляющий первый найденный
        /// конструктор, параметры которого по количеству и типам совместимы
        /// с переданными аргументами.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Выбрасывается, если подходящий конструктор не найден.
        /// </exception>
        public static ConstructorInfo FindConstructor(Type type, object[] args)
        {
            var argTypes = args.Select(a => a?.GetType() ?? typeof(object)).ToArray();

            return type.GetConstructors()
                .First(c =>
                {
                    var ps = c.GetParameters();
                    if (ps.Length != argTypes.Length) return false;

                    for (int i = 0; i < ps.Length; i++)
                    {
                        if (!ps[i].ParameterType.IsAssignableFrom(argTypes[i]))
                            return false;
                    }
                    return true;
                });
        }

        private static Func<object[], object> CreateFactory(ConstructorInfo ctor)
        {
            var argsParam = Expression.Parameter(typeof(object[]), "args");

            var ctorArgs = ctor.GetParameters()
                .Select((p, i) =>
                    Expression.Convert(
                        Expression.ArrayIndex(argsParam, Expression.Constant(i)),
                        p.ParameterType))
                .ToArray<Expression>();

            var newExpr = Expression.New(ctor, ctorArgs);

            var body = Expression.Convert(newExpr, typeof(object));

            return Expression
                .Lambda<Func<object[], object>>(body, argsParam)
                .Compile();
        }

        /// <summary>
        ///     Создает новый экземпляр указанного типа и приводит его к типу <typeparamref name="T" />.
        /// </summary>
        /// <typeparam name="T">Тип, к которому приводится создаваемый объект.</typeparam>
        /// <param name="type">Тип создаваемого объекта. Должен иметь конструктор без параметров.</param>
        /// <returns>Новый экземпляр типа <typeparamref name="T" />.</returns>
        /// <exception cref="InvalidOperationException">Выбрасывается, если тип не имеет конструктора по умолчанию.</exception>
        public static T New<T>(Type type)
        {
            return (T)New(type);
        }

        private static readonly ConcurrentDictionary<ConstructorInfo, Func<object[], object>> CtorCache = new ConcurrentDictionary<ConstructorInfo, Func<object[], object>>();

        /// <summary>
        /// Создаёт новый экземпляр указанного типа, используя конструктор,
        /// соответствующий переданным аргументам.
        /// </summary>
        /// <param name="type">
        /// Тип создаваемого объекта.
        /// </param>
        /// <param name="args">
        /// Аргументы, передаваемые в конструктор.
        /// </param>
        /// <returns>
        /// Новый экземпляр указанного типа.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Выбрасывается, если не удалось найти подходящий конструктор
        /// для переданных аргументов.
        /// </exception>
        public static object New(Type type, params object[] args)
        {
            var ctor = FindConstructor(type, args);
            var factory = CtorCache.GetOrAdd(ctor, CreateFactory);
            return factory(args);
        }

        /// <summary>
        ///     Установить значение свойству объекта по имени.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">Объект</param>
        /// <param name="propertyName">Имя свойства</param>
        /// <param name="value">Значение свойства</param>
        /// <param name="throwIfSourceIsNull"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool SetMemberValue<T>(T source, string propertyName, object value, bool throwIfSourceIsNull = true) where T : class
        {
            if (source == null) return throwIfSourceIsNull ? throw new ArgumentNullException(nameof(source)) : false;

            var setter = GetMemberSetter<T>(propertyName);
            if (setter == null)
                return false;
            setter(source, value);
            return true;
        }

        /// <summary>
        ///     Установить значение свойству объекта по имени.
        /// </summary>
        /// <param name="source">Объект</param>
        /// <param name="propertyName">Имя свойства</param>
        /// <param name="value">Значение свойства</param>
        /// <param name="throwIfSourceIsNull"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool SetMemberValue(this object source, string propertyName, object value, bool throwIfSourceIsNull = true)
        {
            if (source == null) return throwIfSourceIsNull ? throw new ArgumentNullException(nameof(source)) : false;

            var setter = GetMemberSetter(propertyName, source.GetType());
            if (setter == null)
                return false;
            setter(source, value);
            return true;
        }

        /// <summary>
        ///     Обновляет кэши геттеров и сеттеров для всех свойств указанного типа <typeparamref name="T" />.
        /// </summary>
        /// <typeparam name="T">Тип, для которого обновляется кэш.</typeparam>
        /// <remarks>
        ///     Метод получает карту всех свойств типа через <see cref="GetPropertiesMap{T}" />
        ///     и вызывает методы <see cref="GetMemberSetter{T}(string, Type)" /> и
        ///     <see cref="GetMemberGetter{T}(string, Type)" /> для каждого свойства,
        ///     чтобы заранее инициализировать кэшированные делегаты.
        /// </remarks>
        public static void UpdateCache<T>()
        {
            var map = GetPropertiesMap<T>();
            foreach (var kv in map)
            {
                GetMemberSetter<T>(kv.Key);
                GetMemberGetter<T>(kv.Key);
            }
        }

        private static Func<T, TResult> CreateFieldGetter<T, TResult>(FieldInfo fi)
        {
            var dm = new DynamicMethod(
                "get_" + fi.Name,
                typeof(TResult),
                new[] { typeof(T) },
                typeof(T),
                true
            );

            var il = dm.GetILGenerator();

            if (fi.IsStatic)
            {
                il.Emit(OpCodes.Ldsfld, fi);
            }
            else
            {
                il.Emit(OpCodes.Ldarg_0);

                if (typeof(T).IsValueType)
                    il.Emit(OpCodes.Unbox_Any, typeof(T));

                il.Emit(OpCodes.Ldfld, fi);
            }

            EmitCast(il, fi.FieldType, typeof(TResult));

            il.Emit(OpCodes.Ret);

            return (Func<T, TResult>)dm.CreateDelegate(typeof(Func<T, TResult>));
        }

        private static void EmitCast(ILGenerator il, Type from, Type to)
        {
            if (from == to)
                return;

            if (from.IsValueType && to == typeof(object))
            {
                il.Emit(OpCodes.Box, from);
                return;
            }

            if (!from.IsValueType && to.IsValueType)
            {
                il.Emit(OpCodes.Unbox_Any, to);
                return;
            }

            if (!from.IsValueType && !to.IsValueType)
            {
                il.Emit(OpCodes.Castclass, to);
                return;
            }

            throw new InvalidOperationException($"Нельзя привести {from} к {to}");
        }

        private static Action<T, TValue> CreateFieldSetter<T, TValue>(FieldInfo fi)
        {
            var dm = new DynamicMethod(
                "set_" + fi.Name,
                null,
                new[] { typeof(T), typeof(TValue) },
                typeof(T),
                true);

            var il = dm.GetILGenerator();

            var fieldType = fi.FieldType;
            var declaring = fi.DeclaringType;

            if (fi.IsStatic)
            {
                il.Emit(OpCodes.Ldarg_1);
                EmitConvertForSetter(il, typeof(TValue), fieldType);
                il.Emit(OpCodes.Stsfld, fi);
            }
            else
            {
                il.Emit(OpCodes.Ldarg_0);

                // ⚠️ value-type instance → нужен адрес
                if (declaring?.IsValueType == true)
                    il.Emit(OpCodes.Unbox, declaring);

                il.Emit(OpCodes.Ldarg_1);
                EmitConvertForSetter(il, typeof(TValue), fieldType);
                il.Emit(OpCodes.Stfld, fi);
            }

            il.Emit(OpCodes.Ret);

            return (Action<T, TValue>)dm.CreateDelegate(typeof(Action<T, TValue>));
        }

        private static Func<T, TResult> CreateMethodGetter<T, TResult>(MethodInfo mi)
        {
            if (mi == null)
                throw new ArgumentNullException(nameof(mi));

            if (mi.ReturnType == typeof(void))
                throw new InvalidOperationException("Метод возвращает void.");

            if (mi.GetParameters().Length != 0)
                throw new InvalidOperationException("Метод с параметрами нельзя использовать как геттер.");

            // Проверяем совместимость типов
            if (!typeof(TResult).IsAssignableFrom(mi.ReturnType) &&
                mi.ReturnType.IsValueType && typeof(TResult) != typeof(object))
                throw new InvalidOperationException(
                    $"Невозможно преобразовать {mi.ReturnType} в {typeof(TResult)}");

            // ---------------- Static method ----------------
            if (mi.IsStatic)
            {
                var dm = new DynamicMethod(
                    "get_" + mi.Name,
                    typeof(TResult),
                    new[] { typeof(T) }, // параметр игнорируется
                    typeof(T),
                    true);

                var il = dm.GetILGenerator();

                // CALL static method
                il.Emit(OpCodes.Call, mi);

                // Преобразование к TResult, если нужно
                if (mi.ReturnType != typeof(TResult))
                {
                    if (mi.ReturnType.IsValueType && typeof(TResult) == typeof(object))
                        il.Emit(OpCodes.Box, mi.ReturnType);
                    else
                        il.Emit(OpCodes.Castclass, typeof(TResult));
                }

                il.Emit(OpCodes.Ret);

                return (Func<T, TResult>)dm.CreateDelegate(typeof(Func<T, TResult>));
            }

            // ---------------- Instance method ----------------
            {
                var dm = new DynamicMethod(
                    "get_" + mi.Name,
                    typeof(TResult),
                    new[] { typeof(T) },
                    typeof(T),
                    true);

                var il = dm.GetILGenerator();

                // Load argument 0 (instance)
                il.Emit(OpCodes.Ldarg_0);

                // Если T — value type → unbox
                if (typeof(T).IsValueType)
                    il.Emit(OpCodes.Unbox_Any, typeof(T));

                // CALLVIRT instance method
                il.Emit(OpCodes.Callvirt, mi);

                // Преобразование к TResult
                if (mi.ReturnType != typeof(TResult))
                {
                    if (mi.ReturnType.IsValueType && typeof(TResult) == typeof(object))
                        il.Emit(OpCodes.Box, mi.ReturnType);
                    else
                        il.Emit(OpCodes.Castclass, typeof(TResult));
                }

                il.Emit(OpCodes.Ret);

                return (Func<T, TResult>)dm.CreateDelegate(typeof(Func<T, TResult>));
            }
        }

        private static Func<TClass, TProp> CreatePropertyGetter<TClass, TProp>(PropertyInfo pi)
        {
            if (pi == null)
                throw new ArgumentNullException(nameof(pi));

            var getMethod = pi.GetGetMethod(true)
                            ?? throw new InvalidOperationException(
                                $"Property '{pi.Name}' has no getter.");

            var declaring = pi.DeclaringType
                            ?? throw new InvalidOperationException("DeclaringType is null.");

            var propType = pi.PropertyType;

            ////// readonly struct → небезопасно
            ////if (declaring.IsValueType &&
            ////    declaring.IsDefined(typeof(System.Runtime.CompilerServices.IsReadOnlyAttribute), false))
            ////{
            ////    throw new NotSupportedException(
            ////        $"Readonly struct property '{pi.Name}' is not supported.");
            ////}

            var dm = new DynamicMethod(
                "get_" + pi.Name,
                propType,
                new[] { declaring },
                pi.Module, // ✅ ВСЕГДА корректно
                true);

            var il = dm.GetILGenerator();

            //
            // ───── static property ─────
            //
            if (getMethod.IsStatic)
            {
                il.Emit(OpCodes.Call, getMethod);
                il.Emit(OpCodes.Ret);
            }
            else
            {
                //
                // ───── instance property ─────
                //
                il.Emit(OpCodes.Ldarg_0); // declaring instance

                if (declaring.IsValueType)
                {
                    il.Emit(OpCodes.Constrained, declaring);
                    il.Emit(OpCodes.Callvirt, getMethod);
                }
                else
                {
                    il.Emit(OpCodes.Callvirt, getMethod);
                }

                il.Emit(OpCodes.Ret);
            }

            //
            // ───── оборачиваем в Func<TClass, TProp> ─────
            //
            var rawGetter = dm.CreateDelegate(
                typeof(Func<,>).MakeGenericType(declaring, propType));

            return CreateWrapper<TClass, TProp>(rawGetter);
        }

        private static Func<TClass, TProp> CreateWrapper<TClass, TProp>(Delegate rawGetter)
        {
            return instance =>
            {
                var value = rawGetter.DynamicInvoke(instance);
                return (TProp)value;
            };
        }

        private static Action<TClass, TProp> CreatePropertySetter<TClass, TProp>(PropertyInfo pi)
        {
            if (pi == null)
                throw new ArgumentNullException(nameof(pi));

            var setMethod = pi.GetSetMethod(true)
                            ?? throw new InvalidOperationException(
                                $"Property '{pi.Name}' has no setter.");

            var declaring = pi.DeclaringType
                            ?? throw new InvalidOperationException("DeclaringType is null.");

            var propType = pi.PropertyType;

            var dm = new DynamicMethod(
                "set_" + pi.Name,
                typeof(void),
                new[] { typeof(TClass), typeof(TProp) },
                pi.Module,
                true);

            var il = dm.GetILGenerator();

            //
            // ───── static property ─────
            //
            if (setMethod.IsStatic)
            {
                il.Emit(OpCodes.Ldarg_1);
                EmitConvertForSetter(il, typeof(TProp), propType);
                il.Emit(OpCodes.Call, setMethod);
                il.Emit(OpCodes.Ret);

                return (Action<TClass, TProp>)dm.CreateDelegate(typeof(Action<TClass, TProp>));
            }

            //
            // ───── instance property ─────
            //
            il.Emit(OpCodes.Ldarg_0);

            if (declaring.IsValueType)
            {
                // struct: &this + constrained
                il.Emit(OpCodes.Unbox, declaring);

                il.Emit(OpCodes.Ldarg_1);
                EmitConvertForSetter(il, typeof(TProp), propType);

                il.Emit(OpCodes.Constrained, declaring);
                il.Emit(OpCodes.Callvirt, setMethod);
            }
            else
            {
                // class / interface
                if (declaring != typeof(TClass))
                    il.Emit(OpCodes.Castclass, declaring);

                il.Emit(OpCodes.Ldarg_1);
                EmitConvertForSetter(il, typeof(TProp), propType);

                il.Emit(OpCodes.Callvirt, setMethod);
            }

            il.Emit(OpCodes.Ret);

            return (Action<TClass, TProp>)dm.CreateDelegate(typeof(Action<TClass, TProp>));
        }

        private static void EmitConvertForSetter(ILGenerator il, Type from, Type to)
        {
            if (from == to)
                return;

            // object -> value type
            if (!from.IsValueType && to.IsValueType)
            {
                il.Emit(OpCodes.Unbox_Any, to);
                return;
            }

            // value type -> object (обычно не нужно для stfld, но пусть будет)
            if (from.IsValueType && to == typeof(object))
            {
                il.Emit(OpCodes.Box, from);
                return;
            }

            // reference -> reference
            if (!from.IsValueType && !to.IsValueType)
            {
                il.Emit(OpCodes.Castclass, to);
                return;
            }

            throw new InvalidOperationException(
                $"Невозможно привести {from} к {to} для setter");
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

        private static IEnumerable<T> GetMembersInternal<T>(object obj, Func<T, bool> memberFilter, bool recursive, bool searchInCollections, HashSet<object> visited)
        {
            if (obj == null) yield break;

            var type = obj.GetType();

            // Для примитивов и строк обходим только если тип совпадает с T
            if (type.IsPrimitive || obj is string)
            {
                if (obj is T tValue && (memberFilter == null || memberFilter(tValue)))
                    yield return tValue;
                yield break;
            }

            if (!visited.Add(obj)) yield break;

            // Если коллекция и нужно искать в коллекциях
            if (searchInCollections && obj is IEnumerable enumerable)
                foreach (var item in enumerable)
                    foreach (var nested in GetMembersInternal(item, memberFilter, recursive, true, visited))
                        yield return nested;

            // Поля
            var fields = GetFieldsMap(type).Values;
            foreach (var field in fields)
            {
                var value = field.GetValue(obj);
                if (value == null) continue;

                if (value is T tValue && (memberFilter == null || memberFilter(tValue)))
                    yield return tValue;

                if (recursive && !value.GetType().IsPrimitive && !(value is string))
                    foreach (var nested in GetMembersInternal(value, memberFilter, true, searchInCollections, visited))
                        yield return nested;
            }

            // Свойства
            var properties = GetPropertiesMap(type).Values.Where(p => p.GetMethod != null);
            foreach (var prop in properties)
            {
                object value;
                try
                {
                    value = prop.GetValue(obj);
                }
                catch
                {
                    continue; // Пропускаем свойства с исключениями
                }

                switch (value)
                {
                    case null:
                        continue;
                    case T tValue when memberFilter == null || memberFilter(tValue):
                        yield return tValue;
                        break;
                }

                if (recursive && !value.GetType().IsPrimitive && !(value is string))
                    foreach (var nested in GetMembersInternal(value, memberFilter, true, searchInCollections, visited))
                        yield return nested;
            }
        }

        private static int IndexOf<T>(IEnumerable<T> e, Func<T, int, bool> match, bool reverseSearch = false)
        {
            if (e == null)
                return -1;

            // Если исходная коллекция - массив или IList<T>, используем индексацию
            if (e is IList<T> list)
            {
                if (!reverseSearch)
                {
                    for (var i = 0; i < list.Count; i++)
                        if (match(list[i], i))
                            return i;
                }
                else
                {
                    for (var i = list.Count - 1; i >= 0; i--)
                        if (match(list[i], i))
                            return i;
                }

                return -1;
            }

            // Для остальных IEnumerable<T>
            if (!reverseSearch)
            {
                var i = 0;
                foreach (var item in e)
                {
                    if (match(item, i))
                        return i;
                    i++;
                }
            }
            else
            {
                // К сожалению, для IEnumerable<T> без индексации придётся материализовать в список
                var arr = e.ToArray();
                for (var i = arr.Length - 1; i >= 0; i--)
                    if (match(arr[i], i))
                        return i;
            }

            return -1;
        }

        /// <summary>
        ///     Ключ кеша без строковых аллокаций
        /// </summary>
        private readonly struct CacheKey : IEquatable<CacheKey>
        {
            private readonly string _memberName;
            private readonly Type _resultType;
            private readonly Type _sourceType;
            private readonly Type _targetType;

            public CacheKey(
                Type targetType,
                Type sourceType,
                Type resultType,
                string memberName)
            {
                _targetType = targetType;
                _sourceType = sourceType;
                _resultType = resultType;
                _memberName = memberName;
            }

            public bool Equals(CacheKey other)
            {
                return ReferenceEquals(_targetType, other._targetType)
                       && ReferenceEquals(_sourceType, other._sourceType)
                       && ReferenceEquals(_resultType, other._resultType)
                       && string.Equals(_memberName, other._memberName, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is CacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = _targetType.GetHashCode();
                    hash = (hash * 397) ^ _sourceType.GetHashCode();
                    hash = (hash * 397) ^ _resultType.GetHashCode();
                    hash = (hash * 397) ^ (_memberName != null ? _memberName.GetHashCode() : 0);
                    return hash;
                }
            }
        }
    }
}