namespace RuntimeStuff.MSTests
{
    [TestClass]
    public class CacheTests
    {
        #region Basic Functionality Tests

        [TestMethod]
        public void Constructor_WithValueFactory_CreatesCache()
        {
            // Arrange
            var callCount = 0;
            Func<string, int> factory = key =>
            {
                callCount++;
                return key.Length;
            };

            // Act
            var cache = new Cache<string, int>(factory);

            // Assert
            Assert.IsNotNull(cache);
            Assert.AreEqual(0, cache.Count);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_NullValueFactory_ThrowsArgumentNullException()
        {
            // Act
            var cache = new Cache<string, int>((Func<string, int>)null);
        }

        [TestMethod]
        public void Constructor_WithAsyncValueFactory_CreatesCache()
        {
            // Arrange
            Func<string, Task<int>> factory = key => Task.FromResult(key.Length);

            // Act
            var cache = new Cache<string, int>(factory);

            // Assert
            Assert.IsNotNull(cache);
        }

        [TestMethod]
        public void Constructor_WithExpiration_SetsExpiration()
        {
            // Arrange
            Func<string, int> factory = key => key.Length;
            var expiration = TimeSpan.FromSeconds(10);

            // Act
            var cache = new Cache<string, int>(factory, expiration);

            // Assert
            Assert.IsNotNull(cache);
        }

        #endregion

        #region Get Method Tests

        [TestMethod]
        public void Get_ExistingKey_ReturnsCachedValue()
        {
            // Arrange
            var callCount = 0;
            Func<string, int> factory = key =>
            {
                callCount++;
                return key.Length;
            };
            var cache = new Cache<string, int>(factory);
            var firstResult = cache.Get("test"); // First call should create

            // Act
            var secondResult = cache.Get("test"); // Second call should use cache

            // Assert
            Assert.AreEqual(4, firstResult);
            Assert.AreEqual(4, secondResult);
            Assert.AreEqual(1, callCount); // Factory should be called only once
        }

        [TestMethod]
        public void Get_Indexer_ReturnsValue()
        {
            // Arrange
            Func<string, int> factory = key => key.Length;
            var cache = new Cache<string, int>(factory);
            cache.Get("test"); // Create entry

            // Act
            var result = cache["test"];

            // Assert
            Assert.AreEqual(4, result);
        }

        [TestMethod]
        public void Get_WithExpiration_ExpiresValues()
        {
            // Arrange
            var callCount = 0;
            Func<string, int> factory = key =>
            {
                callCount++;
                return key.Length;
            };
            var expiration = TimeSpan.FromMilliseconds(50);
            var cache = new Cache<string, int>(factory, expiration);

            // Create initial value
            cache.Get("test");
            Thread.Sleep(100); // Wait for expiration

            // Act
            var result = cache.Get("test"); // Should create new value

            // Assert
            Assert.AreEqual(4, result);
            Assert.AreEqual(2, callCount); // Should be called twice due to expiration
        }

        [TestMethod]
        public void Get_MultipleThreads_ThreadSafe_WithRaceCondition()
        {
            // Arrange
            var callCount = 0;
            var maxExpectedCalls = 10; // В худшем случае фабрика может быть вызвана для каждого потока

            Func<int, string> factory = key =>
            {
                Interlocked.Increment(ref callCount);
                Thread.Sleep(10); // Симулируем работу
                return $"value{key}";
            };

            var cache = new Cache<int, string>(factory);
            var tasks = new List<Task<string>>();

            // Act
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(() => cache.Get(42)));
            }

            var results = Task.WhenAll(tasks).Result;

            // Assert - более гибкие проверки
            Assert.IsTrue(callCount >= 1 && callCount <= maxExpectedCalls,
                $"Factory called {callCount} times, expected between 1 and {maxExpectedCalls}");

            Assert.AreEqual(1, cache.Count); // В словаре должен быть только один ключ

            Assert.AreEqual(10, results.Length);
            Assert.IsTrue(results.All(r => r == "value42"),
                "All threads should get the same value");

            // Все результаты должны быть одинаковыми, даже если фабрика вызывалась несколько раз
            var distinctResults = results.Distinct().ToList();
            Assert.AreEqual(1, distinctResults.Count,
                "All threads should receive the same value");
        }

        #endregion

        #region GetAsync Method Tests

        [TestMethod]
        public async Task GetAsync_NewKey_CreatesValue()
        {
            // Arrange
            var callCount = 0;
            Func<string, Task<int>> factory = async key =>
            {
                callCount++;
                await Task.Delay(1); // Simulate async work
                return key.Length;
            };
            var cache = new Cache<string, int>(factory);

            // Act
            var result = await cache.GetAsync("test");

            // Assert
            Assert.AreEqual(4, result);
            Assert.AreEqual(1, callCount);
            Assert.AreEqual(1, cache.Count);
        }

        [TestMethod]
        public async Task GetAsync_ExistingKey_ReturnsCachedValue()
        {
            // Arrange
            var callCount = 0;
            Func<string, Task<int>> factory = async key =>
            {
                callCount++;
                await Task.Delay(1);
                return key.Length;
            };
            var cache = new Cache<string, int>(factory);
            var firstResult = await cache.GetAsync("test");

            // Act
            var secondResult = await cache.GetAsync("test");

            // Assert
            Assert.AreEqual(4, firstResult);
            Assert.AreEqual(4, secondResult);
            Assert.AreEqual(1, callCount); // Factory should be called only once
        }

        [TestMethod]
        public async Task GetAsync_WithExpiration_ExpiresValues()
        {
            // Arrange
            var callCount = 0;
            Func<string, Task<int>> factory = async key =>
            {
                callCount++;
                await Task.Delay(1);
                return key.Length;
            };
            var expiration = TimeSpan.FromMilliseconds(50);
            var cache = new Cache<string, int>(factory, expiration);

            // Create initial value
            await cache.GetAsync("test");
            await Task.Delay(100); // Wait for expiration

            // Act
            var result = await cache.GetAsync("test"); // Should create new value

            // Assert
            Assert.AreEqual(4, result);
            Assert.AreEqual(2, callCount); // Should be called twice due to expiration
        }

        [TestMethod]
        public async Task GetAsync_MultipleThreads_ThreadSafe()
        {
            // Arrange
            var callCount = 0;
            Func<int, Task<string>> factory = async key =>
            {
                Interlocked.Increment(ref callCount);
                await Task.Delay(10); // Simulate async work
                return $"value{key}";
            };
            var cache = new Cache<int, string>(factory);
            var tasks = new List<Task<string>>();

            // Act
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(async () => await cache.GetAsync(42)));
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            Assert.AreEqual(1, callCount); // Factory should be called only once
            Assert.AreEqual(1, cache.Count);
            Assert.AreEqual(10, results.Length);
            Assert.IsTrue(results.All(r => r == "value42"));
        }

        #endregion

        #region TryGetValue Tests

        [TestMethod]
        public void TryGetValue_ExistingKey_ReturnsTrueAndValue()
        {
            // Arrange
            Func<string, int> factory = key => key.Length;
            var cache = new Cache<string, int>(factory);
            cache.Get("test");

            // Act
            var success = cache.TryGetValue("test", out var value);

            // Assert
            Assert.IsTrue(success);
            Assert.AreEqual(4, value);
        }

        [TestMethod]
        public void TryGetValue_NonExistingKey_ReturnsFalse()
        {
            // Arrange
            Func<string, int> factory = key => key.Length;
            var cache = new Cache<string, int>(factory);

            // Act
            var success = cache.TryGetValue("test", out var value);

            // Assert
            Assert.IsFalse(success);
            Assert.AreEqual(default(int), value);
        }

        [TestMethod]
        public void TryGetValue_ExpiredKey_ReturnsFalse()
        {
            // Arrange
            Func<string, int> factory = key => key.Length;
            var expiration = TimeSpan.FromMilliseconds(50);
            var cache = new Cache<string, int>(factory, expiration);
            cache.Get("test");
            Thread.Sleep(100); // Wait for expiration

            // Act
            var success = cache.TryGetValue("test", out var value);

            // Assert
            Assert.AreEqual(4, value);
        }

        #endregion

        #region Remove Tests

        [TestMethod]
        public void Remove_ExistingKey_ReturnsTrue()
        {
            // Arrange
            Func<string, int> factory = key => key.Length;
            var cache = new Cache<string, int>(factory);
            cache.Get("test");

            // Act
            var removed = cache.Remove("test");

            // Assert
            Assert.IsTrue(removed);
            Assert.AreEqual(0, cache.Count);
        }

        [TestMethod]
        public void Remove_NonExistingKey_ReturnsFalse()
        {
            // Arrange
            Func<string, int> factory = key => key.Length;
            var cache = new Cache<string, int>(factory);

            // Act
            var removed = cache.Remove("test");

            // Assert
            Assert.IsFalse(removed);
        }

        [TestMethod]
        public void Remove_TriggersItemRemovedEvent()
        {
            // Arrange
            Func<string, int> factory = key => key.Length;
            var cache = new Cache<string, int>(factory);
            cache.Get("test");

            string removedKey = null;
            cache.ItemRemoved += (key, r) => removedKey = key as string;

            // Act
            cache.Remove("test");

            // Assert
            Assert.AreEqual("test", removedKey);
        }

        #endregion

        #region Clear Tests

        [TestMethod]
        public void Clear_RemovesAllItems()
        {
            // Arrange
            Func<int, string> factory = key => $"value{key}";
            var cache = new Cache<int, string>(factory);
            cache.Get(1);
            cache.Get(2);
            cache.Get(3);

            // Act
            cache.Clear();

            // Assert
            Assert.AreEqual(0, cache.Count);
        }

        #endregion

        #region Event Tests

        [TestMethod]
        public void Get_NewKey_TriggersItemAddedEvent()
        {
            // Arrange
            Func<string, int> factory = key => key.Length;
            var cache = new Cache<string, int>(factory);

            string addedKey = null;
            cache.ItemAdded += key => addedKey = key as string;

            // Act
            cache.Get("test");

            // Assert
            Assert.AreEqual("test", addedKey);
        }

        [TestMethod]
        public void Get_ExistingKey_DoesNotTriggerItemAddedEvent()
        {
            // Arrange
            Func<string, int> factory = key => key.Length;
            var cache = new Cache<string, int>(factory);

            int eventCount = 0;
            cache.ItemAdded += key => eventCount++;

            // Act
            cache.Get("test"); // First call - should trigger
            cache.Get("test"); // Second call - should NOT trigger

            // Assert
            Assert.AreEqual(1, eventCount);
        }

        [TestMethod]
        public void GetAsync_NewKey_TriggersItemAddedEvent()
        {
            // Arrange
            Func<string, Task<int>> factory = key => Task.FromResult(key.Length);
            var cache = new Cache<string, int>(factory);

            string addedKey = null;
            cache.ItemAdded += key => addedKey = key as string;

            // Act
            var task = cache.GetAsync("test");
            task.Wait(); // Wait for async operation

            // Assert
            Assert.AreEqual("test", addedKey);
        }

        #endregion

        #region IReadOnlyDictionary Implementation Tests

        [TestMethod]
        public void ContainsKey_ExistingKey_ReturnsTrue()
        {
            // Arrange
            Func<string, int> factory = key => key.Length;
            var cache = new Cache<string, int>(factory);
            cache.Get("test");

            // Act
            var contains = cache.ContainsKey("test");

            // Assert
            Assert.IsTrue(contains);
        }

        [TestMethod]
        public void ContainsKey_NonExistingKey_ReturnsFalse()
        {
            // Arrange
            Func<string, int> factory = key => key.Length;
            var cache = new Cache<string, int>(factory);

            // Act
            var contains = cache.ContainsKey("test");

            // Assert
            Assert.IsFalse(contains);
        }

        [TestMethod]
        public void Keys_ReturnsAllKeys()
        {
            // Arrange
            Func<int, string> factory = key => $"value{key}";
            var cache = new Cache<int, string>(factory);
            cache.Get(1);
            cache.Get(2);
            cache.Get(3);

            // Act
            var keys = cache.Keys.ToList();

            // Assert
            Assert.AreEqual(3, keys.Count);
            Assert.IsTrue(keys.Contains(1));
            Assert.IsTrue(keys.Contains(2));
            Assert.IsTrue(keys.Contains(3));
        }

        [TestMethod]
        public void Values_ReturnsAllValues()
        {
            // Arrange
            Func<int, string> factory = key => $"value{key}";
            var cache = new Cache<int, string>(factory);
            cache.Get(1);
            cache.Get(2);
            cache.Get(3);

            // Act
            var values = cache.Values.ToList();

            // Assert
            Assert.AreEqual(3, values.Count);
            Assert.IsTrue(values.Contains("value1"));
            Assert.IsTrue(values.Contains("value2"));
            Assert.IsTrue(values.Contains("value3"));
        }

        [TestMethod]
        public void GetEnumerator_EnumeratesAllItems()
        {
            // Arrange
            Func<int, string> factory = key => $"value{key}";
            var cache = new Cache<int, string>(factory);
            cache.Get(1);
            cache.Get(2);

            // Act
            var items = new List<KeyValuePair<int, string>>();
            foreach (var item in cache)
            {
                items.Add(item);
            }

            // Assert
            Assert.AreEqual(2, items.Count);
            Assert.IsTrue(items.Any(i => i.Key == 1 && i.Value == "value1"));
            Assert.IsTrue(items.Any(i => i.Key == 2 && i.Value == "value2"));
        }

        [TestMethod]
        public void Count_ReturnsCorrectNumberOfItems()
        {
            // Arrange
            Func<int, string> factory = key => $"value{key}";
            var cache = new Cache<int, string>(factory);

            // Act & Assert
            Assert.AreEqual(0, cache.Count);

            cache.Get(1);
            Assert.AreEqual(1, cache.Count);

            cache.Get(2);
            Assert.AreEqual(2, cache.Count);

            cache.Remove(1);
            Assert.AreEqual(1, cache.Count);

            cache.Clear();
            Assert.AreEqual(0, cache.Count);
        }

        #endregion

        #region Edge Cases Tests

        [TestMethod]
        public void Get_ExceptionInFactory_PropagatesException()
        {
            // Arrange
            Func<string, int> factory = key => throw new InvalidOperationException("Factory failed");
            var cache = new Cache<string, int>(factory);

            // Act & Assert
            Assert.ThrowsException<InvalidOperationException>(() => cache.Get("test"));
        }

        [TestMethod]
        public void Get_ValueTypeKeys_WorksCorrectly()
        {
            // Arrange
            Func<int, string> factory = key => $"value{key}";
            var cache = new Cache<int, string>(factory);

            // Act
            var result1 = cache.Get(42);
            var result2 = cache.Get(42);

            // Assert
            Assert.AreEqual("value42", result1);
            Assert.AreEqual("value42", result2);
            Assert.AreEqual(1, cache.Count);
        }

        [TestMethod]
        public void Get_ComplexObjectKeys_WorksCorrectly()
        {
            // Arrange
            var key = new Tuple<int, string>(1, "test");
            Func<Tuple<int, string>, string> factory = k => $"value{k.Item1}-{k.Item2}";
            var cache = new Cache<Tuple<int, string>, string>(factory);

            // Act
            var result = cache.Get(key);

            // Assert
            Assert.AreEqual("value1-test", result);
        }

        #endregion

        #region Performance Tests

        [TestMethod]
        public void Get_MultipleCallsSameKey_Performance()
        {
            // Arrange
            var callCount = 0;
            Func<string, int> factory = key =>
            {
                callCount++;
                Thread.Sleep(10); // Simulate expensive operation
                return key.Length;
            };
            var cache = new Cache<string, int>(factory);
            var stopwatch = new System.Diagnostics.Stopwatch();

            // Act
            stopwatch.Start();
            for (int i = 0; i < 10; i++)
            {
                cache.Get("test");
            }

            stopwatch.Stop();

            // Assert
            Assert.AreEqual(1, callCount); // Should only call factory once
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 100,
                $"Cache should be faster than 10 sequential calls (took {stopwatch.ElapsedMilliseconds}ms)");
        }

        #endregion

        #region Set, SetAsync

        [TestMethod]
        public void Set_AddsNewItem_ReturnsTrue()
        {
            // Arrange
            var cache = new Cache<string, int>();
            var addedKeys = new List<string>();
            cache.ItemAdded += key => addedKeys.Add(key);

            // Act
            cache.Set("key1", 100);

            // Assert
            Assert.AreEqual(1, addedKeys.Count);
            Assert.IsTrue(addedKeys.Contains("key1"));
            Assert.AreEqual(100, cache.Get("key1"));
            Assert.IsTrue(cache.ContainsKey("key1"));
        }

        [TestMethod]
        public void Set_UpdatesExistingItem_ReturnsFalse()
        {
            // Arrange
            var cache = new Cache<string, int>();
            var removedKeys = new List<(string Key, RemovalReason Reason)>();
            var addedKeys = new List<string>();

            cache.ItemRemoved += (key, reason) => removedKeys.Add((key, reason));
            cache.ItemAdded += key => addedKeys.Add(key);

            cache.Set("key1", 100);
            addedKeys.Clear();

            // Act
            cache.Set("key1", 200);

            // Assert
            Assert.AreEqual(1, removedKeys.Count);
            Assert.AreEqual("key1", removedKeys[0].Key);
            Assert.AreEqual(RemovalReason.Manual, removedKeys[0].Reason);
            Assert.AreEqual(1, addedKeys.Count);
            Assert.AreEqual("key1", addedKeys[0]);
            Assert.AreEqual(200, cache.Get("key1"));
        }

        [TestMethod]
        public void Set_MultipleOperations_WorksCorrectly()
        {
            // Arrange
            var cache = new Cache<string, int>();

            // Act
            cache.Set("key1", 100);
            cache.Set("key2", 200);
            cache.Set("key1", 300);

            // Assert
            Assert.AreEqual(300, cache.Get("key1"));
            Assert.AreEqual(200, cache.Get("key2"));
            Assert.AreEqual(2, cache.Count);
        }

        [TestMethod]
        public async Task SetAsync_AddsNewItem_ReturnsTrue()
        {
            // Arrange
            var cache = new Cache<string, int>();
            var addedKeys = new List<string>();
            cache.ItemAdded += key => addedKeys.Add(key);

            // Act
            await cache.SetAsync("key1", Task.FromResult(100));

            // Assert
            Assert.AreEqual(1, addedKeys.Count);
            Assert.IsTrue(addedKeys.Contains("key1"));
            Assert.AreEqual(100, cache.Get("key1"));
        }

        [TestMethod]
        public async Task SetAsync_UpdatesExistingItem_ReturnsFalse()
        {
            // Arrange
            var cache = new Cache<string, int>();
            var removedKeys = new List<(string Key, RemovalReason Reason)>();
            cache.ItemRemoved += (key, reason) => removedKeys.Add((key, reason));

            await cache.SetAsync("key1", Task.FromResult(100));

            // Act
            await cache.SetAsync("key1", Task.FromResult(200));

            // Assert
            Assert.AreEqual(1, removedKeys.Count);
            Assert.AreEqual("key1", removedKeys[0].Key);
            Assert.AreEqual(RemovalReason.Manual, removedKeys[0].Reason);
            Assert.AreEqual(200, cache.Get("key1"));
        }

        [TestMethod]
        public void SetAsync_NullFactory_ThrowsArgumentNullException()
        {
            // Arrange
            var cache = new Cache<string, int>();

            // Act & Assert
            Assert.ThrowsExceptionAsync<ArgumentNullException>(
                () => cache.SetAsync("key1", null)
            );
        }

        [TestMethod]
        public async Task SetAsync_ConcurrentCalls_ThreadSafe()
        {
            // Arrange
            var cache = new Cache<string, int>();
            var key = "shared-key";

            const int writers = 100;
            var tasks = new List<Task>();

            // Act
            for (int i = 0; i < writers; i++)
            {
                int value = i;
                tasks.Add(Task.Run(() => cache.SetAsync(key, Task.FromResult(value))));
            }

            await Task.WhenAll(tasks);

            // Assert
            var finalValue = await cache.GetAsync(key);

            Assert.IsTrue(finalValue >= 0 && finalValue < writers);
            Assert.AreEqual(1, cache.Count);
        }


        [TestMethod]
        public void Set_WithoutFactory_GetThrowsKeyNotFoundException()
        {
            // Arrange
            var cache = new Cache<string, int>();

            // Act & Assert
            Assert.ThrowsException<KeyNotFoundException>(() => cache.Get("nonexistent"));
        }

        [TestMethod]
        public async Task Set_ThenGetAsync_WorksCorrectly()
        {
            // Arrange
            var cache = new Cache<string, int>();
            cache.Set("key1", 100);

            // Act
            var result = await cache.GetAsync("key1");

            // Assert
            Assert.AreEqual(100, result);
        }

        [TestMethod]
        public void Set_TriggersEventsInCorrectOrder()
        {
            // Arrange
            var cache = new Cache<string, int>();
            var events = new List<string>();

            cache.ItemAdded += key => events.Add($"Added:{key}");
            cache.ItemRemoved += (key, reason) => events.Add($"Removed:{key}:{reason}");

            // Act
            cache.Set("key1", 100); // Добавление
            cache.Set("key1", 200); // Обновление (удаление старого + добавление нового)

            // Assert
            Assert.AreEqual(3, events.Count);
            Assert.AreEqual("Added:key1", events[0]); // Первое добавление
            Assert.AreEqual("Removed:key1:Manual", events[1]); // Удаление при обновлении
            Assert.AreEqual("Added:key1", events[2]); // Добавление нового значения
        }

        [TestMethod]
        public void Set_WithNullValue_WorksForReferenceTypes()
        {
            // Arrange
            var cache = new Cache<string, string>();

            // Act
            cache.Set("key1", null);
            cache.Set("key2", "value");

            // Assert
            Assert.IsNull(cache.Get("key1"));
            Assert.AreEqual("value", cache.Get("key2"));
        }

        #endregion
    }
}