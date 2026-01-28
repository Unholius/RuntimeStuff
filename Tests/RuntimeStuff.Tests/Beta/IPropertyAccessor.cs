namespace RuntimeStuff.MSTests.Beta
{
    public interface IPropertyAccessor
    {
        Func<object> Get { get; }
        Action<object, object> Set { get; }
    }
}
