using RuntimeStuff.Options;
using System;
using System.Globalization;

namespace RuntimeStuff
{
    public sealed class SqlProviderOptions : OptionsBase<SqlProviderOptions>
    {
        public SqlProviderOptions()
        {
        }

        public SqlProviderOptions(Action<SqlProviderOptions> configure)
        {
            configure?.Invoke(this);
        }

        public string NamePrefix { get; set; } = "\"";
        public string NameSuffix { get; set; } = "\"";
        public string DateFormat { get; set; } = "yyyyMMddTHH:mm:ss";
        public string ParamPrefix { get; set; } = "@";
        public string GetInsertedIdQuery { get; set; } = "SELECT SCOPE_IDENTITY()";
        public string TrueValue { get; set; } = "1";
        public string FalseValue { get; set; } = "0";
        public string NullValue { get; set; } = "NULL";
        private string StringPrefix { get; set; } = "'";
        public string StringSuffix { get; set; } = "'";

        public string ToSqlLiteral(object value)
        {
            if (value == null || value == DBNull.Value)
                return NullValue;

            switch (value)
            {
                case string s:
                    return $"{StringPrefix}{EscapeString(s)}{StringSuffix}";

                case char c:
                    return $"{StringPrefix}{EscapeString(c.ToString())}{StringSuffix}";

                case bool b:
                    return b ? TrueValue : FalseValue;

                case DateTime dt:
                    return $"{StringPrefix}{dt.ToString(DateFormat, CultureInfo.InvariantCulture)}{StringSuffix}";

                case DateTimeOffset dto:
                    return $"{StringPrefix}{dto.ToString(DateFormat, CultureInfo.InvariantCulture)}{StringSuffix}";

                case Guid g:
                    return $"{StringPrefix}{g}{StringSuffix}";

                case Enum e:
                    return Convert.ToInt64(e).ToString(CultureInfo.InvariantCulture);

                case TimeSpan ts:
                    return $"{StringPrefix}{ts.ToString("c", CultureInfo.InvariantCulture)}{StringSuffix}";

                case IFormattable formattable:
                    return formattable.ToString(null, CultureInfo.InvariantCulture);

                default:
                    return $"{StringPrefix}{EscapeString(value.ToString())}{StringSuffix}";
            }
        }

        private string EscapeString(string s)
        {
            return s.Replace("'", "''");
        }
    }
}