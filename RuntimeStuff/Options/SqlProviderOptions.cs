namespace RuntimeStuff.Options
{
    using System;
    using System.Globalization;

    public sealed class SqlProviderOptions : OptionsBase<SqlProviderOptions>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SqlProviderOptions"/> class.
        /// </summary>
        public SqlProviderOptions()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlProviderOptions"/> class.
        /// </summary>
        /// <param name="configure"></param>
        public SqlProviderOptions(params Action<SqlProviderOptions>[] configure)
        {
            foreach (var setter in configure)
            {
                setter(this);
            }
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
            {
                return this.NullValue;
            }

            switch (value)
            {
                case string s:
                    return $"{this.StringPrefix}{this.EscapeString(s)}{this.StringSuffix}";

                case char c:
                    return $"{this.StringPrefix}{this.EscapeString(c.ToString())}{this.StringSuffix}";

                case bool b:
                    return b ? this.TrueValue : this.FalseValue;

                case DateTime dt:
                    return $"{this.StringPrefix}{dt.ToString(this.DateTimeFormat, CultureInfo.InvariantCulture)}{this.StringSuffix}";

                case DateTimeOffset dto:
                    return $"{this.StringPrefix}{dto.ToString(this.DateTimeFormat, CultureInfo.InvariantCulture)}{this.StringSuffix}";

                case Guid g:
                    return $"{this.StringPrefix}{g}{this.StringSuffix}";

                case Enum e:
                    return Convert.ToInt64(e).ToString(CultureInfo.InvariantCulture);

                case TimeSpan ts:
                    return $"{this.StringPrefix}{ts.ToString("c", CultureInfo.InvariantCulture)}{this.StringSuffix}";

                case IFormattable formattable:
                    return formattable.ToString(null, CultureInfo.InvariantCulture);

                default:
                    return $"{this.StringPrefix}{this.EscapeString(value.ToString())}{this.StringSuffix}";
            }
        }

        private string EscapeString(string s) => s.Replace("'", "''");
    }
}