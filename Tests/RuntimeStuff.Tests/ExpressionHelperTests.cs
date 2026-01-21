using System.Linq.Expressions;
using RuntimeStuff.Helpers;

namespace RuntimeStuff.MSTests
{

    [TestClass]
    public class ExpressionHelperTests
    {
        [TestMethod]
        public void TestGetPropertyInfo()
        {
            Expression<Func<MemberCacheTests.TestClass, object>> propertySelector = x => x.Id;
            var propertyInfo = ExpressionHelper.GetPropertyInfo(propertySelector);
        }
    }
}
