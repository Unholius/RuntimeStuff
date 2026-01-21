using RuntimeStuff.Extensions;

namespace RuntimeStuff.MSTests;

[TestClass]
public class EnumerableExtensionsTests
{
    #region Tests for IndexOf<T>(this IEnumerable<T> e, Func<T, bool> match, bool reverseSearch = false)

    [TestMethod]
    public void IndexOf_WithMatchFunction_ReturnsFirstMatchingIndex()
    {
        // Arrange
        var collection = new List<int> { 1, 2, 3, 4, 5 };

        // Act
        var result = collection.IndexOf(x => x > 2);

        // Assert
        Assert.AreEqual(2, result);
    }

    [TestMethod]
    public void IndexOf_WithMatchFunction_ReturnsNegativeOneWhenNotFound()
    {
        // Arrange
        var collection = new List<int> { 1, 2, 3, 4, 5 };

        // Act
        var result = collection.IndexOf(x => x > 10);

        // Assert
        Assert.AreEqual(-1, result);
    }

    [TestMethod]
    public void IndexOf_WithMatchFunction_ReverseSearchReturnsLastMatchingIndex()
    {
        // Arrange
        var collection = new List<int> { 1, 2, 3, 2, 1 };

        // Act
        var result = collection.IndexOf(x => x == 2, reverseSearch: true);

        // Assert
        Assert.AreEqual(3, result);
    }

    [TestMethod]
    public void IndexOf_WithMatchFunction_HandlesEmptyCollection()
    {
        // Arrange
        var collection = new List<int>();

        // Act
        var result = collection.IndexOf(x => x > 0);

        // Assert
        Assert.AreEqual(-1, result);
    }

    #endregion Tests for IndexOf<T>(this IEnumerable<T> e, Func<T, bool> match, bool reverseSearch = false)

    #region Tests for IndexOf<T>(this IEnumerable<T> e, T item, int fromIndex)

    [TestMethod]
    public void IndexOf_WithItemAndStartIndex_ReturnsCorrectIndex()
    {
        // Arrange
        var collection = new List<int> { 1, 2, 3, 2, 1 };

        // Act
        var result = collection.IndexOf(2, 2);

        // Assert
        Assert.AreEqual(3, result);
    }

    [TestMethod]
    public void IndexOf_WithItemAndStartIndex_ReturnsNegativeOneWhenNotFound()
    {
        // Arrange
        var collection = new List<int> { 1, 2, 3, 4, 5 };

        // Act
        var result = collection.IndexOf(2, 3);

        // Assert
        Assert.AreEqual(-1, result);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void IndexOf_WithItemAndStartIndex_ThrowsArgumentNullExceptionForNullCollection()
    {
        // Arrange
        IEnumerable<int>? collection = null;

        // Act
        collection.IndexOf(1, 0);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void IndexOf_WithItemAndStartIndex_ThrowsArgumentOutOfRangeExceptionForNegativeIndex()
    {
        // Arrange
        var collection = new List<int> { 1, 2, 3 };

        // Act
        collection.IndexOf(1, -1);
    }

    #endregion Tests for IndexOf<T>(this IEnumerable<T> e, T item, int fromIndex)

    #region Tests for IndexOf<T>(this IEnumerable<T> e, T item, int fromIndex, IEqualityComparer<T> comparer)

    [TestMethod]
    public void IndexOf_WithCustomComparer_UsesComparerForEqualityCheck()
    {
        // Arrange
        var collection = new List<string> { "apple", "banana", "cherry" };
        var comparer = StringComparer.OrdinalIgnoreCase;

        // Act
        var result = collection.IndexOf("APPLE", 0, comparer);

        // Assert
        Assert.AreEqual(0, result);
    }

    [TestMethod]
    public void IndexOf_WithNullComparer_UsesDefaultComparer()
    {
        // Arrange
        var collection = new List<string> { "apple", "banana", "cherry" };

        // Act
        var result = collection.IndexOf("APPLE", 0, null);

        // Assert
        Assert.AreEqual(-1, result);
    }

    [TestMethod]
    public void IndexOf_WithCustomComparerAndStartIndex_ReturnsCorrectIndex()
    {
        // Arrange
        var collection = new List<string> { "a", "b", "a", "b", "a" };
        var comparer = EqualityComparer<string>.Default;

        // Act
        var result = collection.IndexOf("a", 2, comparer);

        // Assert
        Assert.AreEqual(2, result);
    }

    #endregion Tests for IndexOf<T>(this IEnumerable<T> e, T item, int fromIndex, IEqualityComparer<T> comparer)

    #region Tests for IndexOf<T>(this IEnumerable<T> e, Func<T, int, bool> match, bool reverseSearch = false)

    [TestMethod]
    public void IndexOf_WithIndexedMatchFunction_ReturnsFirstMatchingIndex()
    {
        // Arrange
        var collection = new List<int> { 10, 20, 30, 40, 50 };

        // Act
        var result = collection.IndexOf((x, i) => x > 25 && i > 1);

        // Assert
        Assert.AreEqual(2, result);
    }

    [TestMethod]
    public void IndexOf_WithIndexedMatchFunction_ReverseSearchReturnsLastMatchingIndex()
    {
        // Arrange
        var collection = new List<int> { 1, 2, 3, 2, 1 };

        // Act
        var result = collection.IndexOf((x, i) => x == 2 && i < 4, reverseSearch: true);

        // Assert
        Assert.AreEqual(3, result);
    }

    [TestMethod]
    public void IndexOf_WithIndexedMatchFunction_HandlesNullCollection()
    {
        // Arrange
        IEnumerable<int>? collection = null;

        // Act
        var result = collection.IndexOf((x, i) => x > 0);

        // Assert
        Assert.AreEqual(-1, result);
    }

    [TestMethod]
    public void IndexOf_WithIndexedMatchFunction_WorksWithIEnumerable()
    {
        // Arrange
        var collection = Enumerable.Range(1, 5);

        // Act
        var result = collection.IndexOf((x, i) => x == 3);

        // Assert
        Assert.AreEqual(2, result);
    }

    [TestMethod]
    public void IndexOf_WithIndexedMatchFunction_ReverseSearchWorksWithIEnumerable()
    {
        // Arrange
        var collection = Enumerable.Range(1, 5);

        // Act
        var result = collection.IndexOf((x, i) => x == 3, reverseSearch: true);

        // Assert
        Assert.AreEqual(2, result);
    }

    #endregion Tests for IndexOf<T>(this IEnumerable<T> e, Func<T, int, bool> match, bool reverseSearch = false)

    #region Tests for InvalidOperationException (Collection Modified During Enumeration)

    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void IndexOf_ThrowsInvalidOperationException_WhenCollectionModifiedDuringEnumeration()
    {
        // Arrange
        var collection = new List<int> { 1, 2, 3, 4, 5 };
        var enumerator = collection.GetEnumerator();

        // Act & Assert
        try
        {
            enumerator.MoveNext();
            collection.Add(6); // Модифицируем коллекцию во время перечисления
            enumerator.MoveNext(); // Это вызовет InvalidOperationException
        }
        finally
        {
            enumerator.Dispose();
        }
    }

    [TestMethod]
    public void IndexOf_WithArray_DoesNotThrowInvalidOperationException()
    {
        // Arrange
        var array = new[] { 1, 2, 3, 4, 5 };

        // Act
        var result = array.IndexOf(x => x == 3);

        // Assert
        Assert.AreEqual(2, result);
    }

    [TestMethod]
    public void IndexOf_WithList_DoesNotThrowInvalidOperationException()
    {
        // Arrange
        var list = new List<int> { 1, 2, 3, 4, 5 };

        // Act
        var result = list.IndexOf(x => x == 3);

        // Assert
        Assert.AreEqual(2, result);
    }

    #endregion Tests for InvalidOperationException (Collection Modified During Enumeration)

    #region Additional Edge Case Tests

    [TestMethod]
    public void IndexOf_WorksWithComplexObjects()
    {
        // Arrange
        var items = new List<TestPerson>
        {
            new() { Id = 1, Name = "Alice" },
            new() { Id = 2, Name = "Bob" },
            new() { Id = 3, Name = "Charlie" }
        };

        // Act
        var result = items.IndexOf(p => p.Name == "Bob");

        // Assert
        Assert.AreEqual(1, result);

        var csv = items.ToCsv(true, ",", ";");
        Assert.AreEqual("Id,Name;1,Alice;2,Bob;3,Charlie;", csv);
    }

    [TestMethod]
    public void IndexOf_WithZeroStartIndex_FindsFirstOccurrence()
    {
        // Arrange
        var collection = new List<int> { 1, 2, 3, 1, 2, 3 };

        // Act
        var result = collection.IndexOf(3, 0);

        // Assert
        Assert.AreEqual(2, result);
    }

    [TestMethod]
    public void IndexOf_ReverseSearchWithEmptyCollection_ReturnsNegativeOne()
    {
        // Arrange
        var collection = new List<int>();

        // Act
        var result = collection.IndexOf(x => true, reverseSearch: true);

        // Assert
        Assert.AreEqual(-1, result);
    }

    [TestMethod]
    public void IndexOf_WithSingleElementCollection_ReturnsCorrectIndex()
    {
        // Arrange
        var collection = new List<int> { 42 };

        // Act
        var result = collection.IndexOf(42, 0);

        // Assert
        Assert.AreEqual(0, result);
    }

    [TestMethod]
    public void IndexOf_WithLargeCollection_PerformsCorrectly()
    {
        // Arrange
        var collection = Enumerable.Range(0, 10000).ToList();

        // Act
        var result = collection.IndexOf(9999, 0);

        // Assert
        Assert.AreEqual(9999, result);
    }

    #endregion Additional Edge Case Tests

    #region Helper Class

    private class TestPerson
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    #endregion Helper Class
}