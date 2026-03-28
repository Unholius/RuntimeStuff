using System.Helpers;
using System.Linq.Expressions;
using System.MSTests.Models;

namespace System.MSTests
{
    [TestClass]
    public class FilterHelperTests
    {
        [TestMethod]
        public void Test1()
        {
            var lst = new List<TestClassWithBasicProperties>();
            for (var i = 0; i < 100; i++)
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
            fb.Add("Int32", StringFilterBuilder.Token.Like, "%2%");
            var filter = fb.ToString(); // "[Int32] like '%2%'";
            var filtered = FilterHelper.Filter(lst, filter).ToArray();
            FilterHelper.ToPredicate<TestClassWithBasicProperties>(filter);
            Assert.AreEqual(19, filtered.Length);
        }
    }
}