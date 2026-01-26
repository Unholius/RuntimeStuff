// <copyright file="ObservableCollectionEx.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace RuntimeStuff
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Linq;

    /// <summary>
    /// Расширенная коллекция <see cref="ObservableCollection{T}"/>,
    /// поддерживающая подавление уведомлений об изменении коллекции
    /// и автоматическую подписку на события <see cref="INotifyPropertyChanged"/>
    /// элементов.
    /// </summary>
    /// <typeparam name="T">Тип элементов коллекции.</typeparam>
    public class ObservableCollectionEx<T> : ObservableCollection<T>
    {
        private readonly Dictionary<INotifyPropertyChanged, PropertyChangedEventHandler> subscriptions
            = new Dictionary<INotifyPropertyChanged, PropertyChangedEventHandler>();

        /// <summary>
        /// Initializes a new instance of the <see cref="ObservableCollectionEx{T}"/> class.
        /// Создаёт пустую коллекцию.
        /// </summary>
        public ObservableCollectionEx()
            : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ObservableCollectionEx{T}"/> class.
        /// Создаёт коллекцию, инициализированную элементами из указанной последовательности,
        /// и подписывается на события <see cref="INotifyPropertyChanged"/> элементов.
        /// </summary>
        /// <param name="collection">Последовательность элементов для инициализации коллекции.</param>
        public ObservableCollectionEx(IEnumerable<T> collection)
            : base(collection)
        {
            SubscribeAll(collection);
        }

        /// <summary>
        /// Определяет, подавлять ли уведомления CollectionChanged.
        /// </summary>
        public bool SuppressNotifyCollectionChange { get; set; }

        /// <summary>
        /// Добавляет несколько элементов в коллекцию с единым уведомлением.
        /// Автоматически подписывается на события <see cref="INotifyPropertyChanged"/> новых элементов.
        /// </summary>
        /// <param name="items">Элементы для добавления.</param>
        /// <exception cref="ArgumentNullException">Выбрасывается, если <paramref name="items"/> равен <c>null</c>.</exception>
        public void AddRange(IEnumerable<T> items)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));

            var list = items as IList<T> ?? items.ToList();
            if (list.Count == 0)
                return;

            var oldSuppress = SuppressNotifyCollectionChange;
            SuppressNotifyCollectionChange = true;

            try
            {
                foreach (var item in list)
                {
                    Items.Add(item);
                    Subscribe(item);
                }
            }
            finally
            {
                SuppressNotifyCollectionChange = oldSuppress;
            }

            RaiseReset();
        }

        /// <summary>
        /// Удаляет несколько элементов из коллекции с единым уведомлением.
        /// Снимает подписку на события <see cref="INotifyPropertyChanged"/> удаляемых элементов.
        /// </summary>
        /// <param name="items">Элементы для удаления.</param>
        /// <exception cref="ArgumentNullException">Выбрасывается, если <paramref name="items"/> равен <c>null</c>.</exception>
        public void RemoveRange(IEnumerable<T> items)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));

            var list = items as IList<T> ?? items.ToList();
            if (list.Count == 0)
                return;

            var oldSuppress = SuppressNotifyCollectionChange;
            SuppressNotifyCollectionChange = true;

            try
            {
                foreach (var item in list)
                {
                    Unsubscribe(item);
                    Items.Remove(item);
                }
            }
            finally
            {
                SuppressNotifyCollectionChange = oldSuppress;
            }

            RaiseReset();
        }

        /// <summary>
        /// Очищает коллекцию и снимает подписку на события всех элементов.
        /// </summary>
        protected override void ClearItems()
        {
            foreach (var item in this)
                Unsubscribe(item);

            base.ClearItems();
        }

        /// <summary>
        /// Вставляет элемент в коллекцию по указанному индексу
        /// и выполняет подписку на его события изменения свойств.
        /// </summary>
        /// <param name="index">
        /// Индекс, по которому необходимо вставить элемент.
        /// </param>
        /// <param name="item">
        /// Элемент, добавляемый в коллекцию.
        /// </param>
        /// <remarks>
        /// После добавления элемента в коллекцию метод автоматически
        /// подписывается на событие <see cref="INotifyPropertyChanged.PropertyChanged"/>,
        /// если элемент реализует интерфейс <see cref="INotifyPropertyChanged"/>.
        /// Это позволяет реагировать на изменения свойств элементов
        /// и корректно уведомлять подписчиков коллекции.
        /// </remarks>
        protected override void InsertItem(int index, T item)
        {
            base.InsertItem(index, item);
            Subscribe(item);
        }

        /// <summary>
        /// Вызывает событие <see cref="INotifyCollectionChanged.CollectionChanged"/>
        /// при изменении коллекции, если уведомления не подавлены.
        /// </summary>
        /// <param name="e">
        /// Аргументы события, содержащие информацию о характере изменения коллекции
        /// (добавление, удаление, сброс и т.п.).
        /// </param>
        /// <remarks>
        /// Если свойство <see cref="SuppressNotifyCollectionChange"/> установлено в <c>true</c>,
        /// событие изменения коллекции не генерируется.
        /// Используется для оптимизации массовых операций над коллекцией.
        /// </remarks>
        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (!SuppressNotifyCollectionChange)
                base.OnCollectionChanged(e);
        }

        /// <summary>
        /// Вызывает событие <see cref="INotifyPropertyChanged.PropertyChanged"/>
        /// при изменении свойств коллекции, если уведомления не подавлены.
        /// </summary>
        /// <param name="e">
        /// Аргументы события, содержащие информацию об изменённом свойстве
        /// (например, <c>Count</c> или индексатор элементов).
        /// </param>
        /// <remarks>
        /// Если свойство <see cref="SuppressNotifyCollectionChange"/> установлено в <c>true</c>,
        /// уведомления об изменении свойств коллекции не генерируются.
        /// Это используется для предотвращения лишних уведомлений
        /// при массовых операциях над коллекцией.
        /// </remarks>
        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (!SuppressNotifyCollectionChange)
                base.OnPropertyChanged(e);
        }

        /// <summary>
        /// Удаляет элемент из коллекции по указанному индексу
        /// и снимает подписку на его события изменения свойств.
        /// </summary>
        /// <param name="index">
        /// Индекс элемента, который необходимо удалить.
        /// </param>
        /// <remarks>
        /// Перед фактическим удалением элемента из коллекции метод
        /// снимает подписку на событие <see cref="INotifyPropertyChanged.PropertyChanged"/>,
        /// если элемент реализует интерфейс <see cref="INotifyPropertyChanged"/>.
        /// Это предотвращает утечки памяти и лишние уведомления
        /// после удаления элемента из коллекции.
        /// </remarks>
        protected override void RemoveItem(int index)
        {
            Unsubscribe(this[index]);
            base.RemoveItem(index);
        }

        /// <summary>
        /// Генерирует единое событие CollectionChanged и уведомления о свойствах,
        /// вызываемое после массового добавления или удаления элементов.
        /// </summary>
        private void RaiseReset()
        {
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        /// <summary>
        /// Подписывается на событие <see cref="INotifyPropertyChanged.PropertyChanged"/> элемента.
        /// </summary>
        private void Subscribe(T item)
        {
            if (item is INotifyPropertyChanged inpc && !subscriptions.ContainsKey(inpc))
            {
                PropertyChangedEventHandler handler = (s, e) => { OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset)); };

                inpc.PropertyChanged += handler;
                subscriptions[inpc] = handler;
            }
        }

        /// <summary>
        /// Подписывает все элементы коллекции на событие <see cref="INotifyPropertyChanged.PropertyChanged"/>.
        /// </summary>
        private void SubscribeAll(IEnumerable<T> items)
        {
            foreach (var item in items)
            {
                Subscribe(item);
            }
        }

        /// <summary>
        /// Снимает подписку с события <see cref="INotifyPropertyChanged.PropertyChanged"/> элемента.
        /// </summary>
        private void Unsubscribe(T item)
        {
            if (item is INotifyPropertyChanged inpc && subscriptions.TryGetValue(inpc, out var handler))
            {
                inpc.PropertyChanged -= handler;
                subscriptions.Remove(inpc);
            }
        }
    }
}