namespace RuntimeStuff.Options
{
    public class FormatValueOptions : OptionsBase<FormatValueOptions>
    {
        public FormatValueOptions() { }
        public string DateFormat { get; set; } = "yyyy-MM-dd HH:mm:ss";
        public string TrueString { get; set; } = "1";
        public string FalseString { get; set; } = "0";
        public string StringValuePrefix { get; set; } = "'";
        public string StringValueSuffix { get; set; } = "'";
    }
}