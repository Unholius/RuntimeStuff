// <copyright file="TaskHelperTests.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace RuntimeStuff.MSTests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using RuntimeStuff.Helpers;
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Тесты для класса <see cref="TaskHelper{T}"/>.
    /// </summary>
    [TestClass]
    public class TaskHelperTests
    {
        /// <summary>
        /// Тестовый статус для событий.
        /// </summary>
        private enum TestStatus
        {
            Success,
            Failure,
            Timeout,
            Cancelled
        }

        /// <summary>
        /// Проверяет успешное ожидание и завершение события.
        /// </summary>
        [TestMethod]
        public async Task WaitAndComplete_Success()
        {
            // Arrange
            var eventId = Guid.NewGuid();
            var expectedStatus = TestStatus.Success;
            var expectedData = "test data";

            // Act
            var waitTask = SyncHelper.WaitAsync(eventId, 1000);
            var completeResult = SyncHelper.TryComplete(eventId, (int)expectedStatus, expectedData);
            var result = await waitTask;

            // Assert
            Assert.IsTrue(completeResult);
            Assert.IsNotNull(result);
            Assert.AreEqual(eventId, result.EventId);
            Assert.AreEqual((int)expectedStatus, result.Status);
            Assert.AreEqual(expectedData, result.Data);
        }

        /// <summary>
        /// Проверяет, что повторное ожидание того же события возвращает существующую задачу.
        /// </summary>
        [TestMethod]
        public async Task Wait_SameEventId_ReturnsSameTask()
        {
            // Arrange
            var eventId = Guid.NewGuid();

            // Act
            var task1 = SyncHelper.WaitAsync(eventId, 1000);
            var task2 = SyncHelper.WaitAsync(eventId, 1000);

            // Assert
            Assert.AreEqual(task1, task2);
        }

        /// <summary>
        /// Проверяет, что ожидание события завершается таймаутом.
        /// </summary>
        [TestMethod]
        public async Task Wait_Timeout_CancelsTask()
        {
            // Arrange
            var eventId = Guid.NewGuid();

            // Act & Assert
            await Assert.ThrowsExceptionAsync<TaskCanceledException>(async () =>
            {
                var task = SyncHelper.WaitAsync(eventId, 100);
                await task;
            });
        }

        ///// <summary>
        ///// Проверяет ожидание с возвратом статуса при таймауте.
        ///// </summary>
        //[TestMethod]
        //public async Task Wait_WithTimeoutStatus_ReturnsTimeoutStatusOnTimeout()
        //{
        //    // Arrange
        //    var eventId = Guid.NewGuid();
        //    var timeoutStatus = TestStatus.Timeout;

        //    // Act
        //    var task = TaskHelper<TestStatus>.Wait(eventId, timeoutStatus, 100);
        //    var result = await task;

        //    // Assert
        //    Assert.IsNotNull(result);
        //    Assert.AreEqual(eventId, result.EventId);
        //    Assert.AreEqual(timeoutStatus, result.Status);
        //    Assert.IsNull(result.Data);
        //}

        /// <summary>
        /// Проверяет отмену ожидания события.
        /// </summary>
        [TestMethod]
        public void CancelWait_Success()
        {
            // Arrange
            var eventId = Guid.NewGuid();
            var waitTask = SyncHelper.WaitAsync(eventId, 5000);

            // Act
            var cancelResult = SyncHelper.CancelWait(eventId);

            // Assert
            Assert.IsTrue(cancelResult);
            Assert.IsTrue(waitTask.IsCanceled);
        }

        /// <summary>
        /// Проверяет отмену несуществующего ожидания.
        /// </summary>
        [TestMethod]
        public void CancelWait_NoWaiter_ReturnsFalse()
        {
            // Arrange
            var eventId = Guid.NewGuid();

            // Act
            var cancelResult = SyncHelper.CancelWait(eventId);

            // Assert
            Assert.IsFalse(cancelResult);
        }

        /// <summary>
        /// Проверяет установку и получение параметров события.
        /// </summary>
        [TestMethod]
        public void SetAndGetEventParam_Success()
        {
            // Arrange
            var eventId = Guid.NewGuid();
            var paramName = "testParam";
            var paramValue = "testValue";
            var defaultValue = "default";

            // Act
            SyncHelper.SetEventParam(eventId, paramName, paramValue);
            var result = SyncHelper.GetEventParam(eventId, paramName, defaultValue);

            // Assert
            Assert.AreEqual(paramValue, result);
        }

        /// <summary>
        /// Проверяет получение несуществующего параметра с возвратом значения по умолчанию.
        /// </summary>
        [TestMethod]
        public void GetEventParam_NonExisting_ReturnsDefault()
        {
            // Arrange
            var eventId = Guid.NewGuid();
            var defaultValue = "default";

            // Act
            var result = SyncHelper.GetEventParam(eventId, "nonExisting", defaultValue);

            // Assert
            Assert.AreEqual(defaultValue, result);
        }

        /// <summary>
        /// Проверяет очистку всех данных.
        /// </summary>
        [TestMethod]
        public void ClearAll_CleansAllData()
        {
            // Arrange
            var eventId1 = Guid.NewGuid();
            var eventId2 = Guid.NewGuid();
            var paramName = "testParam";
            var paramValue = "testValue";

            var waitTask1 = SyncHelper.WaitAsync(eventId1, 5000);
            var waitTask2 = SyncHelper.WaitAsync(eventId2, 5000);
            SyncHelper.SetEventParam(eventId1, paramName, paramValue);

            // Act
            SyncHelper.ClearAll();

            // Assert
            Assert.IsTrue(waitTask1.IsCanceled);
            Assert.IsTrue(waitTask2.IsCanceled);
            Assert.IsFalse(SyncHelper.CancelWait(eventId1));
            Assert.IsNull(SyncHelper.GetEventParam(eventId1, paramName));
        }

        /// <summary>
        /// Проверяет выброс исключения при передаче null в eventId.
        /// </summary>
        [TestMethod]
        public void Wait_NullEventId_ThrowsNullReferenceException()
        {
            // Act & Assert
            Assert.ThrowsException<NullReferenceException>(() =>
                SyncHelper.WaitAsync(null));
        }

        /// <summary>
        /// Проверяет выброс исключения при передаче null в eventId в TryComplete.
        /// </summary>
        [TestMethod]
        public void TryComplete_NullEventId_ThrowsNullReferenceException()
        {
            // Act & Assert
            Assert.ThrowsException<NullReferenceException>(() =>
                SyncHelper.TryComplete(null, (int)TestStatus.Success));
        }

        /// <summary>
        /// Проверяет выброс исключения при передаче null в eventId в SetEventParam.
        /// </summary>
        [TestMethod]
        public void SetEventParam_NullEventId_ThrowsNullReferenceException()
        {
            // Act & Assert
            Assert.ThrowsException<NullReferenceException>(() =>
                SyncHelper.SetEventParam(null, "param", "value"));
        }

        /// <summary>
        /// Проверяет выброс исключения при передаче пустого имени параметра.
        /// </summary>
        [TestMethod]
        public void SetEventParam_EmptyParamName_ThrowsArgumentException()
        {
            // Arrange
            var eventId = Guid.NewGuid();

            // Act & Assert
            Assert.ThrowsException<ArgumentException>(() =>
                SyncHelper.SetEventParam(eventId, string.Empty, "value"));
        }

        /// <summary>
        /// Проверяет, что TryComplete возвращает false для несуществующего события.
        /// </summary>
        [TestMethod]
        public void TryComplete_NoWaiter_ReturnsFalse()
        {
            // Arrange
            var eventId = Guid.NewGuid();

            // Act
            var result = SyncHelper.TryComplete(eventId, (int)TestStatus.Success);

            // Assert
            Assert.IsFalse(result);
        }

        /// <summary>
        /// Проверяет, что после завершения события параметры очищаются.
        /// </summary>
        [TestMethod]
        public async Task TryComplete_ParametersCleaned()
        {
            // Arrange
            var eventId = Guid.NewGuid();
            var paramName = "testParam";
            var paramValue = "testValue";

            SyncHelper.SetEventParam(eventId, paramName, paramValue);

            // Act
            var waitTask = SyncHelper.WaitAsync(eventId, 1000);
            SyncHelper.TryComplete(eventId, (int)TestStatus.Success);
            await waitTask;

            // Assert
            var param = SyncHelper.GetEventParam(eventId, paramName);
            Assert.IsNull(param);
        }

        /// <summary>
        /// Проверяет, что несколько событий могут ожидаться одновременно.
        /// </summary>
        [TestMethod]
        public async Task MultipleEvents_WaitAndComplete_AllComplete()
        {
            // Arrange
            var eventId1 = Guid.NewGuid();
            var eventId2 = Guid.NewGuid();
            var eventId3 = Guid.NewGuid();

            // Act
            var task1 = SyncHelper.WaitAsync(eventId1, 1000);
            var task2 = SyncHelper.WaitAsync(eventId2, 1000);
            var task3 = SyncHelper.WaitAsync(eventId3, 1000);

            SyncHelper.TryComplete(eventId2, (int)TestStatus.Success, "data2");
            SyncHelper.TryComplete(eventId1, (int)TestStatus.Success, "data1");
            SyncHelper.TryComplete(eventId3, (int)TestStatus.Success, "data3");

            var results = await Task.WhenAll(task1, task2, task3);

            // Assert
            Assert.AreEqual(3, results.Length);
            Assert.AreEqual("data1", results[0].Data);
            Assert.AreEqual("data2", results[1].Data);
            Assert.AreEqual("data3", results[2].Data);
        }
    }
}