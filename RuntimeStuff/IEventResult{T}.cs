// <copyright file="IEventResult{T}.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>
namespace RuntimeStuff
{
    using System;

    /// <summary>
    /// Представляет результат обработки события с типизированным статусом.
    /// </summary>
    /// <typeparam name="T">
    /// Тип статуса события.
    /// </typeparam>
    public interface IEventResult<out T>
    {
        /// <summary>
        /// Уникальный идентификатор события.
        /// </summary>
        /// <remarks>
        /// Может быть любым объектом, однозначно идентифицирующим событие
        /// (например, <see cref="Guid"/>, строка или числовое значение).
        /// </remarks>
        object EventId { get; }

        /// <summary>
        /// Статус события.
        /// </summary>
        /// <remarks>
        /// Определяет результат или текущее состояние обработки события.
        /// Тип статуса задаётся параметром <typeparamref name="T"/>.
        /// </remarks>
        T Status { get; }

        /// <summary>
        /// Дополнительные данные, связанные с событием.
        /// </summary>
        /// <remarks>
        /// Может содержать произвольную полезную нагрузку,
        /// относящуюся к результату события.
        /// </remarks>
        object Data { get; set; }
    }
}