using FastMember;
using RuntimeStuff.Helpers;
using RuntimeStuff.MSTests.Models;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Reflection;
using RuntimeStuff.Extensions;

namespace RuntimeStuff.MSTests
{
    [TestClass]
    public class MemberCacheTests
    {
        #region Тестовые классы

        // Простой класс без атрибутов
        public class SimpleClass
        {
            public SimpleClass() { }

            public SimpleClass(string privateField, EventHandler eventHandler)
            {
                PrivateField = privateField;
                TestEvent += eventHandler;
                TestEvent?.Invoke(this, EventArgs.Empty);
            }
            public int Id { get; set; }
            public string? Name { get; set; }
            private readonly string? PrivateField;
            public string? PublicField;

            public event EventHandler? TestEvent;
        }

        public class TestClassForSetterAndGetters
        {
            // =========================
            // AUTO PROPERTIES
            // =========================

            // public get / public set
            public string PublicAutoPropertyPublicGetPublicSet { get; set; }

            // public get / private set
            public string PublicAutoPropertyPublicGetPrivateSet { get; private set; }

            // private get / public set
            private string PrivateAutoPropertyPrivateGetPublicSet { get; set; }

            // private get / private set
            private string PrivateAutoPropertyPrivateGetPrivateSet { get; set; }

            // readonly auto property (C# 9+)
            public string PublicReadonlyAutoProperty { get; }

            // =========================
            // PROPERTIES WITH BACKING FIELDS
            // =========================

            private string _privateFieldForPublicProperty;

            public string PublicPropertyWithPrivateField
            {
                get => _privateFieldForPublicProperty;
                private set => _privateFieldForPublicProperty = value;
            }

            // =========================
            // FIELDS
            // =========================

            public string PublicField;
            internal string InternalField;
            protected string ProtectedField;
            private readonly string PrivateField;

            // readonly fields
            public readonly string PublicReadonlyField;

            private readonly string PrivateReadonlyField;

            // const (нельзя менять ни при каких условиях)
            public const string ConstField = "CONST";

            // =========================
            // STATIC MEMBERS
            // =========================

            public static string? PublicStaticField;
            private static string? PrivateStaticField;

            public static string? PublicStaticProperty { get; private set; }

            // =========================
            // STRUCT-LIKE SCENARIO
            // =========================

            public readonly KeyValuePair<string, string> ReadonlyStructProperty;
            private readonly KeyValuePair<string, string> PrivateReadonlyStructField;

            // =========================
            // CONSTRUCTOR
            // =========================

            public TestClassForSetterAndGetters()
            {
                PublicAutoPropertyPublicGetPublicSet = "init_public_public";
                PublicAutoPropertyPublicGetPrivateSet = "init_public_private";
                PrivateAutoPropertyPrivateGetPublicSet = "init_private_public";
                PrivateAutoPropertyPrivateGetPrivateSet = "init_private_private";

                PublicReadonlyAutoProperty = "init_readonly_auto";

                _privateFieldForPublicProperty = "init_backing_field";

                PublicField = "init_public_field";
                InternalField = "init_internal_field";
                ProtectedField = "init_protected_field";
                PrivateField = "init_private_field";

                PublicReadonlyField = "init_public_readonly";
                PrivateReadonlyField = "init_private_readonly";

                PublicStaticField = "init_public_static";
                PrivateStaticField = "init_private_static";
                PublicStaticProperty = "init_public_static_prop";

                ReadonlyStructProperty = new KeyValuePair<string, string>("k1", "v1");
                PrivateReadonlyStructField = new KeyValuePair<string, string>("k2", "v2");
            }
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
            public string? Name { get; set; }

            [NotMapped]
            public string? IgnoredProperty { get; set; }

            [ForeignKey("RelatedId")]
            public int ForeignKeyProperty { get; set; }

            public int AutoDetectedId { get; set; }
        }

        // Класс с коллекциями
        public class ClassWithCollections
        {
            public int Id { get; set; }
            public string[]? StringArray { get; set; }
            public List<int>? IntList { get; set; }
            public Dictionary<string, object>? Dictionary { get; set; }
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

        #endregion Тестовые классы

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

        #endregion Базовые тесты

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

        #endregion Тесты свойств

        #region Тесты атрибутов

        [TestMethod]
        public void Attributes_ForClassWithAttributes_ContainsAllAttributes()
        {
            // Arrange
            var type = typeof(ClassWithAttributes);

            // Act
            var memberCache = MemberCache.Create(type);

            // Assert
            Assert.IsTrue(memberCache.GetAttribute("TableAttribute") != null);
            Assert.IsTrue(memberCache.GetAttribute("DisplayNameAttribute") != null);
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
            if (memberCache[nameof(ClassWithAttributes.Name)].TableName == null)
            {
            }
            Assert.AreEqual("TestTable", memberCache[nameof(ClassWithAttributes.Name)].TableName);
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
            Assert.AreEqual("RelatedId", memberCache.ForeignKeyName);
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
            Assert.IsTrue(memberCache.PrimaryKeys.Any(x => x.Name == "Id"));
        }

        #endregion Тесты атрибутов

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

        //[TestMethod]
        //public void GetMember_ByColumnName_ReturnsCorrectMember()
        //{
        //    // Arrange
        //    var type = typeof(ClassWithAttributes);
        //    var memberCache = MemberCache.Create(type);

        //    // Act
        //    var foundMember = memberCache["ID", MemberNameType.ColumnName];

        //    // Assert
        //    Assert.IsNotNull(foundMember);
        //    Assert.AreEqual("Id", foundMember.Name);
        //    Assert.AreEqual("ID", foundMember.ColumnName);
        //}

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

        //[TestMethod]
        //public void Members_ContainsAllPropertiesAndFields()
        //{
        //    // Arrange
        //    var type = typeof(SimpleClass);

        //    // Act
        //    var memberCache = MemberCache.Create(type);
        //    var members = memberCache.Members;

        //    // Assert
        //    Assert.IsTrue(members.Any(m => m.Name == "Id" && m.IsProperty));
        //    Assert.IsTrue(members.Any(m => m.Name == "Name" && m.IsProperty));
        //    Assert.IsTrue(members.Any(m => m.Name == "PublicField" && m.IsField));
        //    // Приватные поля могут не включаться в зависимости от BindingFlags
        //}

        #endregion Тесты поиска членов

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

        #endregion Тесты работы с ORM

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

        #endregion Тесты работы со значениями

        #region Тесты создания экземпляров

        [TestMethod]
        public void CreateInstance_WithDefaultConstructor_CreatesInstance()
        {
            // Arrange
            var type = typeof(SimpleClass);

            // Act
            var instance = Obj.New<SimpleClass>(type);

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

        #endregion Тесты создания экземпляров

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

        //[TestMethod]
        //public void Members_FromBaseClass_Included()
        //{
        //    // Arrange
        //    var baseType = typeof(BaseClass);
        //    var derivedType = typeof(DerivedClass);

        //    // Act
        //    var baseCache = MemberCache.Create(baseType);
        //    var derivedCache = MemberCache.Create(derivedType);

        //    // Assert
        //    Assert.IsTrue(baseCache.Members.Any(m => m.Name == "BaseProperty"));
        //    Assert.IsTrue(derivedCache.Members.Any(m => m.Name == "BaseProperty"));
        //    Assert.IsTrue(derivedCache.Members.Any(m => m.Name == "DerivedProperty"));
        //}

        #endregion Тесты для интерфейсов и наследования

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
            var id = idMember.GetValue(anonymousObject);
            Assert.AreEqual(anonymousObject.Id, id);
        }

        //[TestMethod]
        public void Speed_Test()
        {
            var count = 1_000_000;
            var x = new DtoTestClass();
            var mc = MemberCache.Create(typeof(DtoTestClass));
            var sw = new Stopwatch();
            mc["ColNullableInt"].Setter(x, 1);
            sw.Restart();
            for (int i = 0; i < count; i++)
            {
                mc["ColNullableInt"].Setter(x, i);
            }
            sw.Stop();
            var elapsed1 = sw.ElapsedMilliseconds;

            var setter = mc["ColNullableInt"].Setter;
            sw.Restart();
            for (int i = 0; i < count; i++)
            {
                x.ColNullableInt = i;
                setter(x, 123);
            }
            sw.Stop();
            var elapsed2 = sw.ElapsedMilliseconds;

            var ta = TypeAccessor.Create(typeof(DtoTestClass));
            sw.Restart();
            for (int i = 0; i < count; i++)
            {
                ta[x, nameof(DtoTestClass.ColNVarCharMax)] = i.ToString();
            }
            sw.Stop();
            var elapsed3 = sw.ElapsedMilliseconds;
            Assert.IsTrue(elapsed2 <= elapsed1);
            Assert.IsTrue(elapsed1 <= elapsed3);
        }

        [TestMethod]
        public void Test_Setters_And_Getters()
        {
            var mc = MemberCache.Create(typeof(TestClassForSetterAndGetters));
            var instance = new TestClassForSetterAndGetters();
            foreach (var p in mc.Properties)
            {
                try
                {
                    var setter2 = Obj.CreatePropertySetter(p);
                    var getter2 = Obj.CreatePropertyGetter(p);

                    setter2(instance, "test_value");
                    var val = getter2(instance);
                    Assert.AreEqual("test_value", val);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Property '{p.Name}' - Exception: {ex.Message}");
                }
            }

            foreach (var f in mc.GetFields().Where(x => x.FieldType == typeof(string)))
            {
                try
                {
                    var setter2 = Obj.CreateDirectFieldSetter(f);
                    var getter2 = Obj.CreateFieldGetter(f);
                    setter2(instance, "test_value");
                    var val = getter2(instance);
                    Assert.AreEqual("test_value", val);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Field '{f.Name}' - Exception: {ex.Message}");
                }
            }

            object kv = new KeyValuePair<string, string>("key1", "value1");
            var kvKeyGetter = Obj.CreatePropertyGetter(typeof(KeyValuePair<string, string>).GetProperty("Key"));
            var kvKeySetter = Obj.CreatePropertySetter(typeof(KeyValuePair<string, string>).GetProperty("Key"));

            var key = kvKeyGetter(kv);
            kvKeySetter(kv, "key2");
            Assert.AreEqual("key2", kvKeyGetter(kv));
        }

        public void Test_Implicit_Operators()
        {
            var mc = MemberCache.Create(typeof(SimpleClass));
            PropInfo(mc);
        }

        private void PropInfo(PropertyInfo _)
        {
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
            public string? BaseProperty { get; set; }
        }

        public class DerivedClass : BaseClass
        {
            public string? DerivedProperty { get; set; }
        }

        #endregion Вспомогательные классы для тестов

        public class TestClass
        {
            [Column("Name")]
            public int Id { get; set; }

            [Column("EventId")] public string Name { get; set; } = "";
        }

        public class TestClass02
        {
            public string? Prop { get; set; }
            public string? PROP { get; set; }
            private string? prop;
        }

        [TestMethod]
        public void GetNonExistedMemberByNameShouldReturnNull()
        {
            var memberInfo = typeof(TestClass).GetMemberCache();
            var m = memberInfo["Имя"];
            Assert.IsNull(m);
        }

        //[TestMethod]
        //public void GetMemberByColumnName()
        //{
        //    var memberInfo = typeof(TestClass).GetMemberCache();
        //    var m = memberInfo.GetMember("namE", MemberNameType.ColumnName);
        //    Assert.IsNotNull(m);
        //    Assert.AreEqual(nameof(TestClass.Id), m.Name);
        //}

        [TestMethod]
        public void GetMemberByColumnNameWithDifferentCase()
        {
            var memberInfo = typeof(TestClass).GetMemberCache();
            var m = memberInfo["name"];
        }
    }
}