using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using RuntimeStuff.Extensions;

namespace RuntimeStuff.MSTests
{
    [TestClass]
    public class EnumExtensionsTests
    {
        #region Test Enums

        private enum TestEnum
        {
            [System.ComponentModel.Description("Первое значение")]
            FirstValue,

            SecondValue,

            [Display(Name = "Третье значение")]
            ThirdValue,

            [System.ComponentModel.Description("Описание четвертого")]
            [Display(Name = "Четвертое значение")]
            FourthValue
        }

        private enum EmptyEnum
        {
            // Пустое перечисление для тестов
        }

        #endregion

        #region ToStringComparer Tests

        [TestMethod]
        public void ToStringComparer_ValidValues_ReturnsCorrectComparer()
        {
            // Arrange & Act & Assert
            Assert.AreEqual(StringComparer.Ordinal, StringComparison.Ordinal.ToStringComparer());
            Assert.AreEqual(StringComparer.OrdinalIgnoreCase, StringComparison.OrdinalIgnoreCase.ToStringComparer());
            Assert.AreEqual(StringComparer.CurrentCulture, StringComparison.CurrentCulture.ToStringComparer());
            Assert.AreEqual(StringComparer.CurrentCultureIgnoreCase, StringComparison.CurrentCultureIgnoreCase.ToStringComparer());
            Assert.AreEqual(StringComparer.InvariantCulture, StringComparison.InvariantCulture.ToStringComparer());
            Assert.AreEqual(StringComparer.InvariantCultureIgnoreCase, StringComparison.InvariantCultureIgnoreCase.ToStringComparer());
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void ToStringComparer_InvalidValue_ThrowsArgumentException()
        {
            // Arrange
            var invalidComparison = (StringComparison)999;

            // Act
            invalidComparison.ToStringComparer();
        }

        #endregion

        #region ToStringComparison Tests

        [TestMethod]
        public void ToStringComparison_ValidComparers_ReturnsCorrectComparison()
        {
            // Arrange & Act & Assert
            Assert.AreEqual(StringComparison.Ordinal, StringComparer.Ordinal.ToStringComparison());
            Assert.AreEqual(StringComparison.OrdinalIgnoreCase, StringComparer.OrdinalIgnoreCase.ToStringComparison());
            Assert.AreEqual(StringComparison.CurrentCulture, StringComparer.CurrentCulture.ToStringComparison());
            Assert.AreEqual(StringComparison.CurrentCultureIgnoreCase, StringComparer.CurrentCultureIgnoreCase.ToStringComparison());
            Assert.AreEqual(StringComparison.InvariantCulture, StringComparer.InvariantCulture.ToStringComparison());
            Assert.AreEqual(StringComparison.InvariantCultureIgnoreCase, StringComparer.InvariantCultureIgnoreCase.ToStringComparison());
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void ToStringComparison_CustomComparer_ThrowsArgumentException()
        {
            // Arrange
            var customComparer = StringComparer.Create(System.Globalization.CultureInfo.CurrentCulture, false);

            // Act
            customComparer.ToStringComparison();
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void ToStringComparison_NullComparer_ThrowsArgumentException()
        {
            // Arrange
            StringComparer nullComparer = null;

            // Act
            nullComparer.ToStringComparison();
        }

        #endregion

        #region GetDescription Tests

        [TestMethod]
        public void GetDescription_EnumWithDescriptionAttribute_ReturnsDescription()
        {
            // Arrange
            var enumValue = TestEnum.FirstValue;

            // Act
            var result = enumValue.GetDescription();

            // Assert
            Assert.AreEqual("Первое значение", result);
        }

        [TestMethod]
        public void GetDescription_EnumWithoutDescriptionAttribute_ReturnsEnumName()
        {
            // Arrange
            var enumValue = TestEnum.SecondValue;

            // Act
            var result = enumValue.GetDescription();

            // Assert
            Assert.AreEqual("SecondValue", result);
        }

        [TestMethod]
        public void GetDescription_EnumWithDisplayAttributeOnly_ReturnsEnumName()
        {
            // Arrange
            var enumValue = TestEnum.ThirdValue;

            // Act
            var result = enumValue.GetDescription();

            // Assert
            Assert.AreEqual("ThirdValue", result);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void GetDescription_NullEnum_ThrowsArgumentNullException()
        {
            // Arrange
            TestEnum? nullEnum = null;

            // Act
            nullEnum.GetDescription();
        }

        [TestMethod]
        public void GetDescription_EnumWithBothAttributes_ReturnsDescriptionFromDescriptionAttribute()
        {
            // Arrange
            var enumValue = TestEnum.FourthValue;

            // Act
            var result = enumValue.GetDescription();

            // Assert
            Assert.AreEqual("Описание четвертого", result);
        }

        #endregion

        #region GetEnumDisplayName Tests

        [TestMethod]
        public void GetEnumDisplayName_EnumWithDisplayAttribute_ReturnsDisplayName()
        {
            // Arrange
            var enumValue = TestEnum.ThirdValue;

            // Act
            var result = enumValue.GetDisplayName();

            // Assert
            Assert.AreEqual("Третье значение", result);
        }

        [TestMethod]
        public void GetEnumDisplayName_EnumWithBothAttributes_ReturnsDisplayNameFromDisplayAttribute()
        {
            // Arrange
            var enumValue = TestEnum.FourthValue;

            // Act
            var result = enumValue.GetDisplayName();

            // Assert
            Assert.AreEqual("Четвертое значение", result);
        }

        [TestMethod]
        public void GetEnumDisplayName_EnumWithoutDisplayAttribute_ReturnsNull()
        {
            // Arrange
            var enumValue = TestEnum.FirstValue;

            // Act
            var result = enumValue.GetDisplayName();

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void GetEnumDisplayName_EnumWithoutAnyAttributes_ReturnsNull()
        {
            // Arrange
            var enumValue = TestEnum.SecondValue;

            // Act
            var result = enumValue.GetDisplayName();

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void GetEnumDisplayName_NullEnum_ThrowsArgumentNullException()
        {
            // Arrange
            TestEnum? nullEnum = null;

            // Act
            nullEnum.GetDisplayName();
        }

        #endregion

        #region Edge Cases

        [TestMethod]
        public void GetDescription_EnumValueNotInEnumDefinition_ReturnsToString()
        {
            // Arrange
            var enumValue = (TestEnum)999;

            // Act
            var result = enumValue.GetDescription();

            // Assert
            Assert.AreEqual("999", result);
        }

        [TestMethod]
        public void GetEnumDisplayName_EnumValueNotInEnumDefinition_ReturnsNull()
        {
            // Arrange
            var enumValue = (TestEnum)999;

            // Act
            var result = enumValue.GetDisplayName();

            // Assert
            Assert.IsNull(result);
        }

        #endregion

        #region RoundTrip Tests

        [TestMethod]
        public void RoundTrip_StringComparisonToStringComparerAndBack()
        {
            // Test all valid StringComparison values
            foreach (StringComparison comparison in Enum.GetValues(typeof(StringComparison)))
            {
                // Arrange & Act
                var comparer = comparison.ToStringComparer();
                var roundTripComparison = comparer.ToStringComparison();

                // Assert
                Assert.AreEqual(comparison, roundTripComparison);
            }
        }

        #endregion
    }
}