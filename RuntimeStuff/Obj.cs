// <copyright file="Obj.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace System
{
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Data;
    using System.Diagnostics;
    using System.Globalization;
    using System.Helpers;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Reflection.Emit;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// v.2026.04.27 (RS) COPY-PASTE READY<br />
    /// Вспомогательный класс для быстрого доступа к свойствам объектов с помощью скомпилированных делегатов.<br />
    /// Позволяет получать и изменять значения свойств по имени без постоянного использования Reflection.<br />
    /// Особенности:
    /// <list type="bullet"><item>
    /// Создает делегаты-геттеры (<see cref="Func{T,Object}" />) и сеттеры (<see cref="Action{T, Object}" />)
    /// для указанных свойств.
    /// </item><item>
    /// Использует кеширование для повторного использования скомпилированных выражений, что обеспечивает высокую
    /// производительность.
    /// </item><item>
    /// Поддерживает работу как со ссылочными, так и со значимыми типами свойств (boxing выполняется
    /// автоматически).
    /// </item></list>
    /// </summary>
    public static class Obj
    {
        private static readonly string[] DateFormats =
        [
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

            "yyyy-MM-dd HH:mm:ss",
            "dd.MM.yyyy HH:mm:ss",
            "MM/dd/yyyy HH:mm:ss",
            "yyyy/MM/dd HH:mm:ss",
            "dd-MM-yyyy HH:mm:ss",

            "yyyy-MM-dd HH:mm:ss.fff",
            "dd.MM.yyyy HH:mm:ss.fff",
            "MM/dd/yyyy HH:mm:ss.fff",

            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-ddTHH:mm:ssZ",
            "yyyy-MM-ddTHH:mm:ss.fffZ",
            "yyyy-MM-ddTHH:mm:sszzz",
            "yyyy-MM-ddTHH:mm:ss.fffzzz",
            "o", // ISO 8601 Round-trip
            "s", // Sortable

            "HH:mm",
            "HH:mm:ss",
            "HH:mm:ss.fff",
        ];

        /// <summary>
        /// Словарь соответствий интерфейсов и фабрик по умолчанию для их реализации.
        /// </summary>
        private static readonly Dictionary<Type, Func<Type[], object>> DefaultInterfaceMappings =
            new()
            {
                { typeof(IEnumerable<>), args => Activator.CreateInstance(typeof(List<>).MakeGenericType(args)) },
                { typeof(IList<>), args => Activator.CreateInstance(typeof(List<>).MakeGenericType(args)) },
                { typeof(IList), _ => new ArrayList() },
                { typeof(ICollection<>), args => Activator.CreateInstance(typeof(List<>).MakeGenericType(args)) },
                {
                    typeof(IDictionary<,>),
                    args => Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(args))
                },
                { typeof(IDictionary), _ => new Hashtable() },
                { typeof(ISet<>), args => Activator.CreateInstance(typeof(HashSet<>).MakeGenericType(args)) },
            };

        /// <summary>
        /// The fields cache.
        /// </summary>
        private static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, ObjFieldInfo>> FieldsCache =
            new();

        /// <summary>
        /// The ordinal ignore case comparer.
        /// </summary>
        private static readonly StringComparer OrdinalIgnoreCaseComparer = StringComparer.OrdinalIgnoreCase;

        /// <summary>
        /// The properties cache.
        /// </summary>
        private static readonly ConcurrentDictionary<Type, ReadOnlyDictionary<string, ObjPropertyInfo>> PropertiesCache =
            new();

        private static readonly ConcurrentDictionary<Type, ReadOnlyDictionary<string, ObjMemberInfo>> MembersCache =
            new();

        /// <summary>
        /// Универсальный конвертер строки в DateTime?, не зависящий от региональных настроек.
        /// Пытается распарсить дату из строки, используя набор фиксированных форматов. Если не получается, то пытается угадать
        /// формат.
        /// </summary>
        private static readonly Converter<string, DateTime?> StringToDateTimeConverter = s =>
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                return null;
            }

            const DateTimeStyles styles = DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal;

            if (DateTime.TryParseExact(s.Trim(), DateFormats, CultureInfo.InvariantCulture, styles, out var result))
            {
                return result;
            }

            // Пробуем угадать формат:
            if (DateTime.TryParse(s, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out var d))
            {
                return d;
            }

            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out d))
            {
                return d;
            }

            var dateTimeParts = s.Split([' ', 'T'], StringSplitOptions.RemoveEmptyEntries);
            var dateParts = dateTimeParts[0]
                .Split(['.', '\\', '/', '-'], StringSplitOptions.RemoveEmptyEntries);
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
            {
                return new DateTime((int)year, (int)month, (int)day, 0, 0, 0, DateTimeKind.Unspecified);
            }

            if (dateTimeParts[0].Length == 8)
            {
                return new DateTime(
                    (int)Convert.ChangeType(s.Substring(0, 4), typeof(int)),
                    (int)Convert.ChangeType(s.Substring(4, 2), typeof(int)),
                    (int)Convert.ChangeType(s.Substring(6, 2), typeof(int)),
                    0,
                    0,
                    0,
                    DateTimeKind.Unspecified);
            }

            return null;
        };

        /// <summary>
        /// Хранилище пользовательских конвертеров типов. Ключ первого уровня — исходный тип, ключ второго уровня —
        /// целевой тип, значение — функция преобразования.
        /// </summary>
        public static Dictionary<Type, Dictionary<Type, Func<object, object>>> CustomTypeConverters { get; } =
            [];


        ///// <summary>
        ///// Кеш делегатов для получения значений полей.
        ///// </summary>
        ///// <value>Делегат для получения значений полей.</value>
        //public static ConcurrentDictionary<FieldInfo, Func<object, object>> FieldGetterCache { get; } =
        //    new ConcurrentDictionary<FieldInfo, Func<object, object>>();

        ///// <summary>
        ///// Кеш делегатов для установки значений полей.
        ///// </summary>
        ///// <value>Делегат для установки значений полей.</value>
        //public static ConcurrentDictionary<FieldInfo, Action<object, object>> FieldSetterCache { get; } =
        //    new ConcurrentDictionary<FieldInfo, Action<object, object>>();


        /// <summary>
        /// Кеш делегатов для получения значений свойств.
        /// </summary>
        /// <value>Делегат для получения значений свойств.</value>
        public static ConcurrentDictionary<PropertyInfo, Func<object, object>> PropertyGetterCache { get; } =
            new ConcurrentDictionary<PropertyInfo, Func<object, object>>();

        /// <summary>
        /// Кеш делегатов для установки значений свойств.
        /// </summary>
        /// <value>Делегат для установки значений свойств.</value>
        public static ConcurrentDictionary<PropertyInfo, Action<object, object>> PropertySetterCache { get; } =
            new ConcurrentDictionary<PropertyInfo, Action<object, object>>();

        private static ConcurrentDictionary<ConstructorInfo, Func<object[], object>> CtorCache { get; } =
                    new ConcurrentDictionary<ConstructorInfo, Func<object[], object>>();

        /// <summary>
        /// Флаги для поиска членов класса по умолчанию.
        /// </summary>
        /// <value>The default binding flags.</value>
        

        /// <summary>
        /// Регистрирует пользовательский конвертер между двумя типами.
        /// </summary>
        /// <typeparam name="TFrom">Исходный тип.</typeparam>
        /// <typeparam name="TTo">Целевой тип.</typeparam>
        /// <param name="converter">Функция преобразования значения из <typeparamref name="TFrom" />
        /// в <typeparamref name="TTo" />.</param>
        /// <remarks>Если конвертер для указанной пары типов уже существует,
        /// он будет перезаписан.</remarks>
        public static void AddCustomTypeConverter<TFrom, TTo>(Func<TFrom, TTo> converter)
        {
            if (!CustomTypeConverters.TryGetValue(typeof(TFrom), out var typeConverters) || typeConverters == null)
            {
                typeConverters = [];
                CustomTypeConverters[typeof(TFrom)] = typeConverters;
            }

            typeConverters[typeof(TTo)] =
                (arg) => converter((TFrom)arg);
        }

        /// <summary>
        /// Преобразует значение к указанному типу.
        /// </summary>
        /// <param name="value">Значение для преобразования.</param>
        /// <param name="toType">Тип, в который нужно преобразовать.</param>
        /// <param name="formatProvider">Провайдер формата (по умолчанию <see cref="CultureInfo.InvariantCulture" />).</param>
        /// <returns>Преобразованное значение.</returns>
        /// <exception cref="System.Exception">Ошибка преобразования значения '{value}' ({fromType.Name}) в ({toType.Name})!.</exception>
        /// <exception cref="InvalidCastException">Если преобразование невозможно.</exception>
        /// <exception cref="FormatException">Если формат значения некорректен.</exception>
        /// <exception cref="ArgumentNullException">Если <paramref name="toType" /> равен null.</exception>
        public static object ChangeType(object value, Type toType, IFormatProvider formatProvider = null)
        {
            if (value == null || (value.Equals(DBNull.Value) && TypeHelper.IsNullable(toType)))
            {
                return null;
            }

            if (toType == typeof(object))
            {
                return value;
            }

            formatProvider ??= CultureInfo.InvariantCulture;

            toType = Nullable.GetUnderlyingType(toType) ?? toType;

            var fromType = value.GetType();

            // Быстрый возврат
            if (fromType == toType || toType.IsAssignableFrom(fromType))
            {
                return value;
            }

            try
            {
                var customConverter = GetCustomTypeConverter(fromType, toType);
                if (customConverter != null)
                {
                    return customConverter(value);
                }

                // Преобразование в строку
                if (toType == typeof(string))
                {
                    return string.Format(formatProvider, "{0}", value);
                }

                // ENUM
                if (toType.IsEnum)
                {
                    if (value is string es)
                    {
                        return Enum.Parse(toType, es, true);
                    }

                    if (value is bool b)
                    {
                        return Enum.ToObject(toType, b ? 1 : 0);
                    }

                    if (TypeHelper.IsNumeric(fromType))
                    {
                        return Enum.ToObject(toType, Convert.ToInt32(value, CultureInfo.InvariantCulture));
                    }
                }

                // Преобразование строк
                if (value is string s)
                {
                    if (toType == typeof(bool))
                    {
                        if (bool.TryParse(s, out var boolResult))
                        {
                            return boolResult;
                        }

                        if (s == "1")
                        {
                            return true;
                        }

                        if (s == "0")
                        {
                            return false;
                        }
                    }

                    if (string.IsNullOrWhiteSpace(s) && TypeHelper.IsNullable(toType))
                    {
                        return Default(toType);
                    }

                    if (toType == typeof(DateTime))
                    {
                        return StringToDateTimeConverter(s);
                    }

                    if (TypeHelper.IsNumeric(toType))
                    {
                        // сначала пытаемся корректный parse
                        if (decimal.TryParse(s, NumberStyles.Any, formatProvider, out var dec))
                        {
                            return Convert.ChangeType(dec, toType, CultureInfo.InvariantCulture);
                        }

                        // fallback на замену, если формат "1,23"
                        s = s.Replace(",", ".");
                        return Convert.ChangeType(s, toType, CultureInfo.InvariantCulture);
                    }

                    if (toType.IsClass || toType.IsValueType)
                    {
                        return New(toType, s);
                    }
                }

                // SQL Boolean
                if (fromType == typeof(bool) && toType.Name == "SqlBoolean")
                {
                    return Activator.CreateInstance(toType, (bool)value);
                }

                // Универсальное приведение
                return Convert.ChangeType(value, toType, CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                throw new InvalidCastException(
                    $"Ошибка преобразования значения '{value}' ({fromType.Name}) в ({toType.Name})!", ex);
            }
        }

        /// <summary>
        /// Преобразует значение к указанному типу.
        /// </summary>
        /// <typeparam name="T">Тип, в который нужно преобразовать.</typeparam>
        /// <param name="value">Значение для преобразования.</param>
        /// <param name="formatProvider">Провайдер формата (по умолчанию <see cref="CultureInfo.InvariantCulture" />).</param>
        /// <returns>Преобразованное значение.</returns>
        public static T ChangeType<T>(object value, IFormatProvider formatProvider = null) =>
            (T)ChangeType(value, typeof(T), formatProvider);

        /// <summary>
        /// Очищает все внутренние кеши.
        /// </summary>
        public static void ClearCaches()
        {
            //AssemblyTypesCache.Clear();
            CtorCache.Clear();
            //FieldGetterCache.Clear();
            //FieldSetterCache.Clear();
            FieldsCache.Clear();
            PropertiesCache.Clear();
            //MemberInfoCache.Clear();
            //TypeCache.Clear();
        }

        /// <summary>
        /// Применяет конфигурацию к объекту, устанавливая значения его полей и свойств на основе предоставленной коллекции пар «имя члена ? значение».<br/>
        /// </summary>
        /// <param name="instance">Экземпляр объекта.</param>
        /// <param name="config">Коллекция имя-значение. Если значение словарь, вызов продолжается рекурсивно. Если ключ содержит точки, то пытаемся установить значение для дочерних объектов.</param>
        /// <param name="ignoreNullValues">Игнорировать Null значения из конфигурации.</param>
        public static void Configure(object instance, IDictionary config, bool ignoreNullValues = true)
        {
            foreach (DictionaryEntry item in config)
            {
                var key = item.Key;
                var value = item.Value;

                switch (value)
                {
                    case IDictionary dicSection:
                        Configure(instance, dicSection);
                        continue;
                    case IEnumerable<KeyValuePair<string, object>> section:
                        {
                            Configure(instance, section.ToDictionary(k => key + "." + k.Key, v => v.Value));
                            continue;
                        }
                }

                if (Obj.IsNull(value) && ignoreNullValues)
                {
                    continue;
                }

                Set(instance, $"{key}", value);
            }
        }

        /// <summary>
        /// Копирует значения указанных членов из исходного объекта в целевой объект. Поддерживает копирование как между
        /// отдельными объектами, так и между коллекциями объектов.
        /// </summary>
        /// <typeparam name="TSource">Тип исходного объекта, из которого копируются значения. Должен быть ссылочным типом.</typeparam>
        /// <typeparam name="TTarget">Тип целевого объекта, в который копируются значения. Должен быть ссылочным типом.</typeparam>
        /// <param name="source">Исходный объект, значения членов которого будут скопированы. Не может быть равен null.</param>
        /// <param name="target">Целевой объект, в который будут скопированы значения членов. Не может быть равен null.</param>
        /// <param name="memberNames">Массив имен членов, которые необходимо скопировать. Если не указан или пуст, копируются все доступные
        /// свойства исходного объекта.</param>
        /// <remarks>Если оба параметра <paramref name="source" /> и <paramref name="target" />
        /// являются коллекциями (кроме строк), метод копирует значения для каждого соответствующего элемента коллекции.
        /// При необходимости новые элементы добавляются в целевую коллекцию. Копирование выполняется только по
        /// указанным именам членов или по всем свойствам, если имена не заданы.</remarks>
        public static void Copy<TSource, TTarget>(TSource source, TTarget target, params string[] memberNames)
            where TSource : class
            where TTarget : class
        {
            if (source == null || typeof(TSource) == typeof(string))
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (target == null || typeof(TTarget) == typeof(string))
            {
                throw new ArgumentNullException(nameof(target));
            }

            var sourceType = GetType(source);
            IEnumerable<string> names = memberNames;

            if (names == null || !names.Any())
            {
                names = TypeHelper.GetPublicPropertyNames(sourceType);
            }

            if (TypeHelper.IsCollection(sourceType))
            {
                sourceType = TypeHelper.GetCollectionItemType(sourceType);
            }

            var targetType = GetType(target);
            if (TypeHelper.IsCollection(targetType))
            {
                targetType = TypeHelper.GetCollectionItemType(targetType);
            }

            if (source is IEnumerable srcList && source is not string && target is IEnumerable dstList &&
                target is not string)
            {
                var srcEnumerator = srcList.GetEnumerator();
                var dstEnumerator = dstList.GetEnumerator();
                var dstListChanged = false;
                while (srcEnumerator.MoveNext())
                {
                    var srcItem = srcEnumerator.Current;
                    object dstItem;

                    if (!dstListChanged && dstEnumerator.MoveNext())
                    {
                        dstItem = dstEnumerator.Current;
                    }
                    else
                    {
                        dstItem = New(sourceType);
                        if (dstList is IList dstIList)
                        {
                            dstListChanged = true;
                            dstIList.Add(dstItem);
                        }
                        else
                        {
                            throw new InvalidOperationException(
                                @"Целевая коллекция не реализует IList и не поддерживает добавление новых элементов.");
                        }
                    }

                    Copy(srcItem, dstItem);
                }

                if (srcEnumerator is IDisposable disposableSrc)
                {
                    disposableSrc.Dispose();
                }

                if (dstEnumerator is IDisposable disposableDst)
                {
                    disposableDst.Dispose();
                }
            }
            else
            {
                foreach (var memberName in names)
                {
                    var get = GetMemberGetter(sourceType, memberName);
                    if (get == null)
                    {
                        continue;
                    }

                    var set = GetMemberSetter(targetType, memberName, out _);
                    if (set == null)
                    {
                        continue;
                    }

                    var value = get(source);
                    set(target, value);
                }
            }
        }








        /// <summary>
        /// Creates the field getter.
        /// </summary>
        /// <param name="fi">The fi.</param>
        /// <returns>Func&lt;System.Object, System.Object&gt;.</returns>
        /// <exception cref="System.ArgumentNullException">fi.</exception>
        /// <exception cref="System.ArgumentException">Field has no declaring type - fi.</exception>
        /// <exception cref="System.InvalidOperationException">Failed to create field getter for field '{fi?.DeclaringType?.Name}.{fi?.Name}': {ex.Message}.</exception>


        /// <summary>
        /// Creates the field setter.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <returns>Action&lt;System.Object, System.Object&gt;.</returns>
        /// <exception cref="System.ArgumentNullException">field.</exception>


        /// <summary>
        /// Creates the property setter.
        /// </summary>
        /// <param name="pi">The pi.</param>
        /// <returns>Action&lt;System.Object, System.Object&gt;.</returns>


        /// <summary>
        /// Возвращает значение по умолчанию для указанного типа.
        /// </summary>
        /// <param name="type">Тип, для которого нужно получить значение по умолчанию.</param>
        /// <returns>Значение по умолчанию для указанного типа.</returns>
        public static object Default(Type type) => type?.IsValueType == true ? Activator.CreateInstance(type) : null;

        /// <summary>
        /// Находит конструктор указанного типа, параметры которого совместимы
        /// с переданным набором аргументов.
        /// </summary>
        /// <param name="type">Тип, в котором требуется найти подходящий конструктор.</param>
        /// <param name="args">Массив аргументов, по типам которых выполняется поиск конструктора.
        /// Если элемент массива равен <c>null</c>, считается, что его тип — <see cref="object" />.</param>
        /// <returns>Экземпляр <see cref="ConstructorInfo" />, представляющий первый найденный
        /// конструктор, параметры которого по количеству и типам совместимы
        /// с переданными аргументами.</returns>
        /// <exception cref="InvalidOperationException">Выбрасывается, если подходящий конструктор не найден.</exception>
        public static ConstructorInfo FindConstructor(Type type, object[] args)
        {
            var argTypes = args.Select(a => a?.GetType() ?? typeof(object)).ToArray();

            return type.GetConstructors()
                .FirstOrDefault(c =>
                {
                    var ps = c.GetParameters();
                    if (ps.Length != argTypes.Length)
                    {
                        return false;
                    }

                    for (var i = 0; i < ps.Length; i++)
                    {
                        if (!ps[i].ParameterType.IsAssignableFrom(argTypes[i]))
                        {
                            return false;
                        }
                    }

                    return true;
                });
        }

        /// <summary>
        /// Возвращает значение поля или свойства объекта по имени члена.<br/>
        /// Если указанный член не найден, метод пытается интерпретировать имя как путь к вложенному члену, разделённому точками, слэшами или обратными слэшами, например, "Address.Street.Name" или "Address/Street/Name".<br/>
        /// Для максимальной производительности рекомендуется использовать кэширование делегатов доступа к членам, например, с помощью метода <see cref="GetMemberGetter(Type, string)"/>.<br/>
        /// Если член не найден или объект равен <see langword="null" />, возвращается <see langword="null" />.<br/>
        /// Если указано, возвращаемое значение приводится к заданному типу.
        /// </summary>
        /// <param name="instance">Экземпляр объекта, из которого требуется получить значение.</param>
        /// <param name="memberName">Имя поля или свойства.</param>
        /// <param name="convertToType">Тип, в который требуется преобразовать значение.
        /// Если не задан, возвращается исходное значение.</param>
        /// <returns>Значение поля или свойства, приведённое к указанному типу,
        /// либо <see langword="null" />, если объект равен <see langword="null" />
        /// или член не найден.</returns>
        public static object Get(object instance, string memberName, Type convertToType = null)
        {
            if (instance == null)
            {
                return null;
            }

            var getter = GetMemberGetter(instance.GetType(), memberName);
            if (getter == null)
            {
                var path = memberName.Split('.', '/', '\\');
                if (path.Length > 1)
                {
                    return Get(instance, path, convertToType);
                }

                return null;
            }

            var memberValue = getter(instance);
            return convertToType == null
                ? memberValue
                : ChangeType(memberValue, convertToType);
        }

        /// <summary>
        /// Получает значение вложенного поля или свойства объекта по указанному пути к члену.
        /// </summary>
        /// <param name="instance">Экземпляр объекта, из которого требуется получить значение.</param>
        /// <param name="pathToMemberName">Последовательность имён членов, описывающая путь
        /// к конечному полю или свойству.</param>
        /// <param name="convertToType">Тип, к которому необходимо привести полученное значение.
        /// Если равен <see langword="null" />, преобразование не выполняется.</param>
        /// <returns>Значение конечного члена объекта, приведённое к указанному типу,
        /// либо <see langword="null" />, если объект равен <see langword="null" />,
        /// путь некорректен или один из промежуточных членов имеет значение <see langword="null" />.</returns>
        /// <remarks>Метод поддерживает рекурсивный доступ к вложенным членам.
        /// Если на любом этапе пути значение равно <see langword="null" />,
        /// дальнейший обход прекращается и возвращается <see langword="null" />.</remarks>
        public static object Get(object instance, IEnumerable<string> pathToMemberName, Type convertToType = null)
        {
            if (instance == null)
            {
                return null;
            }

            var path = pathToMemberName as string[] ?? [.. pathToMemberName];

            if (path.Length == 1)
            {
                return Get(instance, path[0], convertToType);
            }

            var getter = GetMemberGetter(instance.GetType(), path[0]);
            var memberValue = getter?.Invoke(instance);

            return memberValue == null
                ? null
                : Get(memberValue, [.. path.Skip(1)], convertToType);
        }

        /// <summary>
        /// Возвращает значение поля или свойства объекта по имени члена, приведённое к указанному типу.
        /// Если указанный член не найден, метод пытается интерпретировать имя как путь к вложенному члену, разделённому точками, слэшами или обратными слэшами, например, "Address.Street.Name" или "Address/Street/Name".<br/>
        /// Для максимальной производительности рекомендуется использовать кэширование делегатов доступа к членам, например, с помощью метода <see cref="GetMemberGetter(Type, string)"/>.<br/>
        /// Если член не найден или объект равен <see langword="null" />, возвращается <see langword="null" />.<br/>
        /// Если указано, возвращаемое значение приводится к заданному типу.
        /// </summary>
        /// <typeparam name="T">Тип возвращаемого значения.</typeparam>
        /// <param name="instance">Экземпляр объекта, из которого требуется получить значение.</param>
        /// <param name="pathToMemberName">Путь к полю или свойству.</param>
        /// <returns>Значение поля или свойства, приведённое к типу <typeparamref name="T" />.</returns>
        public static T Get<T>(object instance, IEnumerable<string> pathToMemberName) =>
            (T)Get(instance, pathToMemberName, typeof(T));

        /// <summary>
        /// Возвращает значение поля или свойства объекта по имени члена, приведённое к указанному типу.
        /// Если указанный член не найден, метод пытается интерпретировать имя как путь к вложенному члену, разделённому точками, слэшами или обратными слэшами, например, "Address.Street.Name" или "Address/Street/Name".<br/>
        /// Для максимальной производительности рекомендуется использовать кэширование делегатов доступа к членам, например, с помощью метода <see cref="GetMemberGetter(Type, string)"/>.<br/>
        /// Если член не найден или объект равен <see langword="null" />, возвращается <see langword="null" />.<br/>
        /// Если указано, возвращаемое значение приводится к заданному типу.
        /// </summary>
        /// <typeparam name="T">Тип возвращаемого значения.</typeparam>
        /// <param name="instance">Экземпляр объекта, из которого требуется получить значение.</param>
        /// <param name="memberName">Имя поля или свойства.</param>
        /// <returns>Значение поля или свойства, приведённое к типу <typeparamref name="T" />.</returns>
        public static T Get<T>(object instance, string memberName) => (T)Get(instance, memberName, typeof(T));

        /// <summary>
        /// Ищет и возвращает первый пользовательский атрибут по имени типа на указанном <see cref="MemberInfo" />.
        /// Метод сравнивает имя типа атрибута с заданным значением <paramref name="attributeName" /> с использованием
        /// указанного <paramref name="stringComparison" />.
        /// Удобен для случаев, когда тип атрибута известен только по имени (например, при работе с внешними библиотеками или
        /// динамическими сценариями).
        /// </summary>
        /// <param name="member">Член, на котором производится поиск атрибута.</param>
        /// <param name="attributeName">Имя типа атрибута для поиска (например, "KeyAttribute").</param>
        /// <param name="stringComparison">Способ сравнения строк для имени атрибута. По умолчанию
        /// <see cref="StringComparison.OrdinalIgnoreCase" />.</param>
        /// <returns>Первый найденный экземпляр <see cref="Attribute" />, либо <c>null</c>, если атрибут не найден.</returns>
        public static Attribute GetCustomAttribute(MemberInfo member, string attributeName, StringComparison stringComparison = StringComparison.OrdinalIgnoreCase)
        {
            var trimAttributeName = !attributeName.ToLower().EndsWith("attribute");
            var memberAttributes = member.GetCustomAttributes();
            foreach (var a in memberAttributes)
            {
                var aName = a.GetType().Name;
                if (trimAttributeName)
                {
                    aName = aName.Substring(0, aName.Length - 9);
                }

                if (attributeName.Equals(aName, stringComparison))
                {
                    return a;
                }
            }

            return null;
        }

        /// <summary>
        /// Возвращает пользовательский конвертер типов в строго типизированном виде.
        /// </summary>
        /// <typeparam name="TFrom">Исходный тип.</typeparam>
        /// <typeparam name="TTo">Целевой тип.</typeparam>
        /// <returns>Функция преобразования из <typeparamref name="TFrom" /> в <typeparamref name="TTo" />,
        /// либо <see langword="null" />, если конвертер не зарегистрирован.</returns>
        public static Func<TFrom, TTo> GetCustomTypeConverter<TFrom, TTo>() =>
            (from) => (TTo)GetCustomTypeConverter(typeof(TFrom), typeof(TTo))(from);

        /// <summary>
        /// Возвращает пользовательский конвертер между двумя типами.
        /// </summary>
        /// <param name="typeFrom">Исходный тип.</param>
        /// <param name="typeTo">Целевой тип.</param>
        /// <returns>Функция преобразования значения,
        /// либо <see langword="null" />, если конвертер не найден.</returns>
        /// <remarks>Возвращаемая функция принимает и возвращает значения типа
        /// <see cref="object" /> и требует явного приведения типов.</remarks>
        public static Func<object, object> GetCustomTypeConverter(Type typeFrom, Type typeTo)
        {
            if (!CustomTypeConverters.TryGetValue(typeFrom, out var typeConverters) || typeConverters == null)
            {
                return null;
            }

            if (!typeConverters.TryGetValue(typeTo, out var converter) || converter == null)
            {
                return null;
            }

            return converter;
        }

        /// <summary>
        /// Возвращает тип реализации по умолчанию для заданного интерфейса.
        /// </summary>
        /// <param name="type">Тип интерфейса, для которого необходимо получить реализацию.</param>
        /// <returns>Если <paramref name="type" /> не является интерфейсом, возвращает сам <paramref name="type" />.
        /// Для известных generic-интерфейсов (<see cref="IEnumerable{T}" />, <see cref="IList{T}" />,
        /// <see cref="ICollection{T}" />, <see cref="IDictionary{TKey, TValue}" />) возвращает соответствующий конкретный тип:
        /// <list type="bullet"><item><description><see cref="IEnumerable{T}" /> ? <see cref="List{T}" /></description></item><item><description><see cref="IList{T}" /> ? <see cref="List{T}" /></description></item><item><description><see cref="ICollection{T}" /> ? <see cref="List{T}" /></description></item><item><description><see cref="IDictionary{TKey, TValue}" /> ? <see cref="Dictionary{TKey, TValue}" /></description></item></list></returns>
        /// <exception cref="System.InvalidOperationException">Cannot create an instance of interface {type}.</exception>
        /// <remarks>Метод использует словарь <see cref="DefaultInterfaceMappings" /> для поиска фабрик конкретных реализаций.
        /// Если тип не найден в словаре, метод пытается обработать известные generic-интерфейсы вручную.</remarks>
        public static Type GetDefaultImplementation(Type type)
        {
            if (!type.IsInterface)
            {
                return type;
            }

            if (type.IsGenericType)
            {
                var genericDef = type.GetGenericTypeDefinition();
                if (DefaultInterfaceMappings.TryGetValue(genericDef, out var factory))
                {
                    return factory(type.GetGenericArguments()).GetType();
                }
            }

            throw new InvalidOperationException($"Cannot create an instance of interface {type}");
        }

        /// <summary>
        /// Получает метод с наименьшего уровня иерархии.
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
                {
                    return member;
                }

                type = type.BaseType;
            }

            return null;
        }

        /// <summary>
        /// Получает свойство с наименьшего уровня иерархии.
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
                {
                    return member;
                }

                type = type.BaseType;
            }

            return null;
        }

        /// <summary>
        /// Возвращает тип значения, который возвращает указанный член типа.
        /// </summary>
        /// <param name="memberInfo">
        /// Метаданные члена типа (<see cref="PropertyInfo"/> или <see cref="FieldInfo"/>),
        /// для которого требуется определить возвращаемый тип.
        /// </param>
        /// <returns>
        /// Тип значения члена:
        /// <list type="bullet">
        /// <item>
        /// <description><see cref="PropertyInfo.PropertyType"/> — если передан объект <see cref="PropertyInfo"/>.</description>
        /// </item>
        /// <item>
        /// <description><see cref="FieldInfo.FieldType"/> — если передан объект <see cref="FieldInfo"/>.</description>
        /// </item>
        /// <item>
        /// <description><see cref="MethodInfo.ReturnType"/> — если передан объект <see cref="MethodInfo"/>.</description>
        /// </item>
        /// </list>
        /// <para>
        /// Возвращает <c>null</c>, если <paramref name="memberInfo"/> равен <c>null</c>
        /// либо если тип члена не поддерживается.
        /// </para>
        /// </returns>
        public static Type GetMemberReturnType(MemberInfo memberInfo)
        {
            if (memberInfo == null)
            {
                return null;
            }

            return memberInfo switch
            {
                PropertyInfo pi => pi.PropertyType,
                FieldInfo fi => fi.FieldType,
                MethodInfo mi => mi.ReturnType,
                _ => null,
            };
        }

        /// <summary>
        /// Возвращает делегат для установки значения поля или свойства.
        /// </summary>
        /// <param name="memberInfo">
        /// Информация о члене типа, для которого требуется получить сеттер.
        /// Поддерживаются поля (<see cref="FieldInfo"/>) и свойства (<see cref="PropertyInfo"/>).
        /// </param>
        /// <returns>
        /// Делегат вида <c>Action&lt;object, object&gt;</c>, принимающий:
        /// <list type="bullet">
        /// <item><description>Экземпляр объекта (или <c>null</c> для статических членов);</description></item>
        /// <item><description>Значение, которое необходимо установить.</description></item>
        /// </list>
        ///
        /// Если переданный член не является полем или свойством,
        /// возвращается <c>null</c>.
        /// </returns>
        /// <remarks>
        /// Для повышения производительности используются внутренние кэши
        /// делегатов сеттеров.
        ///
        /// Создание делегата обычно выполняется с применением выражений
        /// (<see cref="System.Linq.Expressions"/>) или динамической генерации кода,
        /// что значительно быстрее прямого использования Reflection при повторных вызовах.
        ///
        /// Метод не выполняет проверку доступности члена (например, <c>private</c>)
        /// и не гарантирует успешную установку значения при несовпадении типов.
        /// </remarks>
        public static Action<object, object> GetMemberSetter(MemberInfo memberInfo)
        {
            return memberInfo switch
            {
                FieldInfo fi => FieldSetterCache.GetOrAdd(fi, CreateFieldSetter),
                PropertyInfo pi => PropertySetterCache.GetOrAdd(pi, CreatePropertySetter(pi)),
                _ => null,
            };
        }

        /// <summary>
        /// Возвращает делегат, позволяющий установить значение указанного поля или свойства объекта типа по имени члена.
        /// </summary>
        /// <param name="type">Тип в котором искать свойство или поле.</param>
        /// <param name="memberName">Имя поля или свойства, значение которого необходимо установить. Не чувствительно к регистру.</param>
        /// <returns>Делегат Action{object, object}, который устанавливает значение указанного члена для объекта.
        /// Возвращает <see langword="null" />, если член с заданным именем не найден или
        /// не поддерживает установку значения.</returns>
        /// <remarks>Если указанный член является только для чтения или не существует, возвращаемое
        /// значение будет <see langword="null" />. Делегат использует отражение и может иметь меньшую производительность
        /// по сравнению с прямым доступом. Не рекомендуется использовать для часто вызываемых операций.</remarks>
        public static Action<object, object> GetMemberSetter(Type type, string memberName)
        {
            return GetMemberSetter(type, memberName, out _);
        }

        /// <summary>
        /// Возвращает делегат, позволяющий установить значение указанного поля или свойства объекта типа по имени члена.
        /// </summary>
        /// <param name="type">Тип в котором искать свойство или поле.</param>
        /// <param name="memberName">Имя поля или свойства, значение которого необходимо установить. Не чувствительно к регистру.</param>
        /// <param name="memberType">Тип свойства или поля.</param>
        /// <returns>Делегат Action{object, object}, который устанавливает значение указанного члена для объекта.
        /// Возвращает <see langword="null" />, если член с заданным именем не найден или
        /// не поддерживает установку значения.</returns>
        /// <remarks>Если указанный член является только для чтения или не существует, возвращаемое
        /// значение будет <see langword="null" />. Делегат использует отражение и может иметь меньшую производительность
        /// по сравнению с прямым доступом. Не рекомендуется использовать для часто вызываемых операций.</remarks>
        public static Action<object, object> GetMemberSetter(Type type, string memberName, out Type memberType)
        {
            var member = FindMember(type, memberName);
            switch (member)
            {
                case FieldInfo fi:
                    memberType = fi.FieldType;
                    return FieldSetterCache.GetOrAdd(fi, CreateFieldSetter);

                case PropertyInfo pi:
                    memberType = pi.PropertyType;
                    return PropertySetterCache.GetOrAdd(pi, CreatePropertySetter(pi));
            }

            memberType = null;
            return GetMemberSetter(member);
        }

        /// <summary>
        /// Возвращает делегат, позволяющий установить значение указанного поля или свойства объекта типа по имени члена.
        /// </summary>
        /// <typeparam name="T">Type.</typeparam>
        /// <param name="memberName">Имя поля или свойства, значение которого необходимо установить. Не чувствительно к регистру.</param>
        /// <returns>Делегат, который устанавливает значение указанного члена для объекта
        /// типа <typeparamref name="T" />. Возвращает <see langword="null" />, если член с заданным именем не найден или
        /// не поддерживает установку значения.</returns>
        /// <remarks>Если указанный член является только для чтения или не существует, возвращаемое
        /// значение будет <see langword="null" />. Делегат использует отражение и может иметь меньшую производительность
        /// по сравнению с прямым доступом. Не рекомендуется использовать для часто вызываемых операций.</remarks>
        public static Action<object, object> GetMemberSetter<T>(string memberName) => GetMemberSetter<T>(memberName, out _);

        /// <summary>
        /// Возвращает делегат, позволяющий установить значение указанного поля или свойства объекта типа по имени члена.
        /// </summary>
        /// <typeparam name="T">Type.</typeparam>
        /// <param name="memberName">Имя поля или свойства, значение которого необходимо установить. Не чувствительно к регистру.</param>
        /// <param name="memberType">Тип свойства или поля.</param>
        /// <returns>Делегат, который устанавливает значение указанного члена для объекта
        /// типа <typeparamref name="T" />. Возвращает <see langword="null" />, если член с заданным именем не найден или
        /// не поддерживает установку значения.</returns>
        /// <remarks>Если указанный член является только для чтения или не существует, возвращаемое
        /// значение будет <see langword="null" />. Делегат использует отражение и может иметь меньшую производительность
        /// по сравнению с прямым доступом. Не рекомендуется использовать для часто вызываемых операций.</remarks>
        public static Action<object, object> GetMemberSetter<T>(string memberName, out Type memberType) =>
            GetMemberSetter(typeof(T), memberName, out memberType);

        /// <summary>
        /// Возвращает отображение имён свойств указанного типа на объекты <see cref="PropertyInfo" />.
        /// </summary>
        /// <param name="type">Тип, свойства которого требуется получить.</param>
        /// <returns>Словарь «имя свойства ? FieldInfo».</returns>
        public static IReadOnlyDictionary<string, ObjPropertyInfo> GetPropertiesMap(Type type)
        {
            return PropertiesCache.GetOrAdd(type, CacheTypeProperties);
        }

        /// <summary>
        /// Получает все свойства указанного типа, которые имеют определённый тип данных.
        /// </summary>
        /// <param name="type">Тип в котором искать свойства.</param>
        /// <param name="propertyType">Тип значения свойства.</param>
        /// <returns>Свойства указанного типа.</returns>
        public static IEnumerable<ObjPropertyInfo> GetPropertiesOfType(Type type, Type propertyType)
        {
            return PropertiesCache.GetOrAdd(type, CacheTypeProperties).Values.Where(p => p.PropertyInfo.PropertyType == propertyType);
        }

        /// <summary>
        /// Определяет фактический тип переданного объекта с учетом <see cref="Nullable{T}"/>.
        /// </summary>
        /// <param name="obj">
        /// Объект или экземпляр <see cref="Type"/>.
        /// Может быть <c>null</c>.
        /// </param>
        /// <returns>
        /// Тип объекта без обертки <see cref="Nullable{T}"/>, если она присутствует.
        /// Если <paramref name="obj"/> равен <c>null</c>, возвращается <c>null</c>.
        /// </returns>
        /// <remarks>
        /// Если <paramref name="obj"/> уже является типом (<see cref="Type"/>),
        /// он используется напрямую. В противном случае вызывается <see cref="object.GetType"/>.
        /// Для nullable-типов возвращается базовый тип (например, для <c>int?</c> будет возвращен <c>int</c>).
        /// </remarks>
        public static Type GetType(object obj)
        {
            var type = obj as Type ?? obj?.GetType();
            if (type == null)
            {
                return null;
            }

            type = Nullable.GetUnderlyingType(type) ?? type;
            return type;
        }

        /// <summary>
        /// Получает значения всех публичных свойств объекта в виде словаря.
        /// </summary>
        /// <typeparam name="TObject">Тип исходного объекта.</typeparam>
        /// <param name="source">Объект, из которого извлекаются значения свойств.</param>
        /// <returns>
        /// Словарь, где ключом является имя свойства, а значением — его текущее значение.
        /// Если <paramref name="source"/> равен <see langword="null"/>,
        /// возвращается пустой словарь.
        /// </returns>
        public static Dictionary<string, object> GetValues<TObject>(TObject source)
            where TObject : class
        {
            var dic = new Dictionary<string, object>();
            if (source == null)
            {
                return dic;
            }

            if (source is IEnumerable e)
            {
                var i = 0;
                foreach (var x in e)
                {
                    dic[$"{i}"] = x;
                    i++;
                }

                return dic;
            }

            var sourceType = GetType(source);
            var memberNames = TypeHelper.GetPublicPropertyNames(sourceType);
            if (!memberNames.Any())
            {
                memberNames = TypeHelper.GetPublicFieldNames(sourceType);
            }

            if (!memberNames.Any())
            {
                return dic;
            }

            var values = GetValues<TObject>(source, memberNames);
            var j = 0;
            foreach (var name in memberNames)
            {
                dic[name] = values[j++];
            }

            return dic;
        }

        /// <summary>
        /// Получает значения указанных свойств объекта.
        /// </summary>
        /// <typeparam name="TObject">Тип исходного объекта.</typeparam>
        /// <param name="source">Объект, из которого извлекаются значения.</param>
        /// <param name="memberNames">
        /// Имена свойств, значения которых необходимо получить.
        /// Если не указаны, будут использованы все публичные свойства.
        /// </param>
        /// <returns>
        /// Массив значений свойств в порядке их выбора.
        /// </returns>
        public static object[] GetValues<TObject>(TObject source, IEnumerable<string> memberNames)
            where TObject : class
        {
            if (source == null)
            {
                return [];
            }

            var values = new List<object>();
            var sourceType = GetType(source);
            if (memberNames == null || !memberNames.Any())
            {
                memberNames = TypeHelper.GetPublicPropertyNames(sourceType);
            }

            foreach (var propName in memberNames)
            {
                values.Add(GetMemberGetter(sourceType, propName)?.Invoke(source));
            }

            return [.. values];
        }

        /// <summary>
        /// Получает значения указанных свойств объекта с приведением к заданному типу.
        /// </summary>
        /// <typeparam name="TObject">Тип исходного объекта.</typeparam>
        /// <typeparam name="TValue">Тип, к которому будут приведены значения.</typeparam>
        /// <param name="source">Объект, из которого извлекаются значения.</param>
        /// <param name="memberNames">
        /// Имена свойств, значения которых необходимо получить.
        /// Если не указаны, будут использованы все публичные свойства.
        /// </param>
        /// <returns>
        /// Массив значений свойств, приведённых к типу <typeparamref name="TValue"/>.
        /// </returns>
        /// <remarks>
        /// Для преобразования используется вспомогательный метод <c>Obj.ChangeType&lt;T&gt;</c>.
        /// Если преобразование невозможно, может возникнуть исключение.
        /// </remarks>
        public static TValue[] GetValues<TObject, TValue>(TObject source, params string[] memberNames)
            where TObject : class
            => [.. GetValues(source, memberNames).Select(x => ChangeType<TValue>(x))];

        /// <summary>
        /// Проверяет, является ли переданное значение "null-эквивалентом", то есть одним из следующих: <see cref="NullValues"/>.
        /// </summary>
        /// <param name="value">Проверяемое значение.</param>
        /// <returns>Содержится ли значение в массиве <see cref="TypeHelper.NullValues"/>.</returns>
        public static bool IsNull(object value)
        {
            return TypeHelper.NullValues.Contains(value);
        }

        /// <summary>
        /// Создаёт новый экземпляр типа <typeparamref name="T" />
        /// с использованием заранее сгенерированного делегата конструктора.
        /// </summary>
        /// <typeparam name="T">Тип создаваемого объекта.
        /// Требует наличия конструктора без параметров.</typeparam>
        /// <param name="args">The arguments.</param>
        /// <returns>Новый экземпляр типа <typeparamref name="T" />.</returns>
        /// <exception cref="InvalidOperationException">Выбрасывается, если тип не имеет конструктора по умолчанию.</exception>
        /// <remarks>Метод является быстрым способом создания объектов, так как использует
        /// предварительно скомпилированный делегат конструктора, полученный через IL-генерацию.</remarks>
        public static T New<T>(params object[] args) => (T)New(typeof(T), args);

        /// <summary>
        /// Создает новый экземпляр указанного типа и приводит его к типу <typeparamref name="T" />.
        /// </summary>
        /// <typeparam name="T">Тип, к которому приводится создаваемый объект.</typeparam>
        /// <param name="type">Тип создаваемого объекта. Должен иметь конструктор без параметров.</param>
        /// <returns>Новый экземпляр типа <typeparamref name="T" />.</returns>
        /// <exception cref="InvalidOperationException">Выбрасывается, если тип не имеет конструктора по умолчанию.</exception>
        public static T New<T>(Type type) => (T)New(type);

        /// <summary>
        /// Создаёт новый экземпляр указанного типа, используя конструктор,
        /// соответствующий переданным аргументам.
        /// </summary>
        /// <param name="type">Тип создаваемого объекта.</param>
        /// <param name="args">Аргументы, передаваемые в конструктор.</param>
        /// <returns>Новый экземпляр указанного типа.</returns>
        /// <exception cref="System.InvalidOperationException">No constructor found for type {type}.</exception>
        public static object New(Type type, params object[] args)
        {
            // если интерфейс, подставляем стандартную реализацию
            if (type.IsInterface)
            {
                type = GetDefaultImplementation(type);
            }

            if (type == typeof(string))
            {
                return null;
            }

            var ctor = FindConstructor(type, args) ??
                       throw new InvalidOperationException($"No constructor found for type {type}");
            var factory = CtorCache.GetOrAdd(ctor, CreateFactory);

            return factory(args);
        }

        /// <summary>
        /// Создаёт новый экземпляр элемента, соответствующего типу элементов указанной коллекции.
        /// </summary>
        /// <param name="list">Коллекция, тип элементов которой используется для создания нового экземпляра. Не может быть равна null.</param>
        /// <returns>Новый экземпляр элемента того же типа, что и элементы коллекции <paramref name="list" />.</returns>
        public static object NewItem(IEnumerable list)
        {
            var itemType = list.GetType().GetGenericArguments().FirstOrDefault();
            return itemType == null
                ? throw new InvalidOperationException("Cannot determine item type of the collection.")
                : New(itemType);
        }

        /// <summary>
        /// Устанавливает значение поля или свойства объекта по имени или пути свойства/поля.<br/>
        /// Если в имени присутствует путь к вложенному члену (например, "Address.Street"), метод рекурсивно обрабатывает каждый уровень вложенности.<br/>
        /// Для максимальной производительности рекомендуется использовать полученные делегаты сеттеров напрямую <see cref="GetMemberSetter(MemberInfo)"/> <see cref="GetMemberSetter{T}(string, out Type)"/>, так как этот метод выполняет поиск члена и преобразование типов при каждом вызове.
        /// </summary>
        /// <param name="instance">Экземпляр объекта, в котором требуется установить значение.</param>
        /// <param name="memberName">Имя поля или свойства.</param>
        /// <param name="value">Значение, которое необходимо установить.</param>
        /// <returns><see langword="true" />, если значение успешно установлено;
        /// <see langword="false" />, если объект равен <see langword="null" />,
        /// член не найден или недоступен для записи.</returns>
        public static bool Set(object instance, string memberName, object value)
        {
            if (instance == null)
            {
                return false;
            }

            if (instance is DataRow dataRow)
            {
                if (!dataRow.Table.Columns.Contains(memberName))
                {
                    return false;
                }

                dataRow[memberName] = value ?? DBNull.Value;
                return true;
            }

            if (instance is DataRowView dataRowView)
            {
                if (!dataRowView.DataView.Table.Columns.Contains(memberName))
                {
                    return false;
                }

                dataRowView[memberName] = value ?? DBNull.Value;
                return true;
            }

            var objMap = GetPropertiesMap(instance.GetType());
            Action<object, object> setter = null;
            Type memberType;
            if (objMap.TryGetValue(memberName, out var mapItem))
            {
                setter = mapItem.Setter;
                memberType = mapItem.PropertyInfo.PropertyType;
            }
            else
            {
                setter = GetMemberSetter(instance.GetType(), memberName, out memberType);
            }

            if (setter == null)
            {
                var path = memberName.Split('.', '/', '\\');
                if (path.Length > 1)
                {
                    return Set(instance, path, value);
                }

                return false;
            }

            setter(instance, value?.GetType() == memberType ? value : ChangeType(value, memberType));
            return true;
        }

        /// <summary>
        /// Устанавливает значение вложенного поля или свойства объекта
        /// по указанному пути к члену.
        /// </summary>
        /// <param name="instance">Экземпляр объекта, в котором требуется установить значение.</param>
        /// <param name="pathToMemberName">Последовательность имён членов, описывающая путь
        /// к конечному полю или свойству.</param>
        /// <param name="value">Значение, которое необходимо установить.</param>
        /// <returns><see langword="true" />, если значение успешно установлено;
        /// <see langword="false" />, если объект равен <see langword="null" />,
        /// путь некорректен либо один из членов не найден.</returns>
        /// <remarks>Метод поддерживает установку значений во вложенные члены.
        /// Если промежуточный объект отсутствует (<see langword="null" />),
        /// он будет автоматически создан при возможности.</remarks>
        public static bool Set(object instance, IEnumerable<string> pathToMemberName, object value)
        {
            if (instance == null)
            {
                return false;
            }

            var path = pathToMemberName as string[] ?? [.. pathToMemberName];
            if (path.Length == 1)
            {
                // Конечный элемент пути
                return Set(instance, path[0], value);
            }

            var getter = GetMemberGetter(instance.GetType(), path[0]);
            if (getter == null)
            {
                return false;
            }

            var subMemberInstance = Get(instance, path[0]);
            if (subMemberInstance == null)
            {
                var subMember = FindMember(instance.GetType(), path[0]);
                var subMemberType = GetMemberReturnType(subMember);
                if (subMemberType == null)
                {
                    return false;
                }

                subMemberInstance = New(subMemberType);
                Set(instance, path[0], subMemberInstance);
            }

            return Set(subMemberInstance, [.. path.Skip(1)], value);
        }

        /// <summary>
        /// Устанавливает реализацию по умолчанию для заданного интерфейса.
        /// </summary>
        /// <param name="interfaceType">Тип интерфейса, для которого задаётся реализация.</param>
        /// <param name="implementationType">Тип реализации интерфейса.</param>
        /// <exception cref="System.ArgumentNullException">interfaceType.</exception>
        /// <exception cref="System.ArgumentNullException">implementationType.</exception>
        /// <exception cref="System.ArgumentException">Both types must be generic definitions or both non-generic.</exception>
        /// <remarks>Метод создаёт фабрику для нового типа и заменяет существующее соответствие в <see cref="DefaultInterfaceMappings" />.
        /// Для generic-типов используется метод <see cref="Type.MakeGenericType" />.</remarks>
        public static void SetDefaultImplementation(Type interfaceType, Type implementationType)
        {
            if (interfaceType == null)
            {
                throw new ArgumentNullException(nameof(interfaceType));
            }

            if (implementationType == null)
            {
                throw new ArgumentNullException(nameof(implementationType));
            }

            if (!interfaceType.IsInterface)
            {
                throw new ArgumentException($@"{interfaceType} is not an interface", nameof(interfaceType));
            }

            if (implementationType.IsInterface)
            {
                throw new ArgumentException($@"{implementationType} cannot be an interface", nameof(implementationType));
            }

            // проверка generic-совместимости
            if (interfaceType.IsGenericTypeDefinition != implementationType.IsGenericTypeDefinition)
            {
                throw new ArgumentException("Both types must be generic definitions or both non-generic");
            }

            // создаём фабрику
            object Factory(Type[] genericArgs)
            {
                var targetType = implementationType;
                if (implementationType.IsGenericTypeDefinition)
                {
                    targetType = implementationType.MakeGenericType(genericArgs);
                }

                return Activator.CreateInstance(targetType);
            }

            DefaultInterfaceMappings[interfaceType] = Factory;
        }

        /// <summary>
        /// Пытается добавить элемент в указанную коллекцию.
        /// </summary>
        /// <param name="collection">Коллекция, в которую необходимо добавить элемент.</param>
        /// <param name="item">
        /// Элемент для добавления. Если значение <c>null</c>, будет предпринята попытка
        /// создать новый экземпляр типа элемента коллекции.
        /// </param>
        /// <param name="index">
        /// Индекс, по которому необходимо вставить элемент.
        /// Если значение меньше 0, элемент добавляется в конец коллекции.
        /// </param>
        /// <returns>
        /// Добавленный элемент. Если элемент не был передан, возвращается созданный экземпляр.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Выбрасывается, если <paramref name="collection"/> равен <c>null</c>.
        /// </exception>
        /// <exception cref="Exception">
        /// Выбрасывается, если невозможно определить тип элемента коллекции
        /// для создания нового экземпляра.
        /// </exception>
        public static object TryAdd(object collection, object item = null, int index = -1)
        {
            if (collection == null)
            {
                throw new ArgumentNullException(nameof(collection));
            }

            if (item == null)
            {
                var itemType = collection.GetType().GenericTypeArguments.FirstOrDefault() ??
                               throw new Exception($"{nameof(TryAdd)}: {collection.GetType().FullName}");
                item = New(itemType);
            }

            // Проверяем, поддерживает ли коллекция добавление
            if (collection is IList list)
            {
                if (index == -1)
                {
                    list.Add(item);
                }
                else
                {
                    list.Insert(index, item);
                }
            }
            else if (collection is IList<object> genericList)
            {
                if (index == -1)
                {
                    genericList.Add(item);
                }
                else
                {
                    genericList.Insert(index, item);
                }
            }
            else
            {
                throw new InvalidOperationException("Коллекция не поддерживает добавление элементов.");
            }

            return item;
        }

        /// <summary>
        /// Преобразует указанный объект в тип. Если преобразование невозможно, возвращает
        /// значение по умолчанию.
        /// </summary>
        /// <typeparam name="T">Тип, в который требуется выполнить преобразование.</typeparam>
        /// <param name="value">Объект, который необходимо преобразовать.</param>
        /// <param name="defaultValue">Значение, возвращаемое в случае неудачного преобразования. По умолчанию используется значение по умолчанию
        /// для типа <typeparamref name="T" />.</param>
        /// <param name="formatProvider">Объект, предоставляющий сведения о форматировании, используемые при преобразовании. Может быть равен null.</param>
        /// <returns>Значение типа <typeparamref name="T" />, полученное в результате успешного преобразования, либо <paramref name="defaultValue" />, если преобразование не удалось.</returns>
        /// <remarks>Метод не выбрасывает исключения при неудачном преобразовании, а возвращает указанное
        /// значение по умолчанию. Это может быть полезно для безопасного преобразования типов без необходимости
        /// обработки исключений.</remarks>
        public static T TryChangeType<T>(object value, T defaultValue = default, IFormatProvider formatProvider = null)
        {
            try
            {
                return ChangeType<T>(value, formatProvider);
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Пытается преобразовать заданное значение к указанному типу T.
        /// </summary>
        /// <typeparam name="T">Тип, к которому требуется выполнить преобразование.</typeparam>
        /// <param name="value">Значение, которое требуется преобразовать.</param>
        /// <param name="result">Если преобразование выполнено успешно, содержит результат преобразования; в противном случае содержит
        /// значение по умолчанию для типа T.</param>
        /// <param name="formatProvider">Объект, предоставляющий сведения о форматировании, используемые при преобразовании. Может быть null для
        /// использования форматирования по умолчанию.</param>
        /// <returns>Значение <see langword="true" />, если преобразование прошло успешно; в противном случае — <see langword="false" />.</returns>
        /// <remarks>Метод не выбрасывает исключения при неудачном преобразовании. Используйте этот метод,
        /// если не требуется обработка исключений при ошибке преобразования.</remarks>
        public static bool TryChangeType<T>(object value, out T result, IFormatProvider formatProvider = null)
        {
            try
            {
                result = ChangeType<T>(value, formatProvider);
                return true;
            }
            catch
            {
                result = default;
                return false;
            }
        }

        /// <summary>
        /// Создаёт делегат для получения значения свойства.
        /// Делегат создается динамически с помощью <see cref="DynamicMethod"/> и IL-кода, что позволяет обходить ограничения обычного рефлексивного вызова.
        /// </summary>
        /// <param name="pi">Метаданные свойства (<see cref="PropertyInfo"/>), для которого создается геттер.</param>
        /// <returns>
        /// Делегат <see cref="Func{Object, Object}"/>, который возвращает значение указанного свойства.
        /// Если свойство не имеет метода get, возвращается <c>null</c>.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Метод поддерживает как статические, так и нестатические свойства, а также свойства value-типа и ссылочного типа.
        /// </para>
        /// <para>
        /// Для value-типа аргумент должен быть упакованным объектом (boxed value type).
        /// В случае попытки передачи <c>null</c> для value-типа будет выброшено <see cref="NullReferenceException"/>.
        /// </para>
        /// <para>
        /// Для ссылочных типов, если объект не совместим с ожидаемым типом, будет выброшено <see cref="InvalidCastException"/>.
        /// </para>
        /// </remarks>
        internal static Func<object, object> CreatePropertyGetter(PropertyInfo pi)
        {
            return CreatePropertyGetter<object, object>(pi);
        }

        /// <summary>
        /// Создаёт делегат для получения значения свойства <typeparamref name="TProperty"/> объекта <typeparamref name="TObject"/>.
        /// Делегат создается динамически с помощью <see cref="DynamicMethod"/> и IL-кода, что позволяет обходить ограничения обычного рефлексивного вызова.
        /// </summary>
        /// <typeparam name="TObject">Тип объекта, содержащего свойство.</typeparam>
        /// <typeparam name="TProperty">Тип значения свойства.</typeparam>
        /// <param name="pi">Метаданные свойства (<see cref="PropertyInfo"/>), для которого создается геттер.</param>
        /// <returns>
        /// Делегат <see cref="Func{TObject, TProperty}"/>, который возвращает значение указанного свойства.
        /// Если свойство не имеет метода get, возвращается <c>null</c>.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Метод поддерживает как статические, так и нестатические свойства, а также свойства value-типа и ссылочного типа.
        /// </para>
        /// <para>
        /// Для value-типа аргумент <typeparamref name="TObject"/> должен быть упакованным объектом (boxed value type).
        /// В случае попытки передачи <c>null</c> для value-типа будет выброшено <see cref="NullReferenceException"/>.
        /// </para>
        /// <para>
        /// Для ссылочных типов, если объект не совместим с ожидаемым типом, будет выброшено <see cref="InvalidCastException"/>.
        /// </para>
        /// </remarks>
        internal static Func<TObject, TProperty> CreatePropertyGetter<TObject, TProperty>(PropertyInfo pi)
        {
            var getter = pi.GetGetMethod(true);
            if (getter == null)
            {
                return null;
            }

            var declaring = pi.DeclaringType;
            var propertyType = pi.PropertyType;

            if (declaring == null)
            {
                throw new ArgumentException("Property must have a declaring type", nameof(pi));
            }

            var dm = new DynamicMethod(
                "get_" + pi.Name,
                typeof(TObject),
                [typeof(TProperty)],
                declaring.Module,
                true);

            var il = dm.GetILGenerator();

            // Для статических методов
            if (getter.IsStatic)
            {
                il.Emit(System.Reflection.Emit.OpCodes.Call, getter);
                if (propertyType.IsValueType && !propertyType.IsPrimitive)
                {
                    il.Emit(System.Reflection.Emit.OpCodes.Box, propertyType);
                }

                il.Emit(System.Reflection.Emit.OpCodes.Ret);
                return (Func<TObject, TProperty>)dm.CreateDelegate(typeof(Func<TObject, TProperty>));
            }

            // Для нестатических методов
            if (!declaring.IsValueType)
            {
                // Для ссылочных типов
                var lblOk = il.DefineLabel();

                il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
                il.Emit(System.Reflection.Emit.OpCodes.Isinst, declaring);
                il.Emit(System.Reflection.Emit.OpCodes.Brtrue_S, lblOk);

                il.Emit(System.Reflection.Emit.OpCodes.Newobj, typeof(InvalidCastException).GetConstructor(Type.EmptyTypes) ?? throw new InvalidOperationException());
                il.Emit(System.Reflection.Emit.OpCodes.Throw);

                il.MarkLabel(lblOk);
                il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
                il.Emit(System.Reflection.Emit.OpCodes.Castclass, declaring);
                il.Emit(System.Reflection.Emit.OpCodes.Callvirt, getter);
            }
            else
            {
                // Для value types
                // Создаем локальную переменную для хранения распакованной структуры
                var local = il.DeclareLocal(declaring);

                // Загружаем аргумент (упакованную структуру)
                il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);

                // Проверяем, что это не null (для упакованных структур)
                var lblNotNull = il.DefineLabel();
                il.Emit(System.Reflection.Emit.OpCodes.Dup);
                il.Emit(System.Reflection.Emit.OpCodes.Brtrue_S, lblNotNull);

                // Если null, выбрасываем исключение
                il.Emit(System.Reflection.Emit.OpCodes.Newobj, typeof(NullReferenceException).GetConstructor(Type.EmptyTypes) ?? throw new InvalidOperationException());
                il.Emit(System.Reflection.Emit.OpCodes.Throw);

                il.MarkLabel(lblNotNull);

                // Распаковываем структуру
                il.Emit(System.Reflection.Emit.OpCodes.Unbox_Any, declaring);

                // Сохраняем в локальную переменную
                il.Emit(System.Reflection.Emit.OpCodes.Stloc, local);

                // Загружаем адрес локальной переменной (для вызова метода структуры)
                il.Emit(System.Reflection.Emit.OpCodes.Ldloca_S, local);

                // Вызываем getter
                il.Emit(System.Reflection.Emit.OpCodes.Call, getter);
            }

            // Бокс возвращаемого значения, если это value type
            if (propertyType.IsValueType)
            {
                il.Emit(System.Reflection.Emit.OpCodes.Box, propertyType);
            }

            il.Emit(System.Reflection.Emit.OpCodes.Ret);

            return (Func<TObject, TProperty>)dm.CreateDelegate(typeof(Func<TObject, TProperty>));
        }

        private static ReadOnlyDictionary<string, ObjPropertyInfo> CacheTypeProperties(Type type)
        {
            var properties = type.GetProperties(DefaultBindingFlags)
                .DistinctBy(x => x.Name)
                .Where(x => x.GetIndexParameters().Length == 0)
                .ToDictionary((x, i) => x.Name, (v, i) => new ObjPropertyInfo(v, CreatePropertySetter(v), CreatePropertyGetter(v), i));

            var result = new ReadOnlyDictionary<string, ObjPropertyInfo>(properties);
            PropertiesCache[type] = result;
            return result;
        }

        /// <summary>
        /// Finds the field by naming patterns.
        /// </summary>
        /// <param name="declaringType">Type of the declaring.</param>
        /// <param name="propertyName">Name of the property.</param>
        /// <returns>FieldInfo.</returns>


        /// <summary>
        /// the backing field from il.
        /// </summary>
        /// <param name="getter">The getter.</param>
        /// <returns>FieldInfo.</returns>


        /// <summary>
        /// the size of the operand.
        /// </summary>
        /// <param name="operandType">Type of the operand.</param>
        /// <param name="ilBytes">The il bytes.</param>
        /// <param name="position">The position.</param>
        /// <returns>System.Int32.</returns>


        /// <summary>
        /// Indexes the of.
        /// </summary>
        /// <typeparam name="T">Type.</typeparam>
        /// <param name="e">The e.</param>
        /// <param name="match">The match.</param>
        /// <param name="reverseSearch">if set to <c>true</c> [reverse search].</param>
        /// <returns>System.Int32.</returns>
        private static int IndexOf<T>(IEnumerable<T> e, Func<T, int, bool> match, bool reverseSearch = false)
        {
            if (e == null)
            {
                return -1;
            }

            // Если исходная коллекция - массив или IList<T>, используем индексацию
            if (e is IList<T> list)
            {
                if (!reverseSearch)
                {
                    for (var i = 0; i < list.Count; i++)
                    {
                        if (match(list[i], i))
                        {
                            return i;
                        }
                    }
                }
                else
                {
                    for (var i = list.Count - 1; i >= 0; i--)
                    {
                        if (match(list[i], i))
                        {
                            return i;
                        }
                    }
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
                    {
                        return i;
                    }

                    i++;
                }
            }
            else
            {
                // К сожалению, для IEnumerable<T> без индексации придётся материализовать в список
                var arr = e.ToArray();
                for (var i = arr.Length - 1; i >= 0; i--)
                {
                    if (match(arr[i], i))
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        /// <summary>
        /// Кеш для информации о свойствах, включая делегаты доступа.
        /// </summary>
        public sealed class ObjPropertyInfo : ObjMemberInfo
        {
            /// <summary>
            /// Initializes a new instance of the ObjPropertyInfo class with the specified property metadata and
            /// accessor delegates.
            /// </summary>
            /// <param name="propertyInfo">The PropertyInfo object that describes the property to be accessed. Cannot be null.</param>
            /// <param name="setter">A delegate used to set the value of the property. Cannot be null.</param>
            /// <param name="getter">A delegate used to get the value of the property. Cannot be null.</param>
            /// <param name="index">Порядковый номер.</param>
            internal ObjPropertyInfo(PropertyInfo propertyInfo, Action<object, object> setter, Func<object, object> getter, int index)
                : base(propertyInfo, setter, getter, index)
            {
            }

            /// <summary>
            /// Информация о свойстве.
            /// </summary>
            public PropertyInfo PropertyInfo => (PropertyInfo)this.MemberInfo;
        }

        /// <summary>
        /// Кеш для информации о полях, включая делегаты доступа.
        /// </summary>
        public sealed class ObjFieldInfo : ObjMemberInfo
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="ObjFieldInfo"/> class.
            /// </summary>
            /// <param name="fieldInfo">FieldInfo.</param>
            /// <param name="setter">Setter.</param>
            /// <param name="getter">Getter.</param>
            /// <param name="index">Index.</param>
            internal ObjFieldInfo(FieldInfo fieldInfo, Action<object, object> setter, Func<object, object> getter, int index)
                : base(fieldInfo, setter, getter, index)
            {
            }

            /// <summary>
            /// Информация о поле.
            /// </summary>
            public FieldInfo FieldInfo => (FieldInfo)this.MemberInfo;
        }

        /// <summary>
        /// Кеш для информации о полях, включая делегаты доступа.
        /// </summary>
        public class ObjMemberInfo(
            MemberInfo memberInfo,
            Action<object, object> setter,
            Func<object, object> getter,
            int index)
        {
            /// <summary>
            /// Информация о поле.
            /// </summary>
            public MemberInfo MemberInfo { get; } = memberInfo;

            /// <summary>
            /// Делегат для получения значения поля. Принимает экземпляр объекта и возвращает значение поля.
            /// </summary>
            public Func<object, object> Getter { get; } = getter;

            /// <summary>
            /// Делегат для установки значения поля.
            /// </summary>
            public Action<object, object> Setter { get; } = setter;

            /// <summary>
            /// Порядковый номер поля.
            /// </summary>
            public int Index { get; } = index;
        }
    }
}