using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RuntimeStuff.MSTests
{
    [TestClass]
    public class PropertyChangedBaseTests
    {
        [TestMethod]
        public void Get_WhenPropertyNameIsNull_ThrowsArgumentNullException()
        {
            // Arrange
            var testObj = new TestViewModel();

            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => testObj.Get<int>(null));
        }

        [TestMethod]
        public void Get_WhenPropertyNotSet_ReturnsDefault()
        {
            // Arrange
            var testObj = new TestViewModel();

            // Act
            var result = testObj.Get<int>("NonExistentProperty");

            // Assert
            Assert.AreEqual(0, result);
        }

        [TestMethod]
        public void Get_WhenValueIsWrongType_ThrowsInvalidCastException()
        {
            // Arrange
            var testObj = new TestViewModel();
            testObj.Set("string value", propertyName: "StringProperty");

            // Act & Assert
            Assert.ThrowsException<InvalidCastException>(() => testObj.Get<int>("StringProperty"));
        }

        [TestMethod]
        public void Set_WithFieldReference_UpdatesValueAndRaisesEvents()
        {
            // Arrange
            var testObj = new TestViewModel();
            int field = 0;
            bool propertyChangedCalled = false;
            bool propertyChangingCalled = false;
            string changedPropertyName = null;
            string changingPropertyName = null;

            testObj.PropertyChanged += (s, e) =>
            {
                propertyChangedCalled = true;
                changedPropertyName = e.PropertyName;
            };
            testObj.PropertyChanging += (s, e) =>
            {
                propertyChangingCalled = true;
                changingPropertyName = e.PropertyName;
            };

            // Act
            var result = testObj.Set(ref field, 42, propertyName: "TestProperty");

            // Assert
            Assert.IsTrue(result);
            Assert.AreEqual(42, field);
            Assert.IsTrue(propertyChangingCalled);
            Assert.IsTrue(propertyChangedCalled);
            Assert.AreEqual("TestProperty", changingPropertyName);
            Assert.AreEqual("TestProperty", changedPropertyName);
        }

        [TestMethod]
        public void Set_WithFieldReference_WhenValueSame_ReturnsFalseAndNoEvents()
        {
            // Arrange
            var testObj = new TestViewModel();
            int field = 42;
            bool eventCalled = false;
            testObj.PropertyChanged += (s, e) => eventCalled = true;
            testObj.PropertyChanging += (s, e) => eventCalled = true;

            // Act
            var result = testObj.Set(ref field, 42, propertyName: "TestProperty");

            // Assert
            Assert.IsFalse(result);
            Assert.AreEqual(42, field);
            Assert.IsFalse(eventCalled);
        }

        [TestMethod]
        public void Set_WithValue_UpdatesStorageAndRaisesEvents()
        {
            // Arrange
            var testObj = new TestViewModel();
            bool propertyChangedCalled = false;
            bool propertyChangingCalled = false;

            testObj.PropertyChanged += (s, e) => propertyChangedCalled = true;
            testObj.PropertyChanging += (s, e) => propertyChangingCalled = true;

            // Act
            var result = testObj.Set(42, propertyName: "TestProperty");

            // Assert
            Assert.IsTrue(result);
            Assert.AreEqual(42, testObj.Get<int>("TestProperty"));
            Assert.IsTrue(propertyChangingCalled);
            Assert.IsTrue(propertyChangedCalled);
        }

        [TestMethod]
        public void Set_WithValue_WhenValueSame_ReturnsFalseAndNoEvents()
        {
            // Arrange
            var testObj = new TestViewModel();
            testObj.Set(42, propertyName: "TestProperty");
            bool eventCalled = false;
            testObj.PropertyChanged += (s, e) => eventCalled = true;
            testObj.PropertyChanging += (s, e) => eventCalled = true;

            // Act
            var result = testObj.Set(42, propertyName: "TestProperty");

            // Assert
            Assert.IsFalse(result);
            Assert.IsFalse(eventCalled);
        }

        [TestMethod]
        public void Set_WithValue_WhenPropertyNameNull_ThrowsArgumentNullException()
        {
            // Arrange
            var testObj = new TestViewModel();

            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => testObj.Set(42, propertyName: null));
        }

        [TestMethod]
        public void Set_WithFieldReference_InvokesOnChangedAction()
        {
            // Arrange
            var testObj = new TestViewModel();
            int field = 0;
            bool onChangedCalled = false;

            // Act
            testObj.Set(ref field, 42, () => onChangedCalled = true, "TestProperty");

            // Assert
            Assert.IsTrue(onChangedCalled);
        }

        [TestMethod]
        public void SuspendNotifications_WhenSuspended_DoesNotRaiseEvents()
        {
            // Arrange
            var testObj = new TestViewModel();
            bool eventCalled = false;
            testObj.PropertyChanged += (s, e) => eventCalled = true;
            testObj.PropertyChanging += (s, e) => eventCalled = true;

            // Act
            testObj.SuspendNotifications(true);
            testObj.Set(42, propertyName: "TestProperty");

            // Assert
            Assert.IsFalse(eventCalled);
        }

        [TestMethod]
        public void SuspendNotifications_WhenResumed_RaisesEventsAgain()
        {
            // Arrange
            var testObj = new TestViewModel();
            int eventCount = 0;
            testObj.PropertyChanged += (s, e) => eventCount++;
            testObj.PropertyChanging += (s, e) => eventCount++;

            // Act
            testObj.SuspendNotifications(true);
            testObj.Set(42, propertyName: "TestProperty");
            testObj.SuspendNotifications(false);
            testObj.Set(84, propertyName: "TestProperty");

            // Assert
            Assert.AreEqual(2, eventCount); // One changing, one changed for second set only
        }

        [TestMethod]
        public void Get_WithCallerMemberName_GetsCorrectValue()
        {
            // Arrange
            var testObj = new TestViewModelWithCallerMember();

            // Act
            testObj.SetName("John");

            // Assert
            Assert.AreEqual("John", testObj.GetName());
        }

        [TestMethod]
        public void OnPropertyChanged_InvokesCustomHandler()
        {
            // Arrange
            var testObj = new TestViewModelWithCustomHandler();
            bool customHandlerCalled = false;

            // Act
            testObj.Set(42, propertyName: "TestProperty");
            var tp = testObj.Get<int>("TestProperty");

            // Assert
            Assert.IsTrue(testObj.OnTestPropertyChangedCalled);
        }

        [TestMethod]
        public void MultipleProperties_EachMaintainsSeparateValue()
        {
            // Arrange
            var testObj = new TestViewModel();

            // Act
            testObj.Set(42, propertyName: "Property1");
            testObj.Set("Hello", propertyName: "Property2");

            // Assert
            Assert.AreEqual(42, testObj.Get<int>("Property1"));
            Assert.AreEqual("Hello", testObj.Get<string>("Property2"));
        }

        [TestMethod]
        public void PropertyMap_ReusesIndicesForSameProperty()
        {
            // Arrange
            var testObj1 = new TestViewModel();
            var testObj2 = new TestViewModel();

            // Act
            testObj1.Set(42, propertyName: "TestProperty");
            testObj2.Set(84, propertyName: "TestProperty");

            // Assert
            Assert.AreEqual(42, testObj1.Get<int>("TestProperty"));
            Assert.AreEqual(84, testObj2.Get<int>("TestProperty"));
        }

        // Test ViewModels
        private class TestViewModel : ObservableObject
        {
        }

        private class TestViewModelWithCallerMember : ObservableObject
        {
            private string name;

            public void SetName(string value)
            {
                Set(ref name, value);
            }

            public string GetName()
            {
                return name;
            }
        }

        private class TestViewModelWithCustomHandler : ObservableObject
        {
            public bool OnTestPropertyChangedCalled { get; private set; }

            protected virtual void OnTestPropertyChanged()
            {
                OnTestPropertyChangedCalled = true;
            }
        }
    }
}