using System.Data;
using Microsoft.Data.Sqlite;

namespace RuntimeStuff.MSTests
{
    [TestClass]
    public class DbClientTests
    {
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
    }
}
