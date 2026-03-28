using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;

namespace System.MSTests
{
    [TestClass]
    public class ObservableObjectExTests
    {
        private TestObservableObject testObject;

        [TestInitialize]
        public void Setup()
        {
            testObject = new TestObservableObject();
        }

        [TestCleanup]
        public void Cleanup()
        {
            testObject?.Dispose();
        }

        [TestMethod]
        public void Test_Notify_01()
        {
            var x = new ExportProductPhotoData();
            x.ProductId = 1;
            x.ProductId = 0;
        }

        [TestMethod]
        public void Constructor_InitializesEmptyValues()
        {
            // Assert
            Assert.IsNotNull(testObject);
        }

        [TestMethod]
        public void Get_Set_WithCallerMemberName_WorksCorrectly()
        {
            // Arrange
            var expected = "test value";

            // Act
            testObject.TestProperty = expected;
            var actual = testObject.TestProperty;

            // Assert
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void Get_WithNullPropertyName_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => testObject.Get(null));
        }

        [TestMethod]
        public void Set_WithValue_UpdatesAndRaisesEvents()
        {
            // Arrange
            var propertyChangedRaised = false;
            var propertyChangingRaised = false;
            var expected = 42;

            testObject.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(TestObservableObject.IntProperty))
                    propertyChangedRaised = true;
            };

            testObject.PropertyChanging += (s, e) =>
            {
                if (e.PropertyName == nameof(TestObservableObject.IntProperty))
                    propertyChangingRaised = true;
            };

            // Act
            testObject.IntProperty = expected;

            // Assert
            Assert.IsTrue(propertyChangingRaised);
            Assert.IsTrue(propertyChangedRaised);
            Assert.AreEqual(expected, testObject.IntProperty);
        }

        [TestMethod]
        public void Set_WithSameValue_DoesNotRaiseEvents()
        {
            // Arrange
            testObject.IntProperty = 42;
            var eventRaised = false;

            testObject.PropertyChanged += (s, e) => eventRaised = true;

            // Act
            testObject.IntProperty = 42;

            // Assert
            Assert.IsFalse(eventRaised);
        }

        [TestMethod]
        public void Set_WithRefAndOnChangedCallback_InvokesCallback()
        {
            // Arrange
            var callbackInvoked = false;
            var field = 0;

            // Act
            var result = testObject.Set(ref field, 42, () => callbackInvoked = true, nameof(TestObservableObject.IntProperty));

            // Assert
            Assert.IsTrue(result);
            Assert.IsTrue(callbackInvoked);
            Assert.AreEqual(42, field);
        }

        [TestMethod]
        public void Set_WithRefAndValueCallback_InvokesCallbackWithValue()
        {
            // Arrange
            int? callbackValue = null;
            var field = 0;

            // Act
            var result = testObject.Set(ref field, 42, (val) => callbackValue = val, nameof(TestObservableObject.IntProperty));

            // Assert
            Assert.IsTrue(result);
            Assert.AreEqual(42, callbackValue);
            Assert.AreEqual(42, field);
        }

        [TestMethod]
        public void Indexer_GetSet_WorksCorrectly()
        {
            // Arrange
            var propertyName = nameof(TestObservableObject.TestProperty);
            var expected = "indexer value";

            // Act
            testObject[propertyName] = expected;
            var actual = testObject[propertyName];

            // Assert
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void OnPropertyChanged_InvokesEvent()
        {
            // Arrange
            var eventRaised = false;
            string raisedPropertyName = null;

            testObject.PropertyChanged += (s, e) =>
            {
                eventRaised = true;
                raisedPropertyName = e.PropertyName;
            };

            // Act
            testObject.TriggerPropertyChanged("TestProperty");

            // Assert
            Assert.IsTrue(eventRaised);
            Assert.AreEqual("TestProperty", raisedPropertyName);
        }

        [TestMethod]
        public void OnPropertyChanging_InvokesEvent()
        {
            // Arrange
            var eventRaised = false;
            string raisedPropertyName = null;

            testObject.PropertyChanging += (s, e) =>
            {
                eventRaised = true;
                raisedPropertyName = e.PropertyName;
            };

            // Act
            testObject.TriggerPropertyChanging("TestProperty");

            // Assert
            Assert.IsTrue(eventRaised);
            Assert.AreEqual("TestProperty", raisedPropertyName);
        }

        [TestMethod]
        public void BindPropertyChange_WithChildProperty_HandlesSubscription()
        {
            // Arrange
            var childObject = new TestChildObject();
            TestChildObject oldObject = null;
            var handlerInvoked = false;

            // Act
            testObject.BindPropertyChange(ref oldObject, childObject, "ChildProperty", () => handlerInvoked = true);

            // Trigger child property change
            childObject.TriggerPropertyChanged("ChildProperty");
            Assert.IsTrue(handlerInvoked);
        }

        [TestMethod]
        public void BindPropertyChange_WithNullHandler_DoesNotThrow()
        {
            // Arrange
            var childObject = new TestChildObject();
            TestChildObject oldObject = null;

            // Act & Assert
            testObject.BindPropertyChange(ref oldObject, childObject, "ChildProperty", null);
        }

        [TestMethod]
        public void BindPropertyChange_WithNullChildPropertyName_HandlesAnyPropertyChange()
        {
            // Arrange
            var childObject = new TestChildObject();
            TestChildObject oldObject = null;
            var handlerInvoked = false;

            // Act
            testObject.BindPropertyChange(ref oldObject, childObject, null, () => handlerInvoked = true);

            // Trigger any property change
            childObject.TriggerPropertyChanged("AnyProperty");

            // Assert
            Assert.IsTrue(handlerInvoked);
        }

        [TestMethod]
        public void BindPropertyChange_ReplacesOldSubscription()
        {
            // Arrange
            var oldChild = new TestChildObject();
            var newChild = new TestChildObject();
            var oldHandlerInvoked = false;
            var newHandlerInvoked = false;

            testObject.BindPropertyChange(ref oldChild, oldChild, "ChildProperty", () => oldHandlerInvoked = true);

            // Act - replace with new child
            testObject.BindPropertyChange(ref oldChild, newChild, "ChildProperty", () => newHandlerInvoked = true);

            // Trigger old child property change
            oldChild.TriggerPropertyChanged("ChildProperty");
            Assert.IsFalse(oldHandlerInvoked);

            // Trigger new child property change
            newChild.TriggerPropertyChanged("ChildProperty");
            Assert.IsTrue(newHandlerInvoked);
        }

        [TestMethod]
        public void Dispose_CleansUpSubscriptions()
        {
            // Arrange
            var childObject = new TestChildObject();
            TestChildObject oldObject = null;
            var handlerInvoked = false;

            testObject.BindPropertyChange(ref oldObject, childObject, "ChildProperty", () => handlerInvoked = true);

            // Act
            testObject.Dispose();

            // Trigger child property change after dispose
            childObject.TriggerPropertyChanged("ChildProperty");

            // Assert
            Assert.IsFalse(handlerInvoked);
        }

        [TestMethod]
        public void Dispose_MultipleCalls_DoesNotThrow()
        {
            // Arrange
            testObject.Dispose();

            // Act & Assert
            testObject.Dispose(); // Second dispose should not throw
        }

        [TestMethod]
        public void Finalizer_DoesNotThrow()
        {
            // This test just ensures the finalizer doesn't throw
            var obj = new TestObservableObject();
            obj = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    // Test helper classes
    public class TestObservableObject : ObservableObjectEx
    {
        public string TestProperty
        {
            get => Get<string>();
            set => Set(value);
        }

        public int IntProperty
        {
            get => Get<int>();
            set => Set(value);
        }

        public TestChildObject Child
        {
            get => Get<TestChildObject>();
            set => Set(value);
        }

        public void TriggerPropertyChanged(string propertyName)
        {
            OnPropertyChanged(propertyName);
        }

        public void TriggerPropertyChanging(string propertyName)
        {
            OnPropertyChanging(propertyName);
        }

        public ConcurrentDictionary<object, EventHandlers> GetSubscriptions()
        {
            // Use reflection or add internal helper for testing
            var field = typeof(ObservableObjectEx).GetField("subscriptions",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (ConcurrentDictionary<object, EventHandlers>)field.GetValue(this);
        }
    }

    public class ExportProductPhotoData : ObservableObjectEx
    {
        public ExportProductPhotoData()
        {
            this.PropertyChanged += ExportProductPhotoData_PropertyChanged;
        }

        public string AdamasArticul { get; internal set; }

        public bool CanExport { get; internal set; }

        public string ColorId { get; internal set; }

        public string FileName { get; private set; }

        public byte[] Photo { get; internal set; }

        public string ProductCode { get; internal set; }

        public int ProductId { get => Get<int>(); set => Set(value); }

        private void ExportProductPhotoData_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(this.CanExport))
                return;

            this.CanExport = this.ProductId > 0;
            this.NotifyPropertyChanged(nameof(this.CanExport));
        }
    }

    public class TestChildObject : INotifyPropertyChanged, INotifyPropertyChanging
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public event PropertyChangingEventHandler PropertyChanging;

        public void TriggerPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void TriggerPropertyChanging(string propertyName)
        {
            PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(propertyName));
        }
    }

    // Internal helper class for testing
    public class EventHandlers
    {
        public PropertyChangedEventHandler Changed { get; set; }
        public PropertyChangingEventHandler Changing { get; set; }
    }
}