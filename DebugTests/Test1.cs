namespace DebugTests
{
    using RuntimeStuff;
    using RuntimeStuff.Data;
    using System.Data.SqlClient;

    [TestClass]
    public sealed class Test1
    {
        [TestMethod]
        public void TestMethod1()
        {
            var db = new DbClient<SqlConnection>("NAS\\RSSQLSERVER", "musiclib");
            db.EnableLogging = true;
            var list1 = db.ToDataTable("exec dbo.TestProc", new { dateFrom = DateTime.Now.AddDays(-1000), ext = "mp3" });
            var list2 = db.ToDataTable("select top 100000 * from files where created <= @1", new[] { new SqlParameter("@1", DateTime.Now) });
        }
    }
}