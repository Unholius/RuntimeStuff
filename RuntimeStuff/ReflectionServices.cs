// <copyright file="ReflectionServices.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace StandardExtensions
{
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
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Xml.Linq;

    /// <summary>
    /// RS_19.05.2026<br/>
    /// Предоставляет высокопроизводительные вспомогательные методы для:
    /// <list type="bullet">
    /// <item>
    /// <description>работы с типами и reflection metadata;</description>
    /// </item>
    /// <item>
    /// <description>поиска и получения свойств, полей, методов, событий и атрибутов;</description>
    /// </item>
    /// <item>
    /// <description>динамического чтения и записи значений членов объекта;</description>
    /// </item>
    /// <item>
    /// <description>быстрого создания getter/setter/accessor делегатов через DynamicMethod и Expression;</description>
    /// </item>
    /// <item>
    /// <description>конвертации значений между типами;</description>
    /// </item>
    /// <item>
    /// <description>анализа коллекций, generic-типов и интерфейсов;</description>
    /// </item>
    /// <item>
    /// <description>кэширования reflection-операций для уменьшения накладных расходов runtime.</description>
    /// </item>
    /// </list>
    /// <para>
    /// Класс использует внутренние concurrent-кэши для ускорения повторных reflection-операций.
    /// Большинство методов являются thread-safe для чтения.
    /// </para>
    /// <para>
    /// Поддерживаются:
    /// <list type="bullet">
    /// <item>
    /// <description>public/private members;</description>
    /// </item>
    /// <item>
    /// <description>instance/static members;</description>
    /// </item>
    /// <item>
    /// <description>nullable-типы;</description>
    /// </item>
    /// <item>
    /// <description>enum-конверсии;</description>
    /// </item>
    /// <item>
    /// <description>generic collections;</description>
    /// </item>
    /// <item>
    /// <description>кастомные type converters;</description>
    /// </item>
    /// <item>
    /// <description>быстрое создание reflection delegate accessor'ов.</description>
    /// </item>
    /// </list>
    /// </para>
    /// <para>
    /// Некоторые low-level методы используют:
    /// <list type="bullet">
    /// <item>
    /// <description><see cref="System.Reflection.Emit.DynamicMethod"/>;</description>
    /// </item>
    /// <item>
    /// <description>IL generation;</description>
    /// </item>
    /// <item>
    /// <description><see cref="TypedReference"/>;</description>
    /// </item>
    /// <item>
    /// <description>Expression compilation.</description>
    /// </item>
    /// </list>
    /// </para>
    /// <para>
    /// Следует учитывать, что операции с boxed struct, dynamic assemblies,
    /// AssemblyLoadContext и runtime-generated типами могут иметь ограничения
    /// или требовать очистки внутренних кэшей.
    /// </para>
    /// </summary>
    public static class ReflectionServices
    {
        private static readonly BindingFlags AllBindingFlags =
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.Instance |
            BindingFlags.Static |
            BindingFlags.FlattenHierarchy;

        private static readonly ConcurrentDictionary<Assembly, Type[]> AssemblyTypesCache = new();

        private static readonly ConcurrentDictionary<ConstructorInfo, Func<object[], object>> ConstructorInvokersCache = new();
        private static readonly ConcurrentDictionary<Type, ConcurrentDictionary<Type, Func<object, object>>> CustomTypeConverters = new();

        private static readonly string[] DateFormats =
[
            "yyyy-MM-dd",
            "yyyy.MM.dd",
            "yyyy/MM/dd",
            "yyyyMMdd",

            "dd-MM-yyyy",
            "dd.MM.yyyy",
            "dd/MM/yyyy",
            "ddMMyyyy",

            "dd-MM-yy",
            "dd.MM.yy",
            "dd/MM/yy",
            "ddMMyy",

            "yyyy-MM-dd HH:mm:ss.fff",
            "yyyy.MM.dd HH:mm:ss.fff",
            "yyyy/MM/dd HH:mm:ss.fff",
            "yyyyMMdd HH:mm:ss.fff",

            "dd-MM-yyyy HH:mm:ss.fff",
            "dd.MM.yyyy HH:mm:ss.fff",
            "dd/MM/yyyy HH:mm:ss.fff",
            "ddMMyyyy HH:mm:ss.fff",

            "dd-MM-yy HH:mm:ss.fff",
            "dd.MM.yy HH:mm:ss.fff",
            "dd/MM/yy HH:mm:ss.fff",
            "ddMMyy HH:mm:ss.fff",

            "yyyy-MM-ddTHH:mm:ss.fff",
            "yyyy.MM.ddTHH:mm:ss.fff",
            "yyyy/MM/ddTHH:mm:ss.fff",
            "yyyyMMddTHH:mm:ss.fff",

            "dd-MM-yyyyTHH:mm:ss.fff",
            "dd.MM.yyyyTHH:mm:ss.fff",
            "dd/MM/yyyyTHH:mm:ss.fff",
            "ddMMyyyyTHH:mm:ss.fff",

            "dd-MM-yyTHH:mm:ss.fff",
            "dd.MM.yyTHH:mm:ss.fff",
            "dd/MM/yyTHH:mm:ss.fff",
            "ddMMyyTHH:mm:ss.fff",
        ];

        private static readonly ConcurrentDictionary<Type, Func<object>> DefaultConstructorCache = new();

        private static readonly ConcurrentDictionary<FieldInfo, Func<object, object>> FieldGettersCache = new();

        private static readonly ConcurrentDictionary<FieldInfo, Action<object, object>> FieldSettersCache = new();

        private static readonly ConcurrentDictionary<MemberInfo, Attribute[]> MemberAttributesCache = new();

        private static readonly Dictionary<short, OpCode> OpCodes = InitializeOpCodes();

        private static readonly BindingFlags PrivateBindingFlags =
                                                    BindingFlags.NonPublic |
            BindingFlags.Instance |
            BindingFlags.Static |
            BindingFlags.FlattenHierarchy;

        private static readonly ConcurrentDictionary<PropertyInfo, Func<object, object>> PropertyGettersCache = new();

        private static readonly ConcurrentDictionary<PropertyInfo, Action<object, object>> PropertySettersCache = new();

        private static readonly BindingFlags PublicBindingFlags =
                            BindingFlags.Public |
            BindingFlags.Instance |
            BindingFlags.Static |
            BindingFlags.FlattenHierarchy; // Позволяет видеть статические члены из базовых классов

        private static readonly ConcurrentDictionary<string, Type> TypeByFullNameCache = new();
        private static readonly ConcurrentDictionary<Type, TypeCode> TypeCodeCache = new();
        private static readonly ConcurrentDictionary<Type, ConstructorInfo[]> TypeConstructorsCache = new();
        private static readonly ConcurrentDictionary<(Type From, Type To), Func<object, object>> TypeConverterCache = new();
        private static readonly ConcurrentDictionary<Type, EventInfo[]> TypeEventsCache = new();
        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> TypeIndexersCache = new();
        private static readonly ConcurrentDictionary<Type, MethodInfo[]> TypeMethodsCache = new();
        private static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, FieldInfo>> TypePrivateFieldsCache = new();
        private static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, PropertyInfo>> TypePrivatePropertiesCache = new();
        private static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, FieldInfo>> TypePublicFieldsCache = new();
        private static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, PropertyInfo>> TypePublicPropertiesCache = new();

        static ReflectionServices()
        {
            IntNumberTypes =
            [
                typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(short), typeof(ushort), typeof(byte),
                typeof(sbyte),
                typeof(int?), typeof(uint?), typeof(long?), typeof(ulong?), typeof(short?), typeof(ushort?),
                typeof(byte?),
                typeof(sbyte?),
            ];

            FloatNumberTypes =
            [
                typeof(float), typeof(double), typeof(decimal),
                typeof(float?), typeof(double?), typeof(decimal?),
            ];

            NumberTypes = [.. IntNumberTypes.Concat(FloatNumberTypes)];

            BoolTypes =
            [
                typeof(bool),
                typeof(bool?),
            ];

            BasicTypes =
            [
                ..new[]
                    {
                        typeof(object),
                        typeof(char), typeof(char?), typeof(string),
                        typeof(DateTime), typeof(DateTime?),
                        typeof(TimeSpan), typeof(TimeSpan?),
                        typeof(Guid), typeof(Guid?),
                        typeof(Uri),
                        typeof(Enum),
                    }.Concat(NumberTypes)
                    .Concat(BoolTypes)
            ];
        }

        /// <summary>
        /// Набор основных типов: object, char, char?, string, DateTime, DateTime?, TimeSpan, TimeSpan?, Guid, Guid?, Uri, Enum, <see cref="NumberTypes"/>, <see cref="BoolTypes"/>.
        /// </summary>
        /// <value>The basic types.</value>
        public static HashSet<Type> BasicTypes { get; }

        /// <summary>
        /// Типы, представляющие логические значения.
        /// </summary>
        /// <value>The bool types.</value>
        public static HashSet<Type> BoolTypes { get; }

        /// <summary>
        /// Типы, представляющие дату и время.
        /// </summary>
        /// <value>The date types.</value>
        public static HashSet<Type> DateTypes { get; } =
        [
            typeof(DateTime), typeof(DateTime?),
        ];

        /// <summary>
        /// Типы с плавающей запятой (float, double, decimal).
        /// </summary>
        /// <value>The float number types.</value>
        public static HashSet<Type> FloatNumberTypes { get; }

        /// <summary>
        /// Сопоставление интерфейсов с конкретными реализациями, используемыми при создании экземпляров.
        /// </summary>
        public static Dictionary<Type, Type> InterfaceToInstanceMap { get; } = new Dictionary<Type, Type>
        {
            { typeof(IEnumerable), typeof(List<object>) },
            { typeof(IEnumerable<>), typeof(List<>) },
            { typeof(ICollection), typeof(ObservableCollection<object>) },
            { typeof(ICollection<>), typeof(ObservableCollection<>) },
            { typeof(IDictionary<,>), typeof(Dictionary<,>) },
        };

        /// <summary>
        /// Целочисленные типы (byte, int, long и т.д. с nullable и без).
        /// </summary>
        /// <value>The int number types.</value>
        public static HashSet<Type> IntNumberTypes { get; }

        /// <summary>
        /// Значения, трактуемые как null (null, DBNull, NaN).
        /// </summary>
        /// <value>Значения, которые считать как null.</value>
        public static HashSet<object> NullValues { get; } = [null, DBNull.Value, double.NaN, float.NaN];

        /// <summary>
        /// Объединение массивов <see cref="IntNumberTypes"/> и <see cref="FloatNumberTypes"/>.
        /// </summary>
        /// <value>Числовые типы.</value>
        public static HashSet<Type> NumberTypes { get; }

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
        /// Преобразует значение к указанному типу <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Тип результата преобразования.</typeparam>
        /// <param name="value">Исходное значение.</param>
        /// <param name="provider">
        /// Провайдер форматирования, используемый при преобразовании.
        /// </param>
        /// <returns>Преобразованное значение.</returns>
        /// <exception cref="Exception">
        /// Может выбрасывать исключения, связанные с невозможностью преобразования.
        /// </exception>
        public static T ChangeType<T>(this object value, IFormatProvider provider = null) => (T)ChangeType(value, typeof(T), provider);

        /// <summary>
        /// Преобразует значение к указанному типу.
        /// </summary>
        /// <param name="value">Исходное значение.</param>
        /// <param name="conversionType">Целевой тип преобразования.</param>
        /// <param name="provider">
        /// Провайдер форматирования, используемый при преобразовании.
        /// </param>
        /// <returns>Преобразованное значение.</returns>
        /// <exception cref="ArgumentNullException">
        /// Выбрасывается, если <paramref name="conversionType"/> равен <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidCastException">
        /// Выбрасывается, если преобразование невозможно.
        /// </exception>
        /// <exception cref="FormatException">
        /// Выбрасывается при неверном формате исходного значения.
        /// </exception>
        /// <exception cref="OverflowException">
        /// Выбрасывается при переполнении целевого типа.
        /// </exception>
        public static object ChangeType(this object value, Type conversionType, IFormatProvider provider = null)
        {
            if (value == null || ReferenceEquals(value, DBNull.Value))
            {
                return null;
            }

            provider ??= CultureInfo.InvariantCulture;
            conversionType = Nullable.GetUnderlyingType(conversionType) ?? conversionType;

            if (value is IConvertible ic)
            {
                if (ReferenceEquals(conversionType, typeof(bool)))
                {
                    return ic.ToBoolean(provider);
                }

                if (ReferenceEquals(conversionType, typeof(char)))
                {
                    return ic.ToChar(provider);
                }

                if (ReferenceEquals(conversionType, typeof(sbyte)))
                {
                    return ic.ToSByte(provider);
                }

                if (ReferenceEquals(conversionType, typeof(byte)))
                {
                    return ic.ToByte(provider);
                }

                if (ReferenceEquals(conversionType, typeof(short)))
                {
                    return ic.ToInt16(provider);
                }

                if (ReferenceEquals(conversionType, typeof(ushort)))
                {
                    return ic.ToUInt16(provider);
                }

                if (ReferenceEquals(conversionType, typeof(int)))
                {
                    return ic.ToInt32(provider);
                }

                if (ReferenceEquals(conversionType, typeof(uint)))
                {
                    return ic.ToUInt32(provider);
                }

                if (ReferenceEquals(conversionType, typeof(long)))
                {
                    return ic.ToInt64(provider);
                }

                if (ReferenceEquals(conversionType, typeof(ulong)))
                {
                    return ic.ToUInt64(provider);
                }

                if (ReferenceEquals(conversionType, typeof(float)))
                {
                    return ic.ToSingle(provider);
                }

                if (ReferenceEquals(conversionType, typeof(double)))
                {
                    return ic.ToDouble(provider);
                }

                if (ReferenceEquals(conversionType, typeof(decimal)))
                {
                    return ic.ToDecimal(provider);
                }

                if (ReferenceEquals(conversionType, typeof(DateTime)))
                {
                    return ic.ToDateTime(provider);
                }

                if (ReferenceEquals(conversionType, typeof(string)))
                {
                    return ic.ToString(provider);
                }

                if (ReferenceEquals(conversionType, typeof(object)))
                {
                    return (object)value;
                }
            }

            var fromType = value.GetType();
            var customConverter = GetCustomTypeConverter(fromType, conversionType);
            if (customConverter != null)
            {
                return customConverter(value);
            }

            // direct hit
            if (conversionType == typeof(object))
            {
                return value;
            }

            if (conversionType == typeof(string))
            {
                return value.ToString();
            }

            if (fromType == conversionType || conversionType.IsAssignableFrom(fromType))
            {
                return value;
            }

            // enum fast path
            if (conversionType.IsEnum)
            {
                return ToEnum(value, fromType, conversionType, provider);
            }

            // string source path
            if (value is string s)
            {
                return FromString(s, conversionType, provider);
            }

            // bool numeric compatibility
            if (fromType == typeof(bool))
            {
                if (conversionType == typeof(byte))
                {
                    return (byte)((bool)value ? 1 : 0);
                }

                if (conversionType == typeof(short))
                {
                    return (short)((bool)value ? 1 : 0);
                }

                if (conversionType == typeof(int))
                {
                    return (bool)value ? 1 : 0;
                }

                if (conversionType == typeof(long))
                {
                    return (bool)value ? 1L : 0L;
                }
            }

            // typed primitive fast path
            switch (GetTypeCodeCached(conversionType))
            {
                case TypeCode.Boolean:
                    {
                        return value is bool b ? b : value is string str ? bool.Parse(str) :
                            Convert.ToBoolean(value, provider);
                    }

                case TypeCode.Byte:
                    {
                        return value is byte bt ? bt : value is string str ? byte.Parse(str, NumberStyles.Any, provider) :
                            Convert.ToByte(value, provider);
                    }

                case TypeCode.Int16:
                    {
                        return value is short sh ? sh : value is string str ? short.Parse(str, NumberStyles.Any, provider) :
                            Convert.ToInt16(value, provider);
                    }

                case TypeCode.Int32:
                    {
                        return value is int i ? i : value is string str ? int.Parse(str, NumberStyles.Any, provider) :
                            Convert.ToInt32(value, provider);
                    }

                case TypeCode.Int64:
                    {
                        return value is long l ? l : value is string str ? long.Parse(str, NumberStyles.Any, provider) :
                            Convert.ToInt64(value, provider);
                    }

                case TypeCode.Single:
                    {
                        return value is float f ? f : value is string str ? float.Parse(str, NumberStyles.Any, provider) :
                            Convert.ToSingle(value, provider);
                    }

                case TypeCode.Double:
                    {
                        return value is double d ? d : value is string str ? double.Parse(str, NumberStyles.Any, provider) :
                            Convert.ToDouble(value, provider);
                    }

                case TypeCode.Decimal:
                    {
                        return value is decimal dec ? dec : value is string str ? decimal.Parse(str, NumberStyles.Any, provider) :
                            Convert.ToDecimal(value, provider);
                    }

                case TypeCode.UInt16:
                    {
                        return value is ushort us ? us : value is string str ? ushort.Parse(str, NumberStyles.Any, provider) :
                            Convert.ToUInt16(value, provider);
                    }

                case TypeCode.UInt32:
                    {
                        return value is uint ui ? ui : value is string str ? uint.Parse(str, NumberStyles.Any, provider) :
                            Convert.ToUInt32(value, provider);
                    }

                case TypeCode.UInt64:
                    {
                        return value is ulong ul ? ul : value is string str ? ulong.Parse(str, NumberStyles.Any, provider) :
                            Convert.ToUInt64(value, provider);
                    }

                case TypeCode.Char:
                    {
                        return value is char ch ? ch : value is string str ? char.Parse(str) :
                            Convert.ToChar(value, provider);
                    }

                case TypeCode.DateTime:
                    {
                        return value is DateTime dt ? dt : value is string str ? DateTime.Parse(str, provider) :
                            Convert.ToDateTime(value, provider);
                    }

                case TypeCode.String:
                    {
                        return value.ToString();
                    }
            }

            // Guid
            if (conversionType == typeof(Guid))
            {
                if (value is byte[] bytes)
                {
                    return new Guid(bytes);
                }

                return Guid.Parse(value.ToString());
            }

            // TimeSpan
            if (conversionType == typeof(TimeSpan))
            {
                if (value is long ticks)
                {
                    return new TimeSpan(ticks);
                }

                return TimeSpan.Parse(value.ToString(), provider);
            }

            if (value is IConvertible c)
            {
                return c.ToType(conversionType, provider);
            }

            // fallback
            return Convert.ChangeType(value, conversionType, provider);
        }

        /// <summary>
        /// Очистка внутренних кэшей.
        /// </summary>
        public static void ClearCaches()
        {
            AssemblyTypesCache.Clear();
            ConstructorInvokersCache.Clear();
            DefaultConstructorCache.Clear();
            FieldGettersCache.Clear();
            FieldSettersCache.Clear();
            PropertyGettersCache.Clear();
            PropertySettersCache.Clear();
            MemberAttributesCache.Clear();
            TypeByFullNameCache.Clear();
            TypeCodeCache.Clear();
            TypeConstructorsCache.Clear();
            TypeConverterCache.Clear();
            TypeEventsCache.Clear();
            TypeIndexersCache.Clear();
            TypeMethodsCache.Clear();
            TypePrivateFieldsCache.Clear();
            TypePrivatePropertiesCache.Clear();
            TypePublicFieldsCache.Clear();
            TypePublicPropertiesCache.Clear();
        }

        /// <summary>
        /// Очищает все зарегистрированные пользовательские конвертеры типов.
        /// </summary>
        public static void ClearCustomTypeConverters() => CustomTypeConverters.Clear();

        /// <summary>
        /// Возвращает делегат, создающий экземпляр указанного типа с использованием конструктора по умолчанию
        /// или первого доступного конструктора с аргументами по умолчанию.
        /// </summary>
        /// <param name="type">Тип, для которого требуется получить фабрику создания экземпляра.</param>
        /// <returns>
        /// Делегат <see cref="Func{Object}"/>, создающий новый экземпляр типа,
        /// либо <see langword="null"/>, если конструкторы отсутствуют.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Результат кэшируется для повторного использования.
        /// </para>
        /// <para>
        /// Поведение:
        /// <list type="bullet">
        /// <item>
        /// Для значимых типов (<see cref="Type.IsValueType"/>) создаётся выражение,
        /// возвращающее значение по умолчанию.
        /// </item>
        /// <item>
        /// Для ссылочных типов ищется конструктор без параметров.
        /// Если он отсутствует — используется первый доступный конструктор.
        /// </item>
        /// <item>
        /// Для параметризованных конструкторов аргументы инициализируются значениями по умолчанию:
        /// для значимых типов — через <see cref="Activator.CreateInstance(Type)"/>,
        /// для ссылочных — <see langword="null"/>.
        /// </item>
        /// </list>
        /// </para>
        /// </remarks>
        public static Func<object> GetActivator(this Type type)
        {
            return DefaultConstructorCache.GetOrAdd(type, x =>
            {
                if (type.IsValueType)
                {
                    var b = Expression.Convert(
                        Expression.Default(type),
                        typeof(object));

                    return Expression.Lambda<Func<object>>(b).Compile();
                }

                const BindingFlags flags =
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                var ctors = x.GetConstructors(flags);

                if (ctors.Length == 0)
                {
                    return null;
                }

                var ctor = ctors.FirstOrDefault(c => c.GetParameters().Length == 0)
                           ?? ctors.First();

                var parameters = ctor.GetParameters();

                // аргументы по умолчанию
                var defaultArgs = parameters
                    .Select(p => p.ParameterType.IsValueType
                        ? Activator.CreateInstance(p.ParameterType)
                        : null)
                    .ToArray();

                // () => new T(defaultArgs...)
                var argsExpr = parameters
                    .Select((p, i) =>
                        Expression.Constant(defaultArgs[i], p.ParameterType))
                    .ToArray();

                var newExpr = Expression.New(ctor, argsExpr);
                var body = Expression.Convert(newExpr, typeof(object));

                return Expression.Lambda<Func<object>>(body).Compile();
            });
        }

        /// <summary>
        /// Получает первый атрибут указанного типа, применённые к указанному свойству по имени, используя внутренний кэш для ускорения повторных вызовов.
        /// </summary>
        /// <typeparam name="T">Тип, в котором искать свойство.</typeparam>
        /// <param name="type">Тип.</param>
        /// <param name="propertyName">Имя свойства в типе.</param>
        /// <returns>Массив атрибутов.</returns>
        public static T GetAttribute<T>(this Type type, string propertyName)
            where T : Attribute
            => GetAttributes(type, propertyName).OfType<T>().FirstOrDefault();

        /// <summary>
        /// Получает первый атрибут указанного типа, применённые к указанному свойству по имени, используя внутренний кэш для ускорения повторных вызовов.
        /// </summary>
        /// <param name="type">Тип.</param>
        /// <param name="propertyName">Имя свойства в типе.</param>
        /// <param name="attributeTypeName">Имя типа атрибута. Можно указывать без окончания Attribute.</param>
        /// <returns>Массив атрибутов.</returns>
        public static Attribute GetAttribute(this Type type, string propertyName, string attributeTypeName)
        {
            var expectedName = attributeTypeName.EndsWith("Attribute", StringComparison.Ordinal) ? attributeTypeName : attributeTypeName + "Attribute";
            var attributes = GetAttributes(type, propertyName);

            for (var i = 0; i < attributes.Length; i++)
            {
                var attr = attributes[i];

                if (string.Equals(attr.GetType().Name, expectedName, StringComparison.Ordinal))
                {
                    return attr;
                }
            }

            return null;
        }

        /// <summary>
        /// Получает первый атрибут указанного типа, применённые к указанному типу, используя внутренний кэш для ускорения повторных вызовов.
        /// </summary>
        /// <typeparam name="T">Тип, в котором искать атрибуты.</typeparam>
        /// <param name="type">Тип.</param>
        /// <returns>Массив атрибутов.</returns>
        public static T GetAttribute<T>(this Type type)
            where T : Attribute
            => GetAttributes(type).OfType<T>().FirstOrDefault();

        /// <summary>
        /// Получает первый атрибут указанного типа, применённые к указанному свойству по имени, используя внутренний кэш для ускорения повторных вызовов.
        /// </summary>
        /// <param name="type">Тип.</param>
        /// <param name="attributeTypeName">Имя типа атрибута. Можно указывать без окончания Attribute.</param>
        /// <returns>Массив атрибутов.</returns>
        public static Attribute GetAttribute(this Type type, string attributeTypeName)
        {
            var expectedName = attributeTypeName.EndsWith("Attribute", StringComparison.Ordinal) ? attributeTypeName : attributeTypeName + "Attribute";
            var attributes = GetAttributes(type);

            for (var i = 0; i < attributes.Length; i++)
            {
                var attr = attributes[i];

                if (string.Equals(attr.GetType().Name, expectedName, StringComparison.Ordinal))
                {
                    return attr;
                }
            }

            return null;
        }

        /// <summary>
        /// Получает все атрибуты, применённые к указанному типу, используя внутренний кэш для ускорения повторных вызовов.
        /// </summary>
        /// <typeparam name="T">Тип, для которого нужно получить атрибуты.</typeparam>
        /// <returns>Массив атрибутов.</returns>
        public static Attribute[] GetAttributes<T>()
            => GetAttributes(typeof(T));

        /// <summary>
        /// Получает все атрибуты, применённые к указанному свойству по имени, используя внутренний кэш для ускорения повторных вызовов.
        /// </summary>
        /// <typeparam name="T">Тип, в котором искать свойство.</typeparam>
        /// <param name="propertyName">Имя свойства в типе.</param>
        /// <returns>Массив атрибутов.</returns>
        public static Attribute[] GetAttributes<T>(string propertyName)
        {
            return GetAttributes(typeof(T), propertyName);
        }

        /// <summary>
        /// Получает все атрибуты, применённые к указанному свойству по имени, используя внутренний кэш для ускорения повторных вызовов.
        /// </summary>
        /// <param name="type">Тип.</param>
        /// <param name="propertyName">Имя свойства в типе.</param>
        /// <returns>Массив атрибутов.</returns>
        public static Attribute[] GetAttributes(this Type type, string propertyName)
        {
            var p = GetProperty(type, propertyName);
            if (p == null)
            {
                throw new NullReferenceException($"Не найдено свойство '{propertyName}' в типе '{type.FullName ?? type.Name}'");
            }

            return GetAttributes(p);
        }

        /// <summary>
        /// Получает все атрибуты, применённые к указанному типу, используя внутренний кэш для ускорения повторных вызовов.
        /// </summary>
        /// <param name="memberInfo"><see cref="MemberInfo"/>.</param>
        /// <returns>Массив атрибутов.</returns>
        public static Attribute[] GetAttributes(this MemberInfo memberInfo)
        {
            return MemberAttributesCache.GetOrAdd(memberInfo, t => t.GetCustomAttributes(true).Cast<Attribute>().ToArray());
        }

        /// <summary>
        /// Получает цепочку базовых типов и/или интерфейсов без кэширования.
        /// </summary>
        /// <param name="type">Тип, для которого нужно получить базовые типы.</param>
        /// <param name="includeThis">Включать ли текущий тип в результат.</param>
        /// <param name="includeInterfaces">Включать ли интерфейсы в результат.</param>
        /// <param name="includeAbstract">Включать ли абстрактные классы в результат.</param>
        /// <returns>Массив базовых типов и/или интерфейсов.</returns>
        public static Type[] GetBaseTypes(
            this Type type,
            bool includeThis = false,
            bool includeInterfaces = false,
            bool includeAbstract = true)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            var baseTypes = new List<Type>();

            var baseType = type;

            while (baseType.BaseType != null &&
                   baseType.BaseType != typeof(object))
            {
                baseType = baseType.BaseType;

                if (includeAbstract || !baseType.IsAbstract)
                {
                    baseTypes.Add(baseType);
                }
            }

            if (includeThis &&
                (includeAbstract || !type.IsAbstract))
            {
                baseTypes.Add(type);
            }

            if (includeInterfaces)
            {
                foreach (var i in type.GetInterfaces())
                {
                    if (includeAbstract || !i.IsAbstract)
                    {
                        baseTypes.Add(i);
                    }
                }
            }

            return [.. baseTypes];
        }

        /// <summary>
        /// Определяет тип элемента коллекции для указанного типа.
        /// </summary>
        /// <param name="type">
        /// Тип, для которого необходимо определить тип элемента коллекции.
        /// </param>
        /// <returns>
        /// Тип элемента коллекции:
        /// <list type="bullet">
        /// <item>
        /// <description>
        /// Для массива — тип элемента массива.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// Для <c>IDictionary&lt;TKey, TValue&gt;</c> — тип значения (<c>TValue</c>).
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// Для <c>IEnumerable&lt;T&gt;</c> — тип элемента перечисления (<c>T</c>).
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// Для <c>string</c> — <c>char</c>.
        /// </description>
        /// </item>
        /// </list>
        /// Если тип не является коллекцией или равен <c>null</c>, возвращается <c>null</c>.
        /// </returns>
        /// <remarks>
        /// Метод анализирует реализуемые интерфейсы типа для поиска
        /// обобщённых интерфейсов <c>IDictionary&lt;TKey, TValue&gt;</c>
        /// и <c>IEnumerable&lt;T&gt;</c>.
        /// Приоритет проверки следующий:
        /// <c>string</c>, массив, словарь, затем перечисление.
        /// </remarks>
        public static Type GetCollectionItemType(this Type type)
        {
            if (type == null)
            {
                return null;
            }

            if (type == typeof(string))
            {
                return typeof(char);
            }

            if (type.IsArray)
            {
                return type.GetElementType();
            }

            var interfaces = type.GetInterfaces();

            Type enumerableGeneric = null;
            var hasNonGenericEnumerable = false;

            for (var i = 0; i < interfaces.Length; i++)
            {
                var iType = interfaces[i];

                if (iType.IsGenericType)
                {
                    var def = iType.GetGenericTypeDefinition();

                    if (def == typeof(IDictionary<,>))
                    {
                        // TValue
                        var kvp = typeof(KeyValuePair<,>);
                        var returnType = kvp.MakeGenericType(iType.GetGenericArguments()[0], iType.GetGenericArguments()[1]);
                        return returnType;
                    }

                    if (def == typeof(IEnumerable<>))
                    {
                        // запоминаем, но не выходим — вдруг есть IDictionary
                        enumerableGeneric = iType;
                    }
                }
                else if (iType == typeof(IEnumerable))
                {
                    hasNonGenericEnumerable = true;
                }
            }

            if (enumerableGeneric != null)
            {
                return enumerableGeneric.GetGenericArguments()[0];
            }

            if (hasNonGenericEnumerable)
            {
                var props = GetPublicProperties(type);
                PropertyInfo indexer = null;

                foreach (var p in props)
                {
                    if (p.PropertyType == typeof(object))
                    {
                        continue;
                    }

                    if (p.GetIndexParameters().Length > 0)
                    {
                        if (indexer != null)
                        {
                            // больше одного индексатора — невалидно
                            return null;
                        }

                        indexer = p;
                    }
                }

                if (indexer != null)
                {
                    return indexer.PropertyType;
                }
            }

            return null;
        }

        /// <summary>
        /// Получает делегат для вызова конструктора, представленного <see cref="ConstructorInfo"/>.
        /// </summary>
        /// <param name="ctor">Конструктор, для которого нужно создать делегат.</param>
        /// <returns>Делегат для вызова конструктора.</returns>
        public static Func<object[], object> GetConstructorInvoker(this ConstructorInfo ctor)
        {
            return ConstructorInvokersCache.GetOrAdd(ctor, (x) =>
            {
                var argsParam = Expression.Parameter(typeof(object[]), "args");

                var ctorArgs = x.GetParameters()
                    .Select((p, i) =>
                        Expression.Convert(
                            Expression.ArrayIndex(argsParam, Expression.Constant(i)),
                            p.ParameterType))
                    .ToArray<Expression>();

                var newExpr = Expression.New(x, ctorArgs);

                var body = Expression.Convert(newExpr, typeof(object));

                return Expression
                    .Lambda<Func<object[], object>>(body, argsParam)
                    .Compile();
            });
        }

        /// <summary>
        /// Получает все конструкторы, применённые к указанному типу, используя внутренний кэш для ускорения повторных вызовов.
        /// </summary>
        /// <typeparam name="T">Тип, для которого нужно получить конструкторы.</typeparam>
        /// <returns>Массив конструкторов.</returns>
        public static ConstructorInfo[] GetConstructors<T>()
            => GetConstructors(typeof(T));

        /// <summary>
        /// Получает все конструкторы, применённые к указанному типу, используя внутренний кэш для ускорения повторных вызовов.
        /// </summary>
        /// <param name="type"><see cref="Type"/>.</param>
        /// <returns>Массив конструкторов.</returns>
        public static ConstructorInfo[] GetConstructors(this Type type)
        {
            return TypeConstructorsCache.GetOrAdd(type, t => t.GetConstructors(AllBindingFlags).OrderBy(x => x.Name).ThenBy(x => x.GetParameters().Length).ToArray());
        }

        /// <summary>
        /// Возвращает делегат для конвертации объекта из одного типа в другой, используя встроенные механизмы преобразования .NET (например, IConvertible).
        /// </summary>
        /// <param name="fromType">Тип объекта, из которого происходит конвертация.</param>
        /// <param name="toType">Тип объекта, в который происходит конвертация.</param>
        /// <returns>Делегат для конвертации объектов.</returns>
        public static Func<object, object> GetConverter(this Type fromType, Type toType)
        {
            if (fromType == null)
            {
                throw new ArgumentNullException(nameof(fromType));
            }

            if (toType == null)
            {
                throw new ArgumentNullException(nameof(toType));
            }

            return TypeConverterCache.GetOrAdd((fromType, toType), static key =>
            {
                var from = key.From;
                var to = key.To;

                // identity
                if (to.IsAssignableFrom(from))
                {
                    return static x => x;
                }

                var input = Expression.Parameter(typeof(object), "x");

                var body = BuildExpression(input, from, to);

                return Expression
                    .Lambda<Func<object, object>>(body, input)
                    .Compile();
            });
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
        /// Получает делегат для прямой установки значения поля, представленного <see cref="FieldInfo"/>.
        /// </summary>
        /// <param name="fi">Поле, для которого нужно создать делегат.</param>
        /// <returns>Делегат для установки значения поля.</returns>
        public static Action<object, object> GetDirectFieldSetter(this FieldInfo fi) => (instance, value) =>
        {
            var tr = __makeref(instance);
            fi.SetValueDirect(tr, value);
        };

        /// <summary>
        /// Получает все события, применённые к указанному типу, используя внутренний кэш для ускорения повторных вызовов.
        /// </summary>
        /// <typeparam name="T">Тип, для которого нужно получить события.</typeparam>
        /// <returns>Массив событий.</returns>
        public static EventInfo[] GetEvents<T>()
            => GetEvents(typeof(T));

        /// <summary>
        /// Получает все события, применённые к указанному типу, используя внутренний кэш для ускорения повторных вызовов.
        /// </summary>
        /// <param name="type"><see cref="Type"/>.</param>
        /// <returns>Массив событий.</returns>
        public static EventInfo[] GetEvents(this Type type)
        {
            return TypeEventsCache.GetOrAdd(type, t => t.GetEvents(AllBindingFlags));
        }

        /// <summary>
        /// Возвращает первое найденное поле через <see cref="GetPublicField(Type, string, StringComparison)"/> или <see cref="GetPrivateField(Type, string, StringComparison)"/>.
        /// </summary>
        /// <param name="type">Тип в котором искать поле.</param>
        /// <param name="name">Имя поля.</param>
        /// <param name="comparison">Правило сравнения строк.</param>
        /// <returns>FieldInfo.</returns>
        public static FieldInfo GetField(this Type type, string name, StringComparison comparison = StringComparison.Ordinal)
        {
            return
                GetPublicField(type, name, comparison) ??
                GetPrivateField(type, name, comparison);
        }

        /// <summary>
        /// Получает делегат для получения значения поля, представленного <see cref="FieldInfo"/>. Делегат создается динамически с помощью <see cref="DynamicMethod"/> и IL-кода, что позволяет обходить ограничения обычного рефлексивного вызова.
        /// </summary>
        /// <param name="fi">Поле, для которого нужно создать делегат.</param>
        /// <returns>Делегат для получения значения поля.</returns>
        public static Func<object, object> GetFieldGetter(this FieldInfo fi)
        {
            return FieldGettersCache.GetOrAdd(fi, (x) =>
            {
                try
                {
                    if (x == null)
                    {
                        throw new ArgumentNullException(nameof(x));
                    }

                    var declaringType = x.DeclaringType ??
                                        throw new ArgumentException(@"Field has no declaring type", nameof(x));
                    var fieldType = x.FieldType;

                    // Проверяем, является ли поле константой
                    if (x.IsLiteral && !x.IsInitOnly)
                    {
                        // Для const полей возвращаем делегат, который всегда возвращает значение константы
                        var constValue = x.GetRawConstantValue();
                        return _ => constValue;
                    }

                    var dm = new DynamicMethod(
                        $"get_{declaringType.Name}_{x.Name}",
                        typeof(object),
                        [typeof(object)],
                        declaringType.Module,
                        true);

                    var il = dm.GetILGenerator();

                    // Для статических полей (не констант)
                    if (x.IsStatic)
                    {
                        il.Emit(System.Reflection.Emit.OpCodes.Ldsfld, x); // Загружаем статическое поле
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
                        il.Emit(System.Reflection.Emit.OpCodes.Ldfld, x);
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
                        il.Emit(System.Reflection.Emit.OpCodes.Ldflda, x); // Загружаем адрес поля
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
                    throw new InvalidOperationException(
                        $"Failed to create field getter for field '{x?.DeclaringType?.Name}.{x?.Name}': {ex.Message}",
                        ex);
                }
            });
        }

        /// <summary>
        /// Получает <see cref="FieldInfo"/> для поля, связанного с методом доступа (getter) свойства. Метод пытается найти поле, которое может быть связано с данным геттером.
        /// </summary>
        /// <param name="accessor">Метод доступа (getter) свойства.</param>
        /// <returns>Информация о поле, связанном с методом доступа.</returns>
        public static FieldInfo GetFieldInfo(this MethodInfo accessor)
        {
            if (accessor == null)
            {
                throw new ArgumentNullException(nameof(accessor));
            }

            var declaringType = accessor.DeclaringType ??
                                throw new ArgumentException(@"Method has no declaring type", nameof(accessor));
            var propertyName = accessor.Name.Substring(4);

            // Вариант 1: Поиск автоматически сгенерированного поля для автосвойств
            var autoBackingFieldName = $"<{propertyName}>k__BackingField";
            var field = declaringType.GetField(autoBackingFieldName, AllBindingFlags);

            if (field != null)
            {
                return field;
            }

            // Вариант 2: Поиск в базовых типах
            var baseType = declaringType.BaseType;
            while (baseType != null && baseType != typeof(object))
            {
                field = baseType.GetField(autoBackingFieldName, AllBindingFlags);

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
        /// Получает все поля указанного типа включая статические и автоматические backing-fields через <see cref="GetFields(Type)"/>.
        /// Использует внутренний кэш для ускорения повторных вызовов.
        /// </summary>
        /// <typeparam name="T">Тип, для которого нужно получить поля.</typeparam>
        /// <returns>Массив <see cref="FieldInfo" /> всех полей.</returns>
        public static IEnumerable<FieldInfo> GetFields<T>()
            where T : class => GetFields(typeof(T));

        /// <summary>
        /// Получает все поля указанного типа включая статические и автоматические backing-fields через объединение значений <see cref="GetPublicFieldsMap(Type)"/> и <see cref="GetPrivateFieldsMap(Type)"/>.
        /// Использует внутренний кэш для ускорения повторных вызовов.
        /// </summary>
        /// <param name="type">Тип, для которого нужно получить поля.</param>
        /// <returns>Массив <see cref="FieldInfo" /> всех полей.</returns>
        public static IEnumerable<FieldInfo> GetFields(this Type type)
            => GetPublicFieldsMap(type).Values.Concat(GetPrivateFieldsMap(type).Values);

        /// <summary>
        /// Получает делегат для установки значения поля, представленного <see cref="FieldInfo"/>. Делегат создается динамически с помощью <see cref="DynamicMethod"/> и IL-кода, что позволяет обходить ограничения обычного рефлексивного вызова.
        /// </summary>
        /// <param name="fi">Поле, для которого нужно создать делегат.</param>
        /// <returns>Делегат для установки значения поля.</returns>
        public static Action<object, object> GetFieldSetter(this FieldInfo fi)
        {
            return FieldSettersCache.GetOrAdd(fi, (x) =>
            {
                if (x == null)
                {
                    throw new ArgumentNullException(nameof(x));
                }

                var dm = new DynamicMethod(
                    $"Set_{x.Name}",
                    typeof(void),
                    [typeof(object), typeof(object)],
                    restrictedSkipVisibility: true);

                var il = dm.GetILGenerator();

                // local 0: TypedReference
                il.DeclareLocal(typeof(TypedReference));

                // __makeref((T)target)
                il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
                Debug.Assert(x.DeclaringType != null, "field.DeclaringType != null");
                il.Emit(System.Reflection.Emit.OpCodes.Unbox, x.DeclaringType ?? throw new InvalidOperationException());
                il.Emit(System.Reflection.Emit.OpCodes.Mkrefany, x.DeclaringType);
                il.Emit(System.Reflection.Emit.OpCodes.Stloc_0);

                // ref field
                il.Emit(System.Reflection.Emit.OpCodes.Ldloc_0);
                il.Emit(System.Reflection.Emit.OpCodes.Refanyval, x.DeclaringType);
                il.Emit(System.Reflection.Emit.OpCodes.Ldflda, x);

                // value
                il.Emit(System.Reflection.Emit.OpCodes.Ldarg_1);

                if (x.FieldType.IsValueType)
                {
                    il.Emit(System.Reflection.Emit.OpCodes.Unbox_Any, x.FieldType);
                }
                else
                {
                    il.Emit(System.Reflection.Emit.OpCodes.Castclass, x.FieldType);
                }

                il.Emit(System.Reflection.Emit.OpCodes.Stobj, x.FieldType);
                il.Emit(System.Reflection.Emit.OpCodes.Ret);

                return (Action<object, object>)dm.CreateDelegate(typeof(Action<object, object>));
            });
        }

        /// <summary>
        /// Возвращает все типы из указанной сборки (или из сборки вызывающего кода),
        /// которые реализуют интерфейс или наследуются от указанного базового типа.
        /// </summary>
        /// <param name="baseType">Базовый тип или интерфейс для поиска реализаций.</param>
        /// <param name="fromAssembly">Сборка для поиска типов. Если не указана, используется сборка вызывающего кода.</param>
        /// <returns>Массив типов, удовлетворяющих условию.</returns>
        public static Type[] GetImplementationsOf(this Type baseType, Assembly fromAssembly)
        {
            var assembly = fromAssembly ?? Assembly.GetCallingAssembly();
            return [.. assembly
                .GetTypes()
                .Where(x => IsImplements(x, baseType) && x != baseType)];
        }

        /// <summary>
        /// Возвращает все типы из всех загруженных в домен приложений сборок,
        /// которые реализуют интерфейс или наследуются от указанного базового типа.
        /// </summary>
        /// <param name="baseType">Базовый тип или интерфейс для поиска реализаций.</param>
        /// <returns>Массив типов, удовлетворяющих условию.</returns>
        public static Type[] GetImplementationsOf(this Type baseType) => [.. AppDomain.CurrentDomain
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
            .Where(x => IsImplements(x, baseType) && x != baseType)];

        /// <summary>
        /// Возвращает все свойства-индексаторы (this[]) указанного типа, используя внутренний кэш для ускорения повторных вызовов.
        /// </summary>
        /// <param name="type">Тип в котором искать свойства индексаторы.</param>
        /// <returns>Массив PropertyInfo.</returns>
        public static IEnumerable<PropertyInfo> GetIndexers(this Type type)
            => TypeIndexersCache.GetOrAdd(type, (x)
                =>
            {
                var props = x.GetProperties(AllBindingFlags).Where(p => p.GetIndexParameters().Length > 0).ToArray();
                return props;
            });

        /// <summary>
        /// Возвращает делегат для получения значения поля или свойства.
        /// </summary>
        /// <param name="member">
        /// Член типа, для которого требуется получить геттер.
        /// Поддерживаются <see cref="FieldInfo"/> и <see cref="PropertyInfo"/>.
        /// </param>
        /// <returns>
        /// Делегат, принимающий целевой объект и возвращающий текущее значение члена.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Выбрасывается, если передан неподдерживаемый тип члена.
        /// </exception>
        public static Func<object, object> GetMemberGetter(this MemberInfo member)
        {
            if (member == null)
            {
                return null;
            }

            if (member is PropertyInfo pi)
            {
                return GetPropertyGetter(pi);
            }

            if (member is FieldInfo fi)
            {
                return GetFieldGetter(fi);
            }

            throw new ArgumentException("Unsupported member type", nameof(member));
        }

        /// <summary>
        /// Возвращает делегат для установки значения поля или свойства.
        /// </summary>
        /// <param name="member">
        /// Член типа, для которого требуется получить сеттер.
        /// Поддерживаются <see cref="FieldInfo"/> и <see cref="PropertyInfo"/>.
        /// </param>
        /// <returns>
        /// Делегат, принимающий целевой объект и новое значение члена.
        /// </returns>
        /// <remarks>
        /// Для поля используется прямой сеттер,
        /// для свойства — метод установки свойства.
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// Выбрасывается, если передан неподдерживаемый тип члена.
        /// </exception>
        public static Action<object, object> GetMemberSetter(this MemberInfo member)
        {
            if (member == null)
            {
                return null;
            }

            if (member is PropertyInfo pi)
            {
                return GetPropertySetter(pi);
            }

            if (member is FieldInfo fi)
            {
                return GetFieldSetter(fi);
            }

            throw new ArgumentException("Unsupported member type", nameof(member));
        }

        /// <summary>
        /// Возвращает тип данных, связанный с указанным членом отражения.
        /// </summary>
        /// <param name="member">
        /// Объект <see cref="MemberInfo"/>, для которого необходимо определить тип.
        /// </param>
        /// <returns>
        /// Тип, соответствующий переданному члену:
        /// <list type="bullet">
        /// <item><see cref="PropertyInfo"/> — тип свойства <see cref="PropertyInfo.PropertyType"/>.</item>
        /// <item><see cref="FieldInfo"/> — тип поля <see cref="FieldInfo.FieldType"/>.</item>
        /// <item><see cref="EventInfo"/> — тип обработчика события <see cref="EventInfo.EventHandlerType"/>.</item>
        /// <item><see cref="MethodInfo"/> — возвращаемый тип метода <see cref="MethodInfo.ReturnType"/>.</item>
        /// <item><see cref="ConstructorInfo"/> — тип, объявляющий конструктор <see cref="MemberInfo.DeclaringType"/>.</item>
        /// <item><see cref="Type"/> — сам тип.</item>
        /// </list>
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Выбрасывается, если тип члена не поддерживается.
        /// </exception>
        public static Type GetMemberType(this MemberInfo member)
        {
            switch (member)
            {
                case PropertyInfo p: return p.PropertyType;
                case FieldInfo f: return f.FieldType;
                case EventInfo e: return e.EventHandlerType;
                case MethodInfo m: return m.ReturnType;
                case ConstructorInfo c: return c.DeclaringType;
                case Type t: return t;
                default: throw new ArgumentException("Unsupported member type", nameof(member));
            }
        }

        /// <summary>
        /// Получает все методов, применённые к указанному типу, используя внутренний кэш для ускорения повторных вызовов.
        /// </summary>
        /// <typeparam name="T">Тип, для которого нужно получить методы.</typeparam>
        /// <returns>Массив методов.</returns>
        public static MethodInfo[] GetMethods<T>()
            => GetMethods(typeof(T));

        /// <summary>
        /// Получает все методы, применённые к указанному типу, используя внутренний кэш для ускорения повторных вызовов.
        /// </summary>
        /// <param name="type"><see cref="Type"/>.</param>
        /// <returns>Массив методов.</returns>
        public static MethodInfo[] GetMethods(this Type type)
        {
            return TypeMethodsCache.GetOrAdd(type, t => t.GetMethods(AllBindingFlags).OrderBy(x => x.Name).ThenBy(x => x.GetParameters().Length).ToArray());
        }

        /// <summary>
        /// Получает приватное поле по имени для указанного типа, используя внутренний кэш для ускорения повторных вызовов через <see cref="GetPrivateFieldsMap"/>.
        /// </summary>
        /// <param name="type">Тип в котором искать поле.</param>
        /// <param name="fieldName">Имя поля.</param>
        /// <param name="comparison">Правило сравнения строк.</param>
        /// <returns><see cref="FieldInfo"/>.</returns>
        public static FieldInfo GetPrivateField(this Type type, string fieldName, StringComparison comparison = StringComparison.Ordinal)
            => TryGetValue(GetPrivateFieldsMap(type), fieldName, comparison, out var f) ? f : null;

        /// <summary>
        /// Получает приватное поле по имени для указанного типа, используя внутренний кэш для ускорения повторных вызовов через <see cref="GetPrivateFieldsMap"/>.
        /// </summary>
        /// <typeparam name="T">Тип, для которого нужно получить поле.</typeparam>
        /// <param name="fieldName">Имя поля.</param>
        /// <param name="comparison">Правило сравнения строк.</param>
        /// <returns><see cref="FieldInfo"/>.</returns>
        public static FieldInfo GetPrivateField<T>(string fieldName, StringComparison comparison = StringComparison.Ordinal)
            => GetPrivateField(typeof(T), fieldName, comparison);

        /// <summary>
        /// Получает имена всех приватные полей указанного типа включая статические через ключи <see cref="GetPrivateFieldsMap(Type)"/>.
        /// Использует внутренний кэш для ускорения повторных вызовов.
        /// </summary>
        /// <param name="type">Тип, для которого нужно получить имена полей.</param>
        /// <returns>Массив имен приватных полей.</returns>
        public static IEnumerable<string> GetPrivateFieldNames(this Type type)
            => GetPrivateFieldsMap(type).Keys;

        /// <summary>
        /// Получает имена всех публичных полей указанного типа включая статические через <see cref="GetPrivateFieldNames(Type)"/>.
        /// Использует внутренний кэш для ускорения повторных вызовов.
        /// </summary>
        /// <typeparam name="T">Тип, для которого нужно получить имена полей.</typeparam>
        /// <returns>Массив имен публичных полей.</returns>
        public static IEnumerable<string> GetPrivateFieldNames<T>()
            => GetPrivateFieldNames(typeof(T));

        /// <summary>
        /// Получает все приватные поля указанного типа включая статические и автоматические backing-fields через <see cref="GetPrivateFields(Type)"/>.
        /// Использует внутренний кэш для ускорения повторных вызовов.
        /// </summary>
        /// <typeparam name="T">Тип, для которого нужно получить поля.</typeparam>
        /// <returns>Массив <see cref="FieldInfo" /> приватных полей.</returns>
        public static IEnumerable<FieldInfo> GetPrivateFields<T>()
            => GetPrivateFields(typeof(T));

        /// <summary>
        /// Получает все приватные поля указанного типа включая статические и автоматические backing-fields через значения <see cref="GetPrivateFieldsMap(Type)"/>.
        /// Использует внутренний кэш для ускорения повторных вызовов.
        /// </summary>
        /// <param name="type">Тип, для которого нужно получить поля.</param>
        /// <returns>Массив <see cref="FieldInfo" /> приватных полей.</returns>
        public static IEnumerable<FieldInfo> GetPrivateFields(this Type type)
            => GetPrivateFieldsMap(type).Values;

        /// <summary>
        /// Получает все приватные поля указанного типа включая статические и автоматические backing-fields.
        /// Использует внутренний кэш для ускорения повторных вызовов.
        /// </summary>
        /// <param name="type">Тип, для которого нужно получить поля.</param>
        /// <returns>Массив <see cref="FieldInfo" /> приватных полей.</returns>
        public static IReadOnlyDictionary<string, FieldInfo> GetPrivateFieldsMap(this Type type)
            => TypePrivateFieldsCache.GetOrAdd(type, (x) =>
            {
                var fields = x.GetFields(PrivateBindingFlags);
                return new ReadOnlyDictionary<string, FieldInfo>(fields.GroupBy(f => f.Name).ToDictionary(g => g.Key, g => g.First()));
            });

        /// <summary>
        /// Получает все приватные поля указанного типа включая статические через <see cref="GetPrivateFieldsMap(Type)"/>.
        /// Использует внутренний кэш для ускорения повторных вызовов.
        /// </summary>
        /// <typeparam name="T">Тип, для которого нужно получить поля.</typeparam>
        /// <returns>Массив <see cref="FieldInfo" /> приватных полей.</returns>
        public static IReadOnlyDictionary<string, FieldInfo> GetPrivateFieldsMap<T>()
            => GetPrivateFieldsMap(typeof(T));

        /// <summary>
        /// Получает все приватные свойства указанного типа включая статические, кроме свойств индексаторов (this[]) через <see cref="GetPrivateProperties(Type)"/>.
        /// Использует внутренний кэш для ускорения повторных вызовов.
        /// </summary>
        /// <typeparam name="T">Тип, для которого нужно получить свойства.</typeparam>
        /// <returns>Массив <see cref="PropertyInfo" /> всех свойств.</returns>
        public static IEnumerable<PropertyInfo> GetPrivateProperties<T>()
            where T : class => GetPrivateProperties(typeof(T));

        /// <summary>
        /// Получает все приватные свойства указанного типа включая статические, кроме свойств индексаторов (this[]) через значения <see cref="GetPrivatePropertiesMap(Type)"/>.
        /// Использует внутренний кэш для ускорения повторных вызовов.
        /// </summary>
        /// <param name="type">Тип, для которого нужно получить свойства.</param>
        /// <returns>Массив <see cref="PropertyInfo" /> всех свойств.</returns>
        public static IEnumerable<PropertyInfo> GetPrivateProperties(this Type type)
            => GetPrivatePropertiesMap(type).Values;

        /// <summary>
        /// Получает все приватные свойства указанного типа включая статические, кроме свойств индексаторов (this[]).
        /// Использует внутренний кэш для ускорения повторных вызовов.
        /// </summary>
        /// <param name="type">Тип, для которого нужно получить свойства.</param>
        /// <returns>Массив <see cref="PropertyInfo" /> всех свойств.</returns>
        public static IReadOnlyDictionary<string, PropertyInfo> GetPrivatePropertiesMap(this Type type)
            => TypePrivatePropertiesCache.GetOrAdd(type, (x)
                =>
            {
                var props = x.GetProperties(PrivateBindingFlags).Where(p => p.GetIndexParameters().Length == 0).ToArray();
                return new ReadOnlyDictionary<string, PropertyInfo>(props.GroupBy(p => p.Name).ToDictionary(g => g.Key, g => g.First()));
            });

        /// <summary>
        /// Получает все приватные свойства указанного типа включая статические, кроме свойств индексаторов (this[]) через <see cref="GetPrivatePropertiesMap(Type)"/>.
        /// Использует внутренний кэш для ускорения повторных вызовов.
        /// </summary>
        /// <typeparam name="T">Тип, для которого нужно получить свойства.</typeparam>
        /// <returns>Массив <see cref="PropertyInfo" /> всех свойств.</returns>
        public static IReadOnlyDictionary<string, PropertyInfo> GetPrivatePropertiesMap<T>()
            => GetPrivatePropertiesMap(typeof(T));

        /// <summary>
        /// Получает приватное свойство по имени для указанного типа, используя внутренний кэш для ускорения повторных вызовов через <see cref="GetPrivatePropertiesMap(Type)"/>.
        /// </summary>
        /// <param name="type">Тип в котором искать свойство.</param>
        /// <param name="propertyName">Имя свойства.</param>
        /// <param name="comparison">Правило сравнения строк.</param>
        /// <returns><see cref="PropertyInfo"/>.</returns>
        public static PropertyInfo GetPrivateProperty(this Type type, string propertyName, StringComparison comparison = StringComparison.Ordinal)
            => TryGetValue(GetPrivatePropertiesMap(type), propertyName, comparison, out var p) ? p : null;

        /// <summary>
        /// Получает приватное свойство по имени для указанного типа, используя внутренний кэш для ускорения повторных вызовов через <see cref="GetPrivateProperty(Type, string, StringComparison)"/>.
        /// </summary>
        /// <typeparam name="T">Тип в котором искать свойство.</typeparam>
        /// <param name="propertyName">Имя свойства.</param>
        /// <param name="comparison">Правило сравнения строк.</param>
        /// <returns><see cref="PropertyInfo"/>.</returns>
        public static PropertyInfo GetPrivateProperty<T>(string propertyName, StringComparison comparison = StringComparison.Ordinal)
            => GetPrivateProperty(typeof(T), propertyName, comparison);

        /// <summary>
        /// Получает имена всех приватных свойств типа через ключи <see cref="GetPrivatePropertiesMap(Type)"/>.
        /// </summary>
        /// <param name="type">Тип в котором искать свойство.</param>
        /// <returns>System.String[].</returns>
        public static IEnumerable<string> GetPrivatePropertyNames(this Type type)
            => GetPrivatePropertiesMap(type).Keys;

        /// <summary>
        /// Получает имена всех приватных свойств типа через ключи <see cref="GetPrivatePropertyNames(Type)"/>.
        /// </summary>
        /// <typeparam name="T">Тип в котором искать свойство.</typeparam>
        /// <returns>System.String[].</returns>
        public static IEnumerable<string> GetPrivatePropertyNames<T>()
            => GetPrivatePropertyNames(typeof(T));

        /// <summary>
        /// Возвращает первое найденное свойство или поле через <see cref="GetPrivateProperty(Type, string, StringComparison)"/> или <see cref="GetPrivateField(Type, string, StringComparison)"/>.
        /// </summary>
        /// <param name="type">Тип в котором искать свойство или поле.</param>
        /// <param name="name">Имя свойства или поля.</param>
        /// <param name="comparison">Правило сравнения строк.</param>
        /// <returns>PropertyInfo или FieldInfo.</returns>
        public static MemberInfo GetPrivatePropertyOrField(this Type type, string name, StringComparison comparison = StringComparison.Ordinal)
        {
            return GetPrivateProperty(type, name, comparison) as MemberInfo ?? GetPrivateField(type, name, comparison);
        }

        /// <summary>
        /// Возвращает первое найденное свойство или поле через <see cref="GetPrivatePropertyOrField(Type, string, StringComparison)"/>.
        /// </summary>
        /// <typeparam name="T">Тип в котором искать свойство или поле.</typeparam>
        /// <param name="name">Имя свойства или поля.</param>
        /// <param name="comparison">Правило сравнения строк.</param>
        /// <returns>PropertyInfo или FieldInfo.</returns>
        public static MemberInfo GetPrivatePropertyOrField<T>(string name, StringComparison comparison = StringComparison.Ordinal)
            => GetPrivatePropertyOrField(typeof(T), name, comparison);

        /// <summary>
        /// Получает все свойства указанного типа включая статические, кроме свойств индексаторов (this[]) через <see cref="GetProperties(Type)"/>.
        /// Использует внутренний кэш для ускорения повторных вызовов.
        /// </summary>
        /// <typeparam name="T">Тип, для которого нужно получить свойства.</typeparam>
        /// <returns>Массив <see cref="PropertyInfo" /> всех свойств.</returns>
        public static IEnumerable<PropertyInfo> GetProperties<T>()
            where T : class => GetProperties(typeof(T));

        /// <summary>
        /// Получает все свойства указанного типа включая статические, кроме свойств индексаторов (this[]) через объединение значений <see cref="GetPublicPropertiesMap(Type)"/> и <see cref="GetPrivatePropertiesMap(Type)"/>.
        /// Использует внутренний кэш для ускорения повторных вызовов.
        /// </summary>
        /// <param name="type">Тип, для которого нужно получить свойства.</param>
        /// <returns>Массив <see cref="PropertyInfo" /> всех свойств.</returns>
        public static IEnumerable<PropertyInfo> GetProperties(this Type type)
            => GetPublicPropertiesMap(type).Values.Concat(GetPrivatePropertiesMap(type).Values);

        /// <summary>
        /// Возвращает первое найденное свойство через <see cref="GetPublicProperty(Type, string, StringComparison)"/> или <see cref="GetPrivateProperty(Type, string, StringComparison)"/>.
        /// </summary>
        /// <param name="type">Тип в котором искать свойство.</param>
        /// <param name="name">Имя свойства.</param>
        /// <param name="comparison">Правило сравнения строк.</param>
        /// <returns>PropertyInfo.</returns>
        public static PropertyInfo GetProperty(this Type type, string name, StringComparison comparison = StringComparison.Ordinal)
        {
            return
                GetPublicProperty(type, name, comparison) ??
                GetPrivateProperty(type, name, comparison);
        }

        /// <summary>
        /// Возвращает первое найденное свойство через <see cref="GetProperty(Type, string, StringComparison)"/>.
        /// </summary>
        /// <typeparam name="T">Тип в котором искать свойство.</typeparam>
        /// <param name="name">Имя свойства.</param>
        /// <param name="comparison">Правило сравнения строк.</param>
        /// <returns>PropertyInfo.</returns>
        public static PropertyInfo GetProperty<T>(string name, StringComparison comparison = StringComparison.Ordinal)
            => GetProperty(typeof(T), name, comparison);

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
        public static Func<object, object> GetPropertyGetter(this PropertyInfo pi)
        {
            return PropertyGettersCache.GetOrAdd(pi, (x) => GetPropertyGetter<object, object>(pi));
        }

        /// <summary>
        /// Возвращает первое найденное свойство или поле через <see cref="GetPublicPropertyOrField(Type, string, StringComparison)"/> или <see cref="GetPrivatePropertyOrField(Type, string, StringComparison)"/>.
        /// </summary>
        /// <param name="type">Тип в котором искать свойство или поле.</param>
        /// <param name="name">Имя свойства или поля.</param>
        /// <param name="comparison">Правило сравнения строк.</param>
        /// <returns>PropertyInfo или FieldInfo.</returns>
        public static MemberInfo GetPropertyOrField(this Type type, string name, StringComparison comparison = StringComparison.Ordinal)
        {
            return
                GetPublicPropertyOrField(type, name, comparison) ??
                GetPrivatePropertyOrField(type, name, comparison);
        }

        /// <summary>
        /// Возвращает первое найденное свойство или поле через <see cref="GetPropertyOrField(Type, string, StringComparison)"/>.
        /// </summary>
        /// <typeparam name="T">Тип в котором искать свойство или поле.</typeparam>
        /// <param name="name">Имя свойства или поля.</param>
        /// <param name="comparison">Правило сравнения строк.</param>
        /// <returns>PropertyInfo или FieldInfo.</returns>
        public static MemberInfo GetPropertyOrField<T>(string name, StringComparison comparison = StringComparison.Ordinal)
            => GetPropertyOrField(typeof(T), name, comparison);

        /// <summary>
        /// Получает делегат для установки значения свойства.
        /// </summary>
        /// <param name="pi">Свойство, для которого нужно создать делегат.</param>
        /// <returns>Делегат для установки значения свойства.</returns>
        public static Action<object, object> GetPropertySetter(this PropertyInfo pi)
        {
            return PropertySettersCache.GetOrAdd(pi, (x) =>
            {
                var setter = x.GetSetMethod(true);
                if (setter == null)
                {
                    var backingField = GetFieldInfo(x.GetMethod);
                    if (backingField != null)
                    {
                        return GetDirectFieldSetter(backingField);
                    }

                    return null;
                }

                var declaring = x.DeclaringType;
                var propertyType = x.PropertyType;

                if (declaring == null)
                {
                    throw new ArgumentException("Property must have a declaring type", nameof(x));
                }

                var dm = new DynamicMethod(
                    "set_" + x.Name,
                    null,
                    [typeof(object), typeof(object)],
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
            });
        }

        /// <summary>
        /// Получает публичное поле по имени для указанного типа, используя внутренний кэш для ускорения повторных вызовов через <see cref="GetPublicFieldsMap(Type)"/>.
        /// </summary>
        /// <param name="type">Тип в котором искать поле.</param>
        /// <param name="fieldName">Имя поля.</param>
        /// <param name="comparison">Правило сравнения строк.</param>
        /// <returns><see cref="FieldInfo"/>.</returns>
        public static FieldInfo GetPublicField(this Type type, string fieldName, StringComparison comparison = StringComparison.Ordinal)
            => TryGetValue(GetPublicFieldsMap(type), fieldName, comparison, out var f) ? f : null;

        /// <summary>
        /// Получает публичное поле по имени для указанного типа, используя внутренний кэш для ускорения повторных вызовов через <see cref="GetPublicField(Type, string, StringComparison)"/>.
        /// </summary>
        /// <typeparam name="T">Тип в котором искать поле.</typeparam>
        /// <param name="fieldName">Имя поля.</param>
        /// <param name="comparison">Правило сравнения строк.</param>
        /// <returns><see cref="FieldInfo"/>.</returns>
        public static FieldInfo GetPublicField<T>(string fieldName, StringComparison comparison = StringComparison.Ordinal)
            => GetPublicField(typeof(T), fieldName, comparison);

        /// <summary>
        /// Получает имена всех публичных полей указанного типа включая статические через ключи <see cref="GetPublicFieldsMap(Type)"/>.
        /// Использует внутренний кэш для ускорения повторных вызовов.
        /// </summary>
        /// <param name="type">Тип, для которого нужно получить имена полей.</param>
        /// <returns>Массив имен публичных полей.</returns>
        public static IEnumerable<string> GetPublicFieldNames(this Type type)
            => GetPublicFieldsMap(type).Keys;

        /// <summary>
        /// Получает имена всех публичных полей указанного типа включая статические через <see cref="GetPublicFieldNames(Type)"/>.
        /// Использует внутренний кэш для ускорения повторных вызовов.
        /// </summary>
        /// <typeparam name="T">Тип, для которого нужно получить имена полей.</typeparam>
        /// <returns>Массив имен публичных полей.</returns>
        public static IEnumerable<string> GetPublicFieldNames<T>()
            => GetPublicFieldNames(typeof(T));

        /// <summary>
        /// Получает все публичные поля указанного типа включая статические через <see cref="GetPublicFields(Type)"/>.
        /// Использует внутренний кэш для ускорения повторных вызовов.
        /// </summary>
        /// <typeparam name="T">Тип, для которого нужно получить поля.</typeparam>
        /// <returns>Массив <see cref="FieldInfo" /> публичных полей.</returns>
        public static IEnumerable<FieldInfo> GetPublicFields<T>()
            => GetPublicFields(typeof(T));

        /// <summary>
        /// Получает все публичные поля указанного типа включая статические через значения <see cref="GetPublicFieldsMap(Type)"/>.
        /// Использует внутренний кэш для ускорения повторных вызовов.
        /// </summary>
        /// <param name="type">Тип, для которого нужно получить поля.</param>
        /// <returns>Массив <see cref="FieldInfo" /> публичных полей.</returns>
        public static IEnumerable<FieldInfo> GetPublicFields(this Type type)
            => GetPublicFieldsMap(type).Values;

        /// <summary>
        /// Получает все публичные поля указанного типа включая статические.
        /// Использует внутренний кэш для ускорения повторных вызовов.
        /// </summary>
        /// <param name="type">Тип, для которого нужно получить поля.</param>
        /// <returns>Массив <see cref="FieldInfo" /> публичных полей.</returns>
        public static IReadOnlyDictionary<string, FieldInfo> GetPublicFieldsMap(this Type type)
            => TypePublicFieldsCache.GetOrAdd(type, (x) =>
            {
                var fields = x.GetFields(PublicBindingFlags);
                return new ReadOnlyDictionary<string, FieldInfo>(fields.GroupBy(f => f.Name).ToDictionary(g => g.Key, g => g.First()));
            });

        /// <summary>
        /// Получает все публичные поля указанного типа включая статические через <see cref="GetPublicFieldsMap(Type)"/>.
        /// Использует внутренний кэш для ускорения повторных вызовов.
        /// </summary>
        /// <typeparam name="T">Тип, для которого нужно получить поля.</typeparam>
        /// <returns>Массив <see cref="FieldInfo" /> публичных полей.</returns>
        public static IReadOnlyDictionary<string, FieldInfo> GetPublicFieldsMap<T>()
            => GetPublicFieldsMap(typeof(T));

        /// <summary>
        /// Получает все публичные свойства указанного типа включая статические, кроме свойств индексаторов (this[]) через <see cref="GetPublicProperties(Type)"/>.
        /// Использует внутренний кэш для ускорения повторных вызовов.
        /// </summary>
        /// <typeparam name="T">Тип, для которого нужно получить свойства.</typeparam>
        /// <returns>Массив <see cref="PropertyInfo" /> всех свойств.</returns>
        public static IEnumerable<PropertyInfo> GetPublicProperties<T>()
            where T : class => GetPublicProperties(typeof(T));

        /// <summary>
        /// Получает все публичные свойства указанного типа включая статические, кроме свойств индексаторов (this[]) через значения <see cref="GetPublicPropertiesMap(Type)"/>.
        /// Использует внутренний кэш для ускорения повторных вызовов.
        /// </summary>
        /// <param name="type">Тип, для которого нужно получить свойства.</param>
        /// <returns>Массив <see cref="PropertyInfo" /> всех свойств.</returns>
        public static IEnumerable<PropertyInfo> GetPublicProperties(this Type type)
            => GetPublicPropertiesMap(type).Values;

        /// <summary>
        /// Получает все публичные свойства указанного типа включая статические, кроме свойств индексаторов (this[]).
        /// Использует внутренний кэш для ускорения повторных вызовов.
        /// </summary>
        /// <param name="type">Тип, для которого нужно получить свойства.</param>
        /// <returns>Массив <see cref="PropertyInfo" /> всех свойств.</returns>
        public static IReadOnlyDictionary<string, PropertyInfo> GetPublicPropertiesMap(this Type type)
            => TypePublicPropertiesCache.GetOrAdd(type, (x)
                =>
            {
                var props = x.GetProperties(PublicBindingFlags).Where(p => p.GetIndexParameters().Length == 0).ToArray();
                return new ReadOnlyDictionary<string, PropertyInfo>(props.GroupBy(p => p.Name).ToDictionary(g => g.Key, g => g.First()));
            });

        /// <summary>
        /// Получает все публичные свойства указанного типа включая статические, кроме свойств индексаторов (this[]) через <see cref="GetPublicPropertiesMap(Type)"/>.
        /// Использует внутренний кэш для ускорения повторных вызовов.
        /// </summary>
        /// <typeparam name="T">Тип, для которого нужно получить свойства.</typeparam>
        /// <returns>Массив <see cref="PropertyInfo" /> всех свойств.</returns>
        public static IReadOnlyDictionary<string, PropertyInfo> GetPublicPropertiesMap<T>()
            => GetPublicPropertiesMap(typeof(T));

        /// <summary>
        /// Получает публичное свойство по имени для указанного типа, используя внутренний кэш для ускорения повторных вызовов через <see cref="GetPublicPropertiesMap(Type)"/>.
        /// </summary>
        /// <param name="type">Тип в котором искать свойство.</param>
        /// <param name="propertyName">Имя свойства.</param>
        /// <param name="comparison">Правило сравнения строк.</param>
        /// <returns><see cref="PropertyInfo"/>.</returns>
        public static PropertyInfo GetPublicProperty(this Type type, string propertyName, StringComparison comparison = StringComparison.Ordinal)
            => TryGetValue(GetPublicPropertiesMap(type), propertyName, comparison, out var p) ? p : null;

        /// <summary>
        /// Получает публичное свойство по имени для указанного типа, используя внутренний кэш для ускорения повторных вызовов через <see cref="GetPublicProperty(Type, string, StringComparison)"/>.
        /// </summary>
        /// <typeparam name="T">Тип в котором искать свойство.</typeparam>
        /// <param name="propertyName">Имя свойства.</param>
        /// <param name="comparison">Правило сравнения строк.</param>
        /// <returns><see cref="PropertyInfo"/>.</returns>
        public static PropertyInfo GetPublicProperty<T>(string propertyName, StringComparison comparison = StringComparison.Ordinal)
            => GetPublicProperty(typeof(T), propertyName, comparison);

        /// <summary>
        /// Получает имена всех публичных свойств типа через ключи <see cref="GetPublicPropertiesMap(Type)"/>.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>System.String[].</returns>
        public static IEnumerable<string> GetPublicPropertyNames(this Type type)
            => GetPublicPropertiesMap(type).Keys;

        /// <summary>
        /// Получает имена всех публичных свойств типа через ключи <see cref="GetPublicPropertyNames(Type)"/>.
        /// </summary>
        /// <typeparam name="T">The type.</typeparam>
        /// <returns>System.String[].</returns>
        public static IEnumerable<string> GetPublicPropertyNames<T>()
            => GetPublicPropertyNames(typeof(T));

        /// <summary>
        /// Возвращает первое найденное свойство или поле через <see cref="GetPublicProperty(Type, string, StringComparison)"/> или <see cref="GetPublicField(Type, string, StringComparison)"/>.
        /// </summary>
        /// <param name="type">Тип в котором искать свойство или поле.</param>
        /// <param name="name">Имя свойства или поля.</param>
        /// <param name="comparison">Правило сравнения строк.</param>
        /// <returns>PropertyInfo или FieldInfo.</returns>
        public static MemberInfo GetPublicPropertyOrField(this Type type, string name, StringComparison comparison = StringComparison.Ordinal)
        {
            return GetPublicProperty(type, name, comparison) as MemberInfo ?? GetPublicField(type, name, comparison);
        }

        /// <summary>
        /// Возвращает первое найденное свойство или поле через <see cref="GetPublicPropertyOrField(Type, string, StringComparison)"/>.
        /// </summary>
        /// <typeparam name="T">Тип в котором искать свойство или поле.</typeparam>
        /// <param name="name">Имя свойства или поля.</param>
        /// <param name="comparison">Правило сравнения строк.</param>
        /// <returns>PropertyInfo или FieldInfo.</returns>
        public static MemberInfo GetPublicPropertyOrField<T>(string name, StringComparison comparison = StringComparison.Ordinal)
            => GetPublicPropertyOrField(typeof(T), name, comparison);

        /// <summary>
        /// Поиска типа в загруженных сборках по полному имени (namespace + имя типа), если в имени содержатся точки и по короткому имени, если точек нет.
        /// </summary>
        /// <param name="typeName">Полное имя типа.</param>
        /// <param name="stringComparison">Правило сравнения строк.</param>
        /// <returns>Тип.</returns>
        public static Type GetType(string typeName, StringComparison stringComparison = StringComparison.Ordinal)
        {
            if (typeName == null)
            {
                throw new ArgumentNullException(nameof(typeName));
            }

            var isFullName = typeName.Contains('.');

            if (isFullName && TryGetValue(TypeByFullNameCache, typeName, stringComparison, out var t))
            {
                return t;
            }

            t = Type.GetType(typeName) ??
            (isFullName ?
            GetTypes(t => string.Equals(t.FullName, typeName, stringComparison)).FirstOrDefault() :
            GetTypes(t => string.Equals(t.Name, typeName, stringComparison)).FirstOrDefault());
            TypeByFullNameCache[t.FullName] = t;
            return t;
        }

        /// <summary>
        /// Выполняет поиск типов в указанных сборках, удовлетворяющих заданному условию.
        /// </summary>
        /// <param name="filter">
        /// Делегат-фильтр для проверки типов.
        /// Если <c>null</c>, будут возвращены все найденные типы.
        /// </param>
        /// <param name="assemblies">
        /// Сборки, в которых выполняется поиск типов.
        /// Если параметр не указан или равен <c>null</c>, используются все сборки,
        /// загруженные в текущий домен приложения (<see cref="AppDomain.CurrentDomain"/>).
        /// </param>
        /// <returns>
        /// Массив типов (<see cref="Type"/>), удовлетворяющих условию <paramref name="filter"/>.
        /// </returns>
        /// <remarks>
        /// Для повышения производительности используется кэширование типов для каждой сборки.
        /// Метод также безопасно обрабатывает исключение <see cref="ReflectionTypeLoadException"/>,
        /// которое может возникнуть при вызове <see cref="Assembly.GetTypes()"/>.
        /// В этом случае в кэш сохраняются только успешно загруженные типы.
        /// </remarks>
        public static Type[] GetTypes(Func<Type, bool> filter, params Assembly[] assemblies)
        {
            if (assemblies == null || assemblies.Length == 0)
            {
                assemblies = AppDomain.CurrentDomain.GetAssemblies();
            }

            var result = new List<Type>();

            foreach (var assembly in assemblies)
            {
                if (assembly == null)
                {
                    continue;
                }

                var types = GetTypes(assembly);

                foreach (var type in types)
                {
                    if (filter == null || filter(type))
                    {
                        result.Add(type);
                    }
                }
            }

            return [.. result];
        }

        /// <summary>
        /// Возвращает все доступные типы, определённые в указанной сборке.
        /// </summary>
        /// <param name="assembly">Сборка, типы которой необходимо получить.</param>
        /// <returns>
        /// Массив типов, содержащихся в сборке.
        /// Если <paramref name="assembly"/> равен <see langword="null"/>,
        /// возвращается пустой массив.
        /// </returns>
        /// <remarks>
        /// Результаты кэшируются для повторных вызовов.
        /// Если при загрузке типов возникает <see cref="ReflectionTypeLoadException"/>,
        /// возвращаются только успешно загруженные типы.
        /// </remarks>
        public static Type[] GetTypes(this Assembly assembly)
        {
            if (assembly == null)
            {
                return Array.Empty<Type>();
            }

            var types = AssemblyTypesCache.GetOrAdd(assembly, a =>
            {
                try
                {
                    return a.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    return [.. ex.Types.Where(t => t != null)];
                }
            });

            return types;
        }

        /// <summary>
        /// Получить значение свойства или поля объекта по имени.
        /// </summary>
        /// <typeparam name="T">Тип в который конвертировать значение.</typeparam>
        /// <param name="x">Экземпляр объекта.</param>
        /// <param name="name">Имя свойства или поля.</param>
        /// <param name="stringComparison">Тип сравнения имени свойства или поля. Сначала ищутся свойства, затем поля.</param>
        /// <param name="throwOnMissingMember">Выбрасывать исключение, если свойство или поле не найдено.</param>
        /// <returns>Значение свойства или поля, null, если свойство или поле не найдено.</returns>
        public static object GetValue<T>(this T x, string name, StringComparison stringComparison = StringComparison.Ordinal, bool throwOnMissingMember = false)
            where T : class
            => GetValue((object)x, name, stringComparison, throwOnMissingMember);

        /// <summary>
        /// Получить значение свойства или поля объекта по имени.
        /// </summary>
        /// <param name="x">Экземпляр объекта.</param>
        /// <param name="name">Имя свойства или поля.</param>
        /// <param name="stringComparison">Тип сравнения имени свойства или поля. Сначала ищутся свойства, затем поля.</param>
        /// <param name="throwOnMissingMember">Выбрасывать исключение, если свойство или поле не найдено.</param>
        /// <returns>Значение свойства или поля, null, если свойство или поле не найдено.</returns>
        public static object GetValue(object x, string name, StringComparison stringComparison = StringComparison.Ordinal, bool throwOnMissingMember = false)
        {
            if (x == null)
            {
                throw new NullReferenceException(nameof(x));
            }

            var m = GetPropertyOrField(x.GetType(), name, stringComparison);
            if (m == null)
            {
                if (throwOnMissingMember)
                {
                    throw new ArgumentException(nameof(name));
                }

                return null;
            }

            var getter = GetMemberGetter(m);
            var mt = GetMemberType(m);
            return getter(x);
        }

        /// <summary>
        /// Получить значение свойства или поля объекта по имени.
        /// </summary>
        /// <typeparam name="T">Тип в который конвертировать значение через <see cref="ChangeType{T}(object, IFormatProvider)"/>.</typeparam>
        /// <param name="x">Экземпляр объекта.</param>
        /// <param name="name">Имя свойства или поля.</param>
        /// <param name="stringComparison">Тип сравнения имени свойства или поля. Сначала ищутся свойства, затем поля.</param>
        /// <param name="throwOnMissingMember">Выбрасывать исключение, если свойство или поле не найдено.</param>
        /// <returns>Значение свойства или поля, null, если свойство или поле не найдено.</returns>
        public static T GetValue<T>(this object x, string name, StringComparison stringComparison = StringComparison.Ordinal, bool throwOnMissingMember = false)
        {
            return ChangeType<T>(GetValue(x, name, stringComparison, throwOnMissingMember));
        }

        /// <summary>
        /// Проверяет, содержит ли член все указанные атрибуты.
        /// </summary>
        /// <param name="memberInfo">Любой MemberInfo.</param>
        /// <param name="attributeTypeNames">Имена типов атрибутов. Имя должно быть полным, т.е. заканчиваться на Attribute.</param>
        /// <returns>true, если член содержит все указанные атрибуты; в противном случае — false.</returns>
        public static bool HasAllAttributes(this MemberInfo memberInfo, params string[] attributeTypeNames)
        {
            var attributes = GetAttributes(memberInfo);
            for (int i = 0; i < attributes.Length; i++)
            {
                if (!attributeTypeNames.Contains(attributes[i].GetType().Name, StringComparer.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Проверяет, содержит ли член все указанные атрибуты.
        /// </summary>
        /// <param name="memberInfo">Любой MemberInfo.</param>
        /// <param name="attributes">Атрибуты.</param>
        /// <returns>true, если член содержит все указанные атрибуты; в противном случае — false.</returns>
        public static bool HasAllAttributes(this MemberInfo memberInfo, params Attribute[] attributes)
        {
            var memberAttributes = GetAttributes(memberInfo);
            for (int i = 0; i < memberAttributes.Length; i++)
            {
                if (!attributes.Contains(memberAttributes[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Проверяет, содержит ли член любой из указанных атрибутов.
        /// </summary>
        /// <param name="memberInfo">Любой MemberInfo.</param>
        /// <param name="attributeTypeNames">Имена типов атрибутов. Имя должно быть полным, т.е. заканчиваться на Attribute.</param>
        /// <returns>true, если член содержит хотя бы один из указанных атрибутов; в противном случае — false.</returns>
        public static bool HasAnyAttribute(this MemberInfo memberInfo, params string[] attributeTypeNames)
        {
            var attributes = GetAttributes(memberInfo);
            for (int i = 0; i < attributes.Length; i++)
            {
                if (attributeTypeNames.Contains(attributes[i].GetType().Name, StringComparer.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Проверяет, содержит ли член любой из указанных атрибутов.
        /// </summary>
        /// <param name="memberInfo">Любой MemberInfo.</param>
        /// <param name="attributes">Атрибуты.</param>
        /// <returns>true, если член содержит хотя бы один из указанных атрибутов; в противном случае — false.</returns>
        public static bool HasAnyAttribute(this MemberInfo memberInfo, params Attribute[] attributes)
        {
            var memberAttributes = GetAttributes(memberInfo);
            for (int i = 0; i < memberAttributes.Length; i++)
            {
                if (attributes.Contains(memberAttributes[i]))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Проверяет, является ли тип простым (базовым) <see cref="BasicTypes"/>.
        /// </summary>
        /// <param name="t">Тип для проверки.</param>
        /// <returns>True, если тип является базовым, иначе False.</returns>
        public static bool IsBasic(this Type t) => t != null && (t.IsEnum || BasicTypes.Contains(t));

        /// <summary>
        /// Проверяет, является ли тип логическим <see cref="BoolTypes"/>.
        /// </summary>
        /// <param name="t">Тип для проверки.</param>
        /// <returns>True, если тип является логическим, иначе False.</returns>
        public static bool IsBoolean(this Type t) => BoolTypes.Contains(t);

        /// <summary>
        /// Проверяет, является ли тип коллекцией (IsArray, IList, ICollection, IEnumerable) кроме string.
        /// </summary>
        /// <param name="t">Тип для проверки.</param>
        /// <returns>True, если тип является коллекцией, иначе False.</returns>
        public static bool IsCollection(this Type t)
        {
            if (t.IsArray)
            {
                return true;
            }

            if (t == typeof(string))
            {
                return false;
            }

            return typeof(IList).IsAssignableFrom(t) || typeof(ICollection).IsAssignableFrom(t) ||
                   typeof(IEnumerable).IsAssignableFrom(t);
        }

        /// <summary>
        /// Проверяет, является ли тип датой/временем <see cref="DateTypes"/>.
        /// </summary>
        /// <param name="t">Тип для проверки.</param>
        /// <returns>True, если тип представляет дату/время, иначе False.</returns>
        public static bool IsDate(this Type t) => DateTypes.Contains(t);

        /// <summary>
        /// Проверяет, является ли тип делегатом.
        /// </summary>
        /// <param name="type">Тип для проверки.</param>
        /// <returns>True, если тип является делегатом, иначе False.</returns>
        public static bool IsDelegate(this Type type) => typeof(MulticastDelegate).IsAssignableFrom(type.BaseType);

        /// <summary>
        /// Проверяет, является ли тип словарём.
        /// </summary>
        /// <param name="type">Тип для проверки.</param>
        /// <returns>True, если тип является словарём, иначе False.</returns>
        public static bool IsDictionary(this Type type) => IsImplements<IDictionary>(type) ||
                                                      (type.IsGenericType && type.GetGenericTypeDefinition() ==
                                                          typeof(Dictionary<,>)) || type
                                                          .GetInterfaces()
                                                          .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() ==
                                                              typeof(IDictionary<,>));

        /// <summary>
        /// Проверяет, является ли тип числом с плавающей точкой.
        /// </summary>
        /// <param name="t">Тип для проверки.</param>
        /// <returns>True, если тип является числом с плавающей точкой, иначе False.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFloat(this Type t)
        {
            return t == typeof(float)
                   || t == typeof(double)
                   || t == typeof(decimal);
        }

        /// <summary>
        /// Проверяет, является ли тип типизированной коллекцией.
        /// </summary>
        /// <param name="t">Тип для проверки.</param>
        /// <returns>True, если тип является коллекцией, иначе False.</returns>
        public static bool IsGenericCollection(this Type t)
        {
            var hasGenericType = t.GenericTypeArguments.Length > 0;
            return hasGenericType && IsCollection(t);
        }

        /// <summary>
        /// Определяет, реализует ли указанный тип заданный интерфейс или наследуется ли он от указанного базового типа.
        /// Сам тип <paramref name="implementType"/> не считается реализующим самого себя.
        /// </summary>
        /// <param name="t">Тип, который необходимо проверить.</param>
        /// <param name="implementType">Интерфейс или базовый тип, наличие реализации или наследования которого требуется проверить.</param>
        /// <returns>
        /// <see langword="true"/>, если тип <paramref name="t"/> реализует интерфейс или наследуется от <paramref name="implementType"/>,
        /// и при этом не совпадает с ним напрямую; иначе — <see langword="false"/>.
        /// </returns>
        public static bool IsImplements(this Type t, Type implementType) =>
            implementType.IsAssignableFrom(t) && t != implementType;

        /// <summary>
        /// Определяет, реализует ли указанный тип интерфейс или наследуется ли от типа <typeparamref name="T"/>.
        /// Сам тип <typeparamref name="T"/> не считается реализующим самого себя.
        /// </summary>
        /// <typeparam name="T">Интерфейс или базовый тип, наличие реализации или наследования которого требуется проверить.</typeparam>
        /// <param name="t">Тип, который необходимо проверить.</param>
        /// <returns>
        /// <see langword="true"/>, если тип <paramref name="t"/> реализует интерфейс или наследуется от <typeparamref name="T"/>,
        /// и при этом не совпадает с ним напрямую; иначе — <see langword="false"/>.
        /// </returns>
        public static bool IsImplements<T>(this Type t) => IsImplements(t, typeof(T));

        /// <summary>
        /// Проверяет, является ли тип целым числом.
        /// </summary>
        /// <param name="t">Тип для проверки.</param>
        /// <returns>True, если тип является целым числом, иначе False.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNaturalNumeric(this Type t)
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
        /// Проверяет, является ли тип nullable.
        /// </summary>
        /// <param name="t">Тип для проверки.</param>
        /// <returns>True, если тип является nullable, иначе False.</returns>
        public static bool IsNullable(this Type t) =>
            !t.IsValueType || Nullable.GetUnderlyingType(t) != null || t == typeof(object);

        /// <summary>
        /// Проверяет, является ли тип числовым.
        /// </summary>
        /// <param name="t">Тип для проверки.</param>
        /// <param name="includeFloatTypes">Включать ли типы с плавающей точкой.</param>
        /// <returns>True, если тип является числовым, иначе False.</returns>
        public static bool IsNumeric(this Type t, bool includeFloatTypes = true) =>
            includeFloatTypes ? IsFloat(t) || IsNaturalNumeric(t) : IsNaturalNumeric(t);

        /// <summary>
        /// Проверяет, является ли тип кортежем (ValueTuple/Tuple).
        /// </summary>
        /// <param name="type">Тип для проверки.</param>
        /// <returns>True, если тип является кортежем, иначе False.</returns>
        public static bool IsTuple(this Type type)
        {
            return type.FullName?.StartsWith("System.ValueTuple") == true || type.FullName?.StartsWith("System.Tuple") == true ||
                type.Name.Equals("ITuple");
        }

        /// <summary>
        /// Полностью удаляет пользовательский конвертер между двумя типами.
        /// </summary>
        /// <typeparam name="TFrom">Исходный тип.</typeparam>
        /// <typeparam name="TTo">Целевой тип.</typeparam>
        public static void RemoveCustomTypeConverter<TFrom, TTo>()
        {
            if (CustomTypeConverters.TryGetValue(typeof(TFrom), out var typeConverters) && typeConverters != null)
            {
                typeConverters.TryRemove(typeof(TTo), out _);
            }
        }

        /// <summary>
        /// Установить значение свойству или полю объекта по имени.<br/>
        /// Значение будет конвертировано в тип свойства или поля.<br/>
        /// Если тип значения отличается, то выполняется попытка конвертации <see cref="ChangeType(object, Type, IFormatProvider)"/><br/>
        /// Для массовых операций рекомендуется устанавливать значение через сеттер <see cref="GetMemberSetter(MemberInfo)"/>.
        /// </summary>
        /// <typeparam name="T">Тип экземпляра объекта.</typeparam>
        /// <param name="x">Экземпляр объекта.</param>
        /// <param name="name">Имя свойства или поля.</param>
        /// <param name="value">Значение, которое нужно установить.</param>
        /// <param name="stringComparison">Тип сравнения имени свойства или поля. Сначала ищутся свойства, затем поля.</param>
        /// <returns>True - если значение удалось установить, false - свойство или поле не найдено.</returns>
        public static bool SetValue<T>(this T x, string name, object value, StringComparison stringComparison = StringComparison.Ordinal)
            where T : class
            => SetValue((object)x, name, value, stringComparison);

        /// <summary>
        /// Установить значение свойству или полю объекта по имени. Если тип значения отличается, то выполняется попытка конвертации <see cref="ChangeType(object, Type, IFormatProvider)"/><br/>
        /// Значение будет конвертировано в тип свойства или поля.<br/>
        /// Для массовых операций рекомендуется устанавливать значение через сеттер <see cref="GetMemberSetter(MemberInfo)"/>.
        /// </summary>
        /// <param name="x">Экземпляр объекта.</param>
        /// <param name="name">Имя свойства или поля.</param>
        /// <param name="value">Значение, которое нужно установить.</param>
        /// <param name="stringComparison">Тип сравнения имени свойства или поля. Сначала ищутся свойства, затем поля.</param>
        /// <returns>True - если значение удалось установить, false - свойство или поле не найдено.</returns>
        public static bool SetValue(object x, string name, object value, StringComparison stringComparison = StringComparison.Ordinal)
        {
            if (x == null)
            {
                throw new NullReferenceException(nameof(x));
            }

            var m = GetPropertyOrField(x.GetType(), name, stringComparison);
            if (m == null)
            {
                return false;
            }

            var setter = GetMemberSetter(m);
            var mt = GetMemberType(m);
            setter(x, mt == value.GetType() ? value : ChangeType(value, mt));

            return true;
        }

        /// <summary>
        /// Пытается преобразовать значение к указанному типу <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Тип результата преобразования.</typeparam>
        /// <param name="value">Исходное значение.</param>
        /// <param name="result">
        /// При успешном выполнении содержит преобразованное значение;
        /// при ошибке — значение по умолчанию для типа <typeparamref name="T"/>.
        /// </param>
        /// <param name="provider">
        /// Провайдер форматирования, используемый при преобразовании.
        /// </param>
        /// <returns>
        /// <see langword="true"/>, если преобразование выполнено успешно;
        /// иначе — <see langword="false"/>.
        /// </returns>
        /// <remarks>
        /// Любые исключения, возникающие во время преобразования,
        /// подавляются.
        /// </remarks>
        public static bool TryChangeType<T>(this object value, out T result, IFormatProvider provider = null)
        {
            try
            {
                result = ChangeType<T>(value, provider);
                return true;
            }
            catch
            {
                result = default(T);
                return false;
            }
        }

        /// <summary>
        /// Пытается преобразовать значение к указанному типу.
        /// </summary>
        /// <param name="value">Исходное значение.</param>
        /// <param name="toType">Целевой тип преобразования.</param>
        /// <param name="result">
        /// При успешном выполнении содержит преобразованное значение;
        /// при ошибке — <see langword="null"/>.
        /// </param>
        /// <param name="provider">
        /// Провайдер форматирования, используемый при преобразовании.
        /// </param>
        /// <returns>
        /// <see langword="true"/>, если преобразование выполнено успешно;
        /// иначе — <see langword="false"/>.
        /// </returns>
        /// <remarks>
        /// Любые исключения, возникающие во время преобразования,
        /// подавляются.
        /// </remarks>
        public static bool TryChangeType(this object value, Type toType, out object result, IFormatProvider provider = null)
        {
            try
            {
                result = ChangeType(value, toType, provider);
                return true;
            }
            catch
            {
                result = null;
                return false;
            }
        }

        private static Expression BuildExpression(ParameterExpression input, Type fromType, Type toType)
        {
            Expression source = Expression.Convert(input, fromType);

            var targetNullable = Nullable.GetUnderlyingType(toType);
            var sourceNullable = Nullable.GetUnderlyingType(fromType);

            var realFrom = sourceNullable ?? fromType;
            var realTo = targetNullable ?? toType;

            // Nullable<T> -> T
            if (sourceNullable != null)
            {
                source = Expression.Property(source, "Value");
            }

            // прямой cast
            if (realTo.IsAssignableFrom(realFrom))
            {
                Expression result = Expression.Convert(source, realTo);

                if (targetNullable != null)
                {
                    result = Expression.New(toType.GetConstructor(new[] { realTo })!, result);
                }

                return Expression.Convert(result, typeof(object));
            }

            // enum
            if (realTo.IsEnum)
            {
                var underlying = Enum.GetUnderlyingType(realTo);

                Expression value = realFrom == typeof(string)
                    ? Expression.Call(
                        typeof(Enum).GetMethod(nameof(Enum.Parse), new[] { typeof(Type), typeof(string), typeof(bool) })!,
                        Expression.Constant(realTo),
                        source,
                        Expression.Constant(true))
                    : Expression.Convert(source, underlying);

                value = Expression.Convert(value, realTo);

                if (targetNullable != null)
                {
                    value = Expression.New(toType.GetConstructor(new[] { realTo })!, value);
                }

                return Expression.Convert(value, typeof(object));
            }

            // primitive numeric / bool / DateTime через Convert.ChangeType
            if (typeof(IConvertible).IsAssignableFrom(realFrom) &&
                typeof(IConvertible).IsAssignableFrom(realTo))
            {
                var call = Expression.Call(
                    typeof(Convert),
                    nameof(Convert.ChangeType),
                    Type.EmptyTypes,
                    Expression.Convert(source, typeof(object)),
                    Expression.Constant(realTo),
                    Expression.Constant(CultureInfo.InvariantCulture));

                Expression result = Expression.Convert(call, realTo);

                if (targetNullable != null)
                {
                    result = Expression.New(toType.GetConstructor(new[] { realTo })!, result);
                }

                return Expression.Convert(result, typeof(object));
            }

            // TypeConverter fallback
            var converter = TypeDescriptor.GetConverter(realTo);
            if (converter.CanConvertFrom(realFrom))
            {
                return Expression.Call(
                    Expression.Constant(new Func<object, object>(x => converter.ConvertFrom(null, CultureInfo.InvariantCulture, x)!)),
                    typeof(Func<object, object>).GetMethod("Invoke")!,
                    input);
            }

            throw new InvalidOperationException(
                $"Cannot convert from {fromType.FullName} to {toType.FullName}");
        }

        private static FieldInfo FindFieldByNamingPatterns(Type declaringType, string propertyName)
        {
            var property = declaringType.GetProperties(AllBindingFlags).FirstOrDefault(x => x.Name == propertyName);

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
                var field = declaringType.GetField(fieldName, AllBindingFlags);

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
                    var field = baseType.GetField(fieldName, AllBindingFlags);

                    if (field != null && field.FieldType == property.PropertyType)
                    {
                        return field;
                    }
                }

                baseType = baseType.BaseType;
            }

            return null;
        }

        private static object FromString(string s, Type toType, IFormatProvider provider)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                return null;
            }

            switch (GetTypeCodeCached(toType))
            {
                case TypeCode.String:
                    return s;

                case TypeCode.Boolean:
                    if (s == "1")
                    {
                        return true;
                    }

                    if (s == "0")
                    {
                        return false;
                    }

                    return bool.Parse(s);

                case TypeCode.Byte:
                    return byte.Parse(s, NumberStyles.Any, provider);

                case TypeCode.Int16:
                    return short.Parse(s, NumberStyles.Any, provider);

                case TypeCode.Int32:
                    return int.Parse(s, NumberStyles.Any, provider);

                case TypeCode.Int64:
                    return long.Parse(s, NumberStyles.Any, provider);

                case TypeCode.UInt16:
                    return ushort.Parse(s, NumberStyles.Any, provider);

                case TypeCode.UInt32:
                    return uint.Parse(s, NumberStyles.Any, provider);

                case TypeCode.UInt64:
                    return ulong.Parse(s, NumberStyles.Any, provider);

                case TypeCode.Single:
                    return float.Parse(s, NumberStyles.Any, provider);

                case TypeCode.Double:
                    return double.Parse(s, NumberStyles.Any, provider);

                case TypeCode.Decimal:
                    return decimal.Parse(s, NumberStyles.Any, provider);

                case TypeCode.DateTime:
                    return DateTime.TryParseExact(s.Trim(), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var result) ? result : null;

                case TypeCode.Char:
                    return s.Length == 0 ? '\0' : s[0];
            }

            if (toType == typeof(Guid))
            {
                return Guid.Parse(s);
            }

            if (toType == typeof(Uri))
            {
                return new Uri(s);
            }

            if (toType == typeof(TimeSpan))
            {
                return TimeSpan.Parse(s, provider);
            }

            if (toType.IsEnum)
            {
                return Enum.Parse(toType, s, true);
            }

            return Convert.ChangeType(s, toType, provider);
        }

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
                        if ((opCode == System.Reflection.Emit.OpCodes.Ldfld ||
                             opCode == System.Reflection.Emit.OpCodes.Ldsfld ||
                             opCode == System.Reflection.Emit.OpCodes.Ldflda ||
                             opCode == System.Reflection.Emit.OpCodes.Ldsflda) && i + 4 < ilBytes.Length)
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

        private static Action<T, TField> GetDirectFieldSetter<T, TField>(FieldInfo fi) => (instance, value) =>
        {
            var tr = __makeref(instance);
            fi.SetValueDirect(tr, value);
        };

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
        private static Func<TObject, TProperty> GetPropertyGetter<TObject, TProperty>(PropertyInfo pi)
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

        private static TypeCode GetTypeCodeCached(Type type)
        {
            return TypeCodeCache.GetOrAdd(type, Type.GetTypeCode);
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
                field.DeclaringType != null &&
                !field.DeclaringType.IsAssignableFrom(declaringType))
            {
                return false;
            }

            return true;
        }

        private static object ToEnum(object value, Type fromType, Type enumType, IFormatProvider provider)
        {
            if (fromType == typeof(string))
            {
                return Enum.Parse(enumType, (string)value, true);
            }

            if (fromType == typeof(bool))
            {
                return Enum.ToObject(enumType, (bool)value ? 1 : 0);
            }

            return Enum.ToObject(
                enumType,
                Convert.ToInt64(value, provider));
        }

        private static bool TryGetValue<TValue>(IReadOnlyDictionary<string, TValue> dic, string key, StringComparison stringComparison, out TValue result)
        {
            if (stringComparison == StringComparison.Ordinal)
            {
                return dic.TryGetValue(key, out result);
            }

            foreach (var kvp in dic)
            {
                if (string.Equals(kvp.Key, key, stringComparison))
                {
                    result = kvp.Value;
                    return true;
                }
            }

            result = default;
            return false;
        }
    }
}