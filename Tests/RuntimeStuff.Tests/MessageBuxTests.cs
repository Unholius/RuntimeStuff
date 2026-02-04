// <copyright file="MessageBusTests.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

using System.Diagnostics;

namespace RuntimeStuff.MSTests
{
    [TestClass]
    public class MessageBusTests
    {
        #region Тесты конструктора

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void Constructor_ZeroWorkerCount_ThrowsArgumentOutOfRangeException()
        {
            // Act
            var bus = new MessageBus(workerCount: 0);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void Constructor_NegativeWorkerCount_ThrowsArgumentOutOfRangeException()
        {
            // Act
            var bus = new MessageBus(workerCount: -1);
        }

        [TestMethod]
        public void Constructor_ValidWorkerCount_CreatesInstance()
        {
            // Act
            var bus = new MessageBus(workerCount: 3);

            // Assert
            Assert.IsNotNull(bus);
        }

        [TestMethod]
        public void Constructor_WithThreadName_SetsThreadName()
        {
            // Arrange
            string threadName = "TestWorker";

            // Act
            var bus = new MessageBus(threadName: threadName, workerCount: 1);

            // Assert
            // Не можем напрямую проверить имя потока, но убедимся что экземпляр создан
            Assert.IsNotNull(bus);
        }

        [TestMethod]
        public void Default_Instance_Created()
        {
            // Assert
            Assert.IsNotNull(MessageBus.MultiThreaded);
            Assert.AreNotSame(MessageBus.MultiThreaded, MessageBus.SingleThreaded);
        }

        [TestMethod]
        public void Global_Instance_Created()
        {
            // Assert
            Assert.IsNotNull(MessageBus.SingleThreaded);
        }

        #endregion

        #region Тесты Publish

        [TestMethod]
        [ExpectedException(typeof(ObjectDisposedException))]
        public void Publish_AfterDispose_ThrowsObjectDisposedException()
        {
            // Arrange
            var bus = new MessageBus(workerCount: 1);
            bus.Dispose();

            // Act
            bus.Publish(new TestMessage { Id = 1 });
        }

        [TestMethod]
        public void Publish_ValidMessage_DoesNotThrow()
        {
            // Arrange
            using var bus = new MessageBus(workerCount: 1);
            var message = new TestMessage { Id = 1 };

            // Act
            bus.Publish(message);

            // Assert
            // Если не было исключения - тест пройден
            Assert.IsTrue(true);
        }

        [TestMethod]
        public void Publish_NullMessage_DoesNotThrow()
        {
            // Arrange
            using var bus = new MessageBus(workerCount: 1);

            // Act
            bus.Publish<object>(null);

            // Assert
            // Если не было исключения - тест пройден
            Assert.IsTrue(true);
        }

        #endregion

        #region Тесты Subscribe и Unsubscribe

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Subscribe_NullHandler_ThrowsArgumentNullException()
        {
            // Arrange
            using var bus = new MessageBus(workerCount: 1);

            // Act
            bus.Subscribe<TestMessage>(null);
        }

        [TestMethod]
        public void Subscribe_ValidHandler_ReceivesMessages()
        {
            // Arrange
            using var bus = new MessageBus(workerCount: 1);
            var receivedMessages = new List<TestMessage>();
            var message = new TestMessage { Id = 1 };

            bus.Subscribe<TestMessage>(msg => receivedMessages.Add(msg));

            // Act
            bus.Publish(message);
            Thread.Sleep(50); // Даем время на обработку

            // Assert
            Assert.AreEqual(1, receivedMessages.Count);
            Assert.AreEqual(message.Id, receivedMessages[0].Id);
        }

        [TestMethod]
        public void Subscribe_MultipleHandlers_AllReceiveMessages()
        {
            // Arrange
            using var bus = new MessageBus(workerCount: 1);
            var count1 = 0;
            var count2 = 0;
            var message = new TestMessage { Id = 1 };

            bus.Subscribe<TestMessage>(msg => count1++);
            bus.Subscribe<TestMessage>(msg => count2++);

            // Act
            bus.Publish(message);
            Thread.Sleep(50);

            // Assert
            Assert.AreEqual(1, count1);
            Assert.AreEqual(1, count2);
        }

        [TestMethod]
        public void Unsubscribe_Handler_StopsReceivingMessages()
        {
            // Arrange
            using var bus = new MessageBus(workerCount: 1);
            var receivedCount = 0;
            var message = new TestMessage { Id = 1 };

            void Handler(TestMessage msg) => receivedCount++;

            bus.Subscribe<TestMessage>(Handler);

            // Act - публикуем, отписываемся, публикуем снова
            bus.Publish(message);
            Thread.Sleep(50);
            bus.Unsubscribe<TestMessage>(Handler);
            bus.Publish(message);
            Thread.Sleep(50);

            // Assert
            Assert.AreEqual(1, receivedCount); // Только первое сообщение должно быть обработано
        }

        [TestMethod]
        public void Unsubscribe_NonExistentHandler_DoesNothing()
        {
            // Arrange
            using var bus = new MessageBus(workerCount: 1);
            var receivedCount = 0;
            var message = new TestMessage { Id = 1 };

            void Handler1(TestMessage msg) => receivedCount++;
            void Handler2(TestMessage msg) { /* другая реализация */ }

            bus.Subscribe<TestMessage>(Handler1);

            // Act
            bus.Unsubscribe<TestMessage>(Handler2); // Отписываем несуществующий обработчик
            bus.Publish(message);
            Thread.Sleep(50);

            // Assert
            Assert.AreEqual(1, receivedCount); // Обработчик все еще должен работать
        }

        #endregion

        #region Тесты Subscribe с SynchronizationContext

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Subscribe_WithContext_NullHandler_ThrowsArgumentNullException()
        {
            // Arrange
            using var bus = new MessageBus(workerCount: 1);
            var context = new SynchronizationContext();

            // Act
            bus.Subscribe<TestMessage>(null, context);
        }

        [TestMethod]
        public void Subscribe_WithNullContext_WorksLikeNormalSubscribe()
        {
            // Arrange
            using var bus = new MessageBus(workerCount: 1);
            var receivedMessages = new List<TestMessage>();
            var message = new TestMessage { Id = 1 };

            // Act
            bus.Subscribe<TestMessage>(msg => receivedMessages.Add(msg), null);
            bus.Publish(message);
            Thread.Sleep(50);

            // Assert
            Assert.AreEqual(1, receivedMessages.Count);
        }

        #endregion

        #region Тесты WaitForMessage

        [TestMethod]
        [ExpectedException(typeof(ObjectDisposedException))]
        public async Task WaitForMessage_AfterDispose_ThrowsObjectDisposedException()
        {
            // Arrange
            var bus = new MessageBus(workerCount: 1);
            bus.Dispose();

            // Act
            await bus.WaitForMessage<TestMessage>();
        }

        [TestMethod]
        public async Task WaitForMessage_SimpleWait_ReceivesMessage()
        {
            // Arrange
            using var bus = new MessageBus(workerCount: 1);
            var message = new TestMessage { Id = 42 };

            // Act
            var waitTask = bus.WaitForMessage<TestMessage>();
            await Task.Delay(10); // Небольшая задержка чтобы гарантировать что WaitForMessage подписался
            bus.Publish(message);

            var result = await waitTask;

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(42, result.Id);
        }

        [TestMethod]
        public async Task WaitForMessage_WithFilter_ReceivesFilteredMessage()
        {
            // Arrange
            using var bus = new MessageBus(workerCount: 1);

            // Act & Assert
            var waitTask = bus.WaitForMessage<TestMessage>(
                messageFilter: msg => msg.Id == 42);

            // Публикуем сообщение, которое НЕ должно быть принято
            bus.Publish(new TestMessage { Id = 1 });
            await Task.Delay(50);

            // Убеждаемся что задача все еще ожидает
            Assert.IsFalse(waitTask.IsCompleted);

            // Публикуем сообщение, которое ДОЛЖНО быть принято
            bus.Publish(new TestMessage { Id = 42 });

            var result = await waitTask;
            Assert.AreEqual(42, result.Id);
        }

        [TestMethod]
        public async Task WaitForMessage_WithTimeout_ThrowsTaskCanceledException()
        {
            // Arrange
            using var bus = new MessageBus(workerCount: 1);

            // Act
            var waitTask = bus.WaitForMessage<TestMessage>(timeout: 50);

            // Assert
            await Assert.ThrowsExceptionAsync<TaskCanceledException>(() => waitTask);
        }

        [TestMethod]
        public async Task WaitForMessage_WithCancellationToken_CancelsProperly()
        {
            // Arrange
            using var bus = new MessageBus(workerCount: 1);
            using var cts = new CancellationTokenSource();

            // Act
            var waitTask = bus.WaitForMessage<TestMessage>(
                timeout: null,
                cancellationToken: cts.Token);

            // Отменяем через 50 мс
            cts.CancelAfter(50);

            // Assert
            await Assert.ThrowsExceptionAsync<TaskCanceledException>(() => waitTask);
        }

        [TestMethod]
        public async Task WaitForMessage_MultipleWaiters_AllReceiveMessage()
        {
            // Arrange
            using var bus = new MessageBus(workerCount: 1);
            var message = new TestMessage { Id = 1 };

            // Act
            var waiter1 = bus.WaitForMessage<TestMessage>();
            var waiter2 = bus.WaitForMessage<TestMessage>();
            var waiter3 = bus.WaitForMessage<TestMessage>(
                messageFilter: msg => msg.Id == 1); // С фильтром

            await Task.Delay(10); // Гарантируем что все подписались

            bus.Publish(message);

            var results = await Task.WhenAll(waiter1, waiter2, waiter3);

            // Assert
            Assert.AreEqual(3, results.Length);
            foreach (var result in results)
            {
                Assert.AreEqual(1, result.Id);
            }
        }

        [TestMethod]
        public async Task WaitForMessage_AfterMessagePublished_DoesNotReceiveOldMessage()
        {
            // Arrange
            using var bus = new MessageBus(workerCount: 1);
            var message = new TestMessage { Id = 1 };

            // Act
            bus.Publish(message);
            await Task.Delay(50); // Даем время на обработку

            var waitTask = bus.WaitForMessage<TestMessage>(timeout: 100);

            // Assert
            await Assert.ThrowsExceptionAsync<TaskCanceledException>(() => waitTask);
        }

        [TestMethod]
        public async Task WaitForMessage_ExceptionInFilter_DoesNotCompleteTask()
        {
            // Arrange
            using var bus = new MessageBus(workerCount: 1);
            var exceptionThrown = false;

            // Перехватываем исключения через Debug (не идеально, но работает)
            // В реальном коде нужно было бы добавить событие для мониторинга исключений

            // Act
            var waitTask = bus.WaitForMessage<TestMessage>(
                messageFilter: msg =>
                {
                    if (msg.Id == 1)
                        throw new InvalidOperationException("Test exception");
                    return true;
                });

            // Публикуем сообщение, которое вызывает исключение в фильтре
            bus.Publish(new TestMessage { Id = 1 });
            await Task.Delay(50);

            // Задача все еще должна ожидать
            Assert.IsFalse(waitTask.IsCompleted);

            // Публикуем сообщение, которое не вызывает исключение
            bus.Publish(new TestMessage { Id = 2 });

            var result = await waitTask;
            Assert.AreEqual(2, result.Id);
        }

        #endregion

        #region Тесты многопоточности

        [TestMethod]
        public void MultipleThreads_PublishAndSubscribe_ThreadSafe()
        {
            // Arrange
            using var bus = new MessageBus(workerCount: 4);
            var receivedCount = 0;
            var lockObj = new object();

            bus.Subscribe<TestMessage>(msg =>
            {
                lock (lockObj)
                {
                    receivedCount++;
                }
            });

            // Act
            var threads = new List<Thread>();
            for (int i = 0; i < 10; i++)
            {
                var thread = new Thread(() =>
                {
                    for (int j = 0; j < 100; j++)
                    {
                        bus.Publish(new TestMessage { Id = j });
                    }
                });
                threads.Add(thread);
                thread.Start();
            }

            foreach (var thread in threads)
            {
                thread.Join();
            }

            // Даем время на обработку всех сообщений
            Thread.Sleep(500);

            // Assert
            Assert.AreEqual(1000, receivedCount); // 10 потоков * 100 сообщений
        }

        [TestMethod]
        public async Task Concurrent_WaitForMessage_WorksCorrectly()
        {
            // Arrange
            using var bus = new MessageBus(workerCount: 4);
            const int taskCount = 20;
            var tasks = new Task<TestMessage>[taskCount];

            // Act
            for (int i = 0; i < taskCount; i++)
            {
                int taskId = i;
                tasks[taskId] = bus.WaitForMessage<TestMessage>(
                    messageFilter: msg => msg.Id == taskId);
            }

            await Task.Delay(50); // Даем время всем подписаться

            // Публикуем сообщения в обратном порядке
            for (int i = taskCount - 1; i >= 0; i--)
            {
                bus.Publish(new TestMessage { Id = i });
                await Task.Delay(10);
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            Assert.AreEqual(taskCount, results.Length);
            for (int i = 0; i < taskCount; i++)
            {
                Assert.AreEqual(i, results[i].Id);
            }
        }

        #endregion

        #region Тесты Dispose

        [TestMethod]
        public void Dispose_MultipleTimes_DoesNotThrow()
        {
            // Arrange
            var bus = new MessageBus(workerCount: 1);

            // Act
            bus.Dispose();
            bus.Dispose(); // Второй вызов

            // Assert
            // Если не было исключения - тест пройден
            Assert.IsTrue(true);
        }

        [TestMethod]
        public void Dispose_CompletesRemainingMessages()
        {
            // Arrange
            var bus = new MessageBus(workerCount: 1);
            var receivedMessages = new List<TestMessage>();

            bus.Subscribe<TestMessage>(msg => receivedMessages.Add(msg));

            // Act
            bus.Publish(new TestMessage { Id = 1 });
            bus.Publish(new TestMessage { Id = 2 });
            bus.Publish(new TestMessage { Id = 3 });

            bus.Dispose();

            // Даем небольшое время на завершение обработки
            Thread.Sleep(100);

            // Assert
            Assert.AreEqual(3, receivedMessages.Count);
        }

        [TestMethod]
        public async Task Dispose_WhileWaiting_ThrowsObjectDisposedException()
        {
            // Arrange
            var bus = new MessageBus(workerCount: 1);
            var waitTask = bus.WaitForMessage<TestMessage>();

            // Act
            bus.Dispose();

            // Assert
            // WaitForMessage должен завершиться с исключением
            await Assert.ThrowsExceptionAsync<ObjectDisposedException>(() => waitTask);
        }

        // Тест на обработку исключений в подписчиках
        [TestMethod]
        public void Subscribe_HandlerThrowsException_DoesNotBreakBus()
        {
            // Arrange
            using var bus = new MessageBus(workerCount: 1);
            var receivedMessages = new List<TestMessage>();

            bus.Subscribe<TestMessage>(msg =>
            {
                throw new InvalidOperationException("Test exception");
            });

            bus.Subscribe<TestMessage>(msg => receivedMessages.Add(msg));

            // Act
            bus.Publish(new TestMessage { Id = 1 });
            Thread.Sleep(50);

            // Assert
            Assert.AreEqual(1, receivedMessages.Count); // Второй обработчик должен сработать
        }

        // Тест на производительность
        /// <summary>
        /// Defines the test method Performance_PublishManyMessages_HandlesCorrectly.
        /// </summary>
        [TestMethod]
        public void Performance_PublishManyMessages_HandlesCorrectly()
        {
            // Arrange
            using var bus = new MessageBus(workerCount: 4);
            var count = 0;
            var lockObj = new object();

            bus.Subscribe<TestMessage>(msg =>
            {
                lock (lockObj) count++;
            });

            // Act
            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < 10000; i++)
            {
                bus.Publish(new TestMessage { Id = i });
            }

            // Даем время на обработку
            Thread.Sleep(1000);
            stopwatch.Stop();

            // Assert
            Assert.AreEqual(10000, count);
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 2000,
                $"Обработка заняла {stopwatch.ElapsedMilliseconds} мс");
        }

        #endregion

        #region Вспомогательные классы

        private class TestMessage
        {
            public int Id { get; set; }
            public string Text { get; set; } = "Test";
        }

        private class AnotherMessage
        {
            public string Data { get; set; }
        }

        #endregion
    }
}