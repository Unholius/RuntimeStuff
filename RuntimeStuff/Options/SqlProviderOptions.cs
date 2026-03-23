// <copyright file="SqlProviderOptions.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace RuntimeStuff.Options
{
    using System;
    using System.Data;
    using RuntimeStuff.Data;

    /// <summary>
    /// Опции провайдера SQL, определяющие особенности синтаксиса,
    /// форматирования значений и построения запросов для конкретной СУБД.
    /// </summary>
    public sealed class SqlProviderOptions : OptionsBase<SqlProviderOptions>
    {
        private DbEntityMap map = new DbEntityMap();

        /// <summary>
        /// Инициализирует новый экземпляр класса <see cref="SqlProviderOptions"/>.
        /// </summary>
        public SqlProviderOptions()
        {
        }

        /// <summary>
        /// Инициализирует новый экземпляр класса <see cref="SqlProviderOptions"/>
        /// с применением набора конфигураций.
        /// </summary>
        /// <param name="configure">Массив делегатов для настройки опций.</param>
        public SqlProviderOptions(params Action<SqlProviderOptions>[] configure)
        {
            foreach (var setter in configure)
            {
                setter(this);
            }
        }

        /// <summary>
        /// Преднастроенные опции для SQLite.
        /// </summary>
        public static SqlProviderOptions SqliteOptions { get; } = new SqlProviderOptions(
            x => x.ValueFormatter.NonNumberValuePrefix = "'",
            x => x.ValueFormatter.NonNumberValueSuffix = "'",
            x => x.ValueFormatter.EscapeMode = Helpers.StringHelper.EscapeMode.Sql,
            x => x.ValueFormatter.TrueValue = "TRUE",
            x => x.ValueFormatter.FalseValue = "FALSE",
            x => x.GetInsertedIdQuery = "SELECT last_insert_rowid()",
            x => x.OverrideOffsetRowsTemplate = "LIMIT {1} OFFSET {0}",
            x => x.ParamPrefix = ":",
            x => x.DatabaseParameterName = "Data Source",
            x => x.ServerParameterName = null,
            x => x.UserParameterName = null,
            x => x.PasswordParameterName = "Password",
            x => x.IntegratedSecurityParameterName = null,
            x => x.ApplicationNameParameterName = null,
            x => x.ConnectTimeoutParameterName = null);

        /// <summary>
        /// Преднастроенные опции для PostgreSQL.
        /// </summary>
        public static SqlProviderOptions PostgreSqlOptions { get; } = new SqlProviderOptions(
            x => x.ValueFormatter.NonNumberValuePrefix = "'",
            x => x.ValueFormatter.NonNumberValueSuffix = "'",
            x => x.ValueFormatter.EscapeMode = Helpers.StringHelper.EscapeMode.Sql,
            x => x.ValueFormatter.TrueValue = "TRUE",
            x => x.ValueFormatter.FalseValue = "FALSE",
            x => x.GetInsertedIdQuery = "SELECT LASTVAL()",
            x => x.OverrideOffsetRowsTemplate = "LIMIT {1} OFFSET {0}",
            x => x.ParamPrefix = "@",
            x => x.ExecuteProcedure = "CALL",
            x => x.DatabaseParameterName = "Database",
            x => x.ServerParameterName = "Host",
            x => x.UserParameterName = "Username",
            x => x.PasswordParameterName = "Password",
            x => x.IntegratedSecurityParameterName = null,
            x => x.ApplicationNameParameterName = "Application Name",
            x => x.ConnectTimeoutParameterName = "Timeout");

        /// <summary>
        /// Преднастроенные опции для Microsoft SQL Server.
        /// </summary>
        public static SqlProviderOptions SqlServerOptions { get; } = new SqlProviderOptions(
            x => x.ValueFormatter.NonNumberValuePrefix = "'",
            x => x.ValueFormatter.NonNumberValueSuffix = "'",
            x => x.ValueFormatter.EscapeMode = Helpers.StringHelper.EscapeMode.Sql,
            x => x.ValueFormatter.TrueValue = "1",
            x => x.ValueFormatter.FalseValue = "0",
            x => x.GetInsertedIdQuery = "SELECT SCOPE_IDENTITY()",
            x => x.OverrideOffsetRowsTemplate = "OFFSET {0} ROWS FETCH NEXT {1} ROWS ONLY",
            x => x.ParamPrefix = "@");

        /// <summary>
        /// Форматтер значений, используемый при генерации SQL-запросов.
        /// </summary>
        public ValueFormatter ValueFormatter { get; } = new ValueFormatter();

        /// <summary>
        /// SQL-запрос для получения идентификатора последней вставленной записи.
        /// </summary>
        public string GetInsertedIdQuery { get; set; }

        /// <summary>
        /// Отображение сущностей на таблицы базы данных.
        /// </summary>
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
        /// Префикс имени объекта (например, кавычка для экранирования).
        /// </summary>
        public string NamePrefix { get; internal set; } = "\"";

        /// <summary>
        /// Суффикс имени объекта.
        /// </summary>
        public string NameSuffix { get; internal set; } = "\"";

        /// <summary>
        /// Представление значения NULL в SQL.
        /// </summary>
        public string NullValue { get; internal set; } = "NULL";

        /// <summary>
        /// Шаблон для постраничного вывода (OFFSET / LIMIT).
        /// </summary>
        public string OverrideOffsetRowsTemplate { get; internal set; } = "OFFSET {0} ROWS FETCH NEXT {1} ROWS ONLY";

        /// <summary>
        /// Префикс параметров в SQL-запросах.
        /// </summary>
        public string ParamPrefix { get; internal set; } = ":";

        /// <summary>
        /// Символ завершения SQL-оператора.
        /// </summary>
        public string StatementTerminator { get; internal set; } = ";";

        /// <summary>
        /// Ключевое слово для вызова хранимой процедуры.
        /// </summary>
        public string ExecuteProcedure { get; internal set; } = "EXEC";

        /// <summary>
        /// Имя параметра строки подключения для базы данных.
        /// </summary>
        public string DatabaseParameterName { get; internal set; } = "Database";

        /// <summary>
        /// Имя параметра строки подключения для сервера.
        /// </summary>
        public string ServerParameterName { get; internal set; } = "Server";

        /// <summary>
        /// Имя параметра строки подключения для пользователя.
        /// </summary>
        public string UserParameterName { get; internal set; } = "User";

        /// <summary>
        /// Имя параметра строки подключения для пароля.
        /// </summary>
        public string PasswordParameterName { get; internal set; } = "Password";

        /// <summary>
        /// Имя параметра строки подключения для интегрированной безопасности.
        /// </summary>
        public string IntegratedSecurityParameterName { get; internal set; } = "Integrated Security";

        /// <summary>
        /// Имя параметра строки подключения для имени приложения.
        /// </summary>
        public string ApplicationNameParameterName { get; internal set; } = "Application Name";

        /// <summary>
        /// Имя параметра строки подключения для доверия сертификату сервера.
        /// </summary>
        public string TrustServerCertificateParameterName { get; internal set; } = "TrustServerCertificate";

        /// <summary>
        /// Имя параметра строки подключения для таймаута подключения.
        /// </summary>
        public string ConnectTimeoutParameterName { get; internal set; } = "Connect Timeout";

        /// <summary>
        /// Получает экземпляр опций на основе типа подключения.
        /// </summary>
        /// <param name="dbConnection">Объект подключения к базе данных.</param>
        /// <returns>Экземпляр <see cref="SqlProviderOptions"/>.</returns>
        public static SqlProviderOptions GetInstance(IDbConnection dbConnection) => GetInstance(dbConnection.GetType().Name);

        /// <summary>
        /// Получает экземпляр опций на основе имени типа подключения.
        /// </summary>
        /// <param name="sqlConnectionTypeName">Имя типа подключения.</param>
        /// <returns>Экземпляр <see cref="SqlProviderOptions"/>.</returns>
        public static SqlProviderOptions GetInstance(string sqlConnectionTypeName)
        {
            switch (sqlConnectionTypeName.ToLower())
            {
                case "sqlconnection":
                    return SqlServerOptions;

                case "sqliteconnection":
                    return SqliteOptions;

                case "npgsqlconnection":
                    return PostgreSqlOptions;

                default:
                    return Default;
            }
        }
    }
}