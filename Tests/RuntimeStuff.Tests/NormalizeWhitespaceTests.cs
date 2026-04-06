using System.Helpers;

namespace RuntimeStuff.MSTests
{
    [TestClass]
    public class NormalizeWhitespaceTests
    {
        [TestMethod]
        public void NormalizeWhitespace_TrimSpaces()
        {
            // Act
            var result = StringHelper.NormalizeWhiteSpaces(" 123 ");

            // Assert
            Assert.AreEqual("123", result);
        }

        [TestMethod]
        public void NormalizeWhitespace_WhitespaceChars()
        {
            // Act
            var result = StringHelper.NormalizeWhiteSpaces(string.Join("", StringHelper.WhitespaceChars));

            // Assert
            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public void NormalizeWhitespace_NullInput_ReturnsNull()
        {
            // Act
            var result = StringHelper.NormalizeWhiteSpaces(null);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void NormalizeWhitespace_EmptyString_ReturnsEmptyString()
        {
            // Act
            var result = StringHelper.NormalizeWhiteSpaces(string.Empty);

            // Assert
            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public void NormalizeWhitespace_NoWhitespace_ReturnsSameString()
        {
            // Arrange
            var input = "HelloWorld";

            // Act
            var result = StringHelper.NormalizeWhiteSpaces(input);

            // Assert
            Assert.AreEqual("HelloWorld", result);
        }

        [TestMethod]
        public void NormalizeWhitespace_SingleSpace_ReturnsSingleSpace()
        {
            // Arrange
            var input = "Hello World";

            // Act
            var result = StringHelper.NormalizeWhiteSpaces(input);

            // Assert
            Assert.AreEqual("Hello World", result);
        }

        [TestMethod]
        public void NormalizeWhitespace_MultipleSpaces_CollapsesToSingleSpace()
        {
            // Arrange
            var input = "Hello    World";

            // Act
            var result = StringHelper.NormalizeWhiteSpaces(input);

            // Assert
            Assert.AreEqual("Hello World", result);
        }

        [TestMethod]
        public void NormalizeWhitespace_LeadingSpaces_RemovesLeadingSpaces()
        {
            // Arrange
            var input = "   Hello World";

            // Act
            var result = StringHelper.NormalizeWhiteSpaces(input);

            // Assert
            Assert.AreEqual("Hello World", result);
        }

        [TestMethod]
        public void NormalizeWhitespace_TrailingSpaces_RemovesTrailingSpaces()
        {
            // Arrange
            var input = "Hello World   ";

            // Act
            var result = StringHelper.NormalizeWhiteSpaces(input);

            // Assert
            Assert.AreEqual("Hello World", result);
        }

        [TestMethod]
        public void NormalizeWhitespace_LeadingAndTrailingSpaces_RemovesBoth()
        {
            // Arrange
            var input = "   Hello World   ";

            // Act
            var result = StringHelper.NormalizeWhiteSpaces(input);

            // Assert
            Assert.AreEqual("Hello World", result);
        }

        [TestMethod]
        public void NormalizeWhitespace_Tabs_TreatedAsWhitespace()
        {
            // Arrange
            var input = "Hello\tWorld";

            // Act
            var result = StringHelper.NormalizeWhiteSpaces(input);

            // Assert
            Assert.AreEqual("Hello World", result);
        }

        [TestMethod]
        public void NormalizeWhitespace_MultipleTabs_CollapsesToSingleSpace()
        {
            // Arrange
            var input = "Hello\t\t\tWorld";

            // Act
            var result = StringHelper.NormalizeWhiteSpaces(input);

            // Assert
            Assert.AreEqual("Hello World", result);
        }

        [TestMethod]
        public void NormalizeWhitespace_NewLines_TreatedAsWhitespace()
        {
            // Arrange
            var input = "Hello\nWorld";

            // Act
            var result = StringHelper.NormalizeWhiteSpaces(input);

            // Assert
            Assert.AreEqual("Hello World", result);
        }

        [TestMethod]
        public void NormalizeWhitespace_MultipleNewLines_CollapsesToSingleSpace()
        {
            // Arrange
            var input = "Hello\n\n\nWorld";

            // Act
            var result = StringHelper.NormalizeWhiteSpaces(input);

            // Assert
            Assert.AreEqual("Hello World", result);
        }

        [TestMethod]
        public void NormalizeWhitespace_CRLF_TreatedAsWhitespace()
        {
            // Arrange
            var input = "Hello\r\nWorld";

            // Act
            var result = StringHelper.NormalizeWhiteSpaces(input);

            // Assert
            Assert.AreEqual("Hello World", result);
        }

        [TestMethod]
        public void NormalizeWhitespace_MixedWhitespace_CollapsesToSingleSpace()
        {
            // Arrange
            var input = "Hello   \t\n   World";

            // Act
            var result = StringHelper.NormalizeWhiteSpaces(input);

            // Assert
            Assert.AreEqual("Hello World", result);
        }

        [TestMethod]
        public void NormalizeWhitespace_AllWhitespace_ReturnsEmptyString()
        {
            // Arrange
            var input = "   \t\n\r\n   ";

            // Act
            var result = StringHelper.NormalizeWhiteSpaces(input);

            // Assert
            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public void NormalizeWhitespace_UnicodeWhitespace_TreatedAsWhitespace()
        {
            // Arrange
            var input = "Hello\u2000World"; // En quad

            // Act
            var result = StringHelper.NormalizeWhiteSpaces(input);

            // Assert
            Assert.AreEqual("Hello World", result);
        }

        [TestMethod]
        public void NormalizeWhitespace_MultipleUnicodeWhitespace_CollapsesToSingleSpace()
        {
            // Arrange
            var input = "Hello\u2000\u2001\u2002World"; // Multiple Unicode spaces

            // Act
            var result = StringHelper.NormalizeWhiteSpaces(input);

            // Assert
            Assert.AreEqual("Hello World", result);
        }

        [TestMethod]
        public void NormalizeWhitespace_LeadingUnicodeWhitespace_Removed()
        {
            // Arrange
            var input = "\u2000\u2001Hello World";

            // Act
            var result = StringHelper.NormalizeWhiteSpaces(input);

            // Assert
            Assert.AreEqual("Hello World", result);
        }

        [TestMethod]
        public void NormalizeWhitespace_TrailingUnicodeWhitespace_Removed()
        {
            // Arrange
            var input = "Hello World\u2000\u2001";

            // Act
            var result = StringHelper.NormalizeWhiteSpaces(input);

            // Assert
            Assert.AreEqual("Hello World", result);
        }

        [TestMethod]
        public void NormalizeWhitespace_MultipleWordsWithVariousWhitespace_NormalizedCorrectly()
        {
            // Arrange
            var input = "The\tquick\nbrown\r\nfox   jumps\u2000over\tthe\rlazy\ndog";

            // Act
            var result = StringHelper.NormalizeWhiteSpaces(input);

            // Assert
            Assert.AreEqual("The quick brown fox jumps over the lazy dog", result);
        }

        [TestMethod]
        public void NormalizeWhitespace_WhitespaceOnlyInMiddle_HandlesCorrectly()
        {
            // Arrange
            var input = "Start   \t\n\r\n   End";

            // Act
            var result = StringHelper.NormalizeWhiteSpaces(input);

            // Assert
            Assert.AreEqual("Start End", result);
        }

        [TestMethod]
        public void NormalizeWhitespace_SingleCharacter_WithWhitespace()
        {
            // Arrange
            var input = "   A   ";

            // Act
            var result = StringHelper.NormalizeWhiteSpaces(input);

            // Assert
            Assert.AreEqual("A", result);
        }

        [TestMethod]
        public void NormalizeWhitespace_StringWithNumbersAndSymbols_PreservesContent()
        {
            // Arrange
            var input = "Hello   123   !@#   World";

            // Act
            var result = StringHelper.NormalizeWhiteSpaces(input);

            // Assert
            Assert.AreEqual("Hello 123 !@# World", result);
        }

        [TestMethod]
        public void NormalizeWhitespace_VeryLongWhitespaceSequence_CollapsesCorrectly()
        {
            // Arrange
            var input = "Start" + new string(' ', 1000) + "End";

            // Act
            var result = StringHelper.NormalizeWhiteSpaces(input);

            // Assert
            Assert.AreEqual("Start End", result);
        }

        [TestMethod]
        public void NormalizeWhitespace_StringWithOnlyOneNonWhitespaceChar_ReturnsThatChar()
        {
            // Arrange
            var input = "   \t\n\r   X   \t\n\r   ";

            // Act
            var result = StringHelper.NormalizeWhiteSpaces(input);

            // Assert
            Assert.AreEqual("X", result);
        }

        [TestMethod]
        public void NormalizeWhitespace_NoConsecutiveWhitespace_ReturnsOriginalWithSpaces()
        {
            // Note: This test shows that tabs and newlines are replaced with spaces
            // even when not consecutive
            var input = "Hello\tWorld\nTest\r\nCase";

            // Act
            var result = StringHelper.NormalizeWhiteSpaces(input);

            // Assert
            Assert.AreEqual("Hello World Test Case", result);
        }

        [TestMethod]
        public void NormalizeWhitespace_ConsecutiveSpacesAfterWhitespace_CollapsesCorrectly()
        {
            // Arrange
            var input = "Hello \t  World"; // Space, tab, two spaces

            // Act
            var result = StringHelper.NormalizeWhiteSpaces(input);

            // Assert
            Assert.AreEqual("Hello World", result);
        }

        [TestMethod]
        public void NormalizeWhitespace_WhitespaceAtStartWithNoText_ReturnsEmptyString()
        {
            // Arrange
            var input = "   \t\n\r   ";

            // Act
            var result = StringHelper.NormalizeWhiteSpaces(input);

            // Assert
            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public void NormalizeWhitespace_EmptyStringWithWhitespace_ReturnsEmptyString()
        {
            // Arrange
            var input = "   ";

            // Act
            var result = StringHelper.NormalizeWhiteSpaces(input);

            // Assert
            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public void NormalizeWhitespace_StringWithMultipleWhitespaceTypesBetweenWords_InsertsSingleSpace()
        {
            // Arrange
            var input = "word1\t\n\r   word2";

            // Act
            var result = StringHelper.NormalizeWhiteSpaces(input);

            // Assert
            Assert.AreEqual("word1 word2", result);
        }
    }
}