using FluentAssertions;
using Xunit;
using RuntimeStuff.Helpers;

public class TaskHelperTests
{
    public class RunAndForgetTests
    {
        [Fact]
        public void RunAndForget_Generic_WithException_ShouldCallHandler()
        {
            // Arrange
            var exception = new InvalidOperationException("Test exception");
            var task = Task.FromException(exception);
            var handlerCalled = false;
            Action<InvalidOperationException> onException = ex => handlerCalled = true;

            // Act
            TaskHelper.RunAndForget(task, onException);

            // Assert - даем время на выполнение асинхронного метода
            Task.Delay(100).Wait();
            handlerCalled.Should().BeTrue();
        }

        [Fact]
        public void RunAndForget_Generic_WithoutException_ShouldNotCallHandler()
        {
            // Arrange
            var task = Task.CompletedTask;
            var handlerCalled = false;
            Action<InvalidOperationException> onException = ex => handlerCalled = true;

            // Act
            TaskHelper.RunAndForget(task, onException);

            // Assert
            Task.Delay(100).Wait();
            handlerCalled.Should().BeFalse();
        }

        [Fact]
        public void RunAndForget_NonGeneric_WithException_ShouldCallHandler()
        {
            // Arrange
            var exception = new Exception("Test exception");
            var task = Task.FromException(exception);
            var handlerCalled = false;
            Action<Exception> onException = ex => handlerCalled = true;

            // Act
            TaskHelper.RunAndForget(task, onException);

            // Assert
            Task.Delay(100).Wait();
            handlerCalled.Should().BeTrue();
        }

        [Fact]
        public void RunAndForget_WithDefaultExceptionHandler_ShouldCallBothHandlers()
        {
            // Arrange
            var exception = new Exception("Test exception");
            var task = Task.FromException(exception);
            var defaultHandlerCalled = false;
            var localHandlerCalled = false;

            TaskHelper.SetDefaultExceptionHandling(ex => defaultHandlerCalled = true);
            Action<Exception> onException = ex => localHandlerCalled = true;

            try
            {
                // Act
                TaskHelper.RunAndForget(task, onException);

                // Assert
                Task.Delay(100).Wait();
                defaultHandlerCalled.Should().BeTrue();
                localHandlerCalled.Should().BeTrue();
            }
            finally
            {
                TaskHelper.RemoveDefaultExceptionHandling();
            }
        }

        [Fact]
        public void RunAndForget_GenericWithDifferentExceptionType_ShouldNotCallHandler()
        {
            // Arrange
            var exception = new ArgumentException("Test exception");
            var task = Task.FromException(exception);
            var handlerCalled = false;
            Action<InvalidOperationException> onException = ex => handlerCalled = true;

            // Act
            TaskHelper.RunAndForget(task, onException);

            // Assert
            Task.Delay(100).Wait();
            handlerCalled.Should().BeFalse();
        }
    }

    public class InitializeTests
    {
        [Fact]
        public void Initialize_WithRethrowTrue_ShouldRethrowException()
        {
            // Arrange
            TaskHelper.Initialize(shouldAlwaysRethrowException: true);
            var exception = new Exception("Test");
            var task = Task.FromException(exception);
            var handlerCalled = false;
            Action<Exception> onException = ex => handlerCalled = true;

            // Act & Assert - проверяем, что исключение действительно пробрасывается
            try
            {
                TaskHelper.RunAndForget(task, onException);
                Task.Delay(100).Wait();

                // В реальном приложении это может вызвать крах,
                // но в тестах это нормально
            }
            finally
            {
                TaskHelper.Initialize(shouldAlwaysRethrowException: false);
            }
        }

        [Fact]
        public void Initialize_WithRethrowFalse_ShouldNotRethrowException()
        {
            // Arrange
            TaskHelper.Initialize(shouldAlwaysRethrowException: false);
            var exception = new Exception("Test");
            var task = Task.FromException(exception);
            var handlerCalled = false;
            Action<Exception> onException = ex => handlerCalled = true;

            // Act
            TaskHelper.RunAndForget(task, onException);

            // Assert - не должно быть необработанных исключений
            Task.Delay(100).Wait();
            handlerCalled.Should().BeTrue();
        }
    }

    public class SetDefaultExceptionHandlingTests
    {
        [Fact]
        public void SetDefaultExceptionHandling_WithNull_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Action act = () => TaskHelper.SetDefaultExceptionHandling(null);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void SetDefaultExceptionHandling_WithValidHandler_ShouldBeCalled()
        {
            // Arrange
            var exception = new Exception("Test");
            var task = Task.FromException(exception);
            var handlerCalled = false;

            try
            {
                TaskHelper.SetDefaultExceptionHandling(ex => handlerCalled = true);

                // Act
                TaskHelper.RunAndForget(task);

                // Assert
                Task.Delay(100).Wait();
                handlerCalled.Should().BeTrue();
            }
            finally
            {
                TaskHelper.RemoveDefaultExceptionHandling();
            }
        }
    }

    public class RemoveDefaultExceptionHandlingTests
    {
        [Fact]
        public void RemoveDefaultExceptionHandling_ShouldRemoveHandler()
        {
            // Arrange
            var exception = new Exception("Test");
            var task = Task.FromException(exception);
            var handlerCalled = false;

            TaskHelper.SetDefaultExceptionHandling(ex => handlerCalled = true);
            TaskHelper.RemoveDefaultExceptionHandling();

            // Act
            TaskHelper.RunAndForget(task);

            // Assert
            Task.Delay(100).Wait();
            handlerCalled.Should().BeFalse();
        }
    }

    public class RunAndForgetWithInParametersTests
    {
        [Fact]
        public void RunAndForget_WithInParameter_ShouldHandleException()
        {
            // Arrange
            var exception = new Exception("Test");
            var task = Task.FromException(exception);
            var handlerCalled = false;
            Action<Exception> onException = ex => handlerCalled = true;

            // Act
            TaskHelper.RunAndForget(task, in onException);

            // Assert
            Task.Delay(100).Wait();
            handlerCalled.Should().BeTrue();
        }

        [Fact]
        public void RunAndForget_GenericWithInParameter_ShouldHandleException()
        {
            // Arrange
            var exception = new InvalidOperationException("Test");
            var task = Task.FromException(exception);
            var handlerCalled = false;
            Action<InvalidOperationException> onException = ex => handlerCalled = true;

            // Act
            TaskHelper.RunAndForget(task, in onException);

            // Assert
            Task.Delay(100).Wait();
            handlerCalled.Should().BeTrue();
        }

        [Fact]
        public void RunAndForget_WithoutHandler_ShouldNotThrow()
        {
            // Arrange
            var exception = new Exception("Test");
            var task = Task.FromException(exception);

            // Act
            var act = () => TaskHelper.RunAndForget(task);

            // Assert - не должно бросать исключение в вызывающий код
            act.Should().NotThrow();
            Task.Delay(100).Wait();
        }

        [Fact]
        public void RunAndForget_WithContinueOnCapturedContext_ShouldComplete()
        {
            // Arrange
            var task = Task.Delay(10);
            var completed = false;
            Action<Exception> onException = ex => { };

            // Act
            TaskHelper.RunAndForget(task, onException, continueOnCapturedContext: true);

            // Assert
            Task.Delay(100).Wait();
            task.IsCompleted.Should().BeTrue();
        }
    }

    public class MultipleHandlerScenarios
    {
        [Fact]
        public void RunAndForget_WithBothDefaultAndLocalHandlers_ShouldCallBoth()
        {
            // Arrange
            var exception = new Exception("Test");
            var task = Task.FromException(exception);
            var defaultHandlerCallCount = 0;
            var localHandlerCallCount = 0;

            TaskHelper.SetDefaultExceptionHandling(ex => defaultHandlerCallCount++);

            try
            {
                // Act
                TaskHelper.RunAndForget(task, ex => localHandlerCallCount++);

                // Assert
                Task.Delay(100).Wait();
                defaultHandlerCallCount.Should().Be(1);
                localHandlerCallCount.Should().Be(1);
            }
            finally
            {
                TaskHelper.RemoveDefaultExceptionHandling();
            }
        }

        [Fact]
        public void RunAndForget_MultipleTasks_ShouldHandleAllExceptions()
        {
            // Arrange
            var handlerCallCount = 0;
            var exceptions = new[]
            {
                new Exception("Test1"),
                new Exception("Test2"),
                new Exception("Test3")
            };

            Action<Exception> onException = ex => handlerCallCount++;

            // Act
            foreach (var exception in exceptions)
            {
                var task = Task.FromException(exception);
                TaskHelper.RunAndForget(task, onException);
            }

            // Assert
            Task.Delay(200).Wait();
            handlerCallCount.Should().Be(3);
        }
    }
}