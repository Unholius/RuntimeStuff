// ***********************************************************************
// Assembly         : RuntimeStuff
// Author           : RS
// Created          : 01-06-2026
//
// Last Modified By : RS
// Last Modified On : 01-07-2026
// ***********************************************************************
// <copyright file="ObservableObjectEx.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************

namespace RuntimeStuff
{
    using System;
    using System.Collections.Concurrent;
    using System.ComponentModel;
    using System.Runtime.CompilerServices;
    using RuntimeStuff.Extensions;
    using RuntimeStuff.Internal;

    /// <summary>
    /// Базовый класс, предоставляющий реализацию интерфейсов <see cref="INotifyPropertyChanged" />, <see cref="INotifyPropertyChanging" /> и
    /// вспомогательные методы для уведомления об изменении свойств, а также автоматического управления подписками на
    /// изменения во вложенных объектах.
    /// </summary>
    /// <remarks>Класс предназначен для упрощения реализации паттерна "наблюдатель" в моделях данных, поддерживающих
    /// привязку свойств (например, в MVVM). Реализует автоматическую отписку и повторную подписку на события <see cref="PropertyChanged" /> у вложенных объектов, что предотвращает утечки памяти и облегчает управление зависимостями
    /// между объектами. Является потокобезопасным для операций подписки и отписки. Для корректного освобождения ресурсов
    /// рекомендуется явно вызывать <see cref="Dispose()" /> при уничтожении экземпляра.
    /// <code>
    /// public event PropertyChangedEventHandler PropertyChanged
    /// {
    /// add =&gt; _notifier.PropertyChanged += value;
    /// remove =&gt; _notifier.PropertyChanged -= value;
    /// }
    /// </code></remarks>
    public abstract class ObservableObjectEx : INotifyPropertyChanged, INotifyPropertyChanging, IDisposable
    {
        /// <summary>
        /// Сопоставление вложенных объектов (<see cref="INotifyPropertyChanged" />) с их обработчиками
        /// <see cref="PropertyChangedEventHandler" />. Используется для автоматической отписки
        /// при замене вложенного объекта.
        /// </summary>
        private readonly ConcurrentDictionary<object, EventHandlers> subscriptions = new ConcurrentDictionary<object, EventHandlers>();
        private readonly object syncRoot = new object();
        private readonly ConcurrentDictionary<string, object> values = new ConcurrentDictionary<string, object>();
        private bool disposed;

        /// <summary>
        /// Finalizes an instance of the <see cref="ObservableObjectEx"/> class.
        /// Освобождает ресурсы, используемые экземпляром класса PropertyChangeNotifier перед его удалением сборщиком
        /// мусора.
        /// </summary>
        /// <remarks>Этот финализатор вызывается автоматически при удалении объекта сборщиком мусора, если
        /// метод Dispose не был вызван явно. Обычно не требуется вызывать этот метод напрямую в пользовательском
        /// коде.</remarks>
        ~ObservableObjectEx()
        {
            this.Dispose(false);
        }

        /// <summary>
        /// Событие <see cref="PropertyChanged" /> для внешних подписчиков.
        /// </summary>
        /// <returns></returns>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Событие <see cref="PropertyChanging" /> для внешних подписчиков.
        /// </summary>
        /// <returns></returns>
        public event PropertyChangingEventHandler PropertyChanging;

        /// <summary>
        /// Выполняет подписку на изменение указанного свойства вложенного объекта и обеспечивает
        /// автоматическую отписку от старого объекта (если он был). Вызывать до присвоения нового значения
        /// в backing field.
        /// </summary>
        /// <typeparam name="T">Тип вложенного объекта, реализующий <see cref="INotifyPropertyChanged" />.</typeparam>
        /// <param name="oldValue">Ссылка на текущее (старое) значение свойства (backing field).</param>
        /// <param name="newValue">Новое значение вложенного объекта, для которого нужно установить подписку.</param>
        /// <param name="childPropertyName">Имя свойства во вложенном объекте, изменение которого должно вызывать <paramref name="childPropertyChangeHandler" />.</param>
        /// <param name="childPropertyChangeHandler">Действие, выполняемое при изменении <paramref name="childPropertyName" /> во вложенном объекте.</param>
        /// <remarks>Этот метод отписывает обработчик у старого объекта (если он присутствует) и подписывает новый обработчик
        /// к <paramref name="newValue" />; обработчики хранятся в словаре <see cref="subscriptions" /> для корректной последующей отписки.</remarks>
        public void BindPropertyChange<T>(ref T oldValue, T newValue, string childPropertyName, Action childPropertyChangeHandler)
            where T : class, INotifyPropertyChanged
        {
            lock (this.syncRoot)
            {
                // Отписываем старый объект, если он был
                if (oldValue != null && this.subscriptions.TryGetValue(oldValue, out var oldHandler))
                {
                    oldValue.PropertyChanged -= oldHandler.Changed;
                    this.subscriptions.TryRemove(oldValue, out _);
                }

                if (newValue != null && childPropertyChangeHandler != null)
                {
                    var newPropertyChangedEventHandler = (PropertyChangedEventHandler)((sender, args) =>
                    {
                        if (childPropertyName == null || args.PropertyName == childPropertyName)
                        {
                            childPropertyChangeHandler();
                        }
                    });

                    newValue.PropertyChanged += newPropertyChangedEventHandler;
                    var s = this.subscriptions.GetOrAdd(newValue, x => new EventHandlers());
                    s.Changed = newPropertyChangedEventHandler;

                    if (typeof(T).IsImplements<INotifyPropertyChanging>())
                    {
                        var newPropertyChangingEventHandler = (PropertyChangingEventHandler)((sender, args) =>
                        {
                            if (childPropertyName == null || args.PropertyName == childPropertyName)
                            {
                                this.OnPropertyChanging(childPropertyName);
                            }
                        });
                        ((INotifyPropertyChanging)newValue).PropertyChanging += newPropertyChangingEventHandler;
                        s.Changing = newPropertyChangingEventHandler;
                    }
                }
            }
        }

        /// <summary>
        /// Упрощённая перегрузка <see cref="BindPropertyChange{T}(ref T, T, string, Action)" /> для подписки
        /// на любые изменения у вложенного объекта (без фильтрации по имени свойства).
        /// </summary>
        /// <typeparam name="T">Тип вложенного объекта, реализующий <see cref="INotifyPropertyChanged" />.</typeparam>
        /// <param name="oldValue">Ссылка на текущее (старое) значение свойства (backing field).</param>
        /// <param name="newValue">Новое значение вложенного объекта, для которого нужно установить подписку.</param>
        /// <param name="handler">Действие, выполняемое при любом изменении во вложенном объекте.</param>
        public void BindPropertyChange<T>(ref T oldValue, T newValue, Action handler)
            where T : class, INotifyPropertyChanged => this.BindPropertyChange(ref oldValue, newValue, null, handler);

        /// <summary>
        /// Освобождает ресурсы, вызывает Dispose(bool) и подавляет финализацию.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Возвращает значение свойства по его имени, либо значение по умолчанию,
        /// если свойство ещё не было инициализировано.
        /// </summary>
        /// <typeparam name="T">
        /// Тип значения свойства.
        /// </typeparam>
        /// <param name="propertyName">
        /// Имя свойства. Если не указано явно, подставляется имя вызывающего члена
        /// с использованием атрибута <see cref="CallerMemberNameAttribute"/>.
        /// </param>
        /// <returns>
        /// Текущее значение свойства либо значение по умолчанию для типа <typeparamref name="T"/>,
        /// если значение отсутствует.
        /// </returns>
        /// <remarks>
        /// Метод предназначен для использования в реализациях шаблона хранения значений свойств
        /// (например, в базовых классах моделей представления).
        /// Значения кэшируются во внутреннем хранилище и инициализируются лениво.
        /// </remarks>
        public T Get<T>([CallerMemberName] string propertyName = null)
        {
            return (T)values.GetOrAdd(propertyName, x => default(T));
        }

        /// <summary>
        /// Возвращает значение свойства по его имени, либо значение по умолчанию,
        /// если свойство ещё не было инициализировано.
        /// </summary>
        /// <param name="propertyName">
        /// Имя свойства. Если не указано явно, подставляется имя вызывающего члена
        /// с использованием атрибута <see cref="CallerMemberNameAttribute"/>.
        /// </param>
        /// <returns>
        /// Текущее значение свойства либо null,
        /// если значение отсутствует.
        /// </returns>
        /// <remarks>
        /// Метод предназначен для использования в реализациях шаблона хранения значений свойств
        /// (например, в базовых классах моделей представления).
        /// Значения кэшируются во внутреннем хранилище и инициализируются лениво.
        /// </remarks>
        public object Get([CallerMemberName] string propertyName = null)
        {
            return values.GetOrAdd(propertyName, x => null);
        }

        /// <summary>
        /// Вызывает событие <see cref="PropertyChanged" /> для указанного свойства.
        /// </summary>
        /// <param name="propertyName">Имя свойства. Если не задано, используется имя вызывающего члена.</param>
        /// <exception cref="ArgumentNullException">Если <paramref name="propertyName" /> равен <c>null</c>.</exception>
        public virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        /// <summary>
        /// Вызывает событие <see cref="PropertyChanging" /> для указанного свойства.
        /// </summary>
        /// <param name="propertyName">Имя свойства. Если не задано, используется имя вызывающего члена.</param>
        /// <exception cref="ArgumentNullException">Если <paramref name="propertyName" /> равен <c>null</c>.</exception>
        public virtual void OnPropertyChanging([CallerMemberName] string propertyName = null) => this.PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(propertyName));

        /// <summary>
        /// Комбинированная операция: сначала выполняет привязку обработчика изменения свойства у вложенного объекта,
        /// затем устанавливает новое значение для свойства текущего объекта. Если значение изменилось и
        /// <paramref name="thisPropertyChangeHandler" /> не равен <c>null</c>, он будет вызван.
        /// </summary>
        /// <typeparam name="T">Тип вложенного объекта, реализующего <see cref="INotifyPropertyChanged" />.</typeparam>
        /// <param name="oldValue">Ссылка на текущее (старое) значение свойства (backing field).</param>
        /// <param name="newValue">Новое значение, которое будет присвоено.</param>
        /// <param name="childPropertyName">Имя свойства вложенного объекта, за изменением которого нужно следить.</param>
        /// <param name="childPropertyChangeHandler">Действие, вызываемое при изменении <paramref name="childPropertyName" /> у вложенного объекта.</param>
        /// <param name="thisPropertyChangeHandler">Действие, вызываемое после успешного изменения свойства текущего объекта.</param>
        /// <param name="propertyName">Имя свойства текущего объекта. Если не задано, используется имя вызывающего члена.</param>
        public void SetAndBindPropertyChange<T>(ref T oldValue, T newValue, string childPropertyName, Action childPropertyChangeHandler, Action<string> thisPropertyChangeHandler = null, [CallerMemberName] string propertyName = null)
            where T : class, INotifyPropertyChanged
        {
            this.BindPropertyChange(ref oldValue, newValue, childPropertyName, childPropertyChangeHandler);
            if (this.Set(ref oldValue, newValue, (Action)null, propertyName))
            {
                thisPropertyChangeHandler?.Invoke(propertyName);
            }
        }

        /// <summary>
        /// Устанавливает значение backing field и вызывает уведомление об изменении свойства,
        /// если значение фактически изменилось.
        /// </summary>
        /// <typeparam name="T">Тип свойства.</typeparam>
        /// <param name="field">Ссылка на backing field свойства.</param>
        /// <param name="value">Новое значение для поля.</param>
        /// <param name="onChanged">Действие после изменения свойства.</param>
        /// <param name="propertyName">Имя свойства. Если не задано, используется имя вызывающего члена.</param>
        /// <returns><c>true</c>, если значение было изменено и было вызвано событие; иначе <c>false</c>.</returns>
        public bool Set<T>(ref T field, T value, Action onChanged = null, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value))
            {
                return false;
            }

            this.OnPropertyChanging(propertyName);
            field = value;
            onChanged?.Invoke();
            this.OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// Устанавливает значение backing field и вызывает уведомление об изменении свойства,
        /// если значение фактически изменилось.
        /// </summary>
        /// <typeparam name="T">Тип свойства.</typeparam>
        /// <param name="field">Ссылка на backing field свойства.</param>
        /// <param name="value">Новое значение для поля.</param>
        /// <param name="onChanged">Действие после изменения свойства.</param>
        /// <param name="propertyName">Имя свойства. Если не задано, используется имя вызывающего члена.</param>
        /// <returns><c>true</c>, если значение было изменено и было вызвано событие; иначе <c>false</c>.</returns>
        public bool Set<T>(ref T field, T value, Action<T> onChanged, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value))
            {
                return false;
            }

            this.OnPropertyChanging(propertyName);
            field = value;
            onChanged?.Invoke(value);
            this.OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// Устанавливает значение свойства и уведомляет подписчиков
        /// об изменении свойства.
        /// </summary>
        /// <typeparam name="T">
        /// Тип значения свойства.
        /// </typeparam>
        /// <param name="value">
        /// Новое значение свойства.
        /// </param>
        /// <param name="propertyName">
        /// Имя свойства. Если не указано явно, подставляется имя вызывающего члена
        /// с использованием атрибута <see cref="CallerMemberNameAttribute"/>.
        /// </param>
        /// <remarks>
        /// Метод сохраняет значение во внутреннем хранилище и инициирует вызов
        /// <c>OnPropertyChanged</c> для указанного свойства.
        /// Предназначен для использования в реализациях шаблона
        /// <see cref="INotifyPropertyChanged"/>.
        /// </remarks>
        public void Set<T>(T value, [CallerMemberName] string propertyName = null)
        {
            values[propertyName] = value;
            OnPropertyChanged(propertyName);
        }

        /// <summary>
        /// Освобождает управляемые и неуправляемые ресурсы.
        /// </summary>
        /// <param name="disposing">True, если вызвано из Dispose(), false — если из финализатора.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (disposing)
            {
                // Очистка управляемых ресурсов
                lock (this.syncRoot)
                {
                    foreach (var kvp in this.subscriptions)
                    {
                        if (kvp.Key is INotifyPropertyChanged npc1)
                        {
                            npc1.PropertyChanged -= kvp.Value.Changed;
                        }

                        if (kvp.Key is INotifyPropertyChanging npc2)
                        {
                            npc2.PropertyChanging -= kvp.Value.Changing;
                        }
                    }

                    this.subscriptions.Clear();
                }
            }

            this.disposed = true;
        }
    }
}