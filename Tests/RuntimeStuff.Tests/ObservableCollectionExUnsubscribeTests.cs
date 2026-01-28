using System.Collections.Specialized;
using System.ComponentModel;

namespace RuntimeStuff.MSTests
{
    [TestClass]
    public class ObservableCollectionExUnsubscribeTests
    {
        private class TestItem : INotifyPropertyChanged
        {
            private string _name;

            public string Name
            {
                get => _name;
                set
                {
                    if (_name != value)
                    {
                        _name = value;
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
                    }
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
        }

        [TestMethod]
        public void ItemPropertyChanged_ShouldRaiseCollectionChanged_AfterSubscribe()
        {
            // Arrange
            var collection = new ObservableCollectionEx<TestItem>();
            var item = new TestItem { Name = "Test" };
            collection.Add(item);

            bool collectionChangedRaised = false;
            collection.CollectionChanged += (sender, args) =>
            {
                if (args.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
                {
                    collectionChangedRaised = true;
                }
            };

            // Act
            item.Name = "Updated";

            // Assert
            Assert.IsTrue(collectionChangedRaised,
                "Изменение свойства элемента должно вызывать CollectionChanged");
        }

        [TestMethod]
        public void ItemPropertyChanged_ShouldNotRaiseCollectionChanged_AfterUnsubscribe()
        {
            // Arrange
            var collection = new ObservableCollectionEx<TestItem>();
            var item = new TestItem { Name = "Test" };
            collection.Add(item);

            bool collectionChangedRaised = false;
            collection.CollectionChanged += (sender, args) =>
            {
                collectionChangedRaised = true;
            };

            // Удаляем элемент (должна произойти отписка)
            collection.Remove(item);
            collectionChangedRaised = false; // Сбрасываем флаг

            // Act
            item.Name = "Updated";

            // Assert
            Assert.IsFalse(collectionChangedRaised,
                "Изменение свойства удаленного элемента не должно вызывать CollectionChanged");
        }

        [TestMethod]
        public void RemoveItem_ShouldNotReceivePropertyChangedNotifications()
        {
            // Arrange
            var collection = new ObservableCollectionEx<TestItem>();
            var item = new TestItem { Name = "Test" };
            collection.Add(item);

            bool collectionChangedOnItemUpdate = false;
            collection.CollectionChanged += (sender, args) =>
            {
                if (args.Action == NotifyCollectionChangedAction.Reset)
                {
                    collectionChangedOnItemUpdate = true;
                }
            };

            // Проверяем, что при изменении элемента срабатывает событие
            item.Name = "Updated1";
            Assert.IsTrue(collectionChangedOnItemUpdate,
                "До удаления: изменение элемента должно вызывать CollectionChanged");

            collectionChangedOnItemUpdate = false;

            // Act
            collection.Remove(item);

            // Assert
            item.Name = "Updated2";
            Assert.IsFalse(collectionChangedOnItemUpdate,
                "После удаления: изменение элемента НЕ должно вызывать CollectionChanged");
        }

        [TestMethod]
        public void Clear_ShouldUnsubscribeFromAllItems()
        {
            // Arrange
            var collection = new ObservableCollectionEx<TestItem>();
            var items = Enumerable.Range(1, 3)
                .Select(i => new TestItem { Name = $"Item{i}" })
                .ToList();

            collection.AddRange(items);

            int collectionChangedCount = 0;
            collection.CollectionChanged += (sender, args) =>
            {
                if (args.Action == NotifyCollectionChangedAction.Reset)
                {
                    collectionChangedCount++;
                }
            };

            // Act - очищаем коллекцию
            collection.Clear();

            // Assert - изменяем свойства удаленных элементов
            foreach (var item in items)
            {
                var nameBefore = item.Name;
                item.Name = $"{nameBefore}_Updated";
            }

            Assert.AreEqual(0, collectionChangedCount,
                "После очистки коллекции изменения элементов не должны вызывать события");
        }

        [TestMethod]
        public void RemoveRange_ShouldUnsubscribeFromRemovedItems()
        {
            // Arrange
            var collection = new ObservableCollectionEx<TestItem>();
            var itemsToRemove = Enumerable.Range(1, 3)
                .Select(i => new TestItem { Name = $"Remove{i}" })
                .ToList();

            var itemToKeep = new TestItem { Name = "KeepMe" };

            collection.AddRange(itemsToRemove);
            collection.Add(itemToKeep);

            int collectionChangedCount = 0;
            collection.CollectionChanged += (sender, args) =>
            {
                if (args.Action == NotifyCollectionChangedAction.Reset)
                {
                    collectionChangedCount++;
                }
            };

            // Act
            collection.RemoveRange(itemsToRemove);
            collectionChangedCount = 0; // Сбрасываем счетчик

            // Assert - изменяем удаленные элементы
            foreach (var item in itemsToRemove)
            {
                item.Name = $"{item.Name}_Updated";
            }

            Assert.AreEqual(0, collectionChangedCount,
                "Изменение удаленных элементов не должно вызывать события");

            // Проверяем, что оставшийся элемент все еще отслеживается
            itemToKeep.Name = "Updated";
            Assert.AreEqual(1, collectionChangedCount,
                "Изменение элемента, оставшегося в коллекции, должно вызывать событие");
        }

        [TestMethod]
        public void ReplaceItem_ShouldUnsubscribeFromOldAndSubscribeToNew()
        {
            // Arrange
            var collection = new ObservableCollectionEx<TestItem>();
            var oldItem = new TestItem { Name = "Old" };
            var newItem = new TestItem { Name = "New" };

            collection.Add(oldItem);

            int collectionChangedCount = 0;
            collection.CollectionChanged += (sender, args) =>
            {
                if (args.Action == NotifyCollectionChangedAction.Reset)
                {
                    collectionChangedCount++;
                }
            };

            // Act
            collection[0] = newItem;
            collectionChangedCount = 0; // Сбрасываем счетчик после замены

            // Assert
            // Старый элемент больше не должен вызывать события
            oldItem.Name = "Old_Updated";
            Assert.AreEqual(0, collectionChangedCount,
                "Изменение старого элемента после замены не должно вызывать события");

            // Новый элемент должен вызывать события
            newItem.Name = "New_Updated";
            Assert.AreEqual(1, collectionChangedCount,
                "Изменение нового элемента должно вызывать события");
        }

        [TestMethod]
        public void MultipleAddRemove_ShouldNotCauseMemoryLeak()
        {
            // Arrange
            var collection = new ObservableCollectionEx<TestItem>();
            int eventCount = 0;

            // Act & Assert
            for (int i = 0; i < 100; i++)
            {
                var item = new TestItem { Name = $"Item{i}" };
                collection.Add(item);

                // Подписываемся на событие коллекции
                collection.CollectionChanged += (sender, args) =>
                {
                    if (args.Action == NotifyCollectionChangedAction.Reset)
                    {
                        eventCount++;
                    }
                };

                // Изменяем элемент - должно вызывать событие
                item.Name = $"{item.Name}_Updated";
                Assert.IsTrue(eventCount > 0, $"На итерации {i}: событие должно вызываться");
                eventCount = 0;

                // Удаляем элемент
                collection.Remove(item);

                // Еще раз изменяем элемент - НЕ должно вызывать событие
                item.Name = $"{item.Name}_Again";
                Assert.AreEqual(0, eventCount, $"На итерации {i}: после удаления событие НЕ должно вызываться");

                // Убираем обработчик события
                collection.CollectionChanged -= (sender, args) =>
                {
                    if (args.Action == NotifyCollectionChangedAction.Reset)
                    {
                        eventCount++;
                    }
                };
            }
        }

        [TestMethod]
        public void DisposeItems_ShouldNotThrowAfterRemoval()
        {
            // Arrange
            var collection = new ObservableCollectionEx<TestItem>();
            var item = new TestItem { Name = "Test" };
            collection.Add(item);

            // Act
            collection.Remove(item);

            // Assert - изменение свойства после удаления не должно вызывать исключений
            try
            {
                item.Name = "Updated";
                // Если мы здесь, значит исключений не было - это хорошо
                Assert.IsTrue(true);
            }
            catch
            {
                Assert.Fail("Изменение свойства удаленного элемента не должно вызывать исключений");
            }
        }

        [TestMethod]
        public void CollectionChanged_ShouldFireForCurrentItemsOnly()
        {
            // Arrange
            var collection = new ObservableCollectionEx<TestItem>();
            var item1 = new TestItem { Name = "Item1" };
            var item2 = new TestItem { Name = "Item2" };

            collection.Add(item1);
            collection.Add(item2);

            int resetEventsCount = 0;
            collection.CollectionChanged += (sender, args) =>
            {
                if (args.Action == NotifyCollectionChangedAction.Reset)
                {
                    resetEventsCount++;
                }
            };

            // Act & Assert
            // Изменяем первый элемент - должно вызвать событие
            item1.Name = "Item1_Updated";
            Assert.AreEqual(1, resetEventsCount, "Изменение элемента в коллекции должно вызывать событие");

            // Удаляем второй элемент
            collection.Remove(item2);
            resetEventsCount = 0;

            // Изменяем удаленный элемент - НЕ должно вызывать событие
            item2.Name = "Item2_Updated";
            Assert.AreEqual(0, resetEventsCount, "Изменение удаленного элемента не должно вызывать событие");

            // Изменяем первый элемент (все еще в коллекции) - должно вызывать событие
            item1.Name = "Item1_UpdatedAgain";
            Assert.AreEqual(1, resetEventsCount, "Изменение элемента, оставшегося в коллекции, должно вызывать событие");
        }
    }
}
