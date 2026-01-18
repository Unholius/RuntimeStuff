using RuntimeStuff.Helpers;

namespace RuntimeStuff.MSTests;

[TestClass]
public class ObjCopyTests
{
    // Тестовые классы для проверки копирования свойств
    public class SourceClass
    {
        public string? Name { get; set; }
        public int Age { get; set; }
        public double Value { get; set; }
        public string? IgnoredProperty { get; set; }
    }

    public class DestClass
    {
        public string? Name { get; set; }
        public int Age { get; set; }
        public double Value { get; set; }
        public string? AdditionalProperty { get; set; }
    }

    public class Person
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
    }

    public class Employee
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Position { get; set; }
    }

    // Тестовый класс для коллекций
    public class SimpleItem
    {
        public string? Text { get; set; }
        public int Number { get; set; }
    }

    [TestMethod]
    public void Copy_ThrowsArgumentNullException_WhenSourceIsNull()
    {
        // Arrange
        var dest = new DestClass();

        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(() =>
            Obj.Copy<SourceClass, DestClass>(null, dest));
    }

    [TestMethod]
    public void Copy_ThrowsArgumentNullException_WhenDestinationIsNull()
    {
        // Arrange
        var source = new SourceClass();

        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(() =>
            Obj.Copy<SourceClass, DestClass>(source, null));
    }

    [TestMethod]
    public void Copy_CopiesAllProperties_WhenNoMemberNamesSpecified()
    {
        // Arrange
        var source = new SourceClass
        {
            Name = "Test",
            Age = 25,
            Value = 99.99,
            IgnoredProperty = "Should not be copied"
        };

        var dest = new DestClass
        {
            AdditionalProperty = "Existing value"
        };

        // Act
        Obj.Copy(source, dest);

        // Assert
        Assert.AreEqual(source.Name, dest.Name);
        Assert.AreEqual(source.Age, dest.Age);
        Assert.AreEqual(source.Value, dest.Value);
        Assert.AreEqual("Existing value", dest.AdditionalProperty); // Не должно измениться
    }

    [TestMethod]
    public void Copy_CopiesOnlySpecifiedProperties_WhenMemberNamesProvided()
    {
        // Arrange
        var source = new SourceClass
        {
            Name = "Test",
            Age = 30,
            Value = 100.0,
            IgnoredProperty = "Should not be copied"
        };

        var dest = new DestClass();

        // Act
        Obj.Copy(source, dest, "Name", "Age");

        // Assert
        Assert.AreEqual(source.Name, dest.Name);
        Assert.AreEqual(source.Age, dest.Age);
        Assert.AreEqual(0, dest.Value); // Не должно быть скопировано
        Assert.IsNull(dest.AdditionalProperty); // Не должно быть скопировано
    }

    [TestMethod]
    public void Copy_HandlesDifferentTypesWithSamePropertyNames()
    {
        // Arrange
        var person = new Person
        {
            FirstName = "John",
            LastName = "Doe"
        };

        var employee = new Employee();

        // Act
        Obj.Copy(person, employee, "FirstName", "LastName");

        // Assert
        Assert.AreEqual(person.FirstName, employee.FirstName);
        Assert.AreEqual(person.LastName, employee.LastName);
        Assert.IsNull(employee.Position);
    }

    [TestMethod]
    public void Copy_WorksWithEmptyMemberNamesArray()
    {
        // Arrange
        var source = new SourceClass { Name = "Test" };
        var dest = new DestClass();

        // Act
        Obj.Copy(source, dest, new string[0]);

        // Assert - должен скопировать все свойства
        Assert.AreEqual(source.Name, dest.Name);
        Assert.AreEqual(source.Age, dest.Age);
        Assert.AreEqual(source.Value, dest.Value);
    }

    [TestMethod]
    public void Copy_CopiesCollectionsWithSameType()
    {
        // Arrange
        var sourceList = new List<SimpleItem>
        {
            new() { Text = "Item1", Number = 1 },
            new() { Text = "Item2", Number = 2 },
            new() { Text = "Item3", Number = 3 }
        };

        var destList = new List<SimpleItem>
        {
            new() { Text = "Old1", Number = 0 },
            new() { Text = "Old2", Number = 0 }
        };

        // Act
        Obj.Copy(sourceList, destList);

        // Assert
        Assert.AreEqual(3, destList.Count);
        for (int i = 0; i < sourceList.Count; i++)
        {
            Assert.AreEqual(sourceList[i].Text, destList[i].Text);
            Assert.AreEqual(sourceList[i].Number, destList[i].Number);
        }
    }

    [TestMethod]
    public void Copy_CopiesCollectionsWithDifferentTypes()
    {
        // Arrange
        var sourceList = new List<SimpleItem>
        {
            new() { Text = "Item1", Number = 1 },
            new() { Text = "Item2", Number = 2 }
        };

        var destList = new List<SimpleItem>();

        // Act
        Obj.Copy(sourceList, destList);

        // Assert
        Assert.AreEqual(2, destList.Count);
        for (int i = 0; i < sourceList.Count; i++)
        {
            Assert.AreEqual(sourceList[i].Text, destList[i].Text);
            Assert.AreEqual(sourceList[i].Number, destList[i].Number);
        }
    }

    [TestMethod]
    public void Copy_HandlesArrayCollections()
    {
        // Arrange
        var sourceArray = new SimpleItem[]
        {
            new() { Text = "ArrayItem1", Number = 10 },
            new() { Text = "ArrayItem2", Number = 20 }
        };

        var destList = new List<SimpleItem>();

        // Act
        Obj.Copy(sourceArray, destList);

        // Assert
        Assert.AreEqual(2, destList.Count);
        Assert.AreEqual("ArrayItem1", destList[0].Text);
        Assert.AreEqual("ArrayItem2", destList[1].Text);
    }

    [TestMethod]
    public void Copy_ThrowsInvalidOperationException_ForNonIListDestination()
    {
        // Arrange
        var sourceList = new List<SimpleItem>
        {
            new() { Text = "Test", Number = 1 }
        };

        var destHashSet = new HashSet<SimpleItem>();

        // Act & Assert
        Assert.ThrowsException<InvalidOperationException>(() =>
            Obj.Copy(sourceList, destHashSet));
    }

    [TestMethod]
    public void Copy_HandlesNullCollections()
    {
        // Arrange
        List<SimpleItem> sourceList = null;
        var destList = new List<SimpleItem>();

        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(() =>
            Obj.Copy(sourceList, destList));
    }
}