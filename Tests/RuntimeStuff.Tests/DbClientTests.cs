using System.Data;
using Microsoft.Data.Sqlite;

namespace RuntimeStuff.MSTests
{
    [TestClass]
    public class DbClientTests
    {
        private static string dbName() => $".\\Databases\\DB{DateTime.Now.Ticks}.db";
        [TestMethod]
        public void ToDataTables_Test_01()
        {
            var query = "SELECT 1, 'Name1', '01.01.1999'; SELECT 2, 'Name2', '02.02.1999'; ";
            var con = new SqliteConnection();
            var dts = con.ToDataTables(query);
            Assert.AreEqual(dts.Length, 2);
            Assert.AreEqual(dts[0].Columns.Count, 3);
            Assert.AreEqual(dts[1].Columns.Count, 3);

            Assert.AreEqual(dts[0].Rows.Count, 1);
            Assert.AreEqual(dts[1].Rows.Count, 1);

            Assert.AreEqual(dts[0].Rows[0][0], 1L);
            Assert.AreEqual(dts[0].Rows[0][1], "Name1");
            Assert.AreEqual(dts[0].Rows[0][2], "01.01.1999");

            Assert.AreEqual(dts[1].Rows[0][0], 2L);
            Assert.AreEqual(dts[1].Rows[0][1], "Name2");
            Assert.AreEqual(dts[1].Rows[0][2], "02.02.1999");
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
            Assert.AreEqual(dt.Rows.Count, 2);
            Assert.AreEqual(dt.Rows[0]["column1"], 1L);
            Assert.AreEqual(dt.Rows[0]["column2"], "one");
            Assert.AreEqual(dt.Rows[1]["column1"], 2L);
            Assert.AreEqual(dt.Rows[1]["column2"], "two");
        }

        [TestMethod]
        public void Insert_Test_02()
        {
            var tmpTableName = $"T" + DateTime.Now.Ticks;
            var sql = $"CREATE TEMP TABLE {tmpTableName} (column1 int, column2 text);";
            var con = new SqliteConnection().Database(dbName());
            var db = new DbClient(con);
            db.ExecuteNonQuery(sql);
            var tr = db.BeginTransaction();
            db.Insert(tmpTableName, 1, "one");
            db.Insert(tmpTableName, 2, "two");
            db.RollbackTransaction();
            var dt = db.ToDataTable($"select * from {tmpTableName}");
            Assert.AreEqual(dt.Rows.Count, 0);
        }
    }
}
