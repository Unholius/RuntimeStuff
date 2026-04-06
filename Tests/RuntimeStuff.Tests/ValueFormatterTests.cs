using System.Globalization;
using System.Helpers;

namespace RuntimeStuff.MSTests
{
    [TestClass]
    public class ValueFormatterTests
    {
        [TestMethod]
        public void Format_NullValue_ReturnsNullString()
        {
            // Arrange
            var formatter = new ValueFormatter
            {
                NullValue = "NULL"
            };

            // Act
            var result = formatter.Format(null);

            // Assert
            Assert.AreEqual("NULL", result);
        }

        [TestMethod]
        public void Format_NullValueWithAffixes_ReturnsFormattedNull()
        {
            // Arrange
            var formatter = new ValueFormatter
            {
                NullValue = "NULL",
                NullPrefix = "[",
                NullSuffix = "]"
            };

            // Act
            var result = formatter.Format(null);

            // Assert
            Assert.AreEqual("[NULL]", result);
        }

        [TestMethod]
        public void Format_CustomNullValue_ReturnsNullString()
        {
            // Arrange
            var formatter = new ValueFormatter
            {
                NullValue = "NULL",
            };
            formatter.NullValues.Add(-1);
            formatter.NullValues.Add("N/A");
            formatter.NullValues.Add(999);
            // Act
            var result1 = formatter.Format(-1);
            var result2 = formatter.Format("N/A");
            var result3 = formatter.Format(999);
            var result4 = formatter.Format(null);
            var result5 = formatter.Format(DBNull.Value);

            // Assert
            Assert.AreEqual("NULL", result1);
            Assert.AreEqual("NULL", result2);
            Assert.AreEqual("NULL", result3);
            Assert.AreEqual("NULL", result4);
            Assert.AreEqual("NULL", result5);
        }

        [TestMethod]
        public void Format_StringValue_ReturnsTrimmedString()
        {
            // Arrange
            var formatter = new ValueFormatter
            {
                TrimSpaces = true
            };

            // Act
            var result = formatter.Format("  hello world  ");

            // Assert
            Assert.AreEqual("hello world", result);
        }

        [TestMethod]
        public void Format_StringValueWithAffixes_ReturnsFormattedString()
        {
            // Arrange
            var formatter = new ValueFormatter
            {
                StringPrefix = "\"",
                StringSuffix = "\"",
                TrimSpaces = false
            };

            // Act
            var result = formatter.Format("test");

            // Assert
            Assert.AreEqual("\"test\"", result);
        }

        [TestMethod]
        public void Format_StringValueWithNormalizeWhitespaces_ReturnsNormalizedString()
        {
            // Arrange
            var formatter = new ValueFormatter
            {
                TrimSpaces = true,
                NormalizeWhitespaces = true
            };

            // Act
            var result = formatter.Format("hello   world   test");

            // Assert
            Assert.AreEqual("hello world test", result);
        }

        [TestMethod]
        public void Format_BooleanTrue_ReturnsTrueString()
        {
            // Arrange
            var formatter = new ValueFormatter();

            // Act
            var result = formatter.Format(true);

            // Assert
            Assert.AreEqual("true", result);
        }

        [TestMethod]
        public void Format_BooleanFalse_ReturnsFalseString()
        {
            // Arrange
            var formatter = new ValueFormatter();

            // Act
            var result = formatter.Format(false);

            // Assert
            Assert.AreEqual("false", result);
        }

        [TestMethod]
        public void Format_BooleanWithCustomValues_ReturnsCustomStrings()
        {
            // Arrange
            var formatter = new ValueFormatter
            {
                TrueValue = "YES",
                FalseValue = "NO"
            };

            // Act
            var resultTrue = formatter.Format(true);
            var resultFalse = formatter.Format(false);

            // Assert
            Assert.AreEqual("YES", resultTrue);
            Assert.AreEqual("NO", resultFalse);
        }

        [TestMethod]
        public void Format_BooleanWithAffixes_ReturnsFormattedBoolean()
        {
            // Arrange
            var formatter = new ValueFormatter
            {
                BoolPrefix = "(",
                BoolSuffix = ")"
            };

            // Act
            var result = formatter.Format(true);

            // Assert
            Assert.AreEqual("(true)", result);
        }

        [TestMethod]
        public void Format_EnumAsString_ReturnsEnumName()
        {
            // Arrange
            var formatter = new ValueFormatter
            {
                EnumAsString = true
            };

            // Act
            var result = formatter.Format(DayOfWeek.Monday);

            // Assert
            Assert.AreEqual("Monday", result);
        }

        [TestMethod]
        public void Format_EnumAsNumber_ReturnsEnumValue()
        {
            // Arrange
            var formatter = new ValueFormatter
            {
                EnumAsString = false
            };

            // Act
            var result = formatter.Format(DayOfWeek.Monday);

            // Assert
            Assert.AreEqual("1", result);
        }

        [TestMethod]
        public void Format_DateTime_ReturnsFormattedDate()
        {
            // Arrange
            var formatter = new ValueFormatter
            {
                DateFormat = "yyyy-MM-dd"
            };
            var date = new DateTime(2024, 3, 15, 0, 0, 0);

            // Act
            var result = formatter.Format(date);

            // Assert
            Assert.AreEqual("2024-03-15", result);
        }

        [TestMethod]
        public void Format_DateTimeWithTime_ReturnsFormattedDateTime()
        {
            // Arrange
            var formatter = new ValueFormatter
            {
                DateTimeFormat = "yyyy-MM-dd HH:mm:ss"
            };
            var date = new DateTime(2024, 3, 15, 14, 30, 45);

            // Act
            var result = formatter.Format(date);

            // Assert
            Assert.AreEqual("2024-03-15 14:30:45", result);
        }

        [TestMethod]
        public void Format_DateTimeWithAffixes_ReturnsFormattedDateTime()
        {
            // Arrange
            var formatter = new ValueFormatter
            {
                DatePrefix = "[",
                DateSuffix = "]"
            };
            var date = new DateTime(2024, 3, 15, 0, 0, 0);

            // Act
            var result = formatter.Format(date);

            // Assert
            Assert.AreEqual("[2024-03-15]", result);
        }

        [TestMethod]
        public void Format_DateTimeOffset_ReturnsFormattedDateTime()
        {
            // Arrange
            var formatter = new ValueFormatter
            {
                DateTimeFormat = "yyyy-MM-dd HH:mm:ss"
            };
            var date = new DateTimeOffset(2024, 3, 15, 14, 30, 45, TimeSpan.Zero);

            // Act
            var result = formatter.Format(date);

            // Assert
            Assert.AreEqual("2024-03-15 14:30:45", result);
        }

        [TestMethod]
        public void Format_TimeSpan_ReturnsFormattedTimeSpan()
        {
            // Arrange
            var formatter = new ValueFormatter();
            var timeSpan = new TimeSpan(2, 30, 45);

            // Act
            var result = formatter.Format(timeSpan);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Contains("02:30:45"));
        }

        [TestMethod]
        public void Format_Enumerable_ReturnsJoinedElements()
        {
            // Arrange
            var formatter = new ValueFormatter();
            var list = new List<int> { 1, 2, 3, 4, 5 };

            // Act
            var result = formatter.Format(list);

            // Assert
            Assert.AreEqual("1, 2, 3, 4, 5", result);
        }

        [TestMethod]
        public void Format_EnumerableWithCustomSeparator_ReturnsJoinedElements()
        {
            // Arrange
            var formatter = new ValueFormatter
            {
                EnumerableSeparator = " | "
            };
            var list = new List<string> { "a", "b", "c" };

            // Act
            var result = formatter.Format(list);

            // Assert
            Assert.AreEqual("a | b | c", result);
        }

        [TestMethod]
        public void Format_EnumerableWithAffixes_ReturnsFormattedEnumerable()
        {
            // Arrange
            var formatter = new ValueFormatter
            {
                EnumerablePrefix = "[",
                EnumerableSuffix = "]",
                EnumerableSeparator = ", "
            };
            var list = new List<int> { 1, 2, 3 };

            // Act
            var result = formatter.Format(list);

            // Assert
            Assert.AreEqual("[1, 2, 3]", result);
        }

        [TestMethod]
        public void Format_Integer_ReturnsNumberString()
        {
            // Arrange
            var formatter = new ValueFormatter();

            // Act
            var result = formatter.Format(42);

            // Assert
            Assert.AreEqual("42", result);
        }

        [TestMethod]
        public void Format_Decimal_ReturnsFormattedDecimal()
        {
            // Arrange
            var formatter = new ValueFormatter
            {
                DecimalNumberFormat = "N2"
            };

            // Act
            var result = formatter.Format(123.456m);

            // Assert
            Assert.AreEqual("123.46", result);
        }

        [TestMethod]
        public void Format_DecimalWithTrimZeroes_RemovesTrailingZeros()
        {
            // Arrange
            var formatter = new ValueFormatter
            {
                TrimNumberZeroes = true
            };

            // Act
            var result = formatter.Format(123.4500m);

            // Assert
            Assert.AreEqual("123.45", result);
        }

        [TestMethod]
        public void Format_NumberWithAffixes_ReturnsFormattedNumber()
        {
            // Arrange
            var formatter = new ValueFormatter
            {
                NumberPrefix = "(",
                NumberSuffix = ")"
            };

            // Act
            var result = formatter.Format(42);

            // Assert
            Assert.AreEqual("(42)", result);
        }

        [TestMethod]
        public void Format_CustomSerializer_ReturnsCustomFormat()
        {
            // Arrange
            var formatter = new ValueFormatter();
            formatter.AddSerializer(
                t => t == typeof(DateTime),
                (obj, fmt) => "CUSTOM_DATE"
            );

            // Act
            var result = formatter.Format(DateTime.Now);

            // Assert
            Assert.AreEqual("CUSTOM_DATE", result);
        }

        [TestMethod]
        public void Format_WithPostFormatter_AppliesPostProcessing()
        {
            // Arrange
            var formatter = new ValueFormatter();
            formatter.PostFormatters.Add(s => s.ToUpper());
            formatter.PostFormatters.Add(s => "[" + s + "]");

            // Act
            var result = formatter.Format("hello");

            // Assert
            Assert.AreEqual("[HELLO]", result);
        }

        [TestMethod]
        public void Format_ObjectFallback_ReturnsToStringValue()
        {
            // Arrange
            var formatter = new ValueFormatter();
            var obj = new { Name = "Test", Value = 123 };

            // Act
            var result = formatter.Format(obj);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Contains("Test"));
        }

        [TestMethod]
        public void Format_ObjectWithAffixes_ReturnsFormattedObject()
        {
            // Arrange
            var formatter = new ValueFormatter
            {
                ObjectPrefix = "{",
                ObjectSuffix = "}"
            };
            var obj = new { Name = "Test" };

            // Act
            var result = formatter.Format(obj);

            // Assert
            Assert.IsTrue(result.StartsWith("{"));
            Assert.IsTrue(result.EndsWith("}"));
        }

        [TestMethod]
        public void Constructor_WithDateFormat_SetsDateFormat()
        {
            // Arrange & Act
            var formatter = new ValueFormatter("dd/MM/yyyy");

            // Assert
            Assert.AreEqual("dd/MM/yyyy", formatter.DateFormat);
            Assert.AreEqual("dd/MM/yyyy", formatter.DateTimeFormat);
        }

        [TestMethod]
        public void Constructor_WithBaseFormatter_CopiesSettings()
        {
            // Arrange
            var baseFormatter = new ValueFormatter
            {
                NullValue = "NULL",
                NumberPrefix = "<",
                NumberSuffix = ">",
                TrimSpaces = false
            };

            // Act
            var formatter = new ValueFormatter(baseFormatter, "yyyy-MM-dd", true, true, StringHelper.EscapeMode.Json);

            // Assert
            Assert.AreEqual("NULL", formatter.NullValue);
            Assert.AreEqual("<", formatter.NumberPrefix);
            Assert.AreEqual(">", formatter.NumberSuffix);
            Assert.AreEqual("yyyy-MM-dd", formatter.DateFormat);
            Assert.IsTrue(formatter.EnumAsString);
            Assert.IsTrue(formatter.TrimSpaces);
            Assert.AreEqual(StringHelper.EscapeMode.Json, formatter.EscapeMode);
        }

        [TestMethod]
        public void TryGetSerializer_ExistingSerializer_ReturnsTrue()
        {
            // Arrange
            var formatter = new ValueFormatter();
            formatter.AddSerializer(
                t => t == typeof(string),
                (obj, fmt) => "serialized"
            );

            // Act
            bool result = formatter.TryGetSerializer(typeof(string), out var serializer);

            // Assert
            Assert.IsTrue(result);
            Assert.IsNotNull(serializer);
        }

        [TestMethod]
        public void TryGetSerializer_NonExistingSerializer_ReturnsFalse()
        {
            // Arrange
            var formatter = new ValueFormatter();

            // Act
            bool result = formatter.TryGetSerializer(typeof(Guid), out var serializer);

            // Assert
            Assert.IsFalse(result);
            Assert.IsNull(serializer);
        }

        [TestMethod]
        public void Format_NestedEnumerable_RecursivelyFormats()
        {
            // Arrange
            var formatter = new ValueFormatter();
            var nestedList = new List<List<int>>
            {
                new List<int> { 1, 2 },
                new List<int> { 3, 4 }
            };

            // Act
            var result = formatter.Format(nestedList);

            // Assert
            Assert.AreEqual("1, 2, 3, 4", result);
        }

        [TestMethod]
        public void Format_EmptyEnumerable_ReturnsEmptyString()
        {
            // Arrange
            var formatter = new ValueFormatter();
            var emptyList = new List<int>();

            // Act
            var result = formatter.Format(emptyList);

            // Assert
            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public void Format_WithCultureInfo_RespectsCulture()
        {
            // Arrange
            var formatter = new ValueFormatter
            {
                CultureInfo = new CultureInfo("ru-RU"),
                DecimalNumberFormat = "N2"
            };

            // Act
            var result = formatter.Format(1234.56m);

            // Assert
            Assert.AreEqual("1 234,56", result);
        }
    }
}