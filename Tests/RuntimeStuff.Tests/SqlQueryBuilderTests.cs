#if DEBUG
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.MSTests.DTO.SQLite;
using System.Text;
using System.Threading.Tasks;

namespace RuntimeStuff.MSTests
{
    [TestClass]
    public class SqlQueryBuilderTests
    {
        [TestMethod]
        public void Select_Test_01()
        {
            var sqb = new SqlQueryBuilder();
            sqb.UseFullNames = true;
            sqb.Select<User>(x => x.Name, x => x.Id);
            var query = sqb.ToString();
            Assert.AreEqual("SELECT [users].[Name], [users].[Id] FROM [users]", query);
        }

        [TestMethod]
        public void Select_Test_02()
        {
            var sqb = new SqlQueryBuilder();
            sqb.UseFullNames = false;
            sqb.Select<User>(x => x.Name, x => x.Id);
            var query = sqb.ToString();
            Assert.AreEqual("SELECT [Name], [Id] FROM [users]", query);
        }

        [TestMethod]
        public void Select_Test_03()
        {
            var sqb = new SqlQueryBuilder();
            sqb.UseFullNames = true;
            sqb.Select<User>(x => x.Name, x => x.Id);
            sqb.Select<UserProfile>(x => x.UserId, x => x.Bio);
            var query = sqb.ToString();
            Assert.AreEqual("SELECT [users].[Name], [users].[Id], [user_profiles].[user_id], [user_profiles].[bio] FROM [users]", query);
        }

        [TestMethod]
        public void Select_Test_04()
        {
            var sqb = new SqlQueryBuilder();
            sqb.UseFullNames = false;
            sqb.Select<User>(x => x.Name, x => x.Id);
            sqb.Where<User>(x => x.Id, SqlQueryBuilder.SqlOperator.GreaterOrEqual, 666);
            sqb.Where<User>(x => x.Name, SqlQueryBuilder.SqlOperator.Equal, "name");
            var query = sqb.ToString();
            Assert.AreEqual("SELECT [Name], [Id] FROM [users]", query);
        }

        [TestMethod]
        public void Select_Test_05()
        {
            var sqb = new SqlQueryBuilder();
            sqb.UseFullNames = true;
            sqb.Select<UserLogs>(x => x.Created, x => x.UserId);
            var query = sqb.ToString();
            Assert.AreEqual("SELECT [users].[Name], [users].[Id] FROM [users]", query);
        }

    }
}
#endif