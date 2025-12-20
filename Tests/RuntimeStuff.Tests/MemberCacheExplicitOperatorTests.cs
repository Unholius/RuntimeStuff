using System.Reflection;

namespace RuntimeStuff.Tests
{
    [TestClass]
    public class MemberCacheExplicitOperatorTests
    {
        #region Тестовые классы

        public class TestClassForOperators
        {
            public int PublicProperty { get; set; }
            private string PrivateField;
            public event EventHandler TestEvent;

            public void PublicMethod() { }
            private void PrivateMethod() { }

            public TestClassForOperators() { }
            public TestClassForOperators(int value) { }
        }

        #endregion

        #region Тест оператора PropertyInfo

        [TestMethod]
        public void ExplicitOperator_ToPropertyInfo_ForProperty_ReturnsPropertyInfo()
        {
            // Arrange
            var property = typeof(TestClassForOperators).GetProperty("PublicProperty");
            var memberCache = MemberCache.Create(property);

            // Act
            PropertyInfo propertyInfo = (PropertyInfo)memberCache;

            // Assert
            Assert.IsNotNull(propertyInfo);
            Assert.AreEqual("PublicProperty", propertyInfo.Name);
            Assert.AreEqual(typeof(int), propertyInfo.PropertyType);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidCastException))]
        public void ExplicitOperator_ToPropertyInfo_ForNonProperty_ThrowsInvalidCastException()
        {
            // Arrange
            var field = typeof(TestClassForOperators).GetField("PrivateField", BindingFlags.NonPublic | BindingFlags.Instance);
            var memberCache = MemberCache.Create(field);

            // Act
            PropertyInfo propertyInfo = (PropertyInfo)memberCache;

            // Assert - ожидается исключение
        }

        [TestMethod]
        public void AsPropertyInfo_And_ExplicitOperator_ReturnSameResult()
        {
            // Arrange
            var property = typeof(TestClassForOperators).GetProperty("PublicProperty");
            var memberCache = MemberCache.Create(property);

            // Act
            var viaMethod = memberCache.AsPropertyInfo();
            var viaOperator = (PropertyInfo)memberCache;

            // Assert
            Assert.AreSame(viaMethod, viaOperator);
        }

        #endregion

        #region Тест оператора FieldInfo

        [TestMethod]
        public void ExplicitOperator_ToFieldInfo_ForField_ReturnsFieldInfo()
        {
            // Arrange
            var field = typeof(TestClassForOperators).GetField("PrivateField", BindingFlags.NonPublic | BindingFlags.Instance);
            var memberCache = MemberCache.Create(field);

            // Act
            FieldInfo fieldInfo = (FieldInfo)memberCache;

            // Assert
            Assert.IsNotNull(fieldInfo);
            Assert.AreEqual("PrivateField", fieldInfo.Name);
            Assert.AreEqual(typeof(string), fieldInfo.FieldType);
            Assert.IsTrue(fieldInfo.IsPrivate);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidCastException))]
        public void ExplicitOperator_ToFieldInfo_ForNonField_ThrowsInvalidCastException()
        {
            // Arrange
            var property = typeof(TestClassForOperators).GetProperty("PublicProperty");
            var memberCache = MemberCache.Create(property);

            // Act
            FieldInfo fieldInfo = (FieldInfo)memberCache;

            // Assert - ожидается исключение
        }

        [TestMethod]
        public void AsFieldInfo_And_ExplicitOperator_ReturnSameResult()
        {
            // Arrange
            var field = typeof(TestClassForOperators).GetField("PrivateField", BindingFlags.NonPublic | BindingFlags.Instance);
            var memberCache = MemberCache.Create(field);

            // Act
            var viaMethod = memberCache.AsFieldInfo();
            var viaOperator = (FieldInfo)memberCache;

            // Assert
            Assert.AreSame(viaMethod, viaOperator);
        }

        #endregion

        #region Тест оператора MethodInfo

        [TestMethod]
        public void ExplicitOperator_ToMethodInfo_ForMethod_ReturnsMethodInfo()
        {
            // Arrange
            var method = typeof(TestClassForOperators).GetMethod("PublicMethod");
            var memberCache = MemberCache.Create(method);

            // Act
            MethodInfo methodInfo = (MethodInfo)memberCache;

            // Assert
            Assert.IsNotNull(methodInfo);
            Assert.AreEqual("PublicMethod", methodInfo.Name);
            Assert.AreEqual(typeof(void), methodInfo.ReturnType);
            Assert.IsTrue(methodInfo.IsPublic);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidCastException))]
        public void ExplicitOperator_ToMethodInfo_ForNonMethod_ThrowsInvalidCastException()
        {
            // Arrange
            var property = typeof(TestClassForOperators).GetProperty("PublicProperty");
            var memberCache = MemberCache.Create(property);

            // Act
            MethodInfo methodInfo = (MethodInfo)memberCache;

            // Assert - ожидается исключение
        }

        [TestMethod]
        public void AsMethodInfo_And_ExplicitOperator_ReturnSameResult()
        {
            // Arrange
            var method = typeof(TestClassForOperators).GetMethod("PublicMethod");
            var memberCache = MemberCache.Create(method);

            // Act
            var viaMethod = memberCache.AsMethodInfo();
            var viaOperator = (MethodInfo)memberCache;

            // Assert
            Assert.AreSame(viaMethod, viaOperator);
        }

        #endregion

        #region Тест оператора EventInfo

        [TestMethod]
        public void ExplicitOperator_ToEventInfo_ForEvent_ReturnsEventInfo()
        {
            // Arrange
            var eventInfo = typeof(TestClassForOperators).GetEvent("TestEvent");
            var memberCache = MemberCache.Create(eventInfo);

            // Act
            EventInfo eventInfoResult = (EventInfo)memberCache;

            // Assert
            Assert.IsNotNull(eventInfoResult);
            Assert.AreEqual("TestEvent", eventInfoResult.Name);
            Assert.AreEqual(typeof(EventHandler), eventInfoResult.EventHandlerType);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidCastException))]
        public void ExplicitOperator_ToEventInfo_ForNonEvent_ThrowsInvalidCastException()
        {
            // Arrange
            var property = typeof(TestClassForOperators).GetProperty("PublicProperty");
            var memberCache = MemberCache.Create(property);

            // Act
            EventInfo eventInfo = (EventInfo)memberCache;

            // Assert - ожидается исключение
        }

        [TestMethod]
        public void AsEventInfo_And_ExplicitOperator_ReturnSameResult()
        {
            // Arrange
            var eventInfo = typeof(TestClassForOperators).GetEvent("TestEvent");
            var memberCache = MemberCache.Create(eventInfo);

            // Act
            var viaMethod = memberCache.AsEventInfo();
            var viaOperator = (EventInfo)memberCache;

            // Assert
            Assert.AreSame(viaMethod, viaOperator);
        }

        #endregion

        #region Тест оператора ConstructorInfo

        [TestMethod]
        public void ExplicitOperator_ToConstructorInfo_ForConstructor_ReturnsConstructorInfo()
        {
            // Arrange
            var constructor = typeof(TestClassForOperators).GetConstructor(Type.EmptyTypes);
            var memberCache = MemberCache.Create(constructor);

            // Act
            ConstructorInfo constructorInfo = (ConstructorInfo)memberCache;

            // Assert
            Assert.IsNotNull(constructorInfo);
            Assert.AreEqual(typeof(TestClassForOperators), constructorInfo.DeclaringType);
            Assert.AreEqual(0, constructorInfo.GetParameters().Length);
        }

        [TestMethod]
        public void ExplicitOperator_ToConstructorInfo_ForParameterizedConstructor_ReturnsConstructorInfo()
        {
            // Arrange
            var constructor = typeof(TestClassForOperators).GetConstructor(new[] { typeof(int) });
            var memberCache = MemberCache.Create(constructor);

            // Act
            ConstructorInfo constructorInfo = (ConstructorInfo)memberCache;

            // Assert
            Assert.IsNotNull(constructorInfo);
            Assert.AreEqual(typeof(TestClassForOperators), constructorInfo.DeclaringType);
            Assert.AreEqual(1, constructorInfo.GetParameters().Length);
            Assert.AreEqual(typeof(int), constructorInfo.GetParameters()[0].ParameterType);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidCastException))]
        public void ExplicitOperator_ToConstructorInfo_ForNonConstructor_ThrowsInvalidCastException()
        {
            // Arrange
            var property = typeof(TestClassForOperators).GetProperty("PublicProperty");
            var memberCache = MemberCache.Create(property);

            // Act
            ConstructorInfo constructorInfo = (ConstructorInfo)memberCache;

            // Assert - ожидается исключение
        }

        [TestMethod]
        public void AsConstructorInfo_And_ExplicitOperator_ReturnSameResult()
        {
            // Arrange
            var constructor = typeof(TestClassForOperators).GetConstructor(Type.EmptyTypes);
            var memberCache = MemberCache.Create(constructor);

            // Act
            var viaMethod = memberCache.AsConstructorInfo();
            var viaOperator = (ConstructorInfo)memberCache;

            // Assert
            Assert.AreSame(viaMethod, viaOperator);
        }

        #endregion

        #region Тесты для Type (нет оператора, но проверка AsType)

        [TestMethod]
        public void AsType_ForType_ReturnsType()
        {
            // Arrange
            var type = typeof(TestClassForOperators);
            var memberCache = MemberCache.Create(type);

            // Act
            var typeResult = memberCache.AsType();

            // Assert
            Assert.IsNotNull(typeResult);
            Assert.AreEqual(typeof(TestClassForOperators), typeResult);
        }

        [TestMethod]
        public void AsType_ForNonType_ReturnsNull()
        {
            // Arrange
            var property = typeof(TestClassForOperators).GetProperty("PublicProperty");
            var memberCache = MemberCache.Create(property);

            // Act
            var typeResult = memberCache.AsType();

            // Assert
            Assert.IsNull(typeResult);
        }

        #endregion

        #region Комплексные тесты с проверкой поведения

        [TestMethod]
        public void ExplicitOperator_InSwitchStatement_WorksCorrectly()
        {
            // Arrange
            var property = typeof(TestClassForOperators).GetProperty("PublicProperty");
            var memberCache = MemberCache.Create(property);
            string result = null;

            // Act
            switch (memberCache.MemberType)
            {
                case MemberTypes.Property:
                    PropertyInfo propInfo = (PropertyInfo)memberCache;
                    result = $"Property: {propInfo.Name}";
                    break;
                case MemberTypes.Field:
                    FieldInfo fieldInfo = (FieldInfo)memberCache;
                    result = $"Field: {fieldInfo.Name}";
                    break;
                case MemberTypes.Method:
                    MethodInfo methodInfo = (MethodInfo)memberCache;
                    result = $"Method: {methodInfo.Name}";
                    break;
            }

            // Assert
            Assert.AreEqual("Property: PublicProperty", result);
        }

        [TestMethod]
        public void ExplicitOperator_WithNullMemberCache_ThrowsNullReferenceException()
        {
            // Arrange
            MemberCache memberCache = null;

            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() =>
            {
                PropertyInfo propertyInfo = (PropertyInfo)memberCache;
            });
        }

        [TestMethod]
        public void ExplicitOperator_CanBeUsedInLinqQueries()
        {
            // Arrange
            var type = typeof(TestClassForOperators);
            var memberCache = MemberCache.Create(type);
            var properties = memberCache.Members.Where(m => m.IsProperty);

            // Act
            var propertyInfos = properties.Select(m => (PropertyInfo)m).ToList();

            // Assert
            Assert.IsTrue(propertyInfos.Any());
            Assert.IsTrue(propertyInfos.All(p => p is PropertyInfo));
            Assert.AreEqual("PublicProperty", propertyInfos.First().Name);
        }

        [TestMethod]
        public void IsProperty_And_ExplicitOperator_AreConsistent()
        {
            // Arrange
            var type = typeof(TestClassForOperators);
            var memberCache = MemberCache.Create(type);
            var propertyMembers = memberCache.Members.Where(m => m.IsProperty).ToList();

            // Act & Assert
            foreach (var member in propertyMembers)
            {
                // Если IsProperty == true, то приведение должно работать
                Assert.IsTrue(member.IsProperty);
                PropertyInfo propertyInfo = (PropertyInfo)member;
                Assert.IsNotNull(propertyInfo);
            }

            var nonPropertyMembers = memberCache.Members.Where(m => !m.IsProperty).ToList();
            foreach (var member in nonPropertyMembers)
            {
                // Если IsProperty == false, то приведение должно бросать исключение
                Assert.IsFalse(member.IsProperty);
                Assert.ThrowsException<InvalidCastException>(() =>
                {
                    PropertyInfo propertyInfo = (PropertyInfo)member;
                });
            }
        }

        #endregion
    }
}