using System.Data;
using Microsoft.Data.Sqlite;

namespace RuntimeStuff.MSTests
{
    [TestClass]
    public class DbClientTests
    {
        private static string dbName() => $".\\Databases\\DB{DateTime.Now.Ticks}.db";
        private DbClient getdb()
        {
            var sql = $"CREATE TEMP TABLE Tmp (column1 int, column2 text);";
            var con = new SqliteConnection().Database(dbName());
            var db = new DbClient(con);
            db.EnableLogging = true;
            db.ExecuteNonQuery(sql);
            return db;
        }

        [TestMethod]
        public void ToDataTables_Test_01()
        {
            var query = "SELECT 1, 'Name1', '01.01.1999'; SELECT 2, 'Name2', '02.02.1999'; ";
            var con = new SqliteConnection();
            var dts = con.ToDataTables(query);
            Assert.AreEqual(2, dts.Length);
            Assert.AreEqual(3, dts[0].Columns.Count);
            Assert.AreEqual(3, dts[1].Columns.Count);

            Assert.AreEqual(1, dts[0].Rows.Count);
            Assert.AreEqual(1, dts[1].Rows.Count);

            Assert.AreEqual(1L, dts[0].Rows[0][0]);
            Assert.AreEqual("Name1", dts[0].Rows[0][1]);
            Assert.AreEqual("01.01.1999", dts[0].Rows[0][2]);

            Assert.AreEqual(2L, dts[1].Rows[0][0]);
            Assert.AreEqual("Name2", dts[1].Rows[0][1]);
            Assert.AreEqual("02.02.1999", dts[1].Rows[0][2]);
        }

        [TestMethod]
        public void Insert_Test_01()
        {
            var tmpTableName = $"T" + DateTime.Now.Ticks;
            var sql = $"CREATE TEMP TABLE {tmpTableName} (column1 int, column2 text);";
            var con = new SqliteConnection().Database(dbName());
            con.ExecuteNonQuery(sql);
            con.Insert(tmpTableName, 1, "one");
            con.Insert(tmpTableName, 2, "two");
            var dt = con.ToDataTable($"select * from {tmpTableName}");
            Assert.AreEqual(2, dt.Rows.Count);
            Assert.AreEqual(1L, dt.Rows[0]["column1"]);
            Assert.AreEqual("one", dt.Rows[0]["column2"]);
            Assert.AreEqual(2L, dt.Rows[1]["column1"]);
            Assert.AreEqual("two", dt.Rows[1]["column2"]);
        }

        [TestMethod]
        public void Insert_Test_02()
        {
            var db = getdb();
            var tr = db.BeginTransaction();
            db.Insert("Tmp", 1, "one");
            db.Insert("Tmp", 2, "two");
            db.RollbackTransaction();
            var dt = db.ToDataTable($"select * from Tmp");
            Assert.AreEqual(0, dt.Rows.Count);
        }

        [TestMethod]
        public void Select_Test_01()
        {
            var db = getdb();
            var sql = "SELECT @id, @userId";
            var dt  = db.ToDataTable(sql, new { id = 1, UserId = 2 });
            Assert.AreEqual(1L, dt.Rows.Count);
            Assert.AreEqual(1L, dt.Rows[0][0]);
            Assert.AreEqual(2L, dt.Rows[0]["userId"]);
        }

        [TestMethod]
        public void Update_Test_01()
        {
            var db = getdb();
            db.Insert("Tmp", 1, "one");
            db.Update("Tmp", new { column2 = "one-updated" }, new { column1 = 1 });
            var dt = db.ToDataTable($"select * from Tmp");
            Assert.AreEqual(1, dt.Rows.Count);
            Assert.AreEqual("one-updated", dt.Rows[0]["column2"]);
        }

        [TestMethod]
        public void Update_Test_02()
        {
            var db = getdb();
            db.Insert("Tmp", 1, "one");
            db.ExecuteNonQuery($"update Tmp set column2=:p1 where column1 in (:ids)", new { p1 = "one-updated", ids = new[] { 1 } });
            var dt = db.ToDataTable($"select * from Tmp");
            Assert.AreEqual(1, dt.Rows.Count);
            Assert.AreEqual("one-updated", dt.Rows[0]["column2"]);
        }

        [TestMethod]
        public void Update_Test_03()
        {
            var db = getdb();
            db.Insert("Tmp", 1, "one");
            db.Insert("Tmp", 2, "two");
            db.ExecuteNonQuery($"update Tmp set column2='one-updated' where column1 in (:ids)", new[] { 1 });
            var dt = db.ToDataTable($"select * from Tmp");
            Assert.AreEqual(2, dt.Rows.Count);
            Assert.AreEqual("one-updated", dt.Rows[0]["column2"]);
            Assert.AreEqual("two", dt.Rows[1]["column2"]);
        }

        [TestMethod]
        public void Update_Test_04()
        {
            var db = getdb();
            db.Insert("Tmp", 1, "one");
            db.Insert("Tmp", 2, "two");
            db.ExecuteNonQuery($"update Tmp set column2='updated' where column1 in (:ids)", new List<int>(new[] { 1, 2 }));
            var dt = db.ToDataTable($"select * from Tmp");
            Assert.AreEqual(2, dt.Rows.Count);
            Assert.AreEqual("updated", dt.Rows[0]["column2"]);
            Assert.AreEqual("updated", dt.Rows[1]["column2"]);
        }
    }
}
