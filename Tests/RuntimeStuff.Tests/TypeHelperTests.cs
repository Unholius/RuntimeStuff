using RuntimeStuff.Helpers;

namespace RuntimeStuff.MSTests
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class TypeHelperTests
    {
        #region Test Models

        public class TestPerson
        {
            public string Name { get; set; }
            public int Age { get; set; }
            private string PrivateField = "private";
            protected string ProtectedField = "protected";
            internal string InternalField = "internal";
            public DateTime BirthDate { get; set; }
            public decimal Salary { get; set; }
            public bool IsEmployed { get; set; }
            public TestAddress Address { get; set; }
            public List<string> Hobbies { get; set; }
            public string ReadOnlyProperty => "readonly";
            private string PrivateProperty { get; set; } = "privateProp";
        }

        public class TestAddress
        {
            public string City { get; set; }
            public string Street { get; set; }
        }

        public class TestClassWithStatic
        {
            public static string StaticProperty { get; set; } = "static";
            public static int StaticField = 100;
            public string InstanceProperty { get; set; } = "instance";
        }

        public struct TestStruct
        {
            public TestStruct(int id, string name)
            {
                Id = id;
                Name = name;
            }

            public int Id;
            public string Name { get; set; }
        }

        public interface ITestInterface
        {
            string InterfaceProperty { get; }
        }

        public class TestClassWithInterface : ITestInterface
        {
            public string InterfaceProperty => "interface";
            public string OwnProperty { get; set; } = "own";
        }

        public enum TestEnum
        {
            Value1 = 1,
            Value2 = 2
        }

        #endregion Test Models

        #region ChangeType Tests

        [TestMethod]
        public void ChangeType_StringToInt_ReturnsInt()
        {
            // Arrange
            var value = "123";

            // Act
            var result = TypeHelper.ChangeType<int>(value);

            // Assert
            Assert.AreEqual(123, result);
        }

        [TestMethod]
        public void ChangeType_StringToDecimal_ReturnsDecimal()
        {
            // Arrange
            var value = "123.45";

            // Act
            var result = TypeHelper.ChangeType<decimal>(value);

            // Assert
            Assert.AreEqual(123.45m, result);
        }

        [TestMethod]
        public void ChangeType_StringToDateTime_ReturnsDateTime()
        {
            // Arrange
            var value = "2023-12-31";

            // Act
            var result = TypeHelper.ChangeType<DateTime>(value);

            // Assert
            Assert.AreEqual(new DateTime(2023, 12, 31), result);
        }

        [TestMethod]
        public void ChangeType_StringToNullableDateTime_ReturnsDateTime()
        {
            // Arrange
            var value = "2023-12-31";

            // Act
            var result = TypeHelper.ChangeType<DateTime?>(value);

            // Assert
            Assert.AreEqual(new DateTime(2023, 12, 31), result);
        }

        [TestMethod]
        public void ChangeType_NullToNullable_ReturnsNull()
        {
            // Act
            var result = TypeHelper.ChangeType<int?>(null);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void ChangeType_DBNullToNullable_ReturnsNull()
        {
            // Arrange
            var value = DBNull.Value;

            // Act
            var result = TypeHelper.ChangeType<int?>(value);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void ChangeType_IntToString_ReturnsString()
        {
            // Arrange
            var value = 123;

            // Act
            var result = TypeHelper.ChangeType<string>(value);

            // Assert
            Assert.AreEqual("123", result);
        }

        [TestMethod]
        public void ChangeType_StringToEnum_ReturnsEnum()
        {
            // Arrange
            var value = "Value2";

            // Act
            var result = TypeHelper.ChangeType<TestEnum>(value);

            // Assert
            Assert.AreEqual(TestEnum.Value2, result);
        }

        [TestMethod]
        public void ChangeType_IntToEnum_ReturnsEnum()
        {
            // Arrange
            var value = 2;

            // Act
            var result = TypeHelper.ChangeType<TestEnum>(value);

            // Assert
            Assert.AreEqual(TestEnum.Value2, result);
        }

        [TestMethod]
        [ExpectedException(typeof(FormatException))]
        public void ChangeType_InvalidStringToInt_ThrowsFormatException()
        {
            // Arrange
            var value = "not-a-number";

            // Act
            TypeHelper.ChangeType<int>(value);
        }

        #endregion ChangeType Tests

        #region ComputeHash Tests

        [TestMethod]
        public void ComputeHash_TwoObjects_ReturnsConsistentHash()
        {
            // Arrange
            var obj1 = "test";
            var obj2 = 123;

            // Act
            var hash1 = TypeHelper.ComputeHash(obj1, obj2);
            var hash2 = TypeHelper.ComputeHash(obj1, obj2);

            // Assert
            Assert.AreEqual(hash1, hash2);
        }

        [TestMethod]
        public void ComputeHash_WithNull_ReturnsHash()
        {
            // Arrange
            string obj1 = null;
            var obj2 = 123;

            // Act
            var hash = TypeHelper.ComputeHash(obj1, obj2);

            // Assert
            Assert.IsTrue(hash != 0);
        }

        #endregion ComputeHash Tests

        #region Default Tests

        [TestMethod]
        public void Default_ValueType_ReturnsDefaultValue()
        {
            // Act
            var result = TypeHelper.Default(typeof(int));

            // Assert
            Assert.AreEqual(0, result);
        }

        [TestMethod]
        public void Default_ReferenceType_ReturnsNull()
        {
            // Act
            var result = TypeHelper.Default(typeof(string));

            // Assert
            Assert.IsNull(result);
        }

        #endregion Default Tests

        #region FindMember Tests

        [TestMethod]
        public void FindMember_FindsProperty()
        {
            // Arrange
            var type = typeof(TestPerson);

            // Act
            var member = TypeHelper.FindMember(type, "Name");

            // Assert
            Assert.IsNotNull(member);
            Assert.AreEqual("Name", member.Name);
            Assert.IsTrue(member is PropertyInfo);
        }

        [TestMethod]
        public void FindMember_FindsField()
        {
            // Arrange
            var type = typeof(TestPerson);

            // Act
            var member = TypeHelper.FindMember(type, "PrivateField");

            // Assert
            Assert.IsNotNull(member);
            Assert.AreEqual("PrivateField", member.Name);
            Assert.IsTrue(member is FieldInfo);
        }

        [TestMethod]
        public void FindMember_IgnoreCase_ReturnsMember()
        {
            // Arrange
            var type = typeof(TestPerson);

            // Act
            var member = TypeHelper.FindMember(type, "name");

            // Assert
            Assert.IsNotNull(member);
            Assert.AreEqual("Name", member.Name);
        }

        [TestMethod]
        public void FindMember_FromInterface_ReturnsProperty()
        {
            // Arrange
            var type = typeof(TestClassWithInterface);

            // Act
            var member = TypeHelper.FindMember(type, "InterfaceProperty");

            // Assert
            Assert.IsNotNull(member);
            Assert.AreEqual("InterfaceProperty", member.Name);
        }

        #endregion FindMember Tests

        #region GetProperties Tests

        [TestMethod]
        public void GetProperties_ReturnsAllProperties()
        {
            // Act
            var properties = TypeHelper.GetProperties<TestPerson>();

            // Assert
            Assert.IsTrue(properties.Any(p => p.Name == "Name"));
            Assert.IsTrue(properties.Any(p => p.Name == "Age"));
            Assert.IsTrue(properties.Any(p => p.Name == "BirthDate"));
            Assert.IsTrue(properties.Any(p => p.Name == "Salary"));
            Assert.IsTrue(properties.Any(p => p.Name == "IsEmployed"));
            Assert.IsTrue(properties.Any(p => p.Name == "Address"));
            Assert.IsTrue(properties.Any(p => p.Name == "Hobbies"));
            Assert.IsTrue(properties.Any(p => p.Name == "ReadOnlyProperty"));
        }

        [TestMethod]
        public void GetPropertiesMap_ReturnsDictionary()
        {
            // Act
            var map = TypeHelper.GetPropertiesMap<TestPerson>();

            // Assert
            Assert.IsInstanceOfType(map, typeof(Dictionary<string, PropertyInfo>));
            Assert.IsTrue(map.ContainsKey("Name"));
            Assert.IsTrue(map.ContainsKey("Age"));
        }

        [TestMethod]
        public void GetProperty_ByName_ReturnsProperty()
        {
            // Act
            var property = TypeHelper.GetProperty(typeof(TestPerson), "Name");

            // Assert
            Assert.IsNotNull(property);
            Assert.AreEqual("Name", property.Name);
        }

        [TestMethod]
        public void GetProperty_IgnoreCase_ReturnsProperty()
        {
            // Act
            var property = TypeHelper.GetProperty(typeof(TestPerson), "NAME", StringComparison.OrdinalIgnoreCase);

            // Assert
            Assert.IsNotNull(property);
            Assert.AreEqual("Name", property.Name);
        }

        #endregion GetProperties Tests

        #region GetMemberGetter Tests

        //[TestMethod]
        //public void GetMemberGetter_ForProperty_ReturnsDelegate()
        //{
        //    // Arrange
        //    var person = new TestPerson { Name = "John", Age = 30 };

        //    // Act
        //    var getter = TypeHelper.GetMemberGetter<TestPerson, string>("Name");
        //    var result = getter(person);

        //    // Assert
        //    Assert.AreEqual("John", result);
        //}

        //[TestMethod]
        //public void GetMemberGetter_ForField_ReturnsDelegate()
        //{
        //    // Arrange
        //    var person = new TestPerson();

        //    // Act
        //    var getter = TypeHelper.GetMemberGetter<TestPerson, string>("PrivateField");
        //    var result = getter(person);

        //    // Assert
        //    Assert.AreEqual("private", result);
        //}

        //[TestMethod]
        //public void GetMemberGetter_NonGeneric_ReturnsDelegate()
        //{
        //    // Arrange
        //    var person = new TestPerson { Name = "John" };

        //    // Act
        //    var getter = TypeHelper.GetMemberGetter<TestPerson>("Name");
        //    var result = getter(person);

        //    // Assert
        //    Assert.AreEqual("John", result);
        //}

        //[TestMethod]
        //public void GetMemberGetter_Universal_ReturnsDelegate()
        //{
        //    // Arrange
        //    var person = new TestPerson { Name = "John" };

        //    // Act
        //    var getter = TypeHelper.GetMemberGetter("Name", typeof(TestPerson));
        //    var result = getter(person);

        //    // Assert
        //    Assert.AreEqual("John", result);
        //}

        //#endregion GetMemberGetter Tests

        //#region GetMemberSetter Tests

        //[TestMethod]
        //public void GetMemberSetter_ForProperty_ReturnsDelegate()
        //{
        //    // Arrange
        //    var person = new TestPerson();
        //    var setter = TypeHelper.GetMemberSetter<TestPerson, string>("Name");

        //    // Act
        //    setter(person, "John");
        //    var getter = TypeHelper.GetMemberGetter<TestPerson, string>("Name");
        //    var result = getter(person);

        //    // Assert
        //    Assert.AreEqual("John", result);
        //}

        //[TestMethod]
        //public void GetMemberSetter_ForField_ReturnsDelegate()
        //{
        //    // Arrange
        //    var person = new TestPerson();
        //    var setter = TypeHelper.GetMemberSetter<TestPerson, string>("PrivateField");

        //    // Act
        //    setter(person, "newValue");
        //    var getter = TypeHelper.GetMemberGetter<TestPerson, string>("PrivateField");
        //    var result = getter(person);

        //    // Assert
        //    Assert.AreEqual("newValue", result);
        //}

        //[TestMethod]
        //public void GetMemberSetter_NonGeneric_ReturnsDelegate()
        //{
        //    // Arrange
        //    var person = new TestPerson();
        //    var setter = TypeHelper.GetMemberSetter<TestPerson>("Name");

        //    // Act
        //    setter(person, "John");
        //    var getter = TypeHelper.GetMemberGetter<TestPerson>("Name");
        //    var result = getter(person);

        //    // Assert
        //    Assert.AreEqual("John", result);
        //}

        //[TestMethod]
        //public void GetMemberSetter_Universal_ReturnsDelegate()
        //{
        //    // Arrange
        //    var person = new TestPerson();
        //    var setter = TypeHelper.GetMemberSetter("Name", typeof(TestPerson));

        //    // Act
        //    setter(person, "John");
        //    var getter = TypeHelper.GetMemberGetter("Name", typeof(TestPerson));
        //    var result = getter(person);

        //    // Assert
        //    Assert.AreEqual("John", result);
        //}

        //[TestMethod]
        //public void GetMemberSetter_WithExpression_ReturnsDelegate()
        //{
        //    // Arrange
        //    var person = new TestPerson();

        //    // Act
        //    var setter = TypeHelper.GetMemberSetter<TestPerson, string>(x => x.Name, out var propertyName);
        //    setter(person, "John");

        //    // Assert
        //    Assert.AreEqual("Name", propertyName);
        //    Assert.AreEqual("John", person.Name);
        //}

        #endregion GetMemberSetter Tests

        #region GetMemberValue Tests

        [TestMethod]
        public void GetMemberValue_ReturnsPropertyValue()
        {
            // Arrange
            var person = new TestPerson { Name = "John" };

            // Act
            var value = TypeHelper.GetMemberValue(person, "Name");

            // Assert
            Assert.AreEqual("John", value);
        }

        [TestMethod]
        public void GetMemberValue_Generic_ReturnsTypedValue()
        {
            // Arrange
            var person = new TestPerson { Age = 30 };

            // Act
            var value = person.GetMemberValue<int>("Age");

            // Assert
            Assert.AreEqual(30, value);
        }

        [TestMethod]
        public void GetMemberValue_WithConversion_ReturnsConvertedValue()
        {
            // Arrange
            var person = new TestPerson { Age = 30 };

            // Act
            var value = TypeHelper.GetMemberValue(person, "Age", typeof(string));

            // Assert
            Assert.AreEqual("30", value);
        }

        #endregion GetMemberValue Tests

        #region SetMemberValue Tests

        //[TestMethod]
        //public void SetMemberValue_SetsProperty()
        //{
        //    // Arrange
        //    var person = new TestPerson();

        //    // Act
        //    var result = TypeHelper.SetMemberValue(person, "Name", "John");

        //    // Assert
        //    Assert.IsTrue(result);
        //    Assert.AreEqual("John", person.Name);
        //}

        //[TestMethod]
        //public void SetMemberValue_ExtensionMethod_SetsProperty()
        //{
        //    // Arrange
        //    var person = new TestPerson();

        //    // Act
        //    var result = person.SetMemberValue("Name", "John");

        //    // Assert
        //    Assert.IsTrue(result);
        //    Assert.AreEqual("John", person.Name);
        //}

        //[TestMethod]
        //public void SetMemberValue_InvalidProperty_ReturnsFalse()
        //{
        //    // Arrange
        //    var person = new TestPerson();

        //    // Act
        //    var result = TypeHelper.SetMemberValue(person, "NonExistent", "value");

        //    // Assert
        //    Assert.IsFalse(result);
        //}

        #endregion SetMemberValue Tests

        #region GetPropertyValues Tests

        [TestMethod]
        public void GetPropertyValues_ReturnsValuesArray()
        {
            // Arrange
            var person = new TestPerson { Name = "John", Age = 30 };

            // Act
            var values = TypeHelper.GetPropertyValues(person, "Name", "Age");

            // Assert
            Assert.AreEqual(2, values.Length);
            Assert.AreEqual("John", values[0]);
            Assert.AreEqual(30, values[1]);
        }

        [TestMethod]
        public void GetPropertyValues_Generic_ReturnsTypedValues()
        {
            // Arrange
            var person = new TestPerson { Name = "John", Age = 30 };

            // Act
            var values = TypeHelper.GetPropertyValues<TestPerson, string>(person, "Name", "Age");

            // Assert
            Assert.AreEqual(2, values.Length);
            Assert.AreEqual("John", values[0]);
            Assert.AreEqual("30", values[1]);
        }

        #endregion GetPropertyValues Tests

        #region Type Detection Tests

        [TestMethod]
        public void IsBasic_String_ReturnsTrue()
        {
            // Act & Assert
            Assert.IsTrue(TypeHelper.IsBasic(typeof(string)));
        }

        [TestMethod]
        public void IsBasic_Int_ReturnsTrue()
        {
            // Act & Assert
            Assert.IsTrue(TypeHelper.IsBasic(typeof(int)));
        }

        [TestMethod]
        public void IsBasic_DateTime_ReturnsTrue()
        {
            // Act & Assert
            Assert.IsTrue(TypeHelper.IsBasic(typeof(DateTime)));
        }

        [TestMethod]
        public void IsBasic_Class_ReturnsFalse()
        {
            // Act & Assert
            Assert.IsFalse(TypeHelper.IsBasic(typeof(TestPerson)));
        }

        [TestMethod]
        public void IsBoolean_Bool_ReturnsTrue()
        {
            // Act & Assert
            Assert.IsTrue(TypeHelper.IsBoolean(typeof(bool)));
        }

        [TestMethod]
        public void IsBoolean_NullableBool_ReturnsTrue()
        {
            // Act & Assert
            Assert.IsTrue(TypeHelper.IsBoolean(typeof(bool?)));
        }

        [TestMethod]
        public void IsDate_DateTime_ReturnsTrue()
        {
            // Act & Assert
            Assert.IsTrue(TypeHelper.IsDate(typeof(DateTime)));
        }

        [TestMethod]
        public void IsCollection_List_ReturnsTrue()
        {
            // Act & Assert
            Assert.IsTrue(TypeHelper.IsCollection(typeof(List<string>)));
        }

        [TestMethod]
        public void IsCollection_Array_ReturnsTrue()
        {
            // Act & Assert
            Assert.IsTrue(TypeHelper.IsCollection(typeof(string[])));
        }

        [TestMethod]
        public void IsCollection_String_ReturnsFalse()
        {
            // Act & Assert
            Assert.IsFalse(TypeHelper.IsCollection(typeof(string)));
        }

        [TestMethod]
        public void IsDelegate_Action_ReturnsTrue()
        {
            // Act & Assert
            Assert.IsTrue(TypeHelper.IsDelegate(typeof(Action)));
        }

        [TestMethod]
        public void IsDictionary_Dictionary_ReturnsTrue()
        {
            // Act & Assert
            Assert.IsTrue(TypeHelper.IsDictionary(typeof(Dictionary<string, int>)));
        }

        [TestMethod]
        public void IsNullable_NullableInt_ReturnsTrue()
        {
            // Act & Assert
            Assert.IsTrue(TypeHelper.IsNullable(typeof(int?)));
        }

        [TestMethod]
        public void IsNullable_String_ReturnsTrue()
        {
            // Act & Assert
            Assert.IsTrue(TypeHelper.IsNullable(typeof(string)));
        }

        [TestMethod]
        public void IsNullable_Int_ReturnsFalse()
        {
            // Act & Assert
            Assert.IsFalse(TypeHelper.IsNullable(typeof(int)));
        }

        [TestMethod]
        public void IsNumeric_Int_ReturnsTrue()
        {
            // Act & Assert
            Assert.IsTrue(TypeHelper.IsNumeric(typeof(int)));
        }

        [TestMethod]
        public void IsNumeric_Decimal_ReturnsTrue()
        {
            // Act & Assert
            Assert.IsTrue(TypeHelper.IsNumeric(typeof(decimal)));
        }

        [TestMethod]
        public void IsNumeric_String_ReturnsFalse()
        {
            // Act & Assert
            Assert.IsFalse(TypeHelper.IsNumeric(typeof(string)));
        }

        [TestMethod]
        public void IsTuple_ValueTuple_ReturnsTrue()
        {
            // Act & Assert
            Assert.IsTrue(TypeHelper.IsTuple(typeof((string, int))));
        }

        #endregion Type Detection Tests

        #region GetTypeByName Tests

        [TestMethod]
        public void GetTypeByName_KnownType_ReturnsType()
        {
            // Act
            var type = TypeHelper.GetTypeByName("System.String");

            // Assert
            Assert.AreEqual(typeof(string), type);
        }

        [TestMethod]
        public void GetTypeByName_ShortName_ReturnsType()
        {
            // Act
            var type = TypeHelper.GetTypeByName("String");

            // Assert
            Assert.AreEqual(typeof(string), type);
        }

        [TestMethod]
        public void GetTypeByName_Interface_ReturnsType()
        {
            // Act
            var type = TypeHelper.GetTypeByName("System.Collections.IEnumerable");

            // Assert
            Assert.AreEqual(typeof(IEnumerable), type);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void GetTypeByName_Null_ThrowsException()
        {
            // Act
            TypeHelper.GetTypeByName(null);
        }

        #endregion GetTypeByName Tests

        #region New Tests

        [TestMethod]
        public void New_ReferenceType_CreatesInstance()
        {
            // Act
            var instance = TypeHelper.New<TestPerson>();

            // Assert
            Assert.IsNotNull(instance);
            Assert.IsInstanceOfType(instance, typeof(TestPerson));
        }

        [TestMethod]
        public void New_ValueType_CreatesInstance()
        {
            // Act
            var instance = TypeHelper.New<TestStruct>(1, "two");

            // Assert
            Assert.IsNotNull(instance);
            Assert.IsInstanceOfType(instance, typeof(TestStruct));
        }

        [TestMethod]
        public void New_WithType_CreatesInstance()
        {
            // Act
            var instance = TypeHelper.New(typeof(TestPerson));

            // Assert
            Assert.IsNotNull(instance);
            Assert.IsInstanceOfType(instance, typeof(TestPerson));
        }

        #endregion New Tests

        #region GetOrAdd Tests

        [TestMethod]
        public void GetOrAdd_ExistingKey_ReturnsValue()
        {
            // Arrange
            var dict = new Dictionary<string, int> { { "key", 42 } };

            // Act
            var value = dict.GetOrAdd("key", () => 100);

            // Assert
            Assert.AreEqual(42, value);
            Assert.AreEqual(1, dict.Count);
        }

        [TestMethod]
        public void GetOrAdd_NewKey_AddsAndReturnsValue()
        {
            // Arrange
            var dict = new Dictionary<string, int>();

            // Act
            var value = dict.GetOrAdd("key", () => 42);

            // Assert
            Assert.AreEqual(42, value);
            Assert.AreEqual(1, dict.Count);
            Assert.AreEqual(42, dict["key"]);
        }

        #endregion GetOrAdd Tests

        #region GetValueOrDefault Tests

        [TestMethod]
        public void GetValueOrDefault_ExistingKey_ReturnsValue()
        {
            // Arrange
            var dict = new Dictionary<string, int> { { "key", 42 } };

            // Act
            var value = dict.GetValueOrDefault("key");

            // Assert
            Assert.AreEqual(42, value);
        }

        [TestMethod]
        public void GetValueOrDefault_NonExistingKey_ReturnsDefault()
        {
            // Arrange
            var dict = new Dictionary<string, int> { { "key", 42 } };

            // Act
            var value = dict.GetValueOrDefault("nonexistent");

            // Assert
            Assert.AreEqual(0, value);
        }

        #endregion GetValueOrDefault Tests

        #region GetImplementationsOf Tests

        [TestMethod]
        public void GetImplementationsOf_InCurrentAssembly_ReturnsTypes()
        {
            // Act
            var types = TypeHelper.GetImplementationsOf(typeof(ITestInterface), Assembly.GetExecutingAssembly());

            // Assert
            Assert.IsTrue(types.Any(t => t == typeof(TestClassWithInterface)));
        }

        #endregion GetImplementationsOf Tests

        #region GetMembersOfType Tests

        [TestMethod]
        public void GetMembersOfType_FindsStrings()
        {
            // Arrange
            var person = new TestPerson
            {
                Name = "John",
                Address = new TestAddress { City = "New York", Street = "5th Ave" }
            };

            // Act
            var strings = person.GetMembersOfType<TestPerson, string>().ToList();

            // Assert
            Assert.IsTrue(strings.Count >= 3); // Name + City + Street + private fields
            Assert.IsTrue(strings.Contains("John"));
        }

        #endregion GetMembersOfType Tests

        #region Cache Tests

        //[TestMethod]
        //public void UpdateCache_PrecompilesDelegates()
        //{
        //    // Act
        //    TypeHelper.UpdateCache<TestPerson>();

        //    // Assert - нет исключения
        //    Assert.IsTrue(true);
        //}

        #endregion Cache Tests

        #region Static Member Tests

        //[TestMethod]
        //public void GetMemberSetter_StaticProperty_SetsValue()
        //{
        //    // Arrange
        //    var setter = TypeHelper.GetMemberSetter<TestClassWithStatic, string>("StaticProperty");

        //    // Act
        //    setter(default, "newValue");

        //    // Assert
        //    Assert.AreEqual("newValue", TestClassWithStatic.StaticProperty);
        //}

        #endregion Static Member Tests

        #region GetBaseTypes Tests

        [TestMethod]
        public void GetBaseTypes_WithIncludeThis_ReturnsType()
        {
            // Act
            var baseTypes = TypeHelper.GetBaseTypes(typeof(TestPerson), includeThis: true);

            // Assert
            Assert.IsTrue(baseTypes.Contains(typeof(TestPerson)));
        }

        [TestMethod]
        public void GetBaseTypes_WithInterfaces_ReturnsInterfaces()
        {
            // Act
            var baseTypes = TypeHelper.GetBaseTypes(typeof(TestClassWithInterface), getInterfaces: true);

            // Assert
            Assert.IsTrue(baseTypes.Any(t => t.Name.Contains("ITestInterface")));
        }

        #endregion GetBaseTypes Tests

        #region GetLowestMember Tests

        [TestMethod]
        public void GetLowestProperty_FindsProperty()
        {
            // Act
            var property = TypeHelper.GetLowestProperty(typeof(TestPerson), "Name");

            // Assert
            Assert.IsNotNull(property);
            Assert.AreEqual("Name", property.Name);
        }

        [TestMethod]
        public void GetLowestField_FindsField()
        {
            // Act
            var field = TypeHelper.GetLowestField(typeof(TestPerson), "PrivateField");

            // Assert
            Assert.IsNotNull(field);
            Assert.AreEqual("PrivateField", field.Name);
        }

        #endregion GetLowestMember Tests

        #region GetCustomAttribute Tests

        public class TestAttribute : Attribute
        { }

        [TestAttribute]
        public class ClassWithAttribute
        { }

        [TestMethod]
        public void GetCustomAttribute_FindsAttribute()
        {
            // Arrange
            var type = typeof(ClassWithAttribute);

            // Act
            var attribute = TypeHelper.GetCustomAttribute(type, "TestAttribute");

            // Assert
            Assert.IsNotNull(attribute);
            Assert.IsInstanceOfType(attribute, typeof(TestAttribute));
        }

        #endregion GetCustomAttribute Tests
    }
}