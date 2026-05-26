//// <copyright file="MemberCache.cs" company="Rudnev Sergey">
//// Copyright (c) Rudnev Sergey. All rights reserved.
//// </copyright>

//namespace System.Reflection
//{
//    using System;
//    using System.Collections.Concurrent;
//    using System.Collections.Generic;
//    using System.Collections.Specialized;
//    using System.ComponentModel;
//    using System.Linq;
//    using StandardExtensions;

//    public sealed class TypeCache
//    {
//        private static readonly ConcurrentDictionary<Type, TypeCache> Cache = new();
//        private Type type;
//        private MethodInfo onPropertyChanged;
//        private MethodInfo onPropertyChanging;
//        private MemberInfo onCollectionChanged;
//        private bool hasOnCollectionChanged;
//        private bool hasOnPropertyChanged;
//        private bool hasOnPropertyChanging;
//        private bool hasPropertyChanged;
//        private MemberCache propertyChanged;
//        private MemberCache[] indexers;
//        private MemberCache[] methods;
//        private MemberCache[] events;
//        private MemberCache[] properties;
//        private MemberCache[] fields;
//        private MemberCache[] columns;
//        private MemberCache[] fks;
//        private MemberCache[] pks;
//        private MemberCache[] publicBasicEnumerableProperties;
//        private MemberCache[] publicEnumerableProperties;
//        private MemberCache[] publicFields;
//        private MemberCache[] publicProperties;
//        private MemberCache[] publicBasicProperties;

//        public IReadOnlyDictionary<string, MemberCache> EventMap { get; }
//        public IReadOnlyDictionary<string, MemberCache> PropertyMap { get; }
//        public IReadOnlyDictionary<string, MemberCache> FieldMap { get; }
//        public IReadOnlyDictionary<string, MemberCache> MethodMap { get; }

//        /// <summary>
//        /// Initializes a new instance of the <see cref="TypeCache"/> class.
//        /// </summary>
//        /// <param name="type">Тип.</param>
//        public TypeCache(Type type)
//        {
//            this.type = type;
//        }

//        /// <summary>
//        /// Создает или получает из кэша экземпляр MemberCache для указанного MemberInfo.
//        /// </summary>
//        /// <param name="type">Информация о члене типа.</param>
//        /// <returns>Кэшированная информация о члене.</returns>
//        /// <exception cref="InvalidOperationException">Выбрасывается, если DeclaringType равен null.</exception>
//        public static TypeCache Get(Type type)
//        {
//            if (type == null)
//            {
//                throw new ArgumentNullException(nameof(type));
//            }

//            return Cache.GetOrAdd(type, x => new TypeCache(x));
//        }

//        /// <summary>
//        /// Возвращает массив свойств по их именам. Сравнение имен производится по правилу <see cref="StringComparison.Ordinal"/>.
//        /// </summary>
//        /// <param name="propertyNames">Список имен искомых свойств.</param>
//        /// <returns>Массив найденных свойств <see cref="PropertyInfo"/>.</returns>
//        public PropertyInfo[] GetProperties(params string[] propertyNames)
//        {
//            return this.GetProperties(StringComparison.Ordinal, propertyNames);
//        }

//        ///// <summary>
//        ///// Получает все навигационные свойства (таблицы) текущего типа.
//        ///// </summary>
//        ///// <returns>Массив навигационных свойств.</returns>
//        //public TypeCache[] GetTables()
//        //{
//        //    if (this.tables != null)
//        //    {
//        //        return this.tables;
//        //    }

//        //    this.tables = [.. this.Properties.Where(x =>
//        //        x.IsProperty &&
//        //        x.IsPublic &&
//        //        ((x.IsCollection &&
//        //        !x.IsBasicCollection) || !x.IsBasic) &&
//        //        !x.HasAnyAttributeOfType("ColumnAttribute", "NotMappedAttribute", "KeyAttribute"))];

//        //    return this.tables;
//        //}

//        /// <summary>
//        /// Получает или задает значение члена по имени для указанного исходного объекта.
//        /// </summary>
//        /// <param name="source">Исходный объект.</param>
//        /// <param name="memberName">Имя члена (свойства, поля).</param>
//        /// <returns>Значение члена.</returns>
//        public object this[object source, string memberName]
//        {
//            get => this[memberName]?.Getter(source);

//            set => this[memberName]?.Setter(source, value);
//        }

//        /// <summary>
//        /// Возвращает кешированную информацию о поле <c>PropertyChanged</c>.
//        /// </summary>
//        /// <remarks>
//        /// Свойство выполняет ленивый поиск члена с именем <c>PropertyChanged</c>
//        /// среди полей типа (<see cref="MemberTypes.Field"/>). Результат поиска
//        /// кешируется, чтобы избежать повторного использования рефлексии.
//        ///
//        /// Если поле найдено, оно сохраняется в <see cref="propertyChanged"/>.
//        /// Флаг <see cref="hasPropertyChanged"/> используется для того, чтобы
//        /// запомнить факт выполнения поиска и не выполнять его повторно.
//        /// </remarks>
//        /// <value>
//        /// Экземпляр <see cref="MemberCache"/>, представляющий поле
//        /// <c>PropertyChanged</c>, если оно найдено; иначе — <see langword="null"/>.
//        /// </value>
//        public MemberCache PropertyChanged
//        {
//            get
//            {
//                if (this.propertyChanged != null || this.hasPropertyChanged == false)
//                {
//                    return this.propertyChanged;
//                }

//                this.propertyChanged = this.FieldMap.TryGetValue("PropertyChanged", out var f) ? f : null;
//                this.hasPropertyChanged = this.propertyChanged != null;
//                return this.propertyChanged;
//            }
//        }

//        /// <summary>
//        /// Получает массив публичных свойств, которые являются коллекциями базовых типов.
//        /// </summary>
//        public MemberCache[] PublicBasicEnumerableProperties
//        {
//            get
//            {
//                if (this.publicBasicEnumerableProperties != null)
//                {
//                    return this.publicBasicEnumerableProperties;
//                }

//                this.publicBasicEnumerableProperties = [.. this.PublicProperties.Where(x => x.IsBasicCollection)];
//                return this.publicBasicEnumerableProperties;
//            }
//        }

//        /// <summary>
//        /// Получает массив публичных свойств базовых типов.
//        /// </summary>
//        public MemberCache[] PublicBasicProperties
//        {
//            get
//            {
//                if (this.publicBasicProperties != null)
//                {
//                    return this.publicBasicProperties;
//                }

//                this.publicBasicProperties = [.. this.PublicProperties.Where(x => x.IsBasic)];
//                return this.publicBasicProperties;
//            }
//        }

//        private static readonly MemberTypes[] DefaultMemberTypes =
//{
//            MemberTypes.Property, MemberTypes.Field,
//        };

//        /// <summary>
//        /// Получает массив публичных свойств, которые являются коллекциями.
//        /// </summary>
//        public MemberCache[] PublicEnumerableProperties
//        {
//            get
//            {
//                if (this.publicEnumerableProperties != null)
//                {
//                    return this.publicEnumerableProperties;
//                }

//                this.publicEnumerableProperties = [.. this.PublicProperties.Where(x => x.IsCollection)];
//                return this.publicEnumerableProperties;
//            }
//        }

//        /// <summary>
//        /// Получает MemberCache для члена с указанным именем и типом.
//        /// </summary>
//        /// <param name="memberName">Имя свойства, поля, метода или события.</param>
//        /// <param name="nameComparison">Сравнение имен.</param>
//        /// <param name="memberTypes">Тип члена среди которых вести поиск в указанном порядке. Если не указано или <see cref="MemberTypes.All"/>, то поиск идет в таком порядке: свойство, поле, метод, событие.</param>
//        /// <returns>MemberCache для члена или null, если член не найден.</returns>
//        /// <exception cref="NotSupportedException">Выбрасывается для неподдерживаемых типов членов.</exception>
//        public MemberCache GetMember(string memberName, StringComparison nameComparison = StringComparison.Ordinal, params MemberTypes[] memberTypes)
//        {
//            try
//            {
//                if (memberTypes == null || memberTypes.Length == 0)
//                {
//                    memberTypes = DefaultMemberTypes;
//                }

//                foreach (var mt in memberTypes)
//                {
//                    switch (mt)
//                    {
//                        case MemberTypes.Property:
//                            if (this.PropertyMap.TryGetValue(memberName, nameComparison, out var propertyCache))
//                            {
//                                return propertyCache;
//                            }

//                            break;

//                        case MemberTypes.Field:
//                            if (this.FieldMap.TryGetValue(memberName, nameComparison, out var fieldCache))
//                            {
//                                return fieldCache;
//                            }

//                            break;

//                        case MemberTypes.Method:
//                            if (this.MethodMap.TryGetValue(memberName, nameComparison, out var methodCache))
//                            {
//                                return methodCache;
//                            }

//                            break;

//                        case MemberTypes.Event:
//                            if (this.EventMap.TryGetValue(memberName, nameComparison, out var eventCache))
//                            {
//                                return eventCache;
//                            }

//                            break;

//                        case MemberTypes.All:
//                            return this.GetMember(memberName, nameComparison, MemberTypes.Property) ??
//                                   this.GetMember(memberName, nameComparison, MemberTypes.Field) ??
//                                   this.GetMember(memberName, nameComparison, MemberTypes.Method) ??
//                                   this.GetMember(memberName, nameComparison, MemberTypes.Event);

//                        case MemberTypes.Constructor:
//                        case MemberTypes.Custom:
//                        case MemberTypes.NestedType:
//                        case MemberTypes.TypeInfo:
//                        default:
//                            throw new NotSupportedException(nameof(mt));
//                    }
//                }

//                return null;
//            }
//            catch (Exception ex)
//            {
//                throw new NotSupportedException($"Ошибка получения члена {memberName} в {this}: {ex}", ex);
//            }
//        }

//        /// <summary>
//        /// Получает информацию о члене указанного типа по имени.
//        /// </summary>
//        /// <typeparam name="TMember">Тип члена (PropertyInfo, FieldInfo, MethodInfo, EventInfo).</typeparam>
//        /// <param name="memberName">Имя свойства, поля, метода или события.</param>
//        /// <param name="nameComparison">Сравнение имен.</param>
//        /// <param name="memberTypes">Тип члена среди которых вести поиск. Если не указано или <see cref="MemberTypes.All"/>, то поиск идет в таком порядке: свойство, поле, метод, событие.</param>
//        /// <returns>Информация о члене или null, если член не найден.</returns>
//        public TMember GetMember<TMember>(string memberName, StringComparison nameComparison = StringComparison.Ordinal, params MemberTypes[] memberTypes)
//            where TMember : MemberInfo
//        {
//            var memberInfo = this.GetMember(memberName, nameComparison, memberTypes)?.MemberInfo;
//            if (memberInfo is not TMember info)
//            {
//                return null;
//            }

//            return info;
//        }

//        /// <summary>
//        /// Преобразует экземпляр объекта в словарь имен и значений свойств.
//        /// </summary>
//        /// <typeparam name="T">Тип объекта.</typeparam>
//        /// <param name="instance">Экземпляр объекта.</param>
//        /// <param name="propertyFilter">Фильтр свойств для включения (если не указаны, включаются все публичные свойства).</param>
//        /// <returns>Словарь имен и значений свойств.</returns>
//        public Dictionary<string, object> ToDictionary<T>(T instance, Func<MemberCache, bool> propertyFilter = null)
//            where T : class
//        {
//            var dic = new Dictionary<string, object>();

//            this.ToDictionary(instance, dic, propertyFilter);

//            return dic;
//        }

//        /// <summary>
//        /// Преобразует экземпляр объекта в словарь имен и значений свойств и добавляет их в указанный словарь.
//        /// </summary>
//        /// <typeparam name="T">Тип объекта.</typeparam>
//        /// <param name="instance">Экземпляр объекта.</param>
//        /// <param name="dictionary">Словарь, в который добавляются пары имя свойства - значение свойства.</param>
//        /// <param name="propertyFilter">Фильтр свойств для включения (если не указаны, включаются все публичные свойства).</param>
//        public void ToDictionary<T>(T instance, Dictionary<string, object> dictionary, Func<MemberCache, bool> propertyFilter = null)
//            where T : class
//        {
//            propertyFilter ??= x => x.IsPublic;

//            var props = this.Properties.Where(propertyFilter).ToArray();

//            foreach (var mi in props)
//            {
//                dictionary[mi.Name] = mi.GetValue(instance);
//            }
//        }

//        /// <summary>
//        /// Получает последовательность кэшей свойств (MemberCache) по заданному пути к свойству.
//        /// </summary>
//        /// <param name="pathToProperty">Строка, представляющая путь к свойству, элементы пути разделены символом <paramref name="nameDelimiter"/>.</param>
//        /// <param name="nameDelimiter">Символ, используемый для разделения имен свойств в пути. По умолчанию '.'.</param>
//        /// <param name="returnIncompletePath">
//        /// Если <c>true</c>, возвращает путь до первого отсутствующего элемента, если <c>false</c>, возвращает пустой массив в случае отсутствия любого элемента.
//        /// </param>
//        /// <returns>Массив объектов <see cref="MemberCache"/> представляющий путь к свойству. Может быть пустым, если путь не найден и <paramref name="returnIncompletePath"/> = <c>false</c>.</returns>
//        /// <exception cref="ArgumentNullException">Выбрасывается, если <paramref name="pathToProperty"/> равен <c>null</c> или пустой строке.</exception>
//        /// <exception cref="ArgumentException">Выбрасывается, если <paramref name="nameDelimiter"/> является символом '\0'.</exception>
//        public MemberCache[] GetPath(string pathToProperty, char nameDelimiter = '.', bool returnIncompletePath = true)
//        {
//            if (string.IsNullOrEmpty(pathToProperty))
//            {
//                throw new ArgumentNullException(nameof(pathToProperty));
//            }

//            if (nameDelimiter == '\0')
//            {
//                throw new ArgumentException(@"Недопустимый разделитель имен", nameof(nameDelimiter));
//            }

//            var pathNames = pathToProperty.Split(nameDelimiter);
//            MemberCache p = null;
//            var path = new List<MemberCache>();
//            foreach (var name in pathNames)
//            {
//                if (p.IsCollection && int.TryParse(name, out _))
//                {
//                    p = ElementType;
//                    continue;
//                }
//                else
//                {
//                    p = GetProperty(x => x.Name == name);
//                }

//                if (p == null)
//                {
//                    return returnIncompletePath ? [.. path] : Array.Empty<MemberCache>();
//                }

//                path.Add(p);
//            }

//            return [.. path];
//        }

//        /// <summary>
//        /// Получает методы с уникальными именами (включая непубличные) текущего типа и базовых типов.
//        /// </summary>
//        public MemberCache[] Methods
//        {
//            get
//            {
//                if (this.methods != null)
//                {
//                    return this.methods;
//                }

//                this.methods = [.. this.type.GetMethods().Select(x => MemberCache.Get(this.type, x))];
//                return this.methods;
//            }
//        }

//        /// <summary>
//        /// Получает массив свойств-индексаторов this[].
//        /// </summary>
//        public MemberCache[] Indexers
//        {
//            get
//            {
//                if (this.indexers != null)
//                {
//                    return this.indexers;
//                }

//                this.indexers = [.. this.type.GetIndexers().Select(x => MemberCache.Get(this.type, x))];
//                return this.indexers;
//            }
//        }

//        /// <summary>
//        /// Возвращает массив свойств, имена которых совпадают с заданным списком имен,
//        /// используя указанный способ сравнения строк.
//        /// </summary>
//        /// <param name="nameComparison">Правила сравнения имен свойств (регистрозависимость, культура и т.д.).</param>
//        /// <param name="propertyNames">Список имен искомых свойств.</param>
//        /// <returns>Массив найденных свойств <see cref="PropertyInfo"/>.</returns>
//        public PropertyInfo[] GetProperties(StringComparison nameComparison, params string[] propertyNames)
//        {
//            if (propertyNames == null || propertyNames.Length == 0)
//            {
//                return this.GetProperties();
//            }

//            var comparer = nameComparison.ToStringComparer();
//            return [.. this.GetProperties().Where(x => propertyNames.Contains(x.Name, comparer))];
//        }

//        /// <summary>
//        /// Получает массив публичных полей.
//        /// </summary>
//        public MemberCache[] PublicFields
//        {
//            get
//            {
//                if (this.publicFields != null)
//                {
//                    return this.publicFields;
//                }

//                this.publicFields = [.. this.Fields.Where(x => x.IsPublic)];
//                return this.publicFields;
//            }
//        }

//        /// <summary>
//        /// Массив публичных свойств, кроме индексов (<see cref="IsPublic"/> == true.
//        /// </summary>
//        public MemberCache[] PublicProperties
//        {
//            get
//            {
//                if (this.publicProperties != null)
//                {
//                    return this.publicProperties;
//                }

//                this.publicProperties = [.. this.Properties.Where(x => x.IsPublic)];
//                return this.publicProperties;
//            }
//        }

//        /// <summary>
//        /// Получает MemberCache для свойства, поля, метода или события в этом порядке. Обертка вокруг <see cref="GetMember(string, StringComparison, MemberTypes[])"/>.
//        /// </summary>
//        /// <param name="memberName">Имя свойства.</param>
//        /// <returns>Кэшированная информация о члене или null, если член не найден.</returns>
//        public MemberCache this[string memberName]
//            => this.GetMember(memberName, StringComparison.Ordinal, MemberTypes.Property) ??
//            this.GetMember(memberName, StringComparison.OrdinalIgnoreCase, MemberTypes.Property) ??
//            this.GetMember(memberName, StringComparison.Ordinal, MemberTypes.Field) ??
//            this.GetMember(memberName, StringComparison.OrdinalIgnoreCase, MemberTypes.Field) ??
//            this.GetMember(memberName, StringComparison.Ordinal, MemberTypes.Method) ??
//            this.GetMember(memberName, StringComparison.OrdinalIgnoreCase, MemberTypes.Method) ??
//            this.GetMember(memberName, StringComparison.Ordinal, MemberTypes.Event) ??
//            this.GetMember(memberName, StringComparison.OrdinalIgnoreCase, MemberTypes.Event);

//        /// <summary>
//        /// Получает MemberCache для свойства, поля, метода или события с указанным именем. Обертка вокруг <see cref="GetMember(string, StringComparison, MemberTypes[])"/>.
//        /// </summary>
//        /// <param name="memberName">Имя свойства.</param>
//        /// <param name="nameComparison">Способ сравнения имен членов (по умолчанию - Ordinal).</param>
//        /// <param name="memberTypes">Тип члена среди которых вести поиск. Если не указано или <see cref="MemberTypes.All"/>, то поиск идет в таком порядке: свойство, поле, метод, событие.</param>
//        /// <returns>Кэшированная информация о члене или null, если член не найден.</returns>
//        public MemberCache this[string memberName, StringComparison nameComparison, params MemberTypes[] memberTypes]
//            => this.GetMember(memberName, nameComparison, memberTypes);

//        /// <summary>
//        /// Получает PropertyInfo для свойства с указанным именем.
//        /// </summary>
//        /// <param name="predicate">Фильтр свойств.</param>
//        /// <returns>PropertyInfo или null, если свойство не найдено.</returns>
//        public MemberCache GetProperty(Func<MemberCache, bool> predicate) => this.Properties.FirstOrDefault(predicate);

//        /// <summary>
//        /// Получает события с уникальными именами (включая непубличные) текущего типа и базовых типов.
//        /// </summary>
//        public MemberCache[] Events
//        {
//            get
//            {
//                if (this.events != null)
//                {
//                    return this.events;
//                }

//                this.events = [.. this.type.GetEvents().Select(x => MemberCache.Get(this.type, x))];
//                return this.events;
//            }
//        }

//        /// <summary>
//        /// Возвращает набор колонок сущности, включая первичные ключи,
//        /// внешние ключи и обычные колонки.
//        /// </summary>
//        /// <param name="getPk">
//        /// Указывает, нужно ли включать первичные ключи в результат.
//        /// </param>
//        /// <param name="getFk">
//        /// Указывает, нужно ли включать внешние ключи в результат.
//        /// </param>
//        /// <returns>
//        /// Массив <see cref="MemberCache"/>, содержащий выбранные колонки:
//        /// первичные ключи, внешние ключи и обычные свойства.
//        /// </returns>
//        /// <remarks>
//        /// Порядок элементов в результирующем массиве:
//        /// сначала первичные ключи (если <paramref name="getPk"/> = true),
//        /// затем внешние ключи (если <paramref name="getFk"/> = true),
//        /// затем остальные колонки.
//        /// </remarks>
//        public MemberCache[] GetColumns(bool getPk = true, bool getFk = true)
//        {
//            var result = new List<MemberCache>();
//            var seen = new HashSet<MemberCache>();

//            void AddRange(IEnumerable<MemberCache> items)
//            {
//                foreach (var item in items)
//                {
//                    if (seen.Add(item))
//                    {
//                        result.Add(item);
//                    }
//                }
//            }

//            if (getPk)
//            {
//                AddRange(this.PrimaryKeys);
//            }

//            if (getFk)
//            {
//                AddRange(this.ForeignKeys);
//            }

//            AddRange(this.ColumnProperties);

//            return [.. result];
//        }

//        /// <summary>
//        /// Получает все свойства (включая приватные) текущего типа и базовых типов, кроме индексаторов.
//        /// </summary>
//        public MemberCache[] Properties
//        {
//            get
//            {
//                if (this.properties != null)
//                {
//                    return this.properties;
//                }

//                this.properties = [.. this.type.GetProperties().Select(x => MemberCache.Get(this.type, x))];
//                return this.properties;
//            }
//        }

//        public string TableName { get; private set; }

//        /// <summary>
//        /// Получает массив свойств, которые являются первичными ключами.
//        /// </summary>
//        public MemberCache[] PrimaryKeys
//        {
//            get
//            {
//                if (this.pks != null)
//                {
//                    return this.pks;
//                }

//                this.pks = [.. this.PublicBasicProperties.Where(x => x.GetAttribute("KeyAttribute") != null)];

//                if (this.pks.Length == 0)
//                {
//                    var p =
//                        this.PublicBasicProperties.FirstOrDefault(x =>
//                            x.Name.Equals("id", StringComparison.OrdinalIgnoreCase)) ??
//                        this.PublicBasicProperties.FirstOrDefault(x =>
//                            x.Name.Equals(this.TableName + "id", StringComparison.OrdinalIgnoreCase)) ??
//                        this.PublicBasicProperties.FirstOrDefault(x =>
//                            x.Name.Equals("uid", StringComparison.OrdinalIgnoreCase)) ??
//                        this.PublicBasicProperties.FirstOrDefault(x =>
//                            x.Name.Equals(this.TableName + "uid", StringComparison.OrdinalIgnoreCase))
//                    ;
//                    if (p != null)
//                    {
//                        this.pks = [p];
//                    }
//                }

//                return this.pks;
//            }
//        }

//        /// <summary>
//        /// Получает массив свойств, которые являются внешними ключами.
//        /// </summary>
//        public MemberCache[] ForeignKeys
//        {
//            get
//            {
//                if (this.fks != null)
//                {
//                    return this.fks;
//                }

//                this.fks = [.. this.PublicBasicProperties.Where(x => x.GetAttribute("ForeignKeyAttribute") != null)];

//                return this.fks;
//            }
//        }

//        /// <summary>
//        /// Получает все поля (включая непубличные) текущего типа и базовых типов.
//        /// </summary>
//        public MemberCache[] Fields
//        {
//            get
//            {
//                if (this.fields != null)
//                {
//                    return this.fields;
//                }

//                this.fields = [.. this.type.GetFields().Select(x => MemberCache.Get(this.type, x))];
//                return this.fields;
//            }
//        }

//        /// <summary>
//        /// Получает массив свойств, которые представляют столбцы в таблице базы данных, кроме ключей <see cref="PrimaryKeys"/> и <see cref="ForeignKeys"/> и свойств помеченных атрибутом NotMappedAttribute.<br/>
//        /// Для получения колонок по условию <see cref="GetColumns(bool, bool)"/>.
//        /// </summary>
//        public MemberCache[] ColumnProperties
//        {
//            get
//            {
//                if (this.columns != null)
//                {
//                    return this.columns;
//                }

//                this.columns = [.. this.PublicBasicProperties.Where(x =>
//                        !x.IsPrimaryKey
//                        && !x.IsForeignKey
//                        && x.IsColumn
//                        && x.GetAttribute("NotMappedAttribute") == null)];

//                if (this.columns.Length == 0)
//                {
//                    this.columns = [.. this.PublicBasicProperties.Where(x => !x.IsPrimaryKey)];
//                }

//                return this.columns;
//            }
//        }

//        //private MemberCache? GetMemberCache(string memberName, MemberTypes memberType)
//        //{
//        //    if (string.IsNullOrWhiteSpace(memberName))
//        //    {
//        //        throw new ArgumentNullException(nameof(memberName));
//        //    }

//        //    if (this.membersMap.TryGetValue(memberName, out var member))
//        //    {
//        //        return member;
//        //    }
//        //    else
//        //    {
//        //        Func<string, MemberInfo> memberGetter = null;
//        //        switch (memberType)
//        //        {
//        //            case MemberTypes.Property:
//        //                memberGetter = (x) => TypeHelper.GetProperty(this.type, x);
//        //                break;

//        //            case MemberTypes.Field:
//        //                memberGetter = (x) => TypeHelper.GetField(this.type, x);
//        //                break;

//        //            case MemberTypes.Event:
//        //                memberGetter = (x) => TypeHelper.GetEvents(this.type).FirstOrDefault(e => e.Name == x);
//        //                break;

//        //            case MemberTypes.Method:
//        //                memberGetter = (x) => TypeHelper.GetMethods(this.type).FirstOrDefault(e => e.Name == x);
//        //                break;

//        //            case MemberTypes.Constructor:
//        //                memberGetter = (x) => TypeHelper.GetConstructors(this.type).FirstOrDefault();
//        //                break;

//        //            default:
//        //                return null;
//        //        }

//        //        var memberInfo = memberGetter(memberName); // this.type.GetProperty(propertyName, AllBindingFlags);
//        //        if (memberInfo != null)
//        //        {
//        //            member = new MemberCache(memberInfo, this);
//        //            this.membersMap[memberName] = member;
//        //        } else
//        //        {
//        //            this.membersMap[memberName] = null;
//        //        }

//        //        return member;
//        //    }
//        //}

//        /// <summary>
//        /// Метод, вызываемый при изменении коллекции (добавление, удаление, обновление элементов).
//        /// </summary>
//        //public MethodInfo OnCollectionChanged
//        //{
//        //    get
//        //    {
//        //        if (this.onCollectionChanged != null || this.hasOnCollectionChanged == false)
//        //        {
//        //            return this.onCollectionChanged;
//        //        }

//        //        this.onCollectionChanged = this.type.GetMethods(typeof(NotifyCollectionChangedEventArgs)).FirstOrDefault();
//        //        this.hasOnCollectionChanged = this.hasOnCollectionChanged != null;
//        //        return this.onCollectionChanged;
//        //    }
//        //}

//        /// <summary>
//        /// Метод, вызываемый после изменения значения свойства.
//        /// </summary>
//        public MethodInfo OnPropertyChanged
//        {
//            get
//            {
//                if (this.onPropertyChanged != null || this.hasOnPropertyChanged == false)
//                {
//                    return this.onPropertyChanged;
//                }

//                this.onPropertyChanged = this.type.GetMethods(typeof(PropertyChangedEventArgs)).FirstOrDefault() ?? this.type.GetMethods(typeof(object), typeof(PropertyChangedEventArgs)).FirstOrDefault();
//                this.hasOnPropertyChanged = this.onPropertyChanged != null;
//                return this.onPropertyChanged;
//            }
//        }

//        /// <summary>
//        /// Метод, вызываемый перед изменением значения свойства.
//        /// </summary>
//        public MethodInfo OnPropertyChanging
//        {
//            get
//            {
//                if (this.onPropertyChanging != null || this.hasOnPropertyChanging == false)
//                {
//                    return this.onPropertyChanged;
//                }

//                this.onPropertyChanging = this.type.GetMethods(typeof(PropertyChangingEventArgs)).FirstOrDefault();
//                this.hasOnPropertyChanging = this.onPropertyChanging != null;
//                return this.onPropertyChanging;
//            }
//        }
//    }
//}