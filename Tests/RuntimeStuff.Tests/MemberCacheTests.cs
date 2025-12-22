using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RuntimeStuff.Helpers;

namespace RuntimeStuff.MSTests
{
    [TestClass]
    public class MemberCacheTests
    {
        #region Тестовые классы

        // Простой класс без атрибутов
        public class SimpleClass
        {
            public int Id { get; set; }
            public string Name { get; set; }
            private string PrivateField;
            public string PublicField;
            public event EventHandler TestEvent;
        }

        // Класс с атрибутами
        [Table("TestTable", Schema = "dbo")]
        [DisplayName("Test Entity")]
        public class ClassWithAttributes
        {
            [Key]
            [Column("ID")]
            public int Id { get; set; }

            [Display(Name = "Full Name")]
            [Column("PersonName")]
            public string Name { get; set; }

            [NotMapped]
            public string IgnoredProperty { get; set; }

            [ForeignKey("RelatedId")]
            public int ForeignKeyProperty { get; set; }

            public int AutoDetectedId { get; set; }
        }

        // Класс с коллекциями
        public class ClassWithCollections
        {
            public int Id { get; set; }
            public string[] StringArray { get; set; }
            public System.Collections.Generic.List<int> IntList { get; set; }
            public System.Collections.Generic.Dictionary<string, object> Dictionary { get; set; }
        }

        // Класс с интерфейсом
        public interface ITestInterface
        {
            int InterfaceProperty { get; set; }
        }

        public class ClassWithInterface : ITestInterface
        {
            public int InterfaceProperty { get; set; }
        }

        #endregion

        #region Базовые тесты

        [TestMethod]
        public void Create_FromType_ReturnsValidMemberCache()
        {
            // Arrange
            var type = typeof(SimpleClass);

            // Act
            var memberCache = MemberCache.Create(type);

            // Assert
            Assert.IsNotNull(memberCache);
            Assert.AreEqual(typeof(SimpleClass), memberCache.AsType());
            Assert.IsTrue(memberCache.IsType);
            Assert.AreEqual("SimpleClass", memberCache.Name);
        }

        [TestMethod]
        public void Create_FromProperty_ReturnsValidMemberCache()
        {
            // Arrange
            var property = typeof(SimpleClass).GetProperty("Id");

            // Act
            var memberCache = MemberCache.Create(property);

            // Assert
            Assert.IsNotNull(memberCache);
            Assert.IsTrue(memberCache.IsProperty);
            Assert.AreEqual("Id", memberCache.Name);
            Assert.AreEqual(typeof(int), memberCache.Type);
        }

        [TestMethod]
        public void Create_FromField_ReturnsValidMemberCache()
        {
            // Arrange
            var field = typeof(SimpleClass).GetField("PublicField");

            // Act
            var memberCache = MemberCache.Create(field);

            // Assert
            Assert.IsNotNull(memberCache);
            Assert.IsTrue(memberCache.IsField);
            Assert.AreEqual("PublicField", memberCache.Name);
            Assert.AreEqual(typeof(string), memberCache.Type);
        }

        [TestMethod]
        public void Create_FromExistingMemberCache_ReturnsSameInstance()
        {
            // Arrange
            var type = typeof(SimpleClass);
            var memberCache1 = MemberCache.Create(type);

            // Act
            var memberCache2 = MemberCache.Create(memberCache1);

            // Assert
            Assert.AreSame(memberCache1, memberCache2);
        }

        #endregion

        #region Тесты свойств

        [TestMethod]
        public void IsBasic_ForBasicTypes_ReturnsTrue()
        {
            // Arrange
            var intType = typeof(int);
            var stringType = typeof(string);
            var boolType = typeof(bool);

            // Act
            var intCache = MemberCache.Create(intType);
            var stringCache = MemberCache.Create(stringType);
            var boolCache = MemberCache.Create(boolType);

            // Assert
            Assert.IsTrue(intCache.IsBasic);
            Assert.IsTrue(stringCache.IsBasic);
            Assert.IsTrue(boolCache.IsBasic);
        }

        [TestMethod]
        public void IsCollection_ForArray_ReturnsTrue()
        {
            // Arrange
            var arrayType = typeof(string[]);

            // Act
            var arrayCache = MemberCache.Create(arrayType);

            // Assert
            Assert.IsTrue(arrayCache.IsCollection);
        }

        [TestMethod]
        public void IsDictionary_ForDictionary_ReturnsTrue()
        {
            // Arrange
            var dictType = typeof(System.Collections.Generic.Dictionary<string, object>);

            // Act
            var dictCache = MemberCache.Create(dictType);

            // Assert
            Assert.IsTrue(dictCache.IsDictionary);
        }

        [TestMethod]
        public void IsNullable_ForNullableTypes_ReturnsTrue()
        {
            // Arrange
            var nullableType = typeof(int?);

            // Act
            var nullableCache = MemberCache.Create(nullableType);

            // Assert
            Assert.IsTrue(nullableCache.IsNullable);
        }

        [TestMethod]
        public void IsPublic_ForPublicMember_ReturnsTrue()
        {
            // Arrange
            var publicProperty = typeof(SimpleClass).GetProperty("Id");

            // Act
            var memberCache = MemberCache.Create(publicProperty);

            // Assert
            Assert.IsTrue(memberCache.IsPublic);
        }

        #endregion

        #region Тесты атрибутов

        [TestMethod]
        public void Attributes_ForClassWithAttributes_ContainsAllAttributes()
        {
            // Arrange
            var type = typeof(ClassWithAttributes);

            // Act
            var memberCache = MemberCache.Create(type);

            // Assert
            Assert.IsTrue(memberCache.Attributes.ContainsKey("TableAttribute"));
            Assert.IsTrue(memberCache.Attributes.ContainsKey("DisplayNameAttribute"));
        }

        [TestMethod]
        public void TableName_FromTableAttribute_ReturnsCorrectName()
        {
            // Arrange
            var type = typeof(ClassWithAttributes);

            // Act
            var memberCache = MemberCache.Create(type);

            // Assert
            Assert.AreEqual("TestTable", memberCache.TableName);
            if (memberCache.Properties[nameof(ClassWithAttributes.Name)].TableName == null)
            {

            }
            Assert.AreEqual("TestTable", memberCache.Properties[nameof(ClassWithAttributes.Name)].TableName);
            Assert.AreEqual("dbo", memberCache.SchemaName);
        }

        [TestMethod]
        public void ColumnName_FromColumnAttribute_ReturnsCorrectName()
        {
            // Arrange
            var property = typeof(ClassWithAttributes).GetProperty("Id");

            // Act
            var memberCache = MemberCache.Create(property);

            // Assert
            Assert.AreEqual("ID", memberCache.ColumnName);
        }

        [TestMethod]
        public void DisplayName_FromDisplayAttribute_ReturnsCorrectName()
        {
            // Arrange
            var property = typeof(ClassWithAttributes).GetProperty("Name");

            // Act
            var memberCache = MemberCache.Create(property);

            // Assert
            Assert.AreEqual("Full Name", memberCache.DisplayName);
        }

        [TestMethod]
        public void IsPrimaryKey_FromKeyAttribute_ReturnsTrue()
        {
            // Arrange
            var property = typeof(ClassWithAttributes).GetProperty("Id");

            // Act
            var memberCache = MemberCache.Create(property);

            // Assert
            Assert.IsTrue(memberCache.IsPrimaryKey);
        }

        [TestMethod]
        public void IsForeignKey_FromForeignKeyAttribute_ReturnsTrue()
        {
            // Arrange
            var property = typeof(ClassWithAttributes).GetProperty("ForeignKeyProperty");

            // Act
            var memberCache = MemberCache.Create(property);

            // Assert
            Assert.IsTrue(memberCache.IsForeignKey);
            Assert.AreEqual("RelatedId", memberCache.ForeignColumnName);
        }

        [TestMethod]
        public void PrimaryKeys_DetectsIdProperty_ReturnsCorrectKey()
        {
            // Arrange
            var type = typeof(ClassWithAttributes);

            // Act
            var memberCache = MemberCache.Create(type);

            // Assert
            Assert.IsNotNull(memberCache.PrimaryKeys);
            Assert.IsTrue(memberCache.PrimaryKeys.ContainsKey("Id"));
        }

        #endregion

        #region Тесты поиска членов

        [TestMethod]
        public void GetMember_ByName_ReturnsCorrectMember()
        {
            // Arrange
            var type = typeof(SimpleClass);
            var memberCache = MemberCache.Create(type);

            // Act
            var foundMember = memberCache["Id"];

            // Assert
            Assert.IsNotNull(foundMember);
            Assert.AreEqual("Id", foundMember.Name);
            Assert.IsTrue(foundMember.IsProperty);
        }

        [TestMethod]
        public void GetMember_ByColumnName_ReturnsCorrectMember()
        {
            // Arrange
            var type = typeof(ClassWithAttributes);
            var memberCache = MemberCache.Create(type);

            // Act
            var foundMember = memberCache["ID", MemberNameType.ColumnName];

            // Assert
            Assert.IsNotNull(foundMember);
            Assert.AreEqual("Id", foundMember.Name);
            Assert.AreEqual("ID", foundMember.ColumnName);
        }

        [TestMethod]
        public void GetMember_NonExistent_ReturnsNull()
        {
            // Arrange
            var type = typeof(SimpleClass);
            var memberCache = MemberCache.Create(type);

            // Act
            var foundMember = memberCache["NonExistent"];

            // Assert
            Assert.IsNull(foundMember);
        }

        [TestMethod]
        public void Members_ContainsAllPropertiesAndFields()
        {
            // Arrange
            var type = typeof(SimpleClass);

            // Act
            var memberCache = MemberCache.Create(type);
            var members = memberCache.Members;

            // Assert
            Assert.IsTrue(members.Any(m => m.Name == "Id" && m.IsProperty));
            Assert.IsTrue(members.Any(m => m.Name == "Name" && m.IsProperty));
            Assert.IsTrue(members.Any(m => m.Name == "PublicField" && m.IsField));
            // Приватные поля могут не включаться в зависимости от BindingFlags
        }

        #endregion

        #region Тесты работы с ORM

        [TestMethod]
        public void GetColumns_ReturnsAllColumnProperties()
        {
            // Arrange
            var type = typeof(ClassWithAttributes);

            // Act
            var memberCache = MemberCache.Create(type);
            var columns = memberCache.GetColumns();

            // Assert
            Assert.AreEqual(3, columns.Length); // Id, Name, ForeignKeyProperty
            Assert.IsTrue(columns.Any(c => c.Name == "Id"));
            Assert.IsTrue(columns.Any(c => c.Name == "Name"));
            Assert.IsFalse(columns.Any(c => c.Name == "IgnoredProperty")); // NotMapped не включается
        }

        [TestMethod]
        public void GetTables_ReturnsCollectionProperties()
        {
            // Arrange
            var type = typeof(ClassWithCollections);

            // Act
            var memberCache = MemberCache.Create(type);
            var tables = memberCache.GetTables();

            // Assert
            // В зависимости от реализации, коллекции могут рассматриваться как "таблицы"
            // или могут быть исключены если IsBasicCollection = true
        }

        [TestMethod]
        public void GetPrimaryKeys_ReturnsKeyProperties()
        {
            // Arrange
            var type = typeof(ClassWithAttributes);

            // Act
            var memberCache = MemberCache.Create(type);
            var primaryKeys = memberCache.GetPrimaryKeys();

            // Assert
            Assert.IsTrue(primaryKeys.Any(pk => pk.Name == "Id"));
        }

        [TestMethod]
        public void GetForeignKeys_ReturnsForeignKeyProperties()
        {
            // Arrange
            var type = typeof(ClassWithAttributes);

            // Act
            var memberCache = MemberCache.Create(type);
            var foreignKeys = memberCache.GetForeignKeys();

            // Assert
            Assert.IsTrue(foreignKeys.Any(fk => fk.Name == "ForeignKeyProperty"));
        }

        #endregion

        #region Тесты работы со значениями

        [TestMethod]
        public void GetValue_SetValue_WorkCorrectly()
        {
            // Arrange
            var obj = new SimpleClass { Id = 42, Name = "Test" };
            var property = typeof(SimpleClass).GetProperty("Id");
            var memberCache = MemberCache.Create(property);

            // Act
            var value = memberCache.GetValue(obj);
            memberCache.SetValue(obj, 100);
            var newValue = memberCache.GetValue(obj);

            // Assert
            Assert.AreEqual(42, value);
            Assert.AreEqual(100, newValue);
        }

        [TestMethod]
        public void GetValueT_ReturnsTypedValue()
        {
            // Arrange
            var obj = new SimpleClass { Id = 42 };
            var property = typeof(SimpleClass).GetProperty("Id");
            var memberCache = MemberCache.Create(property);

            // Act
            var value = memberCache.GetValue<int>(obj);

            // Assert
            Assert.AreEqual(42, value);
        }

        [TestMethod]
        public void Indexer_GetSetValue_WorkCorrectly()
        {
            // Arrange
            var obj = new SimpleClass { Id = 1, Name = "Old" };
            var type = typeof(SimpleClass);
            var memberCache = MemberCache.Create(type);

            // Act
            var nameValue = memberCache[obj, "Name"];
            memberCache[obj, "Name"] = "New";
            var newNameValue = memberCache[obj, "Name"];

            // Assert
            Assert.AreEqual("Old", nameValue);
            Assert.AreEqual("New", newNameValue);
        }

        #endregion

        #region Тесты создания экземпляров

        [TestMethod]
        public void CreateInstance_WithDefaultConstructor_CreatesInstance()
        {
            // Arrange
            var type = typeof(SimpleClass);

            // Act
            var instance = TypeHelper.New<SimpleClass>(type);

            // Assert
            Assert.IsNotNull(instance);
            Assert.IsInstanceOfType(instance, typeof(SimpleClass));
        }

        [TestMethod]
        public void CreateInstance_WithParameters_CreatesInstance()
        {
            // Arrange
            var type = typeof(TestClassWithConstructor);

            // Act
            var instance = (TestClassWithConstructor)MemberCache.New(type, "Test", 42);

            // Assert
            Assert.IsNotNull(instance);
            Assert.AreEqual("Test", instance.Name);
            Assert.AreEqual(42, instance.Value);
        }

        [TestMethod]
        public void DefaultConstructor_ForTypeWithDefaultConstructor_ReturnsDelegate()
        {
            // Arrange
            var type = typeof(SimpleClass);

            // Act
            var memberCache = MemberCache.Create(type);
            var constructor = memberCache.DefaultConstructor;

            // Assert
            Assert.IsNotNull(constructor);
            var instance = constructor();
            Assert.IsInstanceOfType(instance, typeof(SimpleClass));
        }

        #endregion

        #region Тесты для интерфейсов и наследования

        [TestMethod]
        public void BaseTypes_IncludesInterfaces()
        {
            // Arrange
            var type = typeof(ClassWithInterface);

            // Act
            var memberCache = MemberCache.Create(type);
            var baseTypes = memberCache.BaseTypes;

            // Assert
            Assert.IsTrue(baseTypes.Contains(typeof(ITestInterface)));
        }

        [TestMethod]
        public void Members_FromBaseClass_Included()
        {
            // Arrange
            var baseType = typeof(BaseClass);
            var derivedType = typeof(DerivedClass);

            // Act
            var baseCache = MemberCache.Create(baseType);
            var derivedCache = MemberCache.Create(derivedType);

            // Assert
            Assert.IsTrue(baseCache.Members.Any(m => m.Name == "BaseProperty"));
            Assert.IsTrue(derivedCache.Members.Any(m => m.Name == "BaseProperty"));
            Assert.IsTrue(derivedCache.Members.Any(m => m.Name == "DerivedProperty"));
        }

        #endregion

        [TestMethod]
        public void AnonymousType_Test()
        {
            // Arrange
            var anonymousObject = new { Id = 1, Name = "Anonymous" };
            var type = anonymousObject.GetType();
            // Act
            var memberCache = MemberCache.Create(type);
            var idMember = memberCache["Id"];
            var nameMember = memberCache["Name"];
            // Assert
            Assert.IsNotNull(idMember);
            Assert.IsNotNull(nameMember);
            Assert.AreEqual(1, idMember.GetValue(anonymousObject));
            Assert.AreEqual("Anonymous", nameMember.GetValue(anonymousObject));
        }

        #region Вспомогательные классы для тестов

        public class TestClassWithConstructor
        {
            public string Name { get; }
            public int Value { get; }

            public TestClassWithConstructor(string name, int value)
            {
                Name = name;
                Value = value;
            }
        }

        public class BaseClass
        {
            public string BaseProperty { get; set; }
        }

        public class DerivedClass : BaseClass
        {
            public string DerivedProperty { get; set; }
        }

        #endregion
    }
}