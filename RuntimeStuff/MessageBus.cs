// <copyright file="MessageBus.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace RuntimeStuff
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;

    /// <summary>
    /// v.2026.02.02 (RS) COPY-PASTE READY<br />
    /// Асинхронная потокобезопасная шина сообщений (Message Bus)
    /// с очередью и пулом рабочих потоков.
    /// </summary>
    /// <remarks>
    /// Класс предназначен для обмена сообщениями внутри процесса
    /// с асинхронной доставкой обработчикам.
    ///
    /// Архитектура:
    /// <list type="bullet">
    /// <item><description>сообщения помещаются в неблокирующую очередь;</description></item>
    /// <item><description>один или несколько фоновых потоков извлекают сообщения;</description></item>
    /// <item><description>обработчики вызываются асинхронно относительно издателя.</description></item>
    /// </list>
    ///
    /// Порядок обработки сообщений:
    /// <list type="bullet">
    /// <item><description>FIFO на уровне очереди;</description></item>
    /// <item><description>порядок вызова обработчиков одного сообщения сохраняется;</description></item>
    /// <item><description>между разными сообщениями возможен параллелизм.</description></item>
    /// </list>
    ///
    /// Исключения, возникающие в обработчиках,
    /// перехватываются и передаются в <see cref="OnHandlerException"/>,
    /// не влияя на работу шины.
    /// </remarks>
    public sealed class MessageBus : IDisposable
    {
        private readonly ConcurrentDictionary<Type, List<Delegate>> handlers =
            new ConcurrentDictionary<Type, List<Delegate>>();

        private readonly ConcurrentQueue<object> queue = new ConcurrentQueue<object>();
        private readonly AutoResetEvent signal = new AutoResetEvent(false);
        private readonly Thread[] workers;
        private volatile bool running = true;

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageBus"/> class.
        /// Инициализирует новый экземпляр <see cref="MessageBus"/>
        /// с указанным количеством рабочих потоков.
        /// </summary>
        /// <param name="workerCount">
        /// Количество фоновых потоков,
        /// обрабатывающих сообщения.
        /// </param>
        public MessageBus(int workerCount)
            : this("MessageBusWorker", workerCount)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageBus"/> class.
        /// Инициализирует новый экземпляр <see cref="MessageBus"/>
        /// с настраиваемым именем потоков и количеством воркеров.
        /// </summary>
        /// <param name="threadName">
        /// Базовое имя для рабочих потоков.
        /// </param>
        /// <param name="workerCount">
        /// Количество рабочих потоков.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Генерируется, если <paramref name="workerCount"/> меньше либо равен нулю.
        /// </exception>
        public MessageBus(string threadName = "MessageBusWorker", int workerCount = 1)
        {
            if (workerCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(workerCount));

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
        /// Глобальный экземпляр шины сообщений.
        /// </summary>
        /// <remarks>
        /// По умолчанию количество рабочих потоков
        /// равно <see cref="Environment.ProcessorCount"/>.
        /// </remarks>
        public static MessageBus Default { get; } =
            new MessageBus(workerCount: Environment.ProcessorCount);

        /// <summary>
        /// Глобальный экземпляр шины сообщений.
        /// </summary>
        /// <remarks>
        /// По умолчанию количество рабочих потоков равно 1.
        /// </remarks>
        public static MessageBus Global { get; } =
            new MessageBus(workerCount: 1);

        /// <summary>
        /// Освобождает ресурсы и корректно завершает работу шины сообщений.
        /// </summary>
        /// <remarks>
        /// Метод:
        /// <list type="bullet">
        /// <item><description>Останавливает приём новых сообщений;</description></item>
        /// <item><description>Будит все рабочие потоки;</description></item>
        /// <item><description>Дожидается завершения обработки очереди;</description></item>
        /// <item><description>Освобождает системные ресурсы.</description></item>
        /// </list>
        ///
        /// После вызова <see cref="Dispose"/> публикация сообщений невозможна.
        /// </remarks>
        public void Dispose()
        {
            running = false;

            for (int i = 0; i < workers.Length; i++)
                signal.Set();

            foreach (var t in workers)
                t.Join();

            signal.Dispose();
        }

        /// <summary>
        /// Публикует сообщение в очередь на асинхронную обработку.
        /// </summary>
        /// <typeparam name="T">
        /// Тип сообщения.
        /// </typeparam>
        /// <param name="message">
        /// Экземпляр сообщения.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Генерируется, если шина сообщений уже остановлена.
        /// </exception>
        public void Publish<T>(T message)
        {
            if (!running)
                throw new ObjectDisposedException(nameof(MessageBus));

            queue.Enqueue(message);
            signal.Set();
        }

        /// <summary>
        /// Подписывает обработчик на сообщения указанного типа.
        /// </summary>
        /// <typeparam name="T">
        /// Тип сообщения.
        /// </typeparam>
        /// <param name="handler">
        /// Делегат-обработчик сообщения.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Генерируется, если <paramref name="handler"/> равен <c>null</c>.
        /// </exception>
        public void Subscribe<T>(Action<T> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            var list = handlers.GetOrAdd(typeof(T), _ => new List<Delegate>());
            lock (list)
            {
                list.Add(handler);
            }
        }

        public void Subscribe<T>(Action<T> handler, SynchronizationContext context)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            if (context == null)
            {
                Subscribe(handler);
                return;
            }

            Action<T> wrapped = message =>
            {
                context.Post(
 state =>
 {
     var data = (Tuple<Action<T>, T>)state;
     data.Item1(data.Item2);
 },
                    Tuple.Create(handler, message));
            };

            var list = handlers.GetOrAdd(typeof(T), arg => new List<Delegate>());
            lock (list)
            {
                list.Add(wrapped);
            }
        }

        /// <summary>
        /// Отписывает обработчик от сообщений указанного типа.
        /// </summary>
        /// <typeparam name="T">
        /// Тип сообщения.
        /// </typeparam>
        /// <param name="handler">
        /// Ранее зарегистрированный обработчик.
        /// </param>
        public void Unsubscribe<T>(Action<T> handler)
        {
            if (!handlers.TryGetValue(typeof(T), out var list))
                return;

            lock (list)
            {
                list.Remove(handler);
            }
        }

        /// <summary>
        /// Обрабатывает исключения, возникшие в обработчиках сообщений.
        /// </summary>
        /// <param name="ex">
        /// Исключение, выброшенное обработчиком.
        /// </param>
        /// <param name="message">
        /// Сообщение, при обработке которого возникло исключение.
        /// </param>
        /// <remarks>
        /// По умолчанию исключения выводятся в <see cref="Debug"/>.
        /// Метод может быть расширен для логирования или мониторинга.
        /// </remarks>
        private static void OnHandlerException(Exception ex, object message)
        {
            Debug.WriteLine($"{message}: {ex}");
        }

        /// <summary>
        /// Выполняет доставку сообщения всем подписчикам.
        /// </summary>
        /// <param name="message">
        /// Обрабатываемое сообщение.
        /// </param>
        private void Dispatch(object message)
        {
            var type = message.GetType();

            if (!handlers.TryGetValue(type, out var list))
                return;

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

        /// <summary>
        /// Основной цикл рабочего потока.
        /// </summary>
        /// <remarks>
        /// Поток ожидает появления сообщений в очереди,
        /// извлекает их и передаёт на обработку.
        ///
        /// После остановки шины поток корректно
        /// обрабатывает оставшиеся сообщения в очереди.
        /// </remarks>
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

            // обработка оставшихся сообщений
            while (queue.TryDequeue(out var msg))
            {
                Dispatch(msg);
            }
        }
    }
}