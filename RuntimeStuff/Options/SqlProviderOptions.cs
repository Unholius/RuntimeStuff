// ***********************************************************************
// Assembly         : RuntimeStuff
// Author           : RS
// Created          : 01-06-2026
//
// Last Modified By : RS
// Last Modified On : 01-07-2026
// ***********************************************************************
// <copyright file="SqlProviderOptions.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************
namespace RuntimeStuff.Options
{
    using System;
    using System.Globalization;

    /// <summary>
    /// Class SqlProviderOptions. This class cannot be inherited.
    /// </summary>
    public sealed class SqlProviderOptions : OptionsBase<SqlProviderOptions>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SqlProviderOptions" /> class.
        /// </summary>
        public SqlProviderOptions()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlProviderOptions" /> class.
        /// </summary>
        /// <param name="configure">The configure.</param>
        public SqlProviderOptions(params Action<SqlProviderOptions>[] configure)
        {
            foreach (var setter in configure)
            {
                setter(this);
            }
        }

        /// <summary>
        /// Gets the sqlite options.
        /// </summary>
        /// <value>The sqlite options.</value>
        public static SqlProviderOptions SqliteOptions { get; } = new SqlProviderOptions(
            x => x.GetInsertedIdQuery = "SELECT last_insert_rowid()",
            x => x.OverrideOffsetRowsTemplate = "LIMIT {1} OFFSET {0}",
            x => x.TrueValue = "TRUE",
            x => x.FalseValue = "FALSE",
            x => x.ParamPrefix = ":");

        /// <summary>
        /// Gets the SQL server options.
        /// </summary>
        /// <value>The SQL server options.</value>
        public static SqlProviderOptions SqlServerOptions { get; } = new SqlProviderOptions(
            x => x.GetInsertedIdQuery = "SELECT SCOPE_IDENTITY()",
            x => x.OverrideOffsetRowsTemplate = "OFFSET {0} ROWS FETCH NEXT {1} ROWS ONLY",
            x => x.TrueValue = "1",
            x => x.FalseValue = "0",
            x => x.ParamPrefix = "@");

        /// <summary>
        /// Gets or sets the date format.
        /// </summary>
        /// <value>The date format.</value>
        public string DateFormat { get; set; } = "yyyy-MM-dd";

        /// <summary>
        /// Gets or sets the date time format.
        /// </summary>
        /// <value>The date time format.</value>
        public string DateTimeFormat { get; set; } = "yyyy-MM-dd HH:mm:ss";

        /// <summary>
        /// Gets or sets the false value.
        /// </summary>
        /// <value>The false value.</value>
        public string FalseValue { get; set; } = "0";

        /// <summary>
        /// Gets or sets the get inserted identifier query.
        /// </summary>
        /// <value>The get inserted identifier query.</value>
        public string GetInsertedIdQuery { get; set; }

        /// <summary>
        /// Gets or sets the map.
        /// </summary>
        /// <value>The map.</value>
        public EntityMap Map { get; set; }

        /// <summary>
        /// Gets or sets the name prefix.
        /// </summary>
        /// <value>The name prefix.</value>
        public string NamePrefix { get; set; } = "\"";

        /// <summary>
        /// Gets or sets the name suffix.
        /// </summary>
        /// <value>The name suffix.</value>
        public string NameSuffix { get; set; } = "\"";

        /// <summary>
        /// Gets or sets the null value.
        /// </summary>
        /// <value>The null value.</value>
        public string NullValue { get; set; } = "NULL";

        /// <summary>
        /// Gets or sets the override offset rows template.
        /// </summary>
        /// <value>The override offset rows template.</value>
        public string OverrideOffsetRowsTemplate { get; set; } = "OFFSET {0} ROWS FETCH NEXT {1} ROWS ONLY";

        /// <summary>
        /// Gets or sets the parameter prefix.
        /// </summary>
        /// <value>The parameter prefix.</value>
        public string ParamPrefix { get; set; } = ":";

        /// <summary>
        /// Gets or sets the statement terminator.
        /// </summary>
        /// <value>The statement terminator.</value>
        public string StatementTerminator { get; set; } = ";";

        /// <summary>
        /// Gets or sets the string prefix.
        /// </summary>
        /// <value>The string prefix.</value>
        public string StringPrefix { get; set; } = "'";

        /// <summary>
        /// Gets or sets the string suffix.
        /// </summary>
        /// <value>The string suffix.</value>
        public string StringSuffix { get; set; } = "'";

        /// <summary>
        /// Gets or sets the true value.
        /// </summary>
        /// <value>The true value.</value>
        public string TrueValue { get; set; } = "1";

        /// <summary>
        /// Gets the instance.
        /// </summary>
        /// <param name="sqlConnectionTypeName">Name of the SQL connection type.</param>
        /// <returns>SqlProviderOptions.</returns>
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

        /// <summary>
        /// Converts to sqlliteral.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>System.String.</returns>
        public string ToSqlLiteral(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return this.NullValue;
            }

            switch (value)
            {
                case string s:
                    return $"{this.StringPrefix}{EscapeString(s)}{this.StringSuffix}";

                case char c:
                    return $"{this.StringPrefix}{EscapeString(c.ToString())}{this.StringSuffix}";

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
                    return $"{this.StringPrefix}{EscapeString(value.ToString())}{this.StringSuffix}";
            }
        }

        /// <summary>
        /// Escapes the string.
        /// </summary>
        /// <param name="s">The s.</param>
        /// <returns>System.String.</returns>
        private static string EscapeString(string s) => s.Replace("'", "''");
    }
}