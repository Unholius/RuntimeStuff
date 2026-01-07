// ***********************************************************************
// Assembly         : RuntimeStuff
// Author           : RS
// Created          : 01-06-2026
//
// Last Modified By : RS
// Last Modified On : 01-07-2026
// ***********************************************************************
// <copyright file="DbClient.cs" company="Rudnev Sergey">
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************
namespace RuntimeStuff
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.Common;
    using System.Diagnostics;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using RuntimeStuff.Builders;
    using RuntimeStuff.Extensions;
    using RuntimeStuff.Helpers;
    using RuntimeStuff.Options;

    /// <summary>
    /// Универсальный клиент доступа к базе данных с поддержкой CRUD-операций,
    /// транзакций, агрегаций и асинхронного выполнения команд.
    /// </summary>
    /// <remarks>Предназначен для использования как легковесная альтернатива ORM.</remarks>
    public class DbClient : IDisposable, IHaveOptions<SqlProviderOptions>
    {
        /// <summary>
        /// The ignore case comparer.
        /// </summary>
        private static readonly StringComparer IgnoreCaseComparer = StringComparer.OrdinalIgnoreCase;

        /// <summary>
        /// The empty parameters.
        /// </summary>
        private readonly IReadOnlyDictionary<string, object> emptyParams = new Dictionary<string, object>();

        /// <summary>
        /// The tr.
        /// </summary>
        private readonly AsyncLocal<IDbTransaction> tr = new AsyncLocal<IDbTransaction>();

        /// <summary>
        /// The query logs.
        /// </summary>
        private Cache<DateTime, string> queryLogs = new Cache<DateTime, string>(sizeLimit: 100);

        /// <summary>
        /// The query log maximum size.
        /// </summary>
        private uint queryLogMaxSize = 100;

        /// <summary>
        /// Initializes a new instance of the <see cref="DbClient" /> class.
        /// </summary>
        public DbClient()
        {
            this.ValueConverter = (fieldName, fieldValue, propInfo, item) =>
                ChangeType(fieldValue is string s ? s.Trim() : fieldValue, propInfo.PropertyType);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DbClient" /> class.
        /// </summary>
        /// <param name="con">The con.</param>
        /// <exception cref="System.ArgumentNullException">con.</exception>
        public DbClient(IDbConnection con)
            : this()
        {
            this.Connection = con ?? throw new ArgumentNullException(nameof(con));
            this.Options = SqlProviderOptions.GetInstance(con.GetType().Name);
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="DbClient"/> class.
        /// Финализатор класса <see cref="DbClient" />.
        /// </summary>
        /// <remarks>Вызывается сборщиком мусора, если объект не был явно освобождён.</remarks>
        ~DbClient()
        {
            this.Dispose(false);
        }

        /// <summary>
        /// Делегат для преобразования значения из БД в значение свойства объекта.
        /// </summary>
        /// <param name="fieldName">Имя поля в результирующем наборе.</param>
        /// <param name="fieldValue">Сырое значение из БД.</param>
        /// <param name="propertyInfo">Информация о свойстве назначения.</param>
        /// <param name="item">Экземпляр объекта.</param>
        /// <returns>Преобразованное значение.</returns>
        public delegate object DbValueConverter(
            string fieldName,
            object fieldValue,
            PropertyInfo propertyInfo,
            object item);

        /// <summary>
        /// Типизированная версия делегата преобразования значений.
        /// </summary>
        /// <typeparam name="T">Type.</typeparam>
        /// <param name="fieldName">Name of the field.</param>
        /// <param name="fieldValue">The field value.</param>
        /// <param name="propertyInfo">The property information.</param>
        /// <param name="item">The item.</param>
        /// <returns>System.Object.</returns>
        public delegate object DbValueConverter<in T>(
            string fieldName,
            object fieldValue,
            PropertyInfo propertyInfo,
            T item);

        /// <summary>
        /// Событие, возникающее после успешного выполнения SQL-команды.
        /// </summary>
        /// <remarks>Вызывается после выполнения команды, но до закрытия соединения.</remarks>
        public event Action<IDbCommand> CommandExecuted;

        /// <summary>
        /// Событие, возникающее при ошибке выполнения SQL-команды.
        /// </summary>
        /// <remarks>Позволяет перехватывать исключения и анализировать команду, вызвавшую ошибку.</remarks>
        public event Action<IDbCommand, Exception> CommandFailed;

        /// <summary>
        /// Gets or sets the trim chars.
        /// </summary>
        /// <value>The trim chars.</value>
        public static char[] TrimChars { get; set; } = { '\uFEFF', '\u200B', ' ', '\r', '\n', '\t' };

        /// <summary>
        /// Gets the trim string spaces.
        /// </summary>
        public static DbValueConverter TrimStringSpaces { get; } = (name, value, info, item) => value is string s ? s.Trim(TrimChars) : ChangeType(value, info.PropertyType);

        /// <summary>
        /// Gets or sets a value indicating whether определяет, использовать ли ConfigureAwait(false) для асинхронных операций.
        /// </summary>
        /// <value><c>true</c> if [configure await]; otherwise, <c>false</c>.</value>
        public bool ConfigureAwait { get; set; } = false;

        /// <summary>
        /// Gets or sets активное соединение с базой данных.
        /// </summary>
        /// <value>The connection.</value>
        public IDbConnection Connection { get; set; }

        /// <summary>
        /// Gets or sets таймаут выполнения SQL-команд по умолчанию (в секундах).
        /// </summary>
        /// <value>The default command timeout.</value>
        public int DefaultCommandTimeout { get; set; } = 30;

        /// <summary>
        /// Gets or sets a value indicating whether включить логгирование запросов.
        /// </summary>
        /// <value><c>true</c> if [enable logging]; otherwise, <c>false</c>.</value>
        public bool EnableLogging { get; set; }

        /// <summary>
        /// Gets a value indicating whether признак того, что экземпляр <see cref="DbClient" /> был освобождён.
        /// </summary>
        /// <value><c>true</c> if this instance is disposed; otherwise, <c>false</c>.</value>
        /// <remarks>Устанавливается в <c>true</c> после вызова метода <see cref="Dispose()" />.</remarks>
        public bool IsDisposed { get; private set; }

        /// <summary>
        /// Gets or sets параметры SQL-провайдера (кавычки, префиксы параметров, синтаксис LIMIT/OFFSET и т.п.).
        /// </summary>
        /// <value>The options.</value>
        /// <remarks>Свойство является ковариантным (<c>out T</c>) и предназначено
        /// только для чтения. Для изменения опций рекомендуется использовать
        /// методы самого объекта опций или создавать новый экземпляр.</remarks>
        public SqlProviderOptions Options { get; set; } = new SqlProviderOptions();

        /// <summary>
        /// Gets or sets базовые опции клиента базы данных.
        /// </summary>
        /// <value>The options.</value>
        /// <remarks>Свойство является ковариантным (<c>out T</c>) и предназначено
        /// только для чтения. Для изменения опций рекомендуется использовать
        /// методы самого объекта опций или создавать новый экземпляр.</remarks>
        OptionsBase IHaveOptions.Options
        {
            get => this.Options;
            set => this.Options = (SqlProviderOptions)value;
        }

        /// <summary>
        /// Gets or sets the maximum size of the query log.
        /// </summary>
        /// <value>The maximum size of the query log.</value>
        public uint QueryLogMaxSize
        {
            get => this.queryLogMaxSize;
            set
            {
                this.queryLogMaxSize = value;
                this.queryLogs = new Cache<DateTime, string>(sizeLimit: this.queryLogMaxSize);
            }
        }

        /// <summary>
        /// Gets the query logs.
        /// </summary>
        /// <value>The query logs.</value>
        public IEnumerable<string> QueryLogs =>
            this.queryLogs.GetEntries().OrderBy(x => x.Created).Select(x => x.Value).ToArray();

        /// <summary>
        /// Gets or sets функция преобразования значений, полученных из БД, в значения свойств объектов.
        /// </summary>
        /// <value>The value converter.</value>
        public DbValueConverter<object> ValueConverter { get; set; }

        /// <summary>
        /// Создаёт новый экземпляр <see cref="DbClient{T}" /> по строке подключения.
        /// </summary>
        /// <typeparam name="T">Тип соединения с базой данных, реализующий <see cref="IDbConnection" />
        /// и имеющий публичный конструктор без параметров.</typeparam>
        /// <param name="connectionString">Строка подключения к базе данных.</param>
        /// <returns>Экземпляр <see cref="DbClient{T}" />.</returns>
        public static DbClient<T> Create<T>(string connectionString)
            where T : IDbConnection, new()
        {
            DbClient<T> dbClient = DbClient<T>.Create(connectionString);
            return dbClient;
        }

        /// <summary>
        /// Создаёт или возвращает кэшированный экземпляр <see cref="DbClient" />
        /// для указанного соединения.
        /// </summary>
        /// <param name="con">Соединение с базой данных.</param>
        /// <returns>Экземпляр <see cref="DbClient" />.</returns>
        public static DbClient Create(IDbConnection con)
        {
            DbClient dbClient = new DbClient(con);
            return dbClient;
        }

        /// <summary>
        /// Создаёт новый экземпляр <see cref="DbClient" /> без привязанного соединения.
        /// </summary>
        /// <returns>Новый экземпляр <see cref="DbClient" />.</returns>
        public static DbClient Create() => new DbClient();

        /// <summary>
        /// Устанавливает коллекцию параметров для команды.
        /// </summary>
        /// <param name="cmd">Команда, для которой устанавливаются параметры.</param>
        /// <param name="cmdParams">Коллекция параметров в виде словаря, где ключ — имя параметра, а значение — его значение.</param>
        /// <remarks>Этот метод устанавливает параметры для команды. Если параметр уже существует, его значение обновляется.</remarks>
        public static void SetParameterCollection(IDbCommand cmd, Dictionary<string, object> cmdParams)
        {
            foreach (KeyValuePair<string, object> cp in cmdParams)
            {
                IDbDataParameter p;
                if (cmd.Parameters.Contains(cp.Key))
                {
                    p = (IDbDataParameter)cmd.Parameters[cp.Key];
                }
                else
                {
                    p = cmd.CreateParameter();
                    cmd.Parameters.Add(p);
                }

                p.ParameterName = cp.Key;
                p.Value = cp.Value ?? DBNull.Value;
            }
        }

        /// <summary>
        /// Выполняет агрегационные функции для указанных столбцов с одним агрегатным выражением (например, COUNT).
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых выполняются агрегации.</typeparam>
        /// <param name="aggFunction">Агрегатная функция (например, COUNT, MIN, MAX, SUM, AVG).</param>
        /// <param name="whereExpression">The where expression.</param>
        /// <param name="columnSelectors">Выражения для выбора столбцов, для которых будет выполнена агрегация.</param>
        /// <returns>Словарь с результатами агрегации для указанных столбцов.</returns>
        /// <remarks>Этот метод выполняет одну агрегационную функцию (например, COUNT) для каждого указанного столбца и возвращает
        /// результат в виде словаря.</remarks>
        public Dictionary<string, object> Agg<TFrom>(
            string aggFunction,
            Expression<Func<TFrom, bool>> whereExpression,
            params Expression<Func<TFrom, object>>[] columnSelectors)
            where TFrom : class => this.Agg(
                whereExpression,
                columnSelectors?.Any() == true ? columnSelectors.Select(c => (c, aggFunction)).ToArray() : new[] { ((Expression<Func<TFrom, object>>)null, aggFunction) });

        /// <summary>
        /// Выполняет агрегацию с несколькими агрегационными функциями для выбранных столбцов.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых выполняется агрегация.</typeparam>
        /// <param name="whereExpression">The where expression.</param>
        /// <param name="columnSelectors">Выражения для выбора столбцов, для которых будет выполнена агрегация.</param>
        /// <returns>Словарь с результатами агрегации.</returns>
        /// <remarks>Этот метод позволяет выбрать несколько столбцов и применить различные агрегационные функции (например, COUNT, MIN,
        /// MAX, SUM, AVG).</remarks>
        public Dictionary<string, object> Agg<TFrom>(
            Expression<Func<TFrom, bool>> whereExpression = null,
            params (Expression<Func<TFrom, object>> column, string aggFunction)[] columnSelectors)
            where TFrom : class
        {
            string query = SqlQueryBuilder.GetAggSelectClause(this.Options, columnSelectors);

            if (whereExpression != null)
            {
                query += " " + SqlQueryBuilder.GetWhereClause(whereExpression, this.Options, false, out _);
            }

            DataTable table = this.ToDataTable(query);
            Dictionary<string, object> result = new Dictionary<string, object>(IgnoreCaseComparer);
            foreach (DataColumn dc in table.Columns)
            {
                object value = table.Rows[0][dc.ColumnName];
                result[dc.ColumnName] = value;
            }

            return result;
        }

        /// <summary>
        /// Асинхронно выполняет агрегационные функции для указанных столбцов с одним агрегатным выражением (например, COUNT).
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых выполняются агрегации.</typeparam>
        /// <param name="aggFunction">Агрегатная функция (например, COUNT, MIN, MAX, SUM, AVG).</param>
        /// <param name="whereExpression">The where expression.</param>
        /// <param name="token">Токен отмены асинхронной операции.</param>
        /// <param name="columnSelectors">Выражения для выбора столбцов, для которых будет выполнена агрегация.</param>
        /// <returns>Задача, которая возвращает словарь с результатами агрегации для указанных столбцов.</returns>
        /// <remarks>Этот метод асинхронно выполняет одну агрегационную функцию (например, COUNT) для каждого указанного столбца.</remarks>
        public Task<Dictionary<string, object>> AggAsync<TFrom>(
            string aggFunction,
            Expression<Func<TFrom, bool>> whereExpression = null,
            CancellationToken token = default,
            params Expression<Func<TFrom, object>>[] columnSelectors)
            where TFrom : class => this.AggAsync(
                whereExpression,
                token,
                columnSelectors?.Any() == true ? columnSelectors.Select(c => (c, aggFunction)).ToArray() : new[] { ((Expression<Func<TFrom, object>>)null, aggFunction) });

        /// <summary>
        /// Асинхронно выполняет агрегацию с несколькими агрегационными функциями для выбранных столбцов.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых выполняется агрегация.</typeparam>
        /// <param name="whereExpression">The where expression.</param>
        /// <param name="token">Токен отмены асинхронной операции.</param>
        /// <param name="columnSelectors">Выражения для выбора столбцов, для которых будет выполнена агрегация.</param>
        /// <returns>Задача, которая возвращает словарь с результатами агрегации для выбранных столбцов.</returns>
        /// <remarks>Этот метод асинхронно выполняет агрегацию с несколькими агрегационными функциями для выбранных столбцов.</remarks>
        public async Task<Dictionary<string, object>> AggAsync<TFrom>(
            Expression<Func<TFrom, bool>> whereExpression = null,
            CancellationToken token = default,
            params (Expression<Func<TFrom, object>> column, string aggFunction)[] columnSelectors)
            where TFrom : class
        {
            string query = SqlQueryBuilder.GetAggSelectClause(this.Options, columnSelectors);

            if (whereExpression != null)
            {
                query += " " + SqlQueryBuilder.GetWhereClause(whereExpression, this.Options, false, out _);
            }

            DataTable table = await this.ToDataTableAsync(query, token: token).ConfigureAwait(this.ConfigureAwait);
            Dictionary<string, object> result = new Dictionary<string, object>(IgnoreCaseComparer);
            foreach (DataColumn dc in table.Columns)
            {
                object value = table.Rows[0][dc.ColumnName];
                result[dc.ColumnName] = value;
            }

            return result;
        }

        /// <summary>
        /// Получает среднее значение для указанного столбца.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых вычисляется среднее значение.</typeparam>
        /// <typeparam name="T">Тип, в который будет преобразовано среднее значение.</typeparam>
        /// <param name="columnSelector">Выражение для выбора столбца, для которого будет вычислено среднее значение.</param>
        /// <returns>Среднее значение для указанного столбца, преобразованное в тип <typeparamref name="T" />.</returns>
        /// <remarks>Этот метод использует SQL-функцию AVG для получения среднего значения в столбце, а затем преобразует результат в
        /// тип <typeparamref name="T" />.</remarks>
        public T Avg<TFrom, T>(Expression<Func<TFrom, object>> columnSelector)
            where TFrom : class
        {
            object total = this.Avg(columnSelector);
            return ChangeType<T>(total);
        }

        /// <summary>
        /// Получает среднее значение для указанного столбца.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых вычисляется среднее значение.</typeparam>
        /// <param name="columnSelector">Выражение для выбора столбца, для которого будет вычислено среднее значение.</param>
        /// <param name="whereExpression">The where expression.</param>
        /// <returns>Среднее значение для указанного столбца.</returns>
        /// <remarks>Этот метод использует SQL-функцию AVG для получения среднего значения в столбце.</remarks>
        public object Avg<TFrom>(
            Expression<Func<TFrom, object>> columnSelector,
            Expression<Func<TFrom, bool>> whereExpression = null)
            where TFrom : class => this.Agg("AVG", whereExpression, columnSelector).Values.FirstOrDefault();

        /// <summary>
        /// Асинхронно получает среднее значение для указанного столбца.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых вычисляется среднее значение.</typeparam>
        /// <param name="whereExpression">The where expression.</param>
        /// <param name="columnSelector">Выражение для выбора столбца, для которого будет вычислено среднее значение.</param>
        /// <param name="token">Токен отмены асинхронной операции.</param>
        /// <returns>Задача, которая возвращает среднее значение для указанного столбца.</returns>
        /// <remarks>Этот метод асинхронно использует SQL-функцию AVG для получения среднего значения в столбце.</remarks>
        public async Task<object> AvgAsync<TFrom>(
            Expression<Func<TFrom, bool>> whereExpression = null,
            Expression<Func<TFrom, object>> columnSelector = null,
            CancellationToken token = default)
            where TFrom : class => (await this.AggAsync("AVG", whereExpression, token, columnSelector)
                .ConfigureAwait(this.ConfigureAwait)).Values.FirstOrDefault();

        /// <summary>
        /// Инициирует начало транзакции с заданным уровнем изоляции.
        /// </summary>
        /// <param name="level">Уровень изоляции транзакции. По умолчанию используется <see cref="IsolationLevel.ReadCommitted" />.</param>
        /// <returns>Объект транзакции, который можно использовать для дальнейших операций в рамках транзакции.</returns>
        /// <exception cref="System.InvalidOperationException">Транзакция уже была начата.</exception>
        /// <remarks>Этот метод открывает соединение с базой данных и начинает транзакцию с указанным уровнем изоляции.
        /// Если транзакция уже была начата, будет выброшено исключение.</remarks>
        public IDbTransaction BeginTransaction(IsolationLevel level = IsolationLevel.ReadCommitted)
        {
            if (this.tr.Value != null)
            {
                throw new InvalidOperationException("Транзакция уже была начата.");
            }

            this.BeginConnection();
            this.tr.Value = this.Connection.BeginTransaction(level);
            return this.tr.Value;
        }

        /// <summary>
        /// Получает общее количество строк для выполненного SQL-запроса.
        /// </summary>
        /// <param name="cmd">Команда, для которой будет выполнен подсчет строк.</param>
        /// <returns>Общее количество строк в результате запроса.</returns>
        /// <remarks>Этот метод выполняет подсчет количества строк в запросе, оборачивая его в подзапрос с использованием SQL-функции
        /// COUNT.</remarks>
        public object Count(IDbCommand cmd)
        {
            cmd.CommandText = $"SELECT COUNT(*) FROM ({cmd.CommandText}) AS CountTable";
            return this.ExecuteScalar(cmd);
        }

        /// <summary>
        /// Получает количество строк для данных в сущности с указанной колонкой.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>Общее количество строк в указанной колонке.</returns>
        /// <remarks>Этот метод выполняет агрегацию данных с использованием SQL-функции COUNT для конкретной колонки в сущности.</remarks>
        public object Count(string query)
        {
            query = $"SELECT COUNT(*) FROM ({query}) AS CountTable";
            return this.ExecuteScalar(query);
        }

        /// <summary>
        /// Получает количество строк для данных в сущности с указанной колонкой.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых будет подсчитано количество строк.</typeparam>
        /// <param name="whereExpression">The where expression.</param>
        /// <param name="columnSelector">Выражение для выбора колонки для подсчета.</param>
        /// <returns>Общее количество строк в указанной колонке, приведенное к типу <typeparamref name="T" />.</returns>
        /// <remarks>Этот метод выполняет агрегацию данных с использованием SQL-функции COUNT для конкретной колонки в сущности и
        /// преобразует результат в тип <typeparamref name="T" />.</remarks>
        public object Count<TFrom>(
            Expression<Func<TFrom, bool>> whereExpression = null,
            Expression<Func<TFrom, object>> columnSelector = null)
            where TFrom : class
        {
            object total = this.Agg("count", whereExpression, columnSelector).Values.FirstOrDefault();
            return total;
        }

        /// <summary>
        /// Counts the specified where expression.
        /// </summary>
        /// <typeparam name="TFrom">The type of the t from.</typeparam>
        /// <param name="whereExpression">The where expression.</param>
        /// <returns>System.Object.</returns>
        public object Count<TFrom>(Expression<Func<TFrom, bool>> whereExpression)
            where TFrom : class
        {
            object total = this.Agg("count", whereExpression).Values.FirstOrDefault();
            return total;
        }

        /// <summary>
        /// Получает количество строк для данных в сущности с указанной колонкой.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых будет подсчитано количество строк.</typeparam>
        /// <typeparam name="T">Тип возвращаемого значения.</typeparam>
        /// <param name="whereExpression">The where expression.</param>
        /// <param name="columnSelector">Выражение для выбора колонки для подсчета.</param>
        /// <returns>Общее количество строк в указанной колонке, приведенное к типу <typeparamref name="T" />.</returns>
        /// <remarks>Этот метод выполняет агрегацию данных с использованием SQL-функции COUNT для конкретной колонки в сущности и
        /// преобразует результат в тип <typeparamref name="T" />.</remarks>
        public T Count<TFrom, T>(
            Expression<Func<TFrom, bool>> whereExpression = null,
            Expression<Func<TFrom, object>> columnSelector = null)
            where TFrom : class
        {
            object total = this.Count(whereExpression, columnSelector);
            return ChangeType<T>(total);
        }

        /// <summary>
        /// Асинхронно выполняет подсчет строк в выполненном SQL-запросе.
        /// </summary>
        /// <param name="cmd">Команда, для которой будет выполнен подсчет строк.</param>
        /// <returns>Задача, которая возвращает количество строк в результате запроса.</returns>
        /// <remarks>Этот метод асинхронно выполняет подсчет количества строк в запросе, оборачивая его в подзапрос с использованием
        /// SQL-функции COUNT.</remarks>
        public Task<object> CountAsync(IDbCommand cmd)
        {
            cmd.CommandText = $"SELECT COUNT(*) FROM ({cmd.CommandText}) AS CountTable";
            return this.ExecuteScalarAsync(cmd);
        }

        /// <summary>
        /// Асинхронно выполняет подсчет строк в SQL-запросе.
        /// </summary>
        /// <param name="query">SQL-запрос для выполнения.</param>
        /// <param name="token">Токен отмены операции.</param>
        /// <returns>Задача, которая возвращает количество строк в результате запроса.</returns>
        /// <remarks>Этот метод выполняет асинхронный подсчет строк в SQL-запросе, оборачивая его в подзапрос с использованием
        /// SQL-функции COUNT.</remarks>
        public Task<object> CountAsync(string query, CancellationToken token = default)
        {
            query = $"SELECT COUNT(*) FROM ({query}) AS {this.Options.NamePrefix}CountTable{this.Options.NameSuffix}";
            return this.ExecuteScalarAsync(query, token: token);
        }

        /// <summary>
        /// Асинхронно получает количество строк для данных в сущности с указанной колонкой.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых будет подсчитано количество строк.</typeparam>
        /// <param name="whereExpression">The where expression.</param>
        /// <param name="columnSelector">Выражение для выбора колонки для подсчета.</param>
        /// <param name="token">Токен отмены операции.</param>
        /// <returns>Задача, которая возвращает общее количество строк в указанной колонке.</returns>
        /// <remarks>Этот метод выполняет асинхронный подсчет строк для конкретной колонки с использованием SQL-функции COUNT.</remarks>
        public async Task<object> CountAsync<TFrom>(
            Expression<Func<TFrom, bool>> whereExpression = null,
            Expression<Func<TFrom, object>> columnSelector = null,
            CancellationToken token = default)
            where TFrom : class => (await this.AggAsync("count", whereExpression, token, columnSelector)
                .ConfigureAwait(this.ConfigureAwait)).Values.FirstOrDefault();

        /// <summary>
        /// Асинхронно получает количество строк для данных в сущности с указанной колонкой и преобразует результат в тип
        /// <typeparamref name="T" />.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых будет подсчитано количество строк.</typeparam>
        /// <typeparam name="T">Тип возвращаемого значения.</typeparam>
        /// <param name="whereExpression">The where expression.</param>
        /// <param name="columnSelector">Выражение для выбора колонки для подсчета.</param>
        /// <param name="token">Токен отмены операции.</param>
        /// <returns>Задача, которая возвращает количество строк в указанной колонке, приведенное к типу <typeparamref name="T" />.</returns>
        /// <remarks>Этот метод выполняет асинхронный подсчет строк для конкретной колонки с использованием SQL-функции COUNT и
        /// преобразует результат в тип <typeparamref name="T" />.</remarks>
        public async Task<T> CountAsync<TFrom, T>(
            Expression<Func<TFrom, bool>> whereExpression = null,
            Expression<Func<TFrom, object>> columnSelector = null,
            CancellationToken token = default)
            where TFrom : class
        {
            object total = await this.CountAsync(whereExpression, columnSelector, token)
                .ConfigureAwait(this.ConfigureAwait);
            return ChangeType<T>(total);
        }

        /// <summary>
        /// Создаёт и настраивает команду для выполнения SQL-запроса.
        /// </summary>
        /// <param name="query">SQL-запрос, который будет выполнен.</param>
        /// <param name="cmdParams">Параметры запроса. Может быть <c>null</c>, если параметры не требуются.</param>
        /// <param name="dbTransaction">Транзакция, в рамках которой выполняется запрос. Может быть <c>null</c>.</param>
        /// <param name="commandTimeOut">Тайм-аут выполнения команды в секундах. Если не задано, используется значение по
        /// умолчанию.</param>
        /// <returns>Объект <see cref="DbCommand" />, готовый к выполнению запроса.</returns>
        /// <remarks>Этот метод создаёт команду для выполнения SQL-запроса, назначает ей параметры и устанавливает тайм-аут выполнения.
        /// Если параметры не указаны, команда будет выполнена без них.</remarks>
        public DbCommand CreateCommand(
            string query,
            object cmdParams,
            IDbTransaction dbTransaction = null,
            int? commandTimeOut = null)
        {
            IDbCommand cmd = this.Connection.CreateCommand();
            cmd.CommandText = query;
            cmd.CommandTimeout = commandTimeOut ?? this.DefaultCommandTimeout;
            cmd.CommandType = CommandType.Text;
            cmd.Transaction = dbTransaction;

            IReadOnlyDictionary<string, object> parameters = this.GetParams(cmdParams);

            if (cmdParams != null)
            {
                foreach (KeyValuePair<string, object> cp in parameters)
                {
                    IDbDataParameter p = cmd.CreateParameter();
                    p.ParameterName = cp.Key;
                    p.Value = cp.Value ?? DBNull.Value;
                    cmd.Parameters.Add(p);
                }
            }

            if (this.tr != null)
            {
                cmd.Transaction = this.tr.Value;
            }

            this.LogCommand(cmd);

            return cmd as DbCommand;
        }

        /// <summary>
        /// Удаляет записи из базы данных, соответствующие заданному условию.
        /// </summary>
        /// <typeparam name="T">Тип сущности, из таблицы которой выполняется удаление.</typeparam>
        /// <param name="whereExpression">Лямбда-выражение, задающее условие отбора записей для удаления.</param>
        /// <returns>Количество удалённых строк.</returns>
        public int Delete<T>(Expression<Func<T, bool>> whereExpression)
            where T : class
        {
            string query = (SqlQueryBuilder.GetDeleteQuery<T>(this.Options) + " " + SqlQueryBuilder.GetWhereClause(
                    whereExpression,
                    this.Options,
                    true,
                    out IReadOnlyDictionary<string, object> cmdParam))
                .Trim();
            return this.ExecuteNonQuery(query, cmdParam);
        }

        /// <summary>
        /// Удаляет запись из базы данных на основании значений ключевых полей объекта.
        /// </summary>
        /// <typeparam name="T">Тип сущности, из таблицы которой выполняется удаление.</typeparam>
        /// <param name="item">Объект, содержащий значения ключевых полей, используемых в условии удаления.</param>
        /// <returns>Количество удалённых строк.</returns>
        public int Delete<T>(T item)
            where T : class
        {
            string query = (SqlQueryBuilder.GetDeleteQuery<T>(this.Options) + " " +
                            SqlQueryBuilder.GetWhereClause<T>(this.Options, out _)).Trim();
            return this.ExecuteNonQuery(query, this.GetParams(item));
        }

        /// <summary>
        /// Асинхронно удаляет запись из базы данных на основании значений ключевых полей объекта.
        /// </summary>
        /// <typeparam name="T">Тип сущности, из таблицы которой выполняется удаление.</typeparam>
        /// <param name="item">Объект, содержащий значения ключевых полей, используемых в условии удаления.</param>
        /// <param name="dbTransaction">Транзакция, в рамках которой выполняется операция удаления. Может быть <c>null</c>.</param>
        /// <param name="token">Токен отмены асинхронной операции.</param>
        /// <returns>Задача, результатом которой является количество удалённых строк.</returns>
        public Task<int> DeleteAsync<T>(T item, IDbTransaction dbTransaction = null, CancellationToken token = default)
            where T : class
        {
            string query = (SqlQueryBuilder.GetDeleteQuery<T>(this.Options) + " " +
                            SqlQueryBuilder.GetWhereClause<T>(this.Options, out _)).Trim();
            return this.ExecuteNonQueryAsync(query, this.GetParams(item), dbTransaction, token);
        }

        /// <summary>
        /// Асинхронно удаляет записи из базы данных, соответствующие заданному условию.
        /// </summary>
        /// <typeparam name="T">Тип сущности, из таблицы которой выполняется удаление.</typeparam>
        /// <param name="whereExpression">Лямбда-выражение, задающее условие отбора записей для удаления.</param>
        /// <param name="dbTransaction">Транзакция, в рамках которой выполняется операция удаления.</param>
        /// <param name="token">Токен отмены асинхронной операции.</param>
        /// <returns>Задача, результатом которой является количество удалённых строк.</returns>
        public Task<int> DeleteAsync<T>(
            Expression<Func<T, bool>> whereExpression,
            IDbTransaction dbTransaction,
            CancellationToken token = default)
            where T : class
        {
            string query = (SqlQueryBuilder.GetDeleteQuery<T>(this.Options) + " " + SqlQueryBuilder.GetWhereClause(
                    whereExpression,
                    this.Options,
                    true,
                    out IReadOnlyDictionary<string, object> cmdParams))
                .Trim();
            return this.ExecuteNonQueryAsync(query, cmdParams, dbTransaction, token);
        }

        /// <summary>
        /// Асинхронно удаляет несколько записей из базы данных в рамках одной транзакции.
        /// </summary>
        /// <typeparam name="T">Тип сущности, из таблицы которой выполняется удаление.</typeparam>
        /// <param name="list">Коллекция объектов, содержащих значения ключевых полей удаляемых записей.</param>
        /// <param name="dbTransaction">Транзакция, в рамках которой выполняется удаление. Если <c>null</c>, создаётся новая транзакция.</param>
        /// <param name="token">Токен отмены асинхронной операции.</param>
        /// <returns>Задача, результатом которой является общее количество удалённых строк.</returns>
        /// <remarks>Все операции удаления выполняются в одной транзакции.
        /// В случае возникновения ошибки транзакция откатывается.</remarks>
        public async Task<int> DeleteRangeAsync<T>(
            IEnumerable<T> list,
            IDbTransaction dbTransaction,
            CancellationToken token = default)
            where T : class
        {
            try
            {
                int count = 0;
                using (dbTransaction ?? this.BeginTransaction())
                {
                    foreach (T item in list)
                    {
                        count += await this.DeleteAsync(item, dbTransaction, token).ConfigureAwait(this.ConfigureAwait);
                    }

                    this.EndTransaction();
                }

                return count;
            }
            catch (Exception ex)
            {
                this.RollbackTransaction();
                throw this.HandleDbException(ex, null);
            }
            finally
            {
                this.CloseConnection();
            }
        }

        /// <summary>
        /// Освобождает все ресурсы, используемые текущим экземпляром <see cref="DbClient" />.
        /// </summary>
        /// <remarks>Вызывает защищённый метод <see cref="Dispose(bool)" /> и подавляет финализацию объекта.</remarks>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Завершается текущая транзакция и закрывает соединение с базой данных.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">Транзакция не была начата.</exception>
        /// <remarks>Этот метод коммитит текущую транзакцию и очищает ресурсы, связанные с ней.
        /// После завершения транзакции соединение с базой данных закрывается.</remarks>
        public void EndTransaction()
        {
            if (this.tr.Value == null)
            {
                throw new InvalidOperationException("Транзакция не была начата.");
            }

            this.tr.Value.Commit();
            this.tr.Value.Dispose();
            this.tr.Value = null;
            this.CloseConnection();
        }

        /// <summary>
        /// Выполняет SQL-запрос, который не возвращает результатов (например, INSERT, UPDATE, DELETE).
        /// </summary>
        /// <param name="query">SQL-запрос, который будет выполнен.</param>
        /// <param name="queryParams">Параметры запроса. Может быть <c>null</c>, если параметры не требуются.</param>
        /// <param name="dbTransaction">Транзакция, в рамках которой будет выполнен запрос. Может быть <c>null</c>.</param>
        /// <returns>Количество затронутых строк в базе данных.</returns>
        /// <exception cref="Exception">Вызывается в случае ошибки при выполнении запроса.</exception>
        /// <remarks>Этот метод выполняет запрос синхронно и возвращает количество затронутых строк в базе данных.
        /// В случае ошибки будет выброшено исключение.</remarks>
        public int ExecuteNonQuery(string query, object queryParams = null, IDbTransaction dbTransaction = null)
        {
            using (DbCommand cmd = this.CreateCommand(query, queryParams, dbTransaction))
            {
                this.BeginConnection(this.Connection);

                int i = cmd.ExecuteNonQuery();
                this.CommandExecuted?.Invoke(cmd);
                this.Log(cmd);
                this.CloseConnection(this.Connection);
                return i;
            }
        }

        /// <summary>
        /// Асинхронно выполняет SQL-запрос, который не возвращает результатов (например, INSERT, UPDATE, DELETE).
        /// </summary>
        /// <param name="query">SQL-запрос, который будет выполнен.</param>
        /// <param name="cmdParams">Параметры запроса. Может быть <c>null</c>, если параметры не требуются.</param>
        /// <param name="dbTransaction">Транзакция, в рамках которой будет выполнен запрос. Может быть <c>null</c>.</param>
        /// <param name="token">Токен отмены асинхронной операции.</param>
        /// <returns>Задача, которая возвращает количество затронутых строк в базе данных.</returns>
        /// <exception cref="Exception">Вызывается в случае ошибки при выполнении запроса.</exception>
        /// <remarks>Этот метод выполняет запрос асинхронно и возвращает количество затронутых строк в базе данных.
        /// В случае ошибки будет выброшено исключение.</remarks>
        public async Task<int> ExecuteNonQueryAsync(
            string query,
            object cmdParams = null,
            IDbTransaction dbTransaction = null,
            CancellationToken token = default)
        {
            using (DbCommand cmd = this.CreateCommand(query, cmdParams, dbTransaction))
            {
                try
                {
                    await this.BeginConnectionAsync(token).ConfigureAwait(this.ConfigureAwait);
                    int i = await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(this.ConfigureAwait);
                    this.CommandExecuted?.Invoke(cmd);
                    this.Log(cmd);
                    return i;
                }
                catch (Exception ex)
                {
                    throw this.HandleDbException(ex, cmd);
                }
                finally
                {
                    this.CloseConnection();
                }
            }
        }

        /// <summary>
        /// Выполняет SQL-запрос и возвращает первое значение из первого столбца результата (например, COUNT, SUM, AVG).
        /// </summary>
        /// <param name="query">SQL-запрос, который будет выполнен.</param>
        /// <param name="cmdParams">Параметры запроса. Может быть <c>null</c>, если параметры не требуются.</param>
        /// <param name="dbTransaction">Транзакция, в рамках которой выполняется запрос. Может быть <c>null</c>.</param>
        /// <returns>Результат выполнения запроса в виде объекта.</returns>
        /// <exception cref="Exception">Вызывается в случае ошибки при выполнении запроса.</exception>
        /// <remarks>Этот метод выполняет запрос синхронно и возвращает первое значение из первого столбца результата.
        /// Если запрос не возвращает значений, будет возвращено <c>null</c>.</remarks>
        public object ExecuteScalar(string query, object cmdParams = null, IDbTransaction dbTransaction = null) => this.ExecuteScalar<object>(query, cmdParams, dbTransaction);

        /// <summary>
        /// Выполняет SQL-запрос и возвращает первое значение из первого столбца результата (например, COUNT, SUM, AVG).
        /// </summary>
        /// <param name="cmd">Команда, которая будет выполнена.</param>
        /// <returns>Результат выполнения запроса в виде объекта.</returns>
        /// <exception cref="Exception">Вызывается в случае ошибки при выполнении запроса.</exception>
        /// <remarks>Этот метод выполняет запрос синхронно и возвращает первое значение из первого столбца результата.
        /// Если запрос не возвращает значений, будет возвращено <c>null</c>.</remarks>
        public object ExecuteScalar(IDbCommand cmd) => this.ExecuteScalar<object>(cmd);

        /// <summary>
        /// Выполняет SQL-запрос с выбором значения по указанному выражению и условию, возвращая первое значение.
        /// </summary>
        /// <typeparam name="T">Тип сущности, из которой выполняется выборка.</typeparam>
        /// <typeparam name="TProp">Тип свойства, которое выбирается.</typeparam>
        /// <param name="propertySelector">Выражение, определяющее свойство для выбора.</param>
        /// <param name="whereExpression">Условие для фильтрации записей.</param>
        /// <returns>Результат выполнения запроса в виде выбранного свойства.</returns>
        /// <exception cref="Exception">Вызывается в случае ошибки при выполнении запроса.</exception>
        /// <remarks>Этот метод выполняет SQL-запрос, выбирает значение для указанного свойства и возвращает результат.</remarks>
        public TProp ExecuteScalar<T, TProp>(
            Expression<Func<T, TProp>> propertySelector,
            Expression<Func<T, bool>> whereExpression)
        {
            string query = (SqlQueryBuilder.GetSelectQuery(this.Options, propertySelector) + " " +
                            SqlQueryBuilder.GetWhereClause(
                                whereExpression,
                                this.Options,
                                true,
                                out IReadOnlyDictionary<string, object> cmdParam)).Trim();
            return this.ExecuteScalar<TProp>(query, cmdParam);
        }

        /// <summary>
        /// Выполняет SQL-запрос и возвращает результат как объект указанного типа.
        /// </summary>
        /// <typeparam name="T">Тип результата, в который будет преобразован результат запроса.</typeparam>
        /// <param name="query">SQL-запрос, который будет выполнен.</param>
        /// <param name="cmdParams">Параметры запроса. Может быть <c>null</c>, если параметры не требуются.</param>
        /// <param name="dbTransaction">Транзакция, в рамках которой выполняется запрос. Может быть <c>null</c>.</param>
        /// <returns>Результат выполнения запроса, приведённый к типу <typeparamref name="T" />.</returns>
        /// <exception cref="Exception">Вызывается в случае ошибки при выполнении запроса.</exception>
        /// <remarks>Этот метод выполняет запрос синхронно и преобразует результат в указанный тип.</remarks>
        public T ExecuteScalar<T>(string query, object cmdParams = null, IDbTransaction dbTransaction = null)
        {
            DbCommand cmd = this.CreateCommand(query, cmdParams, dbTransaction);
            return this.ExecuteScalar<T>(cmd);
        }

        /// <summary>
        /// Выполняет SQL-запрос и возвращает результат как объект указанного типа.
        /// </summary>
        /// <typeparam name="T">Тип результата, в который будет преобразован результат запроса.</typeparam>
        /// <param name="cmd">Команда, которая будет выполнена.</param>
        /// <returns>Результат выполнения запроса, приведённый к типу <typeparamref name="T" />.</returns>
        /// <exception cref="Exception">Вызывается в случае ошибки при выполнении запроса.</exception>
        /// <remarks>Этот метод выполняет запрос синхронно и преобразует результат в указанный тип.</remarks>
        public T ExecuteScalar<T>(IDbCommand cmd)
        {
            using (cmd)
            {
                try
                {
                    this.BeginConnection();
                    object v = cmd.ExecuteScalar();
                    this.CommandExecuted?.Invoke(cmd);
                    this.Log(cmd);
                    return (T)ChangeType(v, typeof(T));
                }
                catch (Exception ex)
                {
                    throw this.HandleDbException(ex, cmd);
                }
                finally
                {
                    this.CloseConnection();
                }
            }
        }

        /// <summary>
        /// Асинхронно выполняет SQL-запрос и возвращает первое значение из первого столбца результата.
        /// </summary>
        /// <param name="query">SQL-запрос, который будет выполнен.</param>
        /// <param name="cmdParams">Параметры запроса. Может быть <c>null</c>, если параметры не требуются.</param>
        /// <param name="dbTransaction">Транзакция, в рамках которой выполняется запрос. Может быть <c>null</c>.</param>
        /// <param name="token">Токен отмены асинхронной операции.</param>
        /// <returns>Задача, которая возвращает результат выполнения запроса в виде объекта.</returns>
        /// <exception cref="Exception">Вызывается в случае ошибки при выполнении запроса.</exception>
        /// <remarks>Этот метод выполняет запрос асинхронно и возвращает первое значение из первого столбца результата.
        /// Если запрос не возвращает значений, будет возвращено <c>null</c>.</remarks>
        public Task<object> ExecuteScalarAsync(
            string query,
            object cmdParams = null,
            IDbTransaction dbTransaction = null,
            CancellationToken token = default) => this.ExecuteScalarAsync<object>(query, cmdParams, dbTransaction, token);

        /// <summary>
        /// Асинхронно выполняет SQL-запрос и возвращает результат как объект указанного типа.
        /// </summary>
        /// <param name="cmd">Команда, которая будет выполнена.</param>
        /// <param name="token">Токен отмены асинхронной операции.</param>
        /// <returns>Задача, которая возвращает результат выполнения запроса как объект указанного типа.</returns>
        /// <exception cref="Exception">Вызывается в случае ошибки при выполнении запроса.</exception>
        /// <remarks>Этот метод выполняет запрос асинхронно и преобразует результат в указанный тип.</remarks>
        public Task<object> ExecuteScalarAsync(IDbCommand cmd, CancellationToken token = default) => this.ExecuteScalarAsync<object>(cmd as DbCommand, token);

        /// <summary>
        /// Асинхронно выполняет SQL-запрос с выбором значения по указанному выражению и условию.
        /// </summary>
        /// <typeparam name="T">Тип сущности, из которой выполняется выборка.</typeparam>
        /// <typeparam name="TProp">Тип свойства, которое выбирается.</typeparam>
        /// <param name="propertySelector">Выражение, определяющее свойство для выбора.</param>
        /// <param name="whereExpression">Условие для фильтрации записей.</param>
        /// <param name="token">Токен отмены асинхронной операции.</param>
        /// <returns>Задача, которая возвращает результат выполнения запроса в виде выбранного свойства.</returns>
        /// <exception cref="Exception">Вызывается в случае ошибки при выполнении запроса.</exception>
        /// <remarks>Этот метод выполняет SQL-запрос, выбирает значение для указанного свойства и возвращает результат.</remarks>
        public Task<TProp> ExecuteScalarAsync<T, TProp>(
            Expression<Func<T, TProp>> propertySelector,
            Expression<Func<T, bool>> whereExpression,
            CancellationToken token = default)
        {
            string query = (SqlQueryBuilder.GetSelectQuery(this.Options, propertySelector) + " " +
                            SqlQueryBuilder.GetWhereClause(
                                whereExpression,
                                this.Options,
                                true,
                                out IReadOnlyDictionary<string, object> cmdParam)).Trim();
            return this.ExecuteScalarAsync<TProp>(query, cmdParam, token: token);
        }

        /// <summary>
        /// Асинхронно выполняет SQL-запрос и возвращает результат как объект указанного типа.
        /// </summary>
        /// <typeparam name="T">Тип результата, в который будет преобразован результат запроса.</typeparam>
        /// <param name="query">SQL-запрос, который будет выполнен.</param>
        /// <param name="cmdParams">Параметры запроса. Может быть <c>null</c>, если параметры не требуются.</param>
        /// <param name="dbTransaction">Транзакция, в рамках которой выполняется запрос. Может быть <c>null</c>.</param>
        /// <param name="token">Токен отмены асинхронной операции.</param>
        /// <returns>Задача, которая возвращает результат выполнения запроса, приведённый к типу <typeparamref name="T" />.</returns>
        /// <exception cref="Exception">Вызывается в случае ошибки при выполнении запроса.</exception>
        /// <remarks>Этот метод выполняет запрос асинхронно и преобразует результат в указанный тип.</remarks>
        public Task<T> ExecuteScalarAsync<T>(
            string query,
            object cmdParams = null,
            IDbTransaction dbTransaction = null,
            CancellationToken token = default)
        {
            DbCommand cmd = this.CreateCommand(query, cmdParams, dbTransaction);
            return this.ExecuteScalarAsync<T>(cmd, token);
        }

        /// <summary>
        /// Асинхронно выполняет SQL-запрос и возвращает результат как объект указанного типа.
        /// </summary>
        /// <typeparam name="T">Тип результата, в который будет преобразован результат запроса.</typeparam>
        /// <param name="cmd">Команда, которая будет выполнена.</param>
        /// <param name="token">Токен отмены асинхронной операции.</param>
        /// <returns>Задача, которая возвращает результат выполнения запроса, приведённый к типу <typeparamref name="T" />.</returns>
        /// <exception cref="Exception">Вызывается в случае ошибки при выполнении запроса.</exception>
        /// <remarks>Этот метод выполняет запрос асинхронно и преобразует результат в указанный тип.</remarks>
        public async Task<T> ExecuteScalarAsync<T>(DbCommand cmd, CancellationToken token = default)
        {
            using (cmd)
            {
                try
                {
                    await this.BeginConnectionAsync(token).ConfigureAwait(this.ConfigureAwait);
                    object v = await cmd.ExecuteScalarAsync(token).ConfigureAwait(this.ConfigureAwait);
                    this.CommandExecuted?.Invoke(cmd);
                    this.Log(cmd);
                    return (T)ChangeType(v, typeof(T));
                }
                catch (Exception ex)
                {
                    throw this.HandleDbException(ex, cmd);
                }
                finally
                {
                    this.CloseConnection();
                }
            }
        }

        /// <summary>
        /// Возвращает первый элемент из результата запроса (или <c>null</c>, если результат пуст).
        /// </summary>
        /// <typeparam name="T">Тип объекта, который будет возвращён.</typeparam>
        /// <param name="query">SQL-запрос для выборки данных. Если <c>null</c>, используется стандартный запрос.</param>
        /// <param name="cmdParams">Параметры запроса. Может быть <c>null</c>, если параметры не требуются.</param>
        /// <param name="columns">Список столбцов для выборки. Если <c>null</c>, выбираются все столбцы.</param>
        /// <param name="columnToPropertyMap">Отображение столбцов в свойства объекта. Может быть <c>null</c>.</param>
        /// <param name="converter">Конвертер значений для преобразования типов. Может быть <c>null</c>.</param>
        /// <param name="offsetRows">Количество строк для пропуска перед выборкой. По умолчанию <c>0</c>.</param>
        /// <param name="itemFactory">Фабрика для создания объекта типа <typeparamref name="T" /> из данных строки. Может быть
        /// <c>null</c>.</param>
        /// <returns>Первый элемент результата выборки или <c>null</c>, если результат пуст.</returns>
        /// <remarks>Этот метод выполняет запрос синхронно и возвращает первый элемент результата или <c>null</c>, если данные
        /// отсутствуют.</remarks>
        public T First<T>(
            string query = null,
            object cmdParams = null,
            IEnumerable<string> columns = null,
            IEnumerable<(string, string)> columnToPropertyMap = null,
            DbValueConverter<T> converter = null,
            int offsetRows = 0,
            Func<object[], string[], T> itemFactory = null) => this.ToList(query, cmdParams, columns, columnToPropertyMap, converter, 1, offsetRows, itemFactory)
                .FirstOrDefault();

        /// <summary>
        /// Возвращает первый элемент, соответствующий условию, из результата запроса (или <c>null</c>, если результат пуст).
        /// </summary>
        /// <typeparam name="T">Тип объекта, который будет возвращён.</typeparam>
        /// <param name="whereExpression">Лямбда-выражение, задающее условие для выборки.</param>
        /// <param name="columnToPropertyMap">Отображение столбцов в свойства объекта. Может быть <c>null</c>.</param>
        /// <param name="converter">Конвертер значений для преобразования типов. Может быть <c>null</c>.</param>
        /// <param name="offsetRows">Количество строк для пропуска перед выборкой. По умолчанию <c>0</c>.</param>
        /// <param name="itemFactory">Фабрика для создания объекта типа <typeparamref name="T" /> из данных строки. Может быть
        /// <c>null</c>.</param>
        /// <param name="orderByExpression">Условия сортировки. Если переданы, запрос будет отсортирован.</param>
        /// <returns>Первый элемент, соответствующий условию, или <c>null</c>, если результат пуст.</returns>
        /// <remarks>Этот метод выполняет запрос синхронно с учётом условия выборки и сортировки, и возвращает первый элемент результата
        /// или <c>null</c>, если данные отсутствуют.</remarks>
        public T First<T>(
            Expression<Func<T, bool>> whereExpression,
            IEnumerable<(string, string)> columnToPropertyMap = null,
            DbValueConverter<T> converter = null,
            int offsetRows = 0,
            Func<object[], string[], T> itemFactory = null,
            params (Expression<Func<T, object>>, bool)[] orderByExpression) => this.ToList(
                whereExpression,
                columnToPropertyMap,
                converter,
                1,
                offsetRows,
                itemFactory,
                orderByExpression).FirstOrDefault();

        /// <summary>
        /// Асинхронно возвращает первый элемент из результата запроса (или <c>null</c>, если результат пуст).
        /// </summary>
        /// <typeparam name="T">Тип объекта, который будет возвращён.</typeparam>
        /// <param name="query">SQL-запрос для выборки данных. Если <c>null</c>, используется стандартный запрос.</param>
        /// <param name="cmdParams">Параметры запроса. Может быть <c>null</c>, если параметры не требуются.</param>
        /// <param name="columns">Список столбцов для выборки. Если <c>null</c>, выбираются все столбцы.</param>
        /// <param name="columnToPropertyMap">Отображение столбцов в свойства объекта. Может быть <c>null</c>.</param>
        /// <param name="converter">Конвертер значений для преобразования типов. Может быть <c>null</c>.</param>
        /// <param name="offsetRows">Количество строк для пропуска перед выборкой. По умолчанию <c>0</c>.</param>
        /// <param name="itemFactory">Фабрика для создания объекта типа <typeparamref name="T" /> из данных строки. Может быть
        /// <c>null</c>.</param>
        /// <returns>Задача, которая возвращает первый элемент результата выборки или <c>null</c>, если результат пуст.</returns>
        /// <remarks>Этот метод выполняет запрос асинхронно и возвращает первый элемент результата или <c>null</c>, если данные
        /// отсутствуют.</remarks>
        public async Task<T> FirstAsync<T>(
            string query = null,
            object cmdParams = null,
            IEnumerable<string> columns = null,
            IEnumerable<(string, string)> columnToPropertyMap = null,
            DbValueConverter<T> converter = null,
            int offsetRows = 0,
            Func<object[], string[], T> itemFactory = null) => (await this.ToListAsync(
                query,
                cmdParams,
                columns,
                columnToPropertyMap,
                converter,
                1,
                offsetRows,
                itemFactory).ConfigureAwait(this.ConfigureAwait)).FirstOrDefault();

        /// <summary>
        /// Асинхронно возвращает первый элемент, соответствующий условию, из результата запроса (или <c>null</c>, если
        /// результат пуст).
        /// </summary>
        /// <typeparam name="T">Тип объекта, который будет возвращён.</typeparam>
        /// <param name="whereExpression">Лямбда-выражение, задающее условие для выборки.</param>
        /// <param name="columnToPropertyMap">Отображение столбцов в свойства объекта. Может быть <c>null</c>.</param>
        /// <param name="converter">Конвертер значений для преобразования типов. Может быть <c>null</c>.</param>
        /// <param name="offsetRows">Количество строк для пропуска перед выборкой. По умолчанию <c>0</c>.</param>
        /// <param name="itemFactory">Фабрика для создания объекта типа <typeparamref name="T" /> из данных строки. Может быть
        /// <c>null</c>.</param>
        /// <param name="ct">Токен отмены асинхронной операции.</param>
        /// <param name="orderByExpression">Условия сортировки. Если переданы, запрос будет отсортирован.</param>
        /// <returns>Задача, которая возвращает первый элемент, соответствующий условию, или <c>null</c>, если результат пуст.</returns>
        /// <remarks>Этот метод выполняет запрос асинхронно с учётом условия выборки и сортировки, и возвращает первый элемент
        /// результата или <c>null</c>, если данные отсутствуют.</remarks>
        public async Task<T> FirstAsync<T>(
            Expression<Func<T, bool>> whereExpression,
            IEnumerable<(string, string)> columnToPropertyMap = null,
            DbValueConverter<T> converter = null,
            int offsetRows = 0,
            Func<object[], string[], T> itemFactory = null,
            CancellationToken ct = default,
            params (Expression<Func<T, object>>, bool)[] orderByExpression) => (await this.ToListAsync(
                whereExpression,
                columnToPropertyMap,
                converter,
                1,
                offsetRows,
                itemFactory,
                ct,
                orderByExpression).ConfigureAwait(this.ConfigureAwait)).FirstOrDefault();

        /// <summary>
        /// Получает агрегационные значения для указанных столбцов: количество, минимальное, максимальное, сумма и среднее.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых вычисляются агрегации.</typeparam>
        /// <param name="whereExpression">The where expression.</param>
        /// <param name="columnSelector">Выражения для выбора столбцов, для которых будут вычислены агрегации.</param>
        /// <returns>Словарь, где ключом является имя столбца, а значением кортеж с результатами агрегаций (Count, Min, Max, Sum,
        /// Avg).</returns>
        /// <remarks>Этот метод выполняет несколько агрегационных операций (COUNT, MIN, MAX, SUM, AVG) для каждого указанного столбца и
        /// возвращает результаты в виде словаря.</remarks>
        public Dictionary<string, (long Count, long Min, long Max, long Sum, decimal Avg)> GetAggs<TFrom>(
            Expression<Func<TFrom, bool>> whereExpression = null,
            params Expression<Func<TFrom, object>>[] columnSelector)
            where TFrom : class
        {
            string[] colNames = columnSelector.Select(x => x.GetMemberCache().ColumnName).ToArray();
            List<(Expression<Func<TFrom, object>>, string)> queryExpression =
                new List<(Expression<Func<TFrom, object>>, string)>();
            foreach (Expression<Func<TFrom, object>> cs in columnSelector)
            {
                queryExpression.Add((cs, "COUNT"));
                queryExpression.Add((cs, "MIN"));
                queryExpression.Add((cs, "MAX"));
                queryExpression.Add((cs, "SUM"));
                queryExpression.Add((cs, "AVG"));
            }

            Dictionary<string, object> result = this.Agg(whereExpression, queryExpression.ToArray());

            Dictionary<string, (long, long, long, long, decimal)> dic = colNames.Select((x, i) => (x,
                (
                    ChangeType<long>(result[$"{x}COUNT"]),
                    ChangeType<long>(result[$"{x}MIN"]),
                    ChangeType<long>(result[$"{x}MAX"]),
                    ChangeType<long>(result[$"{x}SUM"]),
                    ChangeType<decimal>(result[$"{x}AVG"])))).ToDictionary(key => key.x, val => val.Item2);

            return dic;
        }

        /// <summary>
        /// Асинхронно получает агрегационные значения для указанных столбцов: количество, минимальное, максимальное, сумма и
        /// среднее.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых вычисляются агрегации.</typeparam>
        /// <param name="whereExpression">The where expression.</param>
        /// <param name="token">Токен отмены асинхронной операции.</param>
        /// <param name="columnSelector">Выражения для выбора столбцов, для которых будут вычислены агрегации.</param>
        /// <returns>Задача, которая возвращает словарь с агрегационными значениями для столбцов.</returns>
        /// <remarks>Этот метод асинхронно выполняет несколько агрегационных операций (COUNT, MIN, MAX, SUM, AVG) для каждого указанного
        /// столбца.</remarks>
        public async Task<Dictionary<string, (long Count, long Min, long Max, long Sum, decimal Avg)>>
            GetAggsAsync<TFrom>(
                Expression<Func<TFrom, bool>> whereExpression,
                CancellationToken token = default,
                params Expression<Func<TFrom, object>>[] columnSelector)
            where TFrom : class
        {
            string[] colNames = columnSelector.Select(x => x.GetMemberCache().ColumnName).ToArray();
            List<(Expression<Func<TFrom, object>>, string)> queryExpression =
                new List<(Expression<Func<TFrom, object>>, string)>();
            foreach (Expression<Func<TFrom, object>> cs in columnSelector)
            {
                queryExpression.Add((cs, "COUNT"));
                queryExpression.Add((cs, "MIN"));
                queryExpression.Add((cs, "MAX"));
                queryExpression.Add((cs, "SUM"));
                queryExpression.Add((cs, "AVG"));
            }

            Dictionary<string, object> result = await this.AggAsync(whereExpression, token, queryExpression.ToArray())
                .ConfigureAwait(this.ConfigureAwait);

            Dictionary<string, (long, long, long, long, decimal)> dic = colNames.Select((x, i) => (x,
                (
                    ChangeType<long>(result[$"{x}COUNT"]),
                    ChangeType<long>(result[$"{x}MIN"]),
                    ChangeType<long>(result[$"{x}MAX"]),
                    ChangeType<long>(result[$"{x}SUM"]),
                    ChangeType<decimal>(result[$"{x}AVG"])))).ToDictionary(key => key.x, val => val.Item2);

            return dic;
        }

        /// <summary>
        /// Получает словарь страниц с информацией о смещении и количестве элементов для каждой страницы.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых будет получен список страниц.</typeparam>
        /// <param name="pageSize">Размер страницы (количество элементов на странице).</param>
        /// <returns>Словарь с ключом — номер страницы, значением — кортеж с смещением и количеством элементов на странице.</returns>
        /// <exception cref="System.ArgumentOutOfRangeException">pageSize.</exception>
        /// <remarks>Этот метод разбивает данные на страницы с учетом заданного размера страницы.</remarks>
        public Dictionary<int, (int offset, int count)> GetPages<TFrom>(int pageSize)
            where TFrom : class
        {
            if (pageSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pageSize));
            }

            long total = this.Count<TFrom, long>();
            int pagesCount = (int)Math.Ceiling(total / (double)pageSize);

            Dictionary<int, (int offset, int count)> pages = new Dictionary<int, (int offset, int count)>(pagesCount);

            for (int page = 1; page <= pagesCount; page++)
            {
                int offset = (page - 1) * pageSize;
                long count = Math.Min(pageSize, total - offset);

                pages[page] = (offset, (int)count);
            }

            return pages;
        }

        /// <summary>
        /// Асинхронно получает словарь страниц с информацией о смещении и количестве элементов для каждой страницы.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых будет получен список страниц.</typeparam>
        /// <param name="pageSize">Размер страницы (количество элементов на странице).</param>
        /// <param name="token">Токен отмены асинхронной операции.</param>
        /// <returns>Задача, которая возвращает словарь с номером страницы в качестве ключа и кортежем с смещением и количеством
        /// элементов.</returns>
        /// <exception cref="System.ArgumentOutOfRangeException">pageSize.</exception>
        /// <remarks>Этот метод асинхронно разбивает данные на страницы с учетом заданного размера страницы.</remarks>
        public async Task<Dictionary<int, (int offset, int count)>> GetPagesAsync<TFrom>(
            int pageSize,
            CancellationToken token = default)
            where TFrom : class
        {
            if (pageSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pageSize));
            }

            long total = await this.CountAsync<TFrom, long>(token: token).ConfigureAwait(this.ConfigureAwait);
            int pagesCount = (int)Math.Ceiling(total / (double)pageSize);

            Dictionary<int, (int offset, int count)> pages = new Dictionary<int, (int offset, int count)>(pagesCount);

            for (int page = 1; page <= pagesCount; page++)
            {
                int offset = (page - 1) * pageSize;
                long count = Math.Min(pageSize, total - offset);

                pages[page] = (offset, (int)count);
            }

            return pages;
        }

        /// <summary>
        /// Получает количество страниц для данных с учетом заданного размера страницы.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых будет вычислено количество страниц.</typeparam>
        /// <param name="pageSize">Размер страницы (количество элементов на странице).</param>
        /// <param name="whereExpression">The where expression.</param>
        /// <returns>Общее количество страниц.</returns>
        /// <remarks>Этот метод выполняет подсчет общего числа строк и делит их на страницы в зависимости от заданного размера страницы.</remarks>
        public int GetPagesCount<TFrom>(int pageSize, Expression<Func<TFrom, bool>> whereExpression = null)
            where TFrom : class
        {
            Dictionary<string, object> numbers = this.Agg(whereExpression, (null, "count"));
            int rowsCount = Convert.ToInt32(numbers.Values.FirstOrDefault());
            int pagesCount = (int)Math.Ceiling((double)rowsCount / pageSize);
            return pagesCount;
        }

        /// <summary>
        /// Асинхронно получает количество страниц для данных с учетом заданного размера страницы.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых будет вычислено количество страниц.</typeparam>
        /// <param name="pageSize">Размер страницы (количество элементов на странице).</param>
        /// <param name="whereExpression">The where expression.</param>
        /// <param name="token">Токен отмены асинхронной операции.</param>
        /// <returns>Задача, которая возвращает количество страниц.</returns>
        /// <remarks>Этот метод выполняет асинхронный подсчет общего числа строк и делит их на страницы в зависимости от заданного
        /// размера страницы.</remarks>
        public async Task<int> GetPagesCountAsync<TFrom>(
            int pageSize,
            Expression<Func<TFrom, bool>> whereExpression,
            CancellationToken token = default)
            where TFrom : class
        {
            Dictionary<string, object> numbers = await this.AggAsync(whereExpression, token, (null, "count"))
                .ConfigureAwait(this.ConfigureAwait);
            int rowsCount = Convert.ToInt32(numbers.Values.FirstOrDefault());
            int pagesCount = (int)Math.Ceiling((double)rowsCount / pageSize);
            return pagesCount;
        }

        /// <summary>
        /// Получает параметры для запроса на основе переданного объекта.
        /// </summary>
        /// <param name="cmdParams">Объект, содержащий параметры запроса. Может быть коллекцией или одиночной парой ключ-значение.</param>
        /// <param name="propertyNames">Список имён свойств для выборки из объекта. Если не указано, выбираются все свойства.</param>
        /// <returns>Коллекция параметров в виде словаря, где ключ — имя параметра, а значение — его значение.</returns>
        /// <remarks>Этот метод извлекает параметры из переданного объекта. Он поддерживает работу с различными типами данных,
        /// такими как: <list type="bullet"><item><description>Словари
        /// (Dictionary)</description></item><item><description>Коллекции
        /// (IEnumerable)</description></item><item><description>Кортежи (Tuple)</description></item></list> Если объект
        /// представляет собой кортеж, параметры будут извлечены из его элементов.</remarks>
        public IReadOnlyDictionary<string, object> GetParams(object cmdParams, params string[] propertyNames)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            if (cmdParams == null)
            {
                return this.emptyParams;
            }

            MemberCache memberCache = MemberCache.Create(cmdParams.GetType());
            switch (cmdParams)
            {
                case KeyValuePair<string, object> kvp:
                    return new Dictionary<string, object> { kvp };

                case Dictionary<string, object> dic:
                    return dic;

                case IDictionary<string, object> idic:
                    return idic.ToDictionary(x => x.Key, x => x.Value);

                case IEnumerable e:
                    {
                        MemberCache elementCache = MemberCache.Create(memberCache.ElementType);

                        MemberCache key = elementCache.GetMember("Key", MemberNameType.Name) ??
                            elementCache.GetMember("Item1", MemberNameType.Name);
                        MemberCache val = elementCache.GetMember("Value", MemberNameType.Name) ??
                            elementCache.GetMember("Item2", MemberNameType.Name);
                        if (key == null || val == null)
                        {
                            break;
                        }

                        foreach (object i in e)
                        {
                            parameters[key.GetValue<string>(i)] = val.GetValue(i);
                        }

                        break;
                    }

                default:
                    {
                        if (memberCache.IsTuple)
                        {
                            MemberCache key = memberCache.GetMember("Key", MemberNameType.Name) ??
                                memberCache.GetMember("Item1", MemberNameType.Name);
                            MemberCache val = memberCache.GetMember("Value", MemberNameType.Name) ??
                                memberCache.GetMember("Item2", MemberNameType.Name);

                            parameters[key.GetValue<string>(cmdParams)] = val.GetValue(cmdParams);
                        }
                        else
                        {
                            parameters = memberCache.ToDictionary(cmdParams, propertyNames);
                        }

                        break;
                    }
            }

            return parameters;
        }

        /// <summary>
        /// Получает строку SQL-запроса с заменой всех параметров на их значения.
        /// </summary>
        /// <param name="command">Команда, содержащая SQL-запрос и параметры.</param>
        /// <returns>Строка SQL-запроса с подставленными значениями параметров.</returns>
        /// <exception cref="System.ArgumentNullException">command.</exception>
        /// <remarks>Этот метод заменяет все параметры в SQL-запросе на их фактические значения,
        /// что полезно для отладки или логирования SQL-запросов с параметрами.</remarks>
        public string GetRawSql(IDbCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            string sql = command.CommandText;

            foreach (IDbDataParameter parameter in command.Parameters)
            {
                string paramToken = this.Options.ParamPrefix + parameter.ParameterName;
                string literal = this.Options.ToSqlLiteral(parameter.Value);

                sql = ReplaceParameterToken(sql, paramToken, literal);
            }

            return sql;
        }

        /// <summary>
        /// Создаёт новый экземпляр сущности, инициализирует его и вставляет в базу данных.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="dbTransaction">Активная транзакция базы данных.
        /// Если не указана, используется текущая или создаётся новая.</param>
        /// <param name="columnSetters">Делегаты инициализации свойств сущности перед вставкой.</param>
        /// <returns>Созданный и сохранённый объект.</returns>
        public T Insert<T>(IDbTransaction dbTransaction = null, params Action<T>[] columnSetters)
            where T : class
        {
            T item = Obj.New<T>();
            foreach (Action<T> a in columnSetters)
            {
                a(item);
            }

            this.Insert(item, dbTransaction);
            return item;
        }

        /// <summary>
        /// Создаёт новый экземпляр сущности, инициализирует его и вставляет в базу данных.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="columnSetters">Делегаты инициализации свойств сущности перед вставкой.</param>
        /// <returns>Созданный и сохранённый объект.</returns>
        public T Insert<T>(params Action<T>[] columnSetters)
            where T : class => this.Insert(null, columnSetters);

        /// <summary>
        /// Вставляет объект в базу данных.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="item">Объект для вставки.</param>
        /// <param name="insertColumns">Список колонок, участвующих во вставке.
        /// Если не указан, используются все сопоставленные свойства.</param>
        /// <returns>Значение сгенерированного первичного ключа,
        /// либо <c>null</c>, если провайдер не поддерживает его получение.</returns>
        public object Insert<T>(T item, params Expression<Func<T, object>>[] insertColumns)
            where T : class => this.Insert(item, null, insertColumns);

        /// <summary>
        /// Вставляет объект в базу данных.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="item">Объект для вставки.</param>
        /// <param name="dbTransaction">Транзакция базы данных.</param>
        /// <param name="insertColumns">Список колонок, участвующих во вставке.
        /// Если не указан, используются все сопоставленные свойства.</param>
        /// <returns>Значение сгенерированного первичного ключа,
        /// либо <c>null</c>, если провайдер не поддерживает его получение.</returns>
        public object Insert<T>(
            T item,
            IDbTransaction dbTransaction,
            params Expression<Func<T, object>>[] insertColumns)
            where T : class
        {
            object id = null;
            string query = SqlQueryBuilder.GetInsertQuery(this.Options, insertColumns);
            if (string.IsNullOrWhiteSpace(this.Options.GetInsertedIdQuery))
            {
                this.ExecuteNonQuery(query, this.GetParams(item), dbTransaction);
            }
            else
            {
                query += $"{this.Options.StatementTerminator} {this.Options.GetInsertedIdQuery}";
                id = this.ExecuteScalar<object>(query, this.GetParams(item));
                MemberCache<T> mi = MemberCache<T>.Create();
                if (id != null && id != DBNull.Value && mi.PrimaryKeys.Count == 1)
                {
                    MemberCache pi = mi.PrimaryKeys.First().Value;
                    pi.SetValue(item, id);
                }
            }

            return id;
        }

        /// <summary>
        /// Асинхронно создаёт и вставляет новую сущность в базу данных.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="insertColumns">Делегаты инициализации свойств.</param>
        /// <param name="dbTransaction">Транзакция базы данных.</param>
        /// <param name="token">Токен отмены операции.</param>
        /// <returns>Значение первичного ключа, либо <c>null</c>.</returns>
        public Task<object> InsertAsync<T>(
            Action<T>[] insertColumns = null,
            IDbTransaction dbTransaction = null,
            CancellationToken token = default)
            where T : class
        {
            T item = Obj.New<T>();
            if (insertColumns == null)
            {
                return this.InsertAsync(item, null, dbTransaction, token);
            }

            foreach (Action<T> a in insertColumns)
            {
                a(item);
            }

            return this.InsertAsync(item, null, dbTransaction, token);
        }

        /// <summary>
        /// Асинхронно вставляет объект в базу данных.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="item">Объект для вставки.</param>
        /// <param name="insertColumns">Список колонок, участвующих во вставке.</param>
        /// <param name="dbTransaction">Транзакция базы данных.</param>
        /// <param name="token">Токен отмены операции.</param>
        /// <returns>Значение сгенерированного первичного ключа,
        /// либо <c>null</c>.</returns>
        public async Task<object> InsertAsync<T>(
            T item,
            Expression<Func<T, object>>[] insertColumns = null,
            IDbTransaction dbTransaction = null,
            CancellationToken token = default)
            where T : class
        {
            object id = null;
            string query = SqlQueryBuilder.GetInsertQuery(this.Options, insertColumns);
            if (string.IsNullOrWhiteSpace(this.Options.GetInsertedIdQuery))
            {
                await this.ExecuteNonQueryAsync(query, this.GetParams(item), dbTransaction, token)
                    .ConfigureAwait(this.ConfigureAwait);
            }
            else
            {
                query += $"{this.Options.StatementTerminator} {this.Options.GetInsertedIdQuery}";
                id = await this.ExecuteScalarAsync<object>(query, this.GetParams(item), dbTransaction, token)
                    .ConfigureAwait(this.ConfigureAwait);
                MemberCache<T> mi = MemberCache<T>.Create();
                if (id != null && id != DBNull.Value && mi.PrimaryKeys.Count == 1)
                {
                    mi.PrimaryKeys.First().Value.SetValue(
                        item,
                        ChangeType(id, mi.PrimaryKeys.First().Value.PropertyType));
                }
            }

            return id;
        }

        /// <summary>
        /// Вставляет коллекцию объектов в базу данных в рамках одной транзакции.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="list">Коллекция объектов для вставки.</param>
        /// <param name="dbTransaction">Внешняя транзакция. Если не указана — создаётся новая.</param>
        /// <param name="insertColumns">Колонки, участвующие во вставке.</param>
        /// <returns>Количество вставленных записей.</returns>
        public int InsertRange<T>(
            IEnumerable<T> list,
            IDbTransaction dbTransaction = null,
            params Expression<Func<T, object>>[] insertColumns)
            where T : class
        {
            try
            {
                int count = 0;
                using (dbTransaction ?? this.BeginTransaction())
                {
                    string query = SqlQueryBuilder.GetInsertQuery(this.Options, insertColumns);
                    if (!string.IsNullOrWhiteSpace(this.Options.GetInsertedIdQuery))
                    {
                        query += $"{this.Options.StatementTerminator} {this.Options.GetInsertedIdQuery}";
                    }

                    MemberCache<T> typeCache = MemberCache<T>.Create();
                    MemberCache pk = typeCache.PrimaryKeys.FirstOrDefault().Value;
                    Dictionary<string, object> queryParams = new Dictionary<string, object>();
                    using (DbCommand cmd = this.CreateCommand(query, dbTransaction))
                    {
                        foreach (T item in list)
                        {
                            typeCache.ToDictionary(item, queryParams);
                            SetParameterCollection(cmd, queryParams);
                            object id = cmd.ExecuteScalar();
                            this.CommandExecuted?.Invoke(cmd);
                            this.Log(cmd);
                            if (pk != null && id != null)
                            {
                                pk.SetValue(item, ChangeType(id, pk.PropertyType));
                            }

                            count++;
                        }
                    }

                    this.EndTransaction();
                }

                return count;
            }
            catch (Exception ex)
            {
                this.RollbackTransaction();
                throw this.HandleDbException(ex, null);
            }
            finally
            {
                this.CloseConnection();
            }
        }

        /// <summary>
        /// Асинхронно вставляет коллекцию объектов в базу данных
        /// в рамках одной транзакции.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="list">Коллекция объектов.</param>
        /// <param name="insertColumns">Колонки, участвующие во вставке.</param>
        /// <param name="dbTransaction">Внешняя транзакция.</param>
        /// <param name="token">Токен отмены.</param>
        /// <returns>Количество вставленных записей.</returns>
        /// <exception cref="System.NullReferenceException">dbCmd.</exception>
        public async Task<int> InsertRangeAsync<T>(
            IEnumerable<T> list,
            Expression<Func<T, object>>[] insertColumns = null,
            IDbTransaction dbTransaction = null,
            CancellationToken token = default)
            where T : class
        {
            try
            {
                int count = 0;
                using (dbTransaction ?? this.BeginTransaction())
                {
                    string query = SqlQueryBuilder.GetInsertQuery(this.Options, insertColumns);
                    if (!string.IsNullOrWhiteSpace(this.Options.GetInsertedIdQuery))
                    {
                        query += $"{this.Options.StatementTerminator} {this.Options.GetInsertedIdQuery}";
                    }

                    MemberCache<T> typeCache = MemberCache<T>.Create();
                    MemberCache pk = typeCache.PrimaryKeys.FirstOrDefault().Value;
                    Dictionary<string, object> queryParams = new Dictionary<string, object>();
                    using (DbCommand cmd = this.CreateCommand(query, dbTransaction))
                    {
                        if (!(cmd is DbCommand dbCmd))
                        {
                            throw new NullReferenceException(nameof(dbCmd));
                        }

                        foreach (T item in list)
                        {
                            typeCache.ToDictionary(item, queryParams);
                            SetParameterCollection(cmd, queryParams);

                            object id = await dbCmd.ExecuteScalarAsync(token).ConfigureAwait(this.ConfigureAwait);
                            this.CommandExecuted?.Invoke(cmd);
                            this.Log(cmd);
                            if (pk != null && id != null)
                            {
                                pk.SetValue(item, ChangeType(id, pk.PropertyType));
                            }

                            count++;
                        }
                    }

                    this.EndTransaction();
                }

                return count;
            }
            catch (Exception ex)
            {
                this.RollbackTransaction();
                throw this.HandleDbException(ex, null);
            }
            finally
            {
                this.CloseConnection();
            }
        }

        /// <summary>
        /// Получает максимальное значение для указанного столбца.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых вычисляется максимальное значение.</typeparam>
        /// <typeparam name="T">Тип, в который будет преобразовано максимальное значение.</typeparam>
        /// <param name="columnSelector">Выражение для выбора столбца, для которого будет вычислено максимальное значение.</param>
        /// <returns>Максимальное значение для указанного столбца, преобразованное в тип <typeparamref name="T" />.</returns>
        /// <remarks>Этот метод использует SQL-функцию MAX для получения максимального значения в столбце, а затем преобразует результат
        /// в тип <typeparamref name="T" />.</remarks>
        public T Max<TFrom, T>(Expression<Func<TFrom, object>> columnSelector)
            where TFrom : class
        {
            object total = this.Max(columnSelector);
            return ChangeType<T>(total);
        }

        /// <summary>
        /// Получает максимальное значение для указанного столбца.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых вычисляется максимальное значение.</typeparam>
        /// <param name="columnSelector">Выражение для выбора столбца, для которого будет вычислено максимальное значение.</param>
        /// <param name="whereExpression">The where expression.</param>
        /// <returns>Максимальное значение для указанного столбца.</returns>
        /// <remarks>Этот метод использует SQL-функцию MAX для получения максимального значения в столбце.</remarks>
        public object Max<TFrom>(
            Expression<Func<TFrom, object>> columnSelector,
            Expression<Func<TFrom, bool>> whereExpression = null)
            where TFrom : class => this.Agg("MAX", whereExpression, columnSelector).Values.FirstOrDefault();

        /// <summary>
        /// Асинхронно получает максимальное значение для указанного столбца.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых вычисляется максимальное значение.</typeparam>
        /// <typeparam name="T">Тип, в который будет преобразовано максимальное значение.</typeparam>
        /// <param name="whereExpression">The where expression.</param>
        /// <param name="columnSelector">Выражение для выбора столбца, для которого будет вычислено максимальное значение.</param>
        /// <param name="token">Токен отмены асинхронной операции.</param>
        /// <returns>Задача, которая возвращает максимальное значение для указанного столбца, преобразованное в тип
        /// <typeparamref name="T" />.</returns>
        /// <remarks>Этот метод асинхронно использует SQL-функцию MAX для получения максимального значения и преобразует результат в тип
        /// <typeparamref name="T" />.</remarks>
        public async Task<T> MaxAsync<TFrom, T>(
            Expression<Func<TFrom, bool>> whereExpression = null,
            Expression<Func<TFrom, object>> columnSelector = null,
            CancellationToken token = default)
            where TFrom : class
        {
            object total = await this.MaxAsync(whereExpression, columnSelector, token)
                .ConfigureAwait(this.ConfigureAwait);
            return ChangeType<T>(total);
        }

        /// <summary>
        /// Асинхронно получает максимальное значение для указанного столбца.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых вычисляется максимальное значение.</typeparam>
        /// <param name="whereExpression">The where expression.</param>
        /// <param name="columnSelector">Выражение для выбора столбца, для которого будет вычислено максимальное значение.</param>
        /// <param name="token">Токен отмены асинхронной операции.</param>
        /// <returns>Задача, которая возвращает максимальное значение для указанного столбца.</returns>
        /// <remarks>Этот метод асинхронно использует SQL-функцию MAX для получения максимального значения в столбце.</remarks>
        public async Task<object> MaxAsync<TFrom>(
            Expression<Func<TFrom, bool>> whereExpression = null,
            Expression<Func<TFrom, object>> columnSelector = null,
            CancellationToken token = default)
            where TFrom : class => (await this.AggAsync("MAX", whereExpression, token, columnSelector)
                .ConfigureAwait(this.ConfigureAwait)).Values.FirstOrDefault();

        /// <summary>
        /// Получает минимальное значение для указанного столбца.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых вычисляется минимальное значение.</typeparam>
        /// <typeparam name="T">Тип, в который будет преобразовано минимальное значение.</typeparam>
        /// <param name="columnSelector">Выражение для выбора столбца, для которого будет вычислено минимальное значение.</param>
        /// <returns>Минимальное значение для указанного столбца, преобразованное в тип <typeparamref name="T" />.</returns>
        /// <remarks>Этот метод использует SQL-функцию MIN для получения минимального значения в столбце, а затем преобразует результат
        /// в тип <typeparamref name="T" />.</remarks>
        public T Min<TFrom, T>(Expression<Func<TFrom, object>> columnSelector)
            where TFrom : class
        {
            object total = this.Min(columnSelector);
            return ChangeType<T>(total);
        }

        /// <summary>
        /// Получает минимальное значение для указанного столбца.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых вычисляется минимальное значение.</typeparam>
        /// <param name="columnSelector">Выражение для выбора столбца, для которого будет вычислено минимальное значение.</param>
        /// <param name="whereExpression">The where expression.</param>
        /// <returns>Минимальное значение для указанного столбца.</returns>
        /// <remarks>Этот метод использует SQL-функцию MIN для получения минимального значения в столбце.</remarks>
        public object Min<TFrom>(
            Expression<Func<TFrom, object>> columnSelector,
            Expression<Func<TFrom, bool>> whereExpression = null)
            where TFrom : class => this.Agg("MIN", whereExpression, columnSelector).Values.FirstOrDefault();

        /// <summary>
        /// Асинхронно получает минимальное значение для указанного столбца.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых вычисляется минимальное значение.</typeparam>
        /// <typeparam name="T">Тип, в который будет преобразовано минимальное значение.</typeparam>
        /// <param name="whereExpression">The where expression.</param>
        /// <param name="columnSelector">Выражение для выбора столбца, для которого будет вычислено минимальное значение.</param>
        /// <param name="token">Токен отмены асинхронной операции.</param>
        /// <returns>Задача, которая возвращает минимальное значение для указанного столбца, преобразованное в тип
        /// <typeparamref name="T" />.</returns>
        /// <remarks>Этот метод асинхронно использует SQL-функцию MIN для получения минимального значения и преобразует результат в тип
        /// <typeparamref name="T" />.</remarks>
        public async Task<T> MinAsync<TFrom, T>(
            Expression<Func<TFrom, bool>> whereExpression = null,
            Expression<Func<TFrom, object>> columnSelector = null,
            CancellationToken token = default)
            where TFrom : class
        {
            object total = await this.MinAsync(whereExpression, columnSelector, token)
                .ConfigureAwait(this.ConfigureAwait);
            return ChangeType<T>(total);
        }

        /// <summary>
        /// Асинхронно получает минимальное значение для указанного столбца.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых вычисляется минимальное значение.</typeparam>
        /// <param name="whereExpression">The where expression.</param>
        /// <param name="columnSelector">Выражение для выбора столбца, для которого будет вычислено минимальное значение.</param>
        /// <param name="token">Токен отмены асинхронной операции.</param>
        /// <returns>Задача, которая возвращает минимальное значение для указанного столбца.</returns>
        /// <remarks>Этот метод асинхронно использует SQL-функцию MIN для получения минимального значения в столбце.</remarks>
        public async Task<object> MinAsync<TFrom>(
            Expression<Func<TFrom, bool>> whereExpression = null,
            Expression<Func<TFrom, object>> columnSelector = null,
            CancellationToken token = default)
            where TFrom : class => (await this.AggAsync("MIN", whereExpression, token, columnSelector)
                .ConfigureAwait(this.ConfigureAwait)).Values.FirstOrDefault();

        /// <summary>
        /// Выполняет SQL-запрос и возвращает результат в виде коллекции объектов.
        /// </summary>
        /// <typeparam name="TList">Тип коллекции, которая будет возвращена (например, <see cref="List{T}" />).</typeparam>
        /// <typeparam name="T">Тип объектов, которые содержатся в коллекции.</typeparam>
        /// <param name="query">SQL-запрос для выборки данных. Если <c>null</c> или пустой, используется запрос по умолчанию.</param>
        /// <param name="cmdParams">Параметры для SQL-запроса.</param>
        /// <param name="columns">Список столбцов для выборки. Если <c>null</c>, выбираются все столбцы.</param>
        /// <param name="columnToPropertyMap">Отображение столбцов в свойства объектов. Может быть <c>null</c>.</param>
        /// <param name="converter">Конвертер для преобразования значений. Может быть <c>null</c>.</param>
        /// <param name="fetchRows">Количество строк для выборки. По умолчанию —1 (выбираются все строки).</param>
        /// <param name="offsetRows">Количество строк для пропуска перед выборкой. По умолчанию — 0.</param>
        /// <param name="itemFactory">Фабрика для создания объектов типа <typeparamref name="T" />. Может быть <c>null</c>.</param>
        /// <returns>Коллекция объектов типа <typeparamref name="T" />, которая содержит результат выполнения запроса.</returns>
        /// <remarks>Этот метод выполняет SQL-запрос синхронно и возвращает результат в виде коллекции объектов.</remarks>
        public TList Query<TList, T>(
            string query = null,
            object cmdParams = null,
            IEnumerable<string> columns = null,
            IEnumerable<(string, string)> columnToPropertyMap = null,
            DbValueConverter<T> converter = null,
            int fetchRows = -1,
            int offsetRows = 0,
            Func<object[], string[], T> itemFactory = null)
            where TList : ICollection<T>, new()
        {
            if (string.IsNullOrEmpty(query))
            {
                query = SqlQueryBuilder.GetSelectQuery<T>(this.Options);
            }

            query = SqlQueryBuilder.AddLimitOffsetClauseToQuery(fetchRows, offsetRows, query, this.Options, typeof(T));

            MemberCache<T> cache = MemberCache<T>.Create();
            if (itemFactory == null)
            {
                itemFactory = this.BuildItemFactory(cache, columnToPropertyMap);
            }

            DbCommand cmd = this.CreateCommand(query, cmdParams);
            try
            {
                this.BeginConnection();

                DbDataReader reader = cmd.ExecuteReader();
                try
                {
                    this.CommandExecuted?.Invoke(cmd);
                    this.Log(cmd);
                    return this.ReadCoreAsync<TList, T>(
                        reader,
                        columns,
                        columnToPropertyMap,
                        converter,
                        fetchRows,
                        itemFactory,
                        false,
                        CancellationToken.None).GetAwaiter().GetResult();
                }
                finally
                {
                    reader.Dispose();
                }
            }
            catch (Exception ex)
            {
                throw this.HandleDbException(ex, cmd);
            }
            finally
            {
                cmd.Dispose();
                this.CloseConnection();
            }
        }

        /// <summary>
        /// Queries the specified return type.
        /// </summary>
        /// <param name="returnType">Type of the return.</param>
        /// <param name="query">The query.</param>
        /// <param name="cmdParams">The command parameters.</param>
        /// <param name="columns">The columns.</param>
        /// <param name="columnToPropertyMap">The column to property map.</param>
        /// <param name="converter">The converter.</param>
        /// <param name="fetchRows">The fetch rows.</param>
        /// <param name="offsetRows">The offset rows.</param>
        /// <param name="itemFactory">The item factory.</param>
        /// <returns>System.Object.</returns>
        public object Query(
            Type returnType,
            string query = null,
            object cmdParams = null,
            IEnumerable<string> columns = null,
            IEnumerable<(string, string)> columnToPropertyMap = null,
            DbValueConverter<object> converter = null,
            int fetchRows = -1,
            int offsetRows = 0,
            Func<object[], string[], object> itemFactory = null)
        {
            MemberCache mc = MemberCache.Create(returnType);
            if (string.IsNullOrEmpty(query))
            {
                query = SqlQueryBuilder.GetSelectQuery(this.Options, mc.ElementType);
            }

            query = SqlQueryBuilder.AddLimitOffsetClauseToQuery(
                fetchRows,
                offsetRows,
                query,
                this.Options,
                mc.ElementType);
            if (itemFactory == null)
            {
                itemFactory = this.BuildItemFactory(mc.ElementType, columnToPropertyMap);
            }

            DbCommand cmd = this.CreateCommand(query, cmdParams);
            try
            {
                this.BeginConnection();

                DbDataReader reader = cmd.ExecuteReader();
                try
                {
                    this.CommandExecuted?.Invoke(cmd);
                    this.Log(cmd);
                    return this.ReadCoreAsync(
                        mc,
                        reader,
                        columns,
                        columnToPropertyMap,
                        converter,
                        fetchRows,
                        itemFactory,
                        false,
                        CancellationToken.None).GetAwaiter().GetResult();
                }
                finally
                {
                    reader.Dispose();
                }
            }
            catch (Exception ex)
            {
                throw this.HandleDbException(ex, cmd);
            }
            finally
            {
                cmd.Dispose();
                this.CloseConnection();
            }
        }

        /// <summary>
        /// Выполняет SQL-запрос и возвращает результат в виде коллекции объектов.
        /// </summary>
        /// <typeparam name="TList">Тип коллекции, которая будет возвращена (например, <see cref="List{T}" />).</typeparam>
        /// <typeparam name="T">Тип объектов, которые содержатся в коллекции.</typeparam>
        /// <param name="query">SQL-запрос для выборки данных. Если <c>null</c> или пустой, используется запрос по умолчанию.</param>
        /// <param name="cmdParams">Параметры для SQL-запроса.</param>
        /// <param name="columns">Список столбцов для выборки. Если <c>null</c>, выбираются все столбцы.</param>
        /// <param name="columnToPropertyMap">Отображение столбцов в свойства объектов. Может быть <c>null</c>.</param>
        /// <param name="converter">Конвертер для преобразования значений. Может быть <c>null</c>.</param>
        /// <param name="fetchRows">Количество строк для выборки. По умолчанию —1 (выбираются все строки).</param>
        /// <param name="offsetRows">Количество строк для пропуска перед выборкой. По умолчанию — 0.</param>
        /// <param name="itemFactory">Фабрика для создания объектов типа <typeparamref name="T" />. Может быть <c>null</c>.</param>
        /// <param name="ct">The cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>Коллекция объектов типа <typeparamref name="T" />, которая содержит результат выполнения запроса.</returns>
        /// <remarks>Этот метод выполняет SQL-запрос синхронно и возвращает результат в виде коллекции объектов.</remarks>
        public async Task<TList> QueryAsync<TList, T>(
            string query = null,
            object cmdParams = null,
            IEnumerable<string> columns = null,
            IEnumerable<(string, string)> columnToPropertyMap = null,
            DbValueConverter<T> converter = null,
            int fetchRows = -1,
            int offsetRows = 0,
            Func<object[], string[], T> itemFactory = null,
            CancellationToken ct = default)
            where TList : ICollection<T>, new()
        {
            if (string.IsNullOrEmpty(query))
            {
                query = SqlQueryBuilder.GetSelectQuery<T>(this.Options);
            }

            query = SqlQueryBuilder.AddLimitOffsetClauseToQuery(fetchRows, offsetRows, query, this.Options, typeof(T));

            MemberCache<T> cache = MemberCache<T>.Create();
            if (itemFactory == null)
            {
                itemFactory = this.BuildItemFactory(cache, columnToPropertyMap);
            }

            DbCommand cmd = this.CreateCommand(query, cmdParams);
            try
            {
                await this.BeginConnectionAsync(ct).ConfigureAwait(this.ConfigureAwait);

                DbDataReader reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(this.ConfigureAwait);
                try
                {
                    this.CommandExecuted?.Invoke(cmd);
                    this.Log(cmd);
                    return await this.ReadCoreAsync<TList, T>(
                        reader,
                        columns,
                        columnToPropertyMap,
                        converter,
                        fetchRows,
                        itemFactory,
                        true,
                        ct).ConfigureAwait(this.ConfigureAwait);
                }
                finally
                {
                    reader.Dispose();
                }
            }
            catch (Exception ex)
            {
                throw this.HandleDbException(ex, cmd);
            }
            finally
            {
                cmd.Dispose();
                this.CloseConnection();
            }
        }

        /// <summary>
        /// Получает сумму для указанного столбца.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых вычисляется сумма.</typeparam>
        /// <typeparam name="T">Тип, в который будет преобразована сумма.</typeparam>
        /// <param name="columnSelector">Выражение для выбора столбца, для которого будет вычислена сумма.</param>
        /// <returns>Сумма для указанного столбца, преобразованная в тип <typeparamref name="T" />.</returns>
        /// <remarks>Этот метод использует SQL-функцию SUM для получения суммы значений в столбце, а затем преобразует результат в тип
        /// <typeparamref name="T" />.</remarks>
        public T Sum<TFrom, T>(Expression<Func<TFrom, object>> columnSelector)
            where TFrom : class
        {
            object total = this.Sum(columnSelector);
            return ChangeType<T>(total);
        }

        /// <summary>
        /// Получает сумму для указанного столбца.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых вычисляется сумма.</typeparam>
        /// <param name="columnSelector">Выражение для выбора столбца, для которого будет вычислена сумма.</param>
        /// <param name="whereExpression">The where expression.</param>
        /// <returns>Сумма для указанного столбца.</returns>
        /// <remarks>Этот метод использует SQL-функцию SUM для получения суммы значений в столбце.</remarks>
        public object Sum<TFrom>(
            Expression<Func<TFrom, object>> columnSelector,
            Expression<Func<TFrom, bool>> whereExpression = null)
            where TFrom : class => this.Agg("SUM", whereExpression, columnSelector).Values.FirstOrDefault();

        /// <summary>
        /// Асинхронно получает сумму для указанного столбца.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых вычисляется сумма.</typeparam>
        /// <typeparam name="T">Тип, в который будет преобразована сумма.</typeparam>
        /// <param name="whereExpression">The where expression.</param>
        /// <param name="columnSelector">Выражение для выбора столбца, для которого будет вычислена сумма.</param>
        /// <param name="token">Токен отмены асинхронной операции.</param>
        /// <returns>Задача, которая возвращает сумму для указанного столбца, преобразованную в тип <typeparamref name="T" />.</returns>
        /// <remarks>Этот метод асинхронно использует SQL-функцию SUM для получения суммы значений в столбце и преобразует результат в
        /// тип <typeparamref name="T" />.</remarks>
        public async Task<T> SumAsync<TFrom, T>(
            Expression<Func<TFrom, bool>> whereExpression = null,
            Expression<Func<TFrom, object>> columnSelector = null,
            CancellationToken token = default)
            where TFrom : class
        {
            object total = await this.SumAsync(whereExpression, columnSelector, token)
                .ConfigureAwait(this.ConfigureAwait);
            return ChangeType<T>(total);
        }

        /// <summary>
        /// Асинхронно получает сумму для указанного столбца.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых вычисляется сумма.</typeparam>
        /// <param name="whereExpression">The where expression.</param>
        /// <param name="columnSelector">Выражение для выбора столбца, для которого будет вычислена сумма.</param>
        /// <param name="token">Токен отмены асинхронной операции.</param>
        /// <returns>Задача, которая возвращает сумму для указанного столбца.</returns>
        /// <remarks>Этот метод асинхронно использует SQL-функцию SUM для получения суммы значений в столбце.</remarks>
        public async Task<object> SumAsync<TFrom>(
            Expression<Func<TFrom, bool>> whereExpression = null,
            Expression<Func<TFrom, object>> columnSelector = null,
            CancellationToken token = default)
            where TFrom : class => (await this.AggAsync("SUM", whereExpression, token, columnSelector)
                .ConfigureAwait(this.ConfigureAwait)).Values.FirstOrDefault();

        /// <summary>
        /// Выполняет SQL-запрос и возвращает результат в виде <see cref="DataTable" />.
        /// </summary>
        /// <typeparam name="TFrom">Тип объектов, для которых будет выполняться запрос.</typeparam>
        /// <param name="whereExpression">Условие выборки данных в виде выражения. Может быть <c>null</c>.</param>
        /// <param name="fetchRows">Количество строк для выборки. По умолчанию -1 (выбираются все строки).</param>
        /// <param name="offsetRows">Количество строк для пропуска перед выборкой. По умолчанию - 0.</param>
        /// <param name="columnSelectors">Селекторы столбцов для выборки. Может быть <c>null</c>.</param>
        /// <returns><see cref="DataTable" />, содержащий результат выполнения запроса.</returns>
        /// <remarks>Этот метод выполняет SQL-запрос синхронно и возвращает результат в виде <see cref="DataTable" />.
        /// Если передан параметр <paramref name="columnSelectors" />, то выборка будет происходить только по указанным
        /// столбцам.</remarks>
        public DataTable ToDataTable<TFrom>(
            Expression<Func<TFrom, bool>> whereExpression = null,
            int fetchRows = -1,
            int offsetRows = 0,
            params Expression<Func<TFrom, object>>[] columnSelectors)
        {
            string query = (SqlQueryBuilder.GetSelectQuery(this.Options, columnSelectors) + " " +
                            SqlQueryBuilder.GetWhereClause(
                                whereExpression,
                                this.Options,
                                true,
                                out IReadOnlyDictionary<string, object> cmdParam)).Trim();
            query = SqlQueryBuilder.AddLimitOffsetClauseToQuery(
                fetchRows,
                offsetRows,
                query,
                this.Options,
                typeof(TFrom));
            return this.ToDataTables(query, cmdParam).FirstOrDefault();
        }

        /// <summary>
        ///     Выполняет SQL-запрос и возвращает результат в виде <see cref="DataTable" />, с возможностью отображения столбцов в
        ///     соответствии с их именами.
        /// </summary>
        /// <param name="query">SQL-запрос для выполнения.</param>
        /// <param name="cmdParams">Параметры запроса.</param>
        /// <param name="columnMap">
        ///     Отображение столбцов запроса в имена свойств объектов. Каждый элемент содержит имя столбца и
        ///     имя свойства объекта.
        /// </param>
        /// <returns><see cref="DataTable" />, содержащий результат выполнения запроса.</returns>
        /// <remarks>
        ///     Этот метод выполняет SQL-запрос синхронно и возвращает результат в виде <see cref="DataTable" />, при этом
        ///     позволяет
        ///     отображать столбцы запроса в соответствии с их именами в объекте.
        public DataTable ToDataTable(string query, object cmdParams = null, params (string, string)[] columnMap) => this.ToDataTables(query, cmdParams, columnMap).FirstOrDefault();

        /// <summary>
        /// Асинхронно выполняет SQL-запрос и возвращает результат в виде <see cref="DataTable" />.
        /// </summary>
        /// <typeparam name="TFrom">Тип объектов, для которых будет выполняться запрос.</typeparam>
        /// <param name="whereExpression">Условие выборки данных в виде выражения. Может быть <c>null</c>.</param>
        /// <param name="fetchRows">Количество строк для выборки. По умолчанию -1 (выбираются все строки).</param>
        /// <param name="offsetRows">Количество строк для пропуска перед выборкой. По умолчанию - 0.</param>
        /// <param name="columnSelectors">Селекторы столбцов для выборки. Может быть <c>null</c>.</param>
        /// <returns>Задача, которая возвращает <see cref="DataTable" />, содержащий результат выполнения запроса.</returns>
        /// <remarks>Этот метод выполняет SQL-запрос асинхронно и возвращает результат в виде <see cref="DataTable" />.</remarks>
        public async Task<DataTable> ToDataTableAsync<TFrom>(
            Expression<Func<TFrom, bool>> whereExpression = null,
            int fetchRows = -1,
            int offsetRows = 0,
            params Expression<Func<TFrom, object>>[] columnSelectors)
        {
            string query = (SqlQueryBuilder.GetSelectQuery(this.Options, columnSelectors) + " " +
                            SqlQueryBuilder.GetWhereClause(
                                whereExpression,
                                this.Options,
                                true,
                                out IReadOnlyDictionary<string, object> cmdParam)).Trim();
            query = SqlQueryBuilder.AddLimitOffsetClauseToQuery(
                fetchRows,
                offsetRows,
                query,
                this.Options,
                typeof(TFrom));
            return (await this.ToDataTablesAsync(query, cmdParam).ConfigureAwait(this.ConfigureAwait)).FirstOrDefault();
        }

        /// <summary>
        ///     Асинхронно выполняет SQL-запрос и возвращает результат в виде <see cref="DataTable" />, с возможностью отображения
        ///     столбцов в соответствии с их именами.
        /// </summary>
        /// <param name="query">SQL-запрос для выполнения.</param>
        /// <param name="cmdParams">Параметры запроса.</param>
        /// <param name="token">Токен отмены асинхронной операции.</param>
        /// <param name="columnMap">
        ///     Отображение столбцов запроса в имена свойств объектов. Каждый элемент содержит имя столбца и
        ///     имя свойства объекта.
        /// </param>
        /// <returns>Задача, которая возвращает <see cref="DataTable" />, содержащий результат выполнения запроса.</returns>
        /// <remarks>
        ///     Этот метод выполняет SQL-запрос асинхронно и возвращает результат в виде <see cref="DataTable" />, при этом
        ///     позволяет
        ///     отображать столбцы запроса в соответствии с их именами в объекте.
        public async Task<DataTable> ToDataTableAsync(
            string query,
            object cmdParams = null,
            CancellationToken token = default,
            params (string, string)[] columnMap) => (await this.ToDataTablesAsync(query, cmdParams, token, columnMap)
                .ConfigureAwait(this.ConfigureAwait)).FirstOrDefault();

        /// <summary>
        ///     Выполняет SQL-запрос и возвращает результат в виде массива <see cref="DataTable" />.
        /// </summary>
        /// <param name="query">SQL-запрос для выполнения.</param>
        /// <param name="cmdParams">Параметры запроса.</param>
        /// <param name="columnMap">Отображение столбцов запроса в имена свойств объектов.</param>
        /// <returns>Массив <see cref="DataTable" />, содержащий результаты выполнения запроса.</returns>
        /// <remarks>
        ///     Этот метод выполняет SQL-запрос синхронно и возвращает результаты в виде массива <see cref="DataTable" />. Если
        ///     запрос
        ///     возвращает несколько наборов данных, они будут разделены в разные таблицы.
        public DataTable[] ToDataTables(string query, object cmdParams = null, params (string, string)[] columnMap)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                throw new NullReferenceException(nameof(query));
            }

            List<DataTable> result = new List<DataTable>();

            using (DbCommand cmd = this.CreateCommand(query, cmdParams))
            {
                try
                {
                    this.BeginConnection();

                    DataTable dataTable = new DataTable(query);
                    dataTable.BeginLoadData();

                    using (DbDataReader r = cmd.ExecuteReader())
                    {
                        do
                        {
                            this.CommandExecuted?.Invoke(cmd);
                            this.Log(cmd);
                            Dictionary<int, string> map = this.GetReaderFieldToPropertyMap(r, columnMap);
                            foreach (KeyValuePair<int, string> kv in map)
                            {
                                DataColumn col = new DataColumn(kv.Value, r.GetFieldType(kv.Key) ?? typeof(object));
                                dataTable.Columns.Add(col);
                            }

                            while (r.Read())
                            {
                                DataRow item = dataTable.NewRow();

                                foreach (KeyValuePair<int, string> kv in map)
                                {
                                    int colIndex = kv.Key;
                                    object raw = r.GetValue(colIndex);
                                    if (raw == null || raw == DBNull.Value)
                                    {
                                        continue;
                                    }

                                    item[kv.Value] = raw;
                                }

                                dataTable.Rows.Add(item);
                            }

                            dataTable.AcceptChanges();
                            dataTable.EndLoadData();
                            result.Add(dataTable);
                        }
                        while (r.NextResult());

                        return result.ToArray();
                    }
                }
                catch (Exception ex)
                {
                    throw this.HandleDbException(ex, cmd);
                }
                finally
                {
                    this.CloseConnection();
                }
            }
        }

        /// <summary>
        ///     Асинхронно выполняет SQL-запрос и возвращает результат в виде массива <see cref="DataTable" />.
        /// </summary>
        /// <param name="query">SQL-запрос для выполнения.</param>
        /// <param name="cmdParams">Параметры запроса.</param>
        /// <param name="token">Токен отмены асинхронной операции.</param>
        /// <param name="columnMap">Отображение столбцов запроса в имена свойств объектов.</param>
        /// <returns>Задача, которая возвращает массив <see cref="DataTable" />, содержащий результаты выполнения запроса.</returns>
        /// <remarks>
        ///     Этот метод выполняет SQL-запрос асинхронно и возвращает результаты в виде массива <see cref="DataTable" />. Если
        ///     запрос
        ///     возвращает несколько наборов данных, они будут разделены в разные таблицы.
        public async Task<DataTable[]> ToDataTablesAsync(
            string query,
            object cmdParams = null,
            CancellationToken token = default,
            params (string, string)[] columnMap)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                throw new NullReferenceException(nameof(query));
            }

            List<DataTable> result = new List<DataTable>();

            using (DbCommand cmd = this.CreateCommand(query, cmdParams))
            {
                try
                {
                    await this.BeginConnectionAsync(token).ConfigureAwait(this.ConfigureAwait);

                    DataTable dataTable = new DataTable(query);
                    dataTable.BeginLoadData();

                    using (DbDataReader r = await cmd.ExecuteReaderAsync(token).ConfigureAwait(this.ConfigureAwait))
                    {
                        do
                        {
                            this.CommandExecuted?.Invoke(cmd);
                            this.Log(cmd);
                            Dictionary<int, string> map = this.GetReaderFieldToPropertyMap(r, columnMap);
                            foreach (KeyValuePair<int, string> kv in map)
                            {
                                DataColumn col = new DataColumn(kv.Value, r.GetFieldType(kv.Key) ?? typeof(object));
                                dataTable.Columns.Add(col);
                            }

                            while (await r.ReadAsync(token).ConfigureAwait(this.ConfigureAwait))
                            {
                                DataRow item = dataTable.NewRow();

                                foreach (KeyValuePair<int, string> kv in map)
                                {
                                    int colIndex = kv.Key;
                                    object raw = r.GetValue(colIndex);
                                    if (raw == null || raw == DBNull.Value)
                                    {
                                        continue;
                                    }

                                    item[kv.Value] = raw;
                                }

                                dataTable.Rows.Add(item);
                            }

                            dataTable.AcceptChanges();
                            dataTable.EndLoadData();
                            result.Add(dataTable);
                        }
                        while (await r.NextResultAsync(token).ConfigureAwait(this.ConfigureAwait));

                        return result.ToArray();
                    }
                }
                catch (Exception ex)
                {
                    throw this.HandleDbException(ex, cmd);
                }
                finally
                {
                    this.CloseConnection();
                }
            }
        }

        /// <summary>
        /// Выполняет SQL-запрос и возвращает результат в виде <see cref="Dictionary{TKey, TValue}" />.
        /// </summary>
        /// <typeparam name="TKey">Тип ключа словаря.</typeparam>
        /// <typeparam name="TValue">Тип значения словаря.</typeparam>
        /// <param name="query">SQL-запрос для выполнения.</param>
        /// <param name="cmdParams">Параметры запроса.</param>
        /// <param name="columns">Столбцы, которые будут выбраны в запросе. Может быть <c>null</c>.</param>
        /// <param name="columnToPropertyMap">Отображение столбцов запроса в свойства объектов. Может быть <c>null</c>.</param>
        /// <param name="fetchRows">Количество строк для выборки. По умолчанию -1 (выбираются все строки).</param>
        /// <param name="offsetRows">Количество строк для пропуска перед выборкой. По умолчанию - 0.</param>
        /// <param name="itemFactory">Функция для создания пары ключ-значение. Может быть <c>null</c>.</param>
        /// <returns>Словарь, содержащий результат выполнения запроса в виде ключ-значение.</returns>
        /// <remarks>Этот метод выполняет SQL-запрос синхронно и преобразует результат в словарь <see cref="Dictionary{TKey, TValue}" />
        /// .
        /// Если <paramref name="itemFactory" /> не задан, то результат будет преобразован в словарь по умолчанию.</remarks>
        public Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(
            string query,
            object cmdParams = null,
            IEnumerable<string> columns = null,
            IEnumerable<(string, string)> columnToPropertyMap = null,
            int fetchRows = -1,
            int offsetRows = 0,
            Func<object[], string[], KeyValuePair<TKey, TValue>> itemFactory = null) => this.ToList(
                query,
                cmdParams,
                columns,
                columnToPropertyMap,
                null,
                fetchRows,
                offsetRows,
                itemFactory).ToDictionary(x => x.Key, x => x.Value);

        /// <summary>
        /// Выполняет SQL-запрос и возвращает результат в виде <see cref="Dictionary{TKey, TValue}" /> с использованием
        /// селекторов ключа и значения.
        /// </summary>
        /// <typeparam name="TKey">Тип ключа словаря.</typeparam>
        /// <typeparam name="TValue">Тип значения словаря.</typeparam>
        /// <typeparam name="TFrom">Тип объекта, по которому будет выполняться запрос.</typeparam>
        /// <param name="keySelector">Выражение для выбора ключа.</param>
        /// <param name="valueSelector">Выражение для выбора значения.</param>
        /// <param name="whereExpression">Условие выборки данных. Может быть <c>null</c>.</param>
        /// <param name="fetchRows">Количество строк для выборки. По умолчанию -1 (выбираются все строки).</param>
        /// <param name="offsetRows">Количество строк для пропуска перед выборкой. По умолчанию - 0.</param>
        /// <param name="itemFactory">Функция для создания пары ключ-значение. Может быть <c>null</c>.</param>
        /// <returns>Словарь, содержащий результат выполнения запроса в виде ключ-значение.</returns>
        /// <remarks>Этот метод выполняет SQL-запрос синхронно и преобразует результат в словарь <see cref="Dictionary{TKey, TValue}" />
        /// .</remarks>
        public Dictionary<TKey, TValue> ToDictionary<TKey, TValue, TFrom>(
            Expression<Func<TFrom, TKey>> keySelector,
            Expression<Func<TFrom, TValue>> valueSelector,
            Expression<Func<TFrom, bool>> whereExpression = null,
            int fetchRows = -1,
            int offsetRows = 0,
            Func<object[], string[], KeyValuePair<TKey, TValue>> itemFactory = null)
        {
            string query = (SqlQueryBuilder.GetSelectQuery(
                                this.Options,
                                typeof(TFrom).GetMemberCache(),
                                keySelector.GetMemberCache(),
                                valueSelector.GetMemberCache()) + " " +
                            SqlQueryBuilder.GetWhereClause(
                                whereExpression,
                                this.Options,
                                true,
                                out IReadOnlyDictionary<string, object> cmdParam)).Trim();
            query = SqlQueryBuilder.AddLimitOffsetClauseToQuery(
                fetchRows,
                offsetRows,
                query,
                this.Options,
                typeof(TFrom));
            List<KeyValuePair<TKey, TValue>> list = this.ToList(
                query,
                cmdParam,
                null,
                null,
                null,
                fetchRows,
                offsetRows,
                itemFactory);
            Dictionary<TKey, TValue> dic = list.ToDictionary(x => x.Key, x => x.Value);
            return dic;
        }

        /// <summary>
        /// Асинхронно выполняет SQL-запрос и возвращает результат в виде <see cref="Dictionary{TKey, TValue}" />.
        /// </summary>
        /// <typeparam name="TKey">Тип ключа словаря.</typeparam>
        /// <typeparam name="TValue">Тип значения словаря.</typeparam>
        /// <param name="query">SQL-запрос для выполнения.</param>
        /// <param name="cmdParams">Параметры запроса.</param>
        /// <param name="columns">Столбцы, которые будут выбраны в запросе. Может быть <c>null</c>.</param>
        /// <param name="columnToPropertyMap">Отображение столбцов запроса в свойства объектов. Может быть <c>null</c>.</param>
        /// <param name="fetchRows">Количество строк для выборки. По умолчанию -1 (выбираются все строки).</param>
        /// <param name="offsetRows">Количество строк для пропуска перед выборкой. По умолчанию - 0.</param>
        /// <param name="itemFactory">Функция для создания пары ключ-значение. Может быть <c>null</c>.</param>
        /// <returns>Задача, которая возвращает <see cref="Dictionary{TKey, TValue}" />, содержащий результат выполнения запроса.</returns>
        /// <remarks>Этот метод выполняет SQL-запрос асинхронно и преобразует результат в словарь
        /// <see cref="Dictionary{TKey, TValue}" />.</remarks>
        public async Task<Dictionary<TKey, TValue>> ToDictionaryAsync<TKey, TValue>(
            string query,
            object cmdParams = null,
            IEnumerable<string> columns = null,
            IEnumerable<(string, string)> columnToPropertyMap = null,
            int fetchRows = -1,
            int offsetRows = 0,
            Func<object[], string[], KeyValuePair<TKey, TValue>> itemFactory = null) => (await this.ToListAsync(
                query,
                cmdParams,
                columns,
                columnToPropertyMap,
                null,
                fetchRows,
                offsetRows,
                itemFactory).ConfigureAwait(this.ConfigureAwait)).ToDictionary(x => x.Key, x => x.Value);

        /// <summary>
        /// Асинхронно выполняет SQL-запрос и возвращает результат в виде <see cref="Dictionary{TKey, TValue}" /> с
        /// использованием селекторов ключа и значения.
        /// </summary>
        /// <typeparam name="TKey">Тип ключа словаря.</typeparam>
        /// <typeparam name="TValue">Тип значения словаря.</typeparam>
        /// <typeparam name="TFrom">Тип объекта, по которому будет выполняться запрос.</typeparam>
        /// <param name="keySelector">Выражение для выбора ключа.</param>
        /// <param name="valueSelector">Выражение для выбора значения.</param>
        /// <param name="whereExpression">Условие выборки данных. Может быть <c>null</c>.</param>
        /// <param name="fetchRows">Количество строк для выборки. По умолчанию -1 (выбираются все строки).</param>
        /// <param name="offsetRows">Количество строк для пропуска перед выборкой. По умолчанию - 0.</param>
        /// <param name="itemFactory">Функция для создания пары ключ-значение. Может быть <c>null</c>.</param>
        /// <returns>Задача, которая возвращает <see cref="Dictionary{TKey, TValue}" />, содержащий результат выполнения запроса.</returns>
        /// <remarks>Этот метод выполняет SQL-запрос асинхронно и преобразует результат в словарь
        /// <see cref="Dictionary{TKey, TValue}" />.</remarks>
        public async Task<Dictionary<TKey, TValue>> ToDictionaryAsync<TKey, TValue, TFrom>(
            Expression<Func<TFrom, TKey>> keySelector,
            Expression<Func<TFrom, TValue>> valueSelector,
            Expression<Func<TFrom, bool>> whereExpression = null,
            int fetchRows = -1,
            int offsetRows = 0,
            Func<object[], string[], KeyValuePair<TKey, TValue>> itemFactory = null)
        {
            string query = (SqlQueryBuilder.GetSelectQuery(
                                this.Options,
                                typeof(TFrom).GetMemberCache(),
                                keySelector.GetMemberCache(),
                                valueSelector.GetMemberCache()) + " " +
                            SqlQueryBuilder.GetWhereClause(
                                whereExpression,
                                this.Options,
                                true,
                                out IReadOnlyDictionary<string, object> cmdParam)).Trim();
            query = SqlQueryBuilder.AddLimitOffsetClauseToQuery(
                fetchRows,
                offsetRows,
                query,
                this.Options,
                typeof(TFrom));
            return (await this.ToListAsync(
                query,
                cmdParam,
                null,
                null,
                null,
                fetchRows,
                offsetRows,
                itemFactory).ConfigureAwait(this.ConfigureAwait)).ToDictionary(x => x.Key, x => x.Value);
        }

        /// <summary>
        /// Выполняет SQL-запрос и возвращает результат в виде списка объектов типа <typeparamref name="TItem" />.
        /// </summary>
        /// <typeparam name="TItem">Тип объектов, которые будут содержаться в списке.</typeparam>
        /// <param name="query">SQL-запрос для выполнения. Если <c>null</c>, будет использован стандартный запрос.</param>
        /// <param name="cmdParams">Параметры запроса.</param>
        /// <param name="columns">Список столбцов для выборки. Может быть <c>null</c>.</param>
        /// <param name="columnToPropertyMap">Отображение столбцов SQL-запроса в свойства объектов. Может быть <c>null</c>.</param>
        /// <param name="converter">Конвертер для преобразования данных. Может быть <c>null</c>.</param>
        /// <param name="fetchRows">Количество строк для выборки. По умолчанию -1 (выбираются все строки).</param>
        /// <param name="offsetRows">Количество строк для пропуска перед выборкой. По умолчанию - 0.</param>
        /// <param name="itemFactory">Функция для создания объектов типа <typeparamref name="TItem" />. Может быть <c>null</c>.</param>
        /// <returns>Список объектов типа <typeparamref name="TItem" />.</returns>
        /// <remarks>Этот метод выполняет SQL-запрос синхронно и возвращает результат в виде списка объектов.
        /// Если <paramref name="itemFactory" /> не задан, используется стандартное преобразование данных в объекты.</remarks>
        public List<TItem> ToList<TItem>(
            string query = null,
            object cmdParams = null,
            IEnumerable<string> columns = null,
            IEnumerable<(string, string)> columnToPropertyMap = null,
            DbValueConverter<TItem> converter = null,
            int fetchRows = -1,
            int offsetRows = 0,
            Func<object[], string[], TItem> itemFactory = null)
        {
            List<TItem> list = this.Query<List<TItem>, TItem>(
                query,
                cmdParams,
                columns,
                columnToPropertyMap,
                converter,
                fetchRows,
                offsetRows,
                itemFactory);

            return list;
        }

        /// <summary>
        /// Выполняет SQL-запрос с фильтрацией и возвращает результат в виде списка объектов типа <typeparamref name="T" />.
        /// </summary>
        /// <typeparam name="T">Тип объектов, которые будут содержаться в списке.</typeparam>
        /// <param name="whereExpression">Выражение для фильтрации данных.</param>
        /// <param name="columnToPropertyMap">Отображение столбцов SQL-запроса в свойства объектов. Может быть <c>null</c>.</param>
        /// <param name="converter">Конвертер для преобразования данных. Может быть <c>null</c>.</param>
        /// <param name="fetchRows">Количество строк для выборки. По умолчанию -1 (выбираются все строки).</param>
        /// <param name="offsetRows">Количество строк для пропуска перед выборкой. По умолчанию - 0.</param>
        /// <param name="itemFactory">Функция для создания объектов типа <typeparamref name="T" />. Может быть <c>null</c>.</param>
        /// <param name="orderByExpression">Выражение для сортировки. Может быть <c>null</c>.</param>
        /// <returns>Список объектов типа <typeparamref name="T" />.</returns>
        /// <remarks>Этот метод выполняет SQL-запрос синхронно с фильтрацией по выражению <paramref name="whereExpression" /> и
        /// возвращает результат в виде списка.</remarks>
        public List<T> ToList<T>(
            Expression<Func<T, bool>> whereExpression,
            IEnumerable<(string, string)> columnToPropertyMap = null,
            DbValueConverter<T> converter = null,
            int fetchRows = -1,
            int offsetRows = 0,
            Func<object[], string[], T> itemFactory = null,
            params (Expression<Func<T, object>>, bool)[] orderByExpression)
        {
            string query = (SqlQueryBuilder.GetSelectQuery<T>(this.Options) + " " + SqlQueryBuilder.GetWhereClause(
                                whereExpression,
                                this.Options,
                                true,
                                out IReadOnlyDictionary<string, object> cmdParam) +
                            " " + SqlQueryBuilder.GetOrderBy(this.Options, orderByExpression)).Trim();

            return this.ToList(
                query,
                cmdParam,
                null,
                columnToPropertyMap,
                converter,
                fetchRows,
                offsetRows,
                itemFactory);
        }

        /// <summary>
        /// Асинхронно выполняет SQL-запрос и возвращает результат в виде списка объектов типа <typeparamref name="T" />.
        /// </summary>
        /// <typeparam name="T">Тип объектов, которые будут содержаться в списке.</typeparam>
        /// <param name="query">SQL-запрос для выполнения. Если <c>null</c>, будет использован стандартный запрос.</param>
        /// <param name="cmdParams">Параметры запроса.</param>
        /// <param name="columns">Список столбцов для выборки. Может быть <c>null</c>.</param>
        /// <param name="columnToPropertyMap">Отображение столбцов SQL-запроса в свойства объектов. Может быть <c>null</c>.</param>
        /// <param name="converter">Конвертер для преобразования данных. Может быть <c>null</c>.</param>
        /// <param name="fetchRows">Количество строк для выборки. По умолчанию -1 (выбираются все строки).</param>
        /// <param name="offsetRows">Количество строк для пропуска перед выборкой. По умолчанию - 0.</param>
        /// <param name="itemFactory">Функция для создания объектов типа <typeparamref name="T" />. Может быть <c>null</c>.</param>
        /// <param name="ct">Токен отмены операции.</param>
        /// <returns>Задача, которая возвращает список объектов типа <typeparamref name="T" />.</returns>
        /// <remarks>Этот метод выполняет SQL-запрос асинхронно и возвращает результат в виде списка объектов.
        /// Если <paramref name="itemFactory" /> не задан, используется стандартное преобразование данных в объекты.</remarks>
        public Task<List<T>> ToListAsync<T>(
            string query = null,
            object cmdParams = null,
            IEnumerable<string> columns = null,
            IEnumerable<(string, string)> columnToPropertyMap = null,
            DbValueConverter<T> converter = null,
            int fetchRows = -1,
            int offsetRows = 0,
            Func<object[], string[], T> itemFactory = null,
            CancellationToken ct = default) => this.QueryAsync<List<T>, T>(
                query,
                cmdParams,
                columns,
                columnToPropertyMap,
                converter,
                fetchRows,
                offsetRows,
                itemFactory,
                ct);

        /// <summary>
        /// Асинхронно выполняет SQL-запрос с фильтрацией и возвращает результат в виде списка объектов типа
        /// <typeparamref name="T" />.
        /// </summary>
        /// <typeparam name="T">Тип объектов, которые будут содержаться в списке.</typeparam>
        /// <param name="whereExpression">Выражение для фильтрации данных.</param>
        /// <param name="columnToPropertyMap">Отображение столбцов SQL-запроса в свойства объектов. Может быть <c>null</c>.</param>
        /// <param name="converter">Конвертер для преобразования данных. Может быть <c>null</c>.</param>
        /// <param name="fetchRows">Количество строк для выборки. По умолчанию -1 (выбираются все строки).</param>
        /// <param name="offsetRows">Количество строк для пропуска перед выборкой. По умолчанию - 0.</param>
        /// <param name="itemFactory">Функция для создания объектов типа <typeparamref name="T" />. Может быть <c>null</c>.</param>
        /// <param name="ct">Токен отмены операции.</param>
        /// <param name="orderByExpression">Выражение для сортировки. Может быть <c>null</c>.</param>
        /// <returns>Задача, которая возвращает список объектов типа <typeparamref name="T" />.</returns>
        /// <remarks>Этот метод выполняет SQL-запрос асинхронно с фильтрацией и сортировкой, и возвращает результат в виде списка.</remarks>
        public Task<List<T>> ToListAsync<T>(
            Expression<Func<T, bool>> whereExpression,
            IEnumerable<(string, string)> columnToPropertyMap = null,
            DbValueConverter<T> converter = null,
            int fetchRows = -1,
            int offsetRows = 0,
            Func<object[], string[], T> itemFactory = null,
            CancellationToken ct = default,
            params (Expression<Func<T, object>>, bool)[] orderByExpression)
        {
            string query = (SqlQueryBuilder.GetSelectQuery<T>(this.Options) + " " + SqlQueryBuilder.GetWhereClause(
                                whereExpression,
                                this.Options,
                                true,
                                out IReadOnlyDictionary<string, object> cmdParam) +
                            " " + SqlQueryBuilder.GetOrderBy(this.Options, orderByExpression)).Trim();

            return this.ToListAsync(
                query,
                cmdParam,
                null,
                columnToPropertyMap,
                converter,
                fetchRows,
                offsetRows,
                itemFactory,
                ct);
        }

        /// <summary>
        /// Обновляет запись в базе данных на основе значений свойств объекта.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="item">Объект, содержащий обновляемые значения.</param>
        /// <param name="dbTransaction">Активная транзакция базы данных.
        /// Если не указана, используется текущая транзакция или соединение.</param>
        /// <param name="updateColumns">Список колонок, которые необходимо обновить.
        /// Если не указан, обновляются все сопоставленные свойства,
        /// за исключением первичных ключей.</param>
        /// <returns>Количество строк, затронутых операцией обновления.</returns>
        public int Update<T>(
            T item,
            IDbTransaction dbTransaction = null,
            params Expression<Func<T, object>>[] updateColumns)
            where T : class => this.Update(item, null, dbTransaction, updateColumns);

        /// <summary>
        /// Обновляет записи в базе данных на основе указанного условия.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="item">Объект, содержащий значения для обновления колонок.</param>
        /// <param name="whereExpression">Лямбда-выражение, определяющее условие <c>WHERE</c>.
        /// Если указано, первичный ключ объекта не используется.</param>
        /// <param name="dbTransaction">Активная транзакция базы данных.
        /// Если не указана, используется текущее соединение или транзакция.</param>
        /// <param name="updateColumns">Список колонок, которые необходимо обновить.
        /// Если не указан, обновляются все сопоставленные свойства,
        /// за исключением первичных ключей.</param>
        /// <returns>Количество строк, затронутых операцией обновления.</returns>
        public int Update<T>(
            T item,
            Expression<Func<T, bool>> whereExpression,
            IDbTransaction dbTransaction = null,
            params Expression<Func<T, object>>[] updateColumns)
            where T : class
        {
            string query = SqlQueryBuilder.GetUpdateQuery(this.Options, updateColumns);
            IReadOnlyDictionary<string, object> cmdParams = this.GetParams(item);
            query += " " + (whereExpression != null
                ? SqlQueryBuilder.GetWhereClause(whereExpression, this.Options, true, out cmdParams)
                : SqlQueryBuilder.GetWhereClause<T>(this.Options, out _));

            return this.ExecuteNonQuery(query, cmdParams, dbTransaction);
        }

        /// <summary>
        /// Асинхронно обновляет запись в базе данных на основе значений свойств объекта.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="item">Объект, содержащий обновляемые значения.</param>
        /// <param name="updateColumns">Список колонок, которые необходимо обновить.
        /// Если не указан, обновляются все сопоставленные свойства,
        /// за исключением первичных ключей.</param>
        /// <param name="dbTransaction">Активная транзакция базы данных.
        /// Если не указана, используется текущее соединение или транзакция.</param>
        /// <param name="token">Токен отмены асинхронной операции.</param>
        /// <returns>Задача, результатом которой является количество строк,
        /// затронутых операцией обновления.</returns>
        public Task<int> UpdateAsync<T>(
            T item,
            Expression<Func<T, object>>[] updateColumns = null,
            IDbTransaction dbTransaction = null,
            CancellationToken token = default)
            where T : class => this.UpdateAsync(
                item,
                null,
                updateColumns ?? Array.Empty<Expression<Func<T, object>>>(),
                dbTransaction,
                token);

        /// <summary>
        /// Асинхронно обновляет записи в базе данных на основе переданных данных.
        /// </summary>
        /// <typeparam name="T">Тип объекта, который будет обновлен.</typeparam>
        /// <param name="item">Объект, содержащий обновленные значения.</param>
        /// <param name="whereExpression">Условие для фильтрации записей для обновления. Может быть null, если условие не
        /// требуется.</param>
        /// <param name="updateColumns">Массив столбцов для обновления. Если null, обновляются все столбцы объекта.</param>
        /// <param name="dbTransaction">Транзакция, в рамках которой будет выполнено обновление. Может быть null.</param>
        /// <param name="token">Токен для отмены операции, может быть использован для отмены выполнения операции.</param>
        /// <returns>Задача, которая возвращает количество затронутых строк в базе данных.</returns>
        /// <remarks>Этот метод создает SQL-запрос для обновления, используя параметры, переданные в аргументах метода.
        /// Если условие whereExpression не задано, обновляются все записи в таблице. Метод возвращает количество строк,
        /// которые были обновлены.</remarks>
        public Task<int> UpdateAsync<T>(
            T item,
            Expression<Func<T, bool>> whereExpression,
            Expression<Func<T, object>>[] updateColumns = null,
            IDbTransaction dbTransaction = null,
            CancellationToken token = default)
            where T : class
        {
            IReadOnlyDictionary<string, object> cmdParams = this.GetParams(item);
            string query = SqlQueryBuilder.GetUpdateQuery(this.Options, updateColumns);
            query += " " + (whereExpression != null
                ? SqlQueryBuilder.GetWhereClause(whereExpression, this.Options, true, out cmdParams)
                : SqlQueryBuilder.GetWhereClause<T>(this.Options, out _));

            return this.ExecuteNonQueryAsync(query, cmdParams, dbTransaction, token);
        }

        /// <summary>
        /// Обновляет несколько записей в базе данных в рамках одной транзакции.
        /// </summary>
        /// <typeparam name="T">Тип объекта, который будет обновлен.</typeparam>
        /// <param name="list">Список объектов, содержащих обновленные значения.</param>
        /// <param name="dbTransaction">Транзакция, в рамках которой будет выполнено обновление. Если null, создается новая
        /// транзакция.</param>
        /// <param name="updateColumns">Массив столбцов для обновления. Если null, обновляются все столбцы объекта.</param>
        /// <returns>Количество обновленных строк в базе данных.</returns>
        /// <exception cref="Exception">Вызывается в случае ошибки при выполнении операции обновления.</exception>
        /// <remarks>Этот метод обновляет несколько записей в базе данных, используя переданный список объектов.
        /// Каждый объект в списке обрабатывается и обновляется в базе данных в рамках одной транзакции.
        /// При возникновении ошибки транзакция откатывается, а исключение обрабатывается и повторно выбрасывается.</remarks>
        public int UpdateRange<T>(
            IEnumerable<T> list,
            IDbTransaction dbTransaction = null,
            params Expression<Func<T, object>>[] updateColumns)
            where T : class
        {
            try
            {
                int count = 0;
                using (dbTransaction ?? this.BeginTransaction())
                {
                    string query = SqlQueryBuilder.GetUpdateQuery(this.Options, updateColumns);
                    MemberCache<T> typeCache = MemberCache<T>.Create();
                    Dictionary<string, object> queryParams = new Dictionary<string, object>();
                    using (DbCommand cmd = this.CreateCommand(query, dbTransaction))
                    {
                        foreach (T item in list)
                        {
                            typeCache.ToDictionary(item, queryParams);
                            SetParameterCollection(cmd, queryParams);
                            count += cmd.ExecuteNonQuery();
                            this.CommandExecuted?.Invoke(cmd);
                            this.Log(cmd);
                        }
                    }

                    this.EndTransaction();
                }

                return count;
            }
            catch (Exception ex)
            {
                this.RollbackTransaction();
                throw this.HandleDbException(ex, null);
            }
            finally
            {
                this.CloseConnection();
            }
        }

        /// <summary>
        /// Асинхронно обновляет несколько записей в базе данных в рамках одной транзакции.
        /// </summary>
        /// <typeparam name="T">Тип объекта, который будет обновлен.</typeparam>
        /// <param name="list">Список объектов, содержащих обновленные значения.</param>
        /// <param name="updateColumns">Массив столбцов для обновления. Если null, обновляются все столбцы объекта.</param>
        /// <param name="dbTransaction">Транзакция, в рамках которой будет выполнено обновление. Если null, создается новая
        /// транзакция.</param>
        /// <param name="token">Токен отмены асинхронной операции. Используется для отмены выполнения запроса.</param>
        /// <returns>Задача, которая возвращает количество обновленных строк в базе данных.</returns>
        /// <exception cref="System.NullReferenceException">dbCmd.</exception>
        /// <remarks>Этот метод асинхронно обновляет несколько записей в базе данных, используя переданный список объектов.
        /// Каждый объект в списке обрабатывается и обновляется в базе данных в рамках одной транзакции.
        /// При возникновении ошибки транзакция откатывается, а исключение обрабатывается и повторно выбрасывается.</remarks>
        public async Task<int> UpdateRangeAsync<T>(
            IEnumerable<T> list,
            Expression<Func<T, object>>[] updateColumns = null,
            IDbTransaction dbTransaction = null,
            CancellationToken token = default)
            where T : class
        {
            try
            {
                int count = 0;
                using (dbTransaction ?? this.BeginTransaction())
                {
                    string query = SqlQueryBuilder.GetUpdateQuery(this.Options, updateColumns);
                    MemberCache<T> typeCache = MemberCache<T>.Create();
                    Dictionary<string, object> queryParams = new Dictionary<string, object>();
                    using (DbCommand cmd = this.CreateCommand(query, dbTransaction))
                    {
                        if (!(cmd is DbCommand dbCmd))
                        {
                            throw new NullReferenceException(nameof(dbCmd));
                        }

                        foreach (T item in list)
                        {
                            typeCache.ToDictionary(item, queryParams);
                            SetParameterCollection(cmd, queryParams);
                            count += await dbCmd.ExecuteNonQueryAsync(token).ConfigureAwait(this.ConfigureAwait);
                            this.CommandExecuted?.Invoke(cmd);
                            this.Log(cmd);
                        }
                    }

                    this.EndTransaction();
                }

                return count;
            }
            catch (Exception ex)
            {
                this.RollbackTransaction();
                throw this.HandleDbException(ex, null);
            }
            finally
            {
                this.CloseConnection();
            }
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!this.IsDisposed && disposing)
            {
                this.CloseConnection();
                this.Connection?.Dispose();
                this.IsDisposed = true;
            }
        }

        /// <summary>
        /// Changes the type.
        /// </summary>
        /// <typeparam name="T">Type.</typeparam>
        /// <param name="value">The value.</param>
        /// <returns>T.</returns>
        private static T ChangeType<T>(object value) => (T)ChangeType(value, typeof(T));

        /// <summary>
        /// Changes the type.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="targetType">Type of the target.</param>
        /// <returns>System.Object.</returns>
        private static object ChangeType(object value, Type targetType) => Obj.ChangeType(value, targetType);

        /// <summary>
        /// Replaces the parameter token.
        /// </summary>
        /// <param name="sql">The SQL.</param>
        /// <param name="token">The token.</param>
        /// <param name="replacement">The replacement.</param>
        /// <returns>System.String.</returns>
        private static string ReplaceParameterToken(string sql, string token, string replacement) => Regex.Replace(
                sql,
                $@"(?<![\w@]){Regex.Escape(token)}(?!\w)",
                replacement,
                RegexOptions.CultureInvariant);

        /// <summary>
        /// Begins the connection.
        /// </summary>
        private void BeginConnection() => this.BeginConnection(this.Connection);

        /// <summary>
        /// Begins the connection.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <exception cref="System.ArgumentNullException">connection.</exception>
        /// <exception cref="System.InvalidOperationException">Не удалось открыть соединение с базой данных.</exception>
        private void BeginConnection(IDbConnection connection)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            try
            {
                if (connection.State == ConnectionState.Broken)
                {
                    connection.Close();
                }

                if (connection.State != ConnectionState.Open)
                {
                    connection.Open();
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Не удалось открыть соединение с базой данных.", ex);
            }
        }

        /// <summary>
        /// Begins the connection asynchronous.
        /// </summary>
        /// <param name="token">The cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>Task&lt;IDbConnection&gt;.</returns>
        private Task<IDbConnection> BeginConnectionAsync(CancellationToken token = default) => this.BeginConnectionAsync(this.Connection, token);

        /// <summary>
        /// Begin connection as an asynchronous operation.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <param name="token">The cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A Task&lt;IDbConnection&gt; representing the asynchronous operation.</returns>
        /// <exception cref="System.ArgumentNullException">connection.</exception>
        /// <exception cref="System.InvalidOperationException">Не удалось открыть соединение с базой данных.</exception>
        private async Task<IDbConnection> BeginConnectionAsync(
            IDbConnection connection,
            CancellationToken token = default)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            try
            {
                if (connection.State == ConnectionState.Broken)
                {
                    connection.Close();
                }

                if (connection.State == ConnectionState.Open)
                {
                    return connection;
                }

                if (connection is DbConnection dc)
                {
                    await dc.OpenAsync(token).ConfigureAwait(this.ConfigureAwait);
                }
                else
                {
                    connection.Open();
                }

                return connection;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Не удалось открыть соединение с базой данных.", ex);
            }
        }

        /// <summary>
        /// Builds the item factory.
        /// </summary>
        /// <param name="itemTypeCache">The item type cache.</param>
        /// <param name="columnToPropertyMap">The column to property map.</param>
        /// <returns>Func&lt;System.Object[], System.String[], T&gt;.</returns>
        private Func<object[], string[], T> BuildItemFactory<T>(
            MemberCache<T> itemTypeCache,
            IEnumerable<(string, string)> columnToPropertyMap)
        {
            ConstructorInfo ctor = itemTypeCache.Constructors.FirstOrDefault(x => x.IsPublic) ??
                                   itemTypeCache.Constructors.FirstOrDefault();
            ParameterInfo[] ctorParams = ctor?.GetParameters() ?? Array.Empty<ParameterInfo>();

            if (ctorParams.Length == 0)
            {
                return (values, names) => itemTypeCache.DefaultConstructor();
            }

            return (values, names) =>
            {
                if (ctorParams.Length > values.Length)
                {
                    throw new InvalidOperationException(
                        $"Недостаточно значений для вызова конструктора типа {typeof(T).FullName}.");
                }

                object[] args = new object[ctorParams.Length];

                int[] indexes = ctorParams
                    .Select(p =>
                        names.IndexOf(n =>
                            p.Name.Equals(
                                columnToPropertyMap?.FirstOrDefault(m => m.Item1 == n).Item2 ?? n,
                                StringComparison.OrdinalIgnoreCase)))
                    .ToArray();

                if (indexes.All(i => i >= 0))
                {
                    for (int i = 0; i < indexes.Length; i++)
                    {
                        args[i] = ChangeType(values[indexes[i]], ctorParams[i].ParameterType);
                    }
                }
                else
                {
                    for (int i = 0; i < ctorParams.Length; i++)
                    {
                        args[i] = ChangeType(values[i], ctorParams[i].ParameterType);
                    }
                }

                return (T)ctor?.Invoke(args);
            };
        }

        /// <summary>
        /// Builds the item factory.
        /// </summary>
        /// <param name="itemTypeCache">The item type cache.</param>
        /// <param name="columnToPropertyMap">The column to property map.</param>
        /// <returns>Func&lt;System.Object[], System.String[], System.Object&gt;.</returns>
        private Func<object[], string[], object> BuildItemFactory(
            MemberCache itemTypeCache,
            IEnumerable<(string, string)> columnToPropertyMap)
        {
            ConstructorInfo ctor = itemTypeCache.Constructors.FirstOrDefault(x => x.IsPublic) ??
                                   itemTypeCache.Constructors.FirstOrDefault();
            ParameterInfo[] ctorParams = ctor?.GetParameters() ?? Array.Empty<ParameterInfo>();

            if (ctorParams.Length == 0)
            {
                return (values, names) => itemTypeCache.DefaultConstructor();
            }

            return (values, names) =>
            {
                if (ctorParams.Length > values.Length)
                {
                    throw new InvalidOperationException(
                        $"Недостаточно значений для вызова конструктора типа {itemTypeCache.Type.FullName}.");
                }

                if (values.Length == 0)
                {
                    return itemTypeCache.DefaultConstructor();
                }

                object[] args = new object[ctorParams.Length];

                int[] indexes = ctorParams
                    .Select(p =>
                        names.IndexOf(n =>
                            p.Name.Equals(
                                columnToPropertyMap?.FirstOrDefault(m => m.Item1 == n).Item2 ?? n,
                                StringComparison.OrdinalIgnoreCase)))
                    .ToArray();

                if (indexes.All(i => i >= 0))
                {
                    for (int i = 0; i < indexes.Length; i++)
                    {
                        args[i] = ChangeType(values[indexes[i]], ctorParams[i].ParameterType);
                    }
                }
                else
                {
                    for (int i = 0; i < ctorParams.Length; i++)
                    {
                        args[i] = ChangeType(values[i], ctorParams[i].ParameterType);
                    }
                }

                return ctor?.Invoke(args);
            };
        }

        /// <summary>
        /// Closes the connection.
        /// </summary>
        private void CloseConnection() => this.CloseConnection(this.Connection);

        /// <summary>
        /// Closes the connection.
        /// </summary>
        /// <param name="con">The con.</param>
        /// <exception cref="System.NullReferenceException">con.</exception>
        private void CloseConnection(IDbConnection con)
        {
            if (this.tr.Value != null)
            {
                return;
            }

            if (con == null)
            {
                throw new NullReferenceException(nameof(con));
            }

            if (con.State != ConnectionState.Closed)
            {
                con.Close();
            }
        }

        /// <summary>
        /// Gets the reader field to property map.
        /// </summary>
        /// <param name="reader">The reader.</param>
        /// <param name="customMap">The custom map.</param>
        /// <param name="onlyFromCustomMap">if set to <c>true</c> [only from custom map].</param>
        /// <returns>Dictionary&lt;System.Int32, System.String&gt;.</returns>
        private Dictionary<int, string> GetReaderFieldToPropertyMap(
            IDataReader reader,
            IEnumerable<(string, string)> customMap = null,
            bool onlyFromCustomMap = true)
        {
            Dictionary<string, string> customMapDic =
                customMap?.ToDictionary(k => k.Item1, v => v.Item2) ?? new Dictionary<string, string>();
            Dictionary<int, string> map = new Dictionary<int, string>();

            int columnsCount = reader.FieldCount;

            for (int i = 0; i < columnsCount; i++)
            {
                int colIndex = i;
                string colName = reader.GetName(i);

                if (customMapDic.Count > 0 && customMapDic.TryGetValue(colName, out string mappedColumn))
                {
                    map[colIndex] = mappedColumn;
                    if (onlyFromCustomMap)
                    {
                        continue;
                    }
                }

                map[colIndex] = colName;
            }

            return map;
        }

        /// <summary>
        /// Gets the reader field to property map.
        /// </summary>
        /// <param name="itemType">Type of the item.</param>
        /// <param name="reader">The reader.</param>
        /// <param name="customMap">The custom map.</param>
        /// <param name="columns">The columns.</param>
        /// <returns>Dictionary&lt;System.Int32, MemberCache&gt;.</returns>
        private Dictionary<int, MemberCache> GetReaderFieldToPropertyMap(
            Type itemType,
            IDataReader reader,
            IEnumerable<(string, string)> customMap = null,
            IEnumerable<string> columns = null)
        {
            if (customMap == null)
            {
                customMap = this.Options.Map?.GetColumnToPropertyMap(itemType);
            }

            Dictionary<string, string> customMapDic =
                customMap?.ToDictionary(k => k.Item1, v => v.Item2) ?? new Dictionary<string, string>();
            Dictionary<int, MemberCache> map = new Dictionary<int, MemberCache>();
            MemberCache typeInfoEx = MemberCache.Create(itemType);
            int columnsCount = reader.FieldCount;

            for (int i = 0; i < columnsCount; i++)
            {
                int colIndex = i;
                string colName = reader.GetName(i);
                MemberCache propInfoEx;
                if (customMap != null)
                {
                    propInfoEx = typeInfoEx.PublicBasicProperties.GetValueOrDefault(
                        customMapDic.GetValueOrDefault(colName, IgnoreCaseComparer),
                        IgnoreCaseComparer);
                    if (propInfoEx != null)
                    {
                        map[colIndex] = propInfoEx;
                        continue;
                    }
                }

                propInfoEx = typeInfoEx.ColumnProperties
                    .FirstOrDefault(x => IgnoreCaseComparer.Equals(x.Value.ColumnName, colName)).Value;
                if (propInfoEx != null)
                {
                    map[colIndex] = propInfoEx;
                    continue;
                }

                propInfoEx = typeInfoEx.PublicBasicProperties
                    .FirstOrDefault(x => IgnoreCaseComparer.Equals(x.Value.ColumnName, colName)).Value;

                if (propInfoEx != null)
                {
                    map[colIndex] = propInfoEx;
                    continue;
                }

                map.Remove(colIndex);
            }

            if (columns?.Any() != true)
            {
                return map;
            }

            List<int> itemsToRemove =
                map.Where(kv => !columns.Contains(kv.Value.ColumnName)).Select(kv => kv.Key).ToList();
            foreach (int item in itemsToRemove)
            {
                map.Remove(item);
            }

            return map;
        }

        /// <summary>
        /// Handles the database exception.
        /// </summary>
        /// <param name="ex">The ex.</param>
        /// <param name="cmd">The command.</param>
        /// <param name="methodName">Name of the method.</param>
        /// <returns>InvalidOperationException.</returns>
        private InvalidOperationException HandleDbException(
            Exception ex,
            IDbCommand cmd,
            [CallerMemberName] string methodName = "")
        {
            string errorMessage = $"Ошибка в методе {methodName}. " +
                                  $"Запрос: {cmd?.CommandText}. " +
                                  $"Параметры: {string.Join(", ", cmd == null ? Array.Empty<string>() : cmd.Parameters.Cast<IDbDataParameter>().Select(p => $"{p.ParameterName}={p.Value}"))}";

            this.CommandFailed?.Invoke(cmd, ex);
            this.Log(errorMessage);
            return new InvalidOperationException(errorMessage, ex);
        }

        /// <summary>
        /// Logs the specified command.
        /// </summary>
        /// <param name="cmd">The command.</param>
        private void Log(IDbCommand cmd)
        {
            if (!this.EnableLogging)
            {
                return;
            }

            this.Log(this.GetRawSql(cmd));
        }

        /// <summary>
        /// Logs the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        private void Log(string message)
        {
            if (!this.EnableLogging)
            {
                return;
            }

            DateTime now = DateTimeHelper.ExactNow();
            this.queryLogs.Set(now, string.Format("{0:yyyy-MM-dd HH:mm:ss.ffff}", now) + ": " + message);
        }

        /// <summary>
        /// Logs the command.
        /// </summary>
        /// <param name="cmd">The command.</param>
        [Conditional("DEBUG")]
        private void LogCommand(IDbCommand cmd)
        {
            Debug.WriteLine($"Executing SQL: {cmd.CommandText}");
            foreach (IDbDataParameter p in cmd.Parameters)
            {
                Debug.WriteLine($"  {p.ParameterName} = {p.Value}");
            }
        }

        /// <summary>
        /// Read core as an asynchronous operation.
        /// </summary>
        /// <typeparam name="TList">The type of the t list.</typeparam>
        /// <typeparam name="T">Type.</typeparam>
        /// <param name="reader">The reader.</param>
        /// <param name="columns">The columns.</param>
        /// <param name="columnToPropertyMap">The column to property map.</param>
        /// <param name="converter">The converter.</param>
        /// <param name="fetchRows">The fetch rows.</param>
        /// <param name="itemFactory">The item factory.</param>
        /// <param name="isAsync">if set to <c>true</c> [is asynchronous].</param>
        /// <param name="ct">The cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A Task&lt;TList&gt; representing the asynchronous operation.</returns>
        /// <exception cref="System.InvalidOperationException">Не удалось получить значение поля '{fieldName}' типа '{dataType?.FullName}'.</exception>
        private async Task<TList> ReadCoreAsync<TList, T>(
            DbDataReader reader,
            IEnumerable<string> columns,
            IEnumerable<(string, string)> columnToPropertyMap,
            DbValueConverter<T> converter,
            int fetchRows,
            Func<object[], string[], T> itemFactory,
            bool isAsync,
            CancellationToken ct)
            where TList : ICollection<T>, new()
        {
            TList list = new TList();

            MemberCache<T> itemTypeCache = MemberCache<T>.Create();
            object[] readerValues = new object[reader.FieldCount];
            string[] readerColumns = Enumerable.Range(0, reader.FieldCount)
                .Select(reader.GetName)
                .ToArray();

            int rowCount = 0;

            if (itemTypeCache.IsBasic)
            {
                int colIndex = columns?.Select(reader.GetOrdinal).FirstOrDefault() ?? 0;

                while (isAsync ? await reader.ReadAsync(ct).ConfigureAwait(this.ConfigureAwait) : reader.Read())
                {
                    if (fetchRows > 0 && rowCount >= fetchRows)
                    {
                        break;
                    }

                    T value = isAsync
                        ? await reader.GetFieldValueAsync<T>(colIndex, ct).ConfigureAwait(this.ConfigureAwait)
                        : reader.GetFieldValue<T>(colIndex);

                    list.Add(converter(readerColumns[0], value, null, value));
                    rowCount++;
                }

                return list;
            }

            Dictionary<int, MemberCache> map = this.GetReaderFieldToPropertyMap(
                typeof(T),
                reader,
                columnToPropertyMap,
                columns);
            DbValueConverter<T> valueConverter = converter ?? this.ValueConverter.ToTypedConverter<T>();

            while (isAsync ? await reader.ReadAsync(ct).ConfigureAwait(this.ConfigureAwait) : reader.Read())
            {
                try
                {
                    reader.GetValues(readerValues);
                }
                catch
                {
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        try
                        {
                            readerValues[i] = reader.GetValue(i);
                        }
                        catch (Exception ex)
                        {
                            string fieldName = reader.GetName(i);
                            Type dataType = reader.GetFieldType(i);
                            throw new InvalidOperationException(
                                $"Не удалось получить значение поля '{fieldName}' типа '{dataType?.FullName}'.",
                                ex);
                        }
                    }
                }

                T item = itemFactory(readerValues, readerColumns);

                if (!itemTypeCache.IsValueType)
                {
                    foreach (KeyValuePair<int, MemberCache> kv in map)
                    {
                        object raw = readerValues[kv.Key];

                        if (raw == null || raw == DBNull.Value)
                        {
                            kv.Value.Setter(item, null);
                            continue;
                        }

                        object value = valueConverter(reader.GetName(kv.Key), raw, kv.Value, item);
                        kv.Value.Setter(item, value);
                    }
                }

                list.Add(item);
                rowCount++;
            }

            return list;
        }

        /// <summary>
        /// Read core as an asynchronous operation.
        /// </summary>
        /// <param name="returnType">Type of the return.</param>
        /// <param name="reader">The reader.</param>
        /// <param name="columns">The columns.</param>
        /// <param name="columnToPropertyMap">The column to property map.</param>
        /// <param name="converter">The converter.</param>
        /// <param name="fetchRows">The fetch rows.</param>
        /// <param name="itemFactory">The item factory.</param>
        /// <param name="isAsync">if set to <c>true</c> [is asynchronous].</param>
        /// <param name="ct">The cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A Task&lt;IEnumerable`1&gt; representing the asynchronous operation.</returns>
        /// <exception cref="System.InvalidOperationException">Не удалось получить значение поля '{fieldName}' типа '{dataType?.FullName}'.</exception>
        private async Task<IEnumerable<object>> ReadCoreAsync(
            Type returnType,
            DbDataReader reader,
            IEnumerable<string> columns,
            IEnumerable<(string, string)> columnToPropertyMap,
            DbValueConverter<object> converter,
            int fetchRows,
            Func<object[], string[], object> itemFactory,
            bool isAsync,
            CancellationToken ct)
        {
            IList list = Obj.New(returnType) as IList;

            MemberCache itemTypeCache = MemberCache.Create(returnType).ElementType;
            object[] readerValues = new object[reader.FieldCount];
            string[] readerColumns = Enumerable.Range(0, reader.FieldCount)
                .Select(reader.GetName)
                .ToArray();

            int rowCount = 0;

            if (itemTypeCache.IsBasic)
            {
                int colIndex = columns?.Select(reader.GetOrdinal).FirstOrDefault() ?? 0;

                while (isAsync ? await reader.ReadAsync(ct).ConfigureAwait(this.ConfigureAwait) : reader.Read())
                {
                    if (fetchRows > 0 && rowCount >= fetchRows)
                    {
                        break;
                    }

                    object value = isAsync
                        ? await reader.GetFieldValueAsync<object>(colIndex, ct).ConfigureAwait(this.ConfigureAwait)
                        : reader.GetFieldValue<object>(colIndex);

                    list.Add(converter(readerColumns[0], value, null, value));
                    rowCount++;
                }

                return (IEnumerable<object>)list;
            }

            Dictionary<int, MemberCache> map = this.GetReaderFieldToPropertyMap(
                itemTypeCache,
                reader,
                columnToPropertyMap,
                columns);
            DbValueConverter<object> valueConverter = converter ?? this.ValueConverter;

            while (isAsync ? await reader.ReadAsync(ct).ConfigureAwait(this.ConfigureAwait) : reader.Read())
            {
                try
                {
                    reader.GetValues(readerValues);
                }
                catch
                {
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        try
                        {
                            readerValues[i] = reader.GetValue(i);
                        }
                        catch (Exception ex)
                        {
                            string fieldName = reader.GetName(i);
                            Type dataType = reader.GetFieldType(i);
                            throw new InvalidOperationException(
                                $"Не удалось получить значение поля '{fieldName}' типа '{dataType?.FullName}'.",
                                ex);
                        }
                    }
                }

                object item = itemFactory(readerValues, readerColumns);

                if (!itemTypeCache.IsValueType)
                {
                    foreach (KeyValuePair<int, MemberCache> kv in map)
                    {
                        object raw = readerValues[kv.Key];

                        if (raw == null || raw == DBNull.Value)
                        {
                            kv.Value.Setter(item, null);
                            continue;
                        }

                        object value = valueConverter(reader.GetName(kv.Key), raw, kv.Value, item);
                        kv.Value.Setter(item, value);
                    }
                }

                list.Add(item);
                rowCount++;
            }

            return (IEnumerable<object>)list;
        }

        /// <summary>
        /// Rollbacks the transaction.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">Транзакция не была начата.</exception>
        private void RollbackTransaction()
        {
            if (this.tr.Value == null)
            {
                throw new InvalidOperationException("Транзакция не была начата.");
            }

            this.tr.Value?.Rollback();
        }
    }
}