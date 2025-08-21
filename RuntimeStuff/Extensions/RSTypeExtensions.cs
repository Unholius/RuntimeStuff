using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlTypes;
using System.Linq;
using System.Reflection;

namespace RuntimeStuff.Extensions
{
    /// <summary>
    /// Расширения для работы с типами (System.Type), упрощающие проверку,
    /// создание экземпляров, получение информации и метаданных о типах.
    /// </summary>
    public static class RSTypeExtensions
    {

        /// <summary>
        /// Типы, представляющие логические значения.
        /// </summary>
        public static Type[] BoolTypes { get; } = new Type[]
        {
            typeof(bool),
            typeof(bool?),
            typeof(SqlBoolean),
            typeof(SqlBoolean?),
        };

        /// <summary>
        /// Типы, представляющие дату и время.
        /// </summary>
        public static Type[] DateTypes { get; } = new Type[]
        {
            typeof(DateTime), typeof(DateTime?),
        };

        /// <summary>
        /// Типы с плавающей запятой (float, double, decimal).
        /// </summary>
        public static Type[] FloatNumberTypes { get; } = new Type[]
        {
            typeof(float), typeof(double), typeof(decimal),
            typeof(float?), typeof(double?), typeof(decimal?)
        };

        /// <summary>
        /// Карта интерфейсов коллекций к конкретным типам реализаций.
        /// </summary>
        public static Dictionary<Type, Type> InterfaceToInstanceMap { get; } = new Dictionary<Type, Type>()
        {
            {typeof(IEnumerable), typeof(List<object>) },
            {typeof(IEnumerable<>), typeof(List<>) },
            {typeof(ICollection), typeof(ObservableCollection<object>) },
            {typeof(ICollection<>), typeof(ObservableCollection<>) },
            {typeof(IDictionary<,>), typeof(Dictionary<,>) },
        };

        /// <summary>
        /// Целочисленные типы (byte, int, long и т.д. с nullable и без).
        /// </summary>
        public static Type[] IntNumberTypes { get; } = new[] {
            typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(short), typeof(ushort), typeof(byte), typeof(sbyte),
            typeof(int?), typeof(uint?), typeof(long?), typeof(ulong?), typeof(short?), typeof(ushort?), typeof(byte?), typeof(sbyte?)
        };

        /// <summary>
        /// Значения, трактуемые как null (null, DBNull, NaN).
        /// </summary>
        public static object[] NullValues { get; } = new object[] { null, DBNull.Value, double.NaN, float.NaN };

        /// <summary>
        /// Все числовые типы: целочисленные и с плавающей точкой.
        /// </summary>
        public static Type[] NumberTypes { get; } = IntNumberTypes.Concat(FloatNumberTypes).ToArray();

        /// <summary>
        /// Набор основных типов: числа, логические, строки, даты, Guid, Enum и др.
        /// </summary>
        public static Type[] BasicTypes { get; } =
            NumberTypes
                .Concat(BoolTypes)
                .Concat(new Type[] { typeof(string), typeof(DateTime), typeof(DateTime?), typeof(TimeSpan), typeof(Guid), typeof(Guid?), typeof(char), typeof(char?), typeof(Enum) })
                .ToArray();

        /// <summary>
        /// Создаёт экземпляр указанного типа с возможностью передачи аргументов конструктора.
        /// </summary>
        /// <param name="type">Тип, экземпляр которого нужно создать.</param>
        /// <param name="ctorArgs">Аргументы конструктора.</param>
        /// <returns>Созданный экземпляр указанного типа.</returns>
        public static object Create(this Type type, params object[] ctorArgs)
        {
            if (ctorArgs == null)
                ctorArgs = Array.Empty<object>();

            var typeInfo = type.GetMemberInfoEx() ?? throw new NullReferenceException(nameof(RSTypeExtensions) + "." + nameof(Create) + ": type is null!");
            if (typeInfo.DefaultConstructor != null && ctorArgs.Length == 0)
                return typeInfo.DefaultConstructor();

            if (typeInfo.IsDelegate)
                return null;

            if (type.IsInterface)
            {
                if (typeInfo.IsCollection)
                {
                    if (!InterfaceToInstanceMap.TryGetValue(type, out var lstType))
                        InterfaceToInstanceMap.TryGetValue(type.GetGenericTypeDefinition(), out lstType);

                    var genericArgs = type.GetGenericArguments();
                    if (genericArgs.Length == 0)
                        genericArgs = new[] { typeof(object) };
                    if (lstType.IsGenericTypeDefinition)
                        lstType = lstType.MakeGenericType(genericArgs);
                    return Activator.CreateInstance(lstType);
                }
                else
                {
                    throw new NotImplementedException();
                }
            }

            if (type.IsArray)
            {
                if (ctorArgs.Length == 0)
                    return Activator.CreateInstance(type, 0);
                if (ctorArgs.Length == 1 && ctorArgs[0] is int)
                    return Activator.CreateInstance(type, ctorArgs[0]);
                return Activator.CreateInstance(type, ctorArgs.Length);
            }

            if (type.IsEnum)
            {
                return ctorArgs.FirstOrDefault(x => x?.GetType() == type) ?? Default(type);
            }

            if (type == typeof(string) && ctorArgs.Length == 0)
                return string.Empty;

            var defaultCtor = typeInfo.DefaultConstructor;
            if (defaultCtor != null && ctorArgs.Length == 0)
            {
                try
                {
                    return defaultCtor();
                }
                catch
                {
                    return Default(type);
                }
            }

            var ctor = typeInfo.GetConstructorByArgs(ref ctorArgs);

            if (ctor == null && type.IsValueType)
                return Default(type);

            if (ctor == null)
                throw new InvalidOperationException($"Не найден конструктор для типа '{type}' с аргументами '{string.Join(",", ctorArgs.Select(arg => arg?.GetType()))}'");

            return ctor.Invoke(ctorArgs);
        }

        /// <summary>
        /// Возвращает значение по умолчанию для указанного типа.
        /// </summary>
        /// <param name="type">Тип, для которого нужно получить значение по умолчанию.</param>
        /// <returns>Значение по умолчанию для указанного типа.</returns>
        public static object Default(this Type type)
        {
            return type?.IsValueType == true ? Activator.CreateInstance(type) : null;
        }

        /// <summary>
        /// Получает цепочку базовых типов и/или интерфейсов.
        /// </summary>
        /// <param name="type">Тип, для которого нужно получить базовые типы.</param>
        /// <param name="includeThis">Включать ли текущий тип в результат.</param>
        /// <param name="getInterfaces">Включать ли интерфейсы в результат.</param>
        /// <returns>Массив базовых типов и/или интерфейсов.</returns>
        public static Type[] GetBaseTypes(this Type type, bool includeThis = false, bool getInterfaces = false)
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
        /// Возвращает тип элементов коллекции.
        /// </summary>
        /// <param name="type">Тип коллекции.</param>
        /// <returns>Тип элементов коллекции или null, если тип не является коллекцией.</returns>
        public static Type GetCollectionItemType(this Type type)
        {
            if (type == null)
                return null;
            var isDic = typeof(IDictionary).IsAssignableFrom(type);
            var ga = type.GetGenericArguments();
            return type.IsArray
                ? type.GetElementType()
                : isDic && ga.Length > 1 ? ga[1] : ga.FirstOrDefault();
        }

        /// <summary>
        /// Возвращает поле по условию фильтрации.
        /// </summary>
        /// <param name="type">Тип, в котором нужно найти поле.</param>
        /// <param name="matchCriteria">Условие фильтрации полей.</param>
        /// <returns>Найденное поле или null, если поле не найдено.</returns>
        public static FieldInfo GetField(this Type type, Func<FieldInfo, bool> matchCriteria)
        {
            return type.GetTypeInfo().DeclaredFields.FirstOrDefault(matchCriteria);
        }

        /// <summary>
        /// Получает событие с наименьшего уровня иерархии.
        /// </summary>
        /// <param name="type">Тип, с которого начинается поиск.</param>
        /// <param name="name">Имя события.</param>
        /// <returns>Найденное событие или null, если событие не найдено.</returns>
        public static EventInfo GetLowestEvent(this Type type, string name)
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
        /// Получает поле с наименьшего уровня иерархии.
        /// </summary>
        /// <param name="type">Тип, с которого начинается поиск.</param>
        /// <param name="name">Имя поля.</param>
        /// <returns>Найденное поле или null, если поле не найдено.</returns>
        public static FieldInfo GetLowestField(this Type type, string name)
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
        /// Получает метод с наименьшего уровня иерархии.
        /// </summary>
        /// <param name="type">Тип, с которого начинается поиск.</param>
        /// <param name="name">Имя метода.</param>
        /// <returns>Найденный метод или null, если метод не найден.</returns>
        public static MethodInfo GetLowestMethod(this Type type, string name)
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
        /// Получает свойство с наименьшего уровня иерархии.
        /// </summary>
        /// <param name="type">Тип, с которого начинается поиск.</param>
        /// <param name="name">Имя свойства.</param>
        /// <returns>Найденное свойство или null, если свойство не найдено.</returns>
        public static PropertyInfo GetLowestProperty(this Type type, string name)
        {
            while (type != null)
            {
                var member = type.GetProperty(name, BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance);
                if (member != null)
                    return member;
                type = type.BaseType;
            }
            return null;
        }

        /// <summary>
        /// Проверяет, является ли тип простым (базовым).
        /// </summary>
        /// <param name="t">Тип для проверки.</param>
        /// <returns>True, если тип является базовым, иначе False.</returns>
        public static bool IsBasic(this Type t)
        {
            return t != null && (t.IsEnum || BasicTypes.Contains(t));
        }

        /// <summary>
        /// Проверяет, является ли тип логическим.
        /// </summary>
        /// <param name="t">Тип для проверки.</param>
        /// <returns>True, если тип является логическим, иначе False.</returns>
        public static bool IsBoolean(this Type t)
        {
            return BoolTypes.Contains(t);
        }

        /// <summary>
        /// Проверяет, является ли тип коллекцией.
        /// </summary>
        /// <param name="t">Тип для проверки.</param>
        /// <returns>True, если тип является коллекцией, иначе False.</returns>
        public static bool IsCollection(this Type t)
        {
            if (t.IsArray)
                return true;
            if (t == typeof(string))
                return false;
            var hasGenericType = t.GenericTypeArguments.Length > 0;
            return (typeof(IList).IsAssignableFrom(t) || typeof(ICollection).IsAssignableFrom(t) || typeof(IEnumerable).IsAssignableFrom(t)) && hasGenericType;
        }

        /// <summary>
        /// Проверяет, является ли тип датой/временем.
        /// </summary>
        /// <param name="t">Тип для проверки.</param>
        /// <returns>True, если тип представляет дату/время, иначе False.</returns>
        public static bool IsDate(this Type t)
        {
            return DateTypes.Contains(t);
        }

        /// <summary>
        /// Проверяет, является ли тип делегатом.
        /// </summary>
        /// <param name="type">Тип для проверки.</param>
        /// <returns>True, если тип является делегатом, иначе False.</returns>
        public static bool IsDelegate(this Type type)
        {
            return typeof(MulticastDelegate).IsAssignableFrom(type.BaseType);
        }

        /// <summary>
        /// Проверяет, является ли тип словарём.
        /// </summary>
        /// <param name="type">Тип для проверки.</param>
        /// <returns>True, если тип является словарём, иначе False.</returns>
        public static bool IsDictionary(this Type type)
        {
            return type.IsImplements<IDictionary>() || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>)) || type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));
        }

        /// <summary>
        /// Проверяет, является ли тип числом с плавающей точкой.
        /// </summary>
        /// <param name="t">Тип для проверки.</param>
        /// <returns>True, если тип является числом с плавающей точкой, иначе False.</returns>
        public static bool IsFloat(this Type t)
        {
            return FloatNumberTypes.Contains(t);
        }

        /// <summary>
        /// Проверяет, реализует ли тип заданный интерфейс.
        /// </summary>
        /// <param name="t">Тип для проверки.</param>
        /// <param name="implementType">Интерфейс, который нужно проверить.</param>
        /// <returns>True, если тип реализует указанный интерфейс, иначе False.</returns>
        public static bool IsImplements(this Type t, Type implementType)
        {
            return implementType.IsAssignableFrom(t);
        }

        /// <summary>
        /// Проверяет, реализует ли тип заданный интерфейс (generic).
        /// </summary>
        /// <typeparam name="T">Интерфейс, который нужно проверить.</typeparam>
        /// <param name="t">Тип для проверки.</param>
        /// <returns>True, если тип реализует указанный интерфейс, иначе False.</returns>
        public static bool IsImplements<T>(this Type t)
        {
            return typeof(T).IsAssignableFrom(t);
        }

        /// <summary>
        /// Проверяет, является ли тип nullable.
        /// </summary>
        /// <param name="t">Тип для проверки.</param>
        /// <returns>True, если тип является nullable, иначе False.</returns>
        public static bool IsNullable(this Type t)
        {
            return !t.IsValueType || Nullable.GetUnderlyingType(t) != null || t == typeof(object);
        }

        /// <summary>
        /// Проверяет, является ли тип числовым.
        /// </summary>
        /// <param name="t">Тип для проверки.</param>
        /// <param name="includeFloatTypes">Включать ли типы с плавающей точкой.</param>
        /// <returns>True, если тип является числовым, иначе False.</returns>
        public static bool IsNumeric(this Type t, bool includeFloatTypes = true)
        {
            return includeFloatTypes ? NumberTypes.Contains(t) : IntNumberTypes.Contains(t);
        }

        /// <summary>
        /// Проверяет, является ли тип кортежем (ValueTuple/Tuple).
        /// </summary>
        /// <param name="type">Тип для проверки.</param>
        /// <returns>True, если тип является кортежем, иначе False.</returns>
        public static bool IsTuple(this Type type)
        {
            var baseTypes = type.GetBaseTypes(true, true);
            return baseTypes.Any(x => x.FullName?.StartsWith("System.ValueTuple") == true || x.FullName?.StartsWith("System.Tuple") == true || x.Name.Equals("ITuple"));
        }
    }
}