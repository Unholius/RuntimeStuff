using System.Data;
using Microsoft.Data.Sqlite;

namespace RuntimeStuff.MSTests
{
    [TestClass]
    public class DbClientTests
    {
        private static Random rnd = new Random();
        private static string dbName() => $".\\Databases\\DB{DateTime.Now.ExactTicks() + rnd.Next(9999)}.db";
        private DbClient getdb()
        {
            var sql = $"CREATE TEMP TABLE Tmp (id INTEGER PRIMARY KEY AUTOINCREMENT, column1 INTEGER, column2 TEXT);";
            var con = new SqliteConnection().Database(dbName());
            var db = new DbClient(con);
            db.EnableLogging = true;
            db.ExecuteNonQuery(sql);

            var sqlTestTable = $@"
            CREATE TABLE test_table (
                id              INTEGER PRIMARY KEY AUTOINCREMENT, -- INTEGER
                int_value       INTEGER,
                real_value      REAL,
                numeric_value   NUMERIC,
                text_value      TEXT,
                blob_value      BLOB,

                boolean_value   INTEGER CHECK (boolean_value IN (0, 1)),
                date_value      TEXT,        -- ISO8601: YYYY-MM-DD
                date_time_value  TEXT,       -- ISO8601: YYYY-MM-DD HH:MM:SS
                time_value      TEXT,        -- HH:MM:SS

                decimal_value   NUMERIC(10,2),
                json_value      TEXT,        -- JSON (SQLite 3.38+ поддерживает JSON-функции)

                nullable_value  TEXT NULL,
                not_null_value  TEXT NOT NULL DEFAULT 'default',

                created_at      TEXT DEFAULT (datetime('now'))
            );
            ";
            var sqlTable11 = $@"
            CREATE TABLE users (
                id      INTEGER PRIMARY KEY AUTOINCREMENT,
                name    TEXT NOT NULL,
                guid    TEXT
            );

            CREATE TABLE user_profiles (
                user_id     INTEGER PRIMARY KEY, -- гарантирует 1:1
                bio         TEXT,
                avatar_url  TEXT,

                FOREIGN KEY (user_id)
                    REFERENCES users(id)
                    ON DELETE CASCADE
            );
            ";
            var sqlTable1M = $@"
            CREATE TABLE authors (
                id      INTEGER PRIMARY KEY AUTOINCREMENT,
                name    TEXT NOT NULL
            );

            CREATE TABLE articles (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                author_id  INTEGER NOT NULL,
                title       TEXT NOT NULL,
                content     TEXT,

                FOREIGN KEY (author_id)
                    REFERENCES authors(id)
                    ON DELETE CASCADE
            );
            ";
            var sqlTablesMM = $@"
            CREATE TABLE students (
                id      INTEGER PRIMARY KEY AUTOINCREMENT,
                name    TEXT NOT NULL
            );

            CREATE TABLE courses (
                id      INTEGER PRIMARY KEY AUTOINCREMENT,
                title   TEXT NOT NULL
            );

            CREATE TABLE student_courses (
                student_id INTEGER NOT NULL,
                course_id  INTEGER NOT NULL,

                PRIMARY KEY (student_id, course_id),

                FOREIGN KEY (student_id)
                    REFERENCES students(id)
                    ON DELETE CASCADE,

                FOREIGN KEY (course_id)
                    REFERENCES courses(id)
                    ON DELETE CASCADE
            );
            ";

            db.ExecuteNonQuery(sqlTestTable);
            db.ExecuteNonQuery(sqlTable11);
            db.ExecuteNonQuery(sqlTable1M);
            db.ExecuteNonQuery(sqlTablesMM);

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

            var db = getdb();
            var list = new List<Tmp>() { new Tmp() { Column1 = 3, Column2 = "three" }, new Tmp() { Column1 = 4, Column2 = "four" } };
            db.InsertRange(list);
            var dt = db.ToDataTable($"select * from Tmp");
            Assert.AreEqual(2, dt.Rows.Count);

            list[0].Column1 = 333;
            list[1].Column1 = 444;
            db.UpdateRange(list);
            dt = db.ToDataTable($"select * from Tmp");

            Assert.AreEqual(333L, dt.Rows[0]["column1"]);
            Assert.AreEqual(444L, dt.Rows[1]["column1"]);

            db.DeleteRange(list);
            dt = db.ToDataTable($"select * from Tmp");
            Assert.AreEqual(0, dt.Rows.Count);
        }

        [TestMethod]
        public void Insert_Test_02()
        {
            var db = getdb();
            var tr = db.BeginTransaction();
            db.Insert("Tmp", null, 1, "one");
            db.Insert("Tmp", null, 2, "two");
            db.RollbackTransaction();
            var dt = db.ToDataTable($"select * from Tmp");
            Assert.AreEqual(0, dt.Rows.Count);
        }

        private class Tmp
        {
            public int Id { get; set; }
            public int Column1 { get; set; }
            public string Column2 { get; set; }
        }

        [TestMethod]
        public void Insert_Test_03()
        {
            {
                var db = getdb();
                var tr = db.BeginTransaction();
                db.Insert("Tmp", null, 1, "one");
                db.Insert("Tmp", null, 2, "two");
                var list = new List<Tmp>() { new Tmp() { Column1 = 3, Column2 = "three" }, new Tmp() { Column1 = 4, Column2 = "four" } };
                db.InsertRange(list);
                db.RollbackTransaction();
                var dt = db.ToDataTable($"select * from Tmp");
                Assert.AreEqual(0, dt.Rows.Count);
            }
        }

        [TestMethod]
        public void AutoCloseConnection_Test_01()
        {
            {
                var db = getdb();
                db.OpenConnection();
            }
        }


        [TestMethod]
        public void Select_Test_01()
        {
            var db = getdb();
            var sql = "SELECT @id, @userId";
            var dt = db.ToDataTable(sql, new { id = 1, userId = 2 });
            Assert.AreEqual(1L, dt.Rows.Count);
            Assert.AreEqual(1L, dt.Rows[0][0]);
            Assert.AreEqual(2L, dt.Rows[0]["userId"]);
        }

        [TestMethod]
        public void Update_Test_01()
        {
            var db = getdb();
            db.Insert("Tmp", null, 1, "one");
            db.Update("Tmp", new { column2 = "one-updated" }, new { column1 = 1 });
            var dt = db.ToDataTable($"select * from Tmp");
            Assert.AreEqual(1, dt.Rows.Count);
            Assert.AreEqual("one-updated", dt.Rows[0]["column2"]);
        }

        [TestMethod]
        public void Update_Test_02()
        {
            var db = getdb();
            db.Insert("Tmp", null, 1, "one");
            db.ExecuteNonQuery($"update Tmp set column2=:p1 where column1 in (:ids)", new { p1 = "one-updated", ids = new[] { 1 } });
            var dt = db.ToDataTable($"select * from Tmp");
            Assert.AreEqual(1, dt.Rows.Count);
            Assert.AreEqual("one-updated", dt.Rows[0]["column2"]);
        }

        [TestMethod]
        public void Update_Test_03()
        {
            var db = getdb();
            db.Insert("Tmp", null, 1, "one");
            db.Insert("Tmp", null, 2, "two");
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
            db.Insert("Tmp", null, 1, "one");
            db.Insert("Tmp", null, 2, "two");
            db.ExecuteNonQuery($"update Tmp set column2='updated' where column1 in (:ids)", new List<int>(new[] { 1, 2 }));
            var dt = db.ToDataTable($"select * from Tmp");
            Assert.AreEqual(2, dt.Rows.Count);
            Assert.AreEqual("updated", dt.Rows[0]["column2"]);
            Assert.AreEqual("updated", dt.Rows[1]["column2"]);
        }

        [TestMethod]
        public void CreateCommand_Test_01()
        {
            var db = getdb();
            var cmd = db.CreateCommand("SELECT @id, @userId", new { id = 1, userId = 2 });
            Assert.AreEqual(2, cmd.Parameters.Count);
            Assert.AreEqual(1, cmd.Parameters["id"].Value);
            Assert.AreEqual(2, cmd.Parameters["userId"].Value);

            var dic1 = new Dictionary<string, object>() { { "id", 1 }, { "userId", 2 } };
            cmd = db.CreateCommand("SELECT @id, @userId", dic1);
            Assert.AreEqual(2, cmd.Parameters.Count);
            Assert.AreEqual(1, cmd.Parameters["id"].Value);
            Assert.AreEqual(2, cmd.Parameters["userId"].Value);

            cmd = db.CreateCommand("SELECT @id", ("id", 1));
            Assert.AreEqual(1, cmd.Parameters.Count);
            Assert.AreEqual(1, cmd.Parameters["id"].Value);

            cmd = db.CreateCommand("SELECT @id, @userId", new[] { ("id", 1), ("userId", 2) });
            Assert.AreEqual(2, cmd.Parameters.Count);
            Assert.AreEqual(1, cmd.Parameters["id"].Value);
            Assert.AreEqual(2, cmd.Parameters["userId"].Value);

            cmd = db.CreateCommand("SELECT @ids", new[] { 1, 2, 3 });
            Assert.AreEqual(3, cmd.Parameters.Count);
            Assert.AreEqual(1, cmd.Parameters["ids_0"].Value);
            Assert.AreEqual(2, cmd.Parameters["ids_1"].Value);
            Assert.AreEqual(3, cmd.Parameters["ids_2"].Value);

            cmd = db.CreateCommand("SELECT @id, @ids", new { id = 123, ids = new[] { 1, 2, 3 } });
            Assert.AreEqual(4, cmd.Parameters.Count);
            Assert.AreEqual(123, cmd.Parameters["id"].Value);
            Assert.AreEqual(1, cmd.Parameters["ids_0"].Value);
            Assert.AreEqual(2, cmd.Parameters["ids_1"].Value);
            Assert.AreEqual(3, cmd.Parameters["ids_2"].Value);

            var dt = new DataTable("dbo.IntList");
            dt.Columns.Add("Value", typeof(int));
            dt.AddRow(1);
            dt.AddRow(2);
            dt.AddRow(3);
            cmd = db.CreateCommand("EXEC MyProc @ids", dt);
        }

        [TestMethod]
        public void DbClient_Test_04()
        {
            using var db = getdb();
            for (var i = 0; i < 10; i++)
            {
                var user = db.Insert<DTO.SQLite.User>(x => x.Name = $"user_{i}");
            }

            var list = db.ToList<string>("select [name] from [users]");
            Assert.AreEqual(10, list.Count);
            Assert.AreEqual("user_0", list[0]);
            Assert.AreEqual("user_9", list[9]);
        }

        //[TestMethod]
        //public void DbClient_Test_01()
        //{
        //    using var db = getdb();
        //    db.Options.Map.MapToSnakeCase<DTO.SQLite.TestTable>();
        //    db.EnableLogging = true;
        //    var row = new DTO.SQLite.TestTable() { IntValue = 1, TextValue = "1" };
        //    var id = db.Insert(row, x => x.IntValue, x => x.TextValue);
        //    var row2 = db.First<DTO.SQLite.TestTable>(x => x.Id == (long)id);
        //    Assert.AreEqual(1, row2.IntValue);
        //    Assert.AreEqual("1", row2.TextValue);
        //    var result = db.Delete<DTO.SQLite.TestTable>(x => x.Id == (long)id);
        //    Assert.AreEqual(1, result);
        //    var count = db.Count<DTO.SQLite.TestTable>(x => x.Id == (long)id);
        //    Assert.AreEqual(0L, count);
        //}

        [TestMethod]
        public void DbClient_InsertRange_Test_01()
        {
            using var db = getdb();
            var rows = new List<(int IntValue, string TextValue)>
                    {
                        new () { IntValue = 111, TextValue = "1" },
                        new () { IntValue = 2222, TextValue = "22" },
                        new () { IntValue = 33333, TextValue = "333" },
                    };

            var rows2 = rows.Select(x => new { int_value = x.IntValue, text_value = x.TextValue }).ToList();

            db.EnableLogging = true;
            var ids = db.InsertRange(rows2, "test_table");
        }

        [TestMethod]
        public void DbClient_Test_02()
        {
            using var db = getdb();
            db.EnableLogging = true;
            var user = db.Insert<DTO.SQLite.User>(x => x.Name = "user_1", x => x.Guid = Guid.NewGuid());
            var profile = db.Insert<DTO.SQLite.UserProfile>(x => x.UserId = user.Id, x => x.AvatarUrl = new Uri("https://ya.ru"));
            var up = db.First<DTO.SQLite.UserProfile>(x => x.UserId == profile.UserId);
            up.User = db.First<DTO.SQLite.User>(x => x.Id == profile.UserId);
            up.Bio = "BIO!";
            var result = db.Update(up);
        }

        [TestMethod]
        public void DbClient_Test_03()
        {
            using var db = getdb();
            for (var i = 0; i < 10; i++)
            {
                var user = db.Insert<DTO.SQLite.User>(x => x.Name = $"user_{i}");
            }

            var d = db.ToDictionary<long, string, DTO.SQLite.User>(x => x.Id, x => x.Name);
            var r = db.Query(typeof(List<string>), "select [name] from [users] where id in (@ids)", new { ids = new[] { 2, 4, 6, 8, 0 } });
        }

        [TestMethod]
        public void DbClient_Test_06()
        {
            using var db = getdb();
            var r = db.Query(typeof(List<long>), "select [id] from [users] where id in (@ids)", new { ids = new[] { 1, 2, 3, 4 } });
        }
    }
}
