// ***********************************************************************
// Assembly         : RuntimeStuff
// Author           : RS
// Created          : 01-06-2026
//
// Last Modified By : RS
// Last Modified On : 01-07-2026
// ***********************************************************************
// <copyright file="BindingListView.cs" company="Rudnev Sergey">
//     Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************
namespace RuntimeStuff
{
    using RuntimeStuff.Extensions;
    using RuntimeStuff.Helpers;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;

    /// <summary>
    /// Определяет тип индекса, используемый для представления исходных или отфильтрованных и отсортированных данных.
    /// </summary>
    /// <remarks>Используется для указания, относится ли индекс к исходной коллекции данных или к коллекции,
    /// полученной после применения фильтрации и сортировки. Это позволяет корректно обрабатывать операции поиска и
    /// отображения в зависимости от типа индекса.</remarks>
    public enum IndexType
    {
        /// <summary>
        /// Gets or sets the source from which the data is obtained.
        /// </summary>
        Source,

        /// <summary>
        /// Gets the collection of items after applying the current filters and sort order.
        /// </summary>
        FilteredSorted,
    }

    /// <summary>
    /// Представляет коллекцию, поддерживающую фильтрацию, сортировку, поиск и уведомления об изменениях,
    /// предназначенную для использования в привязке данных и отображении списков объектов.
    /// </summary>
    /// <typeparam name="T">Тип элементов, содержащихся в списке. Должен быть ссылочным типом.</typeparam>
    /// <remarks>Класс реализует интерфейсы INotifyPropertyChanged, IBindingListView и
    /// INotifyCollectionChanged, что позволяет использовать его в сценариях с динамическим обновлением данных,
    /// например, в UI-приложениях. Поддерживает многопоточный доступ через объект синхронизации <see cref="SyncRoot" />.
    /// Изменения фильтрации и сортировки автоматически применяются к отображаемому списку. Для корректной работы
    /// рекомендуется использовать с типами, обладающими публичными свойствами, по которым возможна сортировка и
    /// фильтрация.</remarks>
    [DebuggerDisplay("Count = {Count} TotalCount = {TotalCount}")]
    public class BindingListView<T> : PropertyChangeNotifier, IBindingListView, INotifyCollectionChanged, IEnumerable<T>
        where T : class
    {
        /// <summary>
        /// The sort separators.
        /// </summary>
        private readonly char[] sortSeparators = { ',', ';' };

        /// <summary>
        /// The source list.
        /// </summary>
        private readonly List<T> sourceList;

        /// <summary>
        /// The allow edit.
        /// </summary>
        private bool allowEdit = true;

        /// <summary>
        /// The allow new.
        /// </summary>
        private bool allowNew = true;

        /// <summary>
        /// The allow remove.
        /// </summary>
        private bool allowRemove = true;

        /// <summary>
        /// The filter.
        /// </summary>
        private string filter;

        /// <summary>
        /// The is sorted.
        /// </summary>
        private bool isSorted;

        /// <summary>
        /// The node map.
        /// </summary>
        private Dictionary<T, BindingListViewRow> nodeMap;

        /// <summary>
        /// The sort by.
        /// </summary>
        private string sortBy;

        /// <summary>
        /// The source filtered and sorted list.
        /// </summary>
        private List<T> sourceFilteredAndSortedList;

        /// <summary>
        /// The suspend list changed events.
        /// </summary>
        private bool suspendListChangedEvents;

        /// <summary>
        /// Initializes a new instance of the <see cref="BindingListView{T}" /> class.
        /// </summary>
        public BindingListView()
        {
            this.sourceList = new List<T>();
            this.sourceFilteredAndSortedList = new List<T>();
            this.nodeMap = new Dictionary<T, BindingListViewRow>();
            this.Properties = Obj.GetProperties<T>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BindingListView{T}" /> class that contains elements copied from the specified.
        /// collection.
        /// </summary>
        /// <param name="items">The collection of items to copy into the list. Cannot be null.</param>
        /// <exception cref="ArgumentNullException">nameof(items).</exception>
        public BindingListView(IEnumerable<T> items)
            : this()
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            this.sourceList = items.ToList();
            this.sourceFilteredAndSortedList = this.sourceList.ToList();
            this.RebuildNodeMap();
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="BindingListView{T}" /> class.
        /// </summary>
        ~BindingListView()
        {
            // Очищаем подписки при уничтожении объекта
            foreach (var item in this.sourceList)
            {
                this.SubscribeOnPropertyChanged(item, false);
            }
        }

        /// <summary>
        /// Событие, возникающее при изменении коллекции.
        /// </summary>
        public event NotifyCollectionChangedEventHandler CollectionChanged;

        /// <summary>
        /// Событие, возникающее при изменении списка.
        /// </summary>
        public event ListChangedEventHandler ListChanged;

        /// <summary>
        /// Gets or sets a value indicating whether разрешено ли редактирование элементов.
        /// </summary>
        /// <value>The allow edit.</value>
        public bool AllowEdit
        {
            get => this.allowEdit;
            set => this.SetProperty(ref this.allowEdit, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether разрешено ли добавление новых элементов.
        /// </summary>
        /// <value>The allow new.</value>
        public bool AllowNew
        {
            get => this.allowNew;
            set => this.SetProperty(ref this.allowNew, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether разрешено ли удаление элементов.
        /// </summary>
        /// <value>The allow remove.</value>
        public bool AllowRemove
        {
            get => this.allowRemove;
            set => this.SetProperty(ref this.allowRemove, value);
        }

        /// <summary>
        /// Gets количество элементов в отображаемом списке.
        /// </summary>
        /// <value>The count.</value>
        public int Count
        {
            get
            {
                lock (this.SyncRoot)
                {
                    return this.sourceFilteredAndSortedList.Count;
                }
            }
        }

        /// <summary>
        /// Gets or sets строка фильтрации для отображаемого списка. Формат: [ИмяСвойства] Оператор Значение. Операторы: ==, &lt; &gt;, &gt;
        /// , &lt;, &gt;=, &lt;=, LIKE, IN.
        /// Строковые значения должны быть в одинарных кавычках. Несколько условий можно объединять логическими операторами
        /// AND, OR.
        /// </summary>
        /// <value>The filter.</value>
        public string Filter
        {
            get => this.filter;
            set
            {
                if (!this.SetProperty(ref this.filter, value))
                {
                    return;
                }

                this.ApplyFilterAndSort();
                this.RebuildNodes();
            }
        }

        /// <summary>
        /// Gets a value indicating whether отфильтрован ли список.
        /// </summary>
        /// <value>The is filtered.</value>
        public bool IsFiltered => this.Count != this.TotalCount;

        /// <summary>
        /// Gets a value indicating whether является ли список фиксированного размера.
        /// </summary>
        /// <value>The size of the is fixed.</value>
        public bool IsFixedSize => false;

        /// <summary>
        /// Gets a value indicating whether является ли список только для чтения.
        /// </summary>
        /// <value>The is read only.</value>
        public bool IsReadOnly => false;

        /// <summary>
        /// Gets or sets a value indicating whether отсортирован ли список.
        /// </summary>
        /// <value>The is sorted.</value>
        public bool IsSorted
        {
            get => this.isSorted;
            set => this.SetProperty(ref this.isSorted, value);
        }

        /// <summary>
        /// Gets a value indicating whether является ли доступ к списку синхронизированным.
        /// </summary>
        /// <value>The is synchronized.</value>
        public bool IsSynchronized => false;

        /// <summary>
        /// Gets or sets фабрика для создания новых элементов при вызове AddNew().
        /// </summary>
        /// <value>The new item factory.</value>
        public Func<T> NewItemFactory { get; set; }

        /// <summary>
        /// Gets the properties.
        /// </summary>
        /// <value>The properties.</value>
        public PropertyInfo[] Properties { get; }

        /// <summary>
        /// Gets or sets строка с перечислением имён свойств, по которым нужно выполнить сортировку.
        /// Можно перечислять имена через запятую или точку с запятой.
        /// Через пробел после имени свойства можно указать направление сортировки: ASC (по возрастанию) или DESC (по
        /// убыванию).
        /// При изменении свойства автоматически применяется сортировка.
        /// </summary>
        /// <value>The sort by.</value>
        public string SortBy
        {
            get => this.sortBy;
            set => this.SetProperty(ref this.sortBy, value, this.OnSortByChanged);
        }

        /// <summary>
        /// Gets описания сортировки для списка.
        /// </summary>
        /// <value>The sort descriptions.</value>
        public ListSortDescriptionCollection SortDescriptions { get; private set; }

        /// <summary>
        /// Gets направление сортировки.
        /// </summary>
        /// <value>The sort direction.</value>
        public ListSortDirection SortDirection { get; private set; }

        /// <summary>
        /// Gets свойство, по которому выполняется сортировка.
        /// </summary>
        /// <value>The sort property.</value>
        public PropertyDescriptor SortProperty { get; private set; }

        /// <summary>
        /// Gets a value indicating whether поддерживает ли расширенную сортировку.
        /// </summary>
        /// <value>The supports advanced sorting.</value>
        public bool SupportsAdvancedSorting => true;

        /// <summary>
        /// Gets a value indicating whether поддерживает ли уведомления об изменениях.
        /// </summary>
        /// <value>The supports change notification.</value>
        public bool SupportsChangeNotification => true;

        /// <summary>
        /// Gets a value indicating whether поддерживает ли фильтрацию.
        /// </summary>
        /// <value>The supports filtering.</value>
        public bool SupportsFiltering => true;

        /// <summary>
        /// Gets a value indicating whether поддерживает ли поиск.
        /// </summary>
        /// <value>The supports searching.</value>
        public bool SupportsSearching => true;

        /// <summary>
        /// Gets a value indicating whether поддерживает ли сортировку.
        /// </summary>
        /// <value>The supports sorting.</value>
        public bool SupportsSorting => true;

        /// <summary>
        /// Gets or sets a value indicating whether прекращает или возобновляет генерацию событий изменения списка.
        /// </summary>
        /// <value>The suspend list changed events.</value>
        public bool SuspendListChangedEvents
        {
            get => this.suspendListChangedEvents;
            set => this.SetProperty(ref this.suspendListChangedEvents, value, this.RaiseResetEvents);
        }

        /// <summary>
        /// Gets объект синхронизации для многопоточного доступа.
        /// </summary>
        /// <value>The synchronize root.</value>
        public object SyncRoot { get; } = new object();

        /// <summary>
        /// Gets возвращает общее количество элементов в исходном списке.
        /// </summary>
        /// <value>The total count.</value>
        public int TotalCount => this.sourceList.Count;

        /// <summary>
        /// Получает или задает элемент по индексу из отфильтрованного и отсортированного списка.
        /// </summary>
        /// <param name="index">Индекс элемента.</param>
        /// <returns>Элемент по указанному индексу.</returns>
        /// <exception cref="ArgumentException">$"Item must be of type {typeof(T).Name}.</exception>
        public object this[int index]
        {
            get
            {
                lock (this.SyncRoot)
                {
                    return this.sourceFilteredAndSortedList[index];
                }
            }

            set
            {
                if (!(value is T item))
                {
                    throw new ArgumentException($"Item must be of type {typeof(T).Name}");
                }

                lock (this.SyncRoot)
                {
                    this.SubscribeOnPropertyChanged(item, true);
                    this.sourceFilteredAndSortedList[index] = item;
                    var bli = new BindingListViewRow(this, item, index);
                    this.nodeMap[item] = bli;
                    this.ApplyFilterAndSort();
                }

                if (!this.SuspendListChangedEvents)
                {
                    this.ListChanged?.Invoke(this, new ListChangedEventArgs(ListChangedType.ItemChanged, index));
                    this.CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace));
                }
            }
        }

        /// <summary>
        /// Добавляет элемент в список. Для оптимизации используй <see cref="AddRange" /> или <see cref="SuspendListChangedEvents" />.
        /// </summary>
        /// <param name="value">Добавляемый элемент.</param>
        /// <returns>Индекс добавленного элемента.</returns>
        /// <exception cref="ArgumentException">$"Item must be of type {typeof(T).Name}.</exception>
        /// <exception cref="ArgumentNullException">nameof(value).</exception>
        public int Add(object value)
        {
            if (!(value is T item))
            {
                throw new ArgumentException($"Item must be of type {typeof(T).Name}");
            }

            this.SubscribeOnPropertyChanged(item, true);

            if (item == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            lock (this.SyncRoot)
            {
                this.sourceList.Add(item);
                this.sourceFilteredAndSortedList.Add(item);
                var bli = new BindingListViewRow(this, item, this.sourceList.Count - 1);
                this.nodeMap[item] = bli;
            }

            if (!this.SuspendListChangedEvents)
            {
                this.ApplyFilterAndSort();
                this.ListChanged?.Invoke(this, new ListChangedEventArgs(ListChangedType.ItemAdded, this.sourceList.Count - 1));
                this.CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add));
            }

            return this.sourceList.Count - 1;
        }

        /// <summary>
        /// Adds the specified property to the list of properties to be used as search criteria.
        /// </summary>
        /// <param name="property">The property descriptor that specifies the property to add as an index. Cannot be null.</param>
        /// <remarks>This method is intended for use with data-binding scenarios that support searching or
        /// sorting based on specific properties. Not all implementations are required to support indexing; calling this
        /// method may have no effect if indexing is not supported.</remarks>
        void IBindingList.AddIndex(PropertyDescriptor property)
        {
        }

        /// <summary>
        /// Добавляет новый элемент, создавая его через конструктор по умолчанию.
        /// </summary>
        /// <returns>Добавленный элемент.</returns>
        public object AddNew()
        {
            var item = this.NewItemFactory?.Invoke() ?? Activator.CreateInstance<T>();
            this.Add(item);
            return item;
        }

        /// <summary>
        /// Добавляет диапазон элементов в список.
        /// </summary>
        /// <param name="items">Коллекция добавляемых элементов.</param>
        /// <exception cref="ArgumentNullException">nameof(items).</exception>
        public void AddRange(IEnumerable<T> items)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            lock (this.SyncRoot)
            {
                var previousSuspendState = this.SuspendListChangedEvents;
                this.suspendListChangedEvents = true;
                foreach (var item in items)
                {
                    this.Add(item);
                }

                this.suspendListChangedEvents = previousSuspendState;
            }

            this.ApplyFilterAndSort();

            if (!this.SuspendListChangedEvents)
            {
                this.ListChanged?.Invoke(this, new ListChangedEventArgs(ListChangedType.ItemAdded, this.sourceList.Count - 1));
                this.CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add));
            }
        }

        /// <summary>
        /// Применяет фильтр к списку.
        /// </summary>
        public void ApplyFilterAndSort()
        {
            lock (this.SyncRoot)
            {
                var sourceCount = this.sourceList.Count;

                try
                {
                    // Вместо создания нового списка каждый раз, очищаем и перезаполняем существующий
                    var filteredList = this.sourceList.Filter(this.filter).ToList();

                    // Удаляем невидимые элементы
                    filteredList.RemoveAll(x => !this.nodeMap[x].Visible);

                    // Применяем сортировку
                    if (this.SortProperty != null)
                    {
                        filteredList = filteredList.Sort(this.SortDirection, this.SortProperty.Name).ToList();
                    }
                    else if (this.SortDescriptions != null && this.SortDescriptions.Count > 0)
                    {
                        filteredList = filteredList.Sort(this.SortDescriptions).ToList();
                    }

                    // Обновляем существующий список вместо создания нового
                    this.sourceFilteredAndSortedList.Clear();
                    this.sourceFilteredAndSortedList.AddRange(filteredList);
                }
                catch (FormatException fe)
                {
                    Debug.WriteLine($"Filter format exception: {fe.Message}");
                    var filteredList = this.sourceList.FilterByText(this.filter).ToList();
                    filteredList.RemoveAll(x => !this.nodeMap[x].Visible);

                    this.sourceFilteredAndSortedList.Clear();
                    this.sourceFilteredAndSortedList.AddRange(filteredList);
                }

                var filteredCount = this.sourceFilteredAndSortedList.Count;
                var prevIsSorted = this.IsSorted;
                this.IsSorted = this.SortProperty != null || this.SortDescriptions != null || !string.IsNullOrWhiteSpace(this.sortBy);

                if (sourceCount != filteredCount || prevIsSorted != this.IsSorted)
                {
                    this.RaiseResetEvents();
                }
            }
        }

        /// <summary>
        /// Applies sorting to the collection based on the specified property and sort direction.
        /// </summary>
        /// <param name="property">The property descriptor that specifies the property to sort by. Cannot be null.</param>
        /// <param name="direction">The direction in which to sort the collection. Specify ascending or descending.</param>
        /// <remarks>Calling this method updates the current sort criteria and re-applies sorting to the
        /// collection. The operation is thread-safe.</remarks>
        public void ApplySort(PropertyDescriptor property, ListSortDirection direction)
        {
            this.SortProperty = property;
            this.SortDirection = direction;

            lock (this.SyncRoot)
            {
                this.ApplyFilterAndSort();
            }
        }

        /// <summary>
        /// Applies the specified sort descriptions to the data source.
        /// </summary>
        /// <param name="sorts">A collection of sort descriptions that defines the sort order to apply. Cannot be null.</param>
        /// <remarks>Calling this method updates the current sort order based on the provided sort
        /// descriptions. Any existing sort order will be replaced.</remarks>
        public void ApplySort(ListSortDescriptionCollection sorts)
        {
            this.SortDescriptions = sorts;
            this.ApplyFilterAndSort();
        }

        /// <summary>
        /// Releases unused resources and optimizes memory usage by trimming internal collections and removing obsolete
        /// items.
        /// </summary>
        /// <remarks>Call this method to manually free memory held by large internal collections and to
        /// remove items from internal mappings that are no longer present in the source list. This method also forces a
        /// garbage collection and waits for finalizers to complete, which may impact application performance. Use with
        /// caution in performance-sensitive scenarios.</remarks>
        public void Cleanup()
        {
            // Принудительно очищаем большие коллекции
            lock (this.SyncRoot)
            {
                this.sourceFilteredAndSortedList.TrimExcess();
                this.sourceList.TrimExcess();

                // Очищаем словарь
                var itemsToRemove = this.nodeMap.Where(kvp => !this.sourceList.Contains(kvp.Key))
                    .Select(kvp => kvp.Key)
                    .ToList();
                foreach (var key in itemsToRemove)
                {
                    this.nodeMap.Remove(key);
                }
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        /// <summary>
        /// Очищает список.
        /// </summary>
        public void Clear()
        {
            lock (this.SyncRoot)
            {
                foreach (var item in this.sourceList)
                {
                    this.SubscribeOnPropertyChanged(item, false);
                }

                this.sourceList.Clear();
                this.sourceFilteredAndSortedList.Clear();
                this.nodeMap.Clear();
            }

            this.RaiseResetEvents();
        }

        /// <summary>
        /// Removes all items from the collection and resets all filters, sorting, and related state to their default
        /// values.
        /// </summary>
        /// <remarks>After calling this method, the collection will be empty and any applied filters or
        /// sorting will be cleared. Any event subscriptions related to the items in the collection are also removed.
        /// This method is thread-safe.</remarks>
        public void ClearAll()
        {
            lock (this.SyncRoot)
            {
                // Отписываемся от всех событий
                foreach (var item in this.sourceList)
                {
                    this.SubscribeOnPropertyChanged(item, false);
                }

                this.sourceList.Clear();
                this.sourceFilteredAndSortedList.Clear();
                this.nodeMap.Clear();

                // Очищаем кэши
                this.filter = null;
                this.sortBy = null;
                this.SortProperty = null;
                this.SortDescriptions = null;
                this.IsSorted = false;
            }

            this.RaiseResetEvents();
        }

        /// <summary>
        /// Проверяет, содержится ли элемент в списке.
        /// </summary>
        /// <param name="value">Проверяемый элемент.</param>
        /// <returns>True, если элемент найден; иначе false.</returns>
        public bool Contains(object value) => value is T item && this.sourceFilteredAndSortedList.Contains(item);

        /// <summary>
        /// Копирует элементы списка в массив, начиная с указанного индекса.
        /// </summary>
        /// <param name="array">Массив назначения.</param>
        /// <param name="index">Начальный индекс копирования.</param>
        public void CopyTo(Array array, int index)
        {
            lock (this.SyncRoot)
            {
                ((ICollection)this.sourceFilteredAndSortedList).CopyTo(array, index);
            }
        }

        /// <summary>
        /// Находит индекс элемента по значению свойства.
        /// </summary>
        /// <param name="property">Свойство для поиска.</param>
        /// <param name="key">Значение для поиска.</param>
        /// <returns>Индекс найденного элемента или -1, если не найден.</returns>
        int IBindingList.Find(PropertyDescriptor property, object key)
        {
            lock (this.SyncRoot)
            {
                for (var i = 0; i < this.sourceFilteredAndSortedList.Count; i++)
                {
                    var value = Obj.Get(this.sourceFilteredAndSortedList[i], property.Name);
                    if (Equals(value, key))
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        /// <summary>
        /// Returns an enumerator that iterates through the filtered and sorted collection.
        /// </summary>
        /// <returns>An enumerator that can be used to iterate through the collection.</returns>
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => this.sourceFilteredAndSortedList.GetEnumerator();

        /// <summary>
        /// Возвращает перечислитель для списка.
        /// </summary>
        /// <returns>Перечислитель элементов.</returns>
        public IEnumerator GetEnumerator()
        {
            lock (this.SyncRoot)
            {
                return this.sourceFilteredAndSortedList.GetEnumerator();
            }
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection using the specified index type.
        /// </summary>
        /// <param name="indexType">The type of index to use when enumerating the collection. Use IndexType.FilteredSorted to enumerate the
        /// filtered and sorted view; otherwise, the original list is enumerated.</param>
        /// <returns>An enumerator for the collection, based on the specified index type.</returns>
        /// <remarks>Enumeration is thread-safe with respect to this collection. The returned enumerator
        /// reflects the state of the collection at the time GetEnumerator is called.</remarks>
        public IEnumerator GetEnumerator(IndexType indexType)
        {
            lock (this.SyncRoot)
            {
                return indexType == IndexType.FilteredSorted ? this.sourceFilteredAndSortedList.GetEnumerator() : this.sourceList.GetEnumerator();
            }
        }

        /// <summary>
        /// Retrieves the values of a specified property for all entities in the collection.
        /// </summary>
        /// <typeparam name="TValue">The type of the property whose values are to be retrieved.</typeparam>
        /// <param name="propertySelector">An expression that specifies the property to retrieve values from. Must refer to a property of the entity
        /// type.</param>
        /// <param name="indexType">Specifies the type of index to use when retrieving property values. The default is IndexType.FilteredSorted.</param>
        /// <param name="distinct">true to return only distinct property values; otherwise, false.</param>
        /// <returns>An array containing the values of the specified property for all entities. The array may be empty if no
        /// entities are present.</returns>
        public TValue[] GetPropertyValues<TValue>(Expression<Func<T, TValue>> propertySelector, IndexType indexType = IndexType.FilteredSorted, bool distinct = true) => this.GetPropertyValues<TValue>(propertySelector.GetPropertyName(), indexType, distinct);

        /// <summary>
        /// Retrieves the values of the specified property from all items in the collection.
        /// </summary>
        /// <param name="propertyName">The name of the property whose values are to be retrieved. Cannot be null or empty.</param>
        /// <param name="indexType">Specifies the type of index to use when retrieving property values. The default is IndexType.FilteredSorted.</param>
        /// <param name="distinct">true to return only distinct property values; otherwise, false.</param>
        /// <returns>An array of objects containing the values of the specified property. The array is empty if no matching
        /// values are found.</returns>
        public object[] GetPropertyValues(string propertyName, IndexType indexType = IndexType.FilteredSorted, bool distinct = true) => this.GetPropertyValues<object>(propertyName, indexType, distinct);

        /// <summary>
        /// Retrieves the values of the specified property from all items in the collection, with optional filtering,
        /// sorting, and duplicate removal.
        /// </summary>
        /// <typeparam name="TValue">The type of the property values to retrieve.</typeparam>
        /// <param name="propertyName">The name of the property whose values are to be retrieved from each item.</param>
        /// <param name="indexType">Specifies whether to use the filtered and sorted view or the original collection when retrieving property
        /// values. The default is IndexType.FilteredSorted.</param>
        /// <param name="distinct">true to return only distinct property values; false to include duplicates. The default is true.</param>
        /// <returns>An array containing the values of the specified property from the collection. If distinct is true, the array
        /// contains only unique values; otherwise, duplicates may be present.</returns>
        /// <remarks>The order of returned values depends on the selected index type. If indexType is
        /// IndexType.FilteredSorted, the values reflect the current filtered and sorted view; otherwise, they reflect
        /// the original collection order. The method is thread-safe.</remarks>
        public TValue[] GetPropertyValues<TValue>(string propertyName, IndexType indexType = IndexType.FilteredSorted, bool distinct = true)
        {
            lock (this.SyncRoot)
            {
                var list = indexType == IndexType.FilteredSorted
                    ? this.sourceFilteredAndSortedList
                    : this.sourceList;

                if (!distinct)
                {
                    var result = new TValue[list.Count];
                    var i = 0;
                    foreach (var item in list)
                    {
                        result[i++] = Obj.Get<TValue>(item, propertyName);
                    }

                    return result;
                }

                var set = new HashSet<TValue>();

                foreach (var item in list)
                {
                    set.Add(Obj.Get<TValue>(item, propertyName));
                }

                return set.ToArray();
            }
        }

        /// <summary>
        /// Retrieves the row at the specified index from the source or filtered and sorted list.
        /// </summary>
        /// <param name="index">The zero-based index of the row to retrieve. Must be within the bounds of the selected list.</param>
        /// <param name="indexType">Specifies whether the index refers to the source list or the filtered and sorted list. The default is
        /// IndexType.FilteredSorted.</param>
        /// <returns>A BindingListViewRow representing the row at the specified index.</returns>
        /// <remarks>Use IndexType.Source to retrieve a row based on its position in the original source
        /// list, or IndexType.FilteredSorted to retrieve a row based on its position in the filtered and sorted view.
        /// The returned row's VisibleIndex and SourceListIndex properties are updated to reflect its current positions
        /// in the filtered/sorted and source lists, respectively.</remarks>
        public BindingListViewRow GetRow(int index, IndexType indexType = IndexType.FilteredSorted)
        {
            var row = this.nodeMap[indexType == IndexType.Source ? this.sourceList[index] : this.sourceFilteredAndSortedList[index]];
            row.VisibleIndex = this.sourceFilteredAndSortedList.IndexOf(row.Item);
            row.SourceListIndex = this.sourceList.IndexOf(row.Item);
            return row;
        }

        /// <summary>
        /// Retrieves the values of all properties for the row at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the row to retrieve values from. The interpretation of this index depends on the
        /// specified index type.</param>
        /// <param name="indexType">Specifies whether the index refers to the filtered and sorted view or the original source list. The default
        /// is FilteredSorted.</param>
        /// <returns>An array of objects containing the values of each property for the specified row. The order of values
        /// corresponds to the order of properties in the Properties collection.</returns>
        /// <remarks>If the index is out of range for the selected list, an exception may be thrown. The
        /// returned array contains property values in the same order as defined by the Properties collection.</remarks>
        public object[] GetRowValues(int index, IndexType indexType = IndexType.FilteredSorted)
        {
            var values = new object[this.Properties.Length];
            var i = 0;
            var rowItem = indexType == IndexType.FilteredSorted ? this.sourceFilteredAndSortedList[index] : this.sourceList[index];
            foreach (var property in this.Properties)
            {
                values[i++] = Obj.Get(rowItem, property.Name);
            }

            return values;
        }

        /// <summary>
        /// Возвращает индекс указанного элемента.
        /// </summary>
        /// <param name="value">Элемент для поиска.</param>
        /// <returns>Индекс элемента или -1, если не найден.</returns>
        public int IndexOf(object value) => value is T item ? this.sourceFilteredAndSortedList.IndexOf(item) : -1;

        /// <summary>
        /// Вставляет элемент в список по указанному индексу.
        /// </summary>
        /// <param name="index">Индекс вставки.</param>
        /// <param name="value">Вставляемый элемент.</param>
        /// <exception cref="ArgumentException">$"Item must be of type {typeof(T).Name}.</exception>
        /// <exception cref="ArgumentNullException">nameof(value).</exception>
        public void Insert(int index, object value)
        {
            if (!(value is T item))
            {
                throw new ArgumentException($"Item must be of type {typeof(T).Name}");
            }

            if (item == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            lock (this.SyncRoot)
            {
                this.sourceList.Insert(index, item);
                var bli = new BindingListViewRow(this, item, index);
                this.nodeMap[item] = bli;
                this.ApplyFilterAndSort();
            }

            if (!this.SuspendListChangedEvents)
            {
                this.ListChanged?.Invoke(this, new ListChangedEventArgs(ListChangedType.ItemAdded, index));
                this.CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
            }
        }

        /// <summary>
        /// Удаляет элемент из списка.
        /// </summary>
        /// <param name="value">Удаляемый элемент.</param>
        public void Remove(object value)
        {
            if (!(value is T item))
            {
                return;
            }

            lock (this.SyncRoot)
            {
                this.SubscribeOnPropertyChanged(item, false);
                if (this.sourceList.Remove(item))
                {
                    this.nodeMap.Remove(item);
                    var index = this.sourceFilteredAndSortedList.IndexOf(item);
                    if (index >= 0 && !this.SuspendListChangedEvents)
                    {
                        this.ApplyFilterAndSort();
                        this.ListChanged?.Invoke(this, new ListChangedEventArgs(ListChangedType.ItemDeleted, index));
                        this.CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, index));
                    }
                }
            }
        }

        /// <summary>
        /// Удаляет элемент по индексу.
        /// </summary>
        /// <param name="index">Индекс удаляемого элемента.</param>
        public void RemoveAt(int index)
        {
            T item;
            lock (this.SyncRoot)
            {
                item = this.sourceList[index];
                this.sourceList.RemoveAt(index);
                this.ApplyFilterAndSort();
            }

            if (!this.SuspendListChangedEvents)
            {
                this.ListChanged?.Invoke(this, new ListChangedEventArgs(ListChangedType.ItemDeleted, index));
                this.CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, index));
            }
        }

        /// <summary>
        /// Удаляет фильтр и сбрасывает отображаемый список.
        /// </summary>
        public void RemoveFilter()
        {
            this.filter = null;

            lock (this.SyncRoot)
            {
                this.sourceFilteredAndSortedList = this.sourceList.ToList();
                this.RebuildNodes();
            }

            this.RaiseResetEvents();
        }

        /// <summary>
        /// Removes the index associated with the specified property from the collection, if one exists.
        /// </summary>
        /// <param name="property">The property descriptor that identifies the index to remove. Cannot be null.</param>
        /// <remarks>This method is typically used to remove a previously added index for optimized
        /// searching or sorting. If the specified property does not have an associated index, the method has no
        /// effect.</remarks>
        void IBindingList.RemoveIndex(PropertyDescriptor property)
        {
        }

        /// <summary>
        /// Удаляет диапазон элементов из списка.
        /// </summary>
        /// <param name="items">Коллекция удаляемых элементов.</param>
        /// <exception cref="ArgumentNullException">nameof(items).</exception>
        public void RemoveRange(IEnumerable<T> items)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            var previousSuspendState = this.SuspendListChangedEvents;
            this.suspendListChangedEvents = true;
            foreach (var item in items)
            {
                this.Remove(item);
            }

            this.suspendListChangedEvents = previousSuspendState;

            this.ApplyFilterAndSort();

            if (!this.SuspendListChangedEvents)
            {
                this.ListChanged?.Invoke(this, new ListChangedEventArgs(ListChangedType.ItemDeleted, -1));
                this.CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove));
            }
        }

        /// <summary>
        /// Удаляет сортировку и сбрасывает отображаемый список.
        /// </summary>
        public void RemoveSort()
        {
            if (this.IsSorted)
            {
                lock (this.SyncRoot)
                {
                    this.ApplyFilterAndSort();
                }
            }

            this.SortProperty = null;
            this.SortDescriptions = null;
            this.IsSorted = false;

            this.RaiseResetEvents();
        }

        /// <summary>
        /// Sets the visibility of all rows in the underlying data source.
        /// </summary>
        /// <param name="visible">true to make all rows visible; false to hide all rows.</param>
        public void SetAllRowsVisible(bool visible) => this.SetRowsVisible(this.sourceList, visible);

        /// <summary>
        /// Sets the visibility of the specified rows.
        /// </summary>
        /// <param name="visible">A value indicating whether the specified rows should be visible. Set to <see langword="true" /> to make the
        /// rows visible; otherwise, <see langword="false" />.</param>
        /// <param name="items">An array of items representing the rows whose visibility will be set. Cannot be null or empty.</param>
        public void SetRowsVisible(bool visible, params T[] items) => this.SetRowsVisible(items.AsEnumerable(), visible);

        /// <summary>
        /// Sets the visibility of the specified rows in the collection.
        /// </summary>
        /// <param name="items">The collection of items whose visibility will be updated.</param>
        /// <param name="visible">A value indicating whether the specified rows should be visible. Set to <see langword="true" /> to make the
        /// rows visible; otherwise, <see langword="false" />.</param>
        /// <remarks>After updating the visibility of the specified rows, the method reapplies any active
        /// filters and sorting to the collection.</remarks>
        public void SetRowsVisible(IEnumerable<T> items, bool visible)
        {
            foreach (var item in items)
            {
                if (this.nodeMap.TryGetValue(item, out var node))
                {
                    node.Visible = visible;
                }
            }

            this.ApplyFilterAndSort();
        }

        /// <summary>
        /// Sets the visibility of the specified row.
        /// </summary>
        /// <param name="item">The item representing the row whose visibility is to be changed.</param>
        /// <param name="visible">A value indicating whether the row should be visible. Set to <see langword="true" /> to make the row visible;
        /// otherwise, <see langword="false" />.</param>
        public void SetRowVisible(T item, bool visible) => this.SetRowsVisible(new[] { item }, visible);

        /// <summary>
        /// Sets the visibility of a single row in the data source at the specified index.
        /// </summary>
        /// <param name="itemSourceIndex">The zero-based index of the row in the data source whose visibility is to be changed. Must be within the
        /// valid range of the data source.</param>
        /// <param name="visible">A value indicating whether the row should be visible. Set to <see langword="true" /> to make the row visible;
        /// otherwise, <see langword="false" />.</param>
        public void SetRowVisible(int itemSourceIndex, bool visible) => this.SetRowsVisible(new[] { this.sourceList[itemSourceIndex] }, visible);

        /// <summary>
        /// Handles the PropertyChanged event for an item in the collection, updating the filtered and sorted view as
        /// necessary.
        /// </summary>
        /// <param name="s">The source object that raised the PropertyChanged event.</param>
        /// <param name="e">An object that contains the event data, including the name of the property that changed.</param>
        private void OnItemOnPropertyChanged(object s, PropertyChangedEventArgs e)
        {
            this.ApplyFilterAndSort();
            this.RaiseResetEvents();
        }

        /// <summary>
        /// Разбирает строку <see cref="SortBy" /> и применяет соответствующую сортировку.
        /// Поддерживаемые разделители: пробел, запятая, точка с запятой.
        /// Если строка пуста или содержит только пробелы — сортировка удаляется.
        /// </summary>
        private void OnSortByChanged()
        {
            var s = this.sortBy;
            if (string.IsNullOrWhiteSpace(s))
            {
                this.RemoveSort();
                return;
            }

            // Разделяем по пробелу, запятой или точке с запятой
            var parts = s.Split(this.sortSeparators, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrEmpty(p))
                .ToArray();

            if (parts.Length == 0)
            {
                this.RemoveSort();
                return;
            }

            // Одно свойство — используем ApplySort(property, direction)
            if (parts.Length == 1)
            {
                var cfg = parts[0].Trim().Split(' ');
                var pd = TypeDescriptor.GetProperties(typeof(T)).Find(cfg[0], false);
                if (pd != null)
                {
                    this.ApplySort(pd, cfg.Length != 1 ? cfg[1].Case(x => this.SortDirection, x => x.ToLower().Trim(), ("asc", then: ListSortDirection.Ascending), ("desc", ListSortDirection.Descending)) : this.SortDirection);
                }
                else
                {
                    // Если свойство не найдено — просто удаляем сортировку
                    this.RemoveSort();
                }

                return;
            }

            // Несколько свойств — создаём ListSortDescriptionCollection и применяем
            var descriptors = TypeDescriptor.GetProperties(typeof(T));
            var list = new List<ListSortDescription>();
            foreach (var name in parts)
            {
                var cfg = name.Trim().Split(' ');
                var pd = descriptors.Find(cfg[0], false);
                if (pd != null)
                {
                    list.Add(
                        new ListSortDescription(pd, cfg.Length == 1 ? this.SortDirection : cfg[1].Case(x => this.SortDirection, x => x.ToLower().Trim(), ("asc", ListSortDirection.Ascending), ("desc", ListSortDirection.Descending))));
                }
            }

            if (list.Count == 0)
            {
                this.RemoveSort();
                return;
            }

            var arr = list.ToArray();
            this.SortDescriptions = new ListSortDescriptionCollection(arr);
            this.ApplyFilterAndSort();
        }

        /// <summary>
        /// Генерирует события сброса для обновления состояния коллекции.
        /// </summary>
        private void RaiseResetEvents()
        {
            if (this.SuspendListChangedEvents)
            {
                return;
            }

            void Raise()
            {
                this.ListChanged?.Invoke(this, new ListChangedEventArgs(ListChangedType.Reset, -1));
                this.CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }

            Raise();
        }

        /// <summary>
        /// Rebuilds the node map.
        /// </summary>
        private void RebuildNodeMap() => this.nodeMap = this.sourceList.Select((x, i) => (x, i)).ToDictionary(x => x.x, v => new BindingListViewRow(this, v.x, v.i));

        /// <summary>
        /// Rebuilds the nodes.
        /// </summary>
        private void RebuildNodes()
        {
            var visibleIndex = 0;
            foreach (var sourceItem in this.sourceFilteredAndSortedList)
            {
                var node = this.nodeMap[sourceItem];
                if (!node.Visible)
                {
                    continue;
                }

                node.VisibleIndex = visibleIndex++;
            }
        }

        /// <summary>
        /// Subscribes the on property changed.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="subscribe">The subscribe.</param>
        private void SubscribeOnPropertyChanged(object item, bool subscribe)
        {
            if (!(item is INotifyPropertyChanged notifyPropertyChanged))
            {
                return;
            }

            if (subscribe)
            {
                notifyPropertyChanged.PropertyChanged += this.OnItemOnPropertyChanged;
            }
            else
            {
                notifyPropertyChanged.PropertyChanged -= this.OnItemOnPropertyChanged;
            }
        }

        /// <summary>
        /// Class BindingListViewRow. This class cannot be inherited.
        /// </summary>
        public sealed class BindingListViewRow
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="BindingListViewRow" /> class.
            /// Инициализирует новый экземпляр класса BindingListViewRow с указанным владельцем, элементом источника и
            /// индексом в исходном списке.
            /// </summary>
            /// <param name="owner">Владелец, к которому относится эта строка. Не может быть null, если требуется определить индекс элемента
            /// в списке по умолчанию.</param>
            /// <param name="sourceItem">Элемент данных, представляемый этой строкой. Не может быть null.</param>
            /// <param name="sourceListIndex">Индекс элемента в исходном списке. Если не указан, индекс будет определён автоматически на основе
            /// владельца и элемента.</param>
            internal BindingListViewRow(BindingListView<T> owner, T sourceItem, int? sourceListIndex)
            {
                this.Item = sourceItem;
                this.SourceListIndex = sourceListIndex ?? owner?.IndexOf(sourceItem, 0) ?? -1;
                this.VisibleIndex = this.SourceListIndex;
            }

            /// <summary>
            /// Gets the value stored in the current instance.
            /// </summary>
            /// <value>The item.</value>
            public T Item { get; internal set; }

            /// <summary>
            /// Gets the zero-based index of the item in the source list.
            /// </summary>
            /// <value>The index of the source list.</value>
            public int SourceListIndex { get; internal set; }

            /// <summary>
            /// Gets or sets a value indicating whether the element is visible.
            /// </summary>
            /// <value>The visible.</value>
            public bool Visible { get; set; } = true;

            /// <summary>
            /// Gets the zero-based index that determines the visible position of the element within its container.
            /// </summary>
            /// <value>The index of the visible.</value>
            public int VisibleIndex { get; internal set; }

            /// <inheritdoc/>
            public override string ToString() => $"[{this.SourceListIndex}] {this.Item?.ToString() ?? base.ToString()}".Trim();
        }
    }
}