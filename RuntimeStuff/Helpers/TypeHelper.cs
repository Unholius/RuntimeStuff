namespace System.Helpers
{
    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Предоставляет вспомогательные методы и предопределённые наборы для работы с объектами <see cref="Type"/>.
    /// Содержит проверки категорий типов, поиск реализаций, а также кэш отражения.
    /// </summary>
    public static class TypeHelper
    {
        private static readonly BindingFlags BindingFlags =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.Static |
            BindingFlags.FlattenHierarchy; // Позволяет видеть статические члены из базовых классов

        private static readonly ConcurrentDictionary<Assembly, Type[]> AssemblyTypesCache = new();
        private static readonly ConcurrentDictionary<string, Type> TypeByFullNameCache = new();
        private static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, PropertyInfo>> TypePublicPropertiesCache = new();
        private static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, FieldInfo>> TypePublicFieldsCache = new();

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
        /// Поиска типа в загруженных сборках по полному имени (namespace + имя типа), если в имени содержатся точки и по короткому имени, если точек нет.
        /// </summary>
        /// <param name="typeName">Полное имя типа.</param>
        /// <returns>Тип.</returns>
        public static Type GetType(string typeName)
        {
            var isFullName = typeName.Contains('.');

            if (isFullName && TypeByFullNameCache.TryGetValue(typeName, out var t))
            {
                return t;
            }

            t = Type.GetType(typeName) ??
            (isFullName ?
            GetTypes(t => StringComparer.Ordinal.Equals(t.FullName, typeName)).FirstOrDefault() :
            GetTypes(t => StringComparer.Ordinal.Equals(t.Name, typeName)).FirstOrDefault());
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

            return typeof(IList).IsAssignableFrom(t) || typeof(ICollection).IsAssignableFrom(t) ||
                   typeof(IEnumerable).IsAssignableFrom(t);
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
        /// Получает все публичные свойства указанного типа включая статические, кроме свойств индексаторов (this[]).
        /// Использует внутренний кеш для ускорения повторных вызовов.
        /// </summary>
        /// <typeparam name="T">Тип, для которого нужно получить свойства.</typeparam>
        /// <returns>Массив <see cref="PropertyInfo" /> всех свойств.</returns>
        public static IEnumerable<PropertyInfo> GetPublicProperties<T>()
            where T : class => GetPublicProperties(typeof(T));

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
                var props = x.GetProperties(BindingFlags).Where(p => p.GetIndexParameters().Length == 0).ToArray();
                return new ReadOnlyDictionary<string, PropertyInfo>(props.GroupBy(p => p.Name).ToDictionary(g => g.Key, g => g.First()));
            });

        /// <summary>
        /// Получает публичное свойство по имени для указанного типа, используя внутренний кеш для ускорения повторных вызовов.
        /// </summary>
        /// <param name="type">Тип в котором искать свойство.</param>
        /// <param name="propertyName">Имя свойства.</param>
        /// <returns><see cref="PropertyInfo"/>.</returns>
        public static PropertyInfo GetPublicProperty(Type type, string propertyName)
            => GetPublicPropertiesMap(type).TryGetValue(propertyName, out var p) ? p : null;

        /// <summary>
        /// Получает все публичные свойства указанного типа включая статические, кроме свойств индексаторов (this[]).
        /// Использует внутренний кеш для ускорения повторных вызовов.
        /// </summary>
        /// <param name="type">Тип, для которого нужно получить свойства.</param>
        /// <returns>Массив <see cref="PropertyInfo" /> всех свойств.</returns>
        public static IEnumerable<PropertyInfo> GetPublicProperties(Type type)
            => GetPublicPropertiesMap(type).Values;

        /// <summary>
        /// Получает имена всех публичных свойств типа.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>System.String[].</returns>
        public static IEnumerable<string> GetPublicPropertyNames(Type type)
            => GetPublicPropertiesMap(type).Keys;

        /// <summary>
        /// Получает публичное поле по имени для указанного типа, используя внутренний кеш для ускорения повторных вызовов.
        /// </summary>
        /// <param name="type">Тип в котором искать поле.</param>
        /// <param name="fieldName">Имя поля.</param>
        /// <returns><see cref="FieldInfo"/>.</returns>
        public static FieldInfo GetPublicField(Type type, string fieldName)
            => GetPublicFieldsMap(type).TryGetValue(fieldName, out var f) ? f : null;


        /// <summary>
        /// Получает все публичные поля указанного типа включая статические.
        /// Использует внутренний кеш для ускорения повторных вызовов.
        /// </summary>
        /// <typeparam name="T">Тип, для которого нужно получить поля.</typeparam>
        /// <returns>Массив <see cref="FieldInfo" /> публичных полей.</returns>
        public static IEnumerable<FieldInfo> GetPublicFields<T>()
            => GetPublicFields(typeof(T));

        /// <summary>
        /// Получает все публичные поля указанного типа включая статические.
        /// Использует внутренний кеш для ускорения повторных вызовов.
        /// </summary>
        /// <param name="type">Тип, для которого нужно получить поля.</param>
        /// <returns>Массив <see cref="FieldInfo" /> публичных полей.</returns>
        public static IEnumerable<FieldInfo> GetPublicFields(Type type)
            => GetPublicFieldsMap(type).Values;

        /// <summary>
        /// Получает имена всех публичных полей указанного типа включая статические.
        /// Использует внутренний кеш для ускорения повторных вызовов.
        /// </summary>
        /// <param name="type">Тип, для которого нужно получить имена полей.</param>
        /// <returns>Массив имен публичных полей.</returns>
        public static IEnumerable<string> GetPublicFieldNames(Type type)
            => GetPublicFieldsMap(type).Keys;

        /// <summary>
        /// Получает имена всех публичных полей указанного типа включая статические.
        /// Использует внутренний кеш для ускорения повторных вызовов.
        /// </summary>
        /// <typeparam name="T">Тип, для которого нужно получить имена полей.</typeparam>
        /// <returns>Массив имен публичных полей.</returns>
        public static IEnumerable<string> GetPublicFieldNames<T>()
            => GetPublicFieldNames(typeof(T));

        /// <summary>
        /// Получает все публичные поля указанного типа включая статические.
        /// Использует внутренний кеш для ускорения повторных вызовов.
        /// </summary>
        /// <param name="type">Тип, для которого нужно получить поля.</param>
        /// <returns>Массив <see cref="FieldInfo" /> публичных полей.</returns>
        public static IReadOnlyDictionary<string, FieldInfo> GetPublicFieldsMap(Type type)
            => TypePublicFieldsCache.GetOrAdd(type, (x) =>
            {
                var fields = x.GetFields(BindingFlags);
                return new ReadOnlyDictionary<string, FieldInfo>(fields.GroupBy(f => f.Name).ToDictionary(g => g.Key, g => g.First()));
            });

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
    }
}