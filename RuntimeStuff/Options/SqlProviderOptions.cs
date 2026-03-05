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
    using System.Data;
    using RuntimeStuff.Data;

    /// <summary>
    /// Class SqlProviderOptions. This class cannot be inherited.
    /// </summary>
    public sealed class SqlProviderOptions : OptionsBase<SqlProviderOptions>
    {
        private DbEntityMap map = new DbEntityMap();

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
            x => x.ValueFormatter.NonNumberValuePrefix = "'",
            x => x.ValueFormatter.NonNumberValueSuffix = "'",
            x => x.ValueFormatter.EscapeMode = Helpers.StringHelper.EscapeMode.Sql,
            x => x.GetInsertedIdQuery = "SELECT last_insert_rowid()",
            x => x.OverrideOffsetRowsTemplate = "LIMIT {1} OFFSET {0}",
            x => x.ValueFormatter.TrueValue = "TRUE",
            x => x.ValueFormatter.FalseValue = "FALSE",
            x => x.ParamPrefix = ":",
            x => x.DatabaseParameterName = "Data Source",
            x => x.ServerParameterName = null,
            x => x.UserParameterName = null,
            x => x.PasswordParameterName = "Password",
            x => x.IntegratedSecurityParameterName = null,
            x => x.ApplicationNameParameterName = null,
            x => x.ConnectTimeoutParameterName = null);

        /// <summary>
        /// Gets the SQL server options.
        /// </summary>
        /// <value>The SQL server options.</value>
        public static SqlProviderOptions SqlServerOptions { get; } = new SqlProviderOptions(
            x => x.ValueFormatter.NonNumberValuePrefix = "'",
            x => x.ValueFormatter.NonNumberValueSuffix = "'",
            x => x.ValueFormatter.EscapeMode = Helpers.StringHelper.EscapeMode.Sql,
            x => x.GetInsertedIdQuery = "SELECT SCOPE_IDENTITY()",
            x => x.OverrideOffsetRowsTemplate = "OFFSET {0} ROWS FETCH NEXT {1} ROWS ONLY",
            x => x.ValueFormatter.TrueValue = "1",
            x => x.ValueFormatter.FalseValue = "0",
            x => x.ParamPrefix = "@");

        /// <summary>
        /// Сериализатор значений.
        /// </summary>
        public ValueFormatter ValueFormatter { get; } = new ValueFormatter();

        /// <summary>
        /// Gets or sets the get inserted identifier query.
        /// </summary>
        /// <value>The get inserted identifier query.</value>
        public string GetInsertedIdQuery { get; set; }

        /// <summary>
        /// Gets or sets the map.
        /// </summary>
        /// <value>The map.</value>
        public DbEntityMap Map
        {
            get
            {
                if (this.map == null)
                {
                    this.map = new DbEntityMap();
                }

                return this.map;
            }

            set => this.map = value;
        }

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
        /// Gets or sets имя параметра строки подключения, используемого для указания имени базы данных.
        /// </summary>
        public string DatabaseParameterName { get; set; } = "Database";

        /// <summary>
        /// Gets or sets имя параметра строки подключения, используемого для указания адреса или имени сервера базы данных.
        /// </summary>
        public string ServerParameterName { get; set; } = "Server";

        /// <summary>
        /// Gets or sets имя параметра строки подключения, используемого для указания имени пользователя базы данных.
        /// </summary>
        public string UserParameterName { get; set; } = "User";

        /// <summary>
        /// Gets or sets имя параметра строки подключения, используемого для указания пароля пользователя базы данных.
        /// </summary>
        public string PasswordParameterName { get; set; } = "Password";

        /// <summary>
        /// Gets or sets имя параметра строки подключения, используемого для указания режима интегрированной безопасности
        /// (аутентификация Windows).
        /// </summary>
        public string IntegratedSecurityParameterName { get; set; } = "Integrated Security";

        /// <summary>
        /// Gets or sets имя параметра строки подключения, используемого для указания имени приложения,
        /// от имени которого устанавливается соединение с базой данных.
        /// </summary>
        public string ApplicationNameParameterName { get; set; } = "Application Name";

        /// <summary>
        /// Gets or sets имя параметра строки подключения, используемого для указания необходимости доверять сертификату сервера
        /// без проверки цепочки доверия.
        /// </summary>
        public string TrustServerCertificateParameterName { get; set; } = "TrustServerCertificate";

        /// <summary>
        /// Gets or sets имя параметра строки подключения, используемого для указания тайм-аута подключения
        /// к серверу базы данных (в секундах).
        /// </summary>
        public string ConnectTimeoutParameterName { get; set; } = "Connect Timeout";

        /// <summary>
        /// Gets the instance.
        /// </summary>
        /// <param name="dbConnection">SQL connection type.</param>
        /// <returns>SqlProviderOptions.</returns>
        public static SqlProviderOptions GetInstance(IDbConnection dbConnection) => GetInstance(dbConnection.GetType().Name);

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
    }
}