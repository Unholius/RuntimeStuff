using RuntimeStuff.Helpers;
using RuntimeStuff.MSTests.Models;
using RuntimeStuff.Options;

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

        [TestMethod]
        public void WhereClause_Test_01()
        {
            var whereClause =
                SqlQueryHelper.GetWhereClause<TestClassWithBasicProperties>(x => x.Double >= 3.14 && x.Str == "name" || x.Int32 == x.Int32, SqlProviderOptions.SqlServerOptions, false, out _);

            Assert.AreEqual("WHERE (((\"Double\" >= 3.14) AND (\"Str\" = 'name')) OR (\"Int32\" = \"Int32\"))", whereClause);
        }

        [TestMethod]
        public void WhereClause_Test_02()
        {
            var whereClause =
                SqlQueryHelper.GetWhereClause<TestClassWithBasicProperties>(x => x.Double >= 3.14 && x.Str == "name" || x.Int32 == x.Int32, SqlProviderOptions.SqlServerOptions, true, out var p);

            Assert.AreEqual("WHERE (((\"Double\" >= @Double_1) AND (\"Str\" = @Str_2)) OR (\"Int32\" = \"Int32\"))", whereClause);
            Assert.AreEqual(2, p.Count);
            Assert.AreEqual("Double_1", p.Keys.ElementAt(0));
            Assert.AreEqual("Str_2", p.Keys.ElementAt(1));
            Assert.AreEqual(3.14, p["Double_1"]);
            Assert.AreEqual("name", p["Str_2"]);
        }

        [TestMethod]
        public void WhereClause_Test_03()
        {
            var id = 3;
            var name = "name";
            var whereClause =
                SqlQueryHelper.GetWhereClause<MemberCacheTests.TestClass>(x => x.Id == id && x.Name == name, SqlProviderOptions.SqlServerOptions, false, out _);

            Assert.AreEqual("WHERE ((\"Name\" = 3) AND (\"EventId\" = 'name'))", whereClause);
        }
    }
}