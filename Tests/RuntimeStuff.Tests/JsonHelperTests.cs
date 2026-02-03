using RuntimeStuff.Helpers;
using System.Collections;

namespace RuntimeStuff.MSTests
{
    [TestClass]
    public class JsonHelperTests
    {
        #region Примитивные типы

        [TestMethod]
        public void Serialize_Null_ReturnsNullString()
        {
            // Arrange
            object obj = null;

            // Act
            var result = JsonHelper.Serialize(obj);

            // Assert
            Assert.AreEqual("null", result);
        }

        [TestMethod]
        public void Serialize_String_ReturnsQuotedString()
        {
            // Arrange
            var str = "test";

            // Act
            var result = JsonHelper.Serialize(str);

            // Assert
            Assert.AreEqual("\"test\"", result);
        }

        [TestMethod]
        public void Serialize_StringWithSpecialCharacters_EscapesCorrectly()
        {
            // Arrange
            var str = "test\"quotes\"and\nnewline";

            // Act
            var result = JsonHelper.Serialize(str);

            // Assert
            Assert.AreEqual("\"test\\\"quotes\\\"and\\nnewline\"", result);
        }

        [TestMethod]
        public void Serialize_BoolTrue_ReturnsTrue()
        {
            // Arrange
            var value = true;

            // Act
            var result = JsonHelper.Serialize(value);

            // Assert
            Assert.AreEqual("true", result);
        }

        [TestMethod]
        public void Serialize_BoolFalse_ReturnsFalse()
        {
            // Arrange
            var value = false;

            // Act
            var result = JsonHelper.Serialize(value);

            // Assert
            Assert.AreEqual("false", result);
        }

        [TestMethod]
        public void Serialize_Integer_ReturnsStringNumber()
        {
            // Arrange
            var value = 42;

            // Act
            var result = JsonHelper.Serialize(value);

            // Assert
            Assert.AreEqual("42", result);
        }

        [TestMethod]
        public void Serialize_Float_ReturnsStringNumber()
        {
            // Arrange
            var value = 3.14f;

            // Act
            var result = JsonHelper.Serialize(value);

            // Assert
            Assert.AreEqual("3.14", result);
        }

        [TestMethod]
        public void Serialize_Decimal_ReturnsStringNumber()
        {
            // Arrange
            var value = 123.456m;

            // Act
            var result = JsonHelper.Serialize(value);

            // Assert
            Assert.AreEqual("123.456", result);
        }

        #endregion

        #region Enum тесты

        private enum TestEnum
        {
            First = 1,
            Second = 2
        }

        [TestMethod]
        public void Serialize_EnumDefault_ReturnsNumber()
        {
            // Arrange
            var value = MSTests.JsonHelperTests.TestEnum.Second;

            // Act
            var result = JsonHelper.Serialize(value);

            // Assert
            Assert.AreEqual("2", result);
        }

        [TestMethod]
        public void Serialize_EnumAsStrings_ReturnsString()
        {
            // Arrange
            var value = MSTests.JsonHelperTests.TestEnum.Second;

            // Act
            var result = JsonHelper.Serialize(value, enumAsStrings: true);

            // Assert
            Assert.AreEqual("\"Second\"", result);
        }

        #endregion

        #region DateTime тесты

        [TestMethod]
        public void Serialize_DateTime_ReturnsFormattedString()
        {
            // Arrange
            var date = new DateTime(2023, 5, 15, 10, 30, 45);

            // Act
            var result = JsonHelper.Serialize(date);

            // Assert
            Assert.AreEqual("\"2023-05-15\"", result); // По умолчанию только дата
        }

        [TestMethod]
        public void Serialize_DateTimeWithCustomFormat_ReturnsFormattedString()
        {
            // Arrange
            var date = new DateTime(2023, 5, 15, 10, 30, 45);

            // Act
            var result = JsonHelper.Serialize(date, dateFormat: "yyyy-MM-dd HH:mm:ss");

            // Assert
            Assert.AreEqual("\"2023-05-15 10:30:45\"", result);
        }

        [TestMethod]
        public void Serialize_DateTimeWithAdditionalFormats_ReturnsCustomFormattedString()
        {
            // Arrange
            var date = new DateTime(2023, 5, 15, 10, 30, 45);
            var additionalFormats = new Dictionary<Type, string>
            {
                { typeof(DateTime), "dd.MM.yyyy" }
            };

            // Act
            var result = JsonHelper.Serialize(date, additionalFormats: additionalFormats);

            // Assert
            Assert.AreEqual("\"15.05.2023\"", result);
        }

        [TestMethod]
        public void Serialize_DateTimeOffset_ReturnsFormattedString()
        {
            // Arrange
            var offset = new DateTimeOffset(2023, 5, 15, 10, 30, 45, TimeSpan.FromHours(3));

            // Act
            var result = JsonHelper.Serialize(offset);

            // Assert
            Assert.AreEqual("\"2023-05-15\"", result);
        }

        [TestMethod]
        public void Serialize_TimeSpan_ReturnsFormattedString()
        {
            // Arrange
            var timeSpan = new TimeSpan(1, 2, 30, 45);

            // Act
            var result = JsonHelper.Serialize(timeSpan);

            // Assert
            Assert.AreEqual("\"1.02:30:45\"", result); // Стандартный формат TimeSpan
        }

        #endregion

        #region Коллекции

        [TestMethod]
        public void Serialize_Array_ReturnsJsonArray()
        {
            // Arrange
            int[] array = { 1, 2, 3 };

            // Act
            var result = JsonHelper.Serialize(array);

            // Assert
            Assert.AreEqual("[1,2,3]", result);
        }

        [TestMethod]
        public void Serialize_List_ReturnsJsonArray()
        {
            // Arrange
            var list = new List<string> { "a", "b", "c" };

            // Act
            var result = JsonHelper.Serialize(list);

            // Assert
            Assert.AreEqual("[\"a\",\"b\",\"c\"]", result);
        }

        [TestMethod]
        public void Serialize_EmptyArray_ReturnsEmptyJsonArray()
        {
            // Arrange
            var array = Array.Empty<int>();

            // Act
            var result = JsonHelper.Serialize(array);

            // Assert
            Assert.AreEqual("[]", result);
        }

        [TestMethod]
        public void Serialize_Dictionary_ReturnsJsonObject()
        {
            // Arrange
            var dict = new Dictionary<string, object>
            {
                ["key1"] = "value1",
                ["key2"] = 123,
                ["key3"] = true
            };

            // Act
            var result = JsonHelper.Serialize(dict);

            // Assert
            Assert.AreEqual("{\"key1\":\"value1\",\"key2\":123,\"key3\":true}", result);
        }

        [TestMethod]
        public void Serialize_Hashtable_ReturnsJsonObject()
        {
            // Arrange
            var hashtable = new Hashtable
            {
                ["key1"] = "value1",
                ["key2"] = 456,
                ["key3"] = false
            };

            // Act
            var result = JsonHelper.Serialize(hashtable);

            // Assert
            // Порядок в Hashtable не гарантирован, поэтому проверяем по частям
            Assert.IsTrue(result.Contains("\"key1\":\"value1\""));
            Assert.IsTrue(result.Contains("\"key2\":456"));
            Assert.IsTrue(result.Contains("\"key3\":false"));
            Assert.IsTrue(result.StartsWith("{"));
            Assert.IsTrue(result.EndsWith("}"));
        }

        [TestMethod]
        public void Serialize_EmptyDictionary_ReturnsEmptyJsonObject()
        {
            // Arrange
            var dict = new Dictionary<string, object>();

            // Act
            var result = JsonHelper.Serialize(dict);

            // Assert
            Assert.AreEqual("{}", result);
        }

        #endregion

        #region Объекты

        private class SimpleObject
        {
            public string? Name { get; set; }
            public int Age { get; set; }
            public bool IsActive { get; set; }
            public DateTime BirthDate { get; set; }
        }

        [TestMethod]
        public void Serialize_SimpleObject_ReturnsJsonObject()
        {
            // Arrange
            var obj = new MSTests.JsonHelperTests.SimpleObject
            {
                Name = "John",
                Age = 30,
                IsActive = true,
                BirthDate = new DateTime(1993, 5, 15)
            };

            // Act
            var result = JsonHelper.Serialize(obj, dateFormat: "yyyy-MM-dd");

            // Assert
            Assert.AreEqual("{\"Name\":\"John\",\"Age\":30,\"IsActive\":true,\"BirthDate\":\"1993-05-15\"}", result);

        }

        const string json = "{\r\n  \"orderId\": 12345,\r\n  \"date\": \"2023-10-27\",\r\n  \"customer\": {\r\n    \"id\": \"C01\",\r\n    \"name\": \"Alice\"\r\n  },\r\n  \"items\": [\r\n    {\"prodId\": \"P01\", \"price\": 10.5},\r\n    {\"prodId\": \"P02\", \"price\": 20.0}\r\n  ]\r\n}\r\n";

        [TestMethod]
        public void Test_GetAttributes()
        {
            var attr = JsonHelper.GetAttributes(json, "customer", true);
        }

        [TestMethod]
        public void Test_GetValues()
        {
            var attr = JsonHelper.GetValues(json, "prodId");
        }

        [TestMethod]
        public void Test_GetContents()
        {
            var attr = JsonHelper.GetContents(json, "customer");
        }

        [TestMethod]
        public void Serialize_ObjectWithNullProperties_ReturnsJsonObjectWithNull()
        {
            // Arrange
            var obj = new MSTests.JsonHelperTests.SimpleObject
            {
                Name = null,
                Age = 30,
                IsActive = true,
                BirthDate = new DateTime(1993, 5, 15)
            };

            // Act
            var result = JsonHelper.Serialize(obj, dateFormat: "yyyy-MM-dd");

            // Assert
            Assert.AreEqual("{\"Age\":30,\"IsActive\":true,\"BirthDate\":\"1993-05-15\"}", result);
        }

        private class ObjectWithEnum
        {
            public MSTests.JsonHelperTests.TestEnum Status { get; set; }
        }

        [TestMethod]
        public void Serialize_ObjectWithEnum_ReturnsJsonObjectWithEnumAsNumber()
        {
            // Arrange
            var obj = new MSTests.JsonHelperTests.ObjectWithEnum { Status = MSTests.JsonHelperTests.TestEnum.Second };

            // Act
            var result = JsonHelper.Serialize(obj);

            // Assert
            Assert.AreEqual("{\"Status\":2}", result);
        }

        [TestMethod]
        public void Serialize_ObjectWithEnumAsString_ReturnsJsonObjectWithEnumAsString()
        {
            // Arrange
            var obj = new MSTests.JsonHelperTests.ObjectWithEnum { Status = MSTests.JsonHelperTests.TestEnum.Second };

            // Act
            var result = JsonHelper.Serialize(obj, enumAsStrings: true);

            // Assert
            Assert.AreEqual("{\"Status\":\"Second\"}", result);
        }

        private class NestedObject
        {
            public string? Name { get; set; }
            public MSTests.JsonHelperTests.SimpleObject? Inner { get; set; }
        }

        [TestMethod]
        public void Serialize_NestedObject_ReturnsJsonObjectWithNestedObject()
        {
            // Arrange
            var obj = new MSTests.JsonHelperTests.NestedObject
            {
                Name = "Outer",
                Inner = new MSTests.JsonHelperTests.SimpleObject
                {
                    Name = "Inner",
                    Age = 25,
                    IsActive = true,
                    BirthDate = new DateTime(1998, 3, 10)
                }
            };

            // Act
            var result = JsonHelper.Serialize(obj, dateFormat: "yyyy-MM-dd");

            // Assert
            var expected = "{\"Name\":\"Outer\",\"Inner\":{\"Name\":\"Inner\",\"Age\":25,\"IsActive\":true,\"BirthDate\":\"1998-03-10\"}}";
            Assert.AreEqual(expected, result);
        }

        #endregion

        #region Сложные сценарии

        [TestMethod]
        public void Serialize_ObjectWithArrayProperty_ReturnsCorrectJson()
        {
            // Arrange
            var obj = new
            {
                Name = "Test",
                Numbers = new[] { 1, 2, 3 },
                Strings = new List<string> { "a", "b" }
            };

            // Act
            var result = JsonHelper.Serialize(obj);

            // Assert
            // Анонимные типы имеют рандомные имена свойств, но в данном случае они сохраняются
            Assert.IsTrue(result.Contains("\"Name\":\"Test\""));
            Assert.IsTrue(result.Contains("\"Numbers\":[1,2,3]"));
            Assert.IsTrue(result.Contains("\"Strings\":[\"a\",\"b\"]"));
        }

        [TestMethod]
        public void Serialize_DictionaryWithComplexValues_ReturnsCorrectJson()
        {
            // Arrange
            var dict = new Dictionary<string, object>
            {
                ["object"] = new MSTests.JsonHelperTests.SimpleObject { Name = "Test", Age = 42 },
                ["array"] = new[] { 1.120, 2.300, 3.666 },
                ["nestedDict"] = new Dictionary<string, string> { ["key"] = "value" }
            };
            var tf = new Dictionary<Type, string>() { { typeof(double), ".0000" } };

            // Act
            var result = JsonHelper.Serialize(dict, dateFormat: "yyyy-MM-dd", additionalFormats: tf);
            

            // Assert
            Assert.IsTrue(result.Contains("\"object\":{\"Name\":\"Test\",\"Age\":42,\"IsActive\":false,\"BirthDate\":\"0001-01-01\"}"));
            Assert.IsTrue(result.Contains("\"array\":[1.1200,2.3000,3.6660]"));
            Assert.IsTrue(result.Contains("\"nestedDict\":{\"key\":\"value\"}"));
        }

        #endregion

        #region Граничные случаи

        [TestMethod]
        public void Serialize_EmptyString_ReturnsQuotedEmptyString()
        {
            // Arrange
            var str = "";

            // Act
            var result = JsonHelper.Serialize(str);

            // Assert
            Assert.AreEqual("\"\"", result);
        }

        [TestMethod]
        public void Serialize_ZeroNumber_ReturnsZeroString()
        {
            // Arrange
            var zero = 0;

            // Act
            var result = JsonHelper.Serialize(zero);

            // Assert
            Assert.AreEqual("0", result);
        }

        [TestMethod]
        public void Serialize_DateTimeMinValue_ReturnsFormattedString()
        {
            // Arrange
            var minDate = DateTime.MinValue;

            // Act
            var result = JsonHelper.Serialize(minDate);

            // Assert
            Assert.AreEqual("\"0001-01-01\"", result);
        }

        [TestMethod]
        public void Serialize_DateTimeMaxValue_ReturnsFormattedString()
        {
            // Arrange
            var maxDate = DateTime.MaxValue;

            // Act
            var result = JsonHelper.Serialize(maxDate);

            // Assert
            Assert.AreEqual("\"9999-12-31\"", result);
        }

        #endregion
    }
}