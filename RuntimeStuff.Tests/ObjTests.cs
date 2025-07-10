using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RuntimeStuff;

namespace RuntimeStuff.MSTests
{
    [TestClass]
    public class ObjTests
    {
        public class TestClass
        {
            public string Name { get; set; }
            public int Age { get; set; }
            public double Score { get; set; }
        }

        [TestMethod]
        public void ChangeType_ShouldConvertToInt()
        {
            object input = "123";
            int result = Obj.ChangeType<int>(input);
            Assert.AreEqual(123, result);
        }

        [TestMethod]
        public void TryChangeType_ShouldHandleValidAndInvalid()
        {
            Assert.IsTrue(Obj.TryChangeType<int>("42", out var num));
            Assert.AreEqual(42, num);

            Assert.IsFalse(Obj.TryChangeType<int>("oops", out _));
        }

        [TestMethod]
        public void Increase_ShouldAddStep()
        {
            object value = 5;
            Obj.Increase(ref value, 3);
            Assert.AreEqual((object)8, value);
        }

        [TestMethod]
        public void SetAndGet_ShouldWorkOnSimpleProperty()
        {
            var obj = new TestClass();
            Obj.Set(obj, "Name", "Sergey");
            string name = Obj.Get<string>(obj, "Name");
            Assert.AreEqual("Sergey", name);
        }

        [TestMethod]
        public void GetValues_ShouldReturnProperties()
        {
            var obj = new TestClass { Name = "A", Age = 11, Score = 9.5 };
            var values = Obj.GetValues(obj, "Name", "Age");
            CollectionAssert.AreEqual(new object[] { "A", 11 }, values);
        }

        [TestMethod]
        public void ZipUnZip_ShouldPreserveData()
        {
            var data = Encoding.UTF8.GetBytes("Hello world!");
            var zipped = Obj.Zip(data);
            var unzipped = Obj.UnZip(zipped);
            Assert.AreEqual("Hello world!", Encoding.UTF8.GetString(unzipped));
        }

        [TestMethod]
        public void Copy_ShouldCopyMatchingValues()
        {
            var src = new TestClass { Name = "Copy", Age = 55 };
            var dst = new TestClass();
            Obj.Copy(src, dst);
            Assert.AreEqual("Copy", dst.Name);
            Assert.AreEqual(55, dst.Age);
        }

        [TestMethod]
        public void Merge_ShouldPreserveNonNullTargetValues()
        {
            var src = new TestClass { Name = "Updated", Age = 22 };
            var dst = new TestClass { Name = null, Age = 100 };
            Obj.Merge(src, dst);
            Assert.AreEqual("Updated", dst.Name);
            Assert.AreEqual(100, dst.Age);
        }

        [TestMethod]
        public void Call_ShouldInvokeMethod()
        {
            var list = new List<string>();
            Obj.Call(list, "Add", "Hello");
            Assert.AreEqual("Hello", list[0]);
        }

        [TestMethod]
        public void Cast_ShouldConvertCompatibleTypes()
        {
            object input = "88.6";
            var result = Obj.Cast<double>(input);
            Assert.AreEqual(88.6, result);
        }

        [TestMethod]
        public void TryCast_ShouldReturnDefaultOnFailure()
        {
            object input = "abc";
            var result = Obj.TryCast<int>(input, -1);
            Assert.AreEqual(-1, result);
        }

        [TestMethod]
        public void New_ShouldCreateSimpleObject()
        {
            var instance = Obj.New<TestClass>();
            Assert.IsNotNull(instance);
        }

        [TestMethod]
        public void MethodExists_ShouldFindKnownMethod()
        {
            var exists = Obj.MethodExists(typeof(List<string>), "Add");
            Assert.IsTrue(exists);
        }

        [TestMethod]
        public void SetValues_ShouldSetMultipleProperties()
        {
            var obj = new TestClass();
            Obj.Set(obj, new Dictionary<string, object>
            {
                { "Name", "Bob" },
                { "Age", 35 }
            });

            Assert.AreEqual("Bob", obj.Name);
            Assert.AreEqual(35, obj.Age);
        }

        [TestMethod]
        public void CopyTo_ShouldStreamCopy()
        {
            byte[] source = Encoding.UTF8.GetBytes("stream test");
            using var src = new MemoryStream(source);
            using var dst = new MemoryStream();
            Obj.CopyTo(src, dst);
            var result = Encoding.UTF8.GetString(dst.ToArray());
            Assert.AreEqual("stream test", result);
        }
    }
}
