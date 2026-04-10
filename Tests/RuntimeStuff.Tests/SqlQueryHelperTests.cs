using RuntimeStuff.MSTests.Models;
using System.Data;

namespace RuntimeStuff.MSTests
{
    [TestClass]
    public class SqlQueryHelperTests
    {
        //[TestMethod]
        //public void TestSelect_01()
        //{
        //    var query = SqlQueryHelper.GetSelectQuery<DTO.SQLite.UserLogs>(SqlDialect.SqliteDialect, false);
        //    Assert.AreEqual("SELECT \"Created\", \"UserId\", \"Message\" FROM \"logs\".\"user_logs\"", query);
        //}

        [TestMethod]
        public void TestSelect_02()
        {
            var query = SqlQueryHelper.GetSelectQuery<DTO.SQLite.UserLogs>(SqlDialect.SqliteDialect, true);
        }

        [TestMethod]
        public void TestJoin_03()
        {
            var join = SqlQueryHelper.GetJoinClause<DTO.SQLite.UserProfile, DTO.SQLite.User>(SqlDialect.SqliteDialect, x => x.UserId, x => x.Id);
            Assert.AreEqual("INNER JOIN \"users\" ON \"users\".\"Id\" = \"user_profiles\".\"user_id\"", join);
        }

        [TestMethod]
        public void WhereClause_Test_01()
        {
            var whereClause =
                SqlQueryHelper.GetWhereClause<TestClassWithBasicProperties>(SqlDialect.SqlServerDialect, x => x.Double >= 3.14 && x.Str == "name" || x.Int32 == x.Int32, false, out _);

            Assert.AreEqual("WHERE ((([Double] >= 3.14) AND ([Str] = 'name')) OR ([Int32] = [Int32]))", whereClause);
        }

        [TestMethod]
        public void WhereClause_Test_04()
        {
            var s = "A01-22006";
            var whereClause =
                SqlQueryHelper.GetWhereClause<TestClassWithBasicProperties>(SqlDialect.SqlServerDialect, x => x.Str == s, true, out var p);

            Assert.AreEqual("WHERE ([Str] = @Str_1)", whereClause);
            Assert.AreEqual(1, p.Count);
            Assert.AreEqual("Str_1", p.Keys.ElementAt(0));
        }

        [TestMethod]
        public void WhereClause_Test_02()
        {
            var whereClause =
                SqlQueryHelper.GetWhereClause<TestClassWithBasicProperties>(SqlDialect.SqlServerDialect, x => x.Double >= 3.14 && x.Str == "name" || x.Int32 == x.Int32, true, out var p);

            Assert.AreEqual("WHERE ((([Double] >= @Double_1) AND ([Str] = @Str_2)) OR ([Int32] = [Int32]))", whereClause);
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
                SqlQueryHelper.GetWhereClause<MemberCacheTests.TestClass>(SqlDialect.SqlServerDialect, x => x.Id == id && x.Name == name, false, out _);

            Assert.AreEqual("WHERE (([Name] = 3) AND ([EventId] = 'name'))", whereClause);
        }

        [TestMethod]
        public void WhereClause_Test_05()
        {
            var whereClause =
                SqlQueryHelper.GetWhereClause<MemberCacheTests.TestClass>(SqlDialect.SqlServerDialect, x => x.Id.In(1, 2, 3), false, out _);

            Assert.AreEqual("WHERE [Name] IN (1, 2, 3)", whereClause);
        }

        [TestMethod]
        public void WhereClause_Test_06()
        {
            var ids = new[] { 1, 2, 3 };
            var whereClause =
                SqlQueryHelper.GetWhereClause<MemberCacheTests.TestClass>(SqlDialect.SqlServerDialect, x => x.Id.In(ids), false, out _);

            Assert.AreEqual("WHERE [Name] IN (1, 2, 3)", whereClause);
        }
    }
}