// ***********************************************************************
// Assembly         : RuntimeStuff
// Author           : RS
// Created          : 01-06-2026
//
// Last Modified By : RS
// Last Modified On : 01-07-2026
// ***********************************************************************
// <copyright file="TaskHelper.cs" company="Rudnev Sergey">
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************
namespace RuntimeStuff.Helpers
{
    using System;

    /// <summary>
    /// Информация о произошедшем событии.
    /// </summary>
    /// <typeparam name="T">Type.</typeparam>
    public sealed class EventResult<T>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EventResult{T}"/> class.
        /// Создаёт новый экземпляр информации о событии.
        /// </summary>
        /// <param name="eventId">Идентификатор события.</param>
        /// <param name="status">Статус события.</param>
        /// <param name="data">Произвольные данные события.</param>
        /// <exception cref="System.NullReferenceException">eventId.</exception>
        public EventResult(object eventId, T status, object data = null)
        {
            this.EventId = eventId ?? throw new NullReferenceException(nameof(eventId));
            this.Status = status;
            this.Data = data;
        }

        /// <summary>
        /// Gets идентификатор события.
        /// </summary>
        /// <value>The event identifier.</value>
        public object EventId { get; }

        /// <summary>
        /// Gets статус события.
        /// </summary>
        /// <value>The status.</value>
        public T Status { get; }

        /// <summary>
        /// Gets or sets дополнительные данные, связанные с событием.
        /// </summary>
        /// <value>The data.</value>
        public object Data { get; set; }
    }
}