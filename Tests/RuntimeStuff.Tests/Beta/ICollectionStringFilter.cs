namespace RuntimeStuff.MSTests.Beta
{
    public interface ICollectionStringFilter<T>
    {
        IEnumerable<T> Filter(IEnumerable<T> list, string filterExpression);
    }
}
