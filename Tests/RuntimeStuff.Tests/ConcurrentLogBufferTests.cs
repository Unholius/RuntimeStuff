using System.Collections.Concurrent;
using System.Diagnostics;
using RuntimeStuff.Collections;

namespace RuntimeStuff.MSTests
{
    [TestClass]
    public class ConcurrentLogBufferAdvancedTests
    {
        [TestMethod]
        public void Add_FromManyThreads_CountNeverExceedsCapacity()
        {
            // Arrange
            const int capacity = 100;
            const int threadCount = 50;
            const int iterations = 1000;
            var buffer = new ConcurrentLogBuffer<string>(capacity);

            // Act
            Parallel.For(0, threadCount, thread =>
            {
                for (int i = 0; i < iterations; i++)
                {
                    buffer.Add((i * threadCount + thread).ToString());
                }
            });

            // Assert
            var snapshot = buffer.Snapshot();
            Assert.IsTrue(snapshot.Count <= capacity,
                $"Count was {snapshot.Count}, expected <= {capacity}");
        }

        [TestMethod]
        public void Snapshot_DuringHighConcurrency_ReturnsConsistentState()
        {
            // Arrange
            const int capacity = 200;
            const int threadCount = 10;
            const int durationMs = 200;
            var buffer = new ConcurrentLogBuffer<string>(capacity);
            var stopwatch = new Stopwatch();
            var exceptions = new ConcurrentBag<Exception>();

            // Act - Start multiple threads adding items
            var cts = new CancellationTokenSource();
            var tasks = new List<Task>();

            for (int i = 0; i < threadCount; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            buffer.Add(DateTime.UtcNow.Ticks.ToString());
                            await Task.Delay(1);
                        }
                        catch (Exception ex)
                        {
                            exceptions.Add(ex);
                        }
                    }
                }));
            }

            // Take snapshots periodically
            var snapshots = new List<IReadOnlyList<string>>();
            stopwatch.Start();

            while (stopwatch.ElapsedMilliseconds < durationMs)
            {
                try
                {
                    var snapshot = buffer.Snapshot();
                    snapshots.Add(snapshot);

                    // Verify snapshot integrity
                    Assert.IsTrue(snapshot.Count <= capacity);

                    // Check that items are in chronological order (or at least not completely out of order)
                    if (snapshot.Count > 1)
                    {
                        // Since multiple threads add concurrently, strict ordering isn't guaranteed,
                        // but we should at least not get exceptions
                        for (int j = 0; j < snapshot.Count; j++)
                        {
                            // Just accessing should not throw
                            var _ = snapshot[j];
                        }
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }

                Thread.Sleep(10);
            }

            cts.Cancel();
            Task.WaitAll(tasks.ToArray(), 5000);

            // Assert
            Assert.AreEqual(0, exceptions.Count,
                $"Exceptions occurred during concurrent operations: {exceptions.FirstOrDefault()}");
        }

        [TestMethod]
        public void Performance_AddOperations_AreFast()
        {
            // Arrange
            const int capacity = 1000;
            const int iterations = 100000;
            var buffer = new ConcurrentLogBuffer<string>(capacity);
            var stopwatch = new Stopwatch();

            // Act
            stopwatch.Start();
            for (int i = 0; i < iterations; i++)
            {
                buffer.Add(i.ToString());
            }

            stopwatch.Stop();

            // Assert
            var elapsedMs = stopwatch.ElapsedMilliseconds;
            var opsPerSecond = iterations / (stopwatch.ElapsedMilliseconds / 1000.0);

            Console.WriteLine($"Single-threaded Add performance: {opsPerSecond:F0} ops/sec, {elapsedMs} ms total");

            // This is a loose assertion - adjust based on your performance requirements
            Assert.IsTrue(opsPerSecond > 100000,
                $"Performance too low: {opsPerSecond:F0} ops/sec");
        }

        [TestMethod]
        public void Memory_Usage_DoesNotGrowUnbounded()
        {
            // Arrange
            const int capacity = 1000;
            const int iterations = 1000000;
            var buffer = new ConcurrentLogBuffer<byte[]>(capacity);

            // Get initial memory
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var initialMemory = GC.GetTotalMemory(true);

            // Act - Add many large items
            var largeArray = new byte[10000]; // 10KB per item

            for (int i = 0; i < iterations; i++)
            {
                buffer.Add(largeArray);
            }

            // Force garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var finalMemory = GC.GetTotalMemory(true);

            // Calculate memory increase
            var memoryIncrease = finalMemory - initialMemory;

            Console.WriteLine($"Memory increase after {iterations} adds: {memoryIncrease / 1024} KB");

            // Assert - Memory should not grow linearly with number of adds
            // Since buffer has fixed capacity, memory usage should be bounded
            var expectedMaxMemory = capacity * largeArray.Length * 1.5; // Allow 50% overhead

            Assert.IsTrue(memoryIncrease < expectedMaxMemory,
                $"Memory increase {memoryIncrease} exceeds expected maximum {expectedMaxMemory}");
        }

        [TestMethod]
        public void EdgeCase_IndexWrapAround_HandledCorrectly()
        {
            // Arrange
            const int capacity = 3;
            var buffer = new ConcurrentLogBuffer<string>(capacity);

            // Act - Force index to wrap around by adding many items
            // We'll add enough items to cause multiple wrap-arounds
            for (int i = 0; i < capacity * 10 + 2; i++) // 32 items for buffer of size 3
            {
                buffer.Add(i.ToString());
            }

            var snapshot = buffer.Snapshot();

            // Assert
            Assert.AreEqual(capacity, snapshot.Count);

            // Should contain the last 3 items
            var expectedStart = capacity * 10 + 2 - capacity; // 32 - 3 = 29
            for (int i = 0; i < capacity; i++)
            {
                Assert.AreEqual((expectedStart + i).ToString(), snapshot[i]);
            }
        }
    }
}

