// ***********************************************************************
// Assembly         : RuntimeStuff
// Author           : RS
// Created          : 01-06-2026
//
// Last Modified By : RS
// Last Modified On : 01-07-2026
// ***********************************************************************
// <copyright file="Obj.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************

namespace RuntimeStuff
{
    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Reflection.Emit;
    using System.Runtime.CompilerServices;
    using RuntimeStuff.Extensions;

    /// <summary>
    /// v.2025.12.24 (RS)<br />
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
    /// Пример:
    /// <code>
    /// var getter = PropertyHelper.Getter&lt;Person&gt;("Name");
    /// var setter = PropertyHelper.Setter&lt;Person&gt;("Name");
    /// var p = new Person { Name = "Alice" };
    /// Console.WriteLine(getter(p)); // Alice
    /// setter(p, "Bob");
    /// Console.WriteLine(getter(p)); // Bob
    /// </code>
    /// </summary>
    public static class Obj
    {
        /// <summary>
        /// The date formats.
        /// </summary>
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
            "HH:mm:ss.fff",
        };

        /// <summary>
        /// Словарь соответствий интерфейсов и фабрик по умолчанию для их реализации.
        /// </summary>
        private static readonly Dictionary<Type, Func<Type[], object>> DefaultInterfaceMappings = new Dictionary<Type, Func<Type[], object>>()
        {
            { typeof(IEnumerable<>), args => Activator.CreateInstance(typeof(List<>).MakeGenericType(args)) },
            { typeof(IList<>), args => Activator.CreateInstance(typeof(List<>).MakeGenericType(args)) },
            { typeof(IList), _ => new ArrayList() },
            { typeof(ICollection<>), args => Activator.CreateInstance(typeof(List<>).MakeGenericType(args)) },
            { typeof(IDictionary<,>), args => Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(args)) },
            { typeof(IDictionary), _ => new Hashtable() },
            { typeof(ISet<>), args => Activator.CreateInstance(typeof(HashSet<>).MakeGenericType(args)) },
        };

        /// <summary>
        /// The fields cache.
        /// </summary>
        private static readonly ConcurrentDictionary<Type, Dictionary<string, FieldInfo>> FieldsCache = new ConcurrentDictionary<Type, Dictionary<string, FieldInfo>>();

        /// <summary>
        /// The op codes.
        /// </summary>
        private static readonly Dictionary<short, OpCode> OpCodes = InitializeOpCodes();

        /// <summary>
        /// The ordinal ignore case comparer.
        /// </summary>
        private static readonly StringComparer OrdinalIgnoreCaseComparer = StringComparer.OrdinalIgnoreCase;

        /// <summary>
        /// The properties cache.
        /// </summary>
        private static readonly ConcurrentDictionary<Type, Dictionary<string, PropertyInfo>> PropertiesCache = new ConcurrentDictionary<Type, Dictionary<string, PropertyInfo>>();

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
        /// The type cache.
        /// </summary>
        private static readonly ConcurrentDictionary<string, Type> TypeCache = new ConcurrentDictionary<string, Type>(OrdinalIgnoreCaseComparer);

        /// <summary>
        /// Gets типы, представляющие логические значения.
        /// </summary>
        /// <value>The bool types.</value>
        public static HashSet<Type> BoolTypes { get; } = new HashSet<Type>
        {
            typeof(bool),
            typeof(bool?),
        };

        /// <summary>
        /// Gets типы с плавающей запятой (float, double, decimal).
        /// </summary>
        /// <value>The float number types.</value>
        public static HashSet<Type> FloatNumberTypes { get; } = new HashSet<Type>
        {
            typeof(float), typeof(double), typeof(decimal),
            typeof(float?), typeof(double?), typeof(decimal?),
        };

        /// <summary>
        /// Gets целочисленные типы (byte, int, long и т.д. с nullable и без).
        /// </summary>
        /// <value>The int number types.</value>
        public static HashSet<Type> IntNumberTypes { get; } = new HashSet<Type>
        {
            typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(short), typeof(ushort), typeof(byte),
            typeof(sbyte),
            typeof(int?), typeof(uint?), typeof(long?), typeof(ulong?), typeof(short?), typeof(ushort?),
            typeof(byte?),
            typeof(sbyte?),
        };

        /// <summary>
        /// Gets все числовые типы: целочисленные и с плавающей точкой.
        /// </summary>
        /// <value>The number types.</value>
        public static HashSet<Type> NumberTypes { get; } = new HashSet<Type>(IntNumberTypes.Concat(FloatNumberTypes));

        /// <summary>
        /// Gets набор основных типов: числа, логические, строки, даты, Guid, Enum и др.
        /// </summary>
        /// <value>The basic types.</value>
        public static Type[] BasicTypes { get; } = new Type[]
        {
            typeof(object),
            typeof(char), typeof(char?), typeof(string),
            typeof(DateTime), typeof(DateTime?), typeof(TimeSpan),
            typeof(Guid), typeof(Guid?),
            typeof(Uri),
            typeof(Enum),
        }.Concat(NumberTypes).Concat(BoolTypes).ToArray();

        /// <summary>
        /// Gets хранилище пользовательских конвертеров типов. Ключ первого уровня — исходный тип, ключ второго уровня —
        /// целевой тип, значение — функция преобразования.
        /// </summary>
        public static Dictionary<Type, Dictionary<Type, Func<object, object>>> CustomTypeConverters { get; } = new Dictionary<Type, Dictionary<Type, Func<object, object>>>();

        /// <summary>
        /// Gets типы, представляющие дату и время.
        /// </summary>
        /// <value>The date types.</value>
        public static HashSet<Type> DateTypes { get; } = new HashSet<Type>
        {
            typeof(DateTime), typeof(DateTime?),
        };

        /// <summary>
        /// Gets флаги для поиска членов класса по умолчанию.
        /// </summary>
        /// <value>The default binding flags.</value>
#pragma warning disable S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields
        public static BindingFlags DefaultBindingFlags { get; } = BindingFlags.Instance | BindingFlags.NonPublic |
#pragma warning restore S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields
                                                                  BindingFlags.Public | BindingFlags.Static;

        /// <summary>
        /// Gets the field getter cache.
        /// </summary>
        /// <value>The field getter cache.</value>
        public static ConcurrentDictionary<FieldInfo, Func<object, object>> FieldGetterCache { get; } = new ConcurrentDictionary<FieldInfo, Func<object, object>>();

        /// <summary>
        /// Gets the field setter cache.
        /// </summary>
        /// <value>The field setter cache.</value>
        public static ConcurrentDictionary<FieldInfo, Action<object, object>> FieldSetterCache { get; } = new ConcurrentDictionary<FieldInfo, Action<object, object>>();

        /// <summary>
        /// Gets the member information cache.
        /// </summary>
        /// <value>The member information cache.</value>
        public static ConcurrentDictionary<string, MemberInfo> MemberInfoCache { get; } = new ConcurrentDictionary<string, MemberInfo>();

        /// <summary>
        /// Gets значения, трактуемые как null (null, DBNull, NaN).
        /// </summary>
        /// <value>The null values.</value>
        public static object[] NullValues { get; } = new object[] { null, DBNull.Value, double.NaN, float.NaN };

        /// <summary>
        /// Gets the property getter cache.
        /// </summary>
        /// <value>The property getter cache.</value>
        public static ConcurrentDictionary<PropertyInfo, Func<object, object>> PropertyGetterCache { get; } = new ConcurrentDictionary<PropertyInfo, Func<object, object>>();

        /// <summary>
        /// Gets the property setter cache.
        /// </summary>
        /// <value>The property setter cache.</value>
        public static ConcurrentDictionary<PropertyInfo, Action<object, object>> PropertySetterCache { get; } = new ConcurrentDictionary<PropertyInfo, Action<object, object>>();

        /// <summary>
        /// Gets the ctor cache.
        /// </summary>
        private static ConcurrentDictionary<ConstructorInfo, Func<object[], object>> CtorCache { get; } = new ConcurrentDictionary<ConstructorInfo, Func<object[], object>>();

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
                typeConverters = new Dictionary<Type, Func<object, object>>();
                CustomTypeConverters[typeof(TFrom)] = typeConverters;
            }

            typeConverters[typeof(TTo)] =
                converter.ConvertFunc(arg => (TFrom)arg);
        }

        /// <summary>
        /// Определяет, является ли указанный член типа публичным (<c>public</c>).
        /// </summary>
        /// <param name="memberInfo">
        /// Метаданные члена типа, для которого требуется проверить уровень доступа.
        /// Поддерживаются следующие типы:
        /// <see cref="PropertyInfo"/>, <see cref="FieldInfo"/>, <see cref="MethodInfo"/>,
        /// <see cref="EventInfo"/>, <see cref="Type"/>, <see cref="ConstructorInfo"/>.
        /// </param>
        /// <returns>
        /// <c>true</c>, если член типа является публичным;
        /// <c>false</c> — если член не является публичным.
        /// </returns>
        /// <exception cref="NotSupportedException">
        /// Выбрасывается, если тип <paramref name="memberInfo"/> не поддерживается
        /// для проверки модификатора доступа.
        /// </exception>
        /// <remarks>
        /// <para>
        /// Логика определения публичности:
        /// </para>
        /// <list type="bullet">
        /// <item>
        /// <description>
        /// <see cref="PropertyInfo"/> — проверяется наличие хотя бы одного публичного аксессора
        /// (getter или setter).
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// <see cref="FieldInfo"/> — используется свойство <see cref="FieldInfo.IsPublic"/>.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// <see cref="MethodInfo"/> — используется свойство MethodInfo.IsPublic.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// <see cref="EventInfo"/> — проверяется публичность методов добавления или удаления обработчика
        /// (<c>add</c>/<c>remove</c>).
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// <see cref="Type"/> — используется свойство <see cref="Type.IsPublic"/>.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// <see cref="ConstructorInfo"/> — используется свойство ConstructorInfo.IsPublic.
        /// </description>
        /// </item>
        /// </list>
        /// </remarks>
        public static bool IsPublic(MemberInfo memberInfo)
        {
            switch (memberInfo)
            {
                case PropertyInfo pi:
                    return pi.GetAccessors().Any(m => m.IsPublic);

                case FieldInfo fi:
                    return fi.IsPublic;

                case MethodInfo mi:
                    return mi.IsPublic;

                case EventInfo ei:
                    return ei.AddMethod?.IsPublic == true || ei.RemoveMethod?.IsPublic == true;

                case Type t:
                    return t.IsPublic;

                case ConstructorInfo ci:
                    return ci.IsPublic;
            }

            throw new NotSupportedException($"Member type {memberInfo.GetType()} is not supported for IsPublic check.");
        }

        /// <summary>
        /// Определяет, является ли указанный член типа приватным (<c>private</c>).
        /// </summary>
        /// <param name="memberInfo">
        /// Метаданные члена типа, для которого требуется проверить уровень доступа.
        /// Поддерживаются следующие типы:
        /// <see cref="PropertyInfo"/>, <see cref="FieldInfo"/>, <see cref="MethodInfo"/>,
        /// <see cref="EventInfo"/>, <see cref="Type"/>, <see cref="ConstructorInfo"/>.
        /// </param>
        /// <returns>
        /// <c>true</c>, если член типа имеет модификатор доступа <c>private</c>;
        /// <c>false</c> — в противном случае.
        /// </returns>
        /// <exception cref="NotSupportedException">
        /// Выбрасывается, если тип <paramref name="memberInfo"/> не поддерживается
        /// для проверки модификатора доступа.
        /// </exception>
        /// <remarks>
        /// <para>
        /// Логика определения приватности:
        /// </para>
        /// <list type="bullet">
        /// <item>
        /// <description>
        /// <see cref="PropertyInfo"/> — проверяется наличие хотя бы одного приватного аксессора
        /// (getter или setter).
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// <see cref="FieldInfo"/> — используется свойство FieldInfo.IsPrivate.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// <see cref="MethodInfo"/> — используется свойство MethodInfo.IsPrivate.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// <see cref="EventInfo"/> — проверяется приватность методов добавления или удаления обработчика
        /// (<c>add</c>/<c>remove</c>).
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// <see cref="Type"/> — считается приватным, если тип не является публичным
        /// (<see cref="Type.IsPublic"/> равен <c>false</c>).
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// <see cref="ConstructorInfo"/> — используется свойство ConstructorInfo.IsPrivate.
        /// </description>
        /// </item>
        /// </list>
        /// <para>
        /// Обратите внимание, что для вложенных типов приватность также может определяться
        /// через <see cref="Type.IsNestedPrivate"/>.
        /// </para>
        /// </remarks>
        public static bool IsPrivate(MemberInfo memberInfo)
        {
            switch (memberInfo)
            {
                case PropertyInfo pi:
                    return pi.GetAccessors().Any(m => m.IsPrivate);

                case FieldInfo fi:
                    return fi.IsPrivate;

                case MethodInfo mi:
                    return mi.IsPrivate;

                case EventInfo ei:
                    return ei.AddMethod?.IsPrivate == true || ei.RemoveMethod?.IsPrivate == true;

                case Type t:
                    return !t.IsPublic;

                case ConstructorInfo ci:
                    return ci.IsPrivate;
            }

            throw new NotSupportedException($"Member type {memberInfo.GetType()} is not supported for IsPublic check.");
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
            if (value == null || (value.Equals(DBNull.Value) && IsNullable(toType)))
            {
                return null;
            }

            if (toType == typeof(object))
            {
                return value;
            }

            if (formatProvider == null)
            {
                formatProvider = CultureInfo.InvariantCulture;
            }

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

                    if (IsNumeric(fromType))
                    {
                        return Enum.ToObject(toType, Convert.ToInt32(value, CultureInfo.InvariantCulture));
                    }
                }

                // Преобразование строк
                if (value is string s)
                {
                    if (string.IsNullOrWhiteSpace(s) && IsNullable(toType))
                    {
                        return Default(toType);
                    }

                    if (toType == typeof(DateTime))
                    {
                        return StringToDateTimeConverter(s);
                    }

                    if (IsNumeric(toType))
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
                throw new InvalidCastException($"Ошибка преобразования значения '{value}' ({fromType.Name}) в ({toType.Name})!", ex);
            }
        }

        /// <summary>
        /// Преобразует значение к указанному типу.
        /// </summary>
        /// <typeparam name="T">Тип, в который нужно преобразовать.</typeparam>
        /// <param name="value">Значение для преобразования.</param>
        /// <param name="formatProvider">Провайдер формата (по умолчанию <see cref="CultureInfo.InvariantCulture" />).</param>
        /// <returns>Преобразованное значение.</returns>
        public static T ChangeType<T>(object value, IFormatProvider formatProvider = null) => (T)ChangeType(value, typeof(T), formatProvider);

        /// <summary>
        /// Копирует значения указанных членов из исходного объекта в целевой объект. Поддерживает копирование как между
        /// отдельными объектами, так и между коллекциями объектов.
        /// </summary>
        /// <typeparam name="TSource">Тип исходного объекта, из которого копируются значения. Должен быть ссылочным типом.</typeparam>
        /// <typeparam name="TDest">Тип целевого объекта, в который копируются значения. Должен быть ссылочным типом.</typeparam>
        /// <param name="source">Исходный объект, значения членов которого будут скопированы. Не может быть равен null.</param>
        /// <param name="destination">Целевой объект, в который будут скопированы значения членов. Не может быть равен null.</param>
        /// <param name="memberNames">Массив имен членов, которые необходимо скопировать. Если не указан или пуст, копируются все доступные
        /// свойства исходного объекта.</param>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        /// <exception cref="System.ArgumentNullException">destination.</exception>
        /// <exception cref="System.InvalidOperationException">Destination collection is not IList and cannot add new items.</exception>
        /// <remarks>Если оба параметра <paramref name="source" /> и <paramref name="destination" />
        /// являются коллекциями (кроме строк), метод копирует значения для каждого соответствующего элемента коллекции.
        /// При необходимости новые элементы добавляются в целевую коллекцию. Копирование выполняется только по
        /// указанным именам членов или по всем свойствам, если имена не заданы.</remarks>
        public static void Copy<TSource, TDest>(TSource source, TDest destination, params string[] memberNames)
            where TSource : class
            where TDest : class
        {
            if (source == null || typeof(TSource) == typeof(string))
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (destination == null || typeof(TDest) == typeof(string))
            {
                throw new ArgumentNullException(nameof(destination));
            }

            if (memberNames == null || memberNames.Length == 0)
            {
                memberNames = GetPropertyNames(source.GetType());
            }

            var sourceTypeCache = MemberCache.Create(source.GetType());
            if (sourceTypeCache.IsCollection)
                sourceTypeCache = MemberCache.Create(sourceTypeCache.ElementType);
            var destTypeCache = MemberCache.Create(destination.GetType());
            if (destTypeCache.IsCollection)
                destTypeCache = MemberCache.Create(destTypeCache.ElementType);

            if (source is IEnumerable srcList && !(source is string) && destination is IEnumerable dstList && !(destination is string))
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
                        dstItem = sourceTypeCache.DefaultConstructor();
                        if (dstList is IList dstIList)
                        {
                            dstListChanged = true;
                            dstIList.Add(dstItem);
                        }
                        else
                        {
                            throw new InvalidOperationException("Destination collection is not IList and cannot add new items.");
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
                foreach (var memberName in memberNames)
                {
                    var get = sourceTypeCache[memberName]?.Getter;
                    if (get == null)
                        continue;
                    var set = destTypeCache[memberName]?.Setter;
                    if (set == null)
                        continue;
                    var value = get(source);
                    set(destination, value);
                }
            }
        }

        /// <summary>
        /// Creates the direct field setter.
        /// </summary>
        /// <param name="fi">The fi.</param>
        /// <returns>Action&lt;System.Object, System.Object&gt;.</returns>
        public static Action<object, object> CreateDirectFieldSetter(FieldInfo fi) => (instance, value) =>
                                                                                               {
                                                                                                   var tr = __makeref(instance);
                                                                                                   fi.SetValueDirect(tr, value);
                                                                                               };

        /// <summary>
        /// Creates the factory.
        /// </summary>
        /// <param name="ctor">The ctor.</param>
        /// <returns>Func&lt;System.Object[], System.Object&gt;.</returns>
        public static Func<object[], object> CreateFactory(ConstructorInfo ctor)
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
        /// Creates the field getter.
        /// </summary>
        /// <param name="fi">The fi.</param>
        /// <returns>Func&lt;System.Object, System.Object&gt;.</returns>
        /// <exception cref="System.ArgumentNullException">fi.</exception>
        /// <exception cref="System.ArgumentException">Field has no declaring type - fi.</exception>
        /// <exception cref="System.InvalidOperationException">Failed to create field getter for field '{fi?.DeclaringType?.Name}.{fi?.Name}': {ex.Message}.</exception>
        public static Func<object, object> CreateFieldGetter(FieldInfo fi)
        {
            try
            {
                if (fi == null)
                {
                    throw new ArgumentNullException(nameof(fi));
                }

                var declaringType = fi.DeclaringType ?? throw new ArgumentException(@"Field has no declaring type", nameof(fi));
                var fieldType = fi.FieldType;

                // Проверяем, является ли поле константой
                if (fi.IsLiteral && !fi.IsInitOnly)
                {
                    // Для const полей возвращаем делегат, который всегда возвращает значение константы
                    var constValue = fi.GetRawConstantValue();
                    return _ => constValue;
                }

                var dm = new DynamicMethod(
                    $"get_{declaringType.Name}_{fi.Name}",
                    typeof(object),
                    new[] { typeof(object) },
                    declaringType.Module,
                    true);

                var il = dm.GetILGenerator();

                // Для статических полей (не констант)
                if (fi.IsStatic)
                {
                    il.Emit(System.Reflection.Emit.OpCodes.Ldsfld, fi); // Загружаем статическое поле
                    if (fieldType.IsValueType)
                    {
                        il.Emit(System.Reflection.Emit.OpCodes.Box, fieldType); // Боксим value type
                    }

                    il.Emit(System.Reflection.Emit.OpCodes.Ret);
                    return (Func<object, object>)dm.CreateDelegate(typeof(Func<object, object>));
                }

                // Для нестатических полей
                if (!declaringType.IsValueType)
                {
                    // Для ссылочных типов
                    var lblOk = il.DefineLabel();

                    // Проверяем целевой объект
                    il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
                    il.Emit(System.Reflection.Emit.OpCodes.Isinst, declaringType);
                    il.Emit(System.Reflection.Emit.OpCodes.Brtrue_S, lblOk);

                    // Если тип не подходит, выбрасываем исключение
                    il.Emit(System.Reflection.Emit.OpCodes.Newobj, typeof(InvalidCastException).GetConstructor(Type.EmptyTypes) ?? throw new InvalidOperationException());
                    il.Emit(System.Reflection.Emit.OpCodes.Throw);

                    il.MarkLabel(lblOk);

                    // Загружаем целевой объект и приводим к правильному типу
                    il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
                    il.Emit(System.Reflection.Emit.OpCodes.Castclass, declaringType);

                    // Загружаем поле
                    il.Emit(System.Reflection.Emit.OpCodes.Ldfld, fi);
                }
                else
                {
                    // Для value types (структур)

                    // Проверяем на null
                    var lblNotNull = il.DefineLabel();
                    il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
                    il.Emit(System.Reflection.Emit.OpCodes.Dup);
                    il.Emit(System.Reflection.Emit.OpCodes.Brtrue_S, lblNotNull);

                    // Если null, выбрасываем исключение
                    il.Emit(System.Reflection.Emit.OpCodes.Newobj, typeof(NullReferenceException).GetConstructor(Type.EmptyTypes) ?? throw new InvalidOperationException());
                    il.Emit(System.Reflection.Emit.OpCodes.Throw);

                    il.MarkLabel(lblNotNull);

                    // Распаковываем структуру
                    il.Emit(System.Reflection.Emit.OpCodes.Unbox_Any, declaringType);

                    // Создаем локальную переменную
                    var local = il.DeclareLocal(declaringType);
                    il.Emit(System.Reflection.Emit.OpCodes.Stloc, local);
                    il.Emit(System.Reflection.Emit.OpCodes.Ldloca_S, local); // Загружаем адрес

                    // Загружаем поле
                    il.Emit(System.Reflection.Emit.OpCodes.Ldflda, fi); // Загружаем адрес поля
                    il.Emit(System.Reflection.Emit.OpCodes.Ldobj, fieldType); // Загружаем значение по адресу
                }

                // Боксим результат, если это value type
                if (fieldType.IsValueType)
                {
                    il.Emit(System.Reflection.Emit.OpCodes.Box, fieldType);
                }

                il.Emit(System.Reflection.Emit.OpCodes.Ret);

                return (Func<object, object>)dm.CreateDelegate(typeof(Func<object, object>));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to create field getter for field '{fi?.DeclaringType?.Name}.{fi?.Name}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Creates the field setter.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <returns>Action&lt;System.Object, System.Object&gt;.</returns>
        /// <exception cref="System.ArgumentNullException">field.</exception>
        public static Action<object, object> CreateFieldSetter(FieldInfo field)
        {
            if (field == null)
            {
                throw new ArgumentNullException(nameof(field));
            }

            var dm = new DynamicMethod(
                $"Set_{field.Name}",
                typeof(void),
                new[] { typeof(object), typeof(object) },
                restrictedSkipVisibility: true);

            var il = dm.GetILGenerator();

            // local 0: TypedReference
            il.DeclareLocal(typeof(TypedReference));

            // __makeref((T)target)
            il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
            Debug.Assert(field.DeclaringType != null, "field.DeclaringType != null");
            il.Emit(System.Reflection.Emit.OpCodes.Unbox, field.DeclaringType);
            il.Emit(System.Reflection.Emit.OpCodes.Mkrefany, field.DeclaringType);
            il.Emit(System.Reflection.Emit.OpCodes.Stloc_0);

            // ref field
            il.Emit(System.Reflection.Emit.OpCodes.Ldloc_0);
            il.Emit(System.Reflection.Emit.OpCodes.Refanyval, field.DeclaringType);
            il.Emit(System.Reflection.Emit.OpCodes.Ldflda, field);

            // value
            il.Emit(System.Reflection.Emit.OpCodes.Ldarg_1);

            if (field.FieldType.IsValueType)
            {
                il.Emit(System.Reflection.Emit.OpCodes.Unbox_Any, field.FieldType);
            }
            else
            {
                il.Emit(System.Reflection.Emit.OpCodes.Castclass, field.FieldType);
            }

            il.Emit(System.Reflection.Emit.OpCodes.Stobj, field.FieldType);
            il.Emit(System.Reflection.Emit.OpCodes.Ret);

            return (Action<object, object>)dm.CreateDelegate(typeof(Action<object, object>));
        }

        /// <summary>
        /// Creates the property getter.
        /// </summary>
        /// <param name="pi">The pi.</param>
        /// <returns>Func&lt;System.Object, System.Object&gt;.</returns>
        public static Func<object, object> CreatePropertyGetter(PropertyInfo pi)
        {
            var getter = pi.GetGetMethod(true);
            var declaring = pi.DeclaringType;
            var propertyType = pi.PropertyType;

            Debug.Assert(declaring?.Module != null, "declaring?.Module != null");
            var dm = new DynamicMethod(
                "get_" + pi.Name,
                typeof(object),
                new[] { typeof(object) },
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
                return (Func<object, object>)dm.CreateDelegate(typeof(Func<object, object>));
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

            return (Func<object, object>)dm.CreateDelegate(typeof(Func<object, object>));
        }

        /// <summary>
        /// Creates the property setter.
        /// </summary>
        /// <param name="pi">The pi.</param>
        /// <returns>Action&lt;System.Object, System.Object&gt;.</returns>
        public static Action<object, object> CreatePropertySetter(PropertyInfo pi)
        {
            var setter = pi.GetSetMethod(true);
            if (setter == null)
            {
                var backingField = GetFieldInfoFromGetAccessor(pi.GetMethod);
                if (backingField != null)
                {
                    return CreateDirectFieldSetter(backingField);
                }

                return null;
            }

            var declaring = pi.DeclaringType;
            var propertyType = pi.PropertyType;

            Debug.Assert(declaring != null, nameof(declaring) + " != null");
            var dm = new DynamicMethod(
                "set_" + pi.Name,
                null,
                new[] { typeof(object), typeof(object) },
                declaring.Module,
                true);

            var il = dm.GetILGenerator();

            // Для статических методов
            if (setter.IsStatic)
            {
                il.Emit(System.Reflection.Emit.OpCodes.Ldarg_1); // Загружаем значение
                if (propertyType.IsValueType)
                {
                    il.Emit(System.Reflection.Emit.OpCodes.Unbox_Any, propertyType);
                }
                else
                {
                    il.Emit(System.Reflection.Emit.OpCodes.Castclass, propertyType);
                }

                il.Emit(System.Reflection.Emit.OpCodes.Call, setter);
                il.Emit(System.Reflection.Emit.OpCodes.Ret);
                return (Action<object, object>)dm.CreateDelegate(typeof(Action<object, object>));
            }

            // Для нестатических методов
            if (!declaring.IsValueType)
            {
                // Для ссылочных типов
                var lblOk = il.DefineLabel();

                // Проверяем целевой объект (obj)
                il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
                il.Emit(System.Reflection.Emit.OpCodes.Isinst, declaring);
                il.Emit(System.Reflection.Emit.OpCodes.Brtrue_S, lblOk);

                il.Emit(System.Reflection.Emit.OpCodes.Newobj, typeof(InvalidCastException).GetConstructor(Type.EmptyTypes) ?? throw new InvalidOperationException());
                il.Emit(System.Reflection.Emit.OpCodes.Throw);

                il.MarkLabel(lblOk);

                // Загружаем целевой объект и приводим к правильному типу
                il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
                il.Emit(System.Reflection.Emit.OpCodes.Castclass, declaring);

                // Загружаем значение
                il.Emit(System.Reflection.Emit.OpCodes.Ldarg_1);
                if (propertyType.IsValueType)
                {
                    il.Emit(System.Reflection.Emit.OpCodes.Unbox_Any, propertyType);
                }
                else
                {
                    il.Emit(System.Reflection.Emit.OpCodes.Castclass, propertyType);
                }

                il.Emit(System.Reflection.Emit.OpCodes.Callvirt, setter);
            }
            else
            {
                // Для value types (структур)
                // Создаем локальную переменную для хранения распакованной структуры
                var local = il.DeclareLocal(declaring);

                // Проверяем целевой объект на null
                var lblNotNull = il.DefineLabel();
                il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
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

                // Загружаем адрес локальной переменной
                il.Emit(System.Reflection.Emit.OpCodes.Ldloca_S, local);

                // Загружаем значение
                il.Emit(System.Reflection.Emit.OpCodes.Ldarg_1);
                if (propertyType.IsValueType)
                {
                    il.Emit(System.Reflection.Emit.OpCodes.Unbox_Any, propertyType);
                }
                else
                {
                    il.Emit(System.Reflection.Emit.OpCodes.Castclass, propertyType);
                }

                // Вызываем setter
                il.Emit(System.Reflection.Emit.OpCodes.Call, setter);

                // Боксим структуру обратно в object (обновляем исходный объект)
                il.Emit(System.Reflection.Emit.OpCodes.Ldloc, local);
                il.Emit(System.Reflection.Emit.OpCodes.Box, declaring);
                il.Emit(System.Reflection.Emit.OpCodes.Starg_S, 0); // Сохраняем обратно в первый аргумент
            }

            il.Emit(System.Reflection.Emit.OpCodes.Ret);

            return (Action<object, object>)dm.CreateDelegate(typeof(Action<object, object>));
        }

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
        /// Выполняет поиск члена с указанным именем в заданном типе и возвращает информацию о найденном члене.
        /// </summary>
        /// <param name="type">Тип, в котором выполняется поиск члена. Не может быть равен null.</param>
        /// <param name="name">Имя члена, который требуется найти. Поиск чувствителен к регистру.</param>
        /// <returns>Объект типа MemberInfo, представляющий найденный член, или null, если член с указанным именем не найден.</returns>
        /// <remarks>Метод использует внутреннее кэширование для повышения производительности повторных
        /// запросов. Если член не найден в кэше, выполняется поиск с различными параметрами привязки. Может возвращать
        /// члены, объявленные как в самом типе, так и унаследованные.</remarks>
        public static MemberInfo FindMember(Type type, string name)
        {
            if (MemberInfoCache.TryGetValue(type.FullName + "." + name, out var memberInfo))
            {
                return memberInfo;
            }

            memberInfo = FindMember(type, name, false, null) ?? FindMember(type, name, true, DefaultBindingFlags);
            MemberInfoCache.TryAdd(type.FullName + "." + name, memberInfo);
            return memberInfo;
        }

        /// <summary>
        /// Ищет член типа (свойство, поле или метод) по его имени,
        /// включая проверку в базовых типах и реализованных интерфейсах.
        /// </summary>
        /// <param name="type">Тип, в котором выполняется поиск.</param>
        /// <param name="name">Имя члена, который необходимо найти.</param>
        /// <param name="ignoreCase">Если <c>true</c>, поиск выполняется без учета регистра букв.</param>
        /// <param name="bindingFlags">Набор флагов <see cref="BindingFlags" />, определяющих стратегию поиска.
        /// Если не указан, используется значение <c>DefaultBindingFlags</c>.</param>
        /// <returns>Объект <see cref="MemberInfo" />, соответствующий найденному члену,
        /// либо <c>null</c>, если подходящий член не найден.</returns>
        /// <remarks>Метод выполняет поиск в следующем порядке:
        /// <list type="number"><item><description>Свойства типа;</description></item><item><description>Поля типа;</description></item><item><description>Свойства интерфейсов, реализованных данным типом;</description></item><item><description>Методы типа;</description></item><item><description>Рекурсивный поиск в базовом типе.</description></item></list></remarks>
        public static MemberInfo FindMember(Type type, string name, bool ignoreCase, BindingFlags? bindingFlags)
        {
            var flags = bindingFlags ?? DefaultBindingFlags;
            if (ignoreCase)
            {
                flags |= BindingFlags.IgnoreCase;
            }

            // 1. Property
            var prop = type.GetProperty(name, flags);
            if (prop != null)
            {
                return prop;
            }

            // 2. Field
            var field = type.GetField(name, flags);
            if (field != null)
            {
                return field;
            }

            // 3. Interface properties
            foreach (var it in type.GetInterfaces())
            {
                var iprop = it.GetProperty(name, flags);
                if (iprop != null)
                {
                    return iprop;
                }
            }

            // 4. Method
            var method = type.GetMethod(name, flags);
            if (method != null)
            {
                return method;
            }

            // 5. Base types (итерация вместо рекурсии)
            var bt = type.BaseType;
            while (bt != null)
            {
                var m = FindMember(bt, name, ignoreCase, bindingFlags);
                if (m != null)
                {
                    return m;
                }

                bt = bt.BaseType;
            }

            return null;
        }

        /// <summary>
        /// Возвращает значение поля или свойства объекта по имени члена.
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
                return null;
            }

            var memberValue = getter(instance);
            return convertToType == null
                ? memberValue
                : ChangeType(memberValue, convertToType);
        }

        /// <summary>
        /// Получает значение вложенного поля или свойства объекта
        /// по указанному пути к члену.
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

            var path = pathToMemberName as string[] ?? pathToMemberName.ToArray();

            if (path.Length == 1)
            {
                return Get(instance, path[0], convertToType);
            }

            var getter = GetMemberGetter(instance.GetType(), path[0]);
            var memberValue = getter?.Invoke(instance);

            return memberValue == null
                ? null
                : Get(memberValue, path.Skip(1).ToArray(), convertToType);
        }

        /// <summary>
        /// Gets the specified instance.
        /// </summary>
        /// <typeparam name="T">Type.</typeparam>
        /// <param name="instance">The instance.</param>
        /// <param name="pathToMemberName">Name of the path to member.</param>
        /// <returns>T.</returns>
        public static T Get<T>(object instance, IEnumerable<string> pathToMemberName) => (T)Get(instance, pathToMemberName, typeof(T));

        /// <summary>
        /// Возвращает значение поля или свойства объекта по имени члена,
        /// приведённое к указанному типу.
        /// </summary>
        /// <typeparam name="T">Тип возвращаемого значения.</typeparam>
        /// <param name="instance">Экземпляр объекта, из которого требуется получить значение.</param>
        /// <param name="memberName">Имя поля или свойства.</param>
        /// <returns>Значение поля или свойства, приведённое к типу <typeparamref name="T" />.</returns>
        public static T Get<T>(object instance, string memberName) => (T)Get(instance, memberName, typeof(T));

        /// <summary>
        /// Получает цепочку базовых типов и/или интерфейсов.
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
            {
                baseTypes.Add(type);
            }

            if (getInterfaces)
            {
                baseTypes.AddRange(type.GetInterfaces());
            }

            return baseTypes.ToArray();
        }

        /// <summary>
        /// Возвращает тип элементов коллекции.
        /// </summary>
        /// <param name="type">Тип коллекции.</param>
        /// <returns>Тип элементов коллекции или null, если тип не является коллекцией.</returns>
        public static Type GetCollectionItemType(Type type)
        {
            if (type == null)
            {
                return null;
            }

            if (type.IsArray)
            {
                return type.GetElementType();
            }

            var isDic = typeof(IDictionary).IsAssignableFrom(type);
            var ga = type.GetGenericArguments();

            if (isDic && ga.Length > 1)
            {
                return ga[1];
            }

            return ga.FirstOrDefault();
        }

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
        public static Func<TFrom, TTo> GetCustomTypeConverter<TFrom, TTo>() => GetCustomTypeConverter(typeof(TFrom), typeof(TTo))
                ?.ConvertFunc<TFrom, TTo>();

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
        /// <list type="bullet"><item><description><see cref="IEnumerable{T}" /> → <see cref="List{T}" /></description></item><item><description><see cref="IList{T}" /> → <see cref="List{T}" /></description></item><item><description><see cref="ICollection{T}" /> → <see cref="List{T}" /></description></item><item><description><see cref="IDictionary{TKey, TValue}" /> → <see cref="Dictionary{TKey, TValue}" /></description></item></list></returns>
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
        /// Возвращает поле по условию фильтрации.
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
        /// Gets the field information from get accessor.
        /// </summary>
        /// <param name="accessor">The accessor.</param>
        /// <returns>FieldInfo.</returns>
        /// <exception cref="System.ArgumentNullException">accessor.</exception>
        /// <exception cref="System.ArgumentException">Method has no declaring type - accessor.</exception>
        public static FieldInfo GetFieldInfoFromGetAccessor(MethodInfo accessor)
        {
            if (accessor == null)
            {
                throw new ArgumentNullException(nameof(accessor));
            }

            var declaringType = accessor.DeclaringType ?? throw new ArgumentException(@"Method has no declaring type", nameof(accessor));
            var propertyName = accessor.Name.Substring(4);

            // Вариант 1: Поиск автоматически сгенерированного поля для автосвойств
            var autoBackingFieldName = $"<{propertyName}>k__BackingField";
            var field = declaringType.GetField(autoBackingFieldName, DefaultBindingFlags);

            if (field != null)
            {
                return field;
            }

            // Вариант 2: Поиск в базовых типах
            var baseType = declaringType.BaseType;
            while (baseType != null && baseType != typeof(object))
            {
                field = baseType.GetField(autoBackingFieldName, DefaultBindingFlags);

                if (field != null)
                {
                    return field;
                }

                baseType = baseType.BaseType;
            }

            // Вариант 3: Анализ IL-кода
            field = GetBackingFieldFromIl(accessor);
            if (field != null)
            {
                return field;
            }

            // Вариант 4: Поиск по стандартным шаблонам именования
            return FindFieldByNamingPatterns(declaringType, propertyName);
        }

        /// <summary>
        /// Возвращает отображение имён полей типа на объекты <see cref="FieldInfo" />.
        /// </summary>
        /// <typeparam name="T">Тип, поля которого требуется получить.</typeparam>
        /// <returns>Словарь «имя поля → FieldInfo».</returns>
        public static Dictionary<string, FieldInfo> GetFieldsMap<T>() => GetFieldsMap(typeof(T));

        /// <summary>
        /// Возвращает отображение имён полей указанного типа на объекты <see cref="FieldInfo" />.
        /// </summary>
        /// <param name="type">Тип, поля которого требуется получить.</param>
        /// <returns>Словарь «имя поля → FieldInfo».</returns>
        public static Dictionary<string, FieldInfo> GetFieldsMap(Type type)
        {
            if (FieldsCache.TryGetValue(type, out var cached))
            {
                return cached;
            }

            var typeFields = type.GetFields(DefaultBindingFlags);
            var dic = new Dictionary<string, FieldInfo>();
            foreach (var field in typeFields)
            {
                dic[field.Name] = field;
            }

            FieldsCache[type] = dic;
            return dic;
        }

        /// <summary>
        /// Возвращает все типы из указанной сборки (или из сборки вызывающего кода),
        /// которые реализуют интерфейс или наследуются от указанного базового типа.
        /// </summary>
        /// <param name="baseType">Базовый тип или интерфейс для поиска реализаций.</param>
        /// <param name="fromAssembly">Сборка для поиска типов. Если не указана, используется сборка вызывающего кода.</param>
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
        /// Возвращает все типы из всех загруженных в домен приложений сборок,
        /// которые реализуют интерфейс или наследуются от указанного базового типа.
        /// </summary>
        /// <param name="baseType">Базовый тип или интерфейс для поиска реализаций.</param>
        /// <returns>Массив типов, удовлетворяющих условию.</returns>
        public static Type[] GetImplementationsOf(Type baseType) => AppDomain.CurrentDomain
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

        /// <summary>
        /// Получает событие с наименьшего уровня иерархии.
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
                {
                    return member;
                }

                type = type.BaseType;
            }

            return null;
        }

        /// <summary>
        /// Получает поле с наименьшего уровня иерархии.
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
                {
                    return member;
                }

                type = type.BaseType;
            }

            return null;
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
        /// Возвращает делегат, позволяющий получить значение указанного поля или свойства объекта заданного типа по
        /// имени.
        /// </summary>
        /// <typeparam name="T">Тип объекта, содержащего поле или свойство, к которому требуется получить доступ.</typeparam>
        /// <param name="memberName">Имя поля или свойства, значение которого необходимо получить. Не может быть null или пустой строкой.</param>
        /// <returns>Делегат, принимающий объект типа <typeparamref name="T" /> и возвращающий значение указанного поля или
        /// свойства. Возвращает null, если член с заданным именем не найден.</returns>
        /// <remarks>Если указанный член не существует или не поддерживается для чтения, возвращаемое
        /// значение будет null. Метод поддерживает все поля и свойства. Делегат не выполняет проверку
        /// типов во время выполнения; некорректное использование может привести к исключениям.</remarks>
        public static Func<object, object> GetMemberGetter<T>(string memberName) => GetMemberGetter(typeof(T), memberName);

        /// <summary>
        /// Возвращает делегат, позволяющий получить значение указанного поля или свойства объекта заданного типа по
        /// имени.
        /// </summary>
        /// <param name="type">Тип объекта, содержащего поле или свойство, к которому требуется получить доступ.</param>
        /// <param name="memberName">Имя поля или свойства, значение которого необходимо получить. Не может быть null или пустой строкой.</param>
        /// <returns>Делегат, принимающий объект типа и возвращающий значение указанного поля или
        /// свойства. Возвращает null, если член с заданным именем не найден.</returns>
        /// <remarks>Если указанный член не существует или не поддерживается для чтения, возвращаемое
        /// значение будет null. Метод поддерживает все поля и свойства. Делегат не выполняет проверку
        /// типов во время выполнения; некорректное использование может привести к исключениям.</remarks>
        public static Func<object, object> GetMemberGetter(Type type, string memberName)
        {
            var member = FindMember(type, memberName);
            return GetMemberGetter(member);
        }

        /// <summary>
        /// Возвращает делегат для получения значения поля или свойства.
        /// </summary>
        /// <param name="memberInfo">
        /// Информация о члене типа, для которого требуется получить геттер.
        /// Поддерживаются поля (<see cref="FieldInfo"/>) и свойства (<see cref="PropertyInfo"/>).
        /// </param>
        /// <returns>
        /// Делегат вида <c>Func&lt;object, object&gt;</c>, принимающий экземпляр объекта
        /// (или <c>null</c> для статических членов) и возвращающий значение члена.
        ///
        /// Если переданный член не является полем или свойством,
        /// возвращается <c>null</c>.
        /// </returns>
        /// <remarks>
        /// Для повышения производительности используются кэши
        /// делегатов геттеров, что позволяет избежать повторного
        /// создания выражений или динамического кода.
        ///
        /// Метод не выполняет проверку доступности члена
        /// (например, <c>private</c>) и не гарантирует успешное
        /// получение значения при ошибках приведения типов
        /// или отсутствии геттера у свойства.
        /// </remarks>
        public static Func<object, object> GetMemberGetter(MemberInfo memberInfo)
        {
            switch (memberInfo)
            {
                case FieldInfo fi:
                    return FieldGetterCache.GetOrAdd(fi, CreateFieldGetter);

                case PropertyInfo pi:
                    return PropertyGetterCache.GetOrAdd(pi, CreatePropertyGetter);

                default:
                    return null;
            }
        }

        /// <summary>
        /// Получить свойство указанное в выражении.
        /// </summary>
        /// <param name="expr">The expr.</param>
        /// <returns>MemberInfo.</returns>
        public static MemberInfo GetMemberInfo(Expression expr)
        {
            if (expr == null)
            {
                return null;
            }

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
            switch (memberInfo)
            {
                case FieldInfo fi:
                    return FieldSetterCache.GetOrAdd(fi, CreateFieldSetter);

                case PropertyInfo pi:
                    return PropertySetterCache.GetOrAdd(pi, CreatePropertySetter);
            }

            return null;
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
                    return PropertySetterCache.GetOrAdd(pi, CreatePropertySetter);
            }

            memberType = null;
            return GetMemberSetter(member);
        }

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
        public static Action<object, object> GetMemberSetter<T>(string memberName, out Type memberType) => GetMemberSetter(typeof(T), memberName, out memberType);

        /// <summary>
        /// Получает значения всех полей и свойств типа T из объекта TClass.
        /// </summary>
        /// <typeparam name="T">Тип члена, который ищем.</typeparam>
        /// <param name="obj">Объект, из которого извлекаем значения.</param>
        /// <param name="memberFilter">Опциональный фильтр значений.</param>
        /// <param name="recursive">Если true, рекурсивно обходит вложенные объекты.</param>
        /// <param name="searchInCollections">Если true, рекурсивно ищет элементы типа T в коллекциях.</param>
        /// <returns>IEnumerable&lt;T&gt;.</returns>
        /// <exception cref="System.ArgumentNullException">obj.</exception>
        public static IEnumerable<T> GetMembersOfType<T>(this object obj, Func<T, bool> memberFilter = null, bool recursive = false, bool searchInCollections = false)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj));
            }

            var visited = new HashSet<object>();
            return GetMembersInternal(obj, memberFilter, recursive, searchInCollections, visited);
        }

        /// <summary>
        /// Возвращает значение по ключу из словаря или добавляет его, если ключ отсутствует.
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
            {
                return val;
            }

            val = valueFactory();
            dic[key] = val;
            return val;
        }

        /// <summary>
        /// Получает все публичные свойства типа <typeparamref name="T" />.
        /// Использует внутренний кеш для ускорения повторных вызовов.
        /// </summary>
        /// <typeparam name="T">Тип, для которого нужно получить свойства.</typeparam>
        /// <returns>Массив <see cref="PropertyInfo" /> всех публичных свойств.</returns>
        public static PropertyInfo[] GetProperties<T>()
            where T : class => GetProperties(typeof(T));

        /// <summary>
        /// Получает все публичные свойства указанного типа.
        /// Использует внутренний кеш для ускорения повторных вызовов.
        /// </summary>
        /// <param name="type">Тип, для которого нужно получить свойства.</param>
        /// <returns>Массив <see cref="PropertyInfo" /> всех публичных свойств.</returns>
        public static PropertyInfo[] GetProperties(Type type) => GetPropertiesMap(type).Values.ToArray();

        /// <summary>
        /// Возвращает отображение имён свойств типа на объекты <see cref="PropertyInfo" />.
        /// </summary>
        /// <typeparam name="T">Тип, свойства которого требуется получить.</typeparam>
        /// <returns>Словарь «имя свойства → PropertyInfo».</returns>
        public static Dictionary<string, PropertyInfo> GetPropertiesMap<T>() => GetPropertiesMap(typeof(T));

        /// <summary>
        /// Возвращает отображение имён свойств указанного типа на объекты <see cref="PropertyInfo" />.
        /// </summary>
        /// <param name="type">Тип, свойства которого требуется получить.</param>
        /// <returns>Словарь «имя свойства → PropertyInfo».</returns>
        public static Dictionary<string, PropertyInfo> GetPropertiesMap(Type type)
        {
            if (PropertiesCache.TryGetValue(type, out var cached))
            {
                return cached;
            }

            var typeProperties = type.GetProperties(DefaultBindingFlags);
            var dic = new Dictionary<string, PropertyInfo>();
            foreach (var prop in typeProperties)
            {
                dic[prop.Name] = prop;
            }

            PropertiesCache[type] = dic;
            return dic;
        }

        /// <summary>
        /// Получить свойство по его имени.
        /// </summary>
        /// <param name="type">Тип в котором искать свойство.</param>
        /// <param name="propertyName">Имя свойства.</param>
        /// <param name="stringComparison">Сравнение имен.</param>
        /// <returns>PropertyInfo.</returns>
        public static PropertyInfo GetProperty(Type type, string propertyName, StringComparison stringComparison = StringComparison.OrdinalIgnoreCase)
        {
            if (stringComparison == StringComparison.OrdinalIgnoreCase)
            {
                return GetPropertiesMap(type).Values.FirstOrDefault(x => string.Compare(x.Name, propertyName, stringComparison) == 0);
            }

            return GetPropertiesMap(type).TryGetValue(propertyName, out var pi) ? pi : null;
        }

        /// <summary>
        /// Получить свойство указанное в выражении.
        /// </summary>
        /// <param name="expr">The expr.</param>
        /// <returns>PropertyInfo.</returns>
        public static PropertyInfo GetProperty(Expression expr) => GetMemberInfo(expr) as PropertyInfo;

        /// <summary>
        /// Получает имена всех публичных свойств типа <typeparamref name="T" />.
        /// </summary>
        /// <typeparam name="T">Тип, для которого нужно получить имена свойств.</typeparam>
        /// <returns>Массив имен свойств.</returns>
        public static string[] GetPropertyNames<T>() => GetPropertyNames(typeof(T));

        /// <summary>
        /// Получает имена всех публичных свойств указанного типа.
        /// Использует внутренний кеш для ускорения повторных вызовов.
        /// </summary>
        /// <param name="type">Тип, для которого нужно получить имена свойств.</param>
        /// <returns>Массив имен свойств.</returns>
        public static string[] GetPropertyNames(Type type) => GetPropertiesMap(type).Keys.ToArray();

        /// <summary>
        /// Ищет тип или интерфейс по указанному имени во всех сборках, загруженных в текущий <see cref="AppDomain" />.
        /// Результаты поиска кэшируются для ускорения последующих вызовов.
        /// </summary>
        /// <param name="typeOrInterfaceName">Полное или короткое имя типа (например, <c>"System.String"</c> или <c>"String"</c>).</param>
        /// <returns>Объект <see cref="Type" />, если тип найден; в противном случае <see langword="null" />.</returns>
        /// <exception cref="System.ArgumentException">Type name cannot be null or empty. - typeOrInterfaceName.</exception>
        /// <example>
        /// Пример использования:
        /// <code language="csharp">
        /// var type1 = TypeHelper.GetTypeByName("System.String");
        /// Console.WriteLine(type1); // Вывод: System.String
        /// var type2 = TypeHelper.GetTypeByName("String");
        /// Console.WriteLine(type2); // Вывод: System.String
        /// var type3 = TypeHelper.GetTypeByName("IEnumerable");
        /// Console.WriteLine(type3); // Вывод: System.Collections.IEnumerable
        /// // Повторный вызов — берётся из кэша, без обхода сборок
        /// var cached = TypeHelper.GetTypeByName("System.String");
        /// Console.WriteLine(ReferenceEquals(type1, cached)); // True
        /// </code></example>
        /// <remarks>Поиск выполняется без учёта регистра, сравниваются <see cref="Type.FullName" /> и Type.Name.
        /// При первом вызове метод перебирает все загруженные сборки, затем кэширует результат.</remarks>
        public static Type GetTypeByName(string typeOrInterfaceName)
        {
            if (string.IsNullOrWhiteSpace(typeOrInterfaceName))
            {
                throw new ArgumentException(@"Type name cannot be null or empty.", nameof(typeOrInterfaceName));
            }

            // Проверяем кэш
            if (TypeCache.TryGetValue(typeOrInterfaceName, out var cachedType))
            {
                return cachedType;
            }

            Type foundType = null;
            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in loadedAssemblies)
            {
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
            }

            // Кэшируем результат (в том числе null, чтобы избежать повторных обходов)
            TypeCache[typeOrInterfaceName] = foundType;

            return foundType;
        }

        /// <summary>
        /// Возвращает значение по ключу из коллекции пар ключ-значение или значение по умолчанию, если ключ отсутствует.
        /// </summary>
        /// <typeparam name="TKey">Тип ключа.</typeparam>
        /// <typeparam name="TValue">Тип значения.</typeparam>
        /// <param name="dic">Коллекция пар ключ-значение.</param>
        /// <param name="key">Ключ для поиска.</param>
        /// <param name="comparer">Компаратор для сравнения ключей.
        /// Если <c>null</c>, используется стандартное сравнение по <see cref="EqualityComparer{TKey}.Default" />.</param>
        /// <returns>Значение, соответствующее ключу, или значение по умолчанию для <typeparamref name="TValue" />, если ключ не найден.</returns>
        /// <exception cref="System.ArgumentNullException">dic.</exception>
        /// <remarks>Метод выбирает стратегию поиска в зависимости от наличия компаратора и размера коллекции:
        /// <list type="number"><item><description>Если компаратор не задан — выполняется линейный поиск.</description></item><item><description>Если коллекция небольшая (<c>Count &lt; ThresholdForSorted</c>) — также линейный поиск.</description></item><item><description>
        /// Если коллекция большая — создаётся кэшированный <see cref="SortedDictionary{TKey, TValue}" />
        /// для быстрого поиска.
        /// </description></item></list></remarks>
        public static TValue GetValueOrDefault<TKey, TValue>(
            this IEnumerable<KeyValuePair<TKey, TValue>> dic,
            TKey key,
            IComparer<TKey> comparer = null)
        {
            if (dic == null)
            {
                throw new ArgumentNullException(nameof(dic));
            }

            // 1. Компаратор не задан — стандартное сравнение
            if (comparer == null)
            {
                return dic
                    .Where(x => EqualityComparer<TKey>.Default.Equals(x.Key, key))
                    .Select(x => x.Value)
                    .FirstOrDefault();
            }

            // 2. Компаратор задан
            return dic
                .Where(kv => comparer.Compare(kv.Key, key) == 0)
                .Select(kv => kv.Value)
                .FirstOrDefault();
        }

        /// <summary>
        /// Получает значения свойств объекта в указанном порядке.
        /// </summary>
        /// <typeparam name="TObject">The type of the t object.</typeparam>
        /// <param name="source">Исходный объект.</param>
        /// <param name="memberNames">Имена свойств объекта с учетом регистра.</param>
        /// <returns>System.Object[].</returns>
        public static object[] GetValues<TObject>(TObject source, params string[] memberNames)
            where TObject : class
        {
            var values = new List<object>();
            var sourceTypeCache = MemberCache.Create(typeof(TObject));
            var props = memberNames?.Any() == true ? sourceTypeCache.Properties.Where(x => memberNames.Contains(x.Name)).ToArray() : sourceTypeCache.PublicProperties;
            foreach (var p in props)
            {
                values.Add(p.Getter?.Invoke(source));
            }

            return values.ToArray();
        }

        /// <summary>
        /// Получает значения свойств объекта в указанном порядке и преобразует в указанный тип через
        /// <see cref="Obj.ChangeType{T}(object, IFormatProvider)" />.
        /// </summary>
        /// <typeparam name="TObject">The type of the t object.</typeparam>
        /// <typeparam name="TValue">The type of the t value.</typeparam>
        /// <param name="source">Исходный объект.</param>
        /// <param name="memberNames">Имена свойств объекта с учетом регистра.</param>
        /// <returns>TValue[].</returns>
        public static TValue[] GetValues<TObject, TValue>(TObject source, params string[] memberNames)
            where TObject : class => GetValues(source, memberNames).Select(x => ChangeType<TValue>(x)).ToArray();

        /// <summary>
        /// Проверяет, является ли тип простым (базовым).
        /// </summary>
        /// <param name="t">Тип для проверки.</param>
        /// <returns>True, если тип является базовым, иначе False.</returns>
        public static bool IsBasic(Type t) => t != null && (t.IsEnum || BasicTypes.Contains(t));

        /// <summary>
        /// Проверяет, является ли тип логическим.
        /// </summary>
        /// <param name="t">Тип для проверки.</param>
        /// <returns>True, если тип является логическим, иначе False.</returns>
        public static bool IsBoolean(Type t) => BoolTypes.Contains(t);

        /// <summary>
        /// Проверяет, является ли тип типизированной коллекцией.
        /// </summary>
        /// <param name="t">Тип для проверки.</param>
        /// <returns>True, если тип является коллекцией, иначе False.</returns>
        public static bool IsGenericCollection(Type t)
        {
            var hasGenericType = t.GenericTypeArguments.Length > 0;
            return hasGenericType && IsCollection(t);
        }

        /// <summary>
        /// Проверяет, является ли тип коллекцией.
        /// </summary>
        /// <param name="t">Тип для проверки.</param>
        /// <returns>True, если тип является коллекцией, иначе False.</returns>
        public static bool IsCollection(Type t)
        {
            if (t.IsArray)
            {
                return true;
            }

            if (t == typeof(string))
            {
                return false;
            }

            return typeof(IList).IsAssignableFrom(t) || typeof(ICollection).IsAssignableFrom(t) || typeof(IEnumerable).IsAssignableFrom(t);
        }

        /// <summary>
        /// Проверяет, является ли тип датой/временем.
        /// </summary>
        /// <param name="t">Тип для проверки.</param>
        /// <returns>True, если тип представляет дату/время, иначе False.</returns>
        public static bool IsDate(Type t) => DateTypes.Contains(t);

        /// <summary>
        /// Проверяет, является ли тип делегатом.
        /// </summary>
        /// <param name="type">Тип для проверки.</param>
        /// <returns>True, если тип является делегатом, иначе False.</returns>
        public static bool IsDelegate(Type type) => typeof(MulticastDelegate).IsAssignableFrom(type.BaseType);

        /// <summary>
        /// Проверяет, является ли тип словарём.
        /// </summary>
        /// <param name="type">Тип для проверки.</param>
        /// <returns>True, если тип является словарём, иначе False.</returns>
        public static bool IsDictionary(Type type) => IsImplements<IDictionary>(type) ||
                   (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>)) || type
                       .GetInterfaces()
                       .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));

        /// <summary>
        /// Проверяет, является ли тип числом с плавающей точкой.
        /// </summary>
        /// <param name="t">Тип для проверки.</param>
        /// <returns>True, если тип является числом с плавающей точкой, иначе False.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFloat(Type t)
        {
            return t == typeof(float)
                   || t == typeof(double)
                   || t == typeof(decimal);
        }

        /// <summary>
        /// Проверяет, является ли тип целым числом.
        /// </summary>
        /// <param name="t">Тип для проверки.</param>
        /// <returns>True, если тип является целым числом, иначе False.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNaturalNumeric(Type t)
        {
            return t == typeof(byte)
                   || t == typeof(sbyte)
                   || t == typeof(short)
                   || t == typeof(ushort)
                   || t == typeof(int)
                   || t == typeof(uint)
                   || t == typeof(long)
                   || t == typeof(ulong);
        }

        /// <summary>
        /// Проверяет, реализует ли тип заданный интерфейс.
        /// </summary>
        /// <param name="t">Тип для проверки.</param>
        /// <param name="implementType">Интерфейс, который нужно проверить.</param>
        /// <returns>True, если тип реализует указанный интерфейс, иначе False.</returns>
        public static bool IsImplements(Type t, Type implementType) => implementType.IsAssignableFrom(t);

        /// <summary>
        /// Проверяет, реализует ли тип заданный интерфейс (generic).
        /// </summary>
        /// <typeparam name="T">Интерфейс, который нужно проверить.</typeparam>
        /// <param name="t">Тип для проверки.</param>
        /// <returns>True, если тип реализует указанный интерфейс, иначе False.</returns>
        public static bool IsImplements<T>(Type t) => typeof(T).IsAssignableFrom(t);

        /// <summary>
        /// Проверяет, является ли тип nullable.
        /// </summary>
        /// <param name="t">Тип для проверки.</param>
        /// <returns>True, если тип является nullable, иначе False.</returns>
        public static bool IsNullable(Type t) => !t.IsValueType || Nullable.GetUnderlyingType(t) != null || t == typeof(object);

        /// <summary>
        /// Проверяет, является ли тип числовым.
        /// </summary>
        /// <param name="t">Тип для проверки.</param>
        /// <param name="includeFloatTypes">Включать ли типы с плавающей точкой.</param>
        /// <returns>True, если тип является числовым, иначе False.</returns>
        public static bool IsNumeric(Type t, bool includeFloatTypes = true) => includeFloatTypes ? IsFloat(t) || IsNaturalNumeric(t) : IsNaturalNumeric(t);

        /// <summary>
        /// Проверяет, является ли тип кортежем (ValueTuple/Tuple).
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

            var ctor = FindConstructor(type, args) ?? throw new InvalidOperationException($"No constructor found for type {type}");
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
            return itemType == null ? throw new InvalidOperationException("Cannot determine item type of the collection.") : New(itemType);
        }

        /// <summary>
        /// Устанавливает значение поля или свойства объекта по имени члена.
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

            var setter = GetMemberSetter(instance.GetType(), memberName, out var memberType);
            if (setter == null)
            {
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

            var path = pathToMemberName as string[] ?? pathToMemberName.ToArray();
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
                if (subMemberType == null) return false;
                subMemberInstance = New(subMemberType);
                Set(instance, path[0], subMemberInstance);
            }

            return Set(subMemberInstance, path.Skip(1).ToArray(), value);
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

            switch (memberInfo)
            {
                case PropertyInfo pi:
                    return pi.PropertyType;
                case FieldInfo fi:
                    return fi.FieldType;
                default:
                    return null;
            }
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
                throw new ArgumentException($"{interfaceType} is not an interface", nameof(interfaceType));
            }

            if (implementationType.IsInterface)
            {
                throw new ArgumentException($"{implementationType} cannot be an interface", nameof(implementationType));
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
                return (T)ChangeType(value, typeof(T), formatProvider);
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
                result = (T)ChangeType(value, typeof(T), formatProvider);
                return true;
            }
            catch
            {
                result = default;
                return false;
            }
        }

        /// <summary>
        /// Finds the field by naming patterns.
        /// </summary>
        /// <param name="declaringType">Type of the declaring.</param>
        /// <param name="propertyName">Name of the property.</param>
        /// <returns>FieldInfo.</returns>
        private static FieldInfo FindFieldByNamingPatterns(Type declaringType, string propertyName)
        {
#pragma warning disable S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields
            var property = declaringType.GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
#pragma warning restore S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields

            if (property == null)
            {
                return null;
            }

            // Стандартные шаблоны именования полей
            var possibleFieldNames = new[]
            {
                $"_{char.ToLower(propertyName[0])}{propertyName.Substring(1)}", // _propertyName
                $"m_{propertyName}", // m_PropertyName
                $"_{propertyName}", // _PropertyName
                propertyName, // PropertyName (для публичных полей)
                $"m{char.ToUpper(propertyName[0])}{propertyName.Substring(1)}", // mPropertyName
                $"{propertyName.ToLower()}",
            };

            // Поиск в текущем типе
            foreach (var fieldName in possibleFieldNames)
            {
                var field = declaringType.GetField(fieldName, DefaultBindingFlags);

                if (field != null && field.FieldType == property.PropertyType)
                {
                    return field;
                }
            }

            // Поиск в базовых классах
            var baseType = declaringType.BaseType;
            while (baseType != null && baseType != typeof(object))
            {
                foreach (var fieldName in possibleFieldNames)
                {
                    var field = baseType.GetField(fieldName, DefaultBindingFlags);

                    if (field != null && field.FieldType == property.PropertyType)
                    {
                        return field;
                    }
                }

                baseType = baseType.BaseType;
            }

            return null;
        }

        /// <summary>
        /// Gets the backing field from il.
        /// </summary>
        /// <param name="getter">The getter.</param>
        /// <returns>FieldInfo.</returns>
        private static FieldInfo GetBackingFieldFromIl(MethodInfo getter)
        {
            try
            {
                var methodBody = getter.GetMethodBody();
                if (methodBody == null)
                {
                    return null;
                }

                var ilBytes = methodBody.GetILAsByteArray();
                if (ilBytes.Length == 0)
                {
                    return null;
                }

                // Анализируем IL-байты
                var i = 0;
                while (i < ilBytes.Length)
                {
                    short opCodeValue = ilBytes[i];

                    // Проверяем двухбайтовые опкоды
                    if (opCodeValue == 0xFE && i + 1 < ilBytes.Length)
                    {
                        opCodeValue = (short)((opCodeValue << 8) | ilBytes[i + 1]);
                        i++; // Пропускаем второй байт
                    }

                    if (OpCodes.TryGetValue(opCodeValue, out var opCode))
                    {
                        // Проверяем инструкции загрузки поля
                        if ((opCode == System.Reflection.Emit.OpCodes.Ldfld || opCode == System.Reflection.Emit.OpCodes.Ldsfld ||
                            opCode == System.Reflection.Emit.OpCodes.Ldflda || opCode == System.Reflection.Emit.OpCodes.Ldsflda) && i + 4 < ilBytes.Length)
                        {
                            var token = BitConverter.ToInt32(ilBytes, i + 1);

                            try
                            {
                                var field = getter.Module.ResolveField(token);
                                if (field != null && IsValidBackingField(field, getter.DeclaringType))
                                {
                                    return field;
                                }
                            }
                            catch
                            {
                                // Игнорируем ошибки разрешения токена
                            }
                        }

                        // Пропускаем байты операнда в зависимости от типа операнда
                        i += GetOperandSize(opCode.OperandType, ilBytes, i + 1);
                    }

                    i++;
                }
            }
            catch
            {
                // Игнорируем ошибки анализа IL
            }

            return null;
        }

        /// <summary>
        /// Gets the member information from lambda.
        /// </summary>
        /// <param name="le">The le.</param>
        /// <returns>MemberInfo.</returns>
        private static MemberInfo GetMemberInfoFromLambda(LambdaExpression le)
        {
            var propDeclaringType = le.Type.GenericTypeArguments.FirstOrDefault();
            var pi = GetMemberInfo(le.Body);
            pi = GetProperties(propDeclaringType)
                .FirstOrDefault(x => string.Compare(x.Name, pi?.Name, StringComparison.Ordinal) == 0) ?? pi;
            return pi;
        }

        /// <summary>
        /// Gets the member information from method call.
        /// </summary>
        /// <param name="mce">The mce.</param>
        /// <returns>MemberInfo.</returns>
        private static MemberInfo GetMemberInfoFromMethodCall(MethodCallExpression mce)
        {
            var pi = GetMemberInfo(mce.Arguments[0]);
            return pi;
        }

        /// <summary>
        /// Gets the members internal.
        /// </summary>
        /// <typeparam name="T">Type.</typeparam>
        /// <param name="obj">The object.</param>
        /// <param name="memberFilter">The member filter.</param>
        /// <param name="recursive">if set to <c>true</c> [recursive].</param>
        /// <param name="searchInCollections">if set to <c>true</c> [search in collections].</param>
        /// <param name="visited">The visited.</param>
        /// <returns>IEnumerable&lt;T&gt;.</returns>
        private static IEnumerable<T> GetMembersInternal<T>(object obj, Func<T, bool> memberFilter, bool recursive, bool searchInCollections, HashSet<object> visited)
        {
            if (obj == null)
            {
                yield break;
            }

            var type = obj.GetType();

            // Для примитивов и строк обходим только если тип совпадает с T
            if (type.IsPrimitive || obj is string)
            {
                if (obj is T tValue && (memberFilter == null || memberFilter(tValue)))
                {
                    yield return tValue;
                }

                yield break;
            }

            if (!visited.Add(obj))
            {
                yield break;
            }

            // Если коллекция и нужно искать в коллекциях
            if (searchInCollections && obj is IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    foreach (var nested in GetMembersInternal(item, memberFilter, recursive, true, visited))
                    {
                        yield return nested;
                    }
                }
            }

            // Поля
            var fields = GetFieldsMap(type).Values;
            foreach (var field in fields)
            {
                var value = field.GetValue(obj);
                if (value == null)
                {
                    continue;
                }

                if (value is T tValue && (memberFilter == null || memberFilter(tValue)))
                {
                    yield return tValue;
                }

                if (recursive && !value.GetType().IsPrimitive && !(value is string))
                {
                    foreach (var nested in GetMembersInternal(value, memberFilter, true, searchInCollections, visited))
                    {
                        yield return nested;
                    }
                }
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
                {
                    foreach (var nested in GetMembersInternal(value, memberFilter, true, searchInCollections, visited))
                    {
                        yield return nested;
                    }
                }
            }
        }

        /// <summary>
        /// Gets the size of the operand.
        /// </summary>
        /// <param name="operandType">Type of the operand.</param>
        /// <param name="ilBytes">The il bytes.</param>
        /// <param name="position">The position.</param>
        /// <returns>System.Int32.</returns>
        private static int GetOperandSize(OperandType operandType, byte[] ilBytes, int position)
        {
            switch (operandType)
            {
                case OperandType.InlineBrTarget:
                case OperandType.InlineField:
                case OperandType.InlineI:
                case OperandType.InlineMethod:
                case OperandType.InlineSig:
                case OperandType.InlineString:
                case OperandType.InlineTok:
                case OperandType.InlineType:
                    return 4;

                case OperandType.InlineI8:
                case OperandType.InlineR:
                    return 8;

                case OperandType.InlineSwitch:
                    if (position + 4 <= ilBytes.Length)
                    {
                        var count = BitConverter.ToInt32(ilBytes, position);
                        return 4 + (count * 4);
                    }

                    return 0;

                case OperandType.InlineVar:
                    return 2;

                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineR:
                case OperandType.ShortInlineVar:
                    return 1;

                default:
                    return 0;
            }
        }

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
        /// Determines whether [is valid backing field] [the specified field].
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="declaringType">Type of the declaring.</param>
        /// <returns><c>true</c> if [is valid backing field] [the specified field]; otherwise, <c>false</c>.</returns>
        private static bool IsValidBackingField(FieldInfo field, Type declaringType)
        {
            if (field == null)
            {
                return false;
            }

            // Поле должно быть приватным (или защищенным для базовых классов)
            if (!field.IsPrivate && !field.IsFamily && !field.IsAssembly && !field.IsFamilyOrAssembly)
            {
                return false;
            }

            // Поле должно принадлежать этому типу или его базовому типу
            Debug.Assert(field.DeclaringType != null, "field.DeclaringType != null");
            if (!declaringType.IsAssignableFrom(field.DeclaringType) &&
                !field.DeclaringType.IsAssignableFrom(declaringType))
            {
                return false;
            }

            return true;
        }

        private static Dictionary<short, OpCode> InitializeOpCodes()
        {
            var dict = new Dictionary<short, OpCode>();
            var fields = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static);
            foreach (var field in fields)
            {
                if (field.FieldType == typeof(OpCode))
                {
                    var opCode = (OpCode)field.GetValue(null);
                    dict[opCode.Value] = opCode;
                }
            }

            return dict;
        }
    }
}