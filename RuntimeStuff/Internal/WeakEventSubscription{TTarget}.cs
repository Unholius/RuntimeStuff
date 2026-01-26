// <copyright file="WeakEventSubscription{TTarget}.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace RuntimeStuff.Internal
{
    using System;
    using System.Reflection;
    using RuntimeStuff.Helpers;

    /// <summary>
    /// Реализует слабую подписку на событие, предотвращающую удержание объекта-цели
    /// в памяти за счёт использования <see cref="WeakReference{T}"/>.
    /// </summary>
    /// <typeparam name="TTarget">
    /// Тип объекта-получателя события.
    /// </typeparam>
    /// <remarks>
    /// Класс предназначен для сценариев, в которых необходимо подписаться на событие
    /// без создания сильной ссылки на объект-цель, что позволяет избежать утечек памяти.
    /// При сборке мусора объекта-цели подписка автоматически удаляется.
    /// </remarks>
    internal sealed class WeakEventSubscription<TTarget> : IDisposable
        where TTarget : class
    {
        private readonly WeakReference<TTarget> targetRef;
        private readonly EventInfo eventInfo;
        private readonly object source;
        private readonly Delegate handler;
        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="WeakEventSubscription{TTarget}"/> class.
        /// Инициализирует новый экземпляр класса WeakEventSubscription{TTarget}
        /// и выполняет подписку на указанное событие.
        /// </summary>
        /// <param name="source">
        /// Объект-источник события.
        /// </param>
        /// <param name="eventInfo">
        /// Метаданные события, на которое выполняется подписка.
        /// </param>
        /// <param name="target">
        /// Объект-цель, который будет получать уведомления о событии.
        /// Хранится в виде слабой ссылки.
        /// </param>
        /// <param name="callback">
        /// Действие, вызываемое при срабатывании события.
        /// Получает объект-цель, источник события и аргументы события.
        /// </param>
        /// <remarks>
        /// Внутри создаётся делегат обработчика события, который проверяет,
        /// доступен ли объект-цель. Если объект был собран сборщиком мусора,
        /// подписка автоматически удаляется.
        /// </remarks>
        public WeakEventSubscription(
            object source,
            EventInfo eventInfo,
            TTarget target,
            Action<TTarget, object, object> callback)
        {
            this.source = source;
            this.eventInfo = eventInfo;
            targetRef = new WeakReference<TTarget>(target);

            handler = EventHelper.CreateEventHandlerDelegate<object, object>(
                eventInfo.EventHandlerType,
                (sender, args) =>
                {
                    if (!targetRef.TryGetTarget(out var targetInstance))
                    {
                        Dispose();
                        return;
                    }

                    callback(targetInstance, sender, args);
                });

            eventInfo.AddEventHandler(source, handler);
        }

        /// <summary>
        /// Отписывается от события и освобождает ресурсы,
        /// связанные со слабой подпиской.
        /// </summary>
        /// <remarks>
        /// Метод безопасен для повторного вызова.
        /// После выполнения подписка на событие удаляется,
        /// и дальнейшие уведомления не обрабатываются.
        /// </remarks>
        public void Dispose()
        {
            if (disposed)
                return;

            eventInfo.RemoveEventHandler(source, handler);
            disposed = true;
        }
    }
}