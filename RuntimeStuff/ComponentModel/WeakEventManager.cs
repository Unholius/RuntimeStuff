// <copyright file="WeakEventManager.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>
namespace System.ComponentModel
{
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Internal;

    /// <summary>
    /// Управляет подписками на события с использованием слабых ссылок,
    /// предотвращая утечки памяти из-за неосвобожденных обработчиков событий.
    /// </summary>
    public class WeakEventManager
    {
        private readonly Dictionary<IWeakEventListener, Delegate> listeners = new();

        /// <summary>
        /// Добавляет слабую подписку на событие по имени события.
        /// </summary>
        /// <typeparam name="T">Тип источника события.</typeparam>
        /// <typeparam name="TArgs">Тип аргументов события.</typeparam>
        /// <param name="source">Источник события.</param>
        /// <param name="eventName">Имя события.</param>
        /// <param name="handler">Обработчик события.</param>
        public void AddWeakEventListener<T, TArgs>(T source, string eventName, Action<T, TArgs> handler)
            where T : class
            where TArgs : EventArgs
        {
            this.listeners.Add(new WeakEventListener<T, TArgs>(source, eventName, handler), handler);
        }

        /// <summary>
        /// Добавляет слабую подписку на событие изменения свойства (<see cref="INotifyPropertyChanged.PropertyChanged"/>).
        /// </summary>
        /// <typeparam name="T">Тип источника события.</typeparam>
        /// <param name="source">Источник события.</param>
        /// <param name="handler">Обработчик события изменения свойства.</param>
        public void AddWeakEventListener<T>(T source, Action<T, PropertyChangedEventArgs> handler)
            where T : class, INotifyPropertyChanged
        {
            this.listeners.Add(new PropertyChangedWeakEventListener<T>(source, handler), handler);
        }

        /// <summary>
        /// Добавляет слабую подписку на событие изменения коллекции (<see cref="INotifyCollectionChanged.CollectionChanged"/>).
        /// </summary>
        /// <typeparam name="T">Тип источника события.</typeparam>
        /// <param name="source">Источник события.</param>
        /// <param name="handler">Обработчик события изменения коллекции.</param>
        public void AddWeakEventListener<T>(T source, Action<T, NotifyCollectionChangedEventArgs> handler)
            where T : class, INotifyCollectionChanged
        {
            this.listeners.Add(new CollectionChangedWeakEventListener<T>(source, handler), handler);
        }

        /// <summary>
        /// Добавляет слабую подписку на событие с явной регистрацией и отпиской обработчика.
        /// </summary>
        /// <typeparam name="T">Тип источника события.</typeparam>
        /// <typeparam name="TArgs">Тип аргументов события.</typeparam>
        /// <param name="source">Источник события.</param>
        /// <param name="register">Делегат для регистрации обработчика события.</param>
        /// <param name="unregister">Делегат для отписки обработчика события.</param>
        /// <param name="handler">Обработчик события.</param>
        public void AddWeakEventListener<T, TArgs>(T source, Action<T, EventHandler<TArgs>> register, Action<T, EventHandler<TArgs>> unregister, Action<T, TArgs> handler)
            where T : class
            where TArgs : EventArgs
        {
            this.listeners.Add(new TypedWeakEventListener<T, TArgs>(source, register, unregister, handler), handler);
        }

        /// <summary>
        /// Добавляет слабую подписку на событие с пользовательским типом делегата.
        /// </summary>
        /// <typeparam name="T">Тип источника события.</typeparam>
        /// <typeparam name="TArgs">Тип аргументов события.</typeparam>
        /// <typeparam name="THandler">Тип делегата события.</typeparam>
        /// <param name="source">Источник события.</param>
        /// <param name="register">Делегат для регистрации обработчика.</param>
        /// <param name="unregister">Делегат для отписки обработчика.</param>
        /// <param name="handler">Обработчик события.</param>
        public void AddWeakEventListener<T, TArgs, THandler>(T source, Action<T, THandler> register, Action<T, THandler> unregister, Action<T, TArgs> handler)
            where T : class
            where TArgs : EventArgs
            where THandler : Delegate
        {
            this.listeners.Add(new TypedWeakEventListener<T, TArgs, THandler>(source, register, unregister, handler), handler);
        }

        /// <summary>
        /// Удаляет все слабые подписки, связанные с указанным источником,
        /// а также очищает неактивные (собранные GC) слушатели.
        /// </summary>
        /// <typeparam name="T">Тип источника события.</typeparam>
        /// <param name="source">Источник события, для которого нужно удалить подписки.</param>
        public void RemoveWeakEventListener<T>(T source)
            where T : class
        {
            var toRemove = new List<IWeakEventListener>();
            foreach (var listener in this.listeners.Keys)
            {
                if (!listener.IsAlive)
                {
                    toRemove.Add(listener);
                }
                else if (listener.Source == source)
                {
                    listener.StopListening();
                    toRemove.Add(listener);
                }
            }

            foreach (var item in toRemove)
            {
                this.listeners.Remove(item);
            }
        }

        /// <summary>
        /// Удаляет все слабые подписки и отписывает активные обработчики событий.
        /// </summary>
        public void ClearWeakEventListeners()
        {
            foreach (var listener in this.listeners.Keys)
            {
                if (listener.IsAlive)
                {
                    listener.StopListening();
                }
            }

            this.listeners.Clear();
        }
    }
}