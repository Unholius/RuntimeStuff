// <copyright file="SyncHelper.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>
namespace System.Helpers
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Предоставляет статические методы для синхронизации и ожидания событий с поддержкой параметров и таймаутов.
    /// </summary>
    /// <typeparam name="T">Тип перечисления, определяющий возможные статусы события.</typeparam>
    public static class SyncHelper<T>
        where T : struct, Enum
    {
        private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, object>> EventParams
            = new ConcurrentDictionary<string, ConcurrentDictionary<string, object>>();

        private static readonly ConcurrentDictionary<string, TaskCompletionSource<EventResult>> Waiters
                    = new ConcurrentDictionary<string, TaskCompletionSource<EventResult>>();

        /// <summary>
        /// Отменяет все ожидающие события, соответствующие указанному предикату.
        /// </summary>
        /// <param name="eventIdPredicate">Предикат для фильтрации идентификаторов событий. Если null, отменяются все ожидания.</param>
        /// <returns>Количество отмененных ожиданий.</returns>
        public static int CancelAllWaiting(Func<string, bool> eventIdPredicate = null)
        {
            var count = 0;
            var keysToRemove = Waiters.Keys
                .Where(k => eventIdPredicate == null || eventIdPredicate(k))
                .ToList();

            foreach (var key in keysToRemove)
            {
                if (Waiters.TryRemove(key, out var tcs))
                {
                    tcs.TrySetCanceled();
                    CleanupEventParams(key);
                    Metrics.OnWaiterCancelled(key);
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Отменяет ожидание указанного события.
        /// </summary>
        /// <param name="eventId">Идентификатор события.</param>
        /// <returns>true, если ожидание было успешно отменено; в противном случае false.</returns>
        /// <exception cref="ArgumentNullException">Возникает, если eventId равен null.</exception>
        public static bool CancelWait(string eventId)
        {
            if (eventId == null)
            {
                throw new ArgumentNullException(nameof(eventId));
            }

            if (Waiters.TryRemove(eventId, out var tcs))
            {
                CleanupEventParams(eventId);
                var result = tcs.TrySetCanceled();
                if (result)
                {
                    Metrics.OnWaiterCancelled(eventId);
                }

                return result;
            }

            return false;
        }

        /// <summary>
        /// Очищает все ожидающие события и их параметры.
        /// </summary>
        public static void ClearAll()
        {
            var waitersSnapshot = Waiters.ToArray();
            Waiters.Clear();

            foreach (var kv in waitersSnapshot)
            {
                kv.Value.TrySetCanceled();
                Metrics.OnWaiterCancelled(kv.Key);
            }

            var paramsSnapshot = EventParams.ToArray();
            EventParams.Clear();

            foreach (var kv in paramsSnapshot)
            {
                kv.Value?.Clear();
            }
        }

        /// <summary>
        /// Возвращает коллекцию идентификаторов активных ожидающих событий.
        /// </summary>
        /// <returns>Коллекция идентификаторов событий, находящихся в ожидании.</returns>
        public static IReadOnlyCollection<string> GetActiveWaiters()
        {
            return Waiters.Keys.ToList().AsReadOnly();
        }

        /// <summary>
        /// Получает значение параметра события.
        /// </summary>
        /// <param name="eventId">Идентификатор события.</param>
        /// <param name="paramName">Имя параметра.</param>
        /// <param name="defaultValue">Значение по умолчанию, если параметр не найден.</param>
        /// <returns>Значение параметра или defaultValue, если параметр не найден.</returns>
        /// <exception cref="ArgumentNullException">Возникает, если eventId равен null.</exception>
        /// <exception cref="ArgumentException">Возникает, если paramName равен null или пустой строке.</exception>
        public static object GetEventParam(string eventId, string paramName, object defaultValue = default)
        {
            return GetEventParam<object>(eventId, paramName, defaultValue);
        }

        /// <summary>
        /// Получает типизированное значение параметра события.
        /// </summary>
        /// <typeparam name="TParam">Тип параметра.</typeparam>
        /// <param name="eventId">Идентификатор события.</param>
        /// <param name="paramName">Имя параметра.</param>
        /// <param name="defaultValue">Значение по умолчанию, если параметр не найден или имеет неверный тип.</param>
        /// <returns>Значение параметра, приведенное к типу TParam, или defaultValue в случае ошибки.</returns>
        /// <exception cref="ArgumentNullException">Возникает, если eventId равен null.</exception>
        /// <exception cref="ArgumentException">Возникает, если paramName равен null или пустой строке.</exception>
        public static TParam GetEventParam<TParam>(string eventId, string paramName, TParam defaultValue = default)
        {
            if (eventId == null)
            {
                throw new ArgumentNullException(nameof(eventId));
            }

            if (string.IsNullOrEmpty(paramName))
            {
                throw new ArgumentException("Имя параметра не может быть null или пустой строкой", nameof(paramName));
            }

            if (!EventParams.TryGetValue(eventId, out var p))
            {
                return defaultValue;
            }

            if (p.TryGetValue(paramName, out var value) && value != null)
            {
                try
                {
                    return (TParam)value;
                }
                catch (InvalidCastException)
                {
                    System.Diagnostics.Debug.WriteLine($"Несоответствие типа для параметра {paramName}: ожидался {typeof(TParam)}, получен {value.GetType()}");
                    return defaultValue;
                }
            }

            return defaultValue;
        }

        /// <summary>
        /// Проверяет наличие параметра у события.
        /// </summary>
        /// <param name="eventId">Идентификатор события.</param>
        /// <param name="paramName">Имя параметра.</param>
        /// <returns>true, если параметр существует; в противном случае false.</returns>
        /// <exception cref="ArgumentNullException">Возникает, если eventId равен null.</exception>
        /// <exception cref="ArgumentException">Возникает, если paramName равен null или пустой строке.</exception>
        public static bool HasParam(string eventId, string paramName)
        {
            if (eventId == null)
            {
                throw new ArgumentNullException(nameof(eventId));
            }

            if (string.IsNullOrEmpty(paramName))
            {
                throw new ArgumentException("Имя параметра не может быть null или пустой строкой", nameof(paramName));
            }

            return EventParams.TryGetValue(eventId, out var p) && p.ContainsKey(paramName);
        }

        /// <summary>
        /// Устанавливает значение параметра события.
        /// </summary>
        /// <param name="eventId">Идентификатор события.</param>
        /// <param name="paramName">Имя параметра.</param>
        /// <param name="paramValue">Значение параметра.</param>
        /// <exception cref="ArgumentNullException">Возникает, если eventId равен null.</exception>
        /// <exception cref="ArgumentException">Возникает, если paramName равен null или пустой строке.</exception>
        public static void SetEventParam(string eventId, string paramName, object paramValue)
        {
            if (eventId == null)
            {
                throw new ArgumentNullException(nameof(eventId));
            }

            if (string.IsNullOrEmpty(paramName))
            {
                throw new ArgumentException("Имя параметра не может быть null или пустой строкой", nameof(paramName));
            }

            var p = EventParams.GetOrAdd(eventId, _ => new ConcurrentDictionary<string, object>());
            p[paramName] = paramValue;
        }

        /// <summary>
        /// Пытается завершить указанное событие с заданным статусом и данными.
        /// </summary>
        /// <param name="eventId">Идентификатор события.</param>
        /// <param name="status">Статус завершения события.</param>
        /// <param name="eventData">Дополнительные данные события.</param>
        /// <returns>true, если событие было успешно завершено; false, если событие не найдено в ожидающих.</returns>
        /// <exception cref="ArgumentNullException">Возникает, если eventId равен null.</exception>
        public static bool TryComplete(string eventId, T status, object eventData = null)
        {
            if (eventId == null)
            {
                throw new ArgumentNullException(nameof(eventId));
            }

            if (Waiters.TryRemove(eventId, out var tcs))
            {
                var result = new EventResult(eventId, status, eventData);
                tcs.SetResult(result);
                CleanupEventParams(eventId);
                Metrics.OnWaiterCompleted(eventId);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Асинхронно ожидает завершения события с указанным таймаутом.
        /// </summary>
        /// <param name="eventId">Идентификатор события.</param>
        /// <param name="timeoutStatus">Статус, возвращаемый при истечении таймаута.</param>
        /// <param name="maxMillisecondsToWait">Максимальное время ожидания в миллисекундах или Timeout.Infinite.</param>
        /// <returns>Задача, представляющая результат ожидания события.</returns>
        /// <exception cref="ArgumentNullException">Возникает, если eventId равен null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Возникает, если maxMillisecondsToWait меньше или равно 0 и не равно Timeout.Infinite.</exception>
        public static Task<EventResult> WaitAsync(string eventId, T timeoutStatus, int maxMillisecondsToWait)
        {
            return WaitAsync(eventId, timeoutStatus, maxMillisecondsToWait, CancellationToken.None);
        }

        /// <summary>
        /// Асинхронно ожидает завершения события с указанным таймаутом и поддержкой отмены.
        /// </summary>
        /// <param name="eventId">Идентификатор события.</param>
        /// <param name="timeoutStatus">Статус, возвращаемый при истечении таймаута.</param>
        /// <param name="maxMillisecondsToWait">Максимальное время ожидания в миллисекундах или Timeout.Infinite.</param>
        /// <param name="cancellationToken">Токен для отмены ожидания.</param>
        /// <returns>Задача, представляющая результат ожидания события.</returns>
        /// <exception cref="ArgumentNullException">Возникает, если eventId равен null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Возникает, если maxMillisecondsToWait меньше или равно 0 и не равно Timeout.Infinite.</exception>
        public static Task<EventResult> WaitAsync(
            string eventId,
            T timeoutStatus,
            int maxMillisecondsToWait,
            CancellationToken cancellationToken)
        {
            if (eventId == null)
            {
                throw new ArgumentNullException(nameof(eventId));
            }

            if (maxMillisecondsToWait <= 0 && maxMillisecondsToWait != Timeout.Infinite)
            {
                throw new ArgumentOutOfRangeException(nameof(maxMillisecondsToWait), "Таймаут должен быть положительным или Timeout.Infinite");
            }

            var tcs = new TaskCompletionSource<EventResult>(TaskCreationOptions.RunContinuationsAsynchronously);

            var existingTcs = Waiters.AddOrUpdate(eventId, tcs, (key, existing) => existing);

            if (!ReferenceEquals(existingTcs, tcs))
            {
                return existingTcs.Task;
            }

            if (maxMillisecondsToWait != Timeout.Infinite)
            {
                SetupTimeoutAndCancellation(eventId, tcs, timeoutStatus, maxMillisecondsToWait, cancellationToken);
            }
            else if (cancellationToken.CanBeCanceled)
            {
                SetupCancellationOnly(eventId, tcs, cancellationToken);
            }

            return tcs.Task;
        }

        private static void CleanupEventParams(string eventId)
        {
            if (EventParams.TryRemove(eventId, out var p))
            {
                p.Clear();
            }
        }

        private static void SetupCancellationOnly(
            string eventId,
            TaskCompletionSource<EventResult> tcs,
            CancellationToken cancellationToken)
        {
            CancellationTokenRegistration registration = default;
            registration = cancellationToken.Register(() =>
            {
                if (Waiters.TryRemove(eventId, out var removed))
                {
                    removed.TrySetCanceled(cancellationToken);
                    Metrics.OnWaiterCancelled(eventId);
                    CleanupEventParams(eventId);
                }

                registration.Dispose();
            });

            tcs.Task.ContinueWith(_ => registration.Dispose(), TaskContinuationOptions.ExecuteSynchronously);
        }

        private static void SetupTimeoutAndCancellation(
            string eventId,
            TaskCompletionSource<EventResult> tcs,
            T timeoutStatus,
            int maxMillisecondsToWait,
            CancellationToken cancellationToken)
        {
            var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(maxMillisecondsToWait));

            CancellationTokenRegistration registration = default;

            if (cancellationToken.CanBeCanceled)
            {
                var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    timeoutCts.Token);
                registration = linkedCts.Token.Register(() =>
                {
                    if (Waiters.TryRemove(eventId, out var removed))
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            removed.TrySetCanceled(cancellationToken);
                            Metrics.OnWaiterCancelled(eventId);
                        }
                        else
                        {
                            removed.TrySetResult(new EventResult(eventId, timeoutStatus, null));
                            Metrics.OnWaiterTimedOut(eventId);
                        }

                        CleanupEventParams(eventId);
                    }

                    registration.Dispose();
                    linkedCts.Dispose();
                    timeoutCts.Dispose();
                });
            }
            else
            {
                registration = timeoutCts.Token.Register(() =>
                {
                    if (Waiters.TryRemove(eventId, out var removed))
                    {
                        removed.TrySetResult(new EventResult(eventId, timeoutStatus, null));
                        Metrics.OnWaiterTimedOut(eventId);
                        CleanupEventParams(eventId);
                    }

                    registration.Dispose();
                    timeoutCts.Dispose();
                });
            }

            tcs.Task.ContinueWith(
                _ =>
                {
                    registration.Dispose();
                    timeoutCts?.Dispose();
                }, TaskContinuationOptions.ExecuteSynchronously);
        }

        /// <summary>
        /// Предоставляет метрики и события для отслеживания состояния ожидающих событий.
        /// </summary>
        public static class Metrics
        {
            /// <summary>
            /// Возникает при отмене ожидания события.
            /// </summary>
            public static event EventHandler<string> WaiterCancelled;

            /// <summary>
            /// Возникает при успешном завершении ожидания события.
            /// </summary>
            public static event EventHandler<string> WaiterCompleted;

            /// <summary>
            /// Возникает при истечении таймаута ожидания события.
            /// </summary>
            public static event EventHandler<string> WaiterTimedOut;

            /// <summary>
            /// Получает количество активных параметров событий.
            /// </summary>
            public static int ActiveParamsCount => EventParams.Count;

            /// <summary>
            /// Получает количество активных ожидающих событий.
            /// </summary>
            public static int ActiveWaitersCount => Waiters.Count;

            /// <summary>
            /// Вызывает событие отмены ожидающего события.
            /// </summary>
            /// <param name="eventId">Идентификатор отмененного события.</param>
            internal static void OnWaiterCancelled(string eventId) =>
                WaiterCancelled?.Invoke(null, eventId);

            /// <summary>
            /// Вызывает событие успешного завершения ожидающего события.
            /// </summary>
            /// <param name="eventId">Идентификатор завершенного события.</param>
            internal static void OnWaiterCompleted(string eventId) =>
                WaiterCompleted?.Invoke(null, eventId);

            /// <summary>
            /// Вызывает событие истечения таймаута ожидающего события.
            /// </summary>
            /// <param name="eventId">Идентификатор события, у которого истек таймаут.</param>
            internal static void OnWaiterTimedOut(string eventId) =>
                WaiterTimedOut?.Invoke(null, eventId);
        }

        /// <summary>
        /// Представляет результат ожидания события.
        /// </summary>
        public class EventResult
        {
            /// <summary>
            /// Инициализирует новый экземпляр класса <see cref="EventResult"/>.
            /// </summary>
            /// <param name="eventId">Идентификатор события.</param>
            /// <param name="status">Статус завершения события.</param>
            /// <param name="data">Дополнительные данные события (необязательно).</param>
            /// <exception cref="ArgumentNullException">Возникает, если eventId равен null.</exception>
            internal EventResult(string eventId, T status, object data = null)
            {
                this.EventId = eventId ?? throw new ArgumentNullException(nameof(eventId));
                this.Status = status;
                this.Data = data;
            }

            /// <summary>
            /// Получает дополнительные данные события.
            /// </summary>
            public object Data { get; }

            /// <summary>
            /// Получает идентификатор события.
            /// </summary>
            public string EventId { get; }

            /// <summary>
            /// Получает статус завершения события.
            /// </summary>
            public T Status { get; }

            /// <summary>
            /// Разлагает результат на компоненты.
            /// </summary>
            /// <param name="eventId">Идентификатор события.</param>
            /// <param name="status">Статус завершения.</param>
            /// <param name="data">Дополнительные данные.</param>
            public void Deconstruct(out string eventId, out T status, out object data)
            {
                eventId = this.EventId;
                status = this.Status;
                data = this.Data;
            }
        }
    }
}