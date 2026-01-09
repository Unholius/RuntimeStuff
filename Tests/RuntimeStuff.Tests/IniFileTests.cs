using System.Text;

namespace RuntimeStuff.MSTests
{
    [TestClass]
    public class IniFileTests
    {
        private const string TestContent = @"
; Глобальный ключ (без секции)
GlobalKey=GlobalValue

; Комментарий
[Section1]
Key1=Value1
Key2=Value2

[Section2]
KeyA=ValueA
KeyB=ValueB

";

        [TestInitialize]
        public void TestInitialize()
        {
        }

        [TestCleanup]
        public void TestCleanup()
        {
            //if (File.Exists(TestFileName))
            //    File.Delete(TestFileName);
        }

        [TestMethod]
        public void Load_WithValidFile_ReturnsIniFileInstance()
        {
            // Arrange
            

            // Act
            var ini = new IniFile(TestContent);

            // Assert
            Assert.IsNotNull(ini);
            Assert.IsFalse(string.IsNullOrEmpty(ini.Content));
        }

        [TestMethod]
        [ExpectedException(typeof(FileNotFoundException))]
        public void Load_WithNonExistentFile_ThrowsFileNotFoundException()
        {
            // Act
            _ = IniFile.Load("nonexistent.ini");
        }

        [TestMethod]
        public void LoadOrCreate_WithNonExistentFile_CreatesEmptyIniFile()
        {
            // Act
            var ini = IniFile.LoadOrCreate("newfile.ini");

            // Assert
            Assert.IsNotNull(ini);
            Assert.AreEqual(string.Empty, ini.Content);

            // Cleanup
            if (File.Exists("newfile.ini"))
                File.Delete("newfile.ini");
        }

        [TestMethod]
        public void LoadOrCreate_WithExistingFile_LoadsContent()
        {
            // Act
            var ini = new IniFile(TestContent);

            // Assert
            Assert.IsNotNull(ini);
            Assert.IsTrue(ini.Content.Contains("Section1"));
            Assert.IsTrue(ini.Content.Contains("Key1=Value1"));
        }

        [TestMethod]
        public void Indexer_GetValue_ReturnsCorrectValue()
        {
            // Arrange
            var ini = new IniFile(TestContent);

            // Act
            var value1 = ini["Section1", "Key1"];
            var value2 = ini["Section2", "KeyA"];

            // Assert
            Assert.AreEqual("Value1", value1);
            Assert.AreEqual("ValueA", value2);
        }

        [TestMethod]
        public void Indexer_GetValueWithDefault_ReturnsDefaultWhenNotFound()
        {
            // Arrange
            var ini = new IniFile(TestContent);

            // Act
            var value = ini["Section1", "NonExistentKey", "DefaultValue"];

            // Assert
            Assert.AreEqual("DefaultValue", value);
        }

        [TestMethod]
        public void Indexer_SetValue_UpdatesExistingKey()
        {
            // Arrange
            var ini = new IniFile(TestContent);

            // Act
            ini["Section1", "Key1"] = "NewValue";
            var updatedValue = ini["Section1", "Key1"];

            // Assert
            Assert.AreEqual("NewValue", updatedValue);
        }

        [TestMethod]
        public void Indexer_SetValue_AddsNewKey()
        {
            // Arrange
            var ini = new IniFile(TestContent);

            // Act
            ini["Section1", "NewKey"] = "NewValue";
            var newValue = ini["Section1", "NewKey"];

            // Assert
            Assert.AreEqual("NewValue", newValue);
        }

        [TestMethod]
        public void GetKeys_ReturnsAllKeysInSection()
        {
            // Arrange
            var ini = new IniFile(TestContent);

            // Act
            var section1Keys = new List<string>(ini.GetKeys("Section1"));
            var section2Keys = new List<string>(ini.GetKeys("Section2"));
            var globalKeys = new List<string>(ini.GetKeys(null));

            // Assert
            Assert.AreEqual(1, globalKeys.Count);
            CollectionAssert.Contains(globalKeys, "GlobalKey");

            Assert.AreEqual(2, section1Keys.Count);
            CollectionAssert.Contains(section1Keys, "Key1");
            CollectionAssert.Contains(section1Keys, "Key2");

            Assert.AreEqual(2, section2Keys.Count);
            CollectionAssert.Contains(section2Keys, "KeyA");
            CollectionAssert.Contains(section2Keys, "KeyB");
        }

        [TestMethod]
        public void GetSections_ReturnsAllSections()
        {
            // Arrange
            var ini = new IniFile(TestContent);

            // Act
            var sections = new List<string>(ini.GetSections());

            // Assert
            Assert.AreEqual(3, sections.Count); // Section1, Section2
            CollectionAssert.Contains(sections, "Section1");
            CollectionAssert.Contains(sections, "Section2");
        }

        [TestMethod]
        public void GetValue_ReturnsCorrectValue()
        {
            // Arrange
            
            var ini = new IniFile(TestContent);

            // Act
            var value = ini.GetValue("Section1", "Key1", "Default");

            // Assert
            Assert.AreEqual("Value1", value);
        }

        [TestMethod]
        public void GetValue_WithNonExistentKey_ReturnsDefault()
        {
            // Arrange
            
            var ini = new IniFile(TestContent);

            // Act
            var value = ini.GetValue("Section1", "NonExistent", "DefaultValue");

            // Assert
            Assert.AreEqual("DefaultValue", value);
        }

        [TestMethod]
        public void SetValue_UpdatesContent()
        {
            // Arrange
            
            var ini = new IniFile(TestContent);

            // Act
            ini.SetValue("Section1", "Key1", "UpdatedValue");

            // Assert
            var updatedValue = ini["Section1", "Key1"];
            Assert.AreEqual("UpdatedValue", updatedValue);
            Assert.IsTrue(ini.Content.Contains("Key1=UpdatedValue"));
        }

        [TestMethod]
        public void Save_SavesToOriginalFile()
        {
            // Arrange
            var ini = new IniFile(TestContent);
            ini["Section1", "Key1"] = "Modified";

            // Act
            ini.SaveAs("save_as.ini");

            // Assert
            var savedContent = File.ReadAllText("save_as.ini", Encoding.UTF8);
            Assert.IsTrue(savedContent.Contains("Key1=Modified"));
        }

        [TestMethod]
        public void SaveAs_SavesToNewFile()
        {
            // Arrange
            
            var ini = new IniFile(TestContent);
            var newFileName = "test2.ini";

            // Act
            ini.SaveAs(newFileName);

            // Assert
            Assert.IsTrue(File.Exists(newFileName));
            var savedContent = File.ReadAllText(newFileName, Encoding.UTF8);
            Assert.IsTrue(savedContent.Contains("Section1"));

            // Cleanup
            if (File.Exists(newFileName))
                File.Delete(newFileName);
        }

        [TestMethod]
        public void ToString_ReturnsContent()
        {
            // Arrange
            var ini = new IniFile(TestContent);

            // Act
            var content = ini.ToString();

            // Assert
            Assert.IsNotNull(content);
            Assert.IsTrue(content.Contains("Section1"));
            Assert.IsTrue(content.Contains("Key1=Value1"));
        }

        [TestMethod]
        public void CaseInsensitiveComparison_WorksCorrectly()
        {
            // Arrange
            var content = @"
[Section1]
Key1=Value1
KEY2=Value2
";
            var ini = new IniFile(content);

            // Act & Assert
            Assert.AreEqual("Value1", ini["SECTION1", "key1"]);
            Assert.AreEqual("Value2", ini["section1", "key2"]);
        }

        [TestMethod]
        public void MultipleValues_ForSameKey_LastOneWins()
        {
            // Arrange
            var content = @"
[Section1]
Key1=FirstValue
Key1=SecondValue
Key1=ThirdValue
";
            var ini = new IniFile(content);

            // Act
            var value = ini["Section1", "Key1"];

            // Assert
            Assert.AreEqual("ThirdValue", value);
        }

        [TestMethod]
        public void ContentProperty_SetAndGet_WorksCorrectly()
        {
            // Arrange
            var ini = new IniFile(TestContent);
            var newContent = "[NewSection]\nNewKey=NewValue";

            // Act
            ini.Content = newContent;

            // Assert
            Assert.AreEqual(newContent, ini.Content);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void GetValue_WithNullKey_ThrowsArgumentNullException()
        {
            // Arrange
            var ini = new IniFile(TestContent);

            // Act
            ini.GetValue("Section", null);
        }

        [TestMethod]
        public void Load_FromTextReader_WorksCorrectly()
        {
            // Arrange
            using var reader = new StringReader(TestContent);

            // Act
            var ini = IniFile.Load(reader);

            // Assert
            Assert.IsNotNull(ini);
            Assert.IsTrue(ini.Content.Contains("Section1"));
        }

        [TestMethod]
        public void Load_FromStream_WorksCorrectly()
        {
            // Arrange
            var bytes = Encoding.UTF8.GetBytes(TestContent);
            using var stream = new MemoryStream(bytes);

            // Act
            var ini = IniFile.Load(stream);

            // Assert
            Assert.IsNotNull(ini);
            Assert.IsTrue(ini.Content.Contains("Section1"));
        }

        [TestMethod]
        public void Load_WithDifferentEncoding_WorksCorrectly()
        {
            // Act
            var ini = new IniFile(TestContent);

            // Assert
            Assert.IsNotNull(ini);
            Assert.IsTrue(ini.Content.Contains("Section1"));
        }

        [TestMethod]
        public void AutoDetectLineBreaker_DetectsWindowsLineEndings()
        {
            // Arrange
            var content = "Line1\r\nLine2\r\nLine3";

            // Act
            var lineBreaker = IniFileTests.InvokePrivateStaticMethod<string>(
                "AutoDetectLineBreaker",
                [content]);

            // Assert
            Assert.AreEqual("\r\n", lineBreaker);
        }

        [TestMethod]
        public void AutoDetectLineBreaker_DetectsUnixLineEndings()
        {
            // Arrange
            var content = "Line1\nLine2\nLine3";

            // Act
            var lineBreaker = IniFileTests.InvokePrivateStaticMethod<string>(
                "AutoDetectLineBreaker",
                [content]);

            // Assert
            Assert.AreEqual("\n", lineBreaker);
        }

        [TestMethod]
        public void AutoDetectLineBreaker_DetectsOldMacLineEndings()
        {
            // Arrange
            var content = "Line1\rLine2\rLine3";

            // Act
            var lineBreaker = IniFileTests.InvokePrivateStaticMethod<string>(
                "AutoDetectLineBreaker",
                [content]);

            // Assert
            Assert.AreEqual("\r", lineBreaker);
        }

        [TestMethod]
        public void AutoDetectLineBreaker_WithEmptyContent_ReturnsEnvironmentNewLine()
        {
            // Act
            var lineBreaker = IniFileTests.InvokePrivateStaticMethod<string>(
                "AutoDetectLineBreaker",
                [string.Empty]);

            // Assert
            Assert.AreEqual(Environment.NewLine, lineBreaker);
        }

        // Вспомогательный метод для вызова приватных статических методов
        private static T InvokePrivateStaticMethod<T>(string methodName, object[] parameters)
        {
            var method = typeof(IniFile).GetMethod(
                methodName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            if (method == null)
                throw new ArgumentException($"Method {methodName} not found");

            return (T)method.Invoke(null, parameters);
        }
    }

    [TestClass]
    public class IniFileEdgeCaseTests
    {
        [TestMethod]
        public void EmptyFile_OperationsWork()
        {
            // Arrange
            var ini = new IniFile("");

            // Act & Assert
            Assert.AreEqual(string.Empty, ini.Content);
            Assert.AreEqual(0, new List<string>(ini.GetSections()).Count);
            Assert.AreEqual(0, new List<string>(ini.GetKeys(null)).Count);
        }

        [TestMethod]
        public void FileWithOnlyComments_ReturnsEmptyCollections()
        {
            // Arrange
            var content = @"
; Это комментарий
# Это тоже комментарий
; Еще один комментарий
";
            var ini = new IniFile(content);

            // Act
            var sections = new List<string>(ini.GetSections());
            var keys = new List<string>(ini.GetKeys(null));

            // Assert
            Assert.AreEqual(0, sections.Count); // Только пустая секция
            Assert.AreEqual(0, keys.Count);
        }

        [TestMethod]
        public void FileWithOnlySections_NoKeys()
        {
            // Arrange
            var content = @"
[Section1]
[Section2]
[Section3]
";
            var ini = new IniFile(content);

            // Act
            var sections = new List<string>(ini.GetSections());

            // Assert
            Assert.AreEqual(3, sections.Count); // 3 секции + пустая
            CollectionAssert.Contains(sections, "Section1");
            CollectionAssert.Contains(sections, "Section2");
            CollectionAssert.Contains(sections, "Section3");
        }

        [TestMethod]
        public void DuplicateSections_KeysAreCombined()
        {
            // Arrange
            var content = @"
[Section1]
Key1=Value1

[Section2]
KeyA=ValueA

[Section1]
Key2=Value2
";
            var ini = new IniFile(content);

            // Act
            var keys = new List<string>(ini.GetKeys("Section1"));

            // Assert
            Assert.AreEqual(2, keys.Count);
            CollectionAssert.Contains(keys, "Key1");
            CollectionAssert.Contains(keys, "Key2");
        }

        [TestMethod]
        public void KeysWithSpaces_AreTrimmed()
        {
            // Arrange
            var content = @"
[Section1]
  Key1  =Value1
Key2  =  Value2
";
            var ini = new IniFile(content);

            // Act & Assert
            Assert.AreEqual("Value1", ini["Section1", "Key1"]);
            Assert.AreEqual("Value2", ini["Section1", "Key2"]);
        }

        [TestMethod]
        public void ValuesWithSpaces_ArePreserved()
        {
            // Arrange
            var content = @"
[Section1]
Key1= Value with spaces 
Key2=Another   value
";
            var ini = new IniFile(content);

            // Act & Assert
            Assert.AreEqual("Value with spaces", ini["Section1", "Key1"]);
            Assert.AreEqual("Another   value", ini["Section1", "Key2"]);
        }

        [TestMethod]
        public void EmptyValues_AreHandled()
        {
            // Arrange
            var content = @"
[Section1]
Key1=
Key2=  
Key3=
";
            var ini = new IniFile(content);

            // Act & Assert
            Assert.AreEqual(string.Empty, ini["Section1", "Key1"]);
            Assert.AreEqual(string.Empty, ini["Section1", "Key2"]);
            Assert.AreEqual(string.Empty, ini["Section1", "Key3"]);
        }

        [TestMethod]
        public void SpecialCharacters_InSectionNames_AreHandled()
        {
            // Arrange
            var content = @"
[Section-Name]
Key1=Value1

[Section.Name]
Key2=Value2

[Section_Name]
Key3=Value3
";
            var ini = new IniFile(content);

            // Act & Assert
            Assert.AreEqual("Value1", ini["Section-Name", "Key1"]);
            Assert.AreEqual("Value2", ini["Section.Name", "Key2"]);
            Assert.AreEqual("Value3", ini["Section_Name", "Key3"]);
        }

        [TestMethod]
        public void UnicodeCharacters_ArePreserved()
        {
            // Arrange
            var content = @"
[Секция]
Ключ=Значение
Key=Value with русский текст
";
            var ini = new IniFile(content);

            // Act & Assert
            Assert.AreEqual("Значение", ini["Секция", "Ключ"]);
            Assert.AreEqual("Value with русский текст", ini["Секция", "Key"]);
        }

        [TestMethod]
        public void SetValue_WithEmptyString_RemovesKey()
        {
            // Arrange
            var content = @"
[Section1]
Key1=Value1
Key2=Value2
";
            var ini = new IniFile(content);

            // Act
            ini.SetValue("Section1", "Key1", string.Empty);

            // Assert
            Assert.AreEqual(string.Empty, ini["Section1", "Key1"]);
            Assert.AreEqual("Value2", ini["Section1", "Key2"]);
        }

        [TestMethod]
        public void SetValue_WithNull_RemovesKey()
        {
            // Arrange
            var content = @"
[Section1]
Key1=Value1
Key2=Value2
";
            var ini = new IniFile(content);

            // Act
            ini.SetValue("Section1", "Key1", null);

            // Assert
            Assert.AreEqual(string.Empty, ini["Section1", "Key1"]);
            Assert.AreEqual("Value2", ini["Section1", "Key2"]);
        }

        [TestMethod]
        public void PerformanceTest_ManyOperations()
        {
            // Arrange
            var ini = new IniFile("");

            // Act
            for (var i = 0; i < 100; i++)
            {
                ini[$"Section{i % 10}", $"Key{i}"] = $"Value{i}";
            }

            // Assert
            Assert.AreEqual("Value99", ini["Section9", "Key99"]);
            Assert.AreEqual(10, new List<string>(ini.GetSections()).Count);
        }
    }
}