// <copyright file="TypeHelper.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace System.Helpers
{
    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Предоставляет вспомогательные методы и предопределённые наборы для работы с объектами <see cref="Type"/>.
    /// Содержит проверки категорий типов, поиск реализаций, а также кэш отражения.
    /// </summary>
    public static class TypeHelper
    {
        private static readonly ConcurrentDictionary<Assembly, Type[]> AssemblyTypesCache = new();

        private static readonly ConcurrentDictionary<Type, Dictionary<Type, Func<object, object>>> CustomTypeConverters = new();

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

        private static readonly BindingFlags AllBindingFlags =
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.Instance |
            BindingFlags.Static |
            BindingFlags.FlattenHierarchy;

        private static readonly BindingFlags PrivateBindingFlags =
            BindingFlags.NonPublic |
            BindingFlags.Instance |
            BindingFlags.Static |
            BindingFlags.FlattenHierarchy;

        private static readonly BindingFlags PublicBindingFlags =
            BindingFlags.Public |
            BindingFlags.Instance |
            BindingFlags.Static |
            BindingFlags.FlattenHierarchy; // Позволяет видеть статические члены из базовых классов

        private static readonly ConcurrentDictionary<string, Type> TypeByFullNameCache = new();
        private static readonly ConcurrentDictionary<Type, TypeCode> TypeCodeCache = new();
        private static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, FieldInfo>> TypePrivateFieldsCache = new();
        private static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, PropertyInfo>> TypePrivatePropertiesCache = new();
        private static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, FieldInfo>> TypePublicFieldsCache = new();
        private static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, PropertyInfo>> TypePublicPropertiesCache = new();
        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> TypeIndexersCache = new();
        private static readonly ConcurrentDictionary<MemberInfo, Attribute[]> MemberAttributesCache = new();
        private static readonly ConcurrentDictionary<Type, ConstructorInfo[]> TypeConstructorsCache = new();
        private static readonly ConcurrentDictionary<Type, EventInfo[]> TypeEventsCache = new();
        private static readonly ConcurrentDictionary<Type, MethodInfo[]> TypeMethodsCache = new();

        static TypeHelper()
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
        /// Набор основных типов: object, char, char?, string, DateTime, DateTime?, TimeSpan, TimeSpan?, Guid, Guid?, Uri,Enum, <see cref="NumberTypes"/>, <see cref="BoolTypes"/>.
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
        /// Целочисленные типы (byte, int, long и т.д. с nullable и без).
        /// </summary>
        /// <value>The int number types.</value>
        public static HashSet<Type> IntNumberTypes { get; }

        /// <summary>
        /// Кеш информации о членах типов (полях, свойствах, методах и т.д.) для быстрого доступа по имени.
        /// </summary>
        /// <value><see cref="MemberCache"/>.</value>
        public static ConcurrentDictionary<string, MemberInfo> MemberInfoCache { get; } =
            new ConcurrentDictionary<string, MemberInfo>();

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
        /// Получает все атрибуты, применённые к указанному типу, используя внутренний кеш для ускорения повторных вызовов.
        /// </summary>
        /// <typeparam name="T">Тип, для которого нужно получить атрибуты.</typeparam>
        /// <returns>Массив атрибутовю.</returns>
        public static Attribute[] GetAttributes<T>()
            => GetAttributes(typeof(T));

        /// <summary>
        /// Получает все атрибуты, применённые к указанному типу, используя внутренний кеш для ускорения повторных вызовов.
        /// </summary>
        /// <param name="memberInfo"><see cref="MemberInfo"/>.</param>
        /// <returns>Массив атрибутовю.</returns>
        public static Attribute[] GetAttributes(MemberInfo memberInfo)
        {
            return MemberAttributesCache.GetOrAdd(memberInfo, t => t.GetCustomAttributes(true).Cast<Attribute>().ToArray());
        }

        /// <summary>
        /// Получает все конструкторы, применённые к указанному типу, используя внутренний кеш для ускорения повторных вызовов.
        /// </summary>
        /// <typeparam name="T">Тип, для которого нужно получить конструкторы.</typeparam>
        /// <returns>Массив конструкторов.</returns>
        public static ConstructorInfo[] GetConstructors<T>()
            => GetConstructors(typeof(T));

        /// <summary>
        /// Получает все конструкторы, применённые к указанному типу, используя внутренний кеш для ускорения повторных вызовов.
        /// </summary>
        /// <param name="type"><see cref="Type"/>.</param>
        /// <returns>Массив конструкторов.</returns>
        public static ConstructorInfo[] GetConstructors(Type type)
        {
            return TypeConstructorsCache.GetOrAdd(type, t => t.GetConstructors(AllBindingFlags).OrderBy(x => x.Name).ThenBy(x => x.GetParameters().Length).ToArray());
        }

        /// <summary>
        /// Получает все события, применённые к указанному типу, используя внутренний кеш для ускорения повторных вызовов.
        /// </summary>
        /// <typeparam name="T">Тип, для которого нужно получить события.</typeparam>
        /// <returns>Массив событий.</returns>
        public static EventInfo[] GetEvents<T>()
            => GetEvents(typeof(T));

        /// <summary>
        /// Получает все собятия, применённые к указанному типу, используя внутренний кеш для ускорения повторных вызовов.
        /// </summary>
        /// <param name="type"><see cref="Type"/>.</param>
        /// <returns>Массив событий.</returns>
        public static EventInfo[] GetEvents(Type type)
        {
            return TypeEventsCache.GetOrAdd(type, t => t.GetEvents(AllBindingFlags));
        }

        /// <summary>
        /// Получает все методов, применённые к указанному типу, используя внутренний кеш для ускорения повторных вызовов.
        /// </summary>
        /// <typeparam name="T">Тип, для которого нужно получить методы.</typeparam>
        /// <returns>Массив методов.</returns>
        public static MethodInfo[] GetMethods<T>()
            => GetMethods(typeof(T));

        /// <summary>
        /// Получает все методы, применённые к указанному типу, используя внутренний кеш для ускорения повторных вызовов.
        /// </summary>
        /// <param name="type"><see cref="Type"/>.</param>
        /// <returns>Массив методов.</returns>
        public static MethodInfo[] GetMethods(Type type)
        {
            return TypeMethodsCache.GetOrAdd(type, t => t.GetMethods(AllBindingFlags).OrderBy(x => x.Name).ThenBy(x => x.GetParameters().Length).ToArray());
        }

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
        public static T ChangeType<T>(object value, IFormatProvider provider = null) => (T)ChangeType(value, typeof(T), provider);

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
        public static object ChangeType(object value, Type conversionType, IFormatProvider provider = null)
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
        /// Очищает все зарегистрированные пользовательские конвертеры типов.
        /// </summary>
        public static void ClearCustomTypeConverters() => CustomTypeConverters.Clear();

        /// <summary>
        /// Получает цепочку базовых типов и/или интерфейсов без кеширования.
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
        public static Type GetCollectionItemType(Type type)
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
                        return iType.GetGenericArguments()[1];
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
        /// Возвращает первое найденное поле через <see cref="GetPublicField(Type, string, StringComparison)"/> или <see cref="GetPrivateField(Type, string, StringComparison)"/>.
        /// </summary>
        /// <param name="type">Тип в котором искать поле.</param>
        /// <param name="name">Имя поля.</param>
        /// <param name="comparison">Правило сравнения строк.</param>
        /// <returns>FieldInfo.</returns>
        public static FieldInfo GetField(Type type, string name, StringComparison comparison = StringComparison.Ordinal)
        {
            return
                GetPublicField(type, name, comparison) ??
                GetPrivateField(type, name, comparison);
        }

        /// <summary>
        /// Получает все поля указанного типа включая статические и автоматические backing-fields через <see cref="GetFields(Type)"/>.
        /// Использует внутренний кеш для ускорения повторных вызовов.
        /// </summary>
        /// <typeparam name="T">Тип, для которого нужно получить поля.</typeparam>
        /// <returns>Массив <see cref="FieldInfo" /> всех полей.</returns>
        public static IEnumerable<FieldInfo> GetFields<T>()
            where T : class => GetFields(typeof(T));

        /// <summary>
        /// Получает все поля указанного типа включая статические и автоматические backing-fields через объединение значений <see cref="GetPublicFieldsMap(Type)"/> и <see cref="GetPrivateFieldsMap(Type)"/>.
        /// Использует внутренний кеш для ускорения повторных вызовов.
        /// </summary>
        /// <param name="type">Тип, для которого нужно получить поля.</param>
        /// <returns>Массив <see cref="FieldInfo" /> всех полей.</returns>
        public static IEnumerable<FieldInfo> GetFields(Type type)
            => GetPublicFieldsMap(type).Values.Concat(GetPrivateFieldsMap(type).Values);

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
        public static Type[] GetImplementationsOf(Type baseType) => [.. AppDomain.CurrentDomain
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
        /// Возвращает все свойства-индексаторы (this[]) указанного типа, используя внутренний кеш для ускорения повторных вызовов.
        /// </summary>
        /// <param name="type">Тип в котором искать свойства индексаторы.</param>
        /// <returns>Массив PropertyInfo.</returns>
        public static IEnumerable<PropertyInfo> GetIndexers(Type type)
            => TypeIndexersCache.GetOrAdd(type, (x)
                =>
            {
                var props = x.GetProperties(AllBindingFlags).Where(p => p.GetIndexParameters().Length > 0).ToArray();
                return props;
            });

        /// <summary>
        /// Возвращает тип данных, связанный с указанным членом отражения.
        /// </summary>
        /// <param name="member">
        /// Объект <see cref="MemberInfo"/>, для которого необходимо определить тип.
        /// </param>
        /// <returns>
        /// Тип, соответствующий переданному члену:
        /// <list type="bullet">
        /// <item><see cref="PropertyInfo"/> — тип свойства.</item>
        /// <item><see cref="FieldInfo"/> — тип поля.</item>
        /// <item><see cref="EventInfo"/> — тип обработчика события.</item>
        /// <item><see cref="MethodInfo"/> — возвращаемый тип метода.</item>
        /// <item><see cref="Type"/> — сам тип.</item>
        /// <item><see cref="ConstructorInfo"/> — тип, объявляющий конструктор.</item>
        /// </list>
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Выбрасывается, если тип члена не поддерживается.
        /// </exception>
        public static Type GetMemberType(MemberInfo member)
        {
            switch (member)
            {
                case PropertyInfo p: return p.PropertyType;
                case FieldInfo f: return f.FieldType;
                case EventInfo e: return e.EventHandlerType;
                case MethodInfo m: return m.ReturnType;
                case Type t: return t;
                case ConstructorInfo c: return c.DeclaringType;
                default: throw new ArgumentException("Unsupported member type", nameof(member));
            }
        }

        /// <summary>
        /// Получает приватное поле по имени для указанного типа, используя внутренний кеш для ускорения повторных вызовов через <see cref="GetPrivateFieldsMap"/>.
        /// </summary>
        /// <param name="type">Тип в котором искать поле.</param>
        /// <param name="fieldName">Имя поля.</param>
        /// <param name="comparison">Правило сравнения строк.</param>
        /// <returns><see cref="FieldInfo"/>.</returns>
        public static FieldInfo GetPrivateField(Type type, string fieldName, StringComparison comparison = StringComparison.Ordinal)
            => GetPrivateFieldsMap(type).TryGetValue(fieldName, comparison, out var f) ? f : null;

        /// <summary>
        /// Получает приватное поле по имени для указанного типа, используя внутренний кеш для ускорения повторных вызовов через <see cref="GetPrivateFieldsMap"/>.
        /// </summary>
        /// <typeparam name="T">Тип, для которого нужно получить поле.</typeparam>
        /// <param name="fieldName">Имя поля.</param>
        /// <param name="comparison">Правило сравнения строк.</param>
        /// <returns><see cref="FieldInfo"/>.</returns>
        public static FieldInfo GetPrivateField<T>(string fieldName, StringComparison comparison = StringComparison.Ordinal)
            => GetPrivateField(typeof(T), fieldName, comparison);

        /// <summary>
        /// Получает имена всех приватные полей указанного типа включая статические через ключи <see cref="GetPrivateFieldsMap(Type)"/>.
        /// Использует внутренний кеш для ускорения повторных вызовов.
        /// </summary>
        /// <param name="type">Тип, для которого нужно получить имена полей.</param>
        /// <returns>Массив имен приватных полей.</returns>
        public static IEnumerable<string> GetPrivateFieldNames(Type type)
            => GetPrivateFieldsMap(type).Keys;

        /// <summary>
        /// Получает имена всех публичных полей указанного типа включая статические через <see cref="GetPrivateFieldNames(Type)"/>.
        /// Использует внутренний кеш для ускорения повторных вызовов.
        /// </summary>
        /// <typeparam name="T">Тип, для которого нужно получить имена полей.</typeparam>
        /// <returns>Массив имен публичных полей.</returns>
        public static IEnumerable<string> GetPrivateFieldNames<T>()
            => GetPrivateFieldNames(typeof(T));

        /// <summary>
        /// Получает все приватные поля указанного типа включая статические и автоматические backing-fields через <see cref="GetPrivateFields(Type)"/>.
        /// Использует внутренний кеш для ускорения повторных вызовов.
        /// </summary>
        /// <typeparam name="T">Тип, для которого нужно получить поля.</typeparam>
        /// <returns>Массив <see cref="FieldInfo" /> приватных полей.</returns>
        public static IEnumerable<FieldInfo> GetPrivateFields<T>()
            => GetPrivateFields(typeof(T));

        /// <summary>
        /// Получает все приватные поля указанного типа включая статические и автоматические backing-fields через значения <see cref="GetPrivateFieldsMap(Type)"/>.
        /// Использует внутренний кеш для ускорения повторных вызовов.
        /// </summary>
        /// <param name="type">Тип, для которого нужно получить поля.</param>
        /// <returns>Массив <see cref="FieldInfo" /> приватных полей.</returns>
        public static IEnumerable<FieldInfo> GetPrivateFields(Type type)
            => GetPrivateFieldsMap(type).Values;

        /// <summary>
        /// Получает все приватные поля указанного типа включая статические и автоматические backing-fields.
        /// Использует внутренний кеш для ускорения повторных вызовов.
        /// </summary>
        /// <param name="type">Тип, для которого нужно получить поля.</param>
        /// <returns>Массив <see cref="FieldInfo" /> приватных полей.</returns>
        public static IReadOnlyDictionary<string, FieldInfo> GetPrivateFieldsMap(Type type)
            => TypePrivateFieldsCache.GetOrAdd(type, (x) =>
            {
                var fields = x.GetFields(PrivateBindingFlags);
                return new ReadOnlyDictionary<string, FieldInfo>(fields.GroupBy(f => f.Name).ToDictionary(g => g.Key, g => g.First()));
            });

        /// <summary>
        /// Получает все приватные поля указанного типа включая статические через <see cref="GetPrivateFieldsMap(Type)"/>.
        /// Использует внутренний кеш для ускорения повторных вызовов.
        /// </summary>
        /// <typeparam name="T">Тип, для которого нужно получить поля.</typeparam>
        /// <returns>Массив <see cref="FieldInfo" /> приватных полей.</returns>
        public static IReadOnlyDictionary<string, FieldInfo> GetPrivateFieldsMap<T>()
            => GetPrivateFieldsMap(typeof(T));

        /// <summary>
        /// Получает все приватные свойства указанного типа включая статические, кроме свойств индексаторов (this[]) через <see cref="GetPrivateProperties(Type)"/>.
        /// Использует внутренний кеш для ускорения повторных вызовов.
        /// </summary>
        /// <typeparam name="T">Тип, для которого нужно получить свойства.</typeparam>
        /// <returns>Массив <see cref="PropertyInfo" /> всех свойств.</returns>
        public static IEnumerable<PropertyInfo> GetPrivateProperties<T>()
            where T : class => GetPrivateProperties(typeof(T));

        /// <summary>
        /// Получает все приватные свойства указанного типа включая статические, кроме свойств индексаторов (this[]) через значения <see cref="GetPrivatePropertiesMap(Type)"/>.
        /// Использует внутренний кеш для ускорения повторных вызовов.
        /// </summary>
        /// <param name="type">Тип, для которого нужно получить свойства.</param>
        /// <returns>Массив <see cref="PropertyInfo" /> всех свойств.</returns>
        public static IEnumerable<PropertyInfo> GetPrivateProperties(Type type)
            => GetPrivatePropertiesMap(type).Values;

        /// <summary>
        /// Получает все приватные свойства указанного типа включая статические, кроме свойств индексаторов (this[]).
        /// Использует внутренний кеш для ускорения повторных вызовов.
        /// </summary>
        /// <param name="type">Тип, для которого нужно получить свойства.</param>
        /// <returns>Массив <see cref="PropertyInfo" /> всех свойств.</returns>
        public static IReadOnlyDictionary<string, PropertyInfo> GetPrivatePropertiesMap(Type type)
            => TypePrivatePropertiesCache.GetOrAdd(type, (x)
                =>
            {
                var props = x.GetProperties(PrivateBindingFlags).Where(p => p.GetIndexParameters().Length == 0).ToArray();
                return new ReadOnlyDictionary<string, PropertyInfo>(props.GroupBy(p => p.Name).ToDictionary(g => g.Key, g => g.First()));
            });

        /// <summary>
        /// Получает все приватные свойства указанного типа включая статические, кроме свойств индексаторов (this[]) через <see cref="GetPrivatePropertiesMap(Type)"/>.
        /// Использует внутренний кеш для ускорения повторных вызовов.
        /// </summary>
        /// <typeparam name="T">Тип, для которого нужно получить свойства.</typeparam>
        /// <returns>Массив <see cref="PropertyInfo" /> всех свойств.</returns>
        public static IReadOnlyDictionary<string, PropertyInfo> GetPrivatePropertiesMap<T>()
            => GetPrivatePropertiesMap(typeof(T));

        /// <summary>
        /// Получает приватное свойство по имени для указанного типа, используя внутренний кеш для ускорения повторных вызовов через <see cref="GetPrivatePropertiesMap(Type)"/>.
        /// </summary>
        /// <param name="type">Тип в котором искать свойство.</param>
        /// <param name="propertyName">Имя свойства.</param>
        /// <param name="comparison">Правило сравнения строк.</param>
        /// <returns><see cref="PropertyInfo"/>.</returns>
        public static PropertyInfo GetPrivateProperty(Type type, string propertyName, StringComparison comparison = StringComparison.Ordinal)
            => GetPrivatePropertiesMap(type).TryGetValue(propertyName, comparison, out var p) ? p : null;

        /// <summary>
        /// Получает приватное свойство по имени для указанного типа, используя внутренний кеш для ускорения повторных вызовов через <see cref="GetPrivateProperty(Type, string, StringComparison)"/>.
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
        public static IEnumerable<string> GetPrivatePropertyNames(Type type)
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
        public static MemberInfo GetPrivatePropertyOrField(Type type, string name, StringComparison comparison = StringComparison.Ordinal)
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
        /// Использует внутренний кеш для ускорения повторных вызовов.
        /// </summary>
        /// <typeparam name="T">Тип, для которого нужно получить свойства.</typeparam>
        /// <returns>Массив <see cref="PropertyInfo" /> всех свойств.</returns>
        public static IEnumerable<PropertyInfo> GetProperties<T>()
            where T : class => GetProperties(typeof(T));

        /// <summary>
        /// Получает все свойства указанного типа включая статические, кроме свойств индексаторов (this[]) через объединение значений <see cref="GetPublicPropertiesMap(Type)"/> и <see cref="GetPrivatePropertiesMap(Type)"/>.
        /// Использует внутренний кеш для ускорения повторных вызовов.
        /// </summary>
        /// <param name="type">Тип, для которого нужно получить свойства.</param>
        /// <returns>Массив <see cref="PropertyInfo" /> всех свойств.</returns>
        public static IEnumerable<PropertyInfo> GetProperties(Type type)
            => GetPublicPropertiesMap(type).Values.Concat(GetPrivatePropertiesMap(type).Values);

        /// <summary>
        /// Возвращает первое найденное свойство через <see cref="GetPublicProperty(Type, string, StringComparison)"/> или <see cref="GetPrivateProperty(Type, string, StringComparison)"/>.
        /// </summary>
        /// <param name="type">Тип в котором искать свойство.</param>
        /// <param name="name">Имя свойства.</param>
        /// <param name="comparison">Правило сравнения строк.</param>
        /// <returns>PropertyInfo.</returns>
        public static PropertyInfo GetProperty(Type type, string name, StringComparison comparison = StringComparison.Ordinal)
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
        /// Возвращает первое найденное свойство или поле через <see cref="GetPublicPropertyOrField(Type, string, StringComparison)"/> или <see cref="GetPrivatePropertyOrField(Type, string, StringComparison)"/>.
        /// </summary>
        /// <param name="type">Тип в котором искать свойство или поле.</param>
        /// <param name="name">Имя свойства или поля.</param>
        /// <param name="comparison">Правило сравнения строк.</param>
        /// <returns>PropertyInfo или FieldInfo.</returns>
        public static MemberInfo GetPropertyOrField(Type type, string name, StringComparison comparison = StringComparison.Ordinal)
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
        /// Получает публичное поле по имени для указанного типа, используя внутренний кеш для ускорения повторных вызовов через <see cref="GetPublicFieldsMap(Type)"/>.
        /// </summary>
        /// <param name="type">Тип в котором искать поле.</param>
        /// <param name="fieldName">Имя поля.</param>
        /// <param name="comparison">Правило сравнения строк.</param>
        /// <returns><see cref="FieldInfo"/>.</returns>
        public static FieldInfo GetPublicField(Type type, string fieldName, StringComparison comparison = StringComparison.Ordinal)
            => GetPublicFieldsMap(type).TryGetValue(fieldName, comparison, out var f) ? f : null;

        /// <summary>
        /// Получает публичное поле по имени для указанного типа, используя внутренний кеш для ускорения повторных вызовов через <see cref="GetPublicField(Type, string, StringComparison)"/>.
        /// </summary>
        /// <typeparam name="T">Тип в котором искать поле.</typeparam>
        /// <param name="fieldName">Имя поля.</param>
        /// <param name="comparison">Правило сравнения строк.</param>
        /// <returns><see cref="FieldInfo"/>.</returns>
        public static FieldInfo GetPublicField<T>(string fieldName, StringComparison comparison = StringComparison.Ordinal)
            => GetPublicField(typeof(T), fieldName, comparison);

        /// <summary>
        /// Получает имена всех публичных полей указанного типа включая статические через ключи <see cref="GetPublicFieldsMap(Type)"/>.
        /// Использует внутренний кеш для ускорения повторных вызовов.
        /// </summary>
        /// <param name="type">Тип, для которого нужно получить имена полей.</param>
        /// <returns>Массив имен публичных полей.</returns>
        public static IEnumerable<string> GetPublicFieldNames(Type type)
            => GetPublicFieldsMap(type).Keys;

        /// <summary>
        /// Получает имена всех публичных полей указанного типа включая статические через <see cref="GetPublicFieldNames(Type)"/>.
        /// Использует внутренний кеш для ускорения повторных вызовов.
        /// </summary>
        /// <typeparam name="T">Тип, для которого нужно получить имена полей.</typeparam>
        /// <returns>Массив имен публичных полей.</returns>
        public static IEnumerable<string> GetPublicFieldNames<T>()
            => GetPublicFieldNames(typeof(T));

        /// <summary>
        /// Получает все публичные поля указанного типа включая статические через <see cref="GetPublicFields(Type)"/>.
        /// Использует внутренний кеш для ускорения повторных вызовов.
        /// </summary>
        /// <typeparam name="T">Тип, для которого нужно получить поля.</typeparam>
        /// <returns>Массив <see cref="FieldInfo" /> публичных полей.</returns>
        public static IEnumerable<FieldInfo> GetPublicFields<T>()
            => GetPublicFields(typeof(T));

        /// <summary>
        /// Получает все публичные поля указанного типа включая статические через значения <see cref="GetPublicFieldsMap(Type)"/>.
        /// Использует внутренний кеш для ускорения повторных вызовов.
        /// </summary>
        /// <param name="type">Тип, для которого нужно получить поля.</param>
        /// <returns>Массив <see cref="FieldInfo" /> публичных полей.</returns>
        public static IEnumerable<FieldInfo> GetPublicFields(Type type)
            => GetPublicFieldsMap(type).Values;

        /// <summary>
        /// Получает все публичные поля указанного типа включая статические.
        /// Использует внутренний кеш для ускорения повторных вызовов.
        /// </summary>
        /// <param name="type">Тип, для которого нужно получить поля.</param>
        /// <returns>Массив <see cref="FieldInfo" /> публичных полей.</returns>
        public static IReadOnlyDictionary<string, FieldInfo> GetPublicFieldsMap(Type type)
            => TypePublicFieldsCache.GetOrAdd(type, (x) =>
            {
                var fields = x.GetFields(PublicBindingFlags);
                return new ReadOnlyDictionary<string, FieldInfo>(fields.GroupBy(f => f.Name).ToDictionary(g => g.Key, g => g.First()));
            });

        /// <summary>
        /// Получает все публичные поля указанного типа включая статические через <see cref="GetPublicFieldsMap(Type)"/>.
        /// Использует внутренний кеш для ускорения повторных вызовов.
        /// </summary>
        /// <typeparam name="T">Тип, для которого нужно получить поля.</typeparam>
        /// <returns>Массив <see cref="FieldInfo" /> публичных полей.</returns>
        public static IReadOnlyDictionary<string, FieldInfo> GetPublicFieldsMap<T>()
            => GetPublicFieldsMap(typeof(T));

        /// <summary>
        /// Получает все публичные свойства указанного типа включая статические, кроме свойств индексаторов (this[]) через <see cref="GetPublicProperties(Type)"/>.
        /// Использует внутренний кеш для ускорения повторных вызовов.
        /// </summary>
        /// <typeparam name="T">Тип, для которого нужно получить свойства.</typeparam>
        /// <returns>Массив <see cref="PropertyInfo" /> всех свойств.</returns>
        public static IEnumerable<PropertyInfo> GetPublicProperties<T>()
            where T : class => GetPublicProperties(typeof(T));

        /// <summary>
        /// Получает все публичные свойства указанного типа включая статические, кроме свойств индексаторов (this[]) через значения <see cref="GetPublicPropertiesMap(Type)"/>.
        /// Использует внутренний кеш для ускорения повторных вызовов.
        /// </summary>
        /// <param name="type">Тип, для которого нужно получить свойства.</param>
        /// <returns>Массив <see cref="PropertyInfo" /> всех свойств.</returns>
        public static IEnumerable<PropertyInfo> GetPublicProperties(Type type)
            => GetPublicPropertiesMap(type).Values;

        /// <summary>
        /// Получает все публичные свойства указанного типа включая статические, кроме свойств индексаторов (this[]).
        /// Использует внутренний кеш для ускорения повторных вызовов.
        /// </summary>
        /// <param name="type">Тип, для которого нужно получить свойства.</param>
        /// <returns>Массив <see cref="PropertyInfo" /> всех свойств.</returns>
        public static IReadOnlyDictionary<string, PropertyInfo> GetPublicPropertiesMap(Type type)
            => TypePublicPropertiesCache.GetOrAdd(type, (x)
                =>
            {
                var props = x.GetProperties(PublicBindingFlags).Where(p => p.GetIndexParameters().Length == 0).ToArray();
                return new ReadOnlyDictionary<string, PropertyInfo>(props.GroupBy(p => p.Name).ToDictionary(g => g.Key, g => g.First()));
            });

        /// <summary>
        /// Получает все публичные свойства указанного типа включая статические, кроме свойств индексаторов (this[]) через <see cref="GetPublicPropertiesMap(Type)"/>.
        /// Использует внутренний кеш для ускорения повторных вызовов.
        /// </summary>
        /// <typeparam name="T">Тип, для которого нужно получить свойства.</typeparam>
        /// <returns>Массив <see cref="PropertyInfo" /> всех свойств.</returns>
        public static IReadOnlyDictionary<string, PropertyInfo> GetPublicPropertiesMap<T>()
            => GetPublicPropertiesMap(typeof(T));

        /// <summary>
        /// Получает публичное свойство по имени для указанного типа, используя внутренний кеш для ускорения повторных вызовов через <see cref="GetPublicPropertiesMap(Type)"/>.
        /// </summary>
        /// <param name="type">Тип в котором искать свойство.</param>
        /// <param name="propertyName">Имя свойства.</param>
        /// <param name="comparison">Правило сравнения строк.</param>
        /// <returns><see cref="PropertyInfo"/>.</returns>
        public static PropertyInfo GetPublicProperty(Type type, string propertyName, StringComparison comparison = StringComparison.Ordinal)
            => GetPublicPropertiesMap(type).TryGetValue(propertyName, comparison, out var p) ? p : null;

        /// <summary>
        /// Получает публичное свойство по имени для указанного типа, используя внутренний кеш для ускорения повторных вызовов через <see cref="GetPublicProperty(Type, string, StringComparison)"/>.
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
        public static IEnumerable<string> GetPublicPropertyNames(Type type)
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
        public static MemberInfo GetPublicPropertyOrField(Type type, string name, StringComparison comparison = StringComparison.Ordinal)
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
        /// <param name="stringComparer">Правило сравнения строк.</param>
        /// <returns>Тип.</returns>
        public static Type GetType(string typeName, StringComparer stringComparer = null)
        {
            if (typeName == null)
            {
                throw new ArgumentNullException(nameof(typeName));
            }

            stringComparer ??= StringComparer.Ordinal;
            var isFullName = typeName.Contains('.');

            if (isFullName && TypeByFullNameCache.TryGetValue(typeName, stringComparer.ToStringComparison(), out var t))
            {
                return t;
            }

            t = Type.GetType(typeName) ??
            (isFullName ?
            GetTypes(t => stringComparer.Equals(t.FullName, typeName)).FirstOrDefault() :
            GetTypes(t => stringComparer.Equals(t.Name, typeName)).FirstOrDefault());
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
        public static Type[] GetTypes(Assembly assembly)
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
        /// Проверяет, является ли тип простым (базовым) <see cref="BasicTypes"/>.
        /// </summary>
        /// <param name="t">Тип для проверки.</param>
        /// <returns>True, если тип является базовым, иначе False.</returns>
        public static bool IsBasic(Type t) => t != null && (t.IsEnum || BasicTypes.Contains(t));

        /// <summary>
        /// Проверяет, является ли тип логическим <see cref="BoolTypes"/>.
        /// </summary>
        /// <param name="t">Тип для проверки.</param>
        /// <returns>True, если тип является логическим, иначе False.</returns>
        public static bool IsBoolean(Type t) => BoolTypes.Contains(t);

        /// <summary>
        /// Проверяет, является ли тип коллекцией (IsArray, IList, ICollection, IEnumerable) кроме string.
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

            return typeof(IList).IsAssignableFrom(t) || typeof(ICollection).IsAssignableFrom(t) ||
                   typeof(IEnumerable).IsAssignableFrom(t);
        }

        /// <summary>
        /// Проверяет, является ли тип датой/временем <see cref="DateTypes"/>.
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
        public static bool IsFloat(Type t)
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
        public static bool IsGenericCollection(Type t)
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
        public static bool IsImplements(Type t, Type implementType) =>
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
        public static bool IsImplements<T>(Type t) => IsImplements(t, typeof(T));

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
        /// Проверяет, является ли тип nullable.
        /// </summary>
        /// <param name="t">Тип для проверки.</param>
        /// <returns>True, если тип является nullable, иначе False.</returns>
        public static bool IsNullable(Type t) =>
            !t.IsValueType || Nullable.GetUnderlyingType(t) != null || t == typeof(object);

        /// <summary>
        /// Проверяет, является ли тип числовым.
        /// </summary>
        /// <param name="t">Тип для проверки.</param>
        /// <param name="includeFloatTypes">Включать ли типы с плавающей точкой.</param>
        /// <returns>True, если тип является числовым, иначе False.</returns>
        public static bool IsNumeric(Type t, bool includeFloatTypes = true) =>
            includeFloatTypes ? IsFloat(t) || IsNaturalNumeric(t) : IsNaturalNumeric(t);

        /// <summary>
        /// Проверяет, является ли тип кортежем (ValueTuple/Tuple).
        /// </summary>
        /// <param name="type">Тип для проверки.</param>
        /// <returns>True, если тип является кортежем, иначе False.</returns>
        public static bool IsTuple(Type type)
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
                typeConverters.Remove(typeof(TTo));
            }
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
        public static bool TryChangeType<T>(object value, out T result, IFormatProvider provider = null)
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
        public static bool TryChangeType(object value, Type toType, out object result, IFormatProvider provider = null)
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

        private static TypeCode GetTypeCodeCached(Type type)
        {
            return TypeCodeCache.GetOrAdd(type, Type.GetTypeCode);
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
    }
}