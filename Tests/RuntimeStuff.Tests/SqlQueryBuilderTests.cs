using RuntimeStuff.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RuntimeStuff.MSTests
{
    [TestClass]
    public class SqlQueryBuilderTests
    {
        [TestMethod]
        public void TestJoin_01()
        {
            var join = SqlQueryBuilder.GetJoinClause(typeof(DTO.SQLite.User), typeof(DTO.SQLite.UserProfile), Options.SqlProviderOptions.SqliteOptions);
            Assert.AreEqual("INNER JOIN \"user_profiles\" ON \"user_profiles\".\"UserId\" = \"users\".\"UserId\"", join);
        }

        [TestMethod]
        public void TestJoin_02()
        {
            var join = SqlQueryBuilder.GetJoinClause( typeof(DTO.SQLite.UserProfile), typeof(DTO.SQLite.User), Options.SqlProviderOptions.SqliteOptions);
            Assert.AreEqual("INNER JOIN \"users\" ON \"users\".\"UserId\" = \"user_profiles\".\"UserId\"", join);
        }
    }
}
