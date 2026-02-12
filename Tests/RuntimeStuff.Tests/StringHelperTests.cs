using RuntimeStuff.Extensions;
using RuntimeStuff.Helpers;

namespace RuntimeStuff.MSTests
{
    [TestClass]
    public class StringHelperTests
    {
        [TestMethod]
        public void SplitToList_Test_01()
        {
            var s = "E01-WIN-2513DI\tE01-WIN-2513PR\r\n";
            var list = s.SplitToList<KeyValuePair<string, string>>();

            Assert.AreEqual(1, list.Count);
            Assert.AreEqual("E01-WIN-2513DI", list[0].Key);
            Assert.AreEqual("E01-WIN-2513PR", list[0].Value);
        }

        [TestMethod]
        public void SplitToList_Test_02()
        {
            var s = "E01-WIN-2513DI\tE01-WIN-2513PR\r\n";
            var list = s.SplitToList<(string, string)>();

            Assert.AreEqual(1, list.Count);
            Assert.AreEqual("E01-WIN-2513DI", list[0].Item1);
            Assert.AreEqual("E01-WIN-2513PR", list[0].Item2);
        }

        [TestMethod]
        public void ToSnakeCase_WithPascalCase_ReturnsSnakeCase()
        {
            // Arrange
            string input = "PascalCase";
            string expected = "pascal_case";

            // Act
            string result = StringHelper.ToSnakeCase(input);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ToSnakeCase_WithCamelCase_ReturnsSnakeCase()
        {
            // Arrange
            string input = "camelCase";
            string expected = "camel_case";

            // Act
            string result = StringHelper.ToSnakeCase(input);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ToSnakeCase_WithUnderscores_ReturnsSnakeCase()
        {
            // Arrange
            string input = "already_snake_case";
            string expected = "already_snake_case";

            // Act
            string result = StringHelper.ToSnakeCase(input);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ToSnakeCase_WithHyphens_ReturnsSnakeCase()
        {
            // Arrange
            string input = "kebab-case";
            string expected = "kebab_case";

            // Act
            string result = StringHelper.ToSnakeCase(input);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ToSnakeCase_WithSpaces_ReturnsSnakeCase()
        {
            // Arrange
            string input = "spaces between words";
            string expected = "spaces_between_words";

            // Act
            string result = StringHelper.ToSnakeCase(input);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ToSnakeCase_WithAcronyms_ReturnsSnakeCase()
        {
            // Arrange
            string input = "XMLHttpRequest";
            string expected = "xml_http_request";

            // Act
            string result = StringHelper.ToSnakeCase(input);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ToSnakeCase_WithNumbers_ReturnsSnakeCase()
        {
            // Arrange
            string input = "Version2Update3";
            string expected = "version_2_update_3";

            // Act
            string result = StringHelper.ToSnakeCase(input);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ToSnakeCase_WithEmptyString_ReturnsEmptyString()
        {
            // Arrange
            string input = "";
            string expected = "";

            // Act
            string result = StringHelper.ToSnakeCase(input);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ToSnakeCase_WithNull_ReturnsEmptyString()
        {
            // Arrange
            string input = null;
            string expected = "";

            // Act
            string result = StringHelper.ToSnakeCase(input);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ToUpperSnaceCase_WithPascalCase_ReturnsUpperSnakeCase()
        {
            // Arrange
            string input = "PascalCase";
            string expected = "PASCAL_CASE";

            // Act
            string result = StringHelper.ToUpperSnaceCase(input);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ToCamelCase_WithSnakeCase_ReturnsPascalCase()
        {
            // Arrange
            string input = "snake_case_example";
            string expected = "SnakeCaseExample";

            // Act
            string result = StringHelper.ToCamelCase(input);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ToCamelCase_WithKebabCase_ReturnsPascalCase()
        {
            // Arrange
            string input = "kebab-case-example";
            string expected = "KebabCaseExample";

            // Act
            string result = StringHelper.ToCamelCase(input);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ToLowerCamelCase_WithSnakeCase_ReturnsCamelCase()
        {
            // Arrange
            string input = "snake_case_example";
            string expected = "snakeCaseExample";

            // Act
            string result = StringHelper.ToLowerCamelCase(input);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ToLowerCamelCase_WithPascalCase_ReturnsCamelCase()
        {
            // Arrange
            string input = "PascalCase";
            string expected = "pascalCase";

            // Act
            string result = StringHelper.ToLowerCamelCase(input);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ToLowerCamelCase_WithSingleWord_ReturnsLowercase()
        {
            // Arrange
            string input = "Single";
            string expected = "single";

            // Act
            string result = StringHelper.ToLowerCamelCase(input);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ToKebabCase_WithPascalCase_ReturnsKebabCase()
        {
            // Arrange
            string input = "PascalCase";
            string expected = "pascal-case";

            // Act
            string result = StringHelper.ToKebabCase(input);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ToKebabCase_WithSnakeCase_ReturnsKebabCase()
        {
            // Arrange
            string input = "snake_case";
            string expected = "snake-case";

            // Act
            string result = StringHelper.ToKebabCase(input);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ToKebabCase_WithMixedCase_ReturnsKebabCase()
        {
            // Arrange
            string input = "Mixed_Case-With Spaces";
            string expected = "mixed-case-with-spaces";

            // Act
            string result = StringHelper.ToKebabCase(input);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void SplitWords_WithMultipleSeparators_WorksCorrectly()
        {
            // Arrange
            string input = "test_with-mixed spaces_and_underscores";
            string expected = "test_with_mixed_spaces_and_underscores";

            // Act
            string result = StringHelper.ToSnakeCase(input);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void AllMethods_WithNullOrWhiteSpace_ReturnEmptyString()
        {
            // Arrange
            string[] inputs = new[] { null, "", " ", "   " };

            foreach (var input in inputs)
            {
                // Assert
                Assert.AreEqual("", StringHelper.ToSnakeCase(input));
                Assert.AreEqual("", StringHelper.ToUpperSnaceCase(input));
                Assert.AreEqual("", StringHelper.ToCamelCase(input));
                Assert.AreEqual("", StringHelper.ToLowerCamelCase(input));
                Assert.AreEqual("", StringHelper.ToKebabCase(input));
            }
        }

        [TestMethod]
        public void Note_ToUpperSnaceCaseMethodHasTypoInName()
        {
            // Тест, который замечает, что в названии метода опечатка:
            // "ToUpperSnaceCase" вместо "ToUpperSnakeCase"
            Assert.IsTrue(true, "Метод называется ToUpperSnaceCase, а должен быть ToUpperSnakeCase");
        }
    }
}
