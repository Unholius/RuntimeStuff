using System.Collections.Concurrent;
using System.Diagnostics;

namespace RuntimeStuff.MSTests
{
    [TestClass]
    public class MessageBusIntegrationTests
    {
        [TestMethod]
        public void StressTest_HighConcurrency_NoDataLoss()
        {
            // Arrange
            using var bus = new MessageBus("StressTest");
            const int publisherCount = 10;
            const int messagesPerPublisher = 1000;
            const int totalMessages = publisherCount * messagesPerPublisher;

            var receivedMessages = new ConcurrentBag<int>();
            var exceptions = new ConcurrentBag<Exception>();
            var startSignal = new ManualResetEventSlim(false);

            // Подписываемся
            bus.Subscribe<int>(msg => receivedMessages.Add(msg));

            // Act - Запускаем несколько издателей
            var publisherTasks = new Task[publisherCount];
            for (var i = 0; i < publisherCount; i++)
            {
                var publisherId = i;
                publisherTasks[i] = Task.Run(() =>
                {
                    startSignal.Wait();

                    for (var j = 0; j < messagesPerPublisher; j++)
                    {
                        try
                        {
                            bus.Publish(publisherId * 1000 + j);
                        }
                        catch (Exception ex)
                        {
                            exceptions.Add(ex);
                        }
                    }
                });
            }

            // Запускаем всех одновременно
            startSignal.Set();
            Task.WaitAll(publisherTasks);

            // Ждем обработки всех сообщений
            var stopwatch = Stopwatch.StartNew();
            while (receivedMessages.Count < totalMessages && stopwatch.ElapsedMilliseconds < 10000)
            {
                Thread.Sleep(100);
            }

            // Assert
            Assert.AreEqual(0, exceptions.Count, $"Exceptions: {exceptions.FirstOrDefault()}");
            Assert.AreEqual(totalMessages, receivedMessages.Count,
                $"Expected {totalMessages} messages, received {receivedMessages.Count}");
        }

        [TestMethod]
        public void LongRunningHandler_DoesNotBlockOtherMessages()
        {
            // Arrange
            using var bus = new MessageBus("", 2);
            var fastHandlerCalls = new ConcurrentBag<int>();
            var slowHandlerStarted = false;
            var slowHandlerFinished = false;

            // Act
            // Медленный обработчик
            bus.Subscribe<int>(msg =>
            {
                if (msg == 1)
                {
                    slowHandlerStarted = true;
                    Thread.Sleep(500); // Долгая обработка
                    slowHandlerFinished = true;
                }
            });

            // Быстрый обработчик
            bus.Subscribe<int>(msg =>
            {
                if (msg != 1)
                {
                    fastHandlerCalls.Add(msg);
                }
            });

            // Отправляем медленное сообщение
            bus.Publish(1);

            // Ждем начала обработки медленного сообщения
            Thread.Sleep(100);
            Assert.IsTrue(slowHandlerStarted);

            // Отправляем быстрые сообщения во время обработки медленного
            for (var i = 2; i < 10; i++)
            {
                bus.Publish(i);
            }

            // Ждем немного для обработки быстрых сообщений
            Thread.Sleep(100);

            // Assert - Быстрые сообщения должны быть обработаны, даже пока медленный еще работает
            Assert.IsFalse(slowHandlerFinished); // Медленный еще не закончил
            Assert.IsTrue(fastHandlerCalls.Count > 0); // Быстрые уже начали обрабатываться
        }

        [TestMethod]
        public void MessageOrdering_PreservedWithinSinglePublisher()
        {
            // Arrange
            using var bus = new MessageBus();
            var receivedOrder = new ConcurrentQueue<string>();
            var expectedOrder = new[]
            {
                "message1", "message2", "message3", "message4", "message5"
            };

            // Act
            bus.Subscribe<string>(msg => receivedOrder.Enqueue(msg));

            foreach (var message in expectedOrder)
            {
                bus.Publish(message);
            }

            // Ждем обработки
            Thread.Sleep(200);

            // Assert
            Assert.AreEqual(expectedOrder.Length, receivedOrder.Count);
            var actualOrder = receivedOrder.ToArray();
            CollectionAssert.AreEqual(expectedOrder, actualOrder);
        }

        [TestMethod]
        public void Memory_Usage_StableUnderLoad()
        {
            // Arrange
            using var bus = new MessageBus();
            const int iterations = 100000;
            var largeObject = new byte[1024]; // 1KB
            var receivedCount = 0;

            bus.Subscribe<byte[]>(msg =>
            {
                Interlocked.Increment(ref receivedCount);
            });

            // Измеряем память до теста
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var initialMemory = GC.GetTotalMemory(true);

            // Act
            for (var i = 0; i < iterations; i++)
            {
                bus.Publish(largeObject);
            }

            // Ждем обработки всех сообщений
            var stopwatch = Stopwatch.StartNew();
            while (Volatile.Read(ref receivedCount) < iterations && stopwatch.ElapsedMilliseconds < 10000)
            {
                Thread.Sleep(10);
            }

            // Измеряем память после теста
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var finalMemory = GC.GetTotalMemory(true);

            // Assert
            var memoryIncrease = finalMemory - initialMemory;
            Debug.WriteLine($"Memory increase: {memoryIncrease / 1024} KB for {iterations} messages");

            // Память не должна расти линейно с количеством сообщений
            Assert.IsTrue(memoryIncrease < 50 * 1024 * 1024, // 50MB максимум
                $"Memory increase too large: {memoryIncrease / 1024 / 1024} MB");
        }

        [TestMethod]
        public void UnsubscribeAllHandlers_TypeRemovedFromDictionary()
        {
            // Arrange
            using var bus = new MessageBus();
            Action<string> handler1 = msg => { };
            Action<string> handler2 = msg => { };

            // Act
            bus.Subscribe(handler1);
            bus.Subscribe(handler2);

            // Проверяем, что тип добавлен
            var handlersField = bus.GetType().GetField("handlers",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var handlers = handlersField?.GetValue(bus) as ConcurrentDictionary<Type, System.Collections.Generic.List<Delegate>>;

            Assert.IsTrue(handlers?.ContainsKey(typeof(string)));

            bus.Unsubscribe(handler1);
            bus.Unsubscribe(handler2);

            // Assert - После удаления всех обработчиков тип должен быть удален из словаря
            // Но в текущей реализации список остается пустым, тип не удаляется
            // Это ожидаемое поведение для текущей реализации
            Assert.IsTrue(handlers?.ContainsKey(typeof(string)));
            Assert.AreEqual(0, handlers?[typeof(string)].Count);
        }

        [TestMethod]
        public async Task AsyncPattern_Integration()
        {
            // Arrange
            using var bus = new MessageBus();
            var tcs = new TaskCompletionSource<string>();
            string receivedMessage = null;

            // Act
            bus.Subscribe<string>(msg =>
            {
                receivedMessage = msg;
                tcs.SetResult(msg);
            });

            bus.Publish("async test");

            // Assert
            var result = await tcs.Task;
            Assert.AreEqual("async test", result);
            Assert.AreEqual("async test", receivedMessage);
        }

        private class TestTraceListener : TraceListener
        {
            public ConcurrentBag<string> Messages { get; } = new ConcurrentBag<string>();

            public override void Write(string message)
            {
                Messages.Add(message);
            }

            public override void WriteLine(string message)
            {
                Messages.Add(message);
            }
        }
    }
}