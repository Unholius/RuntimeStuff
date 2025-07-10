using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RuntimeStuff.Extensions;

namespace RuntimeStuff.MSTests.Extensions
{
    [TestClass]
    public class DataTableSimpleExtensionsTests
    {
        // Вспомогательный класс для тестирования ImportData
        private class SampleItem
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        [TestMethod]
        public void AddCol_ShouldAddNewColumn()
        {
            var dt = new DataTable();
            int before = dt.Columns.Count;

            dt.AddCol("TestCol", "Заголовок", addCopyIfExists: false, colType: typeof(int));

            Assert.AreEqual(before + 1, dt.Columns.Count);
            var col = dt.Columns["TestCol"];
            Assert.IsNotNull(col);
            Assert.AreEqual("Заголовок", col.Caption);
            Assert.AreEqual(typeof(int), col.DataType);
            Assert.IsFalse(col.AutoIncrement);
        }

        [TestMethod]
        public void AddCol_WhenExistsAndAddCopyFalse_ShouldNotAdd()
        {
            var dt = new DataTable();
            dt.Columns.Add("C1");
            int before = dt.Columns.Count;

            dt.AddCol("C1", addCopyIfExists: false);

            Assert.AreEqual(before, dt.Columns.Count);
        }

        [TestMethod]
        public void AddCol_WhenExistsAndAddCopyTrue_ShouldAddCopyWithUniqueName()
        {
            var dt = new DataTable();
            dt.Columns.Add("C1");
            int before = dt.Columns.Count;

            dt.AddCol("C1", addCopyIfExists: true);

            Assert.AreEqual(before + 1, dt.Columns.Count);
            // Новая колонка не должна иметь то же имя "C1"
            Assert.IsTrue(dt.Columns.Cast<DataColumn>().Any(c => c.ColumnName != "C1"));
        }

        [TestMethod]
        public void AddCol_WithPrimaryKey_ShouldConfigurePrimaryKey()
        {
            var dt = new DataTable();
            dt.AddCol("PK", colType: typeof(int), isPrimaryKey: true);

            // У табличного первичного ключа должна быть ровно одна колонка
            Assert.AreEqual(1, dt.PrimaryKey.Length);
            Assert.AreEqual("PK", dt.PrimaryKey[0].ColumnName);
            Assert.IsTrue(dt.PrimaryKey[0].Unique);
            Assert.IsFalse(dt.PrimaryKey[0].AllowDBNull);
        }

        [TestMethod]
        public void AddCol_WithAutoIncrement_ShouldConfigureAutoIncrement()
        {
            var dt = new DataTable();
            dt.AddCol("Auto", colType: typeof(int), isAutoIncrement: true);

            var col = dt.Columns["Auto"];
            Assert.IsTrue(col.AutoIncrement);
            Assert.AreEqual(1, col.AutoIncrementSeed);
            Assert.AreEqual(1, col.AutoIncrementStep);

            // Добавляем несколько строк, проверяем автоинкремент
            dt.AddRow();
            dt.AddRow();
            Assert.AreEqual(1, dt.Rows[0]["Auto"]);
            Assert.AreEqual(2, dt.Rows[1]["Auto"]);
        }

        [TestMethod]
        public void AddRow_ValuesOverload_ShouldAddRowWithValues()
        {
            var dt = new DataTable();
            dt.Columns.Add("A", typeof(int));
            dt.Columns.Add("B", typeof(string));

            dt.AddRow(42, "Hello");

            Assert.AreEqual(1, dt.Rows.Count);
            Assert.AreEqual(42, dt.Rows[0]["A"]);
            Assert.AreEqual("Hello", dt.Rows[0]["B"]);
        }

        [TestMethod]
        public void AddRow_OutRowOverload_ShouldReturnRowReference()
        {
            var dt = new DataTable();
            dt.Columns.Add("X", typeof(string));

            dt.AddRow(out DataRow row, "value");
            Assert.IsNotNull(row);
            Assert.AreSame(row, dt.Rows[0]);
            Assert.AreEqual("value", row["X"]);
        }

        [TestMethod]
        public void AddRow_ByColumnNames_ShouldMapValuesCorrectly()
        {
            var dt = new DataTable();
            dt.Columns.Add("Col1", typeof(int));
            dt.Columns.Add("Col2", typeof(string));

            dt.AddRow(new[] { "Col2", "Col1" }, new object[] { "abc", 99 });

            Assert.AreEqual(1, dt.Rows.Count);
            Assert.AreEqual("abc", dt.Rows[0]["Col2"]);
            Assert.AreEqual(99, dt.Rows[0]["Col1"]);
        }

        [TestMethod]
        public void AddRow_ByColumnNamesOut_ShouldReturnRowReference()
        {
            var dt = new DataTable();
            dt.Columns.Add("F", typeof(double));

            dt.AddRow(new[] { "F" }, new object[] { 3.14 }, out DataRow row);

            Assert.IsNotNull(row);
            Assert.AreSame(row, dt.Rows[0]);
            Assert.AreEqual(3.14, row["F"]);
        }

        [TestMethod]
        public void ImportData_ShouldImportCollectionData()
        {
            var items = new List<SampleItem>
            {
                new SampleItem { Id = 1, Name = "One" },
                new SampleItem { Id = 2, Name = "Two" }
            };

            var dt = new DataTable();
            dt.ImportData(items);

            // Ожидаем две колонки: Id и Name
            Assert.IsTrue(dt.Columns.Contains("Id"));
            Assert.IsTrue(dt.Columns.Contains("Name"));

            // Должно быть две строки
            Assert.AreEqual(2, dt.Rows.Count);
            Assert.AreEqual(1, dt.Rows[0]["Id"]);
            Assert.AreEqual("One", dt.Rows[0]["Name"]);
            Assert.AreEqual(2, dt.Rows[1]["Id"]);
            Assert.AreEqual("Two", dt.Rows[1]["Name"]);
        }

        [TestMethod]
        public void ImportData_ShouldHandleNullItemsGracefully()
        {
            var items = new List<SampleItem>
            {
                new SampleItem { Id = 1, Name = "First" },
                null,
                new SampleItem { Id = 3, Name = "Third" }
            };

            var dt = new DataTable();
            dt.ImportData(items);

            // После null-элемента импорт останавливается
            Assert.AreEqual(1, dt.Rows.Count);
            Assert.AreEqual(1, dt.Rows[0]["Id"]);
            Assert.AreEqual("First", dt.Rows[0]["Name"]);
        }
    }
}
