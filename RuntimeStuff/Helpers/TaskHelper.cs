using System;
using System.Threading.Tasks;

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
    private static async void HandleRunAndForget<TException>(Task task, bool continueOnCapturedContext,
        Action<TException> onException)
        where TException : Exception
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
    private static void HandleException<TException>(in TException exception, in Action<TException> onException)
        where TException : Exception
    {
        _onException?.Invoke(exception);
        onException?.Invoke(exception);
    }
}