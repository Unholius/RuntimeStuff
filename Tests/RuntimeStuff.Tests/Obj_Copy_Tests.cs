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

    public class TargetClass
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
        var target = new TargetClass();

        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(() =>
            Obj.Copy<SourceClass, TargetClass>(null, target));
    }

    [TestMethod]
    public void Copy_ThrowsArgumentNullException_WhenTargetinationIsNull()
    {
        // Arrange
        var source = new SourceClass();

        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(() =>
            Obj.Copy<SourceClass, TargetClass>(source, null));
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

        var target = new TargetClass
        {
            AdditionalProperty = "Existing value"
        };

        // Act
        Obj.Copy(source, target);

        // Assert
        Assert.AreEqual(source.Name, target.Name);
        Assert.AreEqual(source.Age, target.Age);
        Assert.AreEqual(source.Value, target.Value);
        Assert.AreEqual("Existing value", target.AdditionalProperty); // Не должно измениться
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

        var target = new TargetClass();

        // Act
        Obj.Copy(source, target, "Name", "Age");

        // Assert
        Assert.AreEqual(source.Name, target.Name);
        Assert.AreEqual(source.Age, target.Age);
        Assert.AreEqual(0, target.Value); // Не должно быть скопировано
        Assert.IsNull(target.AdditionalProperty); // Не должно быть скопировано
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
        var target = new TargetClass();

        // Act
        Obj.Copy(source, target, new string[0]);

        // Assert - должен скопировать все свойства
        Assert.AreEqual(source.Name, target.Name);
        Assert.AreEqual(source.Age, target.Age);
        Assert.AreEqual(source.Value, target.Value);
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

        var targetList = new List<SimpleItem>
        {
            new() { Text = "Old1", Number = 0 },
            new() { Text = "Old2", Number = 0 }
        };

        // Act
        Obj.Copy(sourceList, targetList);

        // Assert
        Assert.AreEqual(3, targetList.Count);
        for (var i = 0; i < sourceList.Count; i++)
        {
            Assert.AreEqual(sourceList[i].Text, targetList[i].Text);
            Assert.AreEqual(sourceList[i].Number, targetList[i].Number);
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

        var targetList = new List<SimpleItem>();

        // Act
        Obj.Copy(sourceList, targetList);

        // Assert
        Assert.AreEqual(2, targetList.Count);
        for (var i = 0; i < sourceList.Count; i++)
        {
            Assert.AreEqual(sourceList[i].Text, targetList[i].Text);
            Assert.AreEqual(sourceList[i].Number, targetList[i].Number);
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

        var targetList = new List<SimpleItem>();

        // Act
        Obj.Copy(sourceArray, targetList);

        // Assert
        Assert.AreEqual(2, targetList.Count);
        Assert.AreEqual("ArrayItem1", targetList[0].Text);
        Assert.AreEqual("ArrayItem2", targetList[1].Text);
    }

    [TestMethod]
    public void Copy_ThrowsInvalidOperationException_ForNonIListTargetination()
    {
        // Arrange
        var sourceList = new List<SimpleItem>
        {
            new() { Text = "Test", Number = 1 }
        };

        var targetHashSet = new HashSet<SimpleItem>();

        // Act & Assert
        Assert.ThrowsException<InvalidOperationException>(() =>
            Obj.Copy(sourceList, targetHashSet));
    }

    [TestMethod]
    public void Copy_HandlesNullCollections()
    {
        // Arrange
        List<SimpleItem> sourceList = null;
        var targetList = new List<SimpleItem>();

        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(() =>
            Obj.Copy(sourceList, targetList));
    }
}