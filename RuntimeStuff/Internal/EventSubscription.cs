// <copyright file="EventSubscription.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace RuntimeStuff.Internal
{
    using System;
    using System.Reflection;
    using RuntimeStuff.Helpers;

    /// <summary>
    /// Представляет подписку на событие с возможностью последующего
    /// корректного освобождения ресурсов.
    /// </summary>
    /// <remarks>
    /// Класс инкапсулирует информацию об источнике события, целевом объекте
    /// и обработчике, обеспечивая безопасное добавление и удаление подписки.
    /// Используется как вспомогательный объект для управления жизненным циклом
    /// обработчиков событий.
    /// </remarks>
    internal sealed class EventSubscription : IDisposable
    {
        private readonly EventInfo eventInfo;
        private readonly object source;
        private readonly Delegate handler;
        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventSubscription"/> class.
        /// Инициализирует новую подписку на событие указанного источника.
        /// </summary>
        /// <param name="source">
        /// Объект-источник события.
        /// </param>
        /// <param name="eventInfo">
        /// Метаданные события, на которое выполняется подписка.
        /// </param>
        /// <param name="target">
        /// Целевой объект, передаваемый в обратный вызов.
        /// </param>
        /// <param name="callback">
        /// Делегат, вызываемый при возникновении события.
        /// Получает целевой объект, отправителя и аргументы события.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Выбрасывается, если <paramref name="source"/>,
        /// <paramref name="eventInfo"/>,
        /// <paramref name="target"/> или <paramref name="callback"/> равны <c>null</c>.
        /// </exception>
        public EventSubscription(
            object source,
            EventInfo eventInfo,
            object target,
            Action<object, object, object> callback)
        {
            this.source = source;
            this.eventInfo = eventInfo;

            handler = EventHelper.CreateEventHandlerDelegate<object, object>(
                eventInfo.EventHandlerType,
                (sender, args) =>
                {
                    callback(target, sender, args);
                });

            eventInfo.AddEventHandler(source, handler);
        }

        /// <summary>
        /// Отписывается от события и освобождает связанные ресурсы.
        /// </summary>
        /// <remarks>
        /// Метод является идемпотентным и может вызываться многократно
        /// без побочных эффектов.
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