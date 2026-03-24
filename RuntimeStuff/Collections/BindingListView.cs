// <copyright file="BindingListView.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

public class BindingListView<T> : BindingList<T>, IBindingListView, INotifyCollectionChanged, INotifyPropertyChanged
{
    private readonly List<ListSortDirection> _sortDirections = new List<ListSortDirection>();
    private readonly List<PropertyDescriptor> _sortProperties = new List<PropertyDescriptor>();
    private readonly List<T> _source = new List<T>();

    private string _filter;
    private Func<T, int, bool> _filterFunc;
    private bool _isSorted;
    private ListSortDirection _sortDirection;
    private PropertyDescriptor _sortProperty;

    public BindingListView()
    {
    }

    public BindingListView(IEnumerable<T> collection)
    {
        this._source.AddRange(collection);
        this.RebuildView();
    }

    public event NotifyCollectionChangedEventHandler CollectionChanged;

    public new event PropertyChangedEventHandler PropertyChanged;

    public string Filter
    {
        get => this._filter;
        set
        {
            if (this._filter == value)
            {
                return;
            }

            this._filter = value;
            this._filterFunc = this.CreateFilterPredicate(value);

            this.RebuildView();
            this.OnPropertyChanged();
        }
    }

    public ListSortDescriptionCollection SortDescriptions
    {
        get
        {
            var arr = this._sortProperties
                .Select((p, i) => new ListSortDescription(p, this._sortDirections[i]))
                .ToArray();

            return new ListSortDescriptionCollection(arr);
        }
    }

    public bool SupportsAdvancedSorting => true;

    public bool SupportsFiltering => true;

    protected override bool IsSortedCore => this._isSorted;

    protected override ListSortDirection SortDirectionCore => this._sortDirection;

    protected override PropertyDescriptor SortPropertyCore => this._sortProperty;

    protected override bool SupportsSortingCore => true;

    public void AddRange(IEnumerable<T> items)
    {
        if (items == null)
        {
            return;
        }

        this.RaiseListChangedEvents = false;

        this._source.AddRange(items);

        this.RaiseListChangedEvents = true;

        this.RebuildView();
    }

    public void ApplySort(ListSortDescriptionCollection sorts)
    {
        this._sortProperties.Clear();
        this._sortDirections.Clear();

        if (sorts != null)
        {
            foreach (ListSortDescription s in sorts)
            {
                this._sortProperties.Add(s.PropertyDescriptor);
                this._sortDirections.Add(s.SortDirection);
            }
        }

        this._isSorted = this._sortProperties.Any();
        this.RebuildView();
    }

    public void RemoveFilter()
    {
        this.Filter = null;
    }

    public void SetFilter(Func<T, int, bool> predicate)
    {
        this._filterFunc = predicate;
        this._filter = predicate?.ToString();
        this.RebuildView();
    }

    protected override void ApplySortCore(PropertyDescriptor prop, ListSortDirection direction)
    {
        this._sortProperties.Clear();
        this._sortDirections.Clear();

        if (prop != null)
        {
            this._sortProperties.Add(prop);
            this._sortDirections.Add(direction);
        }

        this._sortProperty = prop;
        this._sortDirection = direction;
        this._isSorted = prop != null;

        this.RebuildView();
    }

    protected override void ClearItems()
    {
        this._source.Clear();
        this.RebuildView();
    }

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

    protected override void InsertItem(int index, T item)
    {
        this._source.Add(item);
        this.RebuildView();
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string name = null)
    {
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    protected override void RemoveItem(int index)
    {
        if (index < 0 || index >= this.Count)
        {
            return;
        }

        var item = this[index];
        this._source.Remove(item);

        this.RebuildView();
    }

    protected override void RemoveSortCore()
    {
        this._sortProperties.Clear();
        this._sortDirections.Clear();

        this._sortProperty = null;
        this._isSorted = false;

        this.RebuildView();
    }

    protected override void SetItem(int index, T item)
    {
        if (index < 0 || index >= this.Count)
        {
            return;
        }

        var oldItem = this[index];
        int srcIndex = this._source.IndexOf(oldItem);

        if (srcIndex >= 0)
        {
            this._source[srcIndex] = item;
        }

        this.RebuildView();
    }

    private IEnumerable<T> ApplyOrdering(IEnumerable<T> query)
    {
        IOrderedEnumerable<T> ordered = null;

        for (int i = 0; i < this._sortProperties.Count; i++)
        {
            var prop = this._sortProperties[i];
            var dir = this._sortDirections[i];

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

        IEnumerable<T> query = this._source;

        if (this._filterFunc != null)
        {
            query = query.Where(this.SafeFilter);
        }

        if (this._sortProperties.Count > 0)
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
            return this._filterFunc(item, index);
        }
        catch
        {
            return false;
        }
    }
}