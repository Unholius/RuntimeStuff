using System.Collections;
using System.Text;
using RuntimeStuff.Extensions;

namespace RuntimeStuff.MSTests.Extensions
{
    [TestClass]
    public class RSLinqExtensionsTests
    {
        class Sample
        {
            public Sample() { }
            public Sample(int id, string name)
            {
                Id = id;
                Name = name;
            }

            public int Id { get; set; }
            public string Name { get; set; } = "";
            public override string ToString() => $"{Id}:{Name}";
        }

        [TestMethod]
        public void ToArray_WithConvertTypeFalse_ReturnsTypedArray()
        {
            IEnumerable list = new List<int> { 1, 2, 3 };
            var arr = list.ToArray<int>(false);
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, arr);
        }

        [TestMethod]
        public void ToArray_WithConvertTypeTrue_ConvertsTypes()
        {
            IEnumerable list = new List<string> { "1", "2" };
            var arr = list.ToArray<int>(true);
            CollectionAssert.AreEqual(new[] { 1, 2 }, arr);
        }

        [TestMethod]
        public void Any_ReturnsTrueIfNotEmpty()
        {
            IEnumerable list = new List<int> { 1 };
            Assert.IsTrue(list.Any());
        }

        [TestMethod]
        public void Any_ReturnsFalseIfNullOrEmpty()
        {
            IEnumerable? list = null;
            Assert.IsFalse(list.Any());
            list = new List<int>();
            Assert.IsFalse(list.Any());
        }

        [TestMethod]
        public void Select_EnumeratesAll()
        {
            IEnumerable list = new List<int> { 1, 2 };
            var result = list.Select().ToList();
            CollectionAssert.AreEqual(new object[] { 1, 2 }, result);
        }

        [TestMethod]
        public void GetString_UsesUTF8AndCleansBOM()
        {
            var bytes = Encoding.UTF8.GetBytes("\ufeffabc\u200B﻿");
            var str = bytes.GetString();
            Assert.AreEqual("abc", str);
        }

        [TestMethod]
        public void AddRange_AddsAllPairs()
        {
            var d = new Dictionary<string, int>();
            d.AddRange([new KeyValuePair<string, int>("a", 1), new KeyValuePair<string, int>("b", 2)]);
            Assert.AreEqual(2, d.Count);
            Assert.AreEqual(1, d["a"]);
            Assert.AreEqual(2, d["b"]);
        }

        [TestMethod]
        public void All_WithIndexPredicate()
        {
            var arr = new[] { 2, 4, 6 };
            Assert.IsTrue(arr.All((x, _) => x % 2 == 0));
            Assert.IsFalse(arr.All((x, _) => x == 2));
        }

        [TestMethod]
        public void Cast_ChangesType()
        {
            IEnumerable list = new List<string> { "1", "2" };
            var result = list.Cast(typeof(int)).ToList();
            CollectionAssert.AreEqual(new object[] { 1, 2 }, result);
        }

        [TestMethod]
        public void CountItems_WorksForVariousTypes()
        {
            Assert.AreEqual(3, new[] { 1, 2, 3 }.CountItems());
            Assert.AreEqual(2, new List<int> { 1, 2 }.CountItems());
            Assert.AreEqual(0, ((IEnumerable?)null).CountItems());
        }

        [TestMethod]
        public void DistinctBy_ReturnsDistinct()
        {
            var list = new[] { new Sample { Id = 1, Name = "A" }, new Sample { Id = 1, Name = "B" }, new Sample { Id = 2, Name = "C" } };
            var result = list.DistinctBy(x => x.Id).ToList();
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("A", result[0].Name);
        }

        [TestMethod]
        public void GetElementAt_ReturnsCorrectElement()
        {
            IEnumerable list = new[] { "a", "b", "c" };
            Assert.AreEqual("b", list.GetElementAt(1));
        }

        [TestMethod]
        public void IndexOf_FindsIndex()
        {
            var arr = new[] { 1, 2, 3 };
            Assert.AreEqual(1, arr.IndexOf(x => x == 2));
            Assert.AreEqual(-1, arr.IndexOf(x => x == 99));
        }

        [TestMethod]
        public void IndexOf_WithFuncIndex()
        {
            var arr = new[] { 1, 2, 3 };
            Assert.AreEqual(2, arr.IndexOf((_, i) => i == 2));
        }

        [TestMethod]
        public void IndexOf_EnumerableObject()
        {
            IEnumerable list = new[] { "a", "b", "c" };
            Assert.AreEqual(1, list.IndexOf("b"));
        }

        [TestMethod]
        public void Merge_AddsMissingItems()
        {
            var l1 = new List<int> { 1, 2 };
            var l2 = new List<int> { 2, 3, 4 };
            l1.UnionWith(l2);
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3, 4 }, l1);
        }

        [TestMethod]
        public void Move_MovesElement()
        {
            var list = new List<int> { 1, 2, 3 };
            list.Move(0, 2);
            CollectionAssert.AreEqual(new[] { 2, 3, 1 }, list);
        }

        [TestMethod]
        public void OfType_WithPredicate()
        {
            var list = new object[] { 1, "a", 2, "b" };
            var result = list.OfType<int>().Where(x => x > 1).ToList();
            CollectionAssert.AreEqual(new[] { 2 }, result);
        }

        [TestMethod]
        public void ToArray_Enumerable()
        {
            IEnumerable list = new List<string> { "1", "2" };
            var arr = list.ToArray<int>();
            CollectionAssert.AreEqual(new[] { 1, 2 }, arr);
        }

        [TestMethod]
        public void CreateAndAddItem_AddsNewItem()
        {
            var list = new List<Sample>();
            var obj = ((IList)list).CreateAndAddItem(5, "X");
            Assert.AreEqual(1, list.Count);
            Assert.IsInstanceOfType(obj, typeof(Sample));
        }

        [TestMethod]
        public void CreateItem_CreatesNewItem()
        {
            var list = new List<Sample>();
            var obj = ((IList)list).CreateItem(7, "Y");
            Assert.IsInstanceOfType(obj, typeof(Sample));
        }

        [TestMethod]
        public void CreateItemT_CreatesNewItem()
        {
            var list = new List<Sample>();
            var obj = list.CreateItem();
            Assert.IsInstanceOfType(obj, typeof(Sample));
        }

        [TestMethod]
        public void CreateAndAddItemT_AddsNewItem()
        {
            var list = new List<Sample>();
            var obj = list.CreateAndAddItem(); // Explicitly specify the generic method
            Assert.AreEqual(1, list.Count);
            Assert.IsInstanceOfType(obj, typeof(Sample));
        }

        [TestMethod]
        public void ToCsv_Basic()
        {
            var list = new[] { 1, 2, 3 };
            var csv = list.ToCsv();
            Assert.IsTrue(csv.Contains("1"));
        }

        [TestMethod]
        public void ToDataTable_ConvertsList()
        {
            var list = new[] { new Sample { Id = 1, Name = "A" }, new Sample { Id = 2, Name = "B" } };
            var dt = list.ToDataTable();
            Assert.AreEqual(2, dt.Rows.Count);
            Assert.AreEqual(1, dt.Rows[0]["Id"]);
        }

        [TestMethod]
        public void ToList_Enumerable()
        {
            IEnumerable list = new List<string> { "1", "2" };
            var l = list.ToList<int>();
            CollectionAssert.AreEqual(new[] { 1, 2 }, l);
        }

        [TestMethod]
        public void TryCast_OnlyValid()
        {
            IEnumerable list = new List<string> { "1", "a", "2" };
            var result = list.TryCast(typeof(int)).ToList();
            CollectionAssert.AreEqual(new object[] { 1, 2 }, result);
        }

        [TestMethod]
        public void Between_ReturnsTrueIfBetween()
        {
            Assert.IsTrue(5.Between(1, 10));
            Assert.IsFalse(0.Between(1, 10));
        }

        [TestMethod]
        public void Coalesce_ReturnsFirstNotNull()
        {
            const string? a = null, b = "b", c = null;
            Assert.AreEqual("b", a.Coalesce(b, c));
        }

        [TestMethod]
        public void CoalesceThrow_ThrowsIfAllNull()
        {
            object? a = null, b = null;
            Assert.ThrowsException<NullReferenceException>(() => a.CoalesceThrow(b));
        }

        [TestMethod]
        public void ForEach_ExecutesAction()
        {
            var list = new[] { 1, 2, 3 };
            int sum = 0;
            list.ForEach(x => sum += x);
            Assert.AreEqual(6, sum);
        }

        [TestMethod]
        public void ForEach_WithIndex()
        {
            var list = new[] { "a", "b" };
            var sb = new StringBuilder();
            list.ForEach((x, i) => sb.Append($"{i}:{x};"));
            Assert.AreEqual("0:a;1:b;", sb.ToString());
        }

        [TestMethod]
        public void Greater_Less_OrEqual()
        {
            Assert.IsTrue(5.Greater(3));
            Assert.IsTrue(5.GreaterOrEqual(5));
            Assert.IsTrue(3.Less(5));
            Assert.IsTrue(3.LessOrEqual(3));
        }

        [TestMethod]
        public void If_ReturnsThenOrElse()
        {
            const int x = 5;
            var result = x.If(v => v > 3, "big", "small");
            Assert.AreEqual("big", result);
        }

        [TestMethod]
        public void In_NotIn()
        {
            Assert.IsTrue(2.In(1, 2, 3));
            Assert.IsFalse(4.In(1, 2, 3));
            Assert.IsTrue(2.NotIn(1, 3));
        }

        [TestMethod]
        public void IsNull_IsNotNull()
        {
            const string? s = null;
            Assert.IsTrue(s.IsNull());
            Assert.IsFalse("x".IsNull());
            Assert.IsTrue("x".IsNotNull());
        }

        [TestMethod]
        public void IsNumber_ParsesNumbers()
        {
            Assert.IsTrue("123".IsNumber(out object number));
            Assert.AreEqual(123m, number);
            Assert.IsFalse("abc".IsNumber(out _));
        }

        [TestMethod]
        public void NotBetween()
        {
            Assert.IsTrue(0.NotBetween(1, 10));
            Assert.IsFalse(5.NotBetween(1, 10));
        }

        [TestMethod]
        public void WithWhen_Action()
        {
            int x = 0;
            x.WithWhen(
                ( () => false, () => x = 1 ),
                ( () => true, () => x = 2 )
            );
            Assert.AreEqual(2, x);
        }

        [TestMethod]
        public void WithWhen_ActionT()
        {
            int x = 0;
            x.WithWhen(
                ( () => false, _ => x = 1 ),
                ( () => true, _ => x = 2 )
            );
            Assert.AreEqual(2, x);
        }

        [TestMethod]
        public void WithWhen_DefaultAction()
        {
            int x = 0;
            x.WithWhen(
                _ => x = 3,
                ( () => false, _ => x = 1 )
            );
            Assert.AreEqual(3, x);
        }

        [TestMethod]
        public void WithWhen_Single()
        {
            int x = 0;
            x.WithWhen(() => true, _ => x = 5);
            Assert.AreEqual(5, x);
        }

        [TestMethod]
        public void Case_ValueCases()
        {
            const int v = 2;
            var result = v.Case("none", (1, "one"), (2, "two"));
            Assert.AreEqual("two", result);
        }

        [TestMethod]
        public void Case_ValueCases_WithDefault()
        {
            const int v = 3;
            var result = v.Case("default", (1, "one"), (2, "two"));
            Assert.AreEqual("default", result);
        }

        [TestMethod]
        public void Case_AlternatingParams()
        {
            const int v = 2;
            var result = v.Case("default", 1, "one", 2, "two");
            Assert.AreEqual("two", result);
        }

        [TestMethod]
        public void Case_FuncCases()
        {
            const int v = 5;
            var result = v.Case("none", (x => x > 3, "big"), (x => x < 3, "small"));
            Assert.AreEqual("big", result);
        }
    }
}