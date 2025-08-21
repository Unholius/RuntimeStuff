using System.Diagnostics;

namespace RuntimeStuff.MSTests
{
    [TestClass]
    public class ObjTests
    {
        class TestClass
        {
            public int IntProp { get; set; }
            public string? StrProp { get; set; }
            public double DoubleProp = 0f;
            public int MethodCalled = 0;
            public string StrField;
            public List<SubClass> SubList { get; set; }
            public SubClass Sub { get; set; }
            public int Method1()
            {
                MethodCalled++;
                return 42;
            }

            private int _backField;

            public int BackFieldProp => _backField;
        }

        class SubClass
        {
            public string Name { get; set; }
        }

        [TestMethod]
        public void GetMember_ReturnsMemberInfoEx()
        {
            var obj = new TestClass();
            var member = Obj.GetMember(obj, "IntProp");
            Assert.IsNotNull(member);
            Assert.AreEqual("IntProp", member.Name);
        }

        [TestMethod]
        public void Increase_IncrementsNumericValue()
        {
            object value = 5;
            Obj.Increase(ref value, 2);
            Assert.AreEqual(7, value);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Increase_ThrowsOnNull()
        {
            object? value = null;
            Obj.Increase(ref value);
        }

        [TestMethod]
        public void ChangeType_ConvertsBetweenTypes()
        {
            var result = Obj.ChangeType("123", typeof(int));
            Assert.AreEqual(123, result);
        }

        [TestMethod]
        public void GetCustomTypeConverter_ReturnsNullIfNotFound()
        {
            var converter = Obj.GetCustomTypeConverter(typeof(Guid), typeof(string));
            Assert.IsNull(converter);
        }

        [TestMethod]
        public void AddCustomTypeConverter_Works()
        {
            Obj.AddCustomTypeConverter<int, string>(i => (i + 1).ToString());
            var converter = Obj.GetCustomTypeConverter(typeof(int), typeof(string));
            Assert.IsNotNull(converter);
            Assert.AreEqual("6", converter(5));
        }

        [TestMethod]
        public void ChangeTypeT_Converts()
        {
            int result = Obj.ChangeType<int>("42");
            Assert.AreEqual(42, result);
        }

        [TestMethod]
        public void TryChangeTypeT_Success()
        {
            bool ok = Obj.TryChangeType<int>("123", out var result);
            Assert.IsTrue(ok);
            Assert.AreEqual(123, result);
        }

        [TestMethod]
        public void TryChangeTypeT_Failure()
        {
            bool ok = Obj.TryChangeType<int>("abc", out var result);
            Assert.IsFalse(ok);
            Assert.AreEqual(0, result);
        }

        [TestMethod]
        public void TryChangeType_MultipleTypes()
        {
            bool ok = Obj.TryChangeType("true", [typeof(bool), typeof(int)], out var result);
            Assert.IsTrue(ok);
            Assert.AreEqual(true, result);
        }

        [TestMethod]
        public void TryChangeType_SingleType()
        {
            bool ok = Obj.TryChangeType("42", typeof(int), out var result);
            Assert.IsTrue(ok);
            Assert.AreEqual(42, result);
        }

        [TestMethod]
        public void ZipAndUnZip_RoundTrip()
        {
            var data = new byte[] { 1, 2, 3, 4, 5 };
            var zipped = Obj.Zip(data);
            var unzipped = Obj.UnZip(zipped);
            CollectionAssert.AreEqual(data, unzipped);
        }

        [TestMethod]
        public void CopyTo_CopiesStream()
        {
            var src = new MemoryStream([1, 2, 3]);
            var dest = new MemoryStream();
            Obj.CopyTo(src, dest);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, dest.ToArray());
        }

        [TestMethod]
        public void Get_ReturnsPropertyValue()
        {
            var obj = new TestClass { IntProp = 99 };
            var value = Obj.Get(obj, "IntProp");
            Assert.AreEqual(99, value);
        }

        [TestMethod]
        public void Set_SetsPropertyValue()
        {
            var obj = new TestClass();
            bool ok = Obj.Set(obj, "IntProp", 123);
            Assert.IsTrue(ok);
            Assert.AreEqual(123, obj.IntProp);
        }

        [TestMethod]
        public void Set_MultipleValues()
        {
            var obj = new TestClass();
            Obj.Set(obj, [
                new KeyValuePair<string, object>("IntProp", 1), new KeyValuePair<string, object>("StrProp", "abc")
            ]);
            Assert.AreEqual(1, obj.IntProp);
            Assert.AreEqual("abc", obj.StrProp);
        }

        [TestMethod]
        public void Copy_CopiesProperties()
        {
            var src = new TestClass { IntProp = 5, StrProp = "x" };
            var dest = new TestClass();
            Obj.Copy(src, dest);
            Assert.AreEqual(5, dest.IntProp);
            Assert.AreEqual("x", dest.StrProp);
        }

        [TestMethod]
        public void Merge_OnlyNulls()
        {
            var src = new TestClass { IntProp = 5, StrProp = "x" };
            var dest = new TestClass { IntProp = 0, StrProp = null };
            Obj.Merge(src, dest);
            Assert.AreEqual(0, dest.IntProp);
            Assert.AreEqual("x", dest.StrProp);
        }

        [TestMethod]
        public void Call_InvokesMethod()
        {
            var obj = new TestClass();
            var result = Obj.Call(obj, "Method1");
            Assert.AreEqual(42, result);
            Assert.AreEqual(1, obj.MethodCalled);
        }

        [TestMethod]
        public void CastT_ReturnsCastedValue()
        {
            object value = "123";
            int result = Obj.Cast<int>(value);
            Assert.AreEqual(123, result);
        }

        [TestMethod]
        public void TryCast_ReturnsDefaultOnError()
        {
            var result = Obj.TryCast<int>("notanint", 99);
            Assert.AreEqual(99, result);
        }

        [TestMethod]
        public void GetValues_ReturnsAllPropertyValues()
        {
            var obj = new TestClass { IntProp = 1, StrProp = "a" };
            var values = Obj.GetValues(obj, "IntProp", "StrProp");
            CollectionAssert.AreEqual(new object[] { 1, "a" }, values);
        }

        [TestMethod]
        public void GetValuesT_ReturnsTypedValues()
        {
            var obj = new TestClass { IntProp = 1, StrProp = "a" };
            var values = Obj.GetValues<int>(obj, "IntProp");
            CollectionAssert.AreEqual(new[] { 1 }, values);
        }

        [TestMethod]
        public void GetMembersValues_ReturnsDictionary()
        {
            var obj = new TestClass { IntProp = 1, StrProp = "a" };
            var dict = Obj.GetMembersValues(obj, m => m.Name == "IntProp" || m.Name == "StrProp");
            Assert.AreEqual(2, dict.Count);
            Assert.AreEqual(1, dict["IntProp"]);
            Assert.AreEqual("a", dict["StrProp"]);
        }

        [TestMethod]
        public void GetAttribute_ReturnsNullIfNoAttribute()
        {
            var obj = new TestClass();
            var attr = Obj.GetAttribute<ObsoleteAttribute>(obj);
            Assert.IsNull(attr);
        }

        [TestMethod]
        public void MethodExists_ReturnsTrueIfExists()
        {
            bool exists = Obj.MethodExists(typeof(TestClass), "Method1");
            Assert.IsTrue(exists);
        }

        [TestMethod]
        public void GetMethodInfo_ReturnsMemberInfoEx()
        {
            var mi = Obj.GetMethodInfo(typeof(TestClass), "Method1");
            Assert.IsNotNull(mi);
            Assert.AreEqual("Method1", mi.Name);
        }

        [TestMethod]
        public void New_CreatesInstance()
        {
            var obj = Obj.New(typeof(TestClass));
            Assert.IsInstanceOfType(obj, typeof(TestClass));
        }

        [TestMethod]
        public void NewT_CreatesInstance()
        {
            var obj = Obj.New<TestClass>();
            Assert.IsInstanceOfType(obj, typeof(TestClass));
        }

        [TestMethod]
        public void Set_PerformanceTest()
        {
            var obj = new TestClass();
            const int iterations = 100_000;
            var sw = Stopwatch.StartNew();

            for (int i = 0; i < iterations; i++)
            {
                Obj.Set(obj, [nameof(TestClass.IntProp)], i);
                Obj.Set(obj, [nameof(TestClass.DoubleProp)], i);
            }

            sw.Stop();
            double avgMicroseconds = (sw.Elapsed.TotalMilliseconds * 1000) / iterations;
            Debug.WriteLine($"Obj.Set average time: {avgMicroseconds:F2} μs per call");
            // Assert that average time is within a reasonable threshold (adjust as needed)
            //122-127ms
            //82-89 только MemberInfoEx.SetValue + GetType()
            //100-105 MemberInfoEx.SetValue + GetType() + проверка на ChangeType
        }

        [TestMethod]
        public void FastSet_PerformanceTest()
        {
            var obj = new TestClass();
            const int iterations = 100_000;
            var sw = Stopwatch.StartNew();

            for (int i = 0; i < iterations; i++)
            {
                Obj.FastSet(obj, nameof(TestClass.IntProp), i);
                Obj.FastSet(obj, nameof(TestClass.DoubleProp), (double)i);
                //Obj.FastSet(obj, nameof(TestClass.IntProp), i);
                //Obj.FastSet(obj, nameof(TestClass.DoubleProp), (double)i);
                //Obj.FastSet(obj, nameof(TestClass.IntProp), i);
                //Obj.FastSet(obj, nameof(TestClass.DoubleProp), (double)i);
                //Obj.FastSet(obj, nameof(TestClass.IntProp), i);
                //Obj.FastSet(obj, nameof(TestClass.DoubleProp), (double)i);
                //Obj.FastSet(obj, nameof(TestClass.IntProp), i);
                //Obj.FastSet(obj, nameof(TestClass.DoubleProp), (double)i);
            }

            sw.Stop();
            double avgMicroseconds = (sw.Elapsed.TotalMilliseconds * 1000) / iterations;
            Debug.WriteLine($"Obj.Set average time: {avgMicroseconds:F2} μs per call");
            // Assert that average time is within a reasonable threshold (adjust as needed)
            //122-127ms
            //82-89 только MemberInfoEx.SetValue + GetType()
            //100-105 MemberInfoEx.SetValue + GetType() + проверка на ChangeType
        }

        [TestMethod]
        public void Native_PerformanceTest()
        {
            var obj = new TestClass();
            const int iterations = 100_000;
            var sw = Stopwatch.StartNew();

            for (int i = 0; i < iterations; i++)
            {
                obj.IntProp = i;
                obj.DoubleProp = (double)i;
                obj.IntProp = i;
                obj.DoubleProp = (double)i;
                obj.IntProp = i;
                obj.DoubleProp = (double)i;
                obj.IntProp = i;
                obj.DoubleProp = (double)i;
                obj.IntProp = i;
                obj.DoubleProp = (double)i;
            }

            sw.Stop();
            double avgMicroseconds = (sw.Elapsed.TotalMilliseconds * 1000) / iterations;
            Debug.WriteLine($"Obj.Set average time: {avgMicroseconds:F2} μs per call");
            // Assert that average time is within a reasonable threshold (adjust as needed)
            //122-127ms
            //82-89 только MemberInfoEx.SetValue + GetType()
            //100-105 MemberInfoEx.SetValue + GetType() + проверка на ChangeType
        }

        [TestMethod]
        public void Set_BackFieldTest()
        {
            var x = new TestClass();

            Obj.FastSet(x, "_backField", 123);
            Assert.AreEqual(123, x.BackFieldProp);
        }

        [TestMethod]
        public void GetMember_Property_ReturnsMemberInfoEx()
        {
            var mi = Obj.GetMember(typeof(TestClass), "IntProp");
            Assert.IsNotNull(mi);
            Assert.IsTrue(mi.IsProperty);
            Assert.AreEqual("IntProp", mi.Name);
            Assert.AreEqual(typeof(int), mi.Type);
        }

        [TestMethod]
        public void GetMember_Field_ReturnsMemberInfoEx()
        {
            var mi = Obj.GetMember(typeof(TestClass), "strprop");
            Assert.IsNotNull(mi);
            Assert.IsTrue(mi.IsProperty);
            Assert.AreEqual("StrProp", mi.Name);
            Assert.AreEqual(typeof(string), mi.Type);
        }

        [TestMethod]
        public void GetMember_Method_ReturnsMemberInfoEx()
        {
            var mi = Obj.GetMember(typeof(TestClass), "Method1");
            Assert.IsNotNull(mi);
            Assert.IsTrue(mi.IsMethod);
            Assert.AreEqual("Method1", mi.Name);
        }

        [TestMethod]
        public void GetMember_NestedProperty_ReturnsMemberInfoEx()
        {
            var mi = Obj.GetMember(typeof(TestClass), "Sub.Name");
            Assert.IsNotNull(mi);
            Assert.IsTrue(mi.IsProperty);
            Assert.AreEqual("Name", mi.Name);
            Assert.AreEqual(typeof(string), mi.Type);
        }

        [TestMethod]
        public void GetMember_CollectionElementType_ReturnsMemberInfoEx()
        {
            var mi = Obj.GetMember(typeof(TestClass), "SubList.Name");
            Assert.IsNotNull(mi);
            Assert.IsTrue(mi.IsProperty);
            Assert.AreEqual("Name", mi.Name);
            Assert.AreEqual(typeof(string), mi.Type);
        }

        [TestMethod]
        public void GetMember_NonExistentMember_ReturnsNull()
        {
            var mi = Obj.GetMember(typeof(TestClass), "NoSuchMember");
            Assert.IsNull(mi);
        }

        [TestMethod]
        public void GetMember_NullType_ThrowsArgumentNullException()
        {
            Assert.ThrowsException<ArgumentNullException>(() => Obj.GetMember(null, "Any"));
        }

        [TestMethod]
        public void GetMember_EmptyMemberName_ReturnsTypeInfo()
        {
            var mi = Obj.GetMember(typeof(TestClass), "");
            Assert.IsNotNull(mi);
            Assert.IsTrue(mi.IsType);
            Assert.AreEqual(typeof(TestClass), mi.Type);
        }
    }
}