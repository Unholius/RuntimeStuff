using System.Helpers;
using System.Linq.Expressions;
using System.MSTests.Models;

namespace System.MSTests
{
    [TestClass]
    public class BindingListViewTests
    {
        [TestMethod]
        public void Test1()
        {
            var arr = new[] { new TestClassWithBasicProperties(1), new TestClassWithBasicProperties(2), new TestClassWithBasicProperties(3) };
            var blv = new BindingCollection<TestClassWithBasicProperties>(arr);
            Assert.AreEqual(3, blv.Count);
            //Assert.AreEqual(3, blv.TotalCount);
        }

        [TestMethod]
        public void Test2()
        {
            var arr = new[] { new TestClassWithBasicProperties(1), new TestClassWithBasicProperties(2), new TestClassWithBasicProperties(3) };
            var blv = new BindingCollection<TestClassWithBasicProperties>(arr);
            Assert.AreEqual(3, blv.Count);
            //Assert.AreEqual(3, blv.TotalCount);
        }

        [TestMethod]
        public void Test3()
        {
            var arr = new[] { new TestClassWithBasicProperties(1), new TestClassWithBasicProperties(2), new TestClassWithBasicProperties(3) };
            var blv = new BindingCollection<TestClassWithBasicProperties>();
            blv.AddRange(arr);
            Assert.AreEqual(3, blv.Count);
            //Assert.AreEqual(3, blv.TotalCount);
        }

        [TestMethod]
        public void Test4()
        {
            var arr = new List<TestClassWithBasicProperties>() { new(1), new(2), new(3) };
            var blv = new BindingCollection<TestClassWithBasicProperties>();
            blv.AddRange(arr);
            var count = 100_000;
            blv.Clear();
            var dateFrom = DateTime.Now.Date;
            var dateTo = DateTime.Now.AddDays(-30).Date;
            for (var i = 0; i < count; i++)
                arr.Add(new TestClassWithBasicProperties(i) { Date = DateTime.Now.Date.Random(dateTo) });
            blv.AddRange(arr);
            blv.Filter = "[Int32] == 1";

            Assert.AreEqual(2, blv.Count);

            blv.Insert(0, new TestClassWithBasicProperties(666));
            var arr2 = blv.ToArray();
            blv.Filter = "[Int32] == 666";
            var arr3 = blv.ToArray();
            Assert.AreEqual(2, arr3.Length);

            var fb = new StringFilterBuilder();
            fb
                .Property(nameof(TestClassWithBasicProperties.Date))
                .GreaterOrEqual(dateTo)
                .And()
                .Property(nameof(TestClassWithBasicProperties.Date))
                .LessOrEqual(dateFrom)
                ;
            var f = fb.ToString();
            blv.Filter = f;

            
        }
    }
}