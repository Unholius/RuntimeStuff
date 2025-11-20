namespace RuntimeStuff.Options
{
    public interface IHaveOptions<out T> : IHaveOptions where T : OptionsBase<T>, new()
    {
        new T Options { get; }
    }

    public interface IHaveOptions
    {
        OptionsBase Options { get; set; }
    }
}