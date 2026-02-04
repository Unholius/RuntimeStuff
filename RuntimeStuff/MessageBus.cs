// <copyright file="MessageBus.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace RuntimeStuff
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Асинхронная шина сообщений для публикации и подписки на сообщения различных типов.
    /// </summary>
    /// <remarks>
    /// Позволяет подписчикам получать сообщения синхронно или через контекст синхронизации,
    /// а также ожидать сообщения с возможностью фильтрации, таймаута и отмены.
    /// Потокобезопасен для публикации и подписки.
    /// </remarks>
    public sealed class MessageBus : IDisposable
    {
        private static readonly ConcurrentDictionary<string, MessageBus> Channels = new ConcurrentDictionary<string, MessageBus>();
        private readonly ConcurrentDictionary<Type, List<Delegate>> handlers = new ConcurrentDictionary<Type, List<Delegate>>();
        private readonly ConcurrentQueue<object> queue = new ConcurrentQueue<object>();
        private readonly AutoResetEvent signal = new AutoResetEvent(false);
        private readonly ConcurrentDictionary<Type, List<Action<object>>> waitingHandlers = new ConcurrentDictionary<Type, List<Action<object>>>();
        private readonly Thread[] workers;
        private readonly ConcurrentDictionary<Delegate, Delegate> wrappedHandlers = new ConcurrentDictionary<Delegate, Delegate>();
        private bool disposed = false;
        private volatile bool running = true;

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageBus"/> class.
        /// Инициализирует новый экземпляр <see cref="MessageBus"/> с указанным количеством рабочих потоков.
        /// </summary>
        /// <param name="workerCount">Количество потоков-воркеров для обработки сообщений.</param>
        /// <exception cref="ArgumentOutOfRangeException">Если <paramref name="workerCount"/> меньше или равно нулю.</exception>
        public MessageBus(int workerCount)
            : this("MessageBusWorker", workerCount)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageBus"/> class.
        /// Инициализирует новый экземпляр <see cref="MessageBus"/> с именем потоков и их количеством.
        /// </summary>
        /// <param name="threadName">Базовое имя рабочих потоков.</param>
        /// <param name="workerCount">Количество потоков-воркеров.</param>
        /// <exception cref="ArgumentOutOfRangeException">Если <paramref name="workerCount"/> меньше или равно нулю.</exception>
        public MessageBus(string threadName = "MessageBusWorker", int workerCount = 1)
        {
            if (workerCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(workerCount));

            Name = threadName;

            workers = new Thread[workerCount];

            for (int i = 0; i < workers.Length; i++)
            {
                workers[i] = new Thread(WorkerLoop)
                {
                    IsBackground = true,
                    Name = $"{threadName}-{i}",
                };
                workers[i].Start();
            }
        }

        /// <summary>
        /// Глобальный экземпляр <see cref="MessageBus"/>, использующий количество потоков, равное числу процессоров.
        /// </summary>
        public static MessageBus MultiThreaded { get; } =
            new MessageBus(workerCount: Environment.ProcessorCount);

        /// <summary>
        /// Глобальный экземпляр <see cref="MessageBus"/> с одним рабочим потоком.
        /// </summary>
        public static MessageBus SingleThreaded { get; } =
            new MessageBus(workerCount: 1);

        /// <summary>
        /// Имя текущей шины сообщений.
        /// </summary>
        /// <value>The name.</value>
        public string Name { get; }

        /// <summary>
        /// Gets the <see cref="MessageBus"/> with the specified channel name.
        /// </summary>
        /// <param name="channelName">Name of the channel.</param>
        /// <returns>MessageBus.</returns>
        public MessageBus this[string channelName] => Channels.GetOrAdd(channelName, x => new MessageBus(x, workers.Length));

        /// <summary>
        /// Освобождает ресурсы и завершает работу всех рабочих потоков.
        /// </summary>
        public void Dispose()
        {
            if (disposed) return; // безопасный повторный вызов
            disposed = true;
            running = false;

            // Завершаем все ожидающие задачи
            foreach (var handlersList in waitingHandlers.Select(kvp => kvp.Value))
            {
                lock (handlersList)
                {
                    foreach (var handler in handlersList)
                    {
                        try
                        {
                            // Передаем ObjectDisposedException, чтобы завершить ожидающую задачу
                            handler(new ObjectDisposedException(nameof(MessageBus)));
                        }
                        catch
                        {
                            // Игнорируем исключения при завершении
                        }
                    }

                    handlersList.Clear();
                }
            }

            for (int i = 0; i < workers.Length; i++)
                signal.Set();

            foreach (var t in workers)
                t.Join();

            signal.Dispose();
            Channels.TryRemove(Name, out _);
        }

        /// <summary>
        /// Публикует сообщение в шину.
        /// </summary>
        /// <typeparam name="T">Тип сообщения.</typeparam>
        /// <param name="message">Сообщение для публикации.</param>
        /// <exception cref="ObjectDisposedException">Если шина уже освобождена.</exception>
        public void Publish<T>(T message)
        {
            if (!running)
                throw new ObjectDisposedException(nameof(MessageBus));

            queue.Enqueue(message);
            signal.Set();
        }

        /// <summary>
        /// Подписывает обработчик на сообщения указанного типа с необязательным фильтром.
        /// </summary>
        /// <typeparam name="T">Тип сообщения.</typeparam>
        /// <param name="handler">Делегат для обработки сообщений.</param>
        /// <param name="messageFilter">Фильтр сообщений. Если <see langword="null"/>, вызывается для всех сообщений типа <typeparamref name="T"/>.</param>
        /// <exception cref="ArgumentNullException">Если <paramref name="handler"/> равен <see langword="null"/>.</exception>
        public void Subscribe<T>(Action<T> handler, Func<T, bool> messageFilter = null)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            Action<T> wrapped = message =>
            {
                if (messageFilter == null || messageFilter(message))
                {
                    handler(message);
                }
            };

            var list = handlers.GetOrAdd(typeof(T), _ => new List<Delegate>());
            lock (list)
            {
                list.Add(wrapped);
                wrappedHandlers[handler] = wrapped;
            }
        }

        /// <summary>
        /// Подписывает обработчик на сообщения указанного типа с выполнением в указанном контексте синхронизации и необязательным фильтром.
        /// </summary>
        /// <typeparam name="T">Тип сообщения.</typeparam>
        /// <param name="handler">Делегат для обработки сообщений.</param>
        /// <param name="context">Контекст синхронизации. Если <see langword="null"/>, используется обычная подписка.</param>
        /// <param name="messageFilter">Фильтр сообщений. Если <see langword="null"/>, вызывается для всех сообщений типа <typeparamref name="T"/>.</param>
        /// <exception cref="ArgumentNullException">Если <paramref name="handler"/> равен <see langword="null"/>.</exception>
        public void Subscribe<T>(Action<T> handler, SynchronizationContext context, Func<T, bool> messageFilter = null)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            if (context == null)
            {
                Subscribe(handler, messageFilter);
                return;
            }

            Action<T> wrapped = message =>
            {
                if (messageFilter != null && !messageFilter(message))
                    return;

                context.Post(
                    state =>
                    {
                        var data = (Tuple<Action<T>, T>)state;
                        data.Item1(data.Item2);
                    },
                    Tuple.Create(handler, message));
            };

            var list = handlers.GetOrAdd(typeof(T), _ => new List<Delegate>());
            lock (list)
            {
                list.Add(wrapped);
                wrappedHandlers[handler] = wrapped;
            }
        }

        /// <summary>
        /// Отписывает обработчик от сообщений указанного типа.
        /// </summary>
        /// <typeparam name="T">Тип сообщения.</typeparam>
        /// <param name="handler">Делегат для отписки.</param>
        public void Unsubscribe<T>(Action<T> handler)
        {
            if (!handlers.TryGetValue(typeof(T), out var list))
                return;

            lock (list)
            {
                if (wrappedHandlers.TryRemove(handler, out var wrapped))
                {
                    list.Remove(wrapped);
                }
                else
                {
                    // fallback для старого поведения без фильтра
                    list.Remove(handler);
                }
            }
        }

        /// <summary>
        /// Асинхронно ожидает публикации сообщения указанного типа с возможностью фильтрации, таймаута и отмены.
        /// </summary>
        /// <typeparam name="T">Тип сообщения.</typeparam>
        /// <param name="messageFilter">Фильтр сообщений. Если <see langword="null"/>, принимается любое сообщение типа <typeparamref name="T"/>.</param>
        /// <param name="timeout">Максимальное время ожидания в миллисекундах. Если <see langword="null"/>, ожидание неограниченно.</param>
        /// <param name="cancellationToken">Токен отмены.</param>
        /// <returns>Задача, которая завершится после получения сообщения или отмены.</returns>
        /// <exception cref="ObjectDisposedException">Если шина уже освобождена.</exception>
        public Task<T> WaitForMessage<T>(
            Func<T, bool> messageFilter = null,
            int? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (!running)
                throw new ObjectDisposedException(nameof(MessageBus));

            var tcs = new TaskCompletionSource<T>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            var type = typeof(T);

            Action<object> waitingHandler = null;
            CancellationTokenSource linkedCts = null;
            CancellationTokenRegistration ctr = default;

            waitingHandler = obj =>
            {
                if (!running)
                {
                    tcs.TrySetException(new ObjectDisposedException(nameof(MessageBus)));
                    return;
                }

                if (obj is ObjectDisposedException ex)
                {
                    tcs.TrySetException(ex);
                    return;
                }

                if (!(obj is T message))
                    return;

                if (messageFilter != null && !messageFilter(message))
                    return;

                Cleanup();
                tcs.TrySetResult(message);
            };

            var list = waitingHandlers.GetOrAdd(type, _ => new List<Action<object>>());
            lock (list)
            {
                list.Add(waitingHandler);
            }

            if (timeout.HasValue)
            {
                linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                linkedCts.CancelAfter(timeout.Value);
                cancellationToken = linkedCts.Token;
            }

            if (cancellationToken.CanBeCanceled)
            {
                ctr = cancellationToken.Register(() =>
                {
                    Cleanup();
                    tcs.TrySetCanceled(cancellationToken);
                });
            }

            void Cleanup()
            {
                if (waitingHandlers.TryGetValue(type, out var h))
                {
                    lock (h)
                    {
                        h.Remove(waitingHandler);
                    }
                }

                ctr.Dispose();
                linkedCts?.Dispose();
            }

            return tcs.Task;
        }

        /// <summary>
        /// Асинхронно ожидает публикации сообщения указанного типа и возвращает результат в указанном контексте синхронизации.
        /// </summary>
        /// <typeparam name="T">Тип сообщения.</typeparam>
        /// <param name="context">Контекст синхронизации, в котором будет завершена задача.</param>
        /// <param name="messageFilter">Фильтр сообщений.</param>
        /// <param name="timeout">Максимальное время ожидания в миллисекундах.</param>
        /// <param name="cancellationToken">Токен отмены.</param>
        /// <returns>Задача, которая завершится после получения сообщения, с учётом контекста синхронизации.</returns>
        /// <exception cref="ArgumentNullException">Если <paramref name="context"/> равен <see langword="null"/>.</exception>
        public Task<T> WaitForMessage<T>(
            SynchronizationContext context,
            Func<T, bool> messageFilter = null,
            int? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var tcs = new TaskCompletionSource<T>();

            var waitingTask = WaitForMessage<T>(messageFilter, timeout, cancellationToken);

            waitingTask.ContinueWith(
                task =>
            {
                context.Post(
                    state =>
                {
                    var t = (Task<T>)state;
                    if (t.IsCompleted)
                    {
                        tcs.TrySetResult(t.Result);
                    }
                    else if (t.IsCanceled)
                    {
                        tcs.TrySetCanceled();
                    }
                    else if (t.IsFaulted)
                    {
                        tcs.TrySetException(t.Exception.InnerExceptions);
                    }
                }, task);
            }, TaskContinuationOptions.ExecuteSynchronously);

            return tcs.Task;
        }

        private static void OnHandlerException(Exception ex, object message)
        {
            Debug.WriteLine($"{message}: {ex}");
        }

        private void Dispatch(object message)
        {
            var type = message.GetType();

            // Обычные подписчики
            if (handlers.TryGetValue(type, out var list))
            {
                Delegate[] snapshot;
                lock (list)
                {
                    snapshot = list.ToArray();
                }

                foreach (var handler in snapshot)
                {
                    try
                    {
                        handler.DynamicInvoke(message);
                    }
                    catch (Exception ex)
                    {
                        OnHandlerException(ex, message);
                    }
                }
            }

            // Ожидающие задачи
            if (waitingHandlers.TryGetValue(type, out var waitList))
            {
                Action<object>[] snapshot;
                lock (waitList)
                {
                    snapshot = waitList.ToArray();
                }

                foreach (var handler in snapshot)
                {
                    try
                    {
                        handler(message); // вызываем обработчик, фильтр внутри проверит тип/условие
                    }
                    catch (Exception ex)
                    {
                        OnHandlerException(ex, message);
                    }
                }
            }
        }

        private void WorkerLoop()
        {
            while (running)
            {
                if (!queue.TryDequeue(out var message))
                {
                    signal.WaitOne();
                    continue;
                }

                Dispatch(message);
            }

            while (queue.TryDequeue(out var msg))
            {
                Dispatch(msg);
            }
        }
    }
}