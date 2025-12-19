using System.Data;
using System.Data.SqlClient;
using RuntimeStuff.MSTests.Models;

namespace RuntimeStuff.MSTests
{

    //[TestClass]
    public partial class DbHelperIntegrationTests
    {
        private static string _connectionString;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            // Получаем строку подключения из конфигурации тестов
            _connectionString = context.Properties["TestDbConnectionString"]?.ToString()
                ?? "Server=NAS\\RSSQLSERVER;Database=Test;Trusted_Connection=True;";

            // Создаем тестовые таблицы
            CreateTestTables();
        }


        // Вспомогательные методы
        private static void CreateTestTables()
        {
            using var db = DbClient.Create<SqlConnection>(_connectionString);
            db.Connection.Open();

            var createTable1 = $@"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='TestTable' AND xtype='U')
                CREATE TABLE dbo.TestTable
(
    -- Числовые типы
    IdInt                INT IDENTITY(1,1) PRIMARY KEY,
    ColBigInt            BIGINT,
    ColSmallInt          SMALLINT,
    ColTinyInt           TINYINT,
    ColBit               BIT,

    ColDecimal           DECIMAL(18, 4),
    ColNumeric           NUMERIC(18, 6),
    ColMoney             MONEY,
    ColSmallMoney        SMALLMONEY,
    ColFloat             FLOAT(53),
    ColReal              REAL,

    -- Дата и время
    ColDate              DATE,
    ColTime              TIME(7),
    ColDateTime          DATETIME,
    ColDateTime2         DATETIME2(7),
    ColSmallDateTime     SMALLDATETIME,
    ColDateTimeOffset    DATETIMEOFFSET(7),

    -- Символьные типы
    ColChar              CHAR(10),
    ColVarChar           VARCHAR(100),
    ColVarCharMax        VARCHAR(MAX),

    ColNChar             NCHAR(10),
    ColNVarChar          NVARCHAR(100),
    ColNVarCharMax       NVARCHAR(MAX),

    -- Двоичные типы
    ColBinary            BINARY(16),
    ColVarBinary         VARBINARY(100),
    ColVarBinaryMax      VARBINARY(MAX),

    -- Уникальные идентификаторы и версии
    ColUniqueIdentifier  UNIQUEIDENTIFIER DEFAULT NEWID(),
    ColRowVersion        ROWVERSION,

    -- XML и JSON
    ColXml               XML,
    ColJson              NVARCHAR(MAX), -- JSON хранится как NVARCHAR

    -- Специальные типы
    ColSqlVariant        SQL_VARIANT,
    ColHierarchyId       HIERARCHYID,
    ColGeography         GEOGRAPHY,
    ColGeometry          GEOMETRY,

    -- Вычисляемое поле
    ColComputed AS (ColSmallInt * 2),

    -- Nullable пример
    ColNullableInt       INT NULL
);
";

            using (var command = new SqlCommand(createTable1, db.Connection))
            {
                command.ExecuteNonQuery();
            }
        }

        [TestMethod]
        public void DbClient_Test_01()
        {
            using var db = DbClient.Create<SqlConnection>(_connectionString);

            var record = db.Insert<DtoTestClass>();
            Assert.IsNotNull(record);
            Assert.IsTrue(record.IdInt >= 0);
            Assert.IsTrue(db.Connection.State == ConnectionState.Closed);

            var recordNull = db.First<DtoTestClass>(x => x.IdInt < 0);
            Assert.IsNull(recordNull);
            Assert.IsTrue(db.Connection.State == ConnectionState.Closed);

            var record2 = db.First<DtoTestClass>(x => x.IdInt == record.IdInt);
            Assert.AreEqual(record2.IdInt, record.IdInt);
            Assert.IsTrue(db.Connection.State == ConnectionState.Closed);

            record2.ColNVarCharMax = "123";
            var count = db.Update(record2);
            Assert.AreEqual(1, count);
            Assert.IsTrue(db.Connection.State == ConnectionState.Closed);

            var record3 = db.First<DtoTestClass>(x => x.IdInt == record.IdInt);
            Assert.AreEqual("123", record3.ColNVarCharMax);
            Assert.IsTrue(db.Connection.State == ConnectionState.Closed);

            count = db.Delete(record3);
            Assert.AreEqual(1, count);
            Assert.IsTrue(db.Connection.State == ConnectionState.Closed);

            recordNull = db.First<DtoTestClass>(x => x.IdInt == record3.IdInt);
            Assert.IsNull(recordNull);
            Assert.IsTrue(db.Connection.State == ConnectionState.Closed);

            var insertRows = new List<DtoTestClass>();
            for (var i = 0; i < 10; i++)
            {
                insertRows.Add(new DtoTestClass
                {
                    ColNVarCharMax = $"Test {i}"
                });
            }

            count = db.InsertRange(insertRows);
            Assert.AreEqual(insertRows.Count, count);

            count = db.Delete<DtoTestClass>(x => x.IdInt >= 0);
            Assert.AreEqual(insertRows.Count, count);
        }

        [TestMethod]
        public void DbClient_Test_02()
        {
            using var db = DbClient.Create<SqlConnection>(_connectionString);

            var insertRows = new List<DtoTestClass>();
            for (var i = 0; i < 1_000; i++)
            {
                insertRows.Add(new DtoTestClass
                {
                    ColNVarCharMax = $"Test {i}"
                });
            }

            var count = db.InsertRange(insertRows);
            var dbCount = db.ExecuteScalar("SELECT COUNT(*) FROM [TestTable]");
        }

    }
}