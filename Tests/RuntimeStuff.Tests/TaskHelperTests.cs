using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RuntimeStuff.Helpers;

namespace RuntimeStuff.Tests
{
    [TestClass]
    public class RunAndForgetTests
    {
        [TestMethod]
        public async Task RunAndForget_Generic_WithException_ShouldCallHandler()
        {
            var exception = new InvalidOperationException("Test exception");
            var tcs = new TaskCompletionSource<bool>();

            Action<InvalidOperationException> onException = _ => tcs.SetResult(true);

            var task = Task.FromException(exception);
            TaskHelper.Initialize(shouldAlwaysRethrowException: false);
            TaskHelper.RunAndForget(task, onException);

            var handled = await tcs.Task;
            Assert.IsTrue(handled);
        }

        [TestMethod]
        public async Task RunAndForget_Generic_WithoutException_ShouldNotCallHandler()
        {
            var tcs = new TaskCompletionSource<bool>();
            Action<InvalidOperationException> onException = _ => tcs.SetResult(true);

            var task = Task.CompletedTask;
            TaskHelper.Initialize(shouldAlwaysRethrowException: false);
            TaskHelper.RunAndForget(task, onException);

            // Подождём коротко, если обработчик не вызван, завершится Timeout
            var completed = await Task.WhenAny(tcs.Task, Task.Delay(50));
            Assert.AreNotEqual(tcs.Task, completed); // обработчик не должен был вызваться
        }

        [TestMethod]
        public async Task RunAndForget_NonGeneric_WithException_ShouldCallHandler()
        {
            var exception = new Exception("Test exception");
            var tcs = new TaskCompletionSource<bool>();
            Action<Exception> onException = _ => tcs.SetResult(true);

            var task = Task.FromException(exception);
            TaskHelper.Initialize(shouldAlwaysRethrowException: false);
            TaskHelper.RunAndForget(task, onException);

            var handled = await tcs.Task;
            Assert.IsTrue(handled);
        }

        [TestMethod]
        public async Task RunAndForget_WithDefaultExceptionHandler_ShouldCallBothHandlers()
        {
            var tcsDefault = new TaskCompletionSource<bool>();
            var tcsLocal = new TaskCompletionSource<bool>();

            TaskHelper.SetDefaultExceptionHandling(_ => tcsDefault.SetResult(true));

            try
            {
                var task = Task.FromException(new Exception("Test"));
                TaskHelper.Initialize(shouldAlwaysRethrowException: false);
                TaskHelper.RunAndForget(task, ex => tcsLocal.SetResult(true));

                var handled = await Task.WhenAll(tcsDefault.Task, tcsLocal.Task);
                Assert.IsTrue(handled[0]);
                Assert.IsTrue(handled[1]);
            }
            finally
            {
                TaskHelper.RemoveDefaultExceptionHandling();
            }
        }

        [TestMethod]
        public async Task RunAndForget_GenericWithDifferentExceptionType_ShouldNotCallHandler()
        {
            var tcs = new TaskCompletionSource<bool>();
            Action<InvalidOperationException> onException = _ => tcs.SetResult(true);

            var task = Task.FromException(new ArgumentException("Test"));
            TaskHelper.Initialize(shouldAlwaysRethrowException: false);
            TaskHelper.RunAndForget(task, onException);

            var completed = await Task.WhenAny(tcs.Task, Task.Delay(50));
            Assert.AreNotEqual(tcs.Task, completed); // обработчик не должен был вызваться
        }
    }

    [TestClass]
    public class InitializeTests
    {
        [TestMethod]
        public async Task Initialize_WithRethrowFalse_ShouldNotRethrowException()
        {
            TaskHelper.Initialize(shouldAlwaysRethrowException: false);

            var tcs = new TaskCompletionSource<bool>();
            Action<Exception> onException = _ => tcs.SetResult(true);

            var task = Task.FromException(new Exception("Test"));
            TaskHelper.RunAndForget(task, onException);

            var handled = await tcs.Task;
            Assert.IsTrue(handled);
        }
    }

    [TestClass]
    public class DefaultExceptionHandlingTests
    {
        [TestMethod]
        public void SetDefaultExceptionHandling_WithNull_ShouldThrowArgumentNullException()
        {
            Assert.ThrowsException<ArgumentNullException>(
                () => TaskHelper.SetDefaultExceptionHandling(null));
        }

        [TestMethod]
        public async Task SetDefaultExceptionHandling_WithValidHandler_ShouldBeCalled()
        {
            var tcs = new TaskCompletionSource<bool>();
            try
            {
                TaskHelper.SetDefaultExceptionHandling(_ => tcs.SetResult(true));
                var task = Task.FromException(new Exception("Test"));
                TaskHelper.Initialize(shouldAlwaysRethrowException: false);
                TaskHelper.RunAndForget(task);

                var handled = await tcs.Task;
                Assert.IsTrue(handled);
            }
            finally
            {
                TaskHelper.RemoveDefaultExceptionHandling();
            }
        }

        [TestMethod]
        public async Task RemoveDefaultExceptionHandling_ShouldRemoveHandler()
        {
            var tcs = new TaskCompletionSource<bool>();
            TaskHelper.SetDefaultExceptionHandling(_ => tcs.SetResult(true));
            TaskHelper.RemoveDefaultExceptionHandling();

            var task = Task.FromException(new Exception("Test"));
            TaskHelper.Initialize(shouldAlwaysRethrowException: false);
            TaskHelper.RunAndForget(task);

            var completed = await Task.WhenAny(tcs.Task, Task.Delay(50));
            Assert.AreNotEqual(tcs.Task, completed); // обработчик не должен был вызваться
        }
    }
}
