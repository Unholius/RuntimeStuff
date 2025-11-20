using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using RuntimeStuff.Extensions;
using RuntimeStuff.Helpers;

namespace RuntimeStuff
{
    /// <summary>
    ///     Представляет коллекцию, поддерживающую фильтрацию, сортировку, поиск и уведомления об изменениях,
    ///     предназначенную для использования в привязке данных и отображении списков объектов.
    /// </summary>
    /// <remarks>
    ///     Класс реализует интерфейсы INotifyPropertyChanged, IBindingListView и
    ///     INotifyCollectionChanged, что позволяет использовать его в сценариях с динамическим обновлением данных,
    ///     например, в UI-приложениях. Поддерживает многопоточный доступ через объект синхронизации <see cref="SyncRoot" />.
    ///     Изменения фильтрации и сортировки автоматически применяются к отображаемому списку. Для корректной работы
    ///     рекомендуется использовать с типами, обладающими публичными свойствами, по которым возможна сортировка и
    ///     фильтрация.
    /// </remarks>
    /// <typeparam name="T">Тип элементов, содержащихся в списке. Должен быть ссылочным типом.</typeparam>
    [DebuggerDisplay("Count = {Count} TotalCount = {TotalCount}")]
    public class BindingListView<T> : PropertyChangeNotifier, IBindingListView, INotifyCollectionChanged, IEnumerable<T> where T : class
    {
        public enum IndexType
        {
            Source,
            FilteredSorted
        }

        private readonly List<T> _sourceList;
        public readonly char[] SortSeparators = { ',', ';' };
        private bool _allowEdit = true;
        private bool _allowNew = true;
        private bool _allowRemove = true;
        private string _filter;
        private bool _isSorted;
        private Dictionary<T, BindingListViewRow> _nodeMap;
        private string _sortBy;
        private List<T> _sourceFilteredAndSortedList;
        private bool _suspendListChangedEvents;

        /// <summary>
        ///     Инициализирует новый экземпляр класса <see cref="BindingListView{T}" />.
        /// </summary>
        public BindingListView()
        {
            _sourceList = new List<T>();
            _sourceFilteredAndSortedList = new List<T>();
            _nodeMap = new Dictionary<T, BindingListViewRow>();
            Properties = TypeHelper.GetProperties<T>();
        }

        /// <summary>
        ///     Инициализирует новый экземпляр класса <see cref="BindingListView{T}" />.
        /// </summary>
        /// <param name="items">Исходная коллекция элементов.</param>
        /// <exception cref="ArgumentNullException">Если <paramref name="items" /> равен null.</exception>
        public BindingListView(IEnumerable<T> items) : this()
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            _sourceList = items.ToList();
            _sourceFilteredAndSortedList = _sourceList.ToList();
            RebuildNodeMap();
        }

        public PropertyInfo[] Properties { get; }

        /// <summary>
        ///     Возвращает общее количество элементов в исходном списке.
        /// </summary>
        public int TotalCount => _sourceList.Count;

        /// <summary>
        ///     Строка с перечислением имён свойств, по которым нужно выполнить сортировку.
        ///     Можно перечислять имена через запятую или точку с запятой.
        ///     Через пробел после имени свойства можно указать направление сортировки: ASC (по возрастанию) или DESC (по
        ///     убыванию).
        ///     При изменении свойства автоматически применяется сортировка.
        /// </summary>
        public string SortBy
        {
            get => _sortBy;
            set => SetProperty(ref _sortBy, value, OnSortByChanged);
        }

        /// <summary>
        ///     Прекращает или возобновляет генерацию событий изменения списка.
        /// </summary>
        public bool SuspendListChangedEvents
        {
            get => _suspendListChangedEvents;
            set => SetProperty(ref _suspendListChangedEvents, value, RaiseResetEvents);
        }

        /// <summary>
        ///     Фабрика для создания новых элементов при вызове AddNew().
        /// </summary>
        public Func<T> NewItemFactory { get; set; }

        /// <summary>
        ///     Отфильтрован ли список.
        /// </summary>
        public bool IsFiltered => Count != TotalCount;

        /// <summary>
        ///     Событие, возникающее при изменении списка.
        /// </summary>
        public event ListChangedEventHandler ListChanged;

        /// <summary>
        ///     Разрешено ли редактирование элементов.
        /// </summary>
        public bool AllowEdit
        {
            get => _allowEdit;
            set => SetProperty(ref _allowEdit, value);
        }

        /// <summary>
        ///     Разрешено ли добавление новых элементов.
        /// </summary>
        public bool AllowNew
        {
            get => _allowNew;
            set => SetProperty(ref _allowNew, value);
        }

        /// <summary>
        ///     Разрешено ли удаление элементов.
        /// </summary>
        public bool AllowRemove
        {
            get => _allowRemove;
            set => SetProperty(ref _allowRemove, value);
        }

        /// <summary>
        ///     Количество элементов в отображаемом списке.
        /// </summary>
        public int Count
        {
            get
            {
                lock (SyncRoot)
                {
                    return _sourceFilteredAndSortedList.Count;
                }
            }
        }

        /// <summary>
        ///     Строка фильтрации для отображаемого списка. Формат: [ИмяСвойства] Оператор Значение. Операторы: ==, &lt; &gt;, &gt;
        ///     , &lt;, &gt;=, &lt;=, LIKE, IN.
        ///     Строковые значения должны быть в одинарных кавычках. Несколько условий можно объединять логическими операторами
        ///     AND, OR.
        /// </summary>
        public string Filter
        {
            get => _filter;
            set
            {
                if (!SetProperty(ref _filter, value))
                    return;
                ApplyFilterAndSort();
                RebuildNodes();
            }
        }

        /// <summary>
        ///     Является ли список фиксированного размера.
        /// </summary>
        public bool IsFixedSize => false;

        /// <summary>
        ///     Является ли список только для чтения.
        /// </summary>
        public bool IsReadOnly => false;

        /// <summary>
        ///     Отсортирован ли список.
        /// </summary>
        public bool IsSorted
        {
            get => _isSorted;
            set => SetProperty(ref _isSorted, value);
        }

        /// <summary>
        ///     Является ли доступ к списку синхронизированным.
        /// </summary>
        public bool IsSynchronized => false;

        /// <summary>
        ///     Описания сортировки для списка.
        /// </summary>
        public ListSortDescriptionCollection SortDescriptions { get; private set; }

        /// <summary>
        ///     Направление сортировки.
        /// </summary>
        public ListSortDirection SortDirection { get; private set; }

        /// <summary>
        ///     Свойство, по которому выполняется сортировка.
        /// </summary>
        public PropertyDescriptor SortProperty { get; private set; }

        /// <summary>
        ///     Поддерживает ли расширенную сортировку.
        /// </summary>
        public bool SupportsAdvancedSorting => true;

        /// <summary>
        ///     Поддерживает ли уведомления об изменениях.
        /// </summary>
        public bool SupportsChangeNotification => true;

        /// <summary>
        ///     Поддерживает ли фильтрацию.
        /// </summary>
        public bool SupportsFiltering => true;

        /// <summary>
        ///     Поддерживает ли поиск.
        /// </summary>
        public bool SupportsSearching => true;

        /// <summary>
        ///     Поддерживает ли сортировку.
        /// </summary>
        public bool SupportsSorting => true;

        /// <summary>
        ///     Объект синхронизации для многопоточного доступа.
        /// </summary>
        public object SyncRoot { get; } = new object();

        /// <summary>
        ///     Получает или задает элемент по индексу из отфильтрованного и отсортированного списка.
        /// </summary>
        /// <param name="index">Индекс элемента.</param>
        /// <returns>Элемент по указанному индексу.</returns>
        public object this[int index]
        {
            get
            {
                lock (SyncRoot)
                {
                    return _sourceFilteredAndSortedList[index];
                }
            }
            set
            {
                if (!(value is T item))
                    throw new ArgumentException($"Item must be of type {typeof(T).Name}");

                lock (SyncRoot)
                {
                    SubscribeOnPropertyChanged(item, true);
                    _sourceFilteredAndSortedList[index] = item;
                    var bli = new BindingListViewRow(this, item, index);
                    _nodeMap[item] = bli;
                    ApplyFilterAndSort();
                }

                if (!SuspendListChangedEvents)
                {
                    ListChanged?.Invoke(this, new ListChangedEventArgs(ListChangedType.ItemChanged, index));
                    CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace));
                }
            }
        }

        /// <summary>
        ///     Добавляет элемент в список. Для оптимизации используй <see cref="AddRange"/> или <see cref="SuspendListChangedEvents"/>
        /// </summary>
        /// <param name="value">Добавляемый элемент.</param>
        /// <returns>Индекс добавленного элемента.</returns>
        /// <exception cref="ArgumentException">Если тип элемента неверен.</exception>
        /// <exception cref="ArgumentNullException">Если элемент равен null.</exception>
        public int Add(object value)
        {
            if (!(value is T item))
                throw new ArgumentException($"Item must be of type {typeof(T).Name}");

            SubscribeOnPropertyChanged(item, true);

            if (item == null)
                throw new ArgumentNullException(nameof(value));
            lock (SyncRoot)
            {
                _sourceList.Add(item);
                _sourceFilteredAndSortedList.Add(item);
                var bli = new BindingListViewRow(this, item, _sourceList.Count - 1);
                _nodeMap[item] = bli;
            }

            if (!SuspendListChangedEvents)
            {
                ApplyFilterAndSort();
                ListChanged?.Invoke(this, new ListChangedEventArgs(ListChangedType.ItemAdded, _sourceList.Count - 1));
                CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add));
            }

            return _sourceList.Count - 1;
        }

        void IBindingList.AddIndex(PropertyDescriptor property)
        {
        }

        /// <summary>
        ///     Добавляет новый элемент, создавая его через конструктор по умолчанию.
        /// </summary>
        /// <returns>Добавленный элемент.</returns>
        public object AddNew()
        {
            var item = NewItemFactory?.Invoke() ?? Activator.CreateInstance<T>();
            Add(item);
            return item;
        }

        /// <summary>
        ///     Очищает список.
        /// </summary>
        public void Clear()
        {
            lock (SyncRoot)
            {
                foreach (var item in _sourceList) SubscribeOnPropertyChanged(item, false);
                _sourceList.Clear();
                _sourceFilteredAndSortedList.Clear();
                _nodeMap.Clear();
            }

            RaiseResetEvents();
        }

        /// <summary>
        ///     Проверяет, содержится ли элемент в списке.
        /// </summary>
        /// <param name="value">Проверяемый элемент.</param>
        /// <returns>True, если элемент найден; иначе false.</returns>
        public bool Contains(object value)
        {
            return value is T item && _sourceFilteredAndSortedList.Contains(item);
        }

        /// <summary>
        ///     Копирует элементы списка в массив, начиная с указанного индекса.
        /// </summary>
        /// <param name="array">Массив назначения.</param>
        /// <param name="index">Начальный индекс копирования.</param>
        public void CopyTo(Array array, int index)
        {
            lock (SyncRoot)
            {
                ((ICollection)_sourceFilteredAndSortedList).CopyTo(array, index);
            }
        }

        /// <summary>
        ///     Находит индекс элемента по значению свойства.
        /// </summary>
        /// <param name="property">Свойство для поиска.</param>
        /// <param name="key">Значение для поиска.</param>
        /// <returns>Индекс найденного элемента или -1, если не найден.</returns>
        int IBindingList.Find(PropertyDescriptor property, object key)
        {
            lock (SyncRoot)
            {
                for (var i = 0; i < _sourceFilteredAndSortedList.Count; i++)
                {
                    var value = TypeHelper.GetValue(_sourceFilteredAndSortedList[i], property.Name);
                    if (Equals(value, key))
                        return i;
                }
            }

            return -1;
        }

        /// <summary>
        ///     Возвращает перечислитель для списка.
        /// </summary>
        /// <returns>Перечислитель элементов.</returns>
        public IEnumerator GetEnumerator()
        {
            lock (SyncRoot)
            {
                return _sourceFilteredAndSortedList.GetEnumerator();
            }
        }

        /// <summary>
        ///     Возвращает индекс указанного элемента.
        /// </summary>
        /// <param name="value">Элемент для поиска.</param>
        /// <returns>Индекс элемента или -1, если не найден.</returns>
        public int IndexOf(object value)
        {
            return value is T item ? _sourceFilteredAndSortedList.IndexOf(item) : -1;
        }

        /// <summary>
        ///     Вставляет элемент в список по указанному индексу.
        /// </summary>
        /// <param name="index">Индекс вставки.</param>
        /// <param name="value">Вставляемый элемент.</param>
        public void Insert(int index, object value)
        {
            if (!(value is T item))
                throw new ArgumentException($"Item must be of type {typeof(T).Name}");
            if (item == null)
                throw new ArgumentNullException(nameof(value));

            lock (SyncRoot)
            {
                _sourceList.Insert(index, item);
                var bli = new BindingListViewRow(this, item, index);
                _nodeMap[item] = bli;
                ApplyFilterAndSort();
            }

            if (!SuspendListChangedEvents)
            {
                ListChanged?.Invoke(this, new ListChangedEventArgs(ListChangedType.ItemAdded, index));
                CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
            }
        }

        /// <summary>
        ///     Удаляет элемент из списка.
        /// </summary>
        /// <param name="value">Удаляемый элемент.</param>
        public void Remove(object value)
        {
            if (!(value is T item)) return;

            lock (SyncRoot)
            {
                SubscribeOnPropertyChanged(item, false);
                if (_sourceList.Remove(item))
                {
                    var index = _sourceFilteredAndSortedList.IndexOf(item);
                    if (index >= 0)
                        if (!SuspendListChangedEvents)
                        {
                            ApplyFilterAndSort();
                            ListChanged?.Invoke(this, new ListChangedEventArgs(ListChangedType.ItemDeleted, index));
                            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, index));
                        }
                }
            }
        }

        /// <summary>
        ///     Удаляет элемент по индексу.
        /// </summary>
        /// <param name="index">Индекс удаляемого элемента.</param>
        public void RemoveAt(int index)
        {
            T item;
            lock (SyncRoot)
            {
                item = _sourceList[index];
                _sourceList.RemoveAt(index);
                ApplyFilterAndSort();
            }

            if (!SuspendListChangedEvents)
            {
                ListChanged?.Invoke(this, new ListChangedEventArgs(ListChangedType.ItemDeleted, index));
                CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, index));
            }
        }

        /// <summary>
        ///     Удаляет фильтр и сбрасывает отображаемый список.
        /// </summary>
        public void RemoveFilter()
        {
            _filter = null;

            lock (SyncRoot)
            {
                _sourceFilteredAndSortedList = _sourceList.ToList();
                RebuildNodes();
            }

            RaiseResetEvents();
        }

        void IBindingList.RemoveIndex(PropertyDescriptor property)
        {
        }

        /// <summary>
        ///     Удаляет сортировку и сбрасывает отображаемый список.
        /// </summary>
        public void RemoveSort()
        {
            if (IsSorted)
                lock (SyncRoot)
                {
                    ApplyFilterAndSort();
                }

            SortProperty = null;
            SortDescriptions = null;
            IsSorted = false;

            RaiseResetEvents();
        }

        public void ApplySort(PropertyDescriptor property, ListSortDirection direction)
        {
            SortProperty = property;
            SortDirection = direction;

            lock (SyncRoot)
            {
                ApplyFilterAndSort();
            }
        }

        public void ApplySort(ListSortDescriptionCollection sorts)
        {
            SortDescriptions = sorts;
            ApplyFilterAndSort();
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return _sourceFilteredAndSortedList.GetEnumerator();
        }

        /// <summary>
        ///     Событие, возникающее при изменении коллекции.
        /// </summary>
        public event NotifyCollectionChangedEventHandler CollectionChanged;

        /// <summary>
        ///     Получает строку представления элемента по указанному индексу.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="indexType"></param>
        /// <returns></returns>
        public BindingListViewRow GetRow(int index, IndexType indexType = IndexType.FilteredSorted)
        {
            var row = _nodeMap[indexType == IndexType.Source ? _sourceList[index] : _sourceFilteredAndSortedList[index]];
            row.VisibleIndex = _sourceFilteredAndSortedList.IndexOf(row.Item);
            row.SourceListIndex = _sourceList.IndexOf(row.Item);
            return row;
        }

        public object[] GetRowValues(int index, IndexType indexType = IndexType.FilteredSorted)
        {
            var values = new object[Properties.Length];
            var i = 0;
            var rowItem = indexType == IndexType.FilteredSorted ? _sourceFilteredAndSortedList[i] : _sourceList[i];
            foreach (var property in Properties) 
                values[i++] = TypeHelper.GetValue(rowItem, property.Name);
            return values;
        }

        public TValue[] GetPropertyValues<TValue>(Expression<Func<T, TValue>> propertySelector, IndexType indexType = IndexType.FilteredSorted, bool distinct = true)
        {
            return GetPropertyValues<TValue>(propertySelector.GetPropertyName(), indexType, distinct);
        }

        public object[] GetPropertyValues(string propertyName, IndexType indexType = IndexType.FilteredSorted, bool distinct = true)
        {
            return GetPropertyValues<object>(propertyName, indexType, distinct);
        }

        public TValue[] GetPropertyValues<TValue>(string propertyName, IndexType indexType = IndexType.FilteredSorted, bool distinct = true)
        {
            lock (SyncRoot)
            {
                var list = indexType == IndexType.FilteredSorted
                    ? _sourceFilteredAndSortedList
                    : _sourceList;

                if (!distinct)
                {
                    var result = new TValue[list.Count];
                    var i = 0;
                    foreach (var item in list) result[i++] = TypeHelper.GetValue<TValue>(item, propertyName);

                    return result;
                }

                var set = new HashSet<TValue>();

                foreach (var item in list)
                    set.Add(TypeHelper.GetValue<TValue>(item, propertyName));

                return set.ToArray();
            }
        }

        public IEnumerator GetEnumerator(IndexType indexType)
        {
            lock (SyncRoot)
            {
                return indexType == IndexType.FilteredSorted ? _sourceFilteredAndSortedList.GetEnumerator() : _sourceList.GetEnumerator();
            }
        }

        public void SetRowVisible(T item, bool visible)
        {
            SetRowsVisible(new[] { item }, visible);
        }

        public void SetRowVisible(int itemSourceIndex, bool visible)
        {
            SetRowsVisible(new[] { _sourceList[itemSourceIndex] }, visible);
        }

        public void SetRowsVisible(bool visible, params T[] items)
        {
            SetRowsVisible(items.AsEnumerable(), visible);
        }

        public void SetAllRowsVisible(bool visible)
        {
            SetRowsVisible(_sourceList, visible);
        }

        public void SetRowsVisible(IEnumerable<T> items, bool visible)
        {
            foreach (var item in items)
                if (_nodeMap.TryGetValue(item, out var node))
                    node.Visible = visible;

            ApplyFilterAndSort();
        }

        private void OnItemOnPropertyChanged(object s, PropertyChangedEventArgs e)
        {
            ApplyFilterAndSort();
            RaiseResetEvents();
        }

        private void SubscribeOnPropertyChanged(object item, bool subscribe)
        {
            if (!(item is INotifyPropertyChanged notifyPropertyChanged))
                return;
            if (subscribe)
                notifyPropertyChanged.PropertyChanged += OnItemOnPropertyChanged;
            else
                notifyPropertyChanged.PropertyChanged -= OnItemOnPropertyChanged;
        }

        private void RebuildNodeMap()
        {
            _nodeMap = _sourceList.Select((x, i) => (x, i)).ToDictionary(x => x.x, v => new BindingListViewRow(this, v.x, v.i));
        }

        private void RebuildNodes()
        {
            var visibleIndex = 0;
            foreach (var sourceItem in _sourceFilteredAndSortedList)
            {
                var node = _nodeMap[sourceItem];
                if (!node.Visible)
                    continue;
                node.VisibleIndex = visibleIndex++;
            }
        }

        /// <summary>
        ///     Разбирает строку <see cref="SortBy" /> и применяет соответствующую сортировку.
        ///     Поддерживаемые разделители: пробел, запятая, точка с запятой.
        ///     Если строка пуста или содержит только пробелы — сортировка удаляется.
        /// </summary>
        private void OnSortByChanged()
        {
            var s = _sortBy;
            if (string.IsNullOrWhiteSpace(s))
            {
                RemoveSort();
                return;
            }

            // Разделяем по пробелу, запятой или точке с запятой

            var parts = s.Split(SortSeparators, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrEmpty(p))
                .ToArray();

            if (parts.Length == 0)
            {
                RemoveSort();
                return;
            }

            // Одно свойство — используем ApplySort(property, direction)
            if (parts.Length == 1)
            {
                var cfg = parts[0].Trim().Split(' ');
                var pd = TypeDescriptor.GetProperties(typeof(T)).Find(cfg[0], false);
                if (pd != null)
                    ApplySort(pd, cfg.Length == 1
                        ? SortDirection
                        : cfg[1].Case(x => SortDirection, x => x.ToLower().Trim()
                            , ("asc", ListSortDirection.Ascending)
                            , ("desc", ListSortDirection.Descending)
                        ));
                else
                    // Если свойство не найдено — просто удаляем сортировку
                    RemoveSort();
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
                    list.Add(new ListSortDescription(pd, cfg.Length == 1
                        ? SortDirection
                        : cfg[1].Case(x => SortDirection, x => x.ToLower().Trim()
                            , ("asc", ListSortDirection.Ascending)
                            , ("desc", ListSortDirection.Descending)
                        )));
            }

            if (list.Count == 0)
            {
                RemoveSort();
                return;
            }

            var arr = list.ToArray();
            SortDescriptions = new ListSortDescriptionCollection(arr);
            ApplyFilterAndSort();
        }

        /// <summary>
        ///     Добавляет диапазон элементов в список.
        /// </summary>
        /// <param name="items">Коллекция добавляемых элементов.</param>
        /// <exception cref="ArgumentNullException">Если <paramref name="items" /> равен null или содержит null-элемент.</exception>
        public void AddRange(IEnumerable<T> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));

            lock (SyncRoot)
            {
                var previousSuspendState = SuspendListChangedEvents;
                _suspendListChangedEvents = true;
                foreach (var item in items) Add(item);
                _suspendListChangedEvents = previousSuspendState;
            }

            ApplyFilterAndSort();

            if (!SuspendListChangedEvents)
            {
                ListChanged?.Invoke(this, new ListChangedEventArgs(ListChangedType.ItemAdded, _sourceList.Count - 1));
                CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add));
            }
        }

        /// <summary>
        ///     Удаляет диапазон элементов из списка.
        /// </summary>
        /// <param name="items">Коллекция удаляемых элементов.</param>
        /// <exception cref="ArgumentNullException">Если <paramref name="items" /> равен null.</exception>
        public void RemoveRange(IEnumerable<T> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));

            var previousSuspendState = SuspendListChangedEvents;
            _suspendListChangedEvents = true;
            foreach (var item in items)
                Remove(item);
            _suspendListChangedEvents = previousSuspendState;

            ApplyFilterAndSort();

            if (!SuspendListChangedEvents)
            {
                ListChanged?.Invoke(this, new ListChangedEventArgs(ListChangedType.ItemDeleted, -1));
                CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove));
            }
        }

        /// <summary>
        ///     Применяет фильтр к списку.
        /// </summary>
        public void ApplyFilterAndSort()
        {
            lock (SyncRoot)
            {
                var sourceCount = _sourceList.Count;

                try
                {
                    _sourceFilteredAndSortedList = _sourceList.Filter(_filter).ToList();
                }
                catch (FormatException fe)
                {
                    Debug.WriteLine($"Filter format exception: {fe.Message}");
                    _sourceFilteredAndSortedList = _sourceList.FilterByText(_filter).ToList();
                }

                _sourceFilteredAndSortedList.RemoveAll(x => !_nodeMap[x].Visible);

                var filteredCount = _sourceFilteredAndSortedList.Count;
                var prevIsSorted = IsSorted;

                IsSorted = SortProperty != null || SortDescriptions != null || !string.IsNullOrWhiteSpace(_sortBy);

                if (SortProperty != null)
                    _sourceFilteredAndSortedList = _sourceFilteredAndSortedList.Sort(SortDirection, SortProperty.Name).ToList();
                else if (SortDescriptions != null && SortDescriptions.Count > 0)
                    _sourceFilteredAndSortedList = _sourceFilteredAndSortedList.Sort(SortDescriptions).ToList();

                if (sourceCount != filteredCount || prevIsSorted != IsSorted)
                    RaiseResetEvents();
            }
        }

        /// <summary>
        ///     Генерирует события сброса для обновления состояния коллекции.
        /// </summary>
        private void RaiseResetEvents()
        {
            if (SuspendListChangedEvents)
                return;

            void Raise()
            {
                ListChanged?.Invoke(this, new ListChangedEventArgs(ListChangedType.Reset, -1));
                CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }

            // если мы не в UI-потоке — пересылаем выполнение туда
            //if (SynchronizationContext.Current == null || SynchronizationContext.Current == _uiContext)
            Raise();
            //else
            //_uiContext.Post(_ => Raise(), null);
        }

        public sealed class BindingListViewRow
        {
            internal BindingListViewRow(BindingListView<T> owner, T sourceItem, int? sourceListIndex)
            {
                Item = sourceItem;
                SourceListIndex = sourceListIndex ?? owner?.IndexOf(sourceItem, 0) ?? -1;
                VisibleIndex = SourceListIndex;
            }

            public T Item { get; internal set; }
            public bool Visible { get; set; } = true;
            public int SourceListIndex { get; internal set; }
            public int VisibleIndex { get; internal set; }

            public override string ToString()
            {
                return $"[{SourceListIndex}] {Item?.ToString() ?? base.ToString()}".Trim();
            }
        }
    }
}