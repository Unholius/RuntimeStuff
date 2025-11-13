using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace RuntimeStuff.UI.Core
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
    public class BindingListView<T> : INotifyPropertyChanged, IBindingListView, INotifyCollectionChanged, IEnumerable<T> where T : class
    {
        public enum IndexType
        {
            Source,
            FilteredSorted
        }

        private readonly List<BindingListViewNode<T>> _nodes = new List<BindingListViewNode<T>>();

        private readonly char[] _sortSeparators = { ',', ';' };
        private readonly List<T> _sourceList;
        private readonly SynchronizationContext _uiContext = SynchronizationContext.Current ?? new SynchronizationContext();
        private string _filter;
        private Dictionary<T, BindingListViewNode<T>> _nodeMap;
        private string _sortBy;
        private List<T> _sourceFilteredAndSortedList;

        /// <summary>
        ///     Инициализирует новый экземпляр класса <see cref="BindingListView{T}" />.
        /// </summary>
        public BindingListView()
        {
            _sourceList = new List<T>();
            _sourceFilteredAndSortedList = new List<T>();
            _nodeMap = new Dictionary<T, BindingListViewNode<T>>();
            _nodes = new List<BindingListViewNode<T>>();
            RaiseResetEvents();
        }

        /// <summary>
        ///     Инициализирует новый экземпляр класса <see cref="BindingListView{T}" />.
        /// </summary>
        /// <param name="items">Исходная коллекция элементов.</param>
        /// <exception cref="ArgumentNullException">Если <paramref name="items" /> равен null.</exception>
        public BindingListView(IEnumerable<T> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            _sourceList = items.ToList();
            _sourceFilteredAndSortedList = _sourceList.ToList();
            RebuildNodeMap();
            RebuildNodes();
        }

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
            set
            {
                if (_sortBy == value) return;
                _sortBy = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SortBy)));
                OnSortByChanged();
            }
        }

        public BindingListViewNode<T> this[int index, IndexType indexType] => indexType == IndexType.FilteredSorted ? _nodes[index] : _nodeMap[_sourceFilteredAndSortedList[index]];

        /// <summary>
        ///     Событие, возникающее при изменении списка.
        /// </summary>
        public event ListChangedEventHandler ListChanged;

        /// <summary>
        ///     Разрешено ли редактирование элементов.
        /// </summary>
        public bool AllowEdit { get; set; } = true;

        /// <summary>
        ///     Разрешено ли добавление новых элементов.
        /// </summary>
        public bool AllowNew { get; set; } = true;

        /// <summary>
        ///     Разрешено ли удаление элементов.
        /// </summary>
        public bool AllowRemove { get; set; } = true;

        /// <summary>
        ///     Количество элементов в отображаемом списке.
        /// </summary>
        public int Count
        {
            get
            {
                lock (SyncRoot)
                {
                    return _nodes.Count;
                }
            }
        }

        /// <summary>
        ///     Строка фильтрации для отображаемого списка.
        /// </summary>
        public string Filter
        {
            get => _filter;
            set
            {
                if (_filter == value) return;
                _filter = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Filter)));
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
        public bool IsSorted { get; set; }

        /// <summary>
        ///     Является ли доступ к списку синхронизированным.
        /// </summary>
        public bool IsSynchronized => false;

        /// <summary>
        ///     Описания сортировки для списка.
        /// </summary>
        public ListSortDescriptionCollection SortDescriptions { get; set; }

        /// <summary>
        ///     Направление сортировки.
        /// </summary>
        public ListSortDirection SortDirection { get; set; }

        /// <summary>
        ///     Свойство, по которому выполняется сортировка.
        /// </summary>
        public PropertyDescriptor SortProperty { get; set; }

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
        ///     Получает или задает элемент по индексу.
        /// </summary>
        /// <param name="index">Индекс элемента.</param>
        /// <returns>Элемент по указанному индексу.</returns>
        public object this[int index]
        {
            get
            {
                lock (SyncRoot)
                {
                    return _nodes[index];
                }
            }
            set
            {
                if (!(value is T item))
                    throw new ArgumentException($"Value must be of type {typeof(T).Name}");

                lock (SyncRoot)
                {
                    _sourceList[index] = item;
                    var bli = new BindingListViewNode<T>(item, _sourceList, index);
                    _nodeMap[item] = bli;
                    ApplyFilterAndSort();
                }

                ListChanged?.Invoke(this, new ListChangedEventArgs(ListChangedType.ItemChanged, index));
                CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace));
            }
        }

        /// <summary>
        ///     Добавляет элемент в список.
        /// </summary>
        /// <param name="value">Добавляемый элемент.</param>
        /// <returns>Индекс добавленного элемента.</returns>
        /// <exception cref="ArgumentException">Если тип элемента неверен.</exception>
        /// <exception cref="ArgumentNullException">Если элемент равен null.</exception>
        public int Add(object value)
        {
            if (!(value is T item))
                throw new ArgumentException($"Value must be of type {typeof(T).Name}");
            if (item == null)
                throw new ArgumentNullException(nameof(value));
            lock (SyncRoot)
            {
                _sourceList.Add(item);
                var bli = new BindingListViewNode<T>(item, _sourceList, _sourceList.Count - 1);
                _nodeMap[item] = bli;
            }

            ApplyFilterAndSort();
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add));
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
            var item = Activator.CreateInstance<T>();
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
                _sourceList.Clear();
                _nodeMap.Clear();
                //_fullList.Clear();
                _nodes.Clear();
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
            return value is T item && _nodes.Contains(x => x.Value == item);
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
                ((ICollection)_nodes).CopyTo(array, index);
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
                for (var i = 0; i < _nodes.Count; i++)
                {
                    var value = property.GetValue(_nodes[i]);
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
                return _nodes.Select(x => x.Value).GetEnumerator();
            }
        }

        /// <summary>
        ///     Возвращает индекс указанного элемента.
        /// </summary>
        /// <param name="value">Элемент для поиска.</param>
        /// <returns>Индекс элемента или -1, если не найден.</returns>
        public int IndexOf(object value)
        {
            return value is T item ? _nodes.IndexOf(x => x.Value == item) : -1;
        }

        /// <summary>
        ///     Вставляет элемент в список по указанному индексу.
        /// </summary>
        /// <param name="index">Индекс вставки.</param>
        /// <param name="value">Вставляемый элемент.</param>
        public void Insert(int index, object value)
        {
            if (!(value is T item))
                throw new ArgumentException($"Value must be of type {typeof(T).Name}");
            if (item == null)
                throw new ArgumentNullException(nameof(value));

            lock (SyncRoot)
            {
                _sourceList.Insert(index, item);
                var bli = new BindingListViewNode<T>(item, _sourceList, index);
                _nodeMap[item] = bli;
                ApplyFilterAndSort();
            }

            ListChanged?.Invoke(this, new ListChangedEventArgs(ListChangedType.ItemAdded, index));
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
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
                if (_sourceList.Remove((T)value))
                {
                    var index = _nodes.IndexOf(x => x.Value == item);
                    if (index >= 0)
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

            ListChanged?.Invoke(this, new ListChangedEventArgs(ListChangedType.ItemDeleted, index));
            CollectionChanged?.Invoke(this,
                new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, index));
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
            IsSorted = false;

            RaiseResetEvents();
        }

        public void ApplySort(PropertyDescriptor property, ListSortDirection direction)
        {
            SortProperty = property;
            SortDirection = direction;

            lock (SyncRoot)
            {
                _sourceFilteredAndSortedList = SortHelper.Sort(_sourceFilteredAndSortedList, direction, property.Name).ToList();
                IsSorted = true;
            }

            RaiseResetEvents();
        }

        public void ApplySort(ListSortDescriptionCollection sorts)
        {
            SortDescriptions = sorts;

            // Применяем сортировку, если она установлена
            if (IsSorted && SortProperty != null)
                _sourceFilteredAndSortedList = SortHelper.Sort(_sourceFilteredAndSortedList, SortDirection, SortProperty.Name).ToList();
            else if (SortDescriptions != null && SortDescriptions.Count > 0)
                ApplySort(SortDescriptions);

            RaiseResetEvents();
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return _nodes.Select(x => x.Value).GetEnumerator();
        }

        /// <summary>
        ///     Событие, возникающее при изменении коллекции.
        /// </summary>
        public event NotifyCollectionChangedEventHandler CollectionChanged;

        /// <summary>
        ///     Событие, возникающее при изменении свойства.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        private void RebuildNodeMap()
        {
            _nodeMap = _sourceList.Select((x, i) => (x, i)).ToDictionary(x => x.x, v => new BindingListViewNode<T>(v.x, _sourceList, v.i));
        }

        private void RebuildNodes()
        {
            _nodes.Clear();
            var visibleIndex = 0;
            foreach (var sourceItem in _sourceFilteredAndSortedList)
            {
                var node = _nodeMap[sourceItem];
                if (!node.Visible)
                    continue;
                node.VisibleIndex = visibleIndex++;
                _nodes.Add(node);
            }
        }

        //private void UpdateBindingListIndices(bool srcIndex, bool visibleIndex)
        //{
        //    if (srcIndex && _nodeMap != null && _sourceList != null)
        //    {
        //        for (var i = 0; i < _sourceList.Count; i++)
        //        {
        //            _nodeMap[_sourceList[i]].SourceListIndex = i;
        //            _nodeMap[_sourceList[i]].VisibleIndex = -1;
        //        }
        //    }

        //    if (visibleIndex && _sourceFilteredAndSortedList != null && _nodeMap != null && _sourceList != null)
        //    {
        //        for (var i = 0; i < _sourceFilteredAndSortedList.Count; i++)
        //        {
        //            _nodeMap[_sourceList[i]].VisibleIndex = i;
        //        }
        //    }
        //}

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

            var parts = s.Split(_sortSeparators, StringSplitOptions.RemoveEmptyEntries)
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
            var sorts = new ListSortDescriptionCollection(arr);
            ApplySort(sorts);
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
                var i = _sourceList.Count;
                foreach (var item in items)
                {
                    _sourceList.Add(item);
                    _nodeMap.Add(item, new BindingListViewNode<T>(item, _sourceList, i++));
                }
            }

            ApplyFilterAndSort();
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add));
        }

        /// <summary>
        ///     Удаляет диапазон элементов из списка.
        /// </summary>
        /// <param name="items">Коллекция удаляемых элементов.</param>
        /// <exception cref="ArgumentNullException">Если <paramref name="items" /> равен null.</exception>
        public void RemoveRange(IEnumerable<T> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            lock (SyncRoot)
            {
                _sourceList.RemoveAll(items.Contains);
            }

            ApplyFilterAndSort();
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove));
        }

        /// <summary>
        ///     Применяет фильтр к списку.
        /// </summary>
        private void ApplyFilterAndSort()
        {
            lock (SyncRoot)
            {
                try
                {
                    _sourceFilteredAndSortedList = FilterHelper.Filter(_sourceList, _filter).ToList();
                }
                catch (FormatException)
                {
                    _sourceFilteredAndSortedList = FilterHelper.FilterByText(_sourceList, _filter).ToList();
                }

                if (IsSorted || !string.IsNullOrWhiteSpace(_sortBy))
                    ApplySort(SortDescriptions);
            }

            RaiseResetEvents();
        }

        /// <summary>
        ///     Генерирует события сброса для обновления состояния коллекции.
        /// </summary>
        private void RaiseResetEvents()
        {
            void Raise()
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
                ListChanged?.Invoke(this, new ListChangedEventArgs(ListChangedType.Reset, -1));
                CollectionChanged?.Invoke(this,
                    new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }

            // если мы не в UI-потоке — пересылаем выполнение туда
            if (SynchronizationContext.Current == _uiContext)
                Raise();
            else
                _uiContext.Post(_ => Raise(), null);
        }
    }
}