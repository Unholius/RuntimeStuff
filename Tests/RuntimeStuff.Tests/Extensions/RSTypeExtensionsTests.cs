//using System.Collections;
//using System.Data.SqlTypes;
//using RuntimeStuff.Extensions;

//namespace RuntimeStuff.MSTests.Extensions
//{
//    [TestClass]
//    public class RSTypeExtensionsTests
//    {
//        class Base { public int BaseField; public virtual void BaseMethod() { } public int BaseProp { get; set; } }
//        class Derived : Base { public int DerivedField; public override void BaseMethod() { } public int DerivedProp { get; set; } }
//        delegate void MyDelegate();

//        [TestMethod]
//        public void BoolTypes_ContainsExpectedTypes()
//        {
//            Assert.IsTrue(RSTypeExtensions.BoolTypes.Contains(typeof(bool)));
//            Assert.IsTrue(RSTypeExtensions.BoolTypes.Contains(typeof(SqlBoolean)));
//        }

//        [TestMethod]
//        public void DateTypes_ContainsDateTime()
//        {
//            Assert.IsTrue(RSTypeExtensions.DateTypes.Contains(typeof(DateTime)));
//            Assert.IsTrue(RSTypeExtensions.DateTypes.Contains(typeof(DateTime?)));
//        }

//        [TestMethod]
//        public void FloatNumberTypes_ContainsFloatTypes()
//        {
//            Assert.IsTrue(RSTypeExtensions.FloatNumberTypes.Contains(typeof(float)));
//            Assert.IsTrue(RSTypeExtensions.FloatNumberTypes.Contains(typeof(double?)));
//        }

//        [TestMethod]
//        public void InterfaceToInstanceMap_ContainsMappings()
//        {
//            Assert.AreEqual(typeof(List<object>), RSTypeExtensions.InterfaceToInstanceMap[typeof(IEnumerable)]);
//            Assert.AreEqual(typeof(Dictionary<,>), RSTypeExtensions.InterfaceToInstanceMap[typeof(IDictionary<,>)]);
//        }

//        [TestMethod]
//        public void IntNumberTypes_ContainsIntTypes()
//        {
//            Assert.IsTrue(RSTypeExtensions.IntNumberTypes.Contains(typeof(int)));
//            Assert.IsTrue(RSTypeExtensions.IntNumberTypes.Contains(typeof(long?)));
//        }

//        [TestMethod]
//        public void NullValues_ContainsNullAndDBNull()
//        {
//            Assert.IsTrue(RSTypeExtensions.NullValues.Contains(null));
//            Assert.IsTrue(RSTypeExtensions.NullValues.Contains(DBNull.Value));
//        }

//        [TestMethod]
//        public void NumberTypes_ContainsIntAndFloat()
//        {
//            Assert.IsTrue(RSTypeExtensions.NumberTypes.Contains(typeof(int)));
//            Assert.IsTrue(RSTypeExtensions.NumberTypes.Contains(typeof(double)));
//        }

//        [TestMethod]
//        public void BasicTypes_ContainsStringAndGuid()
//        {
//            Assert.IsTrue(RSTypeExtensions.BasicTypes.Contains(typeof(string)));
//            Assert.IsTrue(RSTypeExtensions.BasicTypes.Contains(typeof(Guid)));
//        }

//        [TestMethod]
//        public void Create_DefaultCtor()
//        {
//            var obj = typeof(List<int>).Create();
//            Assert.IsInstanceOfType(obj, typeof(List<int>));
//        }

//        [TestMethod]
//        public void Create_WithCtorArgs()
//        {
//            var obj = typeof(List<int>).Create(100);
//            Assert.IsInstanceOfType(obj, typeof(List<int>));
//            Assert.AreEqual(100, ((List<int>)obj).Capacity);
//        }

//        [TestMethod]
//        public void Create_InterfaceCollection()
//        {
//            var obj = typeof(IEnumerable<int>).Create();
//            Assert.IsInstanceOfType(obj, typeof(List<int>));
//        }

//        [TestMethod]
//        public void Create_Array()
//        {
//            var arr = (int[])typeof(int[]).Create(3);
//            Assert.AreEqual(3, arr.Length);
//        }

//        [TestMethod]
//        public void Create_Enum()
//        {
//            var val = (DayOfWeek)typeof(DayOfWeek).Create();
//            Assert.AreEqual(default, val);
//        }

//        [TestMethod]
//        public void Create_String()
//        {
//            var str = (string)typeof(string).Create();
//            Assert.AreEqual(string.Empty, str);
//        }

//        [TestMethod]
//        public void Default_ReturnsDefaultValue()
//        {
//            Assert.AreEqual(0, typeof(int).Default());
//            Assert.IsNull(typeof(string).Default());
//        }

//        [TestMethod]
//        public void GetBaseTypes_ReturnsBaseTypes()
//        {
//            var types = typeof(Derived).GetBaseTypes(includeThis: true, getInterfaces: false);
//            Assert.IsTrue(types.Contains(typeof(Base)));
//            Assert.IsTrue(types.Contains(typeof(Derived)));
//        }

//        [TestMethod]
//        public void GetBaseTypes_WithInterfaces()
//        {
//            var types = typeof(List<int>).GetBaseTypes(getInterfaces: true);
//            Assert.IsTrue(types.Any(t => t == typeof(IEnumerable<int>)));
//        }

//        [TestMethod]
//        public void GetCollectionItemType_Array()
//        {
//            Assert.AreEqual(typeof(int), typeof(int[]).GetCollectionItemType());
//        }

//        [TestMethod]
//        public void GetCollectionItemType_GenericList()
//        {
//            Assert.AreEqual(typeof(int), typeof(List<int>).GetCollectionItemType());
//        }

//        [TestMethod]
//        public void GetCollectionItemType_Dictionary()
//        {
//            Assert.AreEqual(typeof(int), typeof(Dictionary<string, int>).GetCollectionItemType());
//        }

//        [TestMethod]
//        public void GetField_FindsField()
//        {
//            var fi = typeof(Derived).GetField(f => f.Name == "DerivedField");
//            Assert.IsNotNull(fi);
//            Assert.AreEqual("DerivedField", fi.Name);
//        }

//        [TestMethod]
//        public void GetLowestEvent_ReturnsNullIfNotFound()
//        {
//            Assert.IsNull(typeof(Derived).GetLowestEvent("NoSuchEvent"));
//        }

//        [TestMethod]
//        public void GetLowestField_FindsField()
//        {
//            var fi = typeof(Derived).GetLowestField("BaseField");
//            Assert.IsNotNull(fi);
//            Assert.AreEqual("BaseField", fi.Name);
//        }

//        [TestMethod]
//        public void GetLowestMethod_FindsMethod()
//        {
//            var mi = typeof(Derived).GetLowestMethod("BaseMethod");
//            Assert.IsNotNull(mi);
//            Assert.AreEqual("BaseMethod", mi.Name);
//        }

//        [TestMethod]
//        public void GetLowestProperty_FindsProperty()
//        {
//            var pi = typeof(Derived).GetLowestProperty("BaseProp");
//            Assert.IsNotNull(pi);
//            Assert.AreEqual("BaseProp", pi.Name);
//        }

//        [TestMethod]
//        public void IsBasic_TrueForIntAndEnum()
//        {
//            Assert.IsTrue(typeof(int).IsBasic());
//            Assert.IsTrue(typeof(DayOfWeek).IsBasic());
//            Assert.IsFalse(typeof(List<int>).IsBasic());
//        }

//        [TestMethod]
//        public void IsBoolean_TrueForBool()
//        {
//            Assert.IsTrue(typeof(bool).IsBoolean());
//            Assert.IsFalse(typeof(int).IsBoolean());
//        }

//        [TestMethod]
//        public void IsCollection_TrueForListAndArray()
//        {
//            Assert.IsTrue(typeof(List<int>).IsCollection());
//            Assert.IsTrue(typeof(int[]).IsCollection());
//            Assert.IsFalse(typeof(string).IsCollection());
//        }

//        [TestMethod]
//        public void IsDate_TrueForDateTime()
//        {
//            Assert.IsTrue(typeof(DateTime).IsDate());
//            Assert.IsFalse(typeof(int).IsDate());
//        }

//        [TestMethod]
//        public void IsDelegate_TrueForDelegate()
//        {
//            Assert.IsTrue(typeof(MyDelegate).IsDelegate());
//            Assert.IsFalse(typeof(int).IsDelegate());
//        }

//        [TestMethod]
//        public void IsDictionary_TrueForDictionary()
//        {
//            Assert.IsTrue(typeof(Dictionary<string, int>).IsDictionary());
//            Assert.IsFalse(typeof(List<int>).IsDictionary());
//        }

//        [TestMethod]
//        public void IsFloat_TrueForDouble()
//        {
//            Assert.IsTrue(typeof(double).IsFloat());
//            Assert.IsFalse(typeof(int).IsFloat());
//        }

//        [TestMethod]
//        public void IsImplements_Interface()
//        {
//            Assert.IsTrue(typeof(List<int>).IsImplements(typeof(IEnumerable)));
//            Assert.IsTrue(typeof(List<int>).IsImplements<IEnumerable>());
//        }

//        [TestMethod]
//        public void IsNullable_TrueForNullable()
//        {
//            Assert.IsTrue(typeof(int?).IsNullable());
//            Assert.IsTrue(typeof(string).IsNullable());
//            Assert.IsFalse(typeof(int).IsNullable());
//        }

//        [TestMethod]
//        public void IsNumeric_TrueForIntAndDouble()
//        {
//            Assert.IsTrue(typeof(int).IsNumeric());
//            Assert.IsTrue(typeof(double).IsNumeric());
//            Assert.IsFalse(typeof(string).IsNumeric());
//        }

//        [TestMethod]
//        public void IsTuple_TrueForTupleTypes()
//        {
//            Assert.IsTrue(typeof(Tuple<int, string>).IsTuple());
//            Assert.IsTrue(typeof(ValueTuple<int, string>).IsTuple());
//            Assert.IsFalse(typeof(int).IsTuple());
//        }
//    }
//}