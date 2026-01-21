using RuntimeStuff.Extensions;
using RuntimeStuff.Helpers;
using RuntimeStuff.MSTests.Models;

namespace RuntimeStuff.MSTests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Reflection;

    [TestClass]
    public class ObjTests
    {
        #region Test Models

        public class TestPerson
        {
            public string? Name { get; set; }
            public int Age { get; set; }
            private string PrivateField = "private";
            protected string ProtectedField = "protected";
            internal string InternalField = "internal";
            public DateTime BirthDate { get; set; }
            public decimal Salary { get; set; }
            public bool IsEmployed { get; set; }
            public TestAddress? Address { get; set; }
            public List<string>? Hobbies { get; set; }
            public string ReadOnlyProperty => "readonly";
            private string PrivateProperty { get; set; } = "privateProp";
        }

        public class TestAddress
        {
            public string? City { get; set; }
            public string? Street { get; set; }
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
            var result = Obj.ChangeType<int>(value);

            // Assert
            Assert.AreEqual(123, result);
        }

        [TestMethod]
        public void ChangeType_StringToDecimal_ReturnsDecimal()
        {
            // Arrange
            var value = "123.45";

            // Act
            var result = Obj.ChangeType<decimal>(value);

            // Assert
            Assert.AreEqual(123.45m, result);
        }

        [TestMethod]
        public void ChangeType_StringToDateTime_ReturnsDateTime()
        {
            // Arrange
            var value = "2023-12-31";

            // Act
            var result = Obj.ChangeType<DateTime>(value);

            // Assert
            Assert.AreEqual(new DateTime(2023, 12, 31), result);
        }

        [TestMethod]
        public void ChangeType_StringToNullableDateTime_ReturnsDateTime()
        {
            // Arrange
            var value = "2023-12-31";

            // Act
            var result = Obj.ChangeType<DateTime?>(value);

            // Assert
            Assert.AreEqual(new DateTime(2023, 12, 31), result);
        }

        [TestMethod]
        public void ChangeType_NullToNullable_ReturnsNull()
        {
            // Act
            var result = Obj.ChangeType<int?>(null);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void ChangeType_DBNullToNullable_ReturnsNull()
        {
            // Arrange
            var value = DBNull.Value;

            // Act
            var result = Obj.ChangeType<int?>(value);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void ChangeType_IntToString_ReturnsString()
        {
            // Arrange
            var value = 123;

            // Act
            var result = Obj.ChangeType<string>(value);

            // Assert
            Assert.AreEqual("123", result);
        }

        [TestMethod]
        public void ChangeType_StringToEnum_ReturnsEnum()
        {
            // Arrange
            var value = "Value2";

            // Act
            var result = Obj.ChangeType<TestEnum>(value);

            // Assert
            Assert.AreEqual(TestEnum.Value2, result);
        }

        [TestMethod]
        public void ChangeType_IntToEnum_ReturnsEnum()
        {
            // Arrange
            var value = 2;

            // Act
            var result = Obj.ChangeType<TestEnum>(value);

            // Assert
            Assert.AreEqual(TestEnum.Value2, result);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidCastException))]
        public void ChangeType_InvalidStringToInt_ThrowsFormatException()
        {
            // Arrange
            var value = "not-a-number";

            // Act
            Obj.ChangeType<int>(value);
        }

        #endregion ChangeType Tests

        #region Default Tests

        [TestMethod]
        public void Default_ValueType_ReturnsDefaultValue()
        {
            // Act
            var result = Obj.Default(typeof(int));

            // Assert
            Assert.AreEqual(0, result);
        }

        [TestMethod]
        public void Default_ReferenceType_ReturnsNull()
        {
            // Act
            var result = Obj.Default(typeof(string));

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
            var member = Obj.FindMember(type, "Name");

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
            var member = Obj.FindMember(type, "PrivateField");

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
            var member = Obj.FindMember(type, "name");

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
            var member = Obj.FindMember(type, "InterfaceProperty");

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
            var properties = Obj.GetProperties<TestPerson>();

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
            var map = Obj.GetPropertiesMap<TestPerson>();

            // Assert
            Assert.IsInstanceOfType(map, typeof(Dictionary<string, PropertyInfo>));
            Assert.IsTrue(map.ContainsKey("Name"));
            Assert.IsTrue(map.ContainsKey("Age"));
        }

        [TestMethod]
        public void GetProperty_ByName_ReturnsProperty()
        {
            // Act
            var property = Obj.GetProperty(typeof(TestPerson), "Name");

            // Assert
            Assert.IsNotNull(property);
            Assert.AreEqual("Name", property.Name);
        }

        [TestMethod]
        public void GetProperty_IgnoreCase_ReturnsProperty()
        {
            // Act
            var property = Obj.GetProperty(typeof(TestPerson), "NAME", StringComparison.OrdinalIgnoreCase);

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

        #endregion GetMemberGetter Tests

        #region GetMemberValue Tests

        [TestMethod]
        public void GetMemberValue_ReturnsPropertyValue()
        {
            // Arrange
            var person = new TestPerson { Name = "John" };

            // Act
            var value = Obj.Get(person, "Name");

            // Assert
            Assert.AreEqual("John", value);
        }

        [TestMethod]
        public void GetMemberValue_Generic_ReturnsTypedValue()
        {
            // Arrange
            var person = new TestPerson { Age = 30 };

            // Act
            var value = Obj.Get<int>(person, "Age");

            // Assert
            Assert.AreEqual(30, value);
        }

        [TestMethod]
        public void GetMemberValue_WithConversion_ReturnsConvertedValue()
        {
            // Arrange
            var person = new TestPerson { Age = 30 };

            // Act
            var value = Obj.Get(person, "Age", typeof(string));

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
            var values = Obj.GetValues(person, "Name", "Age");

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
            var values = Obj.GetValues<TestPerson, string>(person, "Name", "Age");

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
            Assert.IsTrue(Obj.IsBasic(typeof(string)));
        }

        [TestMethod]
        public void IsBasic_Int_ReturnsTrue()
        {
            // Act & Assert
            Assert.IsTrue(Obj.IsBasic(typeof(int)));
        }

        [TestMethod]
        public void IsBasic_DateTime_ReturnsTrue()
        {
            // Act & Assert
            Assert.IsTrue(Obj.IsBasic(typeof(DateTime)));
        }

        [TestMethod]
        public void IsBasic_Class_ReturnsFalse()
        {
            // Act & Assert
            Assert.IsFalse(Obj.IsBasic(typeof(TestPerson)));
        }

        [TestMethod]
        public void IsBoolean_Bool_ReturnsTrue()
        {
            // Act & Assert
            Assert.IsTrue(Obj.IsBoolean(typeof(bool)));
        }

        [TestMethod]
        public void IsBoolean_NullableBool_ReturnsTrue()
        {
            // Act & Assert
            Assert.IsTrue(Obj.IsBoolean(typeof(bool?)));
        }

        [TestMethod]
        public void IsDate_DateTime_ReturnsTrue()
        {
            // Act & Assert
            Assert.IsTrue(Obj.IsDate(typeof(DateTime)));
        }

        [TestMethod]
        public void IsCollection_List_ReturnsTrue()
        {
            // Act & Assert
            Assert.IsTrue(Obj.IsGenericCollection(typeof(List<string>)));
        }

        [TestMethod]
        public void IsCollection_Array_ReturnsTrue()
        {
            // Act & Assert
            Assert.IsTrue(Obj.IsCollection(typeof(string[])));
        }

        [TestMethod]
        public void IsCollection_String_ReturnsFalse()
        {
            // Act & Assert
            Assert.IsFalse(Obj.IsGenericCollection(typeof(string)));
        }

        [TestMethod]
        public void IsDelegate_Action_ReturnsTrue()
        {
            // Act & Assert
            Assert.IsTrue(Obj.IsDelegate(typeof(Action)));
        }

        [TestMethod]
        public void IsDictionary_Dictionary_ReturnsTrue()
        {
            // Act & Assert
            Assert.IsTrue(Obj.IsDictionary(typeof(Dictionary<string, int>)));
        }

        [TestMethod]
        public void IsNullable_NullableInt_ReturnsTrue()
        {
            // Act & Assert
            Assert.IsTrue(Obj.IsNullable(typeof(int?)));
        }

        [TestMethod]
        public void IsNullable_String_ReturnsTrue()
        {
            // Act & Assert
            Assert.IsTrue(Obj.IsNullable(typeof(string)));
        }

        [TestMethod]
        public void IsNullable_Int_ReturnsFalse()
        {
            // Act & Assert
            Assert.IsFalse(Obj.IsNullable(typeof(int)));
        }

        [TestMethod]
        public void IsNumeric_Int_ReturnsTrue()
        {
            // Act & Assert
            Assert.IsTrue(Obj.IsNumeric(typeof(int)));
        }

        [TestMethod]
        public void IsNumeric_Decimal_ReturnsTrue()
        {
            // Act & Assert
            Assert.IsTrue(Obj.IsNumeric(typeof(decimal)));
        }

        [TestMethod]
        public void IsNumeric_String_ReturnsFalse()
        {
            // Act & Assert
            Assert.IsFalse(Obj.IsNumeric(typeof(string)));
        }

        [TestMethod]
        public void IsTuple_ValueTuple_ReturnsTrue()
        {
            // Act & Assert
            Assert.IsTrue(Obj.IsTuple(typeof((string, int))));
        }

        #endregion Type Detection Tests

        #region GetTypeByName Tests

        [TestMethod]
        public void GetTypeByName_KnownType_ReturnsType()
        {
            // Act
            var type = Obj.GetTypeByName("System.String");

            // Assert
            Assert.AreEqual(typeof(string), type);
        }

        [TestMethod]
        public void GetTypeByName_ShortName_ReturnsType()
        {
            // Act
            var type = Obj.GetTypeByName("String");

            // Assert
            Assert.AreEqual(typeof(string), type);
        }

        [TestMethod]
        public void GetTypeByName_Interface_ReturnsType()
        {
            // Act
            var type = Obj.GetTypeByName("System.Collections.IEnumerable");

            // Assert
            Assert.AreEqual(typeof(IEnumerable), type);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void GetTypeByName_Null_ThrowsException()
        {
            // Act
            Obj.GetTypeByName(null);
        }

        #endregion GetTypeByName Tests

        #region New Tests

        [TestMethod]
        public void New_ReferenceType_CreatesInstance()
        {
            // Act
            var instance = Obj.New<TestPerson>();

            // Assert
            Assert.IsNotNull(instance);
            Assert.IsInstanceOfType(instance, typeof(TestPerson));
        }

        [TestMethod]
        public void New_ValueType_CreatesInstance()
        {
            // Act
            var instance = Obj.New<TestStruct>(1, "two");

            // Assert
            Assert.IsNotNull(instance);
            Assert.IsInstanceOfType(instance, typeof(TestStruct));
        }

        [TestMethod]
        public void New_WithType_CreatesInstance()
        {
            // Act
            var instance = Obj.New(typeof(TestPerson));

            // Assert
            Assert.IsNotNull(instance);
            Assert.IsInstanceOfType(instance, typeof(TestPerson));
        }

        [TestMethod]
        public void New_WithEnumerableType_CreatesInstance()
        {
            // Act
            var instance = Obj.New(typeof(IEnumerable<TestPerson>));

            // Assert
            Assert.IsNotNull(instance);
            Assert.IsInstanceOfType(instance, typeof(List<TestPerson>));
        }

        [TestMethod]
        public void New_KeyValuePair_CreatesInstance()
        {
            // Act
            var instance = Obj.New<KeyValuePair<string, object>>("1", "2");

            // Assert
            Assert.IsNotNull(instance);
            Assert.AreEqual("1", instance.Key);
            Assert.AreEqual("2", instance.Value);
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
            var types = Obj.GetImplementationsOf(typeof(ITestInterface), Assembly.GetExecutingAssembly());

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
            var strings = person.GetMembersOfType<string>().ToList();

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
            var baseTypes = Obj.GetBaseTypes(typeof(TestPerson), includeThis: true);

            // Assert
            Assert.IsTrue(baseTypes.Contains(typeof(TestPerson)));
        }

        [TestMethod]
        public void GetBaseTypes_WithInterfaces_ReturnsInterfaces()
        {
            // Act
            var baseTypes = Obj.GetBaseTypes(typeof(TestClassWithInterface), getInterfaces: true);

            // Assert
            Assert.IsTrue(baseTypes.Any(t => t.Name.Contains("ITestInterface")));
        }

        #endregion GetBaseTypes Tests

        #region GetLowestMember Tests

        [TestMethod]
        public void GetLowestProperty_FindsProperty()
        {
            // Act
            var property = Obj.GetLowestProperty(typeof(TestPerson), "Name");

            // Assert
            Assert.IsNotNull(property);
            Assert.AreEqual("Name", property.Name);
        }

        [TestMethod]
        public void GetLowestField_FindsField()
        {
            // Act
            var field = Obj.GetLowestField(typeof(TestPerson), "PrivateField");

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
            var attribute = Obj.GetCustomAttribute(type, "TestAttribute");

            // Assert
            Assert.IsNotNull(attribute);
            Assert.IsInstanceOfType(attribute, typeof(TestAttribute));
        }

        #endregion GetCustomAttribute Tests

        #region Get-Set

        public class RecursiveClass
        {
            public string? Name { get; set; }
            public RecursiveClass? Child { get; set; }
        }

        [TestMethod]
        public void Test_Set_01()
        {
            var rc = new RecursiveClass();
            Obj.Set(rc, ["Child", "Child", "Child", "Name"], "ChildName");
            Assert.AreEqual("ChildName", rc.Child?.Child?.Child?.Name);
            Assert.AreEqual("ChildName", Obj.Get<string>(rc, ["Child", "Child", "Child", "Name"]));
        }

        [TestMethod]
        public void Test_Set_02()
        {
            var rc = new RecursiveClass();
            Obj.AddCustomTypeConverter<int, string>(x => x.ToString() + "_custom");
            Obj.Set(rc, "Name", 4);
            Assert.AreEqual("4_custom", rc.Name);
        }

        [TestMethod]
        public void Test_Dic_01()
        {
            var d = new Dictionary<string, object>(StringComparison.OrdinalIgnoreCase.ToStringComparer());
            d["NAME"] = 0;
            d["name"] = 1;
            d["Name"] = 2;

            var v = d["name"];
        }

        [TestMethod]
        public void TryChangeType_Test_01()
        {
            var ok = Obj.TryChangeType<long>(null, out var n);
            Assert.IsFalse(ok);
        }

        #endregion Get-Set

        [TestMethod]
        public void GetStringCache()
        {
            var mc = MemberCache.Create(typeof(string));
            var p = new SqlParameter();
            Obj.Set(p, "SqlDbType", SqlDbType.Structured);
            var dt = new DataTable("dbo.StrList");
            p.Value = dt;
            Obj.Set(p, "SqlDbType", SqlDbType.Structured);
            Obj.Set(p, "TypeName", ((DataTable)dt).TableName);
            Obj.Set(p, "SqlValue", dt);
        }

        [TestMethod]
        public void FromCsv_Test_01()
        {
            var csv = @"Name,Age,City
John,30,New York
Jane,25,Los Angeles
Bob,40,Chicago
";
            var result = CsvHelper.FromCsv<TestCsv>(csv, true);
            Assert.AreEqual(3, result.Length);
        }

        [TestMethod]
        public void FromCsv_Test_01_1()
        {
            var csv = File.ReadAllText(".\\Databases\\BadCodeGoodCodeUpdateData.csv");
            var list = new List<BadCodeGoodCodeUpdateData>();
            list.FromCsv(csv, x => x.BadCode, x => x.GoodCode);
            Assert.AreEqual(2, list.Count);
        }

        [TestMethod]
        public void FromCsv_Test_02()
        {
            var csv = @"


""John"",30,""New York"";

Jane,25,Los Angeles;
Bob,40,Chicago;
";
            var result = CsvHelper.FromCsv<TestCsv>(csv, ["Name", "Age", "City"], false);
        }

        [TestMethod]
        public void FromCsv_Test_04()
        {
            var csv = @"


""John"",30,""New York"";

Jane,25,Los Angeles;
Bob,40,Chicago;
";
            var list = new List<TestCsv>();
            CsvHelper.FromCsv<TestCsv>(csv);
        }

        [TestMethod]
        public void FromCsv_Test_03()
        {
            var csv = @"R2093-AN595SM;144
";
            var result = CsvHelper.FromCsv<ImportFileData>(csv, ["Key", "Value"], false, new [] {";"});
        }

        public class ImportFileData : INotifyPropertyChanged
        {
            string key;
            public string Key
            {
                get => key;
                set
                {
                    if (key == value)
                    {
                        return;
                    }

                    key = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Key)));
                }
            }

            object valueField;
            public object Value
            {
                get => valueField;
                set
                {
                    if (valueField == value)
                    {
                        return;
                    }

                    valueField = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
                }
            }
            string error;
            public string Error
            {
                get => error;
                set
                {
                    if (error == value)
                    {
                        return;
                    }

                    error = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Error)));
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
        }

        public class TestCsv
        {
            public string? Name { get; set; }
            public string? City { get; set; }
            public int Age { get; set; }
        }

        #region Test Models
        public class TestModel
        {
            public string Name { get; set; }
            public int Age { get; set; }
            public double Salary { get; set; }
            public DateTime BirthDate { get; set; }
        }

        public class SimpleModel
        {
            public string Property1 { get; set; }
            public string Property2 { get; set; }
        }
        #endregion

        #region Empty/Null Input Tests
        [TestMethod]
        public void FromCsv_EmptyString_ReturnsEmptyArray()
        {
            // Arrange
            var csv = "";

            // Act
            var result = CsvHelper.FromCsv<TestModel>(csv, true);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Length);
        }

        [TestMethod]
        public void FromCsv_WhiteSpaceString_ReturnsEmptyArray()
        {
            // Arrange
            var csv = "   \n\n  \t  ";

            // Act
            var result = CsvHelper.FromCsv<TestModel>(csv, true);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Length);
        }

        [TestMethod]
        public void FromCsv_NullString_ReturnsEmptyArray()
        {
            // Act
            var result = CsvHelper.FromCsv<TestModel>(null, true);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Length);
        }
        #endregion

        #region Basic Parsing Tests
        [TestMethod]
        public void FromCsv_WithHeader_ValidData_ReturnsObjects()
        {
            // Arrange
            var csv = "Name;Age;Salary;BirthDate\nJohn;30;50000.50;1990-01-01\nJane;25;60000.75;1995-05-15";
            var dateParser = new Func<string, object>(s => DateTime.Parse(s));

            // Act
            var result = CsvHelper.FromCsv<TestModel>(csv, true, valueParser: s =>
            {
                if (DateTime.TryParse(s, out var date))
                    return date;
                if (int.TryParse(s, out var intValue))
                    return intValue;
                if (double.TryParse(s, out var doubleValue))
                    return doubleValue;
                return s;
            });

            // Assert
            Assert.AreEqual(2, result.Length);

            var first = result[0];
            Assert.AreEqual("John", first.Name);
            Assert.AreEqual(30, first.Age);
            Assert.AreEqual(50000.50, first.Salary);
            Assert.AreEqual(new DateTime(1990, 1, 1), first.BirthDate);

            var second = result[1];
            Assert.AreEqual("Jane", second.Name);
            Assert.AreEqual(25, second.Age);
            Assert.AreEqual(60000.75, second.Salary);
            Assert.AreEqual(new DateTime(1995, 5, 15), second.BirthDate);
        }

        [TestMethod]
        public void FromCsv_WithoutHeader_ValidData_ReturnsObjects()
        {
            // Arrange
            var csv = "Value1;Value2\nValue3;Value4";
            var expectedProperties = typeof(SimpleModel).GetProperties().OrderBy(p => p.Name).Select(p => p.Name).ToArray();

            // Act
            var result = CsvHelper.FromCsv<SimpleModel>(csv, false);

            // Assert
            Assert.AreEqual(2, result.Length);
            Assert.AreEqual("Value1", result[0].Property1);
            Assert.AreEqual("Value2", result[0].Property2);
            Assert.AreEqual("Value3", result[1].Property1);
            Assert.AreEqual("Value4", result[1].Property2);
        }
        #endregion

        #region Separator Tests
        [TestMethod]
        public void FromCsv_CustomColumnSeparator_WorksCorrectly()
        {
            // Arrange
            var csv = "Name;Age;Salary\nJohn;30;50000.50\nJane;25;60000.75";
            var separators = new[] { ";" };

            // Act
            var result = CsvHelper.FromCsv<TestModel>(csv, true, columnSeparators: separators, valueParser: s =>
            {
                if (int.TryParse(s, out var intValue))
                    return intValue;
                if (double.TryParse(s, out var doubleValue))
                    return doubleValue;
                return s;
            });

            // Assert
            Assert.AreEqual(2, result.Length);
            Assert.AreEqual("John", result[0].Name);
            Assert.AreEqual(30, result[0].Age);
            Assert.AreEqual("Jane", result[1].Name);
            Assert.AreEqual(25, result[1].Age);
        }

        [TestMethod]
        public void FromCsv_CustomLineSeparator_WorksCorrectly()
        {
            // Arrange
            var csv = "Name;Age|John;30|Jane;25";
            var lineSeparators = new[] { "|" };

            // Act
            var result = CsvHelper.FromCsv<TestModel>(csv, true, lineSeparators: lineSeparators, valueParser: s =>
            {
                if (int.TryParse(s, out var intValue))
                    return intValue;
                return s;
            });

            // Assert
            Assert.AreEqual(2, result.Length);
            Assert.AreEqual("John", result[0].Name);
            Assert.AreEqual(30, result[0].Age);
            Assert.AreEqual("Jane", result[1].Name);
            Assert.AreEqual(25, result[1].Age);
        }

        [TestMethod]
        public void FromCsv_MultipleColumnSeparators_WorksCorrectly()
        {
            // Arrange
            var csv = "Name\tAge\tSalary\nJohn\t30\t50000.50\nJane\t25\t60000.75";
            var separators = new[] { ",", "\t", ";" };

            // Act
            var result = CsvHelper.FromCsv<TestModel>(csv, true, columnSeparators: separators, valueParser: s =>
            {
                if (int.TryParse(s, out var intValue))
                    return intValue;
                if (double.TryParse(s, out var doubleValue))
                    return doubleValue;
                return s;
            });

            // Assert
            Assert.AreEqual(2, result.Length);
            Assert.AreEqual("John", result[0].Name);
            Assert.AreEqual("Jane", result[1].Name);
        }
        #endregion

        #region ValueParser Tests
        [TestMethod]
        public void FromCsv_CustomValueParser_WorksCorrectly()
        {
            // Arrange
            var csv = "Name;Age\nJohn;30\nJane;25";
            var customParser = new Func<string, object>(s =>
            {
                if (s == "John") return "Mr. John";
                if (s == "Jane") return "Ms. Jane";
                if (int.TryParse(s, out var age)) return age + 100; // Add 100 for test
                return s;
            });

            // Act
            var result = CsvHelper.FromCsv<TestModel>(csv, true, valueParser: customParser);

            // Assert
            Assert.AreEqual(2, result.Length);
            Assert.AreEqual("Mr. John", result[0].Name);
            Assert.AreEqual(130, result[0].Age); // 30 + 100
            Assert.AreEqual("Ms. Jane", result[1].Name);
            Assert.AreEqual(125, result[1].Age); // 25 + 100
        }

        [TestMethod]
        public void FromCsv_DefaultValueParser_ReturnsStrings()
        {
            // Arrange
            var csv = "Name;Age\nJohn;30\nJane;25";

            // Act
            var result = CsvHelper.FromCsv<TestModel>(csv, true);

            // Assert
            Assert.AreEqual(2, result.Length);
            Assert.AreEqual("John", result[0].Name);
            Assert.AreEqual(30, result[0].Age); // Note: Age is string because default parser returns string
            Assert.AreEqual("Jane", result[1].Name);
            Assert.AreEqual(25, result[1].Age);
        }
        #endregion

        #region Edge Cases Tests
        [TestMethod]
        public void FromCsv_MoreColumnsThanProperties_IgnoresExtraColumns()
        {
            // Arrange
            var csv = "Name;Age;Salary;Extra1;Extra2\nJohn;30;50000;ExtraValue1;ExtraValue2";

            // Act
            var result = CsvHelper.FromCsv<TestModel>(csv, true, valueParser: s =>
            {
                if (int.TryParse(s, out var intValue))
                    return intValue;
                if (double.TryParse(s, out var doubleValue))
                    return doubleValue;
                return s;
            });

            // Assert
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual("John", result[0].Name);
            Assert.AreEqual(30, result[0].Age);
            Assert.AreEqual(50000.0, result[0].Salary);
        }

        [TestMethod]
        public void FromCsv_MorePropertiesThanColumns_SetsRemainingToDefault()
        {
            // Arrange
            var csv = "Name\nJohn\nJane";

            // Act
            var result = CsvHelper.FromCsv<TestModel>(csv, true);

            // Assert
            Assert.AreEqual(2, result.Length);
            Assert.AreEqual("John", result[0].Name);
            Assert.AreEqual(default, result[0].Age);
            Assert.AreEqual(default, result[0].Salary);
            Assert.AreEqual(default, result[0].BirthDate);
        }

        [TestMethod]
        public void FromCsv_EmptyLines_AreIgnored()
        {
            // Arrange
            var csv = "Name;Age\n\nJohn;30\n\n\nJane;25\n";

            // Act
            var result = CsvHelper.FromCsv<TestModel>(csv, true, valueParser: s =>
            {
                if (int.TryParse(s, out var intValue))
                    return intValue;
                return s;
            });

            // Assert
            Assert.AreEqual(2, result.Length);
            Assert.AreEqual("John", result[0].Name);
            Assert.AreEqual("Jane", result[1].Name);
        }

        [TestMethod]
        public void FromCsv_OnlyHeader_ReturnsEmptyArray()
        {
            // Arrange
            var csv = "Name,Age,Salary";

            // Act
            var result = CsvHelper.FromCsv<TestModel>(csv, true);

            // Assert
            Assert.AreEqual(0, result.Length);
        }
        #endregion

        #region Property Matching Tests
        [TestMethod]
        public void FromCsv_HeaderWithDifferentCase_MatchesProperties()
        {
            // Arrange
            var csv = "NAME;age;SALARY\nJohn;30;50000";

            // Act
            var result = CsvHelper.FromCsv<TestModel>(csv, true, valueParser: s =>
            {
                if (int.TryParse(s, out var intValue))
                    return intValue;
                if (double.TryParse(s, out var doubleValue))
                    return doubleValue;
                return s;
            });

            // Assert
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual("John", result[0].Name);
            Assert.AreEqual(30, result[0].Age);
            Assert.AreEqual(50000.0, result[0].Salary);
        }

        [TestMethod]
        public void FromCsv_ExtraSpacesInHeader_TrimsAndMatches()
        {
            // Arrange
            var csv = " Name ; Age ; Salary \nJohn;30;50000";

            // Act
            var result = CsvHelper.FromCsv<TestModel>(csv, true, valueParser: s =>
            {
                if (int.TryParse(s, out var intValue))
                    return intValue;
                if (double.TryParse(s, out var doubleValue))
                    return doubleValue;
                return s;
            });

            // Assert
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual("John", result[0].Name);
            Assert.AreEqual(30, result[0].Age);
            Assert.AreEqual(50000.0, result[0].Salary);
        }
        #endregion
    }

    // Helper class for string extension (assuming it exists in your codebase)
    public static class StringExtensions
    {
        public static string[] SplitBy(this string input, StringSplitOptions options, string[] separators)
        {
            return input.Split(separators, options);
        }
    }

    // Stub for MemberCache<T> (assuming it exists in your codebase)
    public class MemberCache<T> where T : class, new()
    {
        public static MemberCache<T> Create()
        {
            return new MemberCache<T>();
        }

        public dynamic GetMember(string name)
        {
            // Simplified implementation for testing
            return new MemberInfoStub(name);
        }

        public dynamic[] PublicBasicProperties { get; } = typeof(T).GetProperties()
            .Select(p => new MemberInfoStub(p.Name))
            .ToArray();
    }

    public class MemberInfoStub
    {
        private readonly string _name;

        public MemberInfoStub(string name)
        {
            _name = name;
        }

        public void SetValue(object obj, object value)
        {
            var property = obj.GetType().GetProperty(_name);
            if (property != null && property.CanWrite)
            {
                // Handle type conversion
                if (value != null && property.PropertyType != value.GetType())
                {
                    if (property.PropertyType == typeof(int) && value is string stringValue)
                    {
                        if (int.TryParse(stringValue, out var intValue))
                            value = intValue;
                    }
                    else if (property.PropertyType == typeof(double) && value is string doubleString)
                    {
                        if (double.TryParse(doubleString, out var doubleValue))
                            value = doubleValue;
                    }
                    else if (property.PropertyType == typeof(DateTime) && value is string dateString)
                    {
                        if (DateTime.TryParse(dateString, out var dateValue))
                            value = dateValue;
                    }
                }

                property.SetValue(obj, value);
            }
        }
    }
}
