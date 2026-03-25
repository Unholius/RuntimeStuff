// <copyright file="BindingListView.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

/// <summary>
/// Представляет расширенную версию <see cref="BindingList{T}"/>,
/// поддерживающую фильтрацию, множественную сортировку и уведомления об изменениях коллекции.
/// </summary>
/// <typeparam name="T">Тип элементов в списке.</typeparam>
public class BindingListView<T> : BindingList<T>, IBindingListView, INotifyCollectionChanged, INotifyPropertyChanged
{
    private readonly List<ListSortDirection> sortDirections = new List<ListSortDirection>();
    private readonly List<PropertyDescriptor> sortProperties = new List<PropertyDescriptor>();
    private readonly List<T> source = new List<T>();

    private string filter;
    private Func<T, int, bool> filterFunc;
    private bool isSorted;
    private ListSortDirection sortDirection;
    private PropertyDescriptor sortProperty;

    /// <summary>
    /// Инициализирует новый пустой экземпляр <see cref="BindingListView{T}"/>.
    /// </summary>
    public BindingListView()
    {
    }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="BindingListView{T}"/>.
    /// с начальной коллекцией элементов.
    /// </summary>
    /// <param name="collection">Исходная коллекция элементов.</param>
    public BindingListView(IEnumerable<T> collection)
    {
        this.source.AddRange(collection);
        this.RebuildView();
    }

    /// <summary>
    /// Событие, возникающее при изменении коллекции.
    /// </summary>
    public event NotifyCollectionChangedEventHandler CollectionChanged;

    /// <summary>
    /// Событие, возникающее при изменении свойства.
    /// </summary>
    public event PropertyChangedEventHandler PropertyChanged;

    /// <summary>
    /// Получает или задаёт строковое представление фильтра.
    /// </summary>
    /// <remarks>
    /// При установке значения автоматически пересобирается представление списка.
    /// </remarks>
    public string Filter
    {
        get => this.filter;
        set
        {
            if (this.filter == value)
            {
                return;
            }

            this.filter = value;
            this.filterFunc = this.CreateFilterPredicate(value);

            this.RebuildView();
            this.OnPropertyChanged();
        }
    }

    /// <summary>
    /// Получает коллекцию описаний сортировки.
    /// </summary>
    public ListSortDescriptionCollection SortDescriptions
    {
        get
        {
            var arr = this.sortProperties
                .Select((p, i) => new ListSortDescription(p, this.sortDirections[i]))
                .ToArray();

            return new ListSortDescriptionCollection(arr);
        }
    }

    /// <inheritdoc/>
    public bool SupportsAdvancedSorting => true;

    /// <inheritdoc/>
    public bool SupportsFiltering => true;

    /// <inheritdoc/>
    protected override bool IsSortedCore => this.isSorted;

    /// <inheritdoc/>
    protected override ListSortDirection SortDirectionCore => this.sortDirection;

    /// <inheritdoc/>
    protected override PropertyDescriptor SortPropertyCore => this.sortProperty;

    /// <inheritdoc/>
    protected override bool SupportsSortingCore => true;

    /// <summary>
    /// Добавляет диапазон элементов в коллекцию.
    /// </summary>
    /// <param name="items">Добавляемые элементы.</param>
    public void AddRange(IEnumerable<T> items)
    {
        if (items == null)
        {
            return;
        }

        this.RaiseListChangedEvents = false;

        this.source.AddRange(items);

        this.RaiseListChangedEvents = true;

        this.RebuildView();
    }

    /// <summary>
    /// Применяет множественную сортировку к списку.
    /// </summary>
    /// <param name="sorts">Коллекция описаний сортировки.</param>
    public void ApplySort(ListSortDescriptionCollection sorts)
    {
        this.sortProperties.Clear();
        this.sortDirections.Clear();

        if (sorts != null)
        {
            foreach (ListSortDescription s in sorts)
            {
                this.sortProperties.Add(s.PropertyDescriptor);
                this.sortDirections.Add(s.SortDirection);
            }
        }

        this.isSorted = this.sortProperties.Any();
        this.RebuildView();
    }

    /// <summary>
    /// Удаляет текущий фильтр.
    /// </summary>
    public void RemoveFilter()
    {
        this.Filter = null;
    }

    /// <summary>
    /// Устанавливает фильтр в виде делегата.
    /// </summary>
    /// <param name="predicate">Функция фильтрации (элемент, индекс) → результат.</param>
    public void SetFilter(Func<T, int, bool> predicate)
    {
        this.filterFunc = predicate;
        this.filter = predicate?.ToString();
        this.RebuildView();
    }

    /// <inheritdoc/>
    protected override void ApplySortCore(PropertyDescriptor prop, ListSortDirection direction)
    {
        this.sortProperties.Clear();
        this.sortDirections.Clear();

        if (prop != null)
        {
            this.sortProperties.Add(prop);
            this.sortDirections.Add(direction);
        }

        this.sortProperty = prop;
        this.sortDirection = direction;
        this.isSorted = prop != null;

        this.RebuildView();
    }

    /// <inheritdoc/>
    protected override void ClearItems()
    {
        this.source.Clear();
        this.RebuildView();
    }

    /// <inheritdoc/>
    protected override int FindCore(PropertyDescriptor prop, object key)
    {
        if (prop == null || key == null)
        {
            return -1;
        }

        for (int i = 0; i < this.Count; i++)
        {
            var value = prop.GetValue(this[i]);
            if (Equals(value, key))
            {
                return i;
            }
        }

        return -1;
    }

    /// <inheritdoc/>
    protected override void InsertItem(int index, T item)
    {
        this.source.Add(item);
        this.RebuildView();
    }

    /// <summary>
    /// Вызывает событие <see cref="PropertyChanged"/> при изменении свойства.
    /// </summary>
    /// <param name="name">
    /// Имя изменённого свойства. Если не указано, имя будет определено автоматически
    /// с помощью атрибута <see cref="CallerMemberNameAttribute"/>.
    /// </param>
    /// <remarks>
    /// Используется для уведомления подписчиков (например, механизмов привязки данных)
    /// об изменении значения свойства.
    /// </remarks>
    protected virtual void OnPropertyChanged([CallerMemberName] string name = null)
    {
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <inheritdoc/>
    protected override void RemoveItem(int index)
    {
        if (index < 0 || index >= this.Count)
        {
            return;
        }

        var item = this[index];
        this.source.Remove(item);

        this.RebuildView();
    }

    /// <inheritdoc/>
    protected override void RemoveSortCore()
    {
        this.sortProperties.Clear();
        this.sortDirections.Clear();

        this.sortProperty = null;
        this.isSorted = false;

        this.RebuildView();
    }

    /// <inheritdoc/>
    protected override void SetItem(int index, T item)
    {
        if (index < 0 || index >= this.Count)
        {
            return;
        }

        var oldItem = this[index];
        int srcIndex = this.source.IndexOf(oldItem);

        if (srcIndex >= 0)
        {
            this.source[srcIndex] = item;
        }

        this.RebuildView();
    }

    private IEnumerable<T> ApplyOrdering(IEnumerable<T> query)
    {
        IOrderedEnumerable<T> ordered = null;

        for (int i = 0; i < this.sortProperties.Count; i++)
        {
            var prop = this.sortProperties[i];
            var dir = this.sortDirections[i];

            Func<T, object> key = x => prop.GetValue(x);

            if (i == 0)
            {
                ordered = dir == ListSortDirection.Ascending
                    ? query.OrderBy(key, Comparer<object>.Create(this.SafeCompare))
                    : query.OrderByDescending(key, Comparer<object>.Create(this.SafeCompare));
            }
            else
            {
                ordered = dir == ListSortDirection.Ascending
                    ? ordered.ThenBy(key, Comparer<object>.Create(this.SafeCompare))
                    : ordered.ThenByDescending(key, Comparer<object>.Create(this.SafeCompare));
            }
        }

        return ordered ?? query;
    }

    private Func<T, int, bool> CreateFilterPredicate(string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return null;
        }

        return RuntimeStuff.Helpers.FilterHelper.ToIndexedPredicate<T>(filter);
    }

    private void FireReset()
    {
        this.OnListChanged(new ListChangedEventArgs(ListChangedType.Reset, -1));
        this.CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        this.OnPropertyChanged(nameof(this.Count));
        this.OnPropertyChanged("Item[]");
    }

    private void RebuildView()
    {
        this.RaiseListChangedEvents = false;

        IEnumerable<T> query = this.source;

        if (this.filterFunc != null)
        {
            query = query.Where(this.SafeFilter);
        }

        if (this.sortProperties.Count > 0)
        {
            query = this.ApplyOrdering(query);
        }

        var result = query.ToList();

        base.ClearItems();

        for (var i = 0; i < result.Count; i++)
        {
            base.InsertItem(i, result[i]);
        }

        this.RaiseListChangedEvents = true;

        this.FireReset();
    }

    private int SafeCompare(object x, object y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x == null)
        {
            return -1;
        }

        if (y == null)
        {
            return 1;
        }

        if (x is IComparable c)
        {
            return c.CompareTo(y);
        }

        return string.Compare(x.ToString(), y.ToString(), StringComparison.Ordinal);
    }

    private bool SafeFilter(T item, int index)
    {
        try
        {
            return this.filterFunc(item, index);
        }
        catch
        {
            return false;
        }
    }
}