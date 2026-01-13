// ***********************************************************************
// Assembly         : RuntimeStuff
// Author           : RS
// Created          : 01-06-2026
//
// Last Modified By : RS
// Last Modified On : 01-07-2026
// ***********************************************************************
// <copyright file="TaskHelper{T}.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************
namespace RuntimeStuff.Helpers
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;
    using RuntimeStuff;

    /// <summary>
    /// Статический класс для асинхронного ожидания событий по идентификатору
    /// и последующего оповещения ожидающих при изменении статуса.
    /// </summary>
    /// <typeparam name="T">Type.</typeparam>
    /// <remarks>Позволяет из одного компонента ожидать событие по <see cref="string" /> id,
    /// а из другого — завершить ожидание, установив статус и данные события.</remarks>
    public static class TaskHelper<T>
    {
        /// <summary>
        /// Хранилище ожидающих задач по идентификатору события.
        /// </summary>
        private static readonly ConcurrentDictionary<object, TaskCompletionSource<EventResult<T>>> Waiters
            = new ConcurrentDictionary<object, TaskCompletionSource<EventResult<T>>>();

        /// <summary>
        /// Асинхронно ожидает событие с указанным идентификатором, пока не будет использовано <see cref="TryComplete" /> или истечет время ожидания.
        /// </summary>
        /// <param name="eventId">Уникальный идентификатор события.</param>
        /// <param name="maxMillisecondsToWait">Максимальное время ожидания в секундах.
        /// По истечении времени ожидание будет автоматически отменено.</param>
        /// <returns>Задача, завершающаяся объектом <see cref="EventResult{T}" /> при установке события.</returns>
        /// <exception cref="System.NullReferenceException">eventId.</exception>
        /// <remarks>Если ожидание по указанному идентификатору уже существует,
        /// будет возвращена существующая задача.</remarks>
        public static Task<EventResult<T>> Wait(object eventId, int maxMillisecondsToWait = 5000)
        {
            if (eventId == null)
            {
                throw new ArgumentNullException(nameof(eventId));
            }

            var tcs = new TaskCompletionSource<EventResult<T>>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            if (!Waiters.TryAdd(eventId, tcs))
            {
                return Waiters[eventId].Task;
            }

            var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(maxMillisecondsToWait));

            cts.Token.Register(() =>
            {
                if (Waiters.TryRemove(eventId, out var removed))
                {
                    removed.TrySetCanceled();
                }
            });
            cts.Dispose();
            return tcs.Task;
        }

        /// <summary>
        /// Асинхронно ожидает событие с указанным идентификатором и возвращает результат с заданным статусом таймаута,
        /// если событие не произошло в течение указанного времени.
        /// </summary>
        /// <param name="eventId">Уникальный идентификатор события, по которому ожидается уведомление.</param>
        /// <param name="timeoutStatus">Статус, который будет установлен в <see cref="EventResult{T}.Status" /> при истечении времени ожидания.</param>
        /// <param name="maxMillisecondsToWait">Максимальное время ожидания события в секундах. По истечении этого времени возвращается <paramref name="timeoutStatus" />.</param>
        /// <returns>Задача, завершающаяся объектом <see cref="EventResult{T}" /> с указанным статусом или статусом таймаута.</returns>
        /// <exception cref="System.NullReferenceException">eventId.</exception>
        /// <remarks>Если ожидание с указанным идентификатором уже существует, возвращается существующая задача.</remarks>
        public static Task<EventResult<T>> Wait(object eventId, T timeoutStatus, int maxMillisecondsToWait)
        {
            if (eventId == null)
            {
                throw new ArgumentNullException(nameof(eventId));
            }

            var tcs = new TaskCompletionSource<EventResult<T>>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            if (!Waiters.TryAdd(eventId, tcs))
            {
                return Waiters[eventId].Task;
            }

            var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(maxMillisecondsToWait));
            cts.Token.Register(() =>
            {
                if (Waiters.TryRemove(eventId, out var removed))
                {
                    // Возвращаем EventResult с заданным статусом таймаута
                    removed.TrySetResult(new EventResult<T>(eventId, timeoutStatus));
                }
            });
            cts.Dispose();
            return tcs.Task;
        }

        /// <summary>
        /// Устанавливает информацию о событии и завершает ожидание для указанного идентификатора.
        /// </summary>
        /// <param name="eventId">Идентификатор события.</param>
        /// <param name="status">Статус события.</param>
        /// <param name="eventData">Произвольные данные, связанные с событием.</param>
        /// <returns><c>true</c>, если ожидание было найдено и успешно завершено;
        /// <c>false</c>, если ожидания по данному идентификатору не существовало.</returns>
        /// <exception cref="System.NullReferenceException">eventId.</exception>
        public static bool TryComplete(object eventId, T status, object eventData = null)
        {
            if (eventId == null)
            {
                throw new ArgumentNullException(nameof(eventId));
            }

            var eventInfo = new EventResult<T>(eventId, status, eventData);

            if (Waiters.TryRemove(eventId, out var tsc))
            {
                tsc.SetResult(eventInfo);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Принудительно отменяет ожидание события по указанному идентификатору.
        /// </summary>
        /// <param name="eventId">Идентификатор события.</param>
        /// <returns><c>true</c>, если ожидание было найдено и отменено;
        /// <c>false</c>, если ожидание отсутствовало.</returns>
        /// <exception cref="System.NullReferenceException">eventId.</exception>
        public static bool CancelWait(object eventId)
        {
            if (eventId == null)
            {
                throw new ArgumentNullException(nameof(eventId));
            }

            return Waiters.TryRemove(eventId, out var tsc) && tsc.TrySetCanceled();
        }

        /// <summary>
        /// Отменяет все активные ожидания и очищает внутреннее хранилище.
        /// </summary>
        /// <remarks>Используется при завершении работы приложения или
        /// при необходимости полного сброса состояния ожиданий.</remarks>
        public static void ClearAll()
        {
            foreach (var tsc in Waiters.Values)
            {
                tsc.TrySetCanceled();
            }

            Waiters.Clear();
        }
    }
}