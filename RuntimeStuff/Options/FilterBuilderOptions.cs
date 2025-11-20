namespace RuntimeStuff.Options
{
    public class FilterBuilderOptions : OptionsBase<FilterBuilderOptions>
    {
        public FilterBuilderOptions() { }

        public FormatValueOptions FormatOptions { get; set; } = new FormatValueOptions();
    }
}