using RuntimeStuff.Extensions;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;

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
        ///     Флаги для поиска членов класса по умолчанию
        /// </summary>
        public static BindingFlags DefaultBindingFlags { get; set; } = BindingFlags.Instance | BindingFlags.NonPublic |
                                                                       BindingFlags.Public | BindingFlags.Static;

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

        private static readonly ConcurrentDictionary<(Type, string, Type), object> GettersCache = new ConcurrentDictionary<(Type, string, Type), object>();
        private static readonly ConcurrentDictionary<(Type, string, Type), object> SettersCache = new ConcurrentDictionary<(Type, string, Type), object>();

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

        public static void Copy(object source, object dest)
        {
            if (source == null || dest == null)
                throw new ArgumentNullException("Исходный или целевой объект не может быть null");

            var sourceType = source.GetType();
            var destType = dest.GetType();

            // Получаем все публичные свойства исходного объекта
            var sourceProperties = GetProperties(sourceType);

            foreach (var prop in sourceProperties)
            {
                // Ищем соответствующее свойство в целевом объекте
                var destProp = GetProperty(destType, prop.Name);

                if (destProp != null)
                {
                    try
                    {
                        // Получаем значение из исходного объекта
                        var value = GetValue(source, prop.Name);
                        var convertedValue = ChangeType(value, destProp.PropertyType);
                        SetPropertyValue(dest, prop.Name, convertedValue);
                    }
                    catch (Exception ex)
                    {
                        // Обработка ошибок для каждого свойства
                        throw new Exception($"Ошибка копирования свойства {prop.Name}: {ex.Message}");
                    }
                }
            }
        }


        public static object DeepCopy(object source, object dest)
        {
            if (source == null || dest == null)
                throw new ArgumentNullException("Исходный или целевой объект не может быть null");

            var sourceType = source.GetType();
            var destType = dest.GetType();

            // Получаем все публичные свойства исходного объекта
            var sourceProperties = GetProperties(sourceType);

            foreach (var prop in sourceProperties)
            {
                // Ищем соответствующее свойство в целевом объекте
                var destProp = GetProperty(destType, prop.Name);

                if (destProp != null)
                {
                    try
                    {
                        // Получаем значение из исходного объекта
                        var sourceValue = GetValue(source, prop.Name);

                        // Проверяем тип свойства
                        if (IsBasic(prop.PropertyType))
                        {
                            // Для базовых типов выполняем простое копирование
                            SetPropertyValue(dest, prop.Name, sourceValue);
                        }
                        else if (IsCollection(prop.PropertyType))
                        {
                            DeepCopyCollection(sourceValue);
                        }
                        else
                        {
                            // Для сложных объектов выполняем глубокое копирование
                            var destValue = GetValue(dest, prop.Name);

                            if (destValue == null)
                            {
                                // Если значение null, создаем новый объект
                                destValue = Activator.CreateInstance(prop.PropertyType);
                                SetPropertyValue(dest, prop.Name, destValue);
                            }

                            // Рекурсивно копируем объект
                            DeepCopy(sourceValue, destValue);
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Ошибка глубокого копирования свойства {prop.Name}: {ex.Message}");
                    }
                }

                void DeepCopyCollection(object value)
                {
                    // Для коллекций выполняем глубокое копирование
                    var itemType = GetCollectionItemType(prop.PropertyType);
                    var sourceCollection = (IList)value;
                    var destCollection = (IList)GetValue(dest, prop.Name);

                    // Очищаем целевую коллекцию
                    destCollection.Clear();

                    foreach (var item in sourceCollection)
                    {
                        // Создаем новый элемент коллекции
                        var newItem = Activator.CreateInstance(itemType);

                        // Рекурсивно копируем элемент
                        DeepCopy(item, newItem);

                        // Добавляем в целевую коллекцию
                        destCollection.Add(newItem);
                    }
                }
            }

            return dest;
        }

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
        ///     Использует внутренний кеш для ускорения повторных вызовов.
        /// </summary>
        /// <typeparam name="T">Тип, для которого нужно получить свойства.</typeparam>
        /// <returns>Массив <see cref="PropertyInfo" /> всех публичных свойств.</returns>
        public static PropertyInfo[] GetProperties<T>() where T : class
        {
            return GetProperties(typeof(T));
        }

        public static Dictionary<string, PropertyInfo> GetPropertiesMap<T>()
        {
            return GetPropertiesMap(typeof(T));
        }

        public static Dictionary<string, ConstructorInfo> GetConstructorsMap(Type type)
        {
            var key = (type, "constructors", typeof(ConstructorInfo)); //$"{type.FullName}.constructors.public";

            if (GettersCache.TryGetValue(key, out var cached))
                return ((Dictionary<string, ConstructorInfo>)cached);

            var typeCtors = type.GetConstructors();
            var dic = new Dictionary<string, ConstructorInfo>();
            foreach (var ctor in typeCtors)
                dic[ctor.Name] = ctor;
            GettersCache[key] = dic;
            return dic;
        }

        public static Dictionary<string, PropertyInfo> GetPropertiesMap(Type type)
        {
            var key = (type, "properties", typeof(PropertyInfo)); //$"{type.FullName}.properties.public";

            if (GettersCache.TryGetValue(key, out var cached))
                return ((Dictionary<string, PropertyInfo>)cached);

            var typeProperties = type.GetProperties();
            var dic = new Dictionary<string, PropertyInfo>();
            foreach (var prop in typeProperties)
                dic[prop.Name] = prop;
            GettersCache[key] = dic;
            return dic;
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
        ///     Получить свойство по его имени
        /// </summary>
        /// <param name="type">Тип в котором искать свойство</param>
        /// <param name="propertyName">Имя свойства</param>
        /// <param name="stringComparison">Сравнение имен</param>
        /// <returns></returns>
        public static PropertyInfo GetProperty(Type type, string propertyName, StringComparison stringComparison = StringComparison.OrdinalIgnoreCase)
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
            var key = (type, "property-names", typeof(string)); //$"{type.FullName}.property-names.{type.FullName}:object";

            if (GettersCache.TryGetValue(key, out var cached))
                return (string[])cached;

            var typePropertyNames = GetProperties(type).Select(x => x.Name).ToArray();
            GettersCache[key] = typePropertyNames;
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
        /// var value = PropertyHelper.GetValue(person, "Name"); // "Alice"
        /// </code>
        /// </summary>
        public static object GetValue(object source, string propertyName, Type tryConvertToType = null, bool throwIfSourceIsNull = true)
        {
            if (source == null)
                if (throwIfSourceIsNull)
                    throw new ArgumentNullException(nameof(source));
                else
                    return null;

            var getter = Getter(propertyName, source.GetType());
            return  tryConvertToType != null
                ? ChangeType(getter(source), tryConvertToType)
                : getter(source);
        }

        public static MemberInfo FindMember(Type type, string name, bool ignoreCase = false, BindingFlags? bindingFlags = null)
        {
            var flags = bindingFlags ?? DefaultBindingFlags;
            if (ignoreCase)
                flags |= BindingFlags.IgnoreCase;

            // Ищем property
            var prop = type.GetProperty(name, flags);

            if (prop != null)
                return prop;

            // Ищем field
            var field = type.GetField(name, flags);

            if (field != null)
                return field;

            // Ищем среди интерфейсов
            var interfaces = type.GetInterfaces();
            foreach (var i in interfaces)
            {
                var p = i.GetProperty(name);
                if (p != null)
                    return p;
            }

            // Ищем среди методов
            var method = type.GetMethod(name, flags);
            if (method != null) return method;

            // Рекурсия по BaseType
            if (type.BaseType != null)
                return FindMember(type.BaseType, name);

            return null;
        }

        /// <summary>
        ///     Возвращает значение свойства с типизацией результата.<br/>
        ///     Создает и кэширует делегат для быстрого доступа к свойству по имени.<br/>
        ///     Особенности:<br/>
        ///     - Имя свойства реегистронезависимо.<br/>
        ///     - Позволяет получить значение свойства с приведением к нужному типу (Тип должен быть совместимым! Автоматической конвертации типов не происходит!).<br/>
        ///     - Использует кэширование делегатов для повышения производительности.<br/>
        ///     - Генерирует исключение, если свойство не найдено или несовместимо по типу.<br/>
        ///     Пример:
        ///     <code>
        /// var person = new Person { Age = 42 };
        /// int age = PropertyHelper.GetValue&lt;Person, int&gt;(person, "Age"); // 42
        /// </code>
        /// </summary>
        public static TReturn GetValue<TReturn>(object source, string propertyName, bool tryConvert = false, bool ignoreCase = false, bool throwIfSourceIsNull = true)
        {
            if (source == null)
                if (throwIfSourceIsNull)
                    throw new ArgumentNullException(nameof(source));
                else
                    return default;
            if (!tryConvert)
            {
                var getter = Getter<object, TReturn>(propertyName, source.GetType(), ignoreCase);
                return getter(source);
            }
            else
            {
                var getter = Getter<object, object>(propertyName, source.GetType(), ignoreCase);
                return ChangeType<TReturn>(getter(source));
            }
        }

        public static TReturn GetValue<TSource, TReturn>(TSource source, string propertyName, bool tryConvert = false, bool ignoreCase = false, bool throwIfSourceIsNull = true)
        {
            if (source == null)
                if (throwIfSourceIsNull)
                    throw new ArgumentNullException(nameof(source));
                else
                    return default;
            if (!tryConvert)
            {
                var getter = Getter<TSource, TReturn>(propertyName, source.GetType(), ignoreCase);
                return getter(source);
            }
            else
            {
                var getter = Getter<TSource, object>(propertyName, source.GetType(), ignoreCase);
                return ChangeType<TReturn>(getter(source));
            }
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
        public static object[] GetPropertyValues<TObject>(TObject source, params string[] propertyNames) where TObject : class
        {
            var values = new List<object>();
            foreach (var property in propertyNames)
            {
                var getter = Getter<TObject>(property, source.GetType());
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

        public static Func<T, TResult> Getter<T, TResult>(string memberName, Type sourceType = null, bool ignoreCase = false)
        {
            var type = sourceType ?? typeof(T);

            var key = (type, memberName, typeof(TResult));

            if (GettersCache.TryGetValue(key, out var cached))
                return (Func<T, TResult>)cached;

            var bindingFlags =
                BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static;

            MemberInfo memberInfo = FindMember(type, memberName, ignoreCase)
                                    ?? FindMember(type, memberName, ignoreCase, bindingFlags);

            if (memberInfo == null)
                throw new MissingMemberException(type.FullName, memberName);

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

        public static Func<T, object> Getter<T>(string memberName, Type sourceType = null, bool ignoreCase = false)
        {
            return Getter<T, object>(memberName, sourceType, ignoreCase);
        }

        public static Func<object, object> Getter(string memberName, Type sourceType = null, bool ignoreCase = false)
        {
            return Getter<object, object>(memberName, sourceType, ignoreCase);
        }

        public static Action<T, TValue> Setter<T, TValue>(string memberName, Type sourceType = null, bool ignoreCase = false)
        {
            var type = sourceType ?? typeof(T);

            var key = (type, memberName, typeof(TValue));

            if (SettersCache.TryGetValue(key, out var cached))
                return (Action<T, TValue>)cached;

            var bindingFlags =
                BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static;

            MemberInfo memberInfo = FindMember(type, memberName, ignoreCase)
                                    ?? FindMember(type, memberName, ignoreCase, bindingFlags);

            if (memberInfo == null)
                throw new MissingMemberException(type.FullName, memberName);

            Action<T, TValue> action;

            switch (memberInfo)
            {
                case PropertyInfo pi:
                    action = !pi.CanWrite ? CreateFieldSetter<T, TValue>(GetPropertyBackingFieldInfo(pi)) : CreatePropertySetter<T, TValue>(pi);
                    break;

                case FieldInfo fi:
                    action = CreateFieldSetter<T, TValue>(fi);
                    break;

                default:
                    throw new NotSupportedException($"Setter не поддерживается для члена: {memberInfo.MemberType}");
            }

            SettersCache[key] = action;
            return action;
        }

        public static FieldInfo GetPropertyBackingFieldInfo(PropertyInfo pi)
        {
            // Для автосвойств компилятор создает поле с именем <PropertyName>k__BackingField
            var backingFieldName = $"<{pi.Name}>k__BackingField";
            var fi = pi.DeclaringType?.GetField(backingFieldName,DefaultBindingFlags) ?? GetFieldInfoFromGetAccessor(pi.GetGetMethod(true));
            return fi;
        }

        private static FieldInfo GetFieldInfoFromGetAccessor(MethodInfo getMethod)
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

        public static Func<T, TResult> CreateFieldGetter<T, TResult>(FieldInfo fi)
        {
            var dm = new DynamicMethod(
                "get_" + fi.Name,
                typeof(TResult),
                new[] { typeof(T) },
                typeof(T),                  // разрешения на private поля
                true                        // skipVisibility
            );

            var il = dm.GetILGenerator();

            if (fi.IsStatic)
            {
                // Static: просто загружаем значение
                il.Emit(OpCodes.Ldsfld, fi);
            }
            else
            {
                // Instance: загружаем аргумент 0 (x)
                il.Emit(OpCodes.Ldarg_0);

                // Если тип T — value type, надо разыменовать
                if (typeof(T).IsValueType)
                    il.Emit(OpCodes.Unbox_Any, typeof(T));

                // Загружаем поле x.field
                il.Emit(OpCodes.Ldfld, fi);
            }

            // Если типы отличаются — кастуем
            if (fi.FieldType != typeof(TResult))
                il.Emit(OpCodes.Castclass, typeof(TResult));

            il.Emit(OpCodes.Ret);

            return (Func<T, TResult>)dm.CreateDelegate(typeof(Func<T, TResult>));
        }

        public static Action<T, TValue> CreateFieldSetter<T, TValue>(FieldInfo fi)
        {
            var dm = new DynamicMethod("set_" + fi.Name, typeof(void), new[] { typeof(T), typeof(TValue) }, typeof(T), true);
            var il = dm.GetILGenerator();

            if (fi.IsStatic)
            {
                il.Emit(OpCodes.Ldarg_1); // value
                if (typeof(TValue) != fi.FieldType)
                {
                    if (typeof(TValue).IsValueType && fi.FieldType == typeof(object))
                        il.Emit(OpCodes.Box, typeof(TValue));
                    else
                        il.Emit(OpCodes.Castclass, fi.FieldType);
                }

                il.Emit(OpCodes.Stsfld, fi);
                il.Emit(OpCodes.Ret);
            }
            else
            {
                il.Emit(OpCodes.Ldarg_0); // instance
                if (typeof(T).IsValueType)
                    il.Emit(OpCodes.Unbox_Any, typeof(T));

                il.Emit(OpCodes.Ldarg_1); // value
                if (typeof(TValue) != fi.FieldType)
                {
                    if (typeof(TValue).IsValueType && fi.FieldType == typeof(object))
                        il.Emit(OpCodes.Box, fi.FieldType);
                    else
                        il.Emit(OpCodes.Castclass, fi.FieldType);
                }

                il.Emit(OpCodes.Stfld, fi);
                il.Emit(OpCodes.Ret);
            }

            return (Action<T, TValue>)dm.CreateDelegate(typeof(Action<T, TValue>));
        }


        private static Func<T, TResult> CreatePropertyGetter<T, TResult>(PropertyInfo pi)
        {
            if (pi == null)
                throw new ArgumentNullException(nameof(pi));

            var getMethod = pi.GetGetMethod(true)
                          ?? throw new InvalidOperationException(
                                $"Свойство '{pi.Name}' не имеет геттера.");

            // Проверяем совместимость типов
            if (!typeof(TResult).IsAssignableFrom(pi.PropertyType) &&
                pi.PropertyType.IsValueType && typeof(TResult) != typeof(object))
                throw new InvalidOperationException(
                    $"Невозможно преобразовать {pi.PropertyType} в {typeof(TResult)}");

            // Static getter → сигнатура: TResult ()
            if (getMethod.IsStatic)
            {
                var dm = new DynamicMethod(
                    "get_" + pi.Name,
                    typeof(TResult),
                    new[] { typeof(T) },
                    typeof(T),
                    true);

                var il = dm.GetILGenerator();

                // CALL static getter
                il.Emit(OpCodes.Call, getMethod);

                // Convert to TResult if needed
                if (pi.PropertyType != typeof(TResult))
                    il.Emit(OpCodes.Castclass, typeof(TResult));

                il.Emit(OpCodes.Ret);

                return (Func<T, TResult>)dm.CreateDelegate(typeof(Func<T, TResult>));
            }

            // Instance getter → сигнатура: TResult (T instance)
            {
                var dm = new DynamicMethod(
                    "get_" + pi.Name,
                    typeof(TResult),
                    new[] { typeof(T) },
                    typeof(T),
                    true);

                var il = dm.GetILGenerator();

                // ARG0: x (T)
                il.Emit(OpCodes.Ldarg_0);

                // Если T — value type → распаковать
                if (typeof(T).IsValueType)
                    il.Emit(OpCodes.Unbox_Any, typeof(T));

                // CALL instance getter
                il.Emit(OpCodes.Callvirt, getMethod);

                // Convert to TResult
                if (pi.PropertyType != typeof(TResult))
                {
                    if (pi.PropertyType.IsValueType && typeof(TResult) == typeof(object))
                    {
                        // boxing
                        il.Emit(OpCodes.Box, pi.PropertyType);
                    }
                    else
                    {
                        il.Emit(OpCodes.Castclass, typeof(TResult));
                    }
                }

                il.Emit(OpCodes.Ret);

                return (Func<T, TResult>)dm.CreateDelegate(typeof(Func<T, TResult>));
            }
        }

        public static Action<T, TValue> CreatePropertySetter<T, TValue>(PropertyInfo pi)
        {
            var setMethod = pi.GetSetMethod(true)
                            ?? throw new InvalidOperationException($"Свойство '{pi.Name}' не имеет сеттера.");

            if (!typeof(TValue).IsAssignableFrom(pi.PropertyType) &&
                pi.PropertyType.IsValueType && typeof(TValue) != typeof(object))
                throw new InvalidOperationException(
                    $"Невозможно присвоить {typeof(TValue)} в {pi.PropertyType}");

            if (setMethod.IsStatic)
            {
                var dm = new DynamicMethod("set_" + pi.Name, typeof(void), new[] { typeof(T), typeof(TValue) }, typeof(T), true);
                var il = dm.GetILGenerator();

                // Для static: загружаем аргумент 1 (value)
                il.Emit(OpCodes.Ldarg_1);

                // Преобразуем тип, если нужно
                if (typeof(TValue) != pi.PropertyType)
                {
                    if (typeof(TValue).IsValueType && pi.PropertyType == typeof(object))
                        il.Emit(OpCodes.Box, typeof(TValue));
                    else
                        il.Emit(OpCodes.Castclass, pi.PropertyType);
                }

                il.Emit(OpCodes.Call, setMethod);
                il.Emit(OpCodes.Ret);

                return (Action<T, TValue>)dm.CreateDelegate(typeof(Action<T, TValue>));
            }
            else
            {
                var dm = new DynamicMethod("set_" + pi.Name, typeof(void), new[] { typeof(T), typeof(TValue) }, typeof(T), true);
                var il = dm.GetILGenerator();

                il.Emit(OpCodes.Ldarg_0); // instance
                if (typeof(T).IsValueType)
                    il.Emit(OpCodes.Unbox_Any, typeof(T));

                il.Emit(OpCodes.Ldarg_1); // value
                if (typeof(TValue) != pi.PropertyType)
                {
                    if (typeof(TValue).IsValueType && pi.PropertyType == typeof(object))
                        il.Emit(OpCodes.Box, typeof(TValue));
                    else
                        il.Emit(OpCodes.Castclass, pi.PropertyType);
                }

                il.Emit(OpCodes.Callvirt, setMethod);
                il.Emit(OpCodes.Ret);

                return (Action<T, TValue>)dm.CreateDelegate(typeof(Action<T, TValue>));
            }
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
                    new[] { typeof(T) },    // параметр игнорируется
                    typeof(T),
                    true);

                var il = dm.GetILGenerator();

                // CALL static method
                il.Emit(OpCodes.Call, mi);

                // Преобразование к TResult, если нужно
                if (mi.ReturnType != typeof(TResult))
                {
                    if (mi.ReturnType.IsValueType && typeof(TResult) == typeof(object))
                    {
                        il.Emit(OpCodes.Box, mi.ReturnType);
                    }
                    else
                    {
                        il.Emit(OpCodes.Castclass, typeof(TResult));
                    }
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

        public static Func<object> CreateConstructorGetter(Type type)
        {
            var ctor = type.GetConstructor(Type.EmptyTypes)
                       ?? throw new InvalidOperationException("Нет конструктора по умолчанию");

            var dm = new DynamicMethod("ctor_" + type.Name,
                typeof(object),
                Type.EmptyTypes,
                type,
                true);

            var il = dm.GetILGenerator();
            il.Emit(OpCodes.Newobj, ctor);

            // Если value type — упаковываем
            if (type.IsValueType)
                il.Emit(OpCodes.Box, type);

            il.Emit(OpCodes.Ret);

            return (Func<object>)dm.CreateDelegate(typeof(Func<object>));
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

            // --- Ищем Property ---
            var prop = type.GetProperty(propertyName, DefaultBindingFlags);
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
            var field = type.GetField(propertyName, DefaultBindingFlags);
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
                throw new ArgumentException(@"Выражение должно указывать на свойство.", nameof(propSelector));

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
    }
}