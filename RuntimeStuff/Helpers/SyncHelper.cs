namespace RuntimeStuff.Helpers
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;

    public static class SyncHelper<T>
        where T : struct, Enum
    {
        private static readonly ConcurrentDictionary<string, TaskCompletionSource<EventResult>> Waiters
            = new ConcurrentDictionary<string, TaskCompletionSource<EventResult>>();

        private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, object>> EventParams
            = new ConcurrentDictionary<string, ConcurrentDictionary<string, object>>();


        public static bool CancelWait(string eventId)
        {
            if (eventId == null)
            {
                throw new ArgumentNullException(nameof(eventId));
            }

            if (Waiters.TryRemove(eventId, out var tcs))
            {
                if (EventParams.TryRemove(eventId, out var p))
                {
                    p.Clear();
                }

                return tcs.TrySetCanceled();
            }

            return false;
        }

        public static void ClearAll()
        {
            foreach (var kv in Waiters)
            {
                if (Waiters.TryRemove(kv.Key, out var tcs))
                {
                    tcs.TrySetCanceled();
                }
            }

            foreach (var ep in EventParams)
            {
                ep.Value?.Clear();
            }

            EventParams.Clear();
        }

        public static bool HasParam(string eventId, string paramName)
        {
            if (eventId == null)
            {
                throw new ArgumentNullException(nameof(eventId));
            }

            if (string.IsNullOrEmpty(paramName))
            {
                throw new ArgumentException(nameof(paramName));
            }

            if (!EventParams.TryGetValue(eventId, out var p))
            {
                return false;
            }

            return p.ContainsKey(paramName);
        }

        public static object GetEventParam(string eventId, string paramName, object defaultValue = default)
        {
            return GetEventParam<object>(eventId, paramName, defaultValue);
        }

        public static TParam GetEventParam<TParam>(string eventId, string paramName, TParam defaultValue = default)
        {
            if (eventId == null)
            {
                throw new ArgumentNullException(nameof(eventId));
            }

            if (string.IsNullOrEmpty(paramName))
            {
                throw new ArgumentException(nameof(paramName));
            }

            if (!EventParams.TryGetValue(eventId, out var p))
            {
                return defaultValue;
            }

            if (p.TryGetValue(paramName, out var value))
            {
                if (value == null)
                {
                    return defaultValue;
                }

                return (TParam)value;
            }

            return defaultValue;
        }

        public static void SetEventParam(string eventId, string paramName, object paramValue)
        {
            if (eventId == null)
            {
                throw new ArgumentNullException(nameof(eventId));
            }

            if (string.IsNullOrEmpty(paramName))
            {
                throw new ArgumentException(nameof(paramName));
            }

            var p = EventParams.GetOrAdd(eventId, _ => new ConcurrentDictionary<string, object>());
            p[paramName] = paramValue;
        }

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
                if (EventParams.TryRemove(eventId, out var p))
                {
                    p.Clear();
                }

                return true;
            }

            return false;
        }

        public static Task<EventResult> WaitAsync(string eventId, T timeoutStatus, int maxMillisecondsToWait)
        {
            if (eventId == null)
            {
                throw new ArgumentNullException(nameof(eventId));
            }

            var tcs = new TaskCompletionSource<EventResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            if (!Waiters.TryAdd(eventId, tcs))
            {
                if (Waiters.TryGetValue(eventId, out var existing))
                {
                    return existing.Task;
                }

                return WaitAsync(eventId, timeoutStatus, maxMillisecondsToWait);
            }

            var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(maxMillisecondsToWait));

            CancellationTokenRegistration registration = default;

            registration = cts.Token.Register(() =>
            {
                if (Waiters.TryRemove(eventId, out var removed))
                {
                    removed.TrySetCanceled();
                }

                registration.Dispose();
                cts.Dispose();
            });

            return tcs.Task;
        }

        public class EventResult
        {
            internal EventResult(string eventId, T status, object data = null)
            {
                this.EventId = eventId ?? throw new ArgumentNullException(nameof(eventId));
                this.Status = status;
                this.Data = data;
            }

            public object Data { get; }

            public string EventId { get; }

            public T Status { get; }
        }
    }
}