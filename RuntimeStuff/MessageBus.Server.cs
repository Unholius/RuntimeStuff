// <copyright file="MessageBus.Server.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace System
{
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.Helpers;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Сервер для MessageBus.
    /// Implements the <see cref="IDisposable" />.
    /// </summary>
    /// <seealso cref="IDisposable" />
    public sealed partial class MessageBus
    {
        private static readonly ConcurrentQueue<PendingMessage> MessageQueue =
            new();

        private static readonly object RetryLock = new();

        private static HttpClient httpClient;
        private static bool isProcessingQueue = false;
        private static int retryIntervalMs = 5000;
        private static Timer retryTimer;

        // Интервал повторных попыток по умолчанию
        private readonly ConcurrentDictionary<int, HttpListener> activeServers =
            new();

        private readonly CancellationTokenSource serverCts = new();

        /// <summary>
        /// Получает количество сообщений в очереди.
        /// </summary>
        public static int QueueLength => MessageQueue.Count;

        /// <summary>
        /// Очищает очередь сообщений без отправки.
        /// </summary>
        public static void ClearQueue()
        {
            while (MessageQueue.TryDequeue(out _))
            {
                // ignore
            }
        }

        /// <summary>
        /// Асинхронно отправляет все накопленные сообщения из очереди.
        /// </summary>
        /// <returns>A <see cref="Task"/>Representing the asynchronous operation.</returns>
        public static async Task FlushQueueAsync()
        {
            await ProcessMessageQueue();
        }

        /// <summary>
        /// Публикует сообщение на сервер с автоматическим повторением при неудаче.
        /// </summary>
        /// <typeparam name="T">Тип сообщения.</typeparam>
        /// <param name="serverUrl">Адрес сервера сообщений. <see cref="StartServer"/>.</param>
        /// <param name="message">Сообщение для публикации.</param>
        /// <returns>Task, представляющий асинхронную операцию.</returns>
        /// <exception cref="ObjectDisposedException">Если шина уже освобождена.</exception>
        public static async Task PublishAsync<T>(Uri serverUrl, T message)
        {
            if (!Channels.Values.All(bus => bus.running))
            {
                throw new ObjectDisposedException(nameof(MessageBus));
            }

            var pendingMessage = new PendingMessage
            {
                ServerUrl = serverUrl,
                Message = message,
                MessageType = typeof(T),
                Timestamp = DateTime.UtcNow,
                RetryCount = 0,
            };

            // Пытаемся отправить сразу
            if (await TrySendMessage(pendingMessage))
            {
                return; // Успешно отправлено
            }

            // Если не удалось, добавляем в очередь и запускаем обработчик
            MessageQueue.Enqueue(pendingMessage);
            StartQueueProcessor();
        }

        /// <summary>
        /// Устанавливает интервал повторных попыток отправки.
        /// </summary>
        /// <param name="intervalMs">Интервал в миллисекундах.</param>
        /// <exception cref="System.ArgumentOutOfRangeException">intervalMs - Interval must be at least 1000ms.</exception>
        public static void SetRetryInterval(int intervalMs)
        {
            if (intervalMs < 1000)
            {
                throw new ArgumentOutOfRangeException(nameof(intervalMs), @"Interval must be at least 1000ms");
            }

            retryIntervalMs = intervalMs;

            lock (RetryLock)
            {
                retryTimer?.Change(intervalMs, intervalMs);
            }
        }

        /// <summary>
        /// Запускает HTTP-сервер для приема сообщений через REST API.
        /// Сервер принимает POST-запросы на эндпоинт с JSON-телом сообщения.
        /// </summary>
        /// <param name="port">Порт для прослушивания.</param>
        /// <param name="path">Путь для эндпоинта (по умолчанию "/").</param>
        /// <returns>Task, представляющий асинхронную операцию сервера.</returns>
        /// <exception cref="ObjectDisposedException">Если шина уже освобождена.</exception>
        /// <exception cref="InvalidOperationException">Если сервер на указанном порту уже запущен.</exception>
        /// <exception cref="HttpListenerException">Если не удалось запустить HttpListener.</exception>
        public Task StartServer(int port, string path = "/")
        {
            if (!this.running)
            {
                throw new ObjectDisposedException(nameof(MessageBus));
            }

            if (this.activeServers.ContainsKey(port))
            {
                throw new InvalidOperationException($"Server already running on port {port}");
            }

            var listener = new HttpListener();
            listener.Prefixes.Add($"http://*:{port}{path}");
            listener.Prefixes.Add($"http://+:{port}{path}"); // + означает все интерфейсы

            try
            {
                listener.Start();
            }
            catch (HttpListenerException ex)
            {
                throw new HttpListenerException(
                    ex.ErrorCode,
                    $"Failed to start HTTP listener on port {port}. On Windows, you may need to run: netsh http add urlacl url=http://+:{port}/ user=Everyone");
            }

            if (!this.activeServers.TryAdd(port, listener))
            {
                listener.Stop();
                listener.Close();
                throw new InvalidOperationException($"Failed to register server on port {port}");
            }

            _ = Task.Run(
                async () =>
                {
                    try
                    {
                        while (!this.serverCts.Token.IsCancellationRequested && listener.IsListening)
                        {
                            var context = await listener.GetContextAsync();
                            _ = this.ProcessRequest(context);
                        }
                    }
                    catch (HttpListenerException)
                    {
                        // Нормальное завершение при остановке сервера
                    }
                    catch (OperationCanceledException)
                    {
                        // Отмена операции
                    }
                    catch (Exception ex)
                    {
                        OnHandlerException(ex, $"HTTP server on port {port}");
                    }
                    finally
                    {
                        this.activeServers.TryRemove(port, out _);
                    }
                },
                this.serverCts.Token);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Останавливает все запущенные HTTP-серверы.
        /// </summary>
        public void StopAllServers()
        {
            this.serverCts.Cancel();

            foreach (var port in this.activeServers.Keys.ToArray())
            {
                this.StopServer(port);
            }
        }

        /// <summary>
        /// Останавливает HTTP-сервер на указанном порту.
        /// </summary>
        /// <param name="port">Порт для остановки.</param>
        public void StopServer(int port)
        {
            if (this.activeServers.TryRemove(port, out var listener))
            {
                try
                {
                    listener.Stop();
                    listener.Close();
                }
                catch (ObjectDisposedException)
                {
                    // Уже освобожден
                }
            }
        }

        private static HttpClient GetHttpClient()
        {
            if (httpClient != null)
            {
                return httpClient;
            }

            httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30),
            };
            return httpClient;
        }

        private static async Task ProcessMessageQueue()
        {
            if (MessageQueue.IsEmpty)
            {
                return;
            }

            var failedMessages = new ConcurrentQueue<PendingMessage>();

            // Обрабатываем все сообщения в очереди
            while (MessageQueue.TryDequeue(out var pendingMessage))
            {
                if (await TrySendMessage(pendingMessage))
                {
                    // Успешно отправлено
                    Debug.WriteLine($"Successfully sent message {pendingMessage.MessageId} after {pendingMessage.RetryCount} retries");
                }
                else
                {
                    pendingMessage.RetryCount++;

                    // Максимальное количество попыток - без ограничений, но можно добавить лимит
                    failedMessages.Enqueue(pendingMessage);
                }
            }

            // Возвращаем неудавшиеся сообщения обратно в очередь
            while (failedMessages.TryDequeue(out var failedMessage))
            {
                MessageQueue.Enqueue(failedMessage);
            }

            // Если очередь пуста, останавливаем таймер
            if (MessageQueue.IsEmpty)
            {
                lock (RetryLock)
                {
                    retryTimer?.Dispose();
                    retryTimer = null;
                    isProcessingQueue = false;
                }
            }
        }

        private static void StartQueueProcessor()
        {
            lock (RetryLock)
            {
                if (isProcessingQueue)
                {
                    return;
                }

                isProcessingQueue = true;

                // Запускаем немедленную обработку
                Task.Run(async () => await ProcessMessageQueue());

                // Настраиваем периодический таймер
                retryTimer = new Timer(
                    async _ => await ProcessMessageQueue(),
                    null,
                    retryIntervalMs,
                    retryIntervalMs);
            }
        }

        /// <summary>
        /// Tries the send message.
        /// </summary>
        /// <param name="pendingMessage">The pending message.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        private static async Task<bool> TrySendMessage(PendingMessage pendingMessage)
        {
            try
            {
                var client = GetHttpClient();
                var response = await client.PostAsync(
                    pendingMessage.ServerUrl.ToString(),
                    new { type = pendingMessage.MessageType, data = pendingMessage.Message });
                var status = JsonHelper.GetValues(response, (x) => x.Equals("status"), false);
                if (status.FirstOrDefault()?.ToLower() == "accepted")
                {
                    return true;
                }

                client.Dispose();
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to send message {pendingMessage.MessageId}: {ex.Message}");
                return false;
            }
        }

        private async Task ProcessRequest(HttpListenerContext context)
        {
            try
            {
                var request = context.Request;
                var response = context.Response;

                if (request.HttpMethod != "POST")
                {
                    response.StatusCode = 405; // Method Not Allowed
                    response.Close();
                    return;
                }

                var contentType = request.ContentType ?? string.Empty;
                if (!contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
                {
                    response.StatusCode = 415; // Unsupported Media Type
                    response.Close();
                    return;
                }

                // Читаем JSON из тела запроса
                using (var reader = new System.IO.StreamReader(request.InputStream, request.ContentEncoding))
                {
                    var json = await reader.ReadToEndAsync();

                    var typeElement = JsonHelper.GetValues(json, (x) => x.Equals("type", StringComparison.CurrentCultureIgnoreCase)).FirstOrDefault();
                    var dataElement = JsonHelper.GetValues(json, (x) => x.Equals("data", StringComparison.CurrentCultureIgnoreCase)).FirstOrDefault();
                    if (string.IsNullOrWhiteSpace(typeElement) || string.IsNullOrWhiteSpace(dataElement))
                    {
                        response.StatusCode = 400; // Bad Request
                        response.Close();
                        return;
                    }

                    var messageType = Obj.GetTypeByName(typeElement);
                    if (messageType == null)
                    {
                        response.StatusCode = 400;
                        response.Close();
                        return;
                    }

                    var message = Obj.New(messageType);
                    var attributes = JsonHelper.GetAttributes(json, "data", false);
                    if (attributes.Length == 0)
                    {
                        message = dataElement;
                    }
                    else
                    {
                        foreach (var a in attributes)
                        {
                            foreach (var kvp in a)
                            {
                                Obj.Set(message, kvp.Key, kvp.Value);
                            }
                        }
                    }

                    // Публикуем сообщение в шину
                    var publishMethod = typeof(MessageBus)
                        .GetMethod(nameof(this.Publish))
                        ?.MakeGenericMethod(messageType);

                    publishMethod?.Invoke(this, [message]);

                    response.StatusCode = 202; // Accepted
                    response.ContentType = "application/json";

                    var successResponse = new
                    {
                        status = "accepted",
                        timestamp_utc = DateTime.UtcNow,
                        timestamp_moscow = DateTime.Now.ExactNow(),
                    };
                    var responseJson = JsonHelper.Serialize(successResponse, "yyyy-MM-ddTHH:mm:ss.fff");
                    var buffer = System.Text.Encoding.UTF8.GetBytes(responseJson);
                    response.ContentLength64 = buffer.Length;
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    response.Close();
                }
            }
            catch (Exception ex)
            {
                OnHandlerException(ex, context.Request.RemoteEndPoint?.ToString());

                try
                {
                    context.Response.StatusCode = 500;
                    context.Response.Close();
                }
                catch
                {
                    // Игнорируем ошибки при отправке ответа об ошибке
                }
            }
        }

        /// <summary>
        /// Класс для хранения отложенного сообщения.
        /// </summary>
        private sealed class PendingMessage
        {
            public object Message { get; set; }

            public string MessageId { get; set; } = Guid.NewGuid().ToString();

            public Type MessageType { get; set; }

            public int RetryCount { get; set; }

            public Uri ServerUrl { get; set; }

            public DateTime Timestamp { get; set; }
        }
    }
}