// <copyright file="XmlHelperTests.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

using System.Xml.Serialization;
using RuntimeStuff.Helpers;
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.

namespace RuntimeStuff.MSTests
{
    [TestClass]
    public class XmlHelperTests
    {
        #region Test Models for Serialization

        [XmlRoot("TestModel")]
        public class TestModel
        {
            public string? Name { get; set; }
            public int Value { get; set; }
            public List<string> Items { get; set; } = new List<string>();
        }

        [XmlRoot("TestModel")]
        public class TestModelRecursive
        {
            public string? Name { get; set; }
            public int Value { get; set; }
            public List<string> Items { get; set; } = new List<string>();
            public TestModelRecursive? Child { get; set; }
        }

        [XmlRoot("SimpleModel")]
        public class SimpleModel
        {
            [XmlAttribute("id")]
            public int Id { get; set; }
            public string? Description { get; set; }
        }

        #endregion

        #region Serialize Tests

        [TestMethod]
        public void Serialize_ValidObject_ReturnsXmlString()
        {
            // Arrange
            var model = new TestModel
            {
                Name = "Test",
                Value = 42,
                Items = { "Item1", "Item2", "Item3" }
            };

            // Act
            var result = XmlHelper.Serialize(model, false, false);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Contains("<TestModel>"));
            Assert.IsTrue(result.Contains("<Name>Test</Name>"));
            Assert.IsTrue(result.Contains("<Value>42</Value>"));
            Assert.IsTrue(result.Contains("<Items>"));
        }

        [TestMethod]
        public void Serialize_ValidRecursiveObject_ReturnsXmlString()
        {
            // Arrange
            var model = new TestModelRecursive
            {
                Name = "Test",
                Value = 42,
                Items = { "Item1", "Item2", "Item3" },
                Child = new TestModelRecursive
                {
                    Name = "ChildTest",
                    Value = 24,
                    Items = { "ChildItem1", "ChildItem2" }
                }
            };

            // Act
            var result = XmlHelper.Serialize(model, false, true);

            // Assert
            Assert.IsNotNull(result);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Serialize_NullObject_ThrowsException()
        {
            // Act
            XmlHelper.Serialize(null, false, false);
        }

        #endregion

        #region GetAttributes Tests

        [TestMethod]
        public void GetAttributes_ValidXmlAndNodeName_ReturnsAttributes()
        {
            // Arrange
            var xml = @"<root><item id='1' name='test1'/><item id='2' name='test2'/></root>";

            // Act
            var result = XmlHelper.GetAttributes(xml, "item");

            // Assert
            Assert.AreEqual(2, result.Length);
            Assert.AreEqual("1", result[0]["id"]);
            Assert.AreEqual("test1", result[0]["name"]);
            Assert.AreEqual("2", result[1]["id"]);
            Assert.AreEqual("test2", result[1]["name"]);
        }

        [TestMethod]
        public void GetAttributes_WithNodeNameSelector_ReturnsFilteredAttributes()
        {
            // Arrange
            var xml = @"<root><item id='1'/><other id='2'/><item id='3'/></root>";

            // Act
            var result = XmlHelper.GetAttributes(xml, x => x == "item");

            // Assert
            Assert.AreEqual(2, result.Length);
            Assert.AreEqual("1", result[0]["id"]);
            Assert.AreEqual("3", result[1]["id"]);
        }

        [TestMethod]
        public void GetAttributes_InvalidXml_ReturnsEmptyArray()
        {
            // Arrange
            var invalidXml = "<root><unclosed>";

            // Act
            var result = XmlHelper.GetAttributes(invalidXml, "item");

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Length);
        }

        [TestMethod]
        public void GetAttributes_NullOrEmptyXml_ReturnsEmptyArray()
        {
            // Act & Assert
            Assert.AreEqual(0, XmlHelper.GetAttributes(null, "item").Length);
            Assert.AreEqual(0, XmlHelper.GetAttributes("", "item").Length);
            Assert.AreEqual(0, XmlHelper.GetAttributes("   ", "item").Length);
        }

        [TestMethod]
        public void GetAttributes_NullSelector_ReturnsEmptyArray()
        {
            // Arrange
            var xml = "<root><item/></root>";

            // Act
            var result = XmlHelper.GetAttributes(xml, (Func<string, bool>)null);

            // Assert
            Assert.AreEqual(0, result.Length);
        }

        #endregion

        #region GetAttributes with Content Filter Tests

        [TestMethod]
        public void GetAttributes_WithContentFilter_ReturnsFilteredAttributes()
        {
            // Arrange
            var xml = @"
<root>
    <content>Filter1</content>
    <content>Filter2</content>
</root>";

            // Act
            var result = XmlHelper.GetAttributes(
                xml,
                "item",
                "content",
                x => x.Contains("Filter1"));

            // Assert
            Assert.AreEqual(0, result.Length);
        }

        [TestMethod]
        public void GetAttributes_WithComplexContentFilter_ReturnsAttributes()
        {
            // Arrange
            var xml = @"
<root>
    <section>
        <item id='1'/>
        <other>data1</other>
    </section>
    <section>
        <item id='2'/>
        <other>data2</other>
    </section>
</root>";

            // Act
            var result = XmlHelper.GetAttributes(
                xml,
                x => x == "item",
                x => x == "section",
                x => x.Contains("data1"));

            // Assert
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual("1", result[0]["id"]);
        }

        [TestMethod]
        public void GetAttributes_WithSelectorsAndContentFilter_EmptyResult()
        {
            // Arrange
            var xml = "<root><item id='1'/></root>";

            // Act
            var result = XmlHelper.GetAttributes(
                xml,
                x => false, // selector that never matches
                x => true,
                x => true);

            // Assert
            Assert.AreEqual(0, result.Length);
        }

        #endregion

        #region GetContents Tests

        [TestMethod]
        public void GetContents_ValidXml_ReturnsNodeContents()
        {
            // Arrange
            var xml = @"<root><item id='1'>Content1</item><item id='2'>Content2</item></root>";

            // Act
            var result = XmlHelper.GetContents(xml, "item");

            // Assert
            Assert.AreEqual(2, result.Length);
            Assert.IsTrue(result[0].Contains("id=\"1\""));
            Assert.IsTrue(result[0].Contains("Content1"));
            Assert.IsTrue(result[1].Contains("id=\"2\""));
            Assert.IsTrue(result[1].Contains("Content2"));
        }

        [TestMethod]
        public void GetContents_WithContentFilter_ReturnsFilteredContents()
        {
            // Arrange
            var xml = @"<root><item>Include</item><item>Exclude</item></root>";

            // Act
            var result = XmlHelper.GetContents(xml, "item", x => x.Contains("Include"));

            // Assert
            Assert.AreEqual(1, result.Length);
            Assert.IsTrue(result[0].Contains("Include"));
        }

        [TestMethod]
        public void GetContents_NullXml_ReturnsEmptyArray()
        {
            // Act
            var result = XmlHelper.GetContents(null, "item");

            // Assert
            Assert.AreEqual(0, result.Length);
        }

        [TestMethod]
        public void GetContents_NullSelector_ReturnsEmptyArray()
        {
            // Arrange
            var xml = "<root><item/></root>";

            // Act
            var result = XmlHelper.GetContents(xml, (Func<string, bool>)null);

            // Assert
            Assert.AreEqual(0, result.Length);
        }

        #endregion

        #region GetValues Tests

        [TestMethod]
        public void GetValues_ValidXml_ReturnsNodeValues()
        {
            // Arrange
            var xml = @"<root><item>Value1</item><item>Value2</item></root>";

            // Act
            var result = XmlHelper.GetValues(xml, "item");

            // Assert
            Assert.AreEqual(2, result.Length);
            Assert.AreEqual("Value1", result[0]);
            Assert.AreEqual("Value2", result[1]);
        }

        [TestMethod]
        public void GetValues_WithNodeNameSelector_ReturnsFilteredValues()
        {
            // Arrange
            var xml = @"<root><item>Value1</item><other>OtherValue</other><item>Value2</item></root>";

            // Act
            var result = XmlHelper.GetValues(xml, x => x == "item");

            // Assert
            Assert.AreEqual(2, result.Length);
            Assert.AreEqual("Value1", result[0]);
            Assert.AreEqual("Value2", result[1]);
        }

        [TestMethod]
        public void GetValues_NestedValues_ReturnsCorrectValues()
        {
            // Arrange
            var xml = @"<root><item><nested>Value1</nested></item><item><nested>Value2</nested></item></root>";

            // Act
            var result = XmlHelper.GetValues(xml, "item");

            // Assert
            Assert.AreEqual(2, result.Length);
            Assert.AreEqual("Value1", result[0]); // Xml сохраняет вложенный текст
            Assert.AreEqual("Value2", result[1]);
        }

        [TestMethod]
        public void GetValues_WithContentFilter_ReturnsFilteredValues()
        {
            // Arrange
            var xml = @"
<root>
    <section><item>Value1</item></section>
    <section><item>Value2</item></section>
</root>";

            // Act
            var result = XmlHelper.GetValues(
                xml,
                "item",
                "section",
                x => x.Contains("Value1"));

            // Assert
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual("Value1", result[0]);
        }

        [TestMethod]
        public void GetValues_EmptyXml_ReturnsEmptyArray()
        {
            // Act
            var result = XmlHelper.GetValues("", "item");

            // Assert
            Assert.AreEqual(0, result.Length);
        }

        #endregion

        #region Edge Cases and Integration Tests

        [TestMethod]
        public void GetAttributes_MultipleNamespaces_ReturnsAttributes()
        {
            // Arrange
            var xml = @"<root xmlns:ns='http://example.com'><ns:item id='1' name='test'/></root>";

            // Act
            var result = XmlHelper.GetAttributes(xml, "item");

            // Assert
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual("1", result[0]["id"]);
            Assert.AreEqual("test", result[0]["name"]);
        }

        [TestMethod]
        public void GetAttributes_NodeWithoutAttributes_ReturnsEmptyDictionary()
        {
            // Arrange
            var xml = @"<root><item/><item attr='value'/></root>";

            // Act
            var result = XmlHelper.GetAttributes(xml, "item");

            // Assert
            Assert.AreEqual(2, result.Length);
            Assert.AreEqual(0, result[0].Count);
            Assert.AreEqual(1, result[1].Count);
            Assert.AreEqual("value", result[1]["attr"]);
        }

        [TestMethod]
        public void GetValues_ComplexXmlStructure_ReturnsCorrectValues()
        {
            // Arrange
            var xml = @"
<products>
    <product>
        <name>Product1</name>
        <price>100</price>
    </product>
    <product>
        <name>Product2</name>
        <price>200</price>
    </product>
</products>";

            // Act
            var names = XmlHelper.GetValues(xml, "name");
            var prices = XmlHelper.GetValues(xml, "price");

            // Assert
            Assert.AreEqual(2, names.Length);
            Assert.AreEqual("Product1", names[0]);
            Assert.AreEqual("Product2", names[1]);

            Assert.AreEqual(2, prices.Length);
            Assert.AreEqual("100", prices[0]);
            Assert.AreEqual("200", prices[1]);
        }

        [TestMethod]
        public void GetContents_WithSpecialCharacters_HandlesCorrectly()
        {
            // Arrange
            var xml = @"<root><item attr=""value with &quot;quotes&quot;"">Content with &lt;tags&gt;</item></root>";

            // Act
            var result = XmlHelper.GetContents(xml, "item");

            // Assert
            Assert.AreEqual(1, result.Length);
            Assert.IsTrue(result[0].Contains("attr=\"value with &quot;quotes&quot;\""));
            Assert.IsTrue(result[0].Contains("Content with &lt;tags&gt;"));
        }

        #endregion

        #region Performance and Stress Tests

        [TestMethod]
        public void GetAttributes_LargeXml_ReturnsAllAttributes()
        {
            // Arrange
            var items = new List<string>();
            for (int i = 0; i < 1000; i++)
            {
                items.Add($"<item id='{i}' name='item{i}'/>");
            }
            var xml = $"<root>{string.Join("", items)}</root>";

            // Act
            var result = XmlHelper.GetAttributes(xml, "item");

            // Assert
            Assert.AreEqual(1000, result.Length);
            for (int i = 0; i < 1000; i++)
            {
                Assert.AreEqual(i.ToString(), result[i]["id"]);
                Assert.AreEqual($"item{i}", result[i]["name"]);
            }
        }

        #endregion
    }
}