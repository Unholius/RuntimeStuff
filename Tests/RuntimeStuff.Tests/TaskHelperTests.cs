// <copyright file="SyncHelperTests.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace System.MSTests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Collections.Generic;
    using System.Helpers;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Тестовый enum для использования в SyncHelper.
    /// </summary>
    public enum TestEventStatus
    {
        Success,
        Failure,
        Timeout,
        Cancelled,
        Pending
    }

    /// <summary>
    /// Тесты для класса <see cref="SyncHelper{T}"/>.
    /// </summary>
    [TestClass]
    public class SyncHelperTests
    {
        /// <summary>
        /// Генератор уникальных идентификаторов событий для изоляции тестов.
        /// </summary>
        private static int _eventIdCounter;

        private object _sync;

        /// <summary>
        /// Очистка состояния после каждого теста.
        /// </summary>
        [TestInitialize]
        [TestCleanup]
        public void Cleanup()
        {
            // Очищаем все ожидающие события после каждого теста
            SyncHelper<TestEventStatus>.ClearAll();
        }

        /// <summary>
        /// Создает уникальный идентификатор события для изоляции тестов.
        /// </summary>
        private string GetUniqueEventId([System.Runtime.CompilerServices.CallerMemberName] string testName = "")
        {
            return $"{testName}_{Interlocked.Increment(ref _eventIdCounter)}_{Guid.NewGuid():N}";
        }

        #region WaitAsync Tests

        ///// <summary>
        ///// Проверяет успешное ожидание и завершение события.
        ///// </summary>
        //[TestMethod]
        //public async Task WaitAsync_EventCompletedSuccessfully_ReturnsCompletedResult()
        //{
        //    // Arrange
        //    string eventId = GetUniqueEventId();
        //    var testData = new { Id = 1, Name = "Test" };

        //    // Act
        //    var waitTask = SyncHelper<TestEventStatus>.WaitAsync(eventId, TestEventStatus.Timeout, 5000);

        //    // Даем немного времени для регистрации ожидания
        //    await Task.Delay(50);

        //    var completed = SyncHelper<TestEventStatus>.TryComplete(eventId, TestEventStatus.Success, testData);
        //    var result = await waitTask;

        //    // Assert
        //    Assert.IsTrue(completed, "Событие должно быть успешно завершено");
        //    Assert.IsNotNull(result, "Результат не должен быть null");
        //    Assert.AreEqual(eventId, result.EventId, "Идентификатор события не совпадает");
        //    Assert.AreEqual(TestEventStatus.Success, result.Status, "Статус должен быть Success");
        //    Assert.AreEqual(testData, result.Data, "Данные не совпадают");
        //}

        ///// <summary>
        ///// Проверяет таймаут при ожидании события.
        ///// </summary>
        //[TestMethod]
        //public async Task WaitAsync_TimeoutOccurs_ReturnsTimeoutStatus()
        //{
        //    // Arrange
        //    string eventId = GetUniqueEventId();
        //    int timeoutMs = 100;

        //    // Act
        //    var stopwatch = Stopwatch.StartNew();
        //    var result = await SyncHelper<TestEventStatus>.WaitAsync(eventId, TestEventStatus.Timeout, timeoutMs);
        //    stopwatch.Stop();

        //    // Assert
        //    Assert.IsNotNull(result, "Результат не должен быть null");
        //    Assert.AreEqual(eventId, result.EventId, "Идентификатор события не совпадает");
        //    Assert.AreEqual(TestEventStatus.Timeout, result.Status, "Должен быть статус Timeout");

        //    // Проверяем, что таймаут сработал примерно через указанное время
        //    // Даем небольшой допуск на погрешность
        //    Assert.IsTrue(stopwatch.ElapsedMilliseconds >= timeoutMs - 50,
        //        $"Таймаут сработал слишком рано: {stopwatch.ElapsedMilliseconds}ms < {timeoutMs - 50}ms");
        //    Assert.IsTrue(stopwatch.ElapsedMilliseconds < timeoutMs * 2,
        //        $"Таймаут сработал слишком поздно: {stopwatch.ElapsedMilliseconds}ms > {timeoutMs * 2}ms");
        //}

        /// <summary>
        /// Проверяет отмену ожидания через CancellationToken.
        /// </summary>
        [TestMethod]
        public async Task WaitAsync_CancellationRequested_ThrowsTaskCanceledException()
        {
            // Arrange
            string eventId = GetUniqueEventId();
            var cts = new CancellationTokenSource();

            // Act
            var waitTask = SyncHelper<TestEventStatus>.WaitAsync(eventId, TestEventStatus.Timeout, 5000, cts.Token);

            cts.CancelAfter(50);

            // Assert
            await Assert.ThrowsExceptionAsync<TaskCanceledException>(async () => await waitTask);
        }

        /// <summary>
        /// Проверяет, что CancellationToken отменяет ожидание немедленно, если он уже отменен.
        /// </summary>
        [TestMethod]
        public async Task WaitAsync_WithAlreadyCancelledToken_ThrowsTaskCanceledException()
        {
            // Arrange
            string eventId = GetUniqueEventId();
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsExceptionAsync<TaskCanceledException>(
                async () => await SyncHelper<TestEventStatus>.WaitAsync(eventId, TestEventStatus.Timeout, 5000, cts.Token));
        }

        ///// <summary>
        ///// Проверяет повторное ожидание того же события - должен вернуться существующий Task.
        ///// </summary>
        //[TestMethod]
        //public async Task WaitAsync_SameEventIdTwice_ReturnsSameTask()
        //{
        //    // Arrange
        //    string eventId = GetUniqueEventId();

        //    // Act
        //    var task1 = SyncHelper<TestEventStatus>.WaitAsync(eventId, TestEventStatus.Timeout, 5000);
        //    var task2 = SyncHelper<TestEventStatus>.WaitAsync(eventId, TestEventStatus.Timeout, 5000);

        //    // Assert
        //    Assert.AreSame(task1, task2, "Должен вернуться тот же самый Task");

        //    // Cleanup
        //    SyncHelper<TestEventStatus>.CancelWait(eventId);

        //    try { await task1; } catch (TaskCanceledException) { }
        //}

        ///// <summary>
        ///// Проверяет передачу данных через EventResult.
        ///// </summary>
        //[TestMethod]
        //public async Task WaitAsync_WithEventData_ReturnsDataInResult()
        //{
        //    // Arrange
        //    string eventId = GetUniqueEventId();
        //    string expectedData = "test-data";

        //    // Act
        //    var waitTask = SyncHelper<TestEventStatus>.WaitAsync(eventId, TestEventStatus.Timeout, 5000);

        //    await Task.Delay(50);
        //    SyncHelper<TestEventStatus>.TryComplete(eventId, TestEventStatus.Success, expectedData);

        //    var result = await waitTask;

        //    // Assert
        //    Assert.IsNotNull(result, "Результат не должен быть null");
        //    Assert.AreEqual(expectedData, result.Data, "Данные не совпадают");

        //    // Проверяем деконструкцию
        //    var (id, status, data) = result;
        //    Assert.AreEqual(eventId, id);
        //    Assert.AreEqual(TestEventStatus.Success, status);
        //    Assert.AreEqual(expectedData, data);
        //}

        /// <summary>
        /// Проверяет исключение при null eventId.
        /// </summary>
        [TestMethod]
        public void WaitAsync_NullEventId_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(
                () => SyncHelper<TestEventStatus>.WaitAsync(null, TestEventStatus.Timeout, 100));
        }

        ///// <summary>
        ///// Проверяет исключение при неверном таймауте.
        ///// </summary>
        //[DataTestMethod]
        //[DataRow(0)]
        //[DataRow(-1)]
        //[DataRow(-100)]
        //public void WaitAsync_InvalidTimeout_ThrowsArgumentOutOfRangeException(int timeoutMs)
        //{
        //    // Act & Assert
        //    Assert.ThrowsException<ArgumentOutOfRangeException>(
        //        () => SyncHelper<TestEventStatus>.WaitAsync(GetUniqueEventId(), TestEventStatus.Timeout, timeoutMs));
        //}

        /// <summary>
        /// Проверяет, что Timeout.Infinite работает корректно.
        /// </summary>
        [TestMethod]
        public async Task WaitAsync_InfiniteTimeout_WaitsIndefinitely()
        {
            // Arrange
            string eventId = GetUniqueEventId();
            var cts = new CancellationTokenSource(100);

            // Act
            var waitTask = SyncHelper<TestEventStatus>.WaitAsync(eventId, TestEventStatus.Timeout, Timeout.Infinite, cts.Token);

            // Assert - должен отмениться по токену, а не по таймауту
            await Assert.ThrowsExceptionAsync<TaskCanceledException>(async () => await waitTask);
        }

        #endregion WaitAsync Tests

        #region CancelWait Tests

        /// <summary>
        /// Проверяет отмену несуществующего ожидания.
        /// </summary>
        [TestMethod]
        public void CancelWait_NonExistingEvent_ReturnsFalse()
        {
            // Act
            var result = SyncHelper<TestEventStatus>.CancelWait("non-existing-" + Guid.NewGuid());

            // Assert
            Assert.IsFalse(result, "Для несуществующего события должна вернуться false");
        }

        /// <summary>
        /// Проверяет исключение при null eventId в CancelWait.
        /// </summary>
        [TestMethod]
        public void CancelWait_NullEventId_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(
                () => SyncHelper<TestEventStatus>.CancelWait(null));
        }

        #endregion CancelWait Tests

        #region TryComplete Tests

        ///// <summary>
        ///// Проверяет успешное завершение события.
        ///// </summary>
        //[TestMethod]
        //public async Task TryComplete_ExistingEvent_ReturnsTrue()
        //{
        //    // Arrange
        //    string eventId = GetUniqueEventId();
        //    var waitTask = SyncHelper<TestEventStatus>.WaitAsync(eventId, TestEventStatus.Timeout, 5000);

        //    await Task.Delay(50);

        //    // Act
        //    var result = SyncHelper<TestEventStatus>.TryComplete(eventId, TestEventStatus.Success);

        //    // Assert
        //    Assert.IsTrue(result, "Завершение должно быть успешным");

        //    var waitResult = await waitTask;
        //    Assert.IsTrue(waitTask.IsCompleted, "Task должен быть завершен");
        //    Assert.AreEqual(TestEventStatus.Success, waitResult.Status);
        //}

        /// <summary>
        /// Проверяет попытку завершить несуществующее событие.
        /// </summary>
        [TestMethod]
        public void TryComplete_NonExistingEvent_ReturnsFalse()
        {
            // Act
            var result = SyncHelper<TestEventStatus>.TryComplete("non-existing-" + Guid.NewGuid(), TestEventStatus.Success);

            // Assert
            Assert.IsFalse(result, "Должна вернуться false для несуществующего события");
        }

        ///// <summary>
        ///// Проверяет, что повторное завершение события возвращает false.
        ///// </summary>
        //[TestMethod]
        //public async Task TryComplete_AlreadyCompletedEvent_ReturnsFalse()
        //{
        //    // Arrange
        //    string eventId = GetUniqueEventId();
        //    var waitTask = SyncHelper<TestEventStatus>.WaitAsync(eventId, TestEventStatus.Timeout, 5000);

        //    await Task.Delay(50);
        //    var firstComplete = SyncHelper<TestEventStatus>.TryComplete(eventId, TestEventStatus.Success);
        //    await waitTask;

        //    // Act
        //    var secondComplete = SyncHelper<TestEventStatus>.TryComplete(eventId, TestEventStatus.Success);

        //    // Assert
        //    Assert.IsTrue(firstComplete, "Первое завершение должно быть успешным");
        //    Assert.IsFalse(secondComplete, "Второе завершение должно вернуть false");
        //}

        #endregion TryComplete Tests

        #region EventParams Tests

        ///// <summary>
        ///// Проверяет установку и получение параметров события.
        ///// </summary>
        //[TestMethod]
        //public void EventParams_SetAndGet_ReturnsCorrectValue()
        //{
        //    // Arrange
        //    string eventId = GetUniqueEventId();
        //    string paramName = "testParam";
        //    int paramValue = 42;

        //    // Act
        //    SyncHelper<TestEventStatus>.SetEventParam(eventId, paramName, paramValue);
        //    var retrievedValue = SyncHelper<TestEventStatus>.GetEventParam(eventId, paramName);

        //    // Assert
        //    Assert.AreEqual(paramValue, retrievedValue, "Полученное значение должно совпадать с установленным");
        //}

        /// <summary>
        /// Проверяет типизированное получение параметров.
        /// </summary>
        //[TestMethod]
        //public void GetEventParam_Typed_ReturnsCorrectType()
        //{
        //    // Arrange
        //    string eventId = GetUniqueEventId();
        //    string paramName = "stringParam";
        //    string paramValue = "hello";

        //    // Act
        //    SyncHelper<TestEventStatus>.SetEventParam(eventId, paramName, paramValue);
        //    var retrievedValue = SyncHelper<TestEventStatus>.GetEventParam<string>(eventId, paramName);

        //    // Assert
        //    Assert.AreEqual(paramValue, retrievedValue);
        //    Assert.IsInstanceOfType(retrievedValue, typeof(string));
        //}

        /// <summary>
        /// Проверяет получение несуществующего параметра - возвращает значение по умолчанию.
        /// </summary>
        [TestMethod]
        public void GetEventParam_NonExistingParam_ReturnsDefaultValue()
        {
            // Arrange
            string eventId = GetUniqueEventId();
            int defaultValue = 100;

            // Act
            var result = SyncHelper<TestEventStatus>.GetEventParam<int>(eventId, "non-existing", defaultValue);

            // Assert
            Assert.AreEqual(defaultValue, result, "Должно вернуться значение по умолчанию");
        }

        /// <summary>
        /// Проверяет получение параметра с неверным типом - возвращает значение по умолчанию.
        /// </summary>
        [TestMethod]
        public void GetEventParam_WrongType_ReturnsDefaultValue()
        {
            // Arrange
            string eventId = GetUniqueEventId();
            string paramName = "intParam";
            int paramValue = 42;
            string defaultValue = "default";

            // Act
            SyncHelper<TestEventStatus>.SetEventParam(eventId, paramName, paramValue);

            // Пытаемся получить как строку
            var result = SyncHelper<TestEventStatus>.GetEventParam<string>(eventId, paramName, defaultValue);

            // Assert
            Assert.AreEqual(defaultValue, result, "При несоответствии типа должно вернуться значение по умолчанию");
        }

        /// <summary>
        /// Проверяет очистку параметров при отмене события.
        /// </summary>
        [TestMethod]
        public async Task EventParams_CleanedAfterCancellation()
        {
            // Arrange
            string eventId = GetUniqueEventId();
            string paramName = "testParam";

            // Act
            SyncHelper<TestEventStatus>.SetEventParam(eventId, paramName, "value");

            var waitTask = SyncHelper<TestEventStatus>.WaitAsync(eventId, TestEventStatus.Timeout, 5000);
            await Task.Delay(50);
            SyncHelper<TestEventStatus>.CancelWait(eventId);

            // Assert
            try
            {
                await waitTask;
            }
            catch (TaskCanceledException)
            {
                // Ожидаемое исключение
            }

            var hasParam = SyncHelper<TestEventStatus>.HasParam(eventId, paramName);
            Assert.IsFalse(hasParam, "Параметры должны быть очищены после отмены события");
        }

        /// <summary>
        /// Проверяет исключение при null eventId в SetEventParam.
        /// </summary>
        [TestMethod]
        public void SetEventParam_NullEventId_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(
                () => SyncHelper<TestEventStatus>.SetEventParam(null, "param", "value"));
        }

        ///// <summary>
        ///// Проверяет исключение при пустом имени параметра.
        ///// </summary>
        //[DataTestMethod]
        //[DataRow(null)]
        //[DataRow("")]
        //[DataRow(" ")]
        //public void SetEventParam_InvalidParamName_ThrowsArgumentException(string paramName)
        //{
        //    // Act & Assert
        //    Assert.ThrowsException<ArgumentException>(
        //        () => SyncHelper<TestEventStatus>.SetEventParam(GetUniqueEventId(), paramName, "value"));
        //}

        #endregion EventParams Tests

        #region GetActiveWaiters Tests

        ///// <summary>
        ///// Проверяет получение списка активных ожиданий.
        ///// </summary>
        //[TestMethod]
        //public void GetActiveWaiters_ReturnsCorrectList()
        //{
        //    // Arrange
        //    string eventId1 = GetUniqueEventId();
        //    string eventId2 = GetUniqueEventId();

        //    // Act
        //    SyncHelper<TestEventStatus>.WaitAsync(eventId1, TestEventStatus.Timeout, 5000);
        //    SyncHelper<TestEventStatus>.WaitAsync(eventId2, TestEventStatus.Timeout, 5000);

        //    var activeWaiters = SyncHelper<TestEventStatus>.GetActiveWaiters();

        //    // Assert
        //    Assert.IsTrue(activeWaiters.Count >= 2, "Должно быть 2 активных ожидания");
        //    CollectionAssert.Contains((System.Collections.ICollection)activeWaiters, eventId1);
        //    CollectionAssert.Contains((System.Collections.ICollection)activeWaiters, eventId2);

        //    // Cleanup
        //    SyncHelper<TestEventStatus>.ClearAll();
        //}

        ///// <summary>
        ///// Проверяет, что GetActiveWaiters возвращает копию, а не ссылку на внутреннюю коллекцию.
        ///// </summary>
        //[TestMethod]
        //public void GetActiveWaiters_ReturnsCopyOfCollection()
        //{
        //    // Arrange
        //    string eventId = GetUniqueEventId();
        //    SyncHelper<TestEventStatus>.WaitAsync(eventId, TestEventStatus.Timeout, 5000);

        //    // Act
        //    var activeWaiters = SyncHelper<TestEventStatus>.GetActiveWaiters();

        //    // Пытаемся изменить полученную коллекцию
        //    if (activeWaiters is List<string> list)
        //    {
        //        list.Clear();
        //    }

        //    // Assert
        //    var activeWaiters2 = SyncHelper<TestEventStatus>.GetActiveWaiters();
        //    Assert.AreEqual(1, activeWaiters2.Count, "Оригинальная коллекция не должна измениться");

        //    // Cleanup
        //    SyncHelper<TestEventStatus>.ClearAll();
        //}

        #endregion GetActiveWaiters Tests

        #region Metrics Tests

        ///// <summary>
        ///// Проверяет события метрик.
        ///// </summary>
        //[TestMethod]
        //public async Task Metrics_RaiseEventsCorrectly()
        //{
        //    // Arrange
        //    string eventId1 = GetUniqueEventId();
        //    string eventId2 = GetUniqueEventId();
        //    string eventId3 = GetUniqueEventId();

        //    var completedEvents = new List<string>();
        //    var cancelledEvents = new List<string>();
        //    var timedOutEvents = new List<string>();

        //    SyncHelper<TestEventStatus>.Metrics.WaiterCompleted += (s, e) => completedEvents.Add(e);
        //    SyncHelper<TestEventStatus>.Metrics.WaiterCancelled += (s, e) => cancelledEvents.Add(e);
        //    SyncHelper<TestEventStatus>.Metrics.WaiterTimedOut += (s, e) => timedOutEvents.Add(e);

        //    try
        //    {
        //        // Act & Assert - Completion
        //        var waitTask1 = SyncHelper<TestEventStatus>.WaitAsync(eventId1, TestEventStatus.Timeout, 5000);
        //        await Task.Delay(50);
        //        SyncHelper<TestEventStatus>.TryComplete(eventId1, TestEventStatus.Success);
        //        await waitTask1;

        //        // Act & Assert - Cancellation
        //        var waitTask2 = SyncHelper<TestEventStatus>.WaitAsync(eventId2, TestEventStatus.Timeout, 5000);
        //        await Task.Delay(50);
        //        SyncHelper<TestEventStatus>.CancelWait(eventId2);
        //        try { await waitTask2; } catch (TaskCanceledException) { }

        //        // Act & Assert - Timeout
        //        await SyncHelper<TestEventStatus>.WaitAsync(eventId3, TestEventStatus.Timeout, 50);
        //    }
        //    finally
        //    {
        //        // Отписываемся от событий, чтобы не влиять на другие тесты
        //        SyncHelper<TestEventStatus>.Metrics.WaiterCompleted -= (s, e) => completedEvents.Add(e);
        //        SyncHelper<TestEventStatus>.Metrics.WaiterCancelled -= (s, e) => cancelledEvents.Add(e);
        //        SyncHelper<TestEventStatus>.Metrics.WaiterTimedOut -= (s, e) => timedOutEvents.Add(e);
        //    }

        //    // Assert
        //    Assert.AreEqual(1, completedEvents.Count, "Должно быть 1 завершенное событие");
        //    Assert.AreEqual(eventId1, completedEvents[0]);

        //    Assert.AreEqual(1, cancelledEvents.Count, "Должно быть 1 отмененное событие");
        //    Assert.AreEqual(eventId2, cancelledEvents[0]);

        //    Assert.AreEqual(1, timedOutEvents.Count, "Должно быть 1 событие с таймаутом");
        //    Assert.AreEqual(eventId3, timedOutEvents[0]);
        //}

        ///// <summary>
        ///// Проверяет счетчики метрик.
        ///// </summary>
        //[TestMethod]
        //public async Task Metrics_Counters_ReturnCorrectValues()
        //{
        //    lock (_sync)
        //    {
        //        // Arrange
        //        string eventId1 = GetUniqueEventId();
        //        string eventId2 = GetUniqueEventId();

        //        // Act
        //        Assert.AreEqual(0, SyncHelper<TestEventStatus>.Metrics.ActiveWaitersCount);
        //        Assert.AreEqual(0, SyncHelper<TestEventStatus>.Metrics.ActiveParamsCount);

        //        var task1 = SyncHelper<TestEventStatus>.WaitAsync(eventId1, TestEventStatus.Timeout, 5000);
        //        var task2 = SyncHelper<TestEventStatus>.WaitAsync(eventId2, TestEventStatus.Timeout, 5000);

        //        SyncHelper<TestEventStatus>.SetEventParam(eventId1, "param1", "value1");
        //        SyncHelper<TestEventStatus>.SetEventParam(eventId2, "param2", "value2");

        //        await Task.Delay(50);

        //        // Assert
        //        Assert.IsTrue(SyncHelper<TestEventStatus>.Metrics.ActiveWaitersCount >= 2, "Должно быть 2 активных ожидания");
        //        Assert.IsTrue(SyncHelper<TestEventStatus>.Metrics.ActiveParamsCount >= 2, "Должно быть 2 активных параметра");

        //        // Cleanup
        //        SyncHelper<TestEventStatus>.ClearAll();

        //        Assert.AreEqual(0, SyncHelper<TestEventStatus>.Metrics.ActiveWaitersCount);
        //        Assert.AreEqual(0, SyncHelper<TestEventStatus>.Metrics.ActiveParamsCount);
        //    }
        //}

        #endregion Metrics Tests

        #region Thread Safety Tests

        /// <summary>
        /// Проверяет потокобезопасность при параллельных операциях.
        /// </summary>
        [TestMethod]
        public async Task ThreadSafety_ParallelOperations_NoExceptions()
        {
            // Arrange
            const int taskCount = 50; // Уменьшил для надежности
            var tasks = new List<Task>();
            var eventIds = new string[taskCount];

            for (int i = 0; i < taskCount; i++)
            {
                eventIds[i] = GetUniqueEventId();
            }

            var random = new Random();

            // Act - параллельно создаем ожидания
            for (int i = 0; i < taskCount; i++)
            {
                int index = i;
                tasks.Add(Task.Run(async () =>
                {
                    var eventId = eventIds[index];

                    // Создаем ожидание
                    var waitTask = SyncHelper<TestEventStatus>.WaitAsync(eventId, TestEventStatus.Timeout, 5000);

                    // Устанавливаем параметры
                    SyncHelper<TestEventStatus>.SetEventParam(eventId, "index", index);
                    SyncHelper<TestEventStatus>.SetEventParam(eventId, "timestamp", DateTime.Now.Ticks);

                    // Проверяем параметры
                    var retrievedIndex = SyncHelper<TestEventStatus>.GetEventParam<int>(eventId, "index");
                    Assert.AreEqual(index, retrievedIndex);

                    // Случайно завершаем или отменяем событие
                    await Task.Delay(random.Next(10, 50));

                    if (random.Next(2) == 0)
                    {
                        SyncHelper<TestEventStatus>.TryComplete(eventId, TestEventStatus.Success, $"data-{index}");
                    }
                    else
                    {
                        SyncHelper<TestEventStatus>.CancelWait(eventId);
                    }

                    try
                    {
                        var result = await waitTask;
                        if (result.Status == TestEventStatus.Success)
                        {
                            Assert.AreEqual($"data-{index}", result.Data);
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        // Ожидаемо для отмененных
                    }
                }));
            }

            // Assert
            await Task.WhenAll(tasks);

            // Небольшая задержка для завершения всех операций очистки
            await Task.Delay(100);

            // Проверяем что все очистилось
            Assert.AreEqual(0, SyncHelper<TestEventStatus>.Metrics.ActiveWaitersCount,
                "Не должно остаться активных ожиданий");
            Assert.AreEqual(0, SyncHelper<TestEventStatus>.Metrics.ActiveParamsCount,
                "Не должно остаться активных параметров");
        }

        ///// <summary>
        ///// Проверяет конкурентный доступ к словарям.
        ///// </summary>
        //[TestMethod]
        //public void ThreadSafety_ConcurrentAccess_NoExceptions()
        //{
        //    // Arrange
        //    const int threadCount = 5; // Уменьшил для надежности
        //    const int operationsPerThread = 50;
        //    var tasks = new List<Task>();
        //    var completedOperations = 0;
        //    var lockObj = new object();

        //    // Act
        //    for (int t = 0; t < threadCount; t++)
        //    {
        //        int threadId = t;
        //        tasks.Add(Task.Run(() =>
        //        {
        //            var random = new Random(threadId);

        //            for (int i = 0; i < operationsPerThread; i++)
        //            {
        //                string eventId = $"event-{threadId}-{i}-{Guid.NewGuid()}";

        //                // Перемешиваем различные операции
        //                switch (random.Next(7))
        //                {
        //                    case 0:
        //                        SyncHelper<TestEventStatus>.WaitAsync(eventId, TestEventStatus.Timeout, 5000);
        //                        break;
        //                    case 1:
        //                        SyncHelper<TestEventStatus>.TryComplete(eventId, TestEventStatus.Success);
        //                        break;
        //                    case 2:
        //                        SyncHelper<TestEventStatus>.CancelWait(eventId);
        //                        break;
        //                    case 3:
        //                        SyncHelper<TestEventStatus>.SetEventParam(eventId, "key", "value");
        //                        break;
        //                    case 4:
        //                        SyncHelper<TestEventStatus>.GetEventParam(eventId, "key");
        //                        break;
        //                    case 5:
        //                        SyncHelper<TestEventStatus>.HasParam(eventId, "key");
        //                        break;
        //                    case 6:
        //                        // Для CancelAllWaiting используем предикат, специфичный для этого потока
        //                        if (random.Next(10) == 0) // Редко, чтобы не мешать другим
        //                        {
        //                            SyncHelper<TestEventStatus>.CancelAllWaiting(id => id.StartsWith($"event-{threadId}"));
        //                        }
        //                        break;
        //                }

        //                lock (lockObj)
        //                {
        //                    completedOperations++;
        //                }
        //            }
        //        }));
        //    }

        //    // Cleanup
        //    SyncHelper<TestEventStatus>.ClearAll();

        //    Assert.AreEqual(threadCount * operationsPerThread, completedOperations,
        //        "Не все операции были выполнены");
        //}

        #endregion Thread Safety Tests

        #region Edge Cases Tests

        ///// <summary>
        ///// Проверяет поведение при очень коротком таймауте.
        ///// </summary>
        //[TestMethod]
        //public async Task WaitAsync_VeryShortTimeout_ReturnsTimeout()
        //{
        //    // Arrange
        //    string eventId = GetUniqueEventId();

        //    // Act
        //    var result = await SyncHelper<TestEventStatus>.WaitAsync(eventId, TestEventStatus.Timeout, 1);

        //    // Assert
        //    Assert.AreEqual(TestEventStatus.Timeout, result.Status);
        //}

        /// <summary>
        /// Проверяет, что событие можно завершить сразу после создания ожидания.
        /// </summary>
        [TestMethod]
        public async Task WaitAsync_CompleteImmediately_CompletesSuccessfully()
        {
            // Arrange
            string eventId = GetUniqueEventId();

            // Act
            var waitTask = SyncHelper<TestEventStatus>.WaitAsync(eventId, TestEventStatus.Timeout, 5000);
            var completed = SyncHelper<TestEventStatus>.TryComplete(eventId, TestEventStatus.Success);
            var result = await waitTask;

            // Assert
            Assert.IsTrue(completed);
            Assert.AreEqual(TestEventStatus.Success, result.Status);
        }

        /// <summary>
        /// Проверяет перезапись параметра.
        /// </summary>
        [TestMethod]
        public void EventParams_OverwriteParam_ValueUpdated()
        {
            // Arrange
            string eventId = GetUniqueEventId();
            string paramName = "test";

            // Act
            SyncHelper<TestEventStatus>.SetEventParam(eventId, paramName, "first");
            var firstValue = SyncHelper<TestEventStatus>.GetEventParam<string>(eventId, paramName);

            SyncHelper<TestEventStatus>.SetEventParam(eventId, paramName, "second");
            var secondValue = SyncHelper<TestEventStatus>.GetEventParam<string>(eventId, paramName);

            // Assert
            Assert.AreEqual("first", firstValue);
            Assert.AreEqual("second", secondValue);
        }

        #endregion Edge Cases Tests
    }
}