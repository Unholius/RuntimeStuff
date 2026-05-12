using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Text;
using Dapper;

namespace DebugTests
{
    [TestClass]
    public class RSSqlServerTests
    {
        [TestMethod]
        public void LoadDataTable_Test_01()
        {
            var sw = new Stopwatch();
            sw.Start();

            var db = new DbClient<System.Data.SqlClient.SqlConnection>("NAS\\RSSQLSERVER", "musiclib");
            var dt = db.ToDataTable("SELECT * FROM files");
            dt = null; // Убираем ссылку
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            dt = db.ToDataTable("SELECT * FROM files");
            sw.Stop();
            var rowCount = dt.Rows.Count;
            var mem = GC.GetTotalMemory(true) / 1024 / 1024;
            var s = sw.ElapsedMilliseconds;
        }

        [TestMethod]
        public async Task LoadDataTable_Test_02()
        {
            var db = new DbClient<System.Data.SqlClient.SqlConnection>("NAS\\RSSQLSERVER", "musiclib");
            db.EnableStringPool = true;
            db.PooledStringColumns.Add("ext");

            var sw = new Stopwatch();
            sw.Start();
            var dt = db.ToDataTable("SELECT * FROM files");
            dt = null; // Убираем ссылку
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            dt = db.ToDataTable("SELECT * FROM files");
            sw.Stop();
            var s = sw.Elapsed.TotalMilliseconds;
            var rowCount = dt.Rows.Count;
            var mem = GC.GetTotalMemory(true) / 1024 / 1024;
        }

        [TestMethod]
        public async Task LoadDataTableByDapper_Test_01()
        {
            var con = new SqlConnection().Connect("NAS\\RSSQLSERVER", "musiclib");
            var sw = new Stopwatch();
            sw.Start();
            var dt = con.Query("SELECT * FROM files").ToList();
            dt = null; // Убираем ссылку
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            dt = con.Query("SELECT * FROM files").ToList();
            sw.Stop();
            var s = sw.Elapsed.TotalMilliseconds;
            var rowCount = dt.Count;
            var mem = GC.GetTotalMemory(true) / 1024 / 1024;
        }

        [TestMethod]
        public void Update_Test_01()
        {
            var db = new DbClient<System.Data.SqlClient.SqlConnection>("NAS\\RSSQLSERVER", "musiclib");
            db.Options.Map.Property<Files>(x => x.IsChecked, "is_checked");
            var rows = db.ToList<Files>(x => x.Size < 100_000);
            rows[0].IsChecked = true;
            db.Update(rows[0]);
            db.Update("files", new { is_checked = 1 }, ("is_checked", 0));
        }
    }

    public class Files
    {
        public Guid Uid { get; set; }
        public string FullName { get; set; }
        public string FileName { get; set; }
        public string Ext { get; set; }
        public DateTime Created { get; set; }
        public DateTime Modified { get; set; }
        public long Size { get; set; }

        //[Column("is_checked")]
        public bool IsChecked { get; set; }
    }

}
