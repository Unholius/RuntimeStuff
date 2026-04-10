using System.Helpers;

namespace RuntimeStuff.MSTests
{
    [TestClass]
    public class StringHelperTests
    {
        [TestMethod]
        public void SplitToList_Test_03()
        {
            var text = "2006310001105 95118164\r\n2007130000002 95114600\r\n2007130000003 95112930\r\n2001620007444 99017320\r\n2005600005426 96213874\r\n2004160004233 122220\r\n2006300001465 95117496\r\n2006300001467 95043926\r\n2005450004856 8180047903\r\n2004970017754 0\r\n2004090005388 0\r\n2004610008220 0";
            var list = StringHelper.SplitToList<KeyValuePair<string, string>>(text, null, [" ", "|", ";", "\t"],
                [Environment.NewLine, "\r", "\n"]);
            Assert.AreEqual(12, list.Count);
        }

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
            var input = "PascalCase";
            var expected = "pascal_case";

            // Act
            var result = StringHelper.ToSnakeCase(input);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ToSnakeCase_WithCamelCase_ReturnsSnakeCase()
        {
            // Arrange
            var input = "camelCase";
            var expected = "camel_case";

            // Act
            var result = StringHelper.ToSnakeCase(input);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ToSnakeCase_WithUnderscores_ReturnsSnakeCase()
        {
            // Arrange
            var input = "already_snake_case";
            var expected = "already_snake_case";

            // Act
            var result = StringHelper.ToSnakeCase(input);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ToSnakeCase_WithHyphens_ReturnsSnakeCase()
        {
            // Arrange
            var input = "kebab-case";
            var expected = "kebab_case";

            // Act
            var result = StringHelper.ToSnakeCase(input);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ToSnakeCase_WithSpaces_ReturnsSnakeCase()
        {
            // Arrange
            var input = "spaces between words";
            var expected = "spaces_between_words";

            // Act
            var result = StringHelper.ToSnakeCase(input);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ToSnakeCase_WithAcronyms_ReturnsSnakeCase()
        {
            // Arrange
            var input = "XMLHttpRequest";
            var expected = "xml_http_request";

            // Act
            var result = StringHelper.ToSnakeCase(input);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ToSnakeCase_WithNumbers_ReturnsSnakeCase()
        {
            // Arrange
            var input = "Version2Update3";
            var expected = "version_2_update_3";

            // Act
            var result = StringHelper.ToSnakeCase(input);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ToSnakeCase_WithEmptyString_ReturnsEmptyString()
        {
            // Arrange
            var input = "";
            var expected = "";

            // Act
            var result = StringHelper.ToSnakeCase(input);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ToSnakeCase_WithNull_ReturnsEmptyString()
        {
            // Arrange
            string? input = null;
            var expected = "";

            // Act
            var result = StringHelper.ToSnakeCase(input);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ToUpperSnaceCase_WithPascalCase_ReturnsUpperSnakeCase()
        {
            // Arrange
            var input = "PascalCase";
            var expected = "PASCAL_CASE";

            // Act
            var result = StringHelper.ToUpperSnakeCase(input);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ToCamelCase_WithSnakeCase_ReturnsPascalCase()
        {
            // Arrange
            var input = "snake_case_example";
            var expected = "SnakeCaseExample";

            // Act
            var result = StringHelper.ToPascalCase(input);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ToCamelCase_WithKebabCase_ReturnsPascalCase()
        {
            // Arrange
            var input = "kebab-case-example";
            var expected = "KebabCaseExample";

            // Act
            var result = StringHelper.ToPascalCase(input);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ToLowerCamelCase_WithSnakeCase_ReturnsCamelCase()
        {
            // Arrange
            var input = "snake_case_example";
            var expected = "snakeCaseExample";

            // Act
            var result = StringHelper.ToCamelCase(input);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ToLowerCamelCase_WithPascalCase_ReturnsCamelCase()
        {
            // Arrange
            var input = "PascalCase";
            var expected = "pascalCase";

            // Act
            var result = StringHelper.ToCamelCase(input);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ToLowerCamelCase_WithSingleWord_ReturnsLowercase()
        {
            // Arrange
            var input = "Single";
            var expected = "single";

            // Act
            var result = StringHelper.ToCamelCase(input);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ToKebabCase_WithPascalCase_ReturnsKebabCase()
        {
            // Arrange
            var input = "PascalCase";
            var expected = "pascal-case";

            // Act
            var result = StringHelper.ToKebabCase(input);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ToKebabCase_WithSnakeCase_ReturnsKebabCase()
        {
            // Arrange
            var input = "snake_case";
            var expected = "snake-case";

            // Act
            var result = StringHelper.ToKebabCase(input);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ToKebabCase_WithMixedCase_ReturnsKebabCase()
        {
            // Arrange
            var input = "Mixed_Case-With Spaces";
            var expected = "mixed-case-with-spaces";

            // Act
            var result = StringHelper.ToKebabCase(input);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void SplitWords_WithMultipleSeparators_WorksCorrectly()
        {
            // Arrange
            var input = "test_with-mixed spaces_and_underscores";
            var expected = "test_with_mixed_spaces_and_underscores";

            // Act
            var result = StringHelper.ToSnakeCase(input);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void AllMethods_WithNullOrWhiteSpace_ReturnEmptyString()
        {
            // Arrange
            string[] inputs = [null!, "", " ", "   "];

            foreach (var input in inputs)
            {
                // Assert
                Assert.AreEqual("", StringHelper.ToSnakeCase(input));
                Assert.AreEqual("", StringHelper.ToUpperSnakeCase(input));
                Assert.AreEqual("", StringHelper.ToPascalCase(input));
                Assert.AreEqual("", StringHelper.ToCamelCase(input));
                Assert.AreEqual("", StringHelper.ToKebabCase(input));
            }
        }
    }
}