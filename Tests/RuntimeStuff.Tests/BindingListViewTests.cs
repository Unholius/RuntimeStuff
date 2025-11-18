using RuntimeStuff.MSTests.Models;

namespace RuntimeStuff.MSTests
{
    [TestClass]
    public class BindingListViewTests
    {
        [TestMethod]
        public void Test1()
        {
            var arr = new[] { new TestClassWithBasicProperties(1), new TestClassWithBasicProperties(2), new TestClassWithBasicProperties(3) };
            var blv = new BindingListView<TestClassWithBasicProperties>(arr);
            Assert.AreEqual(3, blv.Count);
            Assert.AreEqual(3, blv.TotalCount);
        }

        [TestMethod]
        public void Test2()
        {
            var arr = new[] { new TestClassWithBasicProperties(1), new TestClassWithBasicProperties(2), new TestClassWithBasicProperties(3) };
            var blv = new BindingListView<TestClassWithBasicProperties>(arr);
            Assert.AreEqual(3, blv.Count);
            Assert.AreEqual(3, blv.TotalCount);
        }
        [TestMethod]
        public void Test3()
        {
            var arr = new[] { new TestClassWithBasicProperties(1), new TestClassWithBasicProperties(2), new TestClassWithBasicProperties(3) };
            var blv = new BindingListView<TestClassWithBasicProperties>();
            blv.AddRange(arr);
            Assert.AreEqual(3, blv.Count);
            Assert.AreEqual(3, blv.TotalCount);
        }

        [TestMethod]
        public void Test4()
        {
            var arr = new List<TestClassWithBasicProperties>() { new(1), new(2), new(3) };
            var blv = new BindingListView<TestClassWithBasicProperties>();
            blv.AddRange(arr);
            var count = 100_000;
            blv.Clear();
            for (int i = 0; i < count; i++)
                arr.Add(new TestClassWithBasicProperties(i));
            blv.AddRange(arr);
            blv.Filter = "[Int32] > 1";
            blv.SortBy = "Int32 desc";
            //blv[0, BindingListView<TestClassWithBasicProperties>.IndexType.FilteredSorted].Visible = false;
            blv.Insert(0, new TestClassWithBasicProperties(666));
            var arr2 = blv.ToArray();
            blv.Filter = "[Int32] == 666";
            var arr3 = blv.ToArray();
        }
    }
}
