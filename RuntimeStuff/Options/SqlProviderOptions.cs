using System;
using System.Globalization;

namespace RuntimeStuff.Options
{
    public sealed class SqlProviderOptions : OptionsBase<SqlProviderOptions>
    {
        public SqlProviderOptions()
        {
        }

        public SqlProviderOptions(params Action<SqlProviderOptions>[] configure)
        {
            foreach (var setter in configure)
                setter(this);
        }

        public EntityMap Map { get; set; }

        public string NamePrefix { get; set; } = "\"";
        public string NameSuffix { get; set; } = "\"";
        public string DateTimeFormat { get; set; } = "yyyy-MM-dd HH:mm:ss";
        public string DateFormat { get; set; } = "yyyy-MM-dd";
        public string ParamPrefix { get; set; } = ":";
        public string GetInsertedIdQuery { get; set; }
        public string TrueValue { get; set; } = "1";
        public string FalseValue { get; set; } = "0";
        public string NullValue { get; set; } = "NULL";
        public string OverrideOffsetRowsTemplate { get; set; } = "OFFSET {0} ROWS FETCH NEXT {1} ROWS ONLY";
        public string StringPrefix { get; set; } = "'";
        public string StringSuffix { get; set; } = "'";
        public string StatementTerminator { get; set; } = ";";

        public static SqlProviderOptions GetInstance(string sqlConnectionTypeName)
        {
            switch (sqlConnectionTypeName.ToLower())
            {
                case "sqlconnection":
                    return SqlServerOptions;

                case "sqliteconnection":
                    return SqliteOptions;

                default:
                    return Default;
            }
        }

        public static SqlProviderOptions SqlServerOptions { get; } = new SqlProviderOptions(
            x => x.GetInsertedIdQuery = "SELECT SCOPE_IDENTITY()",
            x => x.OverrideOffsetRowsTemplate = "OFFSET {0} ROWS FETCH NEXT {1} ROWS ONLY",
            x => x.TrueValue = "1",
            x => x.FalseValue = "0",
            x => x.ParamPrefix = "@"
            );
        public static SqlProviderOptions SqliteOptions { get; } = new SqlProviderOptions(
            x => x.GetInsertedIdQuery = "SELECT last_insert_rowid()", 
            x => x.OverrideOffsetRowsTemplate = "LIMIT {1} OFFSET {0}",
            x => x.TrueValue = "TRUE",
            x => x.FalseValue = "FALSE",
            x => x.ParamPrefix = ":"
            );

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
                    return $"{StringPrefix}{dt.ToString(DateTimeFormat, CultureInfo.InvariantCulture)}{StringSuffix}";

                case DateTimeOffset dto:
                    return $"{StringPrefix}{dto.ToString(DateTimeFormat, CultureInfo.InvariantCulture)}{StringSuffix}";

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