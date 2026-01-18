using RuntimeStuff.Builders;
using RuntimeStuff.Helpers;
using RuntimeStuff.MSTests.Models;

namespace RuntimeStuff.MSTests
{
    [TestClass]
    public class FilterHelperTests
    {
        [TestMethod]
        public void Test1()
        {
            var lst = new List<TestClassWithBasicProperties>();
            for (int i = 0; i < 100; i++)
            {
                lst.Add(new TestClassWithBasicProperties()
                {
                    Int32 = i,
                    Str = "Str" + i,
                    Bool = i % 2 == 0,
                    Double = i + 0.5
                });
            }

            var fb = new StringFilterBuilder();
            //fb.Property("Int32").Like("%2%");
            fb.Add("Int32", StringFilterBuilder.Operation.Like, "%2%");
            var filter = fb.ToString(); // "[Int32] like '%2%'";
            var filtered = FilterHelper.Filter(lst, filter).ToArray();
            FilterHelper.ToPredicate<TestClassWithBasicProperties>(filter);
            Assert.AreEqual(19, filtered.Length);
        }
    }
}