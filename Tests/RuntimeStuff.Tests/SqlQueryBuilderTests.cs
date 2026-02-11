using RuntimeStuff.Helpers;

namespace RuntimeStuff.MSTests
{
    [TestClass]
    public class SqlQueryBuilderTests
    {
        [TestMethod]
        public void TestJoin_01()
        {
            var join = SqlQueryHelper.GetJoinClause(typeof(DTO.SQLite.User), typeof(DTO.SQLite.UserProfile), Options.SqlProviderOptions.SqliteOptions);
            Assert.AreEqual("INNER JOIN \"user_profiles\" ON \"user_profiles\".\"user_id\" = \"users\".\"user_id\"", join);
        }

        [TestMethod]
        public void TestJoin_02()
        {
            var join = SqlQueryHelper.GetJoinClause(typeof(DTO.SQLite.UserProfile), typeof(DTO.SQLite.User), Options.SqlProviderOptions.SqliteOptions);
            Assert.AreEqual("INNER JOIN \"users\" ON \"users\".\"user_id\" = \"user_profiles\".\"user_id\"", join);
        }
    }
}