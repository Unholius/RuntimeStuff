#if DEBUG
using RuntimeStuff.MSTests.Beta;
using RuntimeStuff.MSTests.DTO.SQLite;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RuntimeStuff.MSTests
{

    public class Entity
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public DateTime Created { get; set; }
        public object[] Photo { get; set; }
        public Guid Guid { get; set; }
        public bool IsActive { get; set; }
        public decimal Price { get; set; }
        public double Rating { get; set; }
        public float Discount { get; set; }
    }

    [TestClass]
    public class SqlQueryBuilderTests
    {
        [TestMethod]
        public void Test01()
        {
            var sqb = new SqlQueryBuilder();
            sqb
                .Select("product_id")
                .Select("product_name")
                .From("products", "p")
                .SelectMany("date", "count")
                .InnerJoin("product_details", "pd", "parent_product_id", "p", "product_id")
                .Where("pd", "date", SqlQueryBuilder.SqlOperator.Equal, DateTime.Now.Date)
                .And()
                .BeginGroup()
                .Where("pd", "count", SqlQueryBuilder.SqlOperator.Less, 0)
                .Or()
                .Where("pd", "count", SqlQueryBuilder.SqlOperator.Greater, 9999)
                .EndGroup()
                ;
        }

        //[TestMethod]
        //public void Select_Test_01()
        //{
        //    var sqb = new SqlQueryBuilder();
        //    sqb.UseFullNames = true;
        //    sqb.Select<User>(x => x.Name, x => x.Id);
        //    var query = sqb.ToString();
        //    Assert.AreEqual("SELECT [users].[Name], [users].[Id] FROM [users]", query);

        //    sqb.UseAliases = true;
        //    sqb.Select<User>(x => x.Name);
        //    query = sqb.ToString();
        //    Assert.AreEqual("SELECT [users].[Name] AS [Name], [users].[Id] AS [Id], [users].[Name] AS [Name] FROM [users]", query);
        //}

        //[TestMethod]
        //public void Select_Test_02()
        //{
        //    var sqb = new SqlQueryBuilder();
        //    sqb.UseFullNames = false;
        //    sqb.Select<User>(x => x.Name, x => x.Id);
        //    var query = sqb.ToString();
        //    Assert.AreEqual("SELECT [Name], [Id] FROM [users]", query);
        //}

        //[TestMethod]
        //public void Select_Test_03()
        //{
        //    var sqb = new SqlQueryBuilder();
        //    sqb.UseFullNames = true;
        //    sqb.Select<User>(x => x.Name, x => x.Id);
        //    sqb.Select<UserProfile>(x => x.UserId, x => x.Bio);
        //    var query = sqb.ToString();
        //    Assert.AreEqual("SELECT [users].[Name], [users].[Id], [user_profiles].[user_id], [user_profiles].[bio] FROM [users]", query);
        //}

        //[TestMethod]
        //public void Select_Test_04()
        //{
        //    var sqb = new SqlQueryBuilder();
        //    sqb.UseFullNames = true;
        //    sqb.Select<User>(x => x.Id);
        //    sqb.Select<User>(x => x.Id);
        //    sqb.Select<User>(x => x.Id);
        //    sqb.Select<User>(x => x.Id);
        //    var query = sqb.ToString();
        //    Assert.AreEqual("SELECT [users].[Id], [users].[Id], [users].[Id], [users].[Id] FROM [users]", query);
        //    sqb.UseAliases = true;
        //    query = sqb.ToString();
        //    Assert.AreEqual("SELECT [users].[Id] AS [Id], [users].[Id] AS [UsersId], [users].[Id] AS [Id2], [users].[Id] AS [Id3] FROM [users]", query);
        //}

        //[TestMethod]
        //public void Select_Test_05()
        //{
        //    var sqb = new SqlQueryBuilder();
        //    sqb.UseFullNames = true;
        //    sqb.Select<UserLogs>(x => x.Created, x => x.UserId);
        //    var query = sqb.ToString();
        //    Assert.AreEqual("SELECT [logs].[user_logs].[Created], [logs].[user_logs].[UserId] FROM [logs].[user_logs]", query);
        //}
    }
}
#endif