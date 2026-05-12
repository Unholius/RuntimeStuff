//using Microsoft.Data.Sqlite;
//using System.Data.SqlClient;
//using RuntimeStuff.Extensions;
//using System.Data;
//using RuntimeStuff.MSTests.Models;
//using RuntimeStuff.Data;

//namespace RuntimeStuff.MSTests
//{
//    [TestClass]
//    public partial class DbHelperIntegrationTests
//    {
//        private static DbEntityMap? map;
//        private static string? _connectionString;

//        [TestInitialize]
//        public void Init()
//        {
//            _connectionString = $"Data Source={Path.GetTempFileName()}.db";
//            CreateTestTables(_connectionString);
//            DbContext.DefaultConnection = () => new SqliteConnection(_connectionString);
//            DbContext.GlobalMap = map;
//        }

//        [ClassInitialize]
//        public static void ClassInitialize(TestContext _)
//        {
//            // Получаем строку подключения из конфигурации тестов
//            //_connectionString = "Data Source=.\\Databases\\sqlte_test.db";
//            map = new DbEntityMap();
//            map
//                .MapToSnakeCase<DTO.SQLite.TestTable>()
//                ;
//        }

//        [TestMethod]
//        public void Dumb_Test()
//        {
//        }

//        //[TestMethod]
//        //public void Fill_Test_01()
//        //{
//        //    var i = new DbEntity<TestTable>() { TextValue = "123" };
//        //    DbEntity<TestTable>.Map = map;
//        //    var db = DbClient.Get<SqliteConnection>(_connectionString, map);
//        //    var id = db.Insert(i, x => x.TextValue);
//        //    i = new TestTable();
//        //    i.Load(id);
//        //    Assert.AreEqual("123", i.TextValue);
//        //    Assert.AreEqual(id, i.Id);
//        //    Assert.IsTrue(db.Connection.State == ConnectionState.Closed);
//        //    i.TextValue = "456";
//        //    i.Save();
//        //    var j = db.First<TestTable>(x => x.Id == (long)id);
//        //    Assert.AreEqual("456", j.TextValue);
//        //}

//        // Вспомогательные методы
//        private static void CreateTestTables(string cs)
//        {
//            using var db = DbClient.Get<SqliteConnection>(cs);


//        }





//        private readonly string ServerName = "NAS\\RSSQLSERVER";
//        private readonly string DatabaseName = "test";

//        //[TestMethod]
//        public void DbClient_Test_05()
//        {
//            var con = new SqlConnection()
//                .Server(ServerName)
//                .Database(DatabaseName)
//                .Timeout(2)
//                .IntegratedSecurity(true);

//            if (!con.TryOpen())
//                return;

//            var list = new List<string>
//            {
//                "1",
//                "2",
//                "3"
//            };
//            var dt = new DataTable("dbo.StrList");
//            dt.AddCol("ID");
//            dt.AddRow("1")
//                .AddRow("2")
//                .AddRow("3");
//            var result = con.ToList<string>("select * from dbo.TestFunction(@list)", new { list = dt });
//        }

//        //[TestMethod]
//        public void DbClient_Test_07()
//        {
//            var con = new SqlConnection()
//                .Server("serv40")
//                .Database("Tamuz")
//                .Timeout(2)
//                .IntegratedSecurity(true);

//            if (!con.TryOpen())
//                return;

//            var list = new List<BadCodeGoodCodeUpdateData>()
//            {
//                new()
//                {
//                    BadCode = "bad1", GoodCode = "good1"
//                },
//                new()
//                {
//                    BadCode = "bad2", GoodCode = "good2"
//                },
//            };

//            var dt = list.ToDataTable("dbo.Tuple2",
//                (x => x.GoodCode, "Item1"),
//                (x => x.BadCode, "Item2")
//                );

//            //var dt = new DataTable("dbo.Tuple2");
//            //dt.AddCol("Item1");
//            //dt.AddCol("Item2");
//            //dt
//            //    .AddRow("1", "2");
//            var result = con.ToList<BadCodeGoodCodeUpdateData>("select * from dbo.GetTableBCGC(@companyId, @bgCodes)", new { companyId = 1, bgCodes = dt });
//        }

//        //[TestMethod]
//        public void DbClient_Tamuz_Test_01()
//        {
//            var con = new SqlConnection()
//                    .Server("serv40")
//                    .Database("Tamuz")
//                    .Timeout(2)
//                    .IntegratedSecurity(true);

//            if (!con.TryOpen()) return;

//            var result = con.ExecuteScalar<bool>(
//                "SELECT\r\n" +
//                "    CASE WHEN EXISTS \r\n" +
//                "    (\r\n" +
//                "        SELECT p.[ID] FROM [Tamuz].[dbo].[Products] p WHERE LTRIM(RTRIM(p.[ProductCode])) = @productCode AND p.[CompanyID] = @companyId\r\n" +
//                "    )\r\n" +
//                "    THEN 1\r\n" +
//                "    ELSE 0\r\n" +
//                "END", new {productCode = "R2093-AN595SM", companyId = 1 });

//            Assert.IsTrue(result);
//        }
//    }
//}