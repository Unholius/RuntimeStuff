using RuntimeStuff.Helpers;
using System.Data.SqlClient;
using System.Data;

namespace DbHelperIntegrationTests
{
    [TestClass]
    public class DbHelperIntegrationTests
    {
        private static string _connectionString;
        private static string _testTableName = "[User]";
        private static string _testTableName2 = "[Product]";

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            // Получаем строку подключения из конфигурации тестов
            _connectionString = context.Properties["TestDbConnectionString"]?.ToString()
                ?? "Server=NAS\\RSSQLSERVER;Database=Test;Trusted_Connection=True;";

            // Создаем тестовые таблицы
            CreateTestTables();
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            // Удаляем тестовые таблицы
            CleanupTestTables();
        }

        [TestInitialize]
        public void TestInitialize()
        {
            // Очищаем таблицы перед каждым тестом
            ClearTestTables();
        }

        [TestMethod]
        public void CreateCommand_WithParameters_CreatesCommandWithCorrectParameters()
        {
            using (var db = DbClient.Create<SqlConnection>(_connectionString))
            {
                // Arrange
                var query = "SELECT * FROM @Table WHERE Id = @Id AND Name = @Name";
                var parameters = new[]
                {
                    ("@Table", (object)_testTableName),
                    ("@Id", 1),
                    ("@Name", "Test User")
                };

                // Act
                var command = db.CreateCommand(query, parameters);

                // Assert
                Assert.IsNotNull(command);
                Assert.AreEqual(query, command.CommandText);
                Assert.AreEqual(3, command.Parameters.Count);
                Assert.AreEqual(_testTableName, ((IDbDataParameter)command.Parameters[0]).Value);
                Assert.AreEqual(1, ((IDbDataParameter)command.Parameters[1]).Value);
                Assert.AreEqual("Test User", ((IDbDataParameter)command.Parameters[2]).Value);
            }
        }

        [TestMethod]
        public void ExecuteNonQuery_InsertRecord_InsertsSuccessfully()
        {
            using (var db = DbClient.Create<SqlConnection>(_connectionString))
            {
                // Arrange
                var query = $"INSERT INTO {_testTableName} (Name, Age, Email) VALUES (@Name, @Age, @Email)";
                var parameters = new[]
                {
                    ("@Name", (object)"John Doe"),
                    ("@Age", 30),
                    ("@Email", "john@example.com")
                };

                // Act
                var affectedRows = db.ExecuteNonQuery(query, parameters);

                // Assert
                Assert.AreEqual(1, affectedRows);

                // Verify the record was inserted
                var countQuery = $"SELECT COUNT(*) FROM {_testTableName} WHERE Name = 'John Doe'";
                var count = db.ExecuteScalar<int>(countQuery);
                Assert.AreEqual(1, count);
            }
        }

        [TestMethod]
        public async Task ExecuteNonQueryAsync_InsertRecord_InsertsSuccessfully()
        {
            using (var db = DbClient.Create<SqlConnection>(_connectionString))
            {
                // Arrange
                var query = $"INSERT INTO {_testTableName} (Name, Age, Email) VALUES (@Name, @Age, @Email)";
                var parameters = new[]
                {
                    ("@Name", (object)"Jane Doe"),
                    ("@Age", 25),
                    ("@Email", "jane@example.com")
                };

                // Act
                var affectedRows = await db.ExecuteNonQueryAsync(query, parameters);

                // Assert
                Assert.AreEqual(1, affectedRows);

                // Verify the record was inserted
                var countQuery = $"SELECT COUNT(*) FROM {_testTableName} WHERE Name = 'Jane Doe'";
                var count = await db.ExecuteScalarAsync<int>(countQuery);
                Assert.AreEqual(1, count);
            }
        }

        [TestMethod]
        public void ExecuteScalar_ReturnsCorrectValue()
        {
            using (var db = DbClient.Create<SqlConnection>(_connectionString))
            {
                // Arrange - вставляем тестовые данные
                var insertQuery = $"INSERT INTO {_testTableName} (Name, Age, Email) VALUES ('Test User', 30, 'test@example.com')";
                db.ExecuteNonQuery(insertQuery);

                // Act
                var query = $"SELECT COUNT(*) FROM {_testTableName}";
                var result = db.ExecuteScalar<int>(query);

                // Assert
                Assert.IsTrue(result > 0);
            }
        }

        [TestMethod]
        public async Task ExecuteScalarAsync_ReturnsCorrectValue()
        {
            using (var db = DbClient.Create<SqlConnection>(_connectionString))
            {
                // Arrange - вставляем тестовые данные
                var insertQuery = $"INSERT INTO {_testTableName} (Name, Age, Email) VALUES ('Async User', 35, 'async@example.com')";
                await db.ExecuteNonQueryAsync(insertQuery);

                // Act
                var query = $"SELECT Age FROM {_testTableName} WHERE Name = 'Async User'";
                var result = await db.ExecuteScalarAsync<int>(query);

                // Assert
                Assert.AreEqual(35, result);
            }
        }

        [TestMethod]
        public void ToList_ReturnsListOfObjects()
        {
            using (var db = DbClient.Create<SqlConnection>(_connectionString))
            {
                // Arrange - вставляем тестовые данные
                var insertQuery = $@"
                    INSERT INTO {_testTableName} (Name, Age, Email) VALUES 
                    ('User1', 20, 'user1@example.com'),
                    ('User2', 25, 'user2@example.com'),
                    ('User3', 30, 'user3@example.com')";
                db.ExecuteNonQuery(insertQuery);

                // Act
                var query = $"SELECT Id, Name, Age, Email FROM {_testTableName} ORDER BY Age";
                var result = db.ToList<User>(query);

                // Assert
                Assert.IsNotNull(result);
                Assert.AreEqual(3, result.Count);
                Assert.AreEqual("User1", result[0].Name);
                Assert.AreEqual(20, result[0].Age);
                Assert.AreEqual("User3", result[2].Name);
            }
        }

        [TestMethod]
        public void ToList_WithParameters_ReturnsFilteredList()
        {
            using (var db = DbClient.Create<SqlConnection>(_connectionString))
            {
                // Arrange - вставляем тестовые данные
                var insertQuery = $@"
                    INSERT INTO {_testTableName} (Name, Age, Email) VALUES 
                    ('Filtered1', 20, 'f1@example.com'),
                    ('Filtered2', 25, 'f2@example.com'),
                    ('Filtered3', 30, 'f3@example.com')";
                db.ExecuteNonQuery(insertQuery);

                // Act
                var query = $"SELECT Id, Name, Age, Email FROM {_testTableName} WHERE Age > @MinAge ORDER BY Age";
                var parameters = new[] { ("@MinAge", (object)22) };
                var result = db.ToList<User>(query, parameters);

                // Assert
                Assert.AreEqual(2, result.Count);
                Assert.AreEqual("Filtered2", result[0].Name);
                Assert.AreEqual("Filtered3", result[1].Name);
            }
        }

        [TestMethod]
        public void ToList_WithColumnMapping_ReturnsCorrectlyMappedObjects()
        {
            using (var db = DbClient.Create<SqlConnection>(_connectionString))
            {
                // Arrange - вставляем тестовые данные
                var insertQuery = $"INSERT INTO {_testTableName} (Name, Age, Email) VALUES ('Mapped User', 40, 'mapped@example.com')";
                db.ExecuteNonQuery(insertQuery);

                // Act - используем кастомное отображение колонок
                var query = $"SELECT Id AS UserId, Name AS UserName, Age AS UserAge, Email AS UserEmail FROM {_testTableName}";
                var columnMap = new[]
                {
                    ("UserId", "Id"),
                    ("UserName", "Name"),
                    ("UserAge", "Age"),
                    ("UserEmail", "Email")
                };
                var result = db.ToList<User>(query, columnToPropertyMap: columnMap);

                // Assert
                Assert.AreEqual(1, result.Count);
                Assert.AreEqual("Mapped User", result[0].Name);
                Assert.AreEqual(40, result[0].Age);
            }
        }

        [TestMethod]
        public async Task ToListAsync_ReturnsListOfObjects()
        {
            using (var db = DbClient.Create<SqlConnection>(_connectionString))
            {
                // Arrange - вставляем тестовые данные
                var insertQuery = $@"
                    INSERT INTO {_testTableName} (Name, Age, Email) VALUES 
                    ('Async1', 20, 'async1@example.com'),
                    ('Async2', 25, 'async2@example.com')";
                await db.ExecuteNonQueryAsync(insertQuery);

                // Act
                var query = $"SELECT Id, Name, Age, Email FROM {_testTableName} ORDER BY Age";
                var result = await db.ToListAsync<User>(query);

                // Assert
                Assert.AreEqual(2, result.Count);
                Assert.AreEqual("Async1", result[0].Name);
                Assert.AreEqual("Async2", result[1].Name);
            }
        }

        [TestMethod]
        public void ToDictionary_ReturnsDictionaryFromQuery()
        {
            using (var db = DbClient.Create<SqlConnection>(_connectionString))
            {
                // Arrange - вставляем тестовые данные
                var insertQuery = $@"
                    INSERT INTO {_testTableName} (Name, Age, Email) VALUES 
                    ('Dict1', 20, 'dict1@example.com'),
                    ('Dict2', 25, 'dict2@example.com')";
                db.ExecuteNonQuery(insertQuery);

                // Act
                var query = $"SELECT Id, Name FROM {_testTableName} ORDER BY Id";
                var result = db.ToDictionary<int, string>(query);

                // Assert
                Assert.AreEqual(2, result.Count);
                Assert.IsTrue(result.ContainsKey(1));
                Assert.IsTrue(result.ContainsKey(2));
            }
        }

        [TestMethod]
        public async Task ToDictionaryAsync_ReturnsDictionaryFromQuery()
        {
            using (var db = DbClient.Create<SqlConnection>(_connectionString))
            {
                // Arrange - вставляем тестовые данные
                var insertQuery = $@"
                    INSERT INTO {_testTableName} (Name, Age, Email) VALUES 
                    ('AsyncDict1', 20, 'ad1@example.com'),
                    ('AsyncDict2', 25, 'ad2@example.com')";
                await db.ExecuteNonQueryAsync(insertQuery);

                // Act
                var query = $"SELECT Email, Age FROM {_testTableName} ORDER BY Age";
                var result = await db.ToDictionaryAsync<string, int>(query);

                // Assert
                Assert.AreEqual(2, result.Count);
                Assert.IsTrue(result.ContainsKey("ad1@example.com"));
                Assert.IsTrue(result.ContainsKey("ad2@example.com"));
            }
        }

        [TestMethod]
        public void First_ReturnsFirstObject()
        {
            using (var db = DbClient.Create<SqlConnection>(_connectionString))
            {
                // Arrange - вставляем тестовые данные
                var insertQuery = $@"
                    INSERT INTO {_testTableName} (Name, Age, Email) VALUES 
                    ('First1', 20, 'first1@example.com'),
                    ('First2', 25, 'first2@example.com')";
                db.ExecuteNonQuery(insertQuery);

                // Act
                var query = $"SELECT Id, Name, Age, Email FROM {_testTableName} ORDER BY Age";
                var result = db.First<User>(query);

                // Assert
                Assert.IsNotNull(result);
                Assert.AreEqual("First1", result.Name);
                Assert.AreEqual(20, result.Age);
            }
        }

        [TestMethod]
        public async Task FirstAsync_ReturnsFirstObject()
        {
            using (var db = DbClient.Create<SqlConnection>(_connectionString))
            {
                // Arrange - вставляем тестовые данные
                var insertQuery = $@"
                    INSERT INTO {_testTableName} (Name, Age, Email) VALUES 
                    ('AsyncFirst1', 30, 'af1@example.com'),
                    ('AsyncFirst2', 35, 'af2@example.com')";
                await db.ExecuteNonQueryAsync(insertQuery);

                // Act
                var query = $"SELECT Id, Name, Age, Email FROM {_testTableName} ORDER BY Age DESC";
                var result = await db.FirstAsync<User>(query);

                // Assert
                Assert.IsNotNull(result);
                Assert.AreEqual("AsyncFirst2", result.Name);
                Assert.AreEqual(35, result.Age);
            }
        }

        [TestMethod]
        public void ToDataTable_ReturnsDataTable()
        {
            using (var db = DbClient.Create<SqlConnection>(_connectionString))
            {
                // Arrange - вставляем тестовые данные
                var insertQuery = $@"
                    INSERT INTO {_testTableName} (Name, Age, Email) VALUES 
                    ('DT1', 20, 'dt1@example.com'),
                    ('DT2', 25, 'dt2@example.com')";
                db.ExecuteNonQuery(insertQuery);

                // Act
                var query = $"SELECT Id, Name, Age, Email FROM {_testTableName} ORDER BY Id";
                var result = db.ToDataTable(query);

                // Assert
                Assert.IsNotNull(result);
                Assert.AreEqual(2, result.Rows.Count);
                Assert.AreEqual(4, result.Columns.Count);
                Assert.AreEqual("Id", result.Columns[0].ColumnName);
                Assert.AreEqual("Name", result.Columns[1].ColumnName);
            }
        }

        [TestMethod]
        public void Insert_WithObject_InsertsSuccessfully()
        {
            using (var db = DbClient.Create<SqlConnection>(_connectionString))
            {
                // Arrange
                var user = new User
                {
                    Name = "Insert Test",
                    Age = 45,
                    Email = "insert@example.com"
                };

                // Act
                var newId = db.Insert(user, queryGetId: "SELECT SCOPE_IDENTITY()");

                // Assert
                Assert.IsNotNull(newId);
                Assert.IsTrue(Convert.ToInt32(newId) > 0);

                // Verify insertion
                var query = $"SELECT COUNT(*) FROM {_testTableName} WHERE Id = @Id";
                var count = db.ExecuteScalar<int>(query, new[] { ("@Id", newId) });
                Assert.AreEqual(1, count);
            }
        }

        [TestMethod]
        public async Task InsertAsync_WithObject_InsertsSuccessfully()
        {
            using (var db = DbClient.Create<SqlConnection>(_connectionString))
            {
                // Arrange
                var user = new User
                {
                    Name = "Async Insert",
                    Age = 50,
                    Email = "async.insert@example.com"
                };

                // Act
                var newId = await db.InsertAsync(user, queryGetId: "SELECT SCOPE_IDENTITY()");

                // Assert
                Assert.IsNotNull(newId);

                // Verify insertion
                var query = $"SELECT Name FROM {_testTableName} WHERE Id = @Id";
                var name = await db.ExecuteScalarAsync<string>(query, new[] { ("@Id", newId) });
                Assert.AreEqual("Async Insert", name);
            }
        }

        [TestMethod]
        public void Update_WithObject_UpdatesSuccessfully()
        {
            using (var db = DbClient.Create<SqlConnection>(_connectionString))
            {
                // Arrange - сначала вставляем запись
                var insertQuery = $"INSERT INTO {_testTableName} (Name, Age, Email) VALUES ('Update Test', 30, 'update@example.com')";
                db.ExecuteNonQuery(insertQuery);

                // Получаем ID вставленной записи
                var id = db.ExecuteScalar<int>($"SELECT MAX(Id) FROM {_testTableName}");

                var user = new User
                {
                    Id = id,
                    Name = "Updated Name",
                    Age = 35,
                    Email = "updated@example.com"
                };

                // Act
                var affectedRows = db.Update(user, u => u.Id == user.Id);

                // Assert
                Assert.AreEqual(1, affectedRows);

                // Verify update
                var query = $"SELECT Name, Age FROM {_testTableName} WHERE Id = @Id";
                var result = db.ExecuteScalar<string>($"{query} AND Name = @Name",
                    new[] { ("@Id", (object)id), ("@Name", "Updated Name") });
                Assert.IsNotNull(result);
            }
        }

        [TestMethod]
        public void Delete_WithObject_DeletesSuccessfully()
        {
            using (var db = DbClient.Create<SqlConnection>(_connectionString))
            {
                // Arrange - сначала вставляем запись
                var insertQuery = $"INSERT INTO {_testTableName} (Name, Age, Email) VALUES ('Delete Test', 40, 'delete@example.com')";
                db.ExecuteNonQuery(insertQuery);

                // Получаем ID вставленной записи
                var id = db.ExecuteScalar<int>($"SELECT MAX(Id) FROM {_testTableName}");

                var user = new User { Id = id };

                // Act
                var affectedRows = db.Delete(user);

                // Assert
                Assert.AreEqual(1, affectedRows);

                // Verify deletion
                var count = db.ExecuteScalar<int>($"SELECT COUNT(*) FROM {_testTableName} WHERE Id = {id}");
                Assert.AreEqual(0, count);
            }
        }

        [TestMethod]
        public async Task DeleteAsync_WithExpression_DeletesSuccessfully()
        {
            using (var db = DbClient.Create<SqlConnection>(_connectionString))
            {
                // Arrange - сначала вставляем записи
                var insertQuery = $@"
                    INSERT INTO {_testTableName} (Name, Age, Email) VALUES 
                    ('Delete1', 10, 'delete1@example.com'),
                    ('Delete2', 20, 'delete2@example.com'),
                    ('Keep', 30, 'keep@example.com')";
                await db.ExecuteNonQueryAsync(insertQuery);

                // Act - удаляем записи с Age < 25
                var affectedRows = await db.DeleteAsync<User>(u => u.Age < 25);

                // Assert
                Assert.AreEqual(2, affectedRows);

                // Verify deletion
                var count = await db.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM {_testTableName} WHERE Age < 25");
                Assert.AreEqual(0, count);

                // Verify that other records still exist
                var keepCount = await db.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM {_testTableName} WHERE Name = 'Keep'");
                Assert.AreEqual(1, keepCount);
            }
        }

        [TestMethod]
        public void Insert_MultipleObjects_InsertsInTransaction()
        {
            using (var db = DbClient.Create<SqlConnection>(_connectionString))
            {
                // Arrange
                var users = new List<User>
                {
                    new User { Name = "Batch1", Age = 20, Email = "batch1@example.com" },
                    new User { Name = "Batch2", Age = 25, Email = "batch2@example.com" },
                    new User { Name = "Batch3", Age = 30, Email = "batch3@example.com" }
                };

                // Act
                db.Insert(users);

                // Assert
                var count = db.ExecuteScalar<int>($"SELECT COUNT(*) FROM {_testTableName} WHERE Name LIKE 'Batch%'");
                Assert.AreEqual(3, count);
            }
        }

        [TestMethod]
        public void GetParams_ReturnsCorrectParameters()
        {
            // Arrange
            var user = new User
            {
                Id = 1,
                Name = "Test User",
                Age = 30,
                Email = "test@example.com"
            };

            // Act
            var parameters = DbClient.Create().GetParams(user);

            // Assert
            Assert.IsNotNull(parameters);
            Assert.AreEqual(4, parameters.Count);
            Assert.AreEqual(1, parameters["Id"]);
            Assert.AreEqual("Test User", parameters["Name"]);
            Assert.AreEqual(30, parameters["Age"]);
            Assert.AreEqual("test@example.com", parameters["Email"]);
        }

        [TestMethod]
        public void GetParams_WithIncludeFilter_ReturnsFilteredParameters()
        {
            // Arrange
            var user = new User
            {
                Id = 1,
                Name = "Test User",
                Age = 30,
                Email = "test@example.com"
            };

            // Act
            var parameters = DbClient.Create().GetParams(user, include: true, "Name", "Email");

            // Assert
            Assert.AreEqual(2, parameters.Count);
            Assert.IsTrue(parameters.ContainsKey("Name"));
            Assert.IsTrue(parameters.ContainsKey("Email"));
            Assert.IsFalse(parameters.ContainsKey("Id"));
            Assert.IsFalse(parameters.ContainsKey("Age"));
        }

        [TestMethod]
        public void CustomDbReaderValueConvertor_IsUsed()
        {

            using (var db = DbClient.Create<SqlConnection>(_connectionString))
            {
                // Arrange
                var originalConverter = db.DbReaderValueConvertor;
                var customConverterCalled = false;

                db.DbReaderValueConvertor = (value, type) =>
                {
                    customConverterCalled = true;
                    // Простая конвертация
                    if (value == DBNull.Value) return null;
                    return Convert.ChangeType(value, type);
                };

                try
                {
                    // Вставляем тестовые данные
                    var insertQuery = $"INSERT INTO {_testTableName} (Name, Age, Email) VALUES ('Converter Test', 35, 'converter@example.com')";
                    db.ExecuteNonQuery(insertQuery);

                    // Act - используем метод, который вызывает конвертер
                    var query = $"SELECT Name FROM {_testTableName} WHERE Name = 'Converter Test'";
                    var result = db.ToList<User>(query);

                    // Assert
                    Assert.IsTrue(customConverterCalled);
                    Assert.AreEqual(1, result.Count);
                }
                finally
                {
                    // Cleanup
                    db.DbReaderValueConvertor = originalConverter;
                }
            }
        }

        [TestMethod]
        public void ToList_WithMaxRows_ReturnsLimitedResults()
        {
            using (var db = DbClient.Create<SqlConnection>(_connectionString))
            {
                // Arrange - вставляем больше записей
                var insertQuery = $@"
                    INSERT INTO {_testTableName} (Name, Age, Email) VALUES 
                    ('Limit1', 10, 'limit1@example.com'),
                    ('Limit2', 20, 'limit2@example.com'),
                    ('Limit3', 30, 'limit3@example.com'),
                    ('Limit4', 40, 'limit4@example.com'),
                    ('Limit5', 50, 'limit5@example.com')";
                db.ExecuteNonQuery(insertQuery);

                // Act - запрашиваем с ограничением в 3 строки
                var query = $"SELECT Id, Name, Age, Email FROM {_testTableName} ORDER BY Age";
                var result = db.ToList<User>(query, maxRows: 3);

                // Assert
                Assert.AreEqual(3, result.Count);
                Assert.AreEqual("Limit1", result[0].Name);
                Assert.AreEqual("Limit2", result[1].Name);
                Assert.AreEqual("Limit3", result[2].Name);
            }
        }

        // Тестовый класс для маппинга
        public class User
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public int Age { get; set; }
            public string Email { get; set; }
        }

        // Вспомогательные методы
        private static void CreateTestTables()
        {
            using (var db = DbClient.Create<SqlConnection>(_connectionString))
            {
                db.Connection.Open();

                var createTable1 = $@"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='{_testTableName}' AND xtype='U')
                CREATE TABLE {_testTableName} (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    Name NVARCHAR(100) NOT NULL,
                    Age INT NOT NULL,
                    Email NVARCHAR(100) NOT NULL,
                    CreatedDate DATETIME DEFAULT GETDATE()
                )";

                var createTable2 = $@"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='{_testTableName2}' AND xtype='U')
                CREATE TABLE {_testTableName2} (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    ProductName NVARCHAR(100) NOT NULL,
                    Price DECIMAL(18,2) NOT NULL,
                    Category NVARCHAR(50) NOT NULL
                )";

                using (var command = new SqlCommand(createTable1, db.Connection))
                {
                    command.ExecuteNonQuery();
                }

                using (var command = new SqlCommand(createTable2, db.Connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        private static void ClearTestTables()
        {
            using (var db = DbClient.Create<SqlConnection>(_connectionString))
            {
                var clearTable1 = $"DELETE FROM {_testTableName}";
                var clearTable2 = $"DELETE FROM {_testTableName2}";

                db.ExecuteNonQuery(clearTable1);
                db.ExecuteNonQuery(clearTable2);

                // Сбрасываем identity
                var resetIdentity1 = $"DBCC CHECKIDENT ('{_testTableName}', RESEED, 0)";
                var resetIdentity2 = $"DBCC CHECKIDENT ('{_testTableName2}', RESEED, 0)";

                try
                {
                    db.ExecuteNonQuery(resetIdentity1);
                    db.ExecuteNonQuery(resetIdentity2);
                }
                catch
                {
                    // Игнорируем ошибки сброса identity
                }
            }
        }

        private static void CleanupTestTables()
        {
            using (var db = DbClient.Create<SqlConnection>(_connectionString))
            {
                var dropTable1 = $"DROP TABLE IF EXISTS {_testTableName}";
                var dropTable2 = $"DROP TABLE IF EXISTS {_testTableName2}";

                db.ExecuteNonQuery(dropTable1);
                db.ExecuteNonQuery(dropTable2);
            }
        }
    }
}