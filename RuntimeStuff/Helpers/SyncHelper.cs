//namespace RuntimeStuff.Helpers
//{
//    using System;
//    using System.Collections.Concurrent;
//    using System.Collections.Generic;
//    using System.Linq;
//    using System.Threading;
//    using System.Threading.Tasks;

//    /// <summary>
//    /// Provides synchronization helpers for waiting on events with typed status codes.
//    /// Thread-safe implementation for managing async event waiters and parameters.
//    /// </summary>
//    /// <typeparam name="T">Enum type representing possible status values</typeparam>
//    public static class SyncHelper<T> where T : struct, Enum
//    {
//        private static readonly ConcurrentDictionary<string, TaskCompletionSource<EventResult>> Waiters
//            = new ConcurrentDictionary<string, TaskCompletionSource<EventResult>>();

//        private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, object>> EventParams
//            = new ConcurrentDictionary<string, ConcurrentDictionary<string, object>>();

//        /// <summary>
//        /// Metrics and monitoring for the SyncHelper.
//        /// </summary>
//        public static class Metrics
//        {
//            /// <summary>Gets the number of active waiters.</summary>
//            public static int ActiveWaitersCount => Waiters.Count;

//            /// <summary>Gets the number of active event parameters collections.</summary>
//            public static int ActiveParamsCount => EventParams.Count;

//            /// <summary>Occurs when a waiter completes successfully.</summary>
//            public static event EventHandler<string> WaiterCompleted;

//            /// <summary>Occurs when a waiter is cancelled.</summary>
//            public static event EventHandler<string> WaiterCancelled;

//            /// <summary>Occurs when a waiter times out.</summary>
//            public static event EventHandler<string> WaiterTimedOut;

//            internal static void OnWaiterCompleted(string eventId) =>
//                WaiterCompleted?.Invoke(null, eventId);

//            internal static void OnWaiterCancelled(string eventId) =>
//                WaiterCancelled?.Invoke(null, eventId);

//            internal static void OnWaiterTimedOut(string eventId) =>
//                WaiterTimedOut?.Invoke(null, eventId);
//        }

//        /// <summary>
//        /// Cancels a waiting operation for the specified event.
//        /// </summary>
//        /// <param name="eventId">Unique identifier for the event</param>
//        /// <returns>True if the waiter was found and cancelled; otherwise, false</returns>
//        /// <exception cref="ArgumentNullException">Thrown if eventId is null</exception>
//        public static bool CancelWait(string eventId)
//        {
//            if (eventId == null)
//                throw new ArgumentNullException(nameof(eventId));

//            if (Waiters.TryRemove(eventId, out var tcs))
//            {
//                CleanupEventParams(eventId);
//                var result = tcs.TrySetCanceled();
//                if (result)
//                    Metrics.OnWaiterCancelled(eventId);
//                return result;
//            }

//            return false;
//        }

//        /// <summary>
//        /// Cancels all waiting operations that match the optional predicate.
//        /// </summary>
//        /// <param name="eventIdPredicate">Optional predicate to filter event IDs. If null, cancels all.</param>
//        /// <returns>Number of cancelled waiters</returns>
//        public static int CancelAllWaiting(Func<string, bool> eventIdPredicate = null)
//        {
//            int count = 0;
//            var keysToRemove = Waiters.Keys
//                .Where(k => eventIdPredicate == null || eventIdPredicate(k))
//                .ToList();

//            foreach (var key in keysToRemove)
//            {
//                if (Waiters.TryRemove(key, out var tcs))
//                {
//                    tcs.TrySetCanceled();
//                    CleanupEventParams(key);
//                    Metrics.OnWaiterCancelled(key);
//                    count++;
//                }
//            }

//            return count;
//        }

//        /// <summary>
//        /// Clears all waiters and event parameters.
//        /// </summary>
//        public static void ClearAll()
//        {
//            // Atomic snapshot and clear of waiters
//            var waitersSnapshot = Waiters.ToArray();
//            Waiters.Clear();

//            foreach (var kv in waitersSnapshot)
//            {
//                kv.Value.TrySetCanceled();
//                Metrics.OnWaiterCancelled(kv.Key);
//            }

//            // Atomic snapshot and clear of parameters
//            var paramsSnapshot = EventParams.ToArray();
//            EventParams.Clear();

//            foreach (var kv in paramsSnapshot)
//            {
//                kv.Value?.Clear();
//            }
//        }

//        /// <summary>
//        /// Gets a read-only collection of active waiter IDs.
//        /// </summary>
//        public static IReadOnlyCollection<string> GetActiveWaiters()
//        {
//            return Waiters.Keys.ToList().AsReadOnly();
//        }

//        /// <summary>
//        /// Checks if an event has a specific parameter.
//        /// </summary>
//        /// <param name="eventId">Unique identifier for the event</param>
//        /// <param name="paramName">Name of the parameter</param>
//        /// <returns>True if the parameter exists; otherwise, false</returns>
//        /// <exception cref="ArgumentNullException">Thrown if eventId is null</exception>
//        /// <exception cref="ArgumentException">Thrown if paramName is null or empty</exception>
//        public static bool HasParam(string eventId, string paramName)
//        {
//            if (eventId == null)
//                throw new ArgumentNullException(nameof(eventId));
//            if (string.IsNullOrEmpty(paramName))
//                throw new ArgumentException("Parameter name cannot be null or empty", nameof(paramName));

//            return EventParams.TryGetValue(eventId, out var p) && p.ContainsKey(paramName);
//        }

//        /// <summary>
//        /// Gets an event parameter as object.
//        /// </summary>
//        /// <param name="eventId">Unique identifier for the event</param>
//        /// <param name="paramName">Name of the parameter</param>
//        /// <param name="defaultValue">Default value to return if parameter doesn't exist</param>
//        /// <returns>Parameter value or default value</returns>
//        public static object GetEventParam(string eventId, string paramName, object defaultValue = default)
//        {
//            return GetEventParam<object>(eventId, paramName, defaultValue);
//        }

//        /// <summary>
//        /// Gets a typed event parameter.
//        /// </summary>
//        /// <typeparam name="TParam">Type of the parameter</typeparam>
//        /// <param name="eventId">Unique identifier for the event</param>
//        /// <param name="paramName">Name of the parameter</param>
//        /// <param name="defaultValue">Default value to return if parameter doesn't exist or cast fails</param>
//        /// <returns>Typed parameter value or default value</returns>
//        /// <exception cref="ArgumentNullException">Thrown if eventId is null</exception>
//        /// <exception cref="ArgumentException">Thrown if paramName is null or empty</exception>
//        public static TParam GetEventParam<TParam>(string eventId, string paramName, TParam defaultValue = default)
//        {
//            if (eventId == null)
//                throw new ArgumentNullException(nameof(eventId));
//            if (string.IsNullOrEmpty(paramName))
//                throw new ArgumentException("Parameter name cannot be null or empty", nameof(paramName));

//            if (!EventParams.TryGetValue(eventId, out var p))
//                return defaultValue;

//            if (p.TryGetValue(paramName, out var value) && value != null)
//            {
//                try
//                {
//                    return (TParam)value;
//                }
//                catch (InvalidCastException)
//                {
//                    // Log type mismatch if logging is available
//                    System.Diagnostics.Debug.WriteLine($"Type mismatch for parameter {paramName}: expected {typeof(TParam)}, got {value.GetType()}");
//                    return defaultValue;
//                }
//            }

//            return defaultValue;
//        }

//        /// <summary>
//        /// Sets an event parameter.
//        /// </summary>
//        /// <param name="eventId">Unique identifier for the event</param>
//        /// <param name="paramName">Name of the parameter</param>
//        /// <param name="paramValue">Value to set</param>
//        /// <exception cref="ArgumentNullException">Thrown if eventId is null</exception>
//        /// <exception cref="ArgumentException">Thrown if paramName is null or empty</exception>
//        public static void SetEventParam(string eventId, string paramName, object paramValue)
//        {
//            if (eventId == null)
//                throw new ArgumentNullException(nameof(eventId));
//            if (string.IsNullOrEmpty(paramName))
//                throw new ArgumentException("Parameter name cannot be null or empty", nameof(paramName));

//            var p = EventParams.GetOrAdd(eventId, _ => new ConcurrentDictionary<string, object>());
//            p[paramName] = paramValue;
//        }

//        /// <summary>
//        /// Tries to complete a waiting operation with the specified status and data.
//        /// </summary>
//        /// <param name="eventId">Unique identifier for the event</param>
//        /// <param name="status">Status to set</param>
//        /// <param name="eventData">Optional event data</param>
//        /// <returns>True if the waiter was found and completed; otherwise, false</returns>
//        /// <exception cref="ArgumentNullException">Thrown if eventId is null</exception>
//        public static bool TryComplete(string eventId, T status, object eventData = null)
//        {
//            if (eventId == null)
//                throw new ArgumentNullException(nameof(eventId));

//            if (Waiters.TryRemove(eventId, out var tcs))
//            {
//                var result = new EventResult(eventId, status, eventData);
//                tcs.SetResult(result);
//                CleanupEventParams(eventId);
//                Metrics.OnWaiterCompleted(eventId);
//                return true;
//            }

//            return false;
//        }

//        /// <summary>
//        /// Waits asynchronously for an event to complete or timeout.
//        /// </summary>
//        /// <param name="eventId">Unique identifier for the event</param>
//        /// <param name="timeoutStatus">Status to return on timeout</param>
//        /// <param name="maxMillisecondsToWait">Maximum wait time in milliseconds. Use -1 for infinite.</param>
//        /// <returns>Task representing the wait operation</returns>
//        /// <exception cref="ArgumentNullException">Thrown if eventId is null</exception>
//        /// <exception cref="ArgumentOutOfRangeException">Thrown if timeout is invalid</exception>
//        public static Task<EventResult> WaitAsync(string eventId, T timeoutStatus, int maxMillisecondsToWait)
//        {
//            return WaitAsync(eventId, timeoutStatus, maxMillisecondsToWait, CancellationToken.None);
//        }

//        /// <summary>
//        /// Waits asynchronously for an event to complete or timeout with cancellation support.
//        /// </summary>
//        /// <param name="eventId">Unique identifier for the event</param>
//        /// <param name="timeoutStatus">Status to return on timeout</param>
//        /// <param name="maxMillisecondsToWait">Maximum wait time in milliseconds. Use -1 for infinite.</param>
//        /// <param name="cancellationToken">Cancellation token to cancel the wait</param>
//        /// <returns>Task representing the wait operation</returns>
//        /// <exception cref="ArgumentNullException">Thrown if eventId is null</exception>
//        /// <exception cref="ArgumentOutOfRangeException">Thrown if timeout is invalid</exception>
//        public static Task<EventResult> WaitAsync(
//            string eventId,
//            T timeoutStatus,
//            int maxMillisecondsToWait,
//            CancellationToken cancellationToken)
//        {
//            if (eventId == null)
//                throw new ArgumentNullException(nameof(eventId));

//            if (maxMillisecondsToWait <= 0 && maxMillisecondsToWait != Timeout.Infinite)
//                throw new ArgumentOutOfRangeException(nameof(maxMillisecondsToWait),
//                    "Timeout must be positive or Timeout.Infinite");

//            // Atomic add or get existing
//            var tcs = Waiters.AddOrUpdate(eventId,
//                // Add new
//                _ => new TaskCompletionSource<EventResult>(TaskCreationOptions.RunContinuationsAsynchronously),
//                // Return existing if present
//                (_, existing) => existing);

//            // If this is a new waiter, set up timeout handling
//            if (tcs.Task.IsCompleted == false &&
//                ReferenceEquals(GetExistingTcs(eventId), tcs) &&
//                maxMillisecondsToWait != Timeout.Infinite)
//            {
//                SetupTimeoutAndCancellation(eventId, tcs, timeoutStatus, maxMillisecondsToWait, cancellationToken);
//            }

//            return tcs.Task;
//        }

//        /// <summary>
//        /// Waits asynchronously with Task optimization for already completed tasks.
//        /// </summary>
//        public static Task<EventResult> WaitAsyncValue(
//            string eventId,
//            T timeoutStatus,
//            int maxMillisecondsToWait,
//            CancellationToken cancellationToken = default)
//        {
//            // Fast path: check if waiter already exists and is completed
//            if (Waiters.TryGetValue(eventId, out var existing) && existing.IsCompleted)
//            {
//                return new Task<EventResult>(existing);
//            }

//            // Slow path: need to wait
//            return new Task<EventResult>(WaitAsync(eventId, timeoutStatus, maxMillisecondsToWait, cancellationToken));
//        }

//        private static TaskCompletionSource<EventResult> GetExistingTcs(string eventId)
//        {
//            Waiters.TryGetValue(eventId, out var tcs);
//            return tcs;
//        }

//        private static void SetupTimeoutAndCancellation(
//            string eventId,
//            TaskCompletionSource<EventResult> tcs,
//            T timeoutStatus,
//            int maxMillisecondsToWait,
//            CancellationToken cancellationToken)
//        {
//            if (cancellationToken.CanBeCanceled || maxMillisecondsToWait > 0)
//            {
//                // Create timeout source if needed
//                using var timeoutCts = maxMillisecondsToWait > 0
//                    ? new CancellationTokenSource(TimeSpan.FromMilliseconds(maxMillisecondsToWait))
//                    : null;

//                // Combine tokens if both exist
//                var linkedCts = cancellationToken.CanBeCanceled && timeoutCts != null
//                    ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token)
//                    : null;

//                var token = linkedCts?.Token ?? timeoutCts?.Token ?? cancellationToken;

//                if (token.CanBeCanceled)
//                {
//                    var registration = token.Register(() =>
//                    {
//                        if (Waiters.TryRemove(eventId, out var removed))
//                        {
//                            if (cancellationToken.IsCancellationRequested)
//                            {
//                                removed.TrySetCanceled(cancellationToken);
//                                Metrics.OnWaiterCancelled(eventId);
//                            }
//                            else // Timeout occurred
//                            {
//                                removed.TrySetResult(new EventResult(eventId, timeoutStatus, null));
//                                Metrics.OnWaiterTimedOut(eventId);
//                            }
//                            CleanupEventParams(eventId);
//                        }
//                    });

//                    // Clean up registration when task completes
//                    tcs.Task.ContinueWith(_ =>
//                    {
//                        registration.Dispose();
//                        linkedCts?.Dispose();
//                        timeoutCts?.Dispose();
//                    }, TaskContinuationOptions.ExecuteSynchronously);
//                }
//            }
//        }

//        private static void CleanupEventParams(string eventId)
//        {
//            if (EventParams.TryRemove(eventId, out var p))
//            {
//                p.Clear();
//            }
//        }

//        /// <summary>
//        /// Represents the result of a wait operation.
//        /// </summary>
//        public class EventResult
//        {
//            internal EventResult(string eventId, T status, object data = null)
//            {
//                EventId = eventId ?? throw new ArgumentNullException(nameof(eventId));
//                Status = status;
//                Data = data;
//            }

//            /// <summary>Gets the event data.</summary>
//            public object Data { get; }

//            /// <summary>Gets the event identifier.</summary>
//            public string EventId { get; }

//            /// <summary>Gets the status.</summary>
//            public T Status { get; }

//            /// <summary>
//            /// Deconstructs the result into its components.
//            /// </summary>
//            public void Deconstruct(out string eventId, out T status, out object data)
//            {
//                eventId = EventId;
//                status = Status;
//                data = Data;
//            }
//        }
//    }
//}