// <copyright file="ObservableCollectionEx.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace System.Collections
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
        private readonly WeakEventManager weakEventManager = new WeakEventManager();

        /// <summary>
        /// Initializes a new instance of the <see cref="ObservableCollectionEx{T}"/> class.
        /// Создаёт пустую коллекцию.
        /// </summary>
        public ObservableCollectionEx()
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
            this.SubscribeAll(this);
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
            {
                throw new ArgumentNullException(nameof(items));
            }

            var list = items as IList<T> ?? items.ToList();
            if (list.Count == 0)
            {
                return;
            }

            var oldSuppress = this.SuppressNotifyCollectionChange;
            this.SuppressNotifyCollectionChange = true;

            try
            {
                foreach (var item in list)
                {
                    this.Items.Add(item);
                    this.Subscribe(item);
                }
            }
            finally
            {
                this.SuppressNotifyCollectionChange = oldSuppress;
            }

            this.RaiseReset();
        }

        /// <summary>
        /// Генерирует единое событие CollectionChanged и уведомления о свойствах,
        /// вызываемое после массового добавления или удаления элементов.
        /// </summary>
        public void NotifyCollectionChanged()
        {
            this.RaiseReset();
        }

        /// <summary>
        /// Удаление элемента с отпиской от <see cref="INotifyPropertyChanged"/>.
        /// </summary>
        /// <param name="item">Элемент для удаления.</param>
        /// <returns>Результат удаления.</returns>
        public new bool Remove(T item)
        {
            this.Unsubscribe(item);
            return base.Remove(item);
        }

        /// <summary>
        /// Удаление элемента из позиции с отпиской от <see cref="INotifyPropertyChanged"/>.
        /// </summary>
        /// <param name="index">Индекс элемента в списке.</param>
        public new void RemoveAt(int index)
        {
            this.Unsubscribe(this[index]);
            base.RemoveAt(index);
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
            {
                throw new ArgumentNullException(nameof(items));
            }

            var list = items as IList<T> ?? items.ToList();
            if (list.Count == 0)
            {
                return;
            }

            var oldSuppress = this.SuppressNotifyCollectionChange;
            this.SuppressNotifyCollectionChange = true;
            var removed = false;
            try
            {
                for (var i = this.Items.Count - 1; i >= 0; i--)
                {
                    var item = this.Items[i];
                    if (!list.Contains(item))
                    {
                        continue;
                    }

                    this.Unsubscribe(item);
                    this.Items.RemoveAt(i);
                    removed = true;
                }
            }
            finally
            {
                this.SuppressNotifyCollectionChange = oldSuppress;
                if (removed)
                {
                    this.RaiseReset();
                }
            }
        }

        /// <summary>
        /// Удаляет из коллекции все элементы, удовлетворяющие заданному условию.
        /// </summary>
        /// <param name="filter">
        /// Предикат, определяющий, какие элементы должны быть удалены из коллекции.
        /// Элемент удаляется, если функция возвращает <c>true</c>.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Выбрасывается, если параметр <paramref name="filter"/> равен <c>null</c>.
        /// </exception>
        /// <remarks>
        /// Метод выполняет удаление элементов в обратном порядке, чтобы избежать
        /// проблем с изменением индексов во время итерации.
        /// На время выполнения операции уведомления об изменениях коллекции
        /// подавляются, после чего вызывается единое уведомление о сбросе состояния
        /// коллекции (<see cref="RaiseReset"/>).
        /// </remarks>
        /// <returns>Возвращает список удаленных элементов.</returns>
        public IEnumerable<T> RemoveRange(Func<T, bool> filter)
        {
            if (filter == null)
            {
                throw new ArgumentNullException(nameof(filter));
            }

            var removedList = new List<T>();

            var oldSuppress = this.SuppressNotifyCollectionChange;
            this.SuppressNotifyCollectionChange = true;

            var removed = false;

            try
            {
                for (var i = this.Items.Count - 1; i >= 0; i--)
                {
                    var item = this.Items[i];
                    if (!filter(item))
                    {
                        continue;
                    }

                    this.Unsubscribe(item);
                    this.Items.RemoveAt(i);
                    removed = true;
                    removedList.Add(item);
                }
            }
            finally
            {
                this.SuppressNotifyCollectionChange = oldSuppress;
                if (removed)
                {
                    this.RaiseReset();
                }
            }

            return removedList;
        }

        /// <summary>
        /// <inheritdoc cref="ClearItems" />
        /// </summary>
        public new void Clear()
        {
            this.ClearItems();
        }

        /// <summary>
        /// Удаляет все элементы из коллекции,
        /// предварительно корректно освобождая связанные ресурсы.
        /// </summary>
        /// <remarks>
        /// Перед очисткой коллекции метод отписывается от событий всех элементов.
        /// После этого очищаются все слабые подписчики событий,
        /// и затем выполняется базовая очистка коллекции.
        /// Такой порядок предотвращает утечки памяти и некорректные уведомления.
        /// </remarks>
        protected override void ClearItems()
        {
            var oldSuppress = this.SuppressNotifyCollectionChange;
            this.SuppressNotifyCollectionChange = true;

            try
            {
                foreach (var item in this)
                {
                    this.Unsubscribe(item);
                }

                this.weakEventManager.ClearWeakEventListeners();
                base.ClearItems();
            }
            finally
            {
                this.SuppressNotifyCollectionChange = oldSuppress;
                this.RaiseReset();
            }
        }

        /// <summary>
        /// Вставка элемента с подпиской <see cref="INotifyPropertyChanged"/>.
        /// </summary>
        /// <param name="index">Индекс вставки.</param>
        /// <param name="item">Новый элемент.</param>
        protected new void Insert(int index, T item)
        {
            this.InsertItem(index, item);
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
            this.Subscribe(item);
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
            if (!this.SuppressNotifyCollectionChange)
            {
                base.OnCollectionChanged(e);
            }
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
            if (!this.SuppressNotifyCollectionChange)
            {
                base.OnPropertyChanged(e);
            }
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
            this.Unsubscribe(this[index]);
            base.RemoveItem(index);
        }

        /// <summary>
        /// Заменяет элемент коллекции по указанному индексу,
        /// корректно управляя подписками на события элементов.
        /// </summary>
        /// <param name="index">
        /// Индекс элемента, который требуется заменить.
        /// </param>
        /// <param name="item">
        /// Новый элемент, устанавливаемый в коллекцию.
        /// </param>
        /// <remarks>
        /// Перед заменой метод отписывается от событий старого элемента,
        /// после чего выполняет замену и подписывается на события нового элемента.
        /// Это обеспечивает корректную работу коллекции и предотвращает утечки памяти.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Выбрасывается, если <paramref name="index"/> выходит за пределы коллекции.
        /// </exception>
        protected override void SetItem(int index, T item)
        {
            var oldItem = this[index];
            this.Unsubscribe(oldItem);
            base.SetItem(index, item);
            this.Subscribe(item);
        }

        private static void OnItemPropertyChanged(ObservableCollectionEx<T> collection)
        {
            if (collection.SuppressNotifyCollectionChange)
            {
                return;
            }

            collection.OnCollectionChanged(
                new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Reset));
        }

        /// <summary>
        /// Генерирует единое событие CollectionChanged и уведомления о свойствах,
        /// вызываемое после массового добавления или удаления элементов.
        /// </summary>
        private void RaiseReset()
        {
            this.OnPropertyChanged(new PropertyChangedEventArgs(nameof(this.Count)));
            this.OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        /// <summary>
        /// Подписывается на событие <see cref="INotifyPropertyChanged.PropertyChanged"/> элемента.
        /// </summary>
        private void Subscribe(T item)
        {
            if (item is not INotifyPropertyChanged inpc)
            {
                return;
            }

            this.weakEventManager.AddWeakEventListener(inpc, (s, e) => OnItemPropertyChanged(this));
        }

        /// <summary>
        /// Подписывает все элементы коллекции на событие <see cref="INotifyPropertyChanged.PropertyChanged"/>.
        /// </summary>
        private void SubscribeAll(IEnumerable<T> items)
        {
            foreach (var item in items)
            {
                this.Subscribe(item);
            }
        }

        /// <summary>
        /// Снимает подписку с события <see cref="INotifyPropertyChanged.PropertyChanged"/> элемента.
        /// </summary>
        private void Unsubscribe(T item)
        {
            if (item is INotifyPropertyChanged inpc)
            {
                this.weakEventManager.RemoveWeakEventListener(inpc);
            }
        }
    }
}