using System.Data;

namespace RuntimeStuff.MSTests;

[TestClass]
public class DataTableHelperTests
{
    [TestClass]
    public class AddColTests
    {
        [TestMethod]
        public void AddCol_ValidParameters_AddsColumn()
        {
            // Arrange
            var table = new DataTable();

            // Act
            var column = DataTableHelper.AddCol(table, "TestColumn", typeof(string));

            // Assert
            Assert.AreEqual(1, table.Columns.Count);
            Assert.IsNotNull(column);
            Assert.AreEqual("TestColumn", column.ColumnName);
            Assert.AreEqual(typeof(string), column.DataType);
            Assert.IsTrue(column.AllowDBNull);
        }

        [TestMethod]
        public void AddCol_WithIsPrimaryKey_AddsPrimaryKey()
        {
            // Arrange
            var table = new DataTable();

            // Act
            var column = DataTableHelper.AddCol(table, "Id", typeof(int), true);

            // Assert
            Assert.AreEqual(1, table.PrimaryKey.Length);
            Assert.AreEqual(column, table.PrimaryKey[0]);
            Assert.IsFalse(column.AllowDBNull);
        }

        [TestMethod]
        public void AddCol_WithPrimaryKey_AddsToExistingPrimaryKeys()
        {
            // Arrange
            var table = new DataTable();
            var col1 = DataTableHelper.AddCol(table, "Id1", typeof(int), true);

            // Act
            var col2 = DataTableHelper.AddCol(table, "Id2", typeof(string), true);

            // Assert
            Assert.AreEqual(2, table.PrimaryKey.Length);
            CollectionAssert.Contains(table.PrimaryKey, col1);
            CollectionAssert.Contains(table.PrimaryKey, col2);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void AddCol_NullTable_ThrowsArgumentNullException()
        {
            // Arrange
            DataTable table = null;

            // Act
            DataTableHelper.AddCol(table, "Test", typeof(string));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void AddCol_NullColumnType_ThrowsArgumentNullException()
        {
            // Arrange
            var table = new DataTable();

            // Act
            DataTableHelper.AddCol(table, "Test", null);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void AddCol_EmptyColumnName_ThrowsArgumentException()
        {
            // Arrange
            var table = new DataTable();

            // Act
            DataTableHelper.AddCol(table, "", typeof(string));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void AddCol_WhiteSpaceColumnName_ThrowsArgumentException()
        {
            // Arrange
            var table = new DataTable();

            // Act
            DataTableHelper.AddCol(table, "   ", typeof(string));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void AddCol_DuplicateColumnName_ThrowsArgumentException()
        {
            // Arrange
            var table = new DataTable();
            DataTableHelper.AddCol(table, "Test", typeof(string));

            // Act
            DataTableHelper.AddCol(table, "Test", typeof(int));
        }
    }

    [TestClass]
    public class AddRowTests
    {
        [TestMethod]
        public void AddRow_WithObjectArray_AddsRow()
        {
            // Arrange
            var table = new DataTable();
            DataTableHelper.AddCol(table, "Id", typeof(int));
            DataTableHelper.AddCol(table, "Name", typeof(string));
            var rowData = new object[] { 1, "Test" };

            // Act
            var row = DataTableHelper.AddRow(table, rowData);

            // Assert
            Assert.AreEqual(1, table.Rows.Count);
            Assert.AreEqual(1, row["Id"]);
            Assert.AreEqual("Test", row["Name"]);
        }

        [TestMethod]
        public void AddRow_WithNullValues_ConvertsToDBNull()
        {
            // Arrange
            var table = new DataTable();
            DataTableHelper.AddCol(table, "Id", typeof(int));
            DataTableHelper.AddCol(table, "Name", typeof(string));
            var rowData = new object[] { 1, null };

            // Act
            var row = DataTableHelper.AddRow(table, rowData);

            // Assert
            Assert.AreEqual(1, row["Id"]);
            Assert.AreEqual(DBNull.Value, row["Name"]);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void AddRow_NullTable_ThrowsArgumentNullException()
        {
            // Arrange
            DataTable table = null;
            var rowData = new object[] { 1, "Test" };

            // Act
            DataTableHelper.AddRow(table, rowData);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void AddRow_NullRowData_ThrowsArgumentNullException()
        {
            // Arrange
            var table = new DataTable();

            // Act
            DataTableHelper.AddRow(table, null);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void AddRow_InvalidRowDataLength_ThrowsArgumentException()
        {
            // Arrange
            var table = new DataTable();
            DataTableHelper.AddCol(table, "Id", typeof(int));
            var rowData = new object[] { 1, "Extra" };

            // Act
            DataTableHelper.AddRow(table, rowData);
        }
    }

    [TestClass]
    public class AddRowGenericTests
    {
        private class TestEntity
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public DateTime Date { get; set; }
        }

        [TestMethod]
        public void AddRow_WithObject_AddsRow()
        {
            // Arrange
            var table = new DataTable();
            DataTableHelper.AddCol(table, "Id", typeof(int));
            DataTableHelper.AddCol(table, "Name", typeof(string));
            DataTableHelper.AddCol(table, "Date", typeof(DateTime));

            var entity = new TestEntity
            {
                Id = 1,
                Name = "Test",
                Date = new DateTime(2024, 1, 1)
            };

            // Act
            var row = DataTableHelper.AddRow(table, entity);

            // Assert
            Assert.AreEqual(1, table.Rows.Count);
            Assert.AreEqual(1, row["Id"]);
            Assert.AreEqual("Test", row["Name"]);
            Assert.AreEqual(entity.Date, row["Date"]);
        }

        [TestMethod]
        public void AddRow_WithNullPropertyValue_ConvertsToDBNull()
        {
            // Arrange
            var table = new DataTable();
            DataTableHelper.AddCol(table, "Id", typeof(int));
            DataTableHelper.AddCol(table, "Name", typeof(string));

            var entity = new TestEntity
            {
                Id = 1,
                Name = null
            };

            // Act
            var row = DataTableHelper.AddRow(table, entity);

            // Assert
            Assert.AreEqual(1, row["Id"]);
            Assert.AreEqual(DBNull.Value, row["Name"]);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void AddRowGeneric_NullTable_ThrowsArgumentNullException()
        {
            // Arrange
            DataTable table = null;
            var entity = new TestEntity();

            // Act
            DataTableHelper.AddRow(table, entity);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void AddRowGeneric_NullItem_ThrowsArgumentNullException()
        {
            // Arrange
            var table = new DataTable();

            // Act
            DataTableHelper.AddRow(table, (TestEntity)null);
        }

        [TestMethod]
        public void AddRow_WithExtraTableColumns_IgnoresMissingProperties()
        {
            // Arrange
            var table = new DataTable();
            DataTableHelper.AddCol(table, "Id", typeof(int));
            DataTableHelper.AddCol(table, "Name", typeof(string));
            DataTableHelper.AddCol(table, "ExtraColumn", typeof(string)); // No property for this

            var entity = new TestEntity
            {
                Id = 1,
                Name = "Test"
            };

            // Act
            var row = DataTableHelper.AddRow(table, entity);

            // Assert
            Assert.AreEqual(1, row["Id"]);
            Assert.AreEqual("Test", row["Name"]);
            Assert.AreEqual(DBNull.Value, row["ExtraColumn"]);
        }
    }

    [TestClass]
    public class ToListWithColumnNameTests
    {
        [TestMethod]
        public void ToList_ValidColumn_ReturnsList()
        {
            // Arrange
            var table = new DataTable();
            DataTableHelper.AddCol(table, "Id", typeof(int));
            DataTableHelper.AddCol(table, "Name", typeof(string));

            DataTableHelper.AddRow(table, new object[] { 1, "A" });
            DataTableHelper.AddRow(table, new object[] { 2, "B" });
            DataTableHelper.AddRow(table, new object[] { 3, "C" });

            // Act
            var result = DataTableHelper.ToList<int>(table, "Id");

            // Assert
            Assert.AreEqual(3, result.Count);
            CollectionAssert.AreEqual(new List<int> { 1, 2, 3 }, result);
        }

        [TestMethod]
        public void ToList_WithNullValues_SkipsNulls()
        {
            // Arrange
            var table = new DataTable();
            DataTableHelper.AddCol(table, "Id", typeof(int));
            DataTableHelper.AddCol(table, "Name", typeof(string));

            DataTableHelper.AddRow(table, new object[] { 1, "A" });
            DataTableHelper.AddRow(table, new object[] { DBNull.Value, "B" });
            DataTableHelper.AddRow(table, new object[] { 3, "C" });

            // Act
            var result = DataTableHelper.ToList<int>(table, "Id");

            // Assert
            Assert.AreEqual(2, result.Count);
            CollectionAssert.AreEqual(new List<int> { 1, 3 }, result);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ToList_NullTable_ThrowsArgumentNullException()
        {
            // Arrange
            DataTable table = null;

            // Act
            DataTableHelper.ToList<int>(table, "Id");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void ToList_EmptyColumnName_ThrowsArgumentException()
        {
            // Arrange
            var table = new DataTable();

            // Act
            DataTableHelper.ToList<int>(table, "");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void ToList_NonexistentColumn_ThrowsArgumentException()
        {
            // Arrange
            var table = new DataTable();
            DataTableHelper.AddCol(table, "Id", typeof(int));

            // Act
            DataTableHelper.ToList<int>(table, "NonExistent");
        }
    }

    [TestClass]
    public class ToListGenericTests
    {
        private class TestEntity
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public DateTime Created { get; set; }
        }

        [TestMethod]
        public void ToList_ValidTable_ReturnsListOfObjects()
        {
            // Arrange
            var table = new DataTable();
            DataTableHelper.AddCol(table, "Id", typeof(int));
            DataTableHelper.AddCol(table, "Name", typeof(string));
            DataTableHelper.AddCol(table, "Created", typeof(DateTime));

            var date = new DateTime(2024, 1, 1);
            DataTableHelper.AddRow(table, new object[] { 1, "A", date });
            DataTableHelper.AddRow(table, new object[] { 2, "B", date.AddDays(1) });

            // Act
            var result = DataTableHelper.ToList<TestEntity>(table);

            // Assert
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(1, result[0].Id);
            Assert.AreEqual("A", result[0].Name);
            Assert.AreEqual(date, result[0].Created);
            Assert.AreEqual(2, result[1].Id);
            Assert.AreEqual("B", result[1].Name);
            Assert.AreEqual(date.AddDays(1), result[1].Created);
        }

        [TestMethod]
        public void ToList_WithNullValues_IgnoresNulls()
        {
            // Arrange
            var table = new DataTable();
            DataTableHelper.AddCol(table, "Id", typeof(int));
            DataTableHelper.AddCol(table, "Name", typeof(string));
            DataTableHelper.AddCol(table, "Created", typeof(DateTime));

            DataTableHelper.AddRow(table, new object[] { 1, DBNull.Value, DateTime.Now });

            // Act
            var result = DataTableHelper.ToList<TestEntity>(table);

            // Assert
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(1, result[0].Id);
            Assert.IsNull(result[0].Name);
            Assert.AreNotEqual(DateTime.MinValue, result[0].Created);
        }

        [TestMethod]
        public void ToList_WithExtraTableColumns_IgnoresExtraColumns()
        {
            // Arrange
            var table = new DataTable();
            DataTableHelper.AddCol(table, "Id", typeof(int));
            DataTableHelper.AddCol(table, "Name", typeof(string));
            DataTableHelper.AddCol(table, "ExtraColumn", typeof(string)); // No property for this

            DataTableHelper.AddRow(table, new object[] { 1, "Test", "ExtraValue" });

            // Act
            var result = DataTableHelper.ToList<TestEntity>(table);

            // Assert
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(1, result[0].Id);
            Assert.AreEqual("Test", result[0].Name);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ToListGeneric_NullTable_ThrowsArgumentNullException()
        {
            // Arrange
            DataTable table = null;

            // Act
            DataTableHelper.ToList<TestEntity>(table);
        }

        [TestMethod]
        public void ToList_EmptyTable_ReturnsEmptyList()
        {
            // Arrange
            var table = new DataTable();
            DataTableHelper.AddCol(table, "Id", typeof(int));
            DataTableHelper.AddCol(table, "Name", typeof(string));

            // Act
            var result = DataTableHelper.ToList<TestEntity>(table);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }
    }

    [TestClass]
    public class IntegrationTests
    {
        private class Product
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public decimal Price { get; set; }
            public bool InStock { get; set; }
        }

        [TestMethod]
        public void FullFlow_CreateTableAddRowsConvertBack_WorksCorrectly()
        {
            // Arrange - Create table structure
            var table = new DataTable();

            DataTableHelper.AddCol(table, "Id", typeof(int), true);
            DataTableHelper.AddCol(table, "Name", typeof(string));
            DataTableHelper.AddCol(table, "Price", typeof(decimal));
            DataTableHelper.AddCol(table, "InStock", typeof(bool));

            // Act - Add rows using different methods
            var products = new List<Product>
            {
                new Product { Id = 1, Name = "Laptop", Price = 999.99m, InStock = true },
                new Product { Id = 2, Name = "Mouse", Price = 29.99m, InStock = true },
                new Product { Id = 3, Name = "Keyboard", Price = 89.99m, InStock = false }
            };

            foreach (var product in products)
            {
                DataTableHelper.AddRow(table, product);
            }

            // Add one more row using object array
            DataTableHelper.AddRow(table, new object[] { 4, "Monitor", 199.99m, true });

            // Assert - Verify table structure
            Assert.AreEqual(4, table.Columns.Count);
            Assert.AreEqual(1, table.PrimaryKey.Length);
            Assert.AreEqual("Id", table.PrimaryKey[0].ColumnName);
            Assert.AreEqual(4, table.Rows.Count);

            // Act - Convert back to objects
            var result = DataTableHelper.ToList<Product>(table);

            // Assert - Verify data
            Assert.AreEqual(4, result.Count);
            Assert.AreEqual(1, result[0].Id);
            Assert.AreEqual("Laptop", result[0].Name);
            Assert.AreEqual(999.99m, result[0].Price);
            Assert.IsTrue(result[0].InStock);
            Assert.AreEqual(4, result[3].Id);
            Assert.AreEqual("Monitor", result[3].Name);
        }

        [TestMethod]
        public void ToList_WithSpecificColumn_ExtractsColumnValues()
        {
            // Arrange
            var table = new DataTable();
            DataTableHelper.AddCol(table, "Id", typeof(int));
            DataTableHelper.AddCol(table, "Name", typeof(string));

            var products = new List<Product>
            {
                new Product { Id = 1, Name = "A" },
                new Product { Id = 2, Name = "B" },
                new Product { Id = 3, Name = "C" }
            };

            foreach (var product in products)
            {
                DataTableHelper.AddRow(table, product);
            }

            // Act
            var ids = DataTableHelper.ToList<int>(table, "Id");
            var names = DataTableHelper.ToList<string>(table, "Name");

            // Assert
            CollectionAssert.AreEqual(new List<int> { 1, 2, 3 }, ids);
            CollectionAssert.AreEqual(new List<string> { "A", "B", "C" }, names);
        }
    }
}