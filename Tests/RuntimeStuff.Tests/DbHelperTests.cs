using Microsoft.Data.Sqlite;

namespace RuntimeStuff.MSTests
{
    [TestClass]
    public partial class DbHelperIntegrationTests
    {
        private static EntityMap? map;
        private static string? _connectionString;

        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
        {
            // Получаем строку подключения из конфигурации тестов
            _connectionString = "Data Source=.\\Databases\\sqlte_test.db";
            map = new EntityMap();
            map
                .Table<DTO.SQLite.TestTable>("test_table")
                .Property(x => x.IntValue, "int_value")
                .Property(x => x.TextValue, "text_value")
                .Table<DTO.SQLite.User>("users")
                ;
            // Создаем тестовые таблицы
            CreateTestTables();
        }

        [TestMethod]
        public void Dumb_Test()
        {
        }

        // Вспомогательные методы
        private static void CreateTestTables()
        {
            using var db = DbClient.Create<SqliteConnection>(_connectionString);

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
    datetime_value  TEXT,        -- ISO8601: YYYY-MM-DD HH:MM:SS
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
        }

        [TestMethod]
        public void DbClient_Test_01()
        {
            using var db = DbClient.Create<SqliteConnection>(_connectionString);
            db.Options.Map = map;
            db.EnableLogging = true;
            var row = new DTO.SQLite.TestTable() { IntValue = 1, TextValue = "1" };
            var id = db.Insert(row, x => x.IntValue, x => x.TextValue);
            var row2 = db.First<DTO.SQLite.TestTable>(x => x.Id == (long)id);
            Assert.AreEqual(1, row2.IntValue);
            Assert.AreEqual("1", row2.TextValue);
            var result = db.Delete<DTO.SQLite.TestTable>(x => x.Id == (long)id);
            Assert.AreEqual(1, result);
            var count = db.Count<DTO.SQLite.TestTable, long>(x => x.Id == (long)id);
            Assert.AreEqual(0L, count);
        }

        [TestMethod]
        public void DbClient_Test_02()
        {
            using var db = DbClient.Create<SqliteConnection>(_connectionString);
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
            using var db = DbClient.Create<SqliteConnection>(_connectionString);
            for (int i = 0; i < 10; i++)
            {
                var user = db.Insert<DTO.SQLite.User>(x => x.Name = $"user_{i}");
            }

            var d = db.ToDictionary<long, string, DTO.SQLite.User>(x => x.Id, x => x.Name);
        }
    }
}