using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace RuntimeStuff.Helpers
{

    /// <summary>
    ///     Предоставляет вспомогательные методы для безопасного запуска задач в фоновом режиме без ожидания завершения
    ///     (fire-and-forget).
    ///     Позволяет централизованно обрабатывать исключения, возникающие в задачах.
    /// </summary>
    public static class TaskHelper
    {
        private static Action<Exception> _onException;
        private static bool _shouldAlwaysRethrowException;

        /// <summary>
        ///     Выполняет задачу в фоновом режиме без ожидания завершения, с обработкой исключений общего типа
        ///     <see cref="Exception" />.
        /// </summary>
        /// <param name="task">Задача, которую нужно запустить.</param>
        /// <param name="onException">Действие, выполняемое при возникновении исключения.</param>
        /// <param name="continueOnCapturedContext">Если <c>true</c>, продолжение выполняется в исходном контексте синхронизации.</param>
        public static void RunAndForget(Task task, Action<Exception> onException, bool continueOnCapturedContext = false)
        {
            RunAndForget(task, in onException, in continueOnCapturedContext);
        }

        /// <summary>
        ///     Выполняет задачу в фоновом режиме без ожидания завершения, с обработкой исключений определённого типа.
        /// </summary>
        /// <typeparam name="TException">Тип обрабатываемого исключения.</typeparam>
        /// <param name="task">Задача, которую нужно запустить.</param>
        /// <param name="onException">Действие, выполняемое при возникновении исключения указанного типа.</param>
        /// <param name="continueOnCapturedContext">Если <c>true</c>, продолжение выполняется в исходном контексте синхронизации.</param>
        public static void RunAndForget<TException>(Task task, Action<TException> onException,
            bool continueOnCapturedContext = false)
            where TException : Exception
        {
            RunAndForget(task, in onException, in continueOnCapturedContext);
        }

        /// <summary>
        ///     Выполняет задачу в фоновом режиме без ожидания завершения и с необязательной обработкой исключений.
        /// </summary>
        /// <param name="task">Задача, которую нужно запустить.</param>
        /// <param name="onException">Действие, выполняемое при возникновении исключения (необязательно).</param>
        /// <param name="continueOnCapturedContext">Если <c>true</c>, продолжение выполняется в исходном контексте синхронизации.</param>
        public static void RunAndForget(Task task, in Action<Exception> onException = null,
            in bool continueOnCapturedContext = false)
        {
            HandleRunAndForget(task, continueOnCapturedContext, onException);
        }

        /// <summary>
        ///     Выполняет задачу в фоновом режиме без ожидания завершения и с необязательной обработкой исключений указанного типа.
        /// </summary>
        /// <typeparam name="TException">Тип обрабатываемого исключения.</typeparam>
        /// <param name="task">Задача, которую нужно запустить.</param>
        /// <param name="onException">Действие, выполняемое при возникновении исключения (необязательно).</param>
        /// <param name="continueOnCapturedContext">Если <c>true</c>, продолжение выполняется в исходном контексте синхронизации.</param>
        public static void RunAndForget<TException>(Task task, in Action<TException> onException = null,
            in bool continueOnCapturedContext = false)
            where TException : Exception
        {
            HandleRunAndForget(task, continueOnCapturedContext, onException);
        }

        /// <summary>
        ///     Инициализирует вспомогательную систему, указывая, следует ли всегда повторно выбрасывать исключения после
        ///     обработки.
        /// </summary>
        /// <param name="shouldAlwaysRethrowException">Если <c>true</c>, исключения будут повторно выброшены после обработки.</param>
        public static void Initialize(in bool shouldAlwaysRethrowException = false)
        {
            _shouldAlwaysRethrowException = shouldAlwaysRethrowException;
        }

        /// <summary>
        ///     Удаляет глобальный обработчик исключений, установленный методом <see cref="SetDefaultExceptionHandling" />.
        /// </summary>
        public static void RemoveDefaultExceptionHandling()
        {
            _onException = null;
        }

        /// <summary>
        ///     Устанавливает глобальный обработчик исключений, вызываемый при возникновении ошибок в задачах, запущенных методом
        ///     <c>RunAndForget</c>.
        /// </summary>
        /// <param name="onException">Действие, выполняемое при возникновении исключения.</param>
        /// <exception cref="ArgumentNullException">Выбрасывается, если <paramref name="onException" /> имеет значение <c>null</c>.</exception>
        public static void SetDefaultExceptionHandling(in Action<Exception> onException)
        {
            _onException = onException ?? throw new ArgumentNullException(nameof(onException));
        }

        /// <summary>
        ///     Обрабатывает выполнение задачи и перехватывает исключения указанного типа.
        /// </summary>
        /// <typeparam name="TException">Тип обрабатываемого исключения.</typeparam>
        /// <param name="task">Задача, выполняемая в фоне.</param>
        /// <param name="continueOnCapturedContext">Если <c>true</c>, продолжение выполняется в исходном контексте синхронизации.</param>
        /// <param name="onException">Действие, выполняемое при возникновении исключения.</param>
        private static async void HandleRunAndForget<TException>(Task task, bool continueOnCapturedContext, Action<TException> onException) where TException : Exception
        {
            try
            {
                await task.ConfigureAwait(continueOnCapturedContext);
            }
            catch (TException ex) when (!(_onException is null) || !(onException is null))
            {
                HandleException(ex, onException);

                if (_shouldAlwaysRethrowException)
                {
#if NET5_0_OR_GREATER
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(ex);
#else
                    throw;
#endif
                }
            }
        }

        /// <summary>
        ///     Вызывает зарегистрированные обработчики исключений.
        /// </summary>
        /// <typeparam name="TException">Тип исключения.</typeparam>
        /// <param name="exception">Исключение, которое необходимо обработать.</param>
        /// <param name="onException">Локальный обработчик исключений.</param>
        private static void HandleException<TException>(in TException exception, in Action<TException> onException) where TException : Exception
        {
            _onException?.Invoke(exception);
            onException?.Invoke(exception);
        }
    }

    /// <summary>
    ///     Статический класс для асинхронного ожидания событий по идентификатору
    ///     и последующего оповещения ожидающих при изменении статуса.
    /// </summary>
    /// <remarks>
    ///     Позволяет из одного компонента ожидать событие по <see cref="string" /> id,
    ///     а из другого — завершить ожидание, установив статус и данные события.
    /// </remarks>
    public static class TaskHelper<T>
    {
        /// <summary>
        ///     Хранилище ожидающих задач по идентификатору события.
        /// </summary>
        private static readonly ConcurrentDictionary<object, TaskCompletionSource<EventInfo<T>>> _waiters
            = new ConcurrentDictionary<object, TaskCompletionSource<EventInfo<T>>>();

        /// <summary>
        ///     Асинхронно ожидает событие с указанным идентификатором, пока не будет использовано <see cref="TryComplete"/> или истечет время ожидания
        /// </summary>
        /// <param name="eventId">Уникальный идентификатор события.</param>
        /// <param name="maxMillisecondsToWait">
        ///     Максимальное время ожидания в секундах.
        ///     По истечении времени ожидание будет автоматически отменено.
        /// </param>
        /// <returns>
        ///     Задача, завершающаяся объектом <see cref="EventInfo" /> при установке события.
        /// </returns>
        /// <remarks>
        ///     Если ожидание по указанному идентификатору уже существует,
        ///     будет возвращена существующая задача.
        /// </remarks>
        public static Task<EventInfo<T>> Wait(object eventId, int maxMillisecondsToWait = 5000)
        {
            if (eventId == null)
                throw new NullReferenceException(nameof(eventId));

            var tcs = new TaskCompletionSource<EventInfo<T>>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            if (!_waiters.TryAdd(eventId, tcs)) return _waiters[eventId].Task;

            var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(maxMillisecondsToWait));

            cts.Token.Register(() =>
            {
                if (_waiters.TryRemove(eventId, out var removed)) removed.TrySetCanceled();
            });
            return tcs.Task;
        }

        /// <summary>
        /// Асинхронно ожидает событие с указанным идентификатором и возвращает результат с заданным статусом таймаута,
        /// если событие не произошло в течение указанного времени.
        /// </summary>
        /// <param name="eventId">Уникальный идентификатор события, по которому ожидается уведомление.</param>
        /// <param name="timeoutStatus">
        /// Статус, который будет установлен в <see cref="EventInfo{T}.Status"/> при истечении времени ожидания.
        /// </param>
        /// <param name="maxMillisecondsToWait">
        /// Максимальное время ожидания события в секундах. По истечении этого времени возвращается <paramref name="timeoutStatus"/>.
        /// </param>
        /// <returns>
        /// Задача, завершающаяся объектом <see cref="EventInfo{T}"/> с указанным статусом или статусом таймаута.
        /// </returns>
        /// <remarks>
        /// Если ожидание с указанным идентификатором уже существует, возвращается существующая задача.
        /// </remarks>
        public static Task<EventInfo<T>> Wait(object eventId, T timeoutStatus, int maxMillisecondsToWait)
        {
            if (eventId == null)
                throw new NullReferenceException(nameof(eventId));

            var tcs = new TaskCompletionSource<EventInfo<T>>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            if (!_waiters.TryAdd(eventId, tcs))
                return _waiters[eventId].Task;

            var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(maxMillisecondsToWait));
            cts.Token.Register(() =>
            {
                if (_waiters.TryRemove(eventId, out var removed))
                {
                    // Возвращаем EventInfo с заданным статусом таймаута
                    removed.TrySetResult(new EventInfo<T>(eventId, timeoutStatus));
                }
            });

            return tcs.Task;
        }

        /// <summary>
        ///     Устанавливает информацию о событии и завершает ожидание для указанного идентификатора.
        /// </summary>
        /// <param name="eventId">Идентификатор события.</param>
        /// <param name="status">Статус события.</param>
        /// <param name="eventData">Произвольные данные, связанные с событием.</param>
        /// <returns>
        ///     <c>true</c>, если ожидание было найдено и успешно завершено;
        ///     <c>false</c>, если ожидания по данному идентификатору не существовало.
        /// </returns>
        public static bool TryComplete(object eventId, T status, object eventData = null)
        {
            if (eventId == null)
                throw new NullReferenceException(nameof(eventId));

            var eventInfo = new EventInfo<T>(eventId, status, eventData);

            if (_waiters.TryRemove(eventId, out var tsc))
            {
                tsc.SetResult(eventInfo);
                return true;
            }

            return false;
        }

        /// <summary>
        ///     Принудительно отменяет ожидание события по указанному идентификатору.
        /// </summary>
        /// <param name="eventId">Идентификатор события.</param>
        /// <returns>
        ///     <c>true</c>, если ожидание было найдено и отменено;
        ///     <c>false</c>, если ожидание отсутствовало.
        /// </returns>
        public static bool CancelWait(object eventId)
        {
            if (eventId == null)
                throw new NullReferenceException(nameof(eventId));

            return _waiters.TryRemove(eventId, out var tsc) && tsc.TrySetCanceled();
        }

        /// <summary>
        ///     Отменяет все активные ожидания и очищает внутреннее хранилище.
        /// </summary>
        /// <remarks>
        ///     Используется при завершении работы приложения или
        ///     при необходимости полного сброса состояния ожиданий.
        /// </remarks>
        public static void ClearAll()
        {
            foreach (var tsc in _waiters.Values) tsc.TrySetCanceled();

            _waiters.Clear();
        }
    }

    /// <summary>
    ///     Информация о произошедшем событии.
    /// </summary>
    public sealed class EventInfo<T>
    {
        /// <summary>
        ///     Создаёт новый экземпляр информации о событии.
        /// </summary>
        /// <param name="eventId">Идентификатор события.</param>
        /// <param name="status">Статус события.</param>
        /// <param name="data">Произвольные данные события.</param>
        public EventInfo(object eventId, T status, object data = null)
        {
            EventId = eventId ?? throw new NullReferenceException(nameof(eventId));
            Status = status;
            Data = data;
        }

        /// <summary>
        ///     Идентификатор события.
        /// </summary>
        public object EventId { get; }

        /// <summary>
        ///     Статус события.
        /// </summary>
        public T Status { get; }

        /// <summary>
        ///     Дополнительные данные, связанные с событием.
        /// </summary>
        public object Data { get; set; }
    }
}