// <copyright file="Obj.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace System
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Data;
    using System.Helpers;
    using System.Linq;
    using System.Reflection;

    /// <summary>
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
        /// <param name="comparison">Сравнение строк.</param>
        /// <param name="memberNames">Массив имен членов, которые необходимо скопировать. Если не указан или пуст, копируются все доступные
        /// свойства исходного объекта.</param>
        /// <remarks>Если оба параметра <paramref name="source" /> и <paramref name="target" />
        /// являются коллекциями (кроме строк), метод копирует значения для каждого соответствующего элемента коллекции.
        /// При необходимости новые элементы добавляются в целевую коллекцию. Копирование выполняется только по
        /// указанным именам членов или по всем свойствам, если имена не заданы.</remarks>
        public static void Copy<TSource, TTarget>(TSource source, TTarget target, StringComparison comparison = StringComparison.Ordinal, params string[] memberNames)
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
                    var get = GetMemberGetter(sourceType, memberName, out _, comparison);
                    if (get == null)
                    {
                        continue;
                    }

                    var set = GetMemberSetter(targetType, memberName, out _, comparison);
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
            if (args == null || args.Length == 0)
            {
                return type.GetConstructor(Type.EmptyTypes);
            }

            var argTypes = args.Select(a => a?.GetType() ?? typeof(object)).ToArray();

            return type.GetConstructors(DefaultBindingFlags)
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
        /// Для максимальной производительности рекомендуется использовать кэширование делегатов доступа к свойствам и полям.<br/>
        /// Если член не найден или объект равен <see langword="null" />, возвращается <see langword="null" />.<br/>
        /// Если указано, возвращаемое значение приводится к заданному типу.
        /// </summary>
        /// <param name="instance">Экземпляр объекта, из которого требуется получить значение.</param>
        /// <param name="memberName">Имя поля или свойства.</param>
        /// <param name="convertToType">Тип, в который требуется преобразовать значение.
        /// Если не задан, возвращается исходное значение.</param>
        /// <param name="comparison">Сравнение строк.</param>
        /// <returns>Значение поля или свойства, приведённое к указанному типу,
        /// либо <see langword="null" />, если объект равен <see langword="null" />
        /// или член не найден.</returns>
        public static object Get(object instance, string memberName, Type convertToType = null, StringComparison comparison = StringComparison.Ordinal)
        {
            if (instance == null)
            {
                return null;
            }

            var getter = GetMemberGetter(instance.GetType(), memberName, out _, comparison);
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
                : TypeHelper.ChangeType(memberValue, convertToType);
        }

        /// <summary>
        /// Получает значение вложенного поля или свойства объекта по указанному пути к члену.
        /// </summary>
        /// <param name="instance">Экземпляр объекта, из которого требуется получить значение.</param>
        /// <param name="pathToMemberName">Последовательность имён членов, описывающая путь
        /// к конечному полю или свойству.</param>
        /// <param name="convertToType">Тип, к которому необходимо привести полученное значение.
        /// Если равен <see langword="null" />, преобразование не выполняется.</param>
        /// <param name="comparison">Сравнение строк.</param>
        /// <returns>Значение конечного члена объекта, приведённое к указанному типу,
        /// либо <see langword="null" />, если объект равен <see langword="null" />,
        /// путь некорректен или один из промежуточных членов имеет значение <see langword="null" />.</returns>
        /// <remarks>Метод поддерживает рекурсивный доступ к вложенным членам.
        /// Если на любом этапе пути значение равно <see langword="null" />,
        /// дальнейший обход прекращается и возвращается <see langword="null" />.</remarks>
        public static object Get(object instance, IEnumerable<string> pathToMemberName, Type convertToType = null, StringComparison comparison = StringComparison.Ordinal)
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

            var getter = GetMemberGetter(instance.GetType(), path[0], out _, comparison);
            var memberValue = getter?.Invoke(instance);

            return memberValue == null
                ? null
                : Get(memberValue, [.. path.Skip(1)], convertToType);
        }

        /// <summary>
        /// Возвращает значение поля или свойства объекта по имени члена, приведённое к указанному типу.
        /// Если указанный член не найден, метод пытается интерпретировать имя как путь к вложенному члену, разделённому точками, слэшами или обратными слэшами, например, "Address.Street.Name" или "Address/Street/Name".<br/>
        /// Для максимальной производительности рекомендуется использовать кэширование делегатов доступа к членам.<br/>
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
        /// Для максимальной производительности рекомендуется использовать кэширование делегатов доступа к членам.<br/>
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
        /// Получает делегат для получения значения поля или свойства, представленного указанным членом типа.<br/>
        /// </summary>
        /// <param name="member">Поле или свойство, для которого нужно получить делегат.</param>
        /// <returns>Делегат для получения значения члена типа.</returns>
        public static Func<object, object> GetMemberGetter(MemberInfo member)
        {
            switch (member)
            {
                case FieldInfo fi:
                    return MemberAccessorHelper.GetFieldGetter(fi);

                case PropertyInfo pi:
                    return MemberAccessorHelper.GetPropertyGetter(pi);
            }

            return null;
        }

        /// <summary>
        /// Получает делегат для получения значения поля или свойства объекта типа по имени члена.<br/>
        /// </summary>
        /// <param name="type">Тип объекта, для которого нужно получить делегат.</param>
        /// <param name="memberName">Имя члена, для которого нужно получить делегат.</param>
        /// <param name="memberType">Тип члена, для которого нужно получить делегат.</param>
        /// <param name="comparison">Сравнение строк.</param>
        /// <returns>Делегат для получения значения члена типа.</returns>
        public static Func<object, object> GetMemberGetter(Type type, string memberName, out Type memberType, StringComparison comparison = StringComparison.Ordinal)
        {
            MemberInfo member = TypeHelper.GetPropertyOrField(type, memberName, comparison);
            switch (member)
            {
                case FieldInfo fi:
                    memberType = fi.FieldType;
                    return MemberAccessorHelper.GetFieldGetter(fi);

                case PropertyInfo pi:
                    memberType = pi.PropertyType;
                    return MemberAccessorHelper.GetPropertyGetter(pi);
            }

            memberType = null;
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
                FieldInfo fi => MemberAccessorHelper.GetFieldSetter(fi),
                PropertyInfo pi => MemberAccessorHelper.GetPropertySetter(pi),
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
        /// <param name="comparison">Сравнение строк.</param>
        /// <returns>Делегат Action{object, object}, который устанавливает значение указанного члена для объекта.
        /// Возвращает <see langword="null" />, если член с заданным именем не найден или
        /// не поддерживает установку значения.</returns>
        /// <remarks>Если указанный член является только для чтения или не существует, возвращаемое
        /// значение будет <see langword="null" />. Делегат использует отражение и может иметь меньшую производительность
        /// по сравнению с прямым доступом. Не рекомендуется использовать для часто вызываемых операций.</remarks>
        public static Action<object, object> GetMemberSetter(Type type, string memberName, out Type memberType, StringComparison comparison = StringComparison.Ordinal)
        {
            MemberInfo member = TypeHelper.GetPropertyOrField(type, memberName, comparison);
            switch (member)
            {
                case FieldInfo fi:
                    memberType = fi.FieldType;
                    return MemberAccessorHelper.GetFieldSetter(fi);

                case PropertyInfo pi:
                    memberType = pi.PropertyType;
                    return MemberAccessorHelper.GetPropertySetter(pi);
            }

            memberType = null;
            return null;
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
        public static Action<object, object> GetMemberSetter<T>(string memberName) => GetMemberSetter(typeof(T), memberName, out _);

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
                values.Add(GetMemberGetter(sourceType, propName, out _)?.Invoke(source));
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
        /// Для преобразования используется вспомогательный метод <c>TypeHelper.ChangeType&lt;T&gt;</c>.
        /// Если преобразование невозможно, может возникнуть исключение.
        /// </remarks>
        public static TValue[] GetValues<TObject, TValue>(TObject source, params string[] memberNames)
            where TObject : class
            => [.. GetValues(source, memberNames).Select(x => TypeHelper.ChangeType<TValue>(x))];

        /// <summary>
        /// Проверяет, является ли переданное значение "null-эквивалентом", то есть одним из следующих: <see cref="TypeHelper.NullValues"/>.
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
                return string.Empty;
            }

            var ctor = FindConstructor(type, args) ??
                       throw new InvalidOperationException($"No constructor found for type {type}");
            var factory = MemberAccessorHelper.GetConstructorInvoker(ctor);

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
        /// Для максимальной производительности рекомендуется использовать делегаты сеттеров, так как этот метод выполняет поиск члена и преобразование типов при каждом вызове.
        /// </summary>
        /// <param name="instance">Экземпляр объекта, в котором требуется установить значение.</param>
        /// <param name="memberName">Имя поля или свойства.</param>
        /// <param name="value">Значение, которое необходимо установить.</param>
        /// <param name="comparison">Способ сравнения имен свойств \ полей.</param>
        /// <returns><see langword="true" />, если значение успешно установлено;
        /// <see langword="false" />, если объект равен <see langword="null" />,
        /// член не найден или недоступен для записи.</returns>
        public static bool Set(object instance, string memberName, object value, StringComparison comparison = StringComparison.Ordinal)
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

            var member = TypeHelper.GetPropertyOrField(instance.GetType(), memberName, comparison);
            var setter = MemberAccessorHelper.GetSetter(member);
            if (setter == null)
            {
                var path = memberName.Split('.', '/', '\\');
                if (path.Length > 1)
                {
                    return Set(instance, path, value);
                }

                return false;
            }

            setter(instance, TypeHelper.ChangeType(value, TypeHelper.GetMemberType(member)));
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

            var getter = MemberAccessorHelper.GetPropertyGetter(TypeHelper.GetProperty(instance.GetType(), path[0]));
            if (getter == null)
            {
                return false;
            }

            var subMemberInstance = Get(instance, path[0]);
            if (subMemberInstance == null)
            {
                var subMember = TypeHelper.GetPropertyOrField(instance.GetType(), path[0]);
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
                return TypeHelper.ChangeType<T>(value, formatProvider);
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
                result = TypeHelper.ChangeType<T>(value, formatProvider);
                return true;
            }
            catch
            {
                result = default;
                return false;
            }
        }
    }
}