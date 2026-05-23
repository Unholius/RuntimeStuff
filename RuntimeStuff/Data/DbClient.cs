// <copyright file="DbClient.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace System.Data
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Data.Common;
    using System.Diagnostics;
    using System.Helpers;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Универсальный клиент доступа к базе данных с поддержкой CRUD-операций,
    /// транзакций, агрегаций и асинхронного выполнения команд.
    /// </summary>
    /// <remarks>Предназначен для использования как легковесная альтернатива ORM.</remarks>
    public partial class DbClient : IDisposable
    {
        private static readonly ConditionalWeakTable<IDbConnection, DbClient> ClientCache = new();
        private readonly IReadOnlyDictionary<string, object> emptyParams = new Dictionary<string, object>();
        private readonly AsyncLocal<IDbTransaction> tr = new();
        private int queryLogMaxSize = 100;
        private ConcurrentLogBuffer<string> queryLogs = new(100);

        /// <summary>
        /// Initializes a new instance of the <see cref="DbClient" /> class.
        /// </summary>
        /// <param name="commandTimeout">Максимальное время исполнения команды.</param>
        public DbClient(int commandTimeout = 45)
        {
            this.CommandTimeout = commandTimeout;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DbClient" /> class.
        /// </summary>
        /// <param name="map">Сопоставление типов и имен сущностей в БД.</param>
        /// <param name="commandTimeout">Максимальное время исполнения команды.</param>
        public DbClient(DbEntityMap map = null, int commandTimeout = 45)
            : this(commandTimeout)
        {
            this.ValueConverter = (fieldName, fieldValue, propInfo, item) =>
                ChangeType(fieldValue is string s ? s.Trim() : fieldValue, propInfo.PropertyType);

            this.Options.Map = map;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DbClient" /> class.
        /// </summary>
        /// <param name="con">The con.</param>
        /// <param name="map">Сопоставление типов и имен сущностей в БД.</param>
        /// <param name="commandTimeout">Максимальное время исполнения команды.</param>
        /// <exception cref="System.ArgumentNullException">con.</exception>
        public DbClient(IDbConnection con, DbEntityMap map = null, int commandTimeout = 45)
            : this(map, commandTimeout)
        {
            this.Connection = con ?? throw new ArgumentNullException(nameof(con));
            this.Options = SqlOptions.GetInstance(con.GetType().Name);
            this.Options.Map = map;
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="DbClient"/> class.
        /// Финализатор класса <see cref="DbClient" />.
        /// </summary>
        /// <remarks>Вызывается сборщиком мусора, если объект не был явно освобождён.</remarks>
        ~DbClient()
        {
            this.Dispose(true);
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
        /// Таймаут выполнения SQL-команд по умолчанию (в секундах).
        /// </summary>
        /// <value>The default command timeout.</value>
        public static int DefaultCommandTimeout { get; set; } = 30;

        /// <summary>
        /// Количество попыток выполнить команду при timeout exception.
        /// </summary>
        public static int RetryCount { get; set; } = 3;

        /// <summary>
        /// Количество секунд на которое будет увеличино время ожидания выполнения команды при повторе.
        /// </summary>
        public static int RetryTimeoutStep { get; set; } = 10;

        /// <summary>
        /// the trim chars.
        /// </summary>
        /// <value>The trim chars.</value>
        public static char[] TrimChars { get; set; } = ['\uFEFF', '\u200B', ' ', '\r', '\n', '\t'];

        /// <summary>
        /// the trim string spaces.
        /// </summary>
        public static DbValueConverter TrimStringSpaces { get; } = (name, value, info, item) => value is string s ? s.Trim(TrimChars) : ChangeType(value, info.PropertyType);

        /// <summary>
        /// a value indicating whether определяет, использовать ли ConfigureAwait(false) для асинхронных операций.
        /// </summary>
        /// <value><c>true</c> if [configure await]; otherwise, <c>false</c>.</value>
        public bool ConfigureAwait { get; set; } = false;

        /// <summary>
        /// Соединение с базой данных.
        /// </summary>
        /// <value>The connection.</value>
        public IDbConnection Connection { get; set; }

        /// <summary>
        /// Включить логирование запросов.
        /// </summary>
        /// <value><c>true</c> if [enable logging]; otherwise, <c>false</c>.</value>
        public bool EnableLogging { get; set; }

        /// <summary>
        /// Использовать пул строк для оптимизации памяти при работе с большим количеством строковых данных. Если включено, строки будут интернированы через <see cref="StringPool" />, что может снизить использование памяти за счет повторного использования одинаковых строк.
        /// </summary>
        public bool EnableStringPool { get; set; }

        /// <summary>
        /// Признак того, что экземпляр <see cref="DbClient" /> был освобождён.
        /// </summary>
        /// <value><c>true</c> if this instance is disposed; otherwise, <c>false</c>.</value>
        /// <remarks>Устанавливается в <c>true</c> после вызова метода <see cref="Dispose()" />.</remarks>
        public bool IsDisposed { get; private set; }

        /// <summary>
        /// Последний запущенный запрос.
        /// </summary>
        public string LastQuery { get; private set; }

        /// <summary>
        /// Параметры SQL-провайдера (кавычки, префиксы параметров, синтаксис LIMIT/OFFSET и т.п.).
        /// </summary>
        /// <value>The options.</value>
        /// <remarks>Свойство является ковариантным (<c>out T</c>) и предназначено
        /// только для чтения. Для изменения опций рекомендуется использовать
        /// методы самого объекта опций или создавать новый экземпляр.</remarks>
        public SqlOptions Options { get; set; } = new SqlOptions();

        /// <summary>
        /// Имена колонок, для которых будет использоваться пул строк. Если <see cref="EnableStringPool" /> включён, строки из этих колонок будут интернированы через <see cref="StringPool" />.
        /// </summary>
        public HashSet<string> PooledStringColumns { get; } = new();

        /// <summary>
        /// Максимальное количество записей в логах запросов. При достижении этого количества, самые старые записи будут удаляться.
        /// </summary>
        public int QueryLogMaxSize
        {
            get => this.queryLogMaxSize;
            set
            {
                this.queryLogMaxSize = value;
                this.queryLogs = new ConcurrentLogBuffer<string>(this.queryLogMaxSize);
            }
        }

        /// <summary>
        /// Таймаут выполнения SQL-команд по умолчанию (в секундах) (default: null <see cref="DefaultCommandTimeout"/>).
        /// </summary>
        /// <value>The default command timeout.</value>
        public int? CommandTimeout { get; set; }

        /// <summary>
        /// Коллекция логов выполненных SQL-запросов. Содержит текст SQL-запросов, которые были выполнены через этот экземпляр <see cref="DbClient" />.
        /// </summary>
        public IEnumerable<string> QueryLogs => this.queryLogs;

        /// <summary>
        /// Использовать полные имена в запросах: к колонкам добавляется имя таблицы.
        /// </summary>
        public bool UseFullNamesInQueries { get; set; }

        /// <summary>
        /// Функция преобразования значений, полученных из БД, в значения свойств объектов.
        /// </summary>
        /// <value>The value converter.</value>
        public DbValueConverter<object> ValueConverter { get; set; }

        /// <summary>
        /// Создаёт или возвращает кэшированный экземпляр <see cref="DbClient" />
        /// для указанного соединения.
        /// </summary>
        /// <param name="con">Соединение с базой данных.</param>
        /// <param name="commandTimeout">Максимальное время исполнения команды.</param>
        /// <returns>Экземпляр <see cref="DbClient" />.</returns>
        public static DbClient Create(IDbConnection con, int commandTimeout = 45)
        {
            if (con == null)
            {
                throw new ArgumentNullException(nameof(con));
            }

            var db = ClientCache.GetValue(
                con,
                key => new DbClient(key, null, commandTimeout));

            db.CommandTimeout = commandTimeout;
            return db;
        }

        /// <summary>
        /// Создаёт или возвращает кэшированный экземпляр <see cref="DbClient" />
        /// для указанного соединения.
        /// </summary>
        /// <typeparam name="T">Тип соединения, наследующий от <see cref="IDbConnection" /> и имеющий конструктор без параметров.</typeparam>
        /// <param name="commandTimeout">Максимальное время исполнения команды.</param>
        /// <returns>Экземпляр <see cref="DbClient" />.</returns>
        public static DbClient<T> Create<T>(int commandTimeout = 45)
            where T : IDbConnection, new()
        {
            var con = new T();
            var db = (DbClient<T>)ClientCache.GetValue(
                con,
                key => new DbClient<T>(con, commandTimeout));

            db.CommandTimeout = commandTimeout;
            return db;
        }

        /// <summary>
        /// Получить словарь ключевых параметров для типа {T}.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="item">Экземпляр сущности.</param>
        /// <param name="id">Значения ключевых полей.</param>
        /// <returns>Словарь ключевых параметров.</returns>
        public static IReadOnlyDictionary<string, object> GetKeyParams<T>(T item, params object[] id)
        {
            var parameters = new Dictionary<string, object>();
            var typeCache = MemberCache.Get<T>();
            for (var i = 0; i < typeCache.PrimaryKeys.Length; i++)
            {
                parameters[typeCache.PrimaryKeys[i].Name] = i < id.Length && id[i] != null ? id[i] : typeCache.PrimaryKeys[i].Getter(item);
            }

            return parameters;
        }

        /// <summary>
        /// Определение является ли исключение command timeout exception.
        /// </summary>
        /// <param name="ex">Исключение при выполнении DbCommand.</param>
        /// <returns>Является ли исключение command timeout exception.</returns>
        public static bool IsTimeoutException(Exception ex)
        {
            do
            {
                if (ex is DbException dbEx)
                {
                    // SQL Server
                    if (dbEx.GetType().Name == "SqlException")
                    {
                        var numberProp = dbEx.GetType().GetProperty("Number");
                        if (numberProp != null)
                        {
                            var number = (int)numberProp.GetValue(dbEx);
                            if (number == -2)
                            {
                                return true;
                            }
                        }
                    }

                    // PostgreSQL / MySQL / fallback
                    if (dbEx.Message.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }

                ex = ex.InnerException;
            }
            while (ex != null);

            return false;
        }

        /// <summary>
        /// Устанавливает коллекцию параметров для команды.
        /// </summary>
        /// <param name="cmd">Команда, для которой устанавливаются параметры.</param>
        /// <param name="cmdParams">Коллекция параметров в виде словаря, где ключ — имя параметра, а значение — его значение.</param>
        /// <remarks>Этот метод устанавливает параметры для команды. Если параметр уже существует, его значение обновляется.</remarks>
        public static void SetParameterCollection(IDbCommand cmd, Dictionary<string, object> cmdParams)
        {
            foreach (var cp in cmdParams)
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
                columnSelectors.Length > 0 ? columnSelectors.Select(c => (c, aggFunction)).ToArray() : [((Expression<Func<TFrom, object>>)null, aggFunction)]);

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
            params (Expression<Func<TFrom, object>> Column, string AggFunction)[] columnSelectors)
            where TFrom : class
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            var query = SqlQueryHelper.GetAggSelectClause(this.Options, columnSelectors);

            if (whereExpression != null)
            {
                query += " " + SqlQueryHelper.GetWhereClause(this.Options, whereExpression, false, out _);
            }

            var table = this.ToDataTable(query);
            if (table == null || table.Rows.Count == 0)
            {
                return result;
            }

            foreach (DataColumn dc in table.Columns)
            {
                var value = table.Rows[0][dc.ColumnName];
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
                columnSelectors.Length > 0 ? columnSelectors.Select(c => (c, aggFunction)).ToArray() : [((Expression<Func<TFrom, object>>)null, aggFunction)]);

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
            params (Expression<Func<TFrom, object>> Column, string AggFunction)[] columnSelectors)
            where TFrom : class
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            var query = SqlQueryHelper.GetAggSelectClause(this.Options, columnSelectors);

            if (whereExpression != null)
            {
                query += " " + SqlQueryHelper.GetWhereClause(this.Options, whereExpression, false, out _);
            }

            var table = await this.ToDataTableAsync(query, token: token).ConfigureAwait(this.ConfigureAwait);
            if (table == null || table.Rows.Count == 0)
            {
                return result;
            }

            foreach (DataColumn dc in table.Columns)
            {
                var value = table.Rows[0][dc.ColumnName];
                result[dc.ColumnName] = value;
            }

            return result;
        }

        /// <summary>
        /// Получает среднее значение для указанного столбца.
        /// </summary>
        /// <typeparam name="TFrom">Тип сущности.</typeparam>
        /// <typeparam name="T">Тип данных.</typeparam>
        /// <param name="columnSelector">Выражение для выбора столбца, для которого будет вычислено среднее значение.</param>
        /// <param name="whereExpression">The where expression.</param>
        /// <returns>Среднее значение для указанного столбца.</returns>
        /// <remarks>Этот метод использует SQL-функцию AVG для получения среднего значения в столбце.</remarks>
        public T Avg<TFrom, T>(
            Expression<Func<TFrom, T>> columnSelector,
            Expression<Func<TFrom, bool>> whereExpression = null)
            where TFrom : class => ChangeType<T>(this.Agg("AVG", whereExpression, columnSelector.ConvertExpression()).Values.FirstOrDefault());

        /// <summary>
        /// Асинхронно получает среднее значение для указанного столбца.
        /// </summary>
        /// <typeparam name="TFrom">Тип сущности.</typeparam>
        /// <typeparam name="T">Тип данных.</typeparam>
        /// <param name="columnSelector">Выражение для выбора столбца, для которого будет вычислено среднее значение.</param>
        /// <param name="whereExpression">The where expression.</param>
        /// <param name="token">Токен отмены асинхронной операции.</param>
        /// <returns>Задача, которая возвращает среднее значение для указанного столбца.</returns>
        /// <remarks>Этот метод асинхронно использует SQL-функцию AVG для получения среднего значения в столбце.</remarks>
        public async Task<T> AvgAsync<TFrom, T>(
            Expression<Func<TFrom, T>> columnSelector,
            Expression<Func<TFrom, bool>> whereExpression = null,
            CancellationToken token = default)
            where TFrom : class => ChangeType<T>((await this.AggAsync("AVG", whereExpression, token, columnSelector.ConvertExpression())
                .ConfigureAwait(this.ConfigureAwait)).Values.FirstOrDefault());

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
                return this.tr.Value;
            }

            this.BeginConnection();
            this.tr.Value = this.Connection.BeginTransaction(level);
            return this.tr.Value;
        }

        /// <summary>
        /// Завершается текущая транзакция и закрывает соединение с базой данных.
        /// </summary>
        /// <param name="closeConnection">Закрыть соединение после подтверждения транзакции.</param>
        /// <remarks>Этот метод коммитит текущую транзакцию и очищает ресурсы, связанные с ней.
        /// После завершения транзакции соединение с базой данных закрывается.</remarks>
        public void CommitTransaction(bool closeConnection = true)
        {
            if (this.tr.Value == null)
            {
                return;
            }

            this.tr.Value.Commit();
            this.tr.Value.Dispose();
            this.tr.Value = null;
            if (closeConnection)
            {
                this.CloseConnection();
            }
        }

        /// <summary>
        /// Завершается текущая транзакция и закрывает соединение с базой данных.
        /// </summary>
        /// <param name="dbTransaction">Транзакция.</param>
        /// <remarks>Этот метод коммитит текущую транзакцию и очищает ресурсы, связанные с ней.
        /// После завершения транзакции соединение с базой данных закрывается.</remarks>
        public void CommitTransaction(IDbTransaction dbTransaction)
        {
            if (dbTransaction == null)
            {
                return;
            }

            dbTransaction.Commit();
            dbTransaction.Dispose();
            dbTransaction = null;
        }

        /// <summary>
        /// Получает общее количество строк для выполненного SQL-запроса.
        /// </summary>
        /// <param name="cmd">Команда, для которой будет выполнен подсчет строк.</param>
        /// <returns>Общее количество строк в результате запроса.</returns>
        /// <remarks>Этот метод выполняет подсчет количества строк в запросе, оборачивая его в подзапрос с использованием SQL-функции
        /// COUNT.</remarks>
        public long Count(IDbCommand cmd)
        {
            cmd.CommandText = $"SELECT COUNT(*) FROM ({cmd.CommandText}) AS CountTable";
            return ChangeType<long>(this.ExecuteScalar(cmd));
        }

        /// <summary>
        /// Получает количество строк для данных в сущности с указанной колонкой.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>Общее количество строк в указанной колонке.</returns>
        /// <remarks>Этот метод выполняет агрегацию данных с использованием SQL-функции COUNT для конкретной колонки в сущности.</remarks>
        public long Count(string query)
        {
            query = $"SELECT COUNT(*) FROM ({query}) AS CountTable";
            return ChangeType<long>(this.ExecuteScalar(query));
        }

        /// <summary>
        /// Получает количество строк для данных в сущности с указанной колонкой.
        /// </summary>
        /// <typeparam name="TFrom">Тип сущности.</typeparam>
        /// <param name="columnSelector">Выражение для выбора колонки для подсчета.</param>
        /// <param name="whereExpression">The where expression.</param>
        /// <returns>Общее количество строк в указанной колонке, приведенное к типу.</returns>
        /// <remarks>Этот метод выполняет агрегацию данных с использованием SQL-функции COUNT для конкретной колонки в сущности и
        /// преобразует результат в тип.</remarks>
        public long Count<TFrom>(
            Expression<Func<TFrom, object>> columnSelector,
            Expression<Func<TFrom, bool>> whereExpression = null)
            where TFrom : class
        {
            var total = this.Agg("count", whereExpression, columnSelector.ConvertExpression()).Values.FirstOrDefault();
            return ChangeType<long>(total);
        }

        /// <summary>
        /// Counts the specified where expression.
        /// </summary>
        /// <typeparam name="TFrom">The type of the t from.</typeparam>
        /// <param name="whereExpression">The where expression.</param>
        /// <returns>System.Object.</returns>
        public long Count<TFrom>(Expression<Func<TFrom, bool>> whereExpression = null)
            where TFrom : class
        {
            var total = this.Agg("count", whereExpression).Values.FirstOrDefault();
            return ChangeType<long>(total);
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
        /// <typeparam name="TFrom">Тип сущности.</typeparam>
        /// <param name="whereExpression">The where expression.</param>
        /// <param name="token">Токен отмены операции.</param>
        /// <returns>Задача, которая возвращает общее количество строк в указанной колонке.</returns>
        /// <remarks>Этот метод выполняет асинхронный подсчет строк для конкретной колонки с использованием SQL-функции COUNT.</remarks>
        public async Task<long> CountAsync<TFrom>(
            Expression<Func<TFrom, bool>> whereExpression = null,
            CancellationToken token = default)
            where TFrom : class => ChangeType<long>((await this.AggAsync("count", whereExpression, token, null)
                .ConfigureAwait(this.ConfigureAwait)).Values.FirstOrDefault());

        /// <summary>
        /// Асинхронно получает количество строк для данных в сущности с указанной колонкой.
        /// </summary>
        /// <typeparam name="TFrom">Тип сущности.</typeparam>
        /// <param name="columnSelector">Выражение для выбора колонки для подсчета.</param>
        /// <param name="whereExpression">The where expression.</param>
        /// <param name="token">Токен отмены операции.</param>
        /// <returns>Задача, которая возвращает общее количество строк в указанной колонке.</returns>
        /// <remarks>Этот метод выполняет асинхронный подсчет строк для конкретной колонки с использованием SQL-функции COUNT.</remarks>
        public async Task<long> CountAsync<TFrom>(
            Expression<Func<TFrom, object>> columnSelector,
            Expression<Func<TFrom, bool>> whereExpression = null,
            CancellationToken token = default)
            where TFrom : class => ChangeType<long>((await this.AggAsync("count", whereExpression, token, columnSelector)
                .ConfigureAwait(this.ConfigureAwait)).Values.FirstOrDefault());

        /// <summary>
        /// Создаёт и настраивает команду для выполнения SQL-запроса.
        /// </summary>
        /// <param name="query">SQL-запрос, который будет выполнен.</param>
        /// <param name="cmdParams">Параметры запроса. Может быть <c>null</c>, если параметры не требуются.</param>
        /// <param name="dbTransaction">Транзакция, в рамках которой выполняется запрос. Может быть <c>null</c>.</param>
        /// <param name="commandTimeout">Тайм-аут выполнения команды в секундах. Если не задано, используется значение по
        /// умолчанию.</param>
        /// <param name="commandType">Тип команды.</param>
        /// <returns>Объект <see cref="DbCommand" />, готовый к выполнению запроса.</returns>
        /// <remarks>Этот метод создаёт команду для выполнения SQL-запроса, назначает ей параметры и устанавливает тайм-аут выполнения.
        /// Если параметры не указаны, команда будет выполнена без них.</remarks>
        public DbCommand CreateCommand(
            string query,
            object cmdParams,
            IDbTransaction dbTransaction = null,
            int? commandTimeout = null,
            CommandType commandType = CommandType.Text)
        {
            var cmd = DbConnectionExtensions.CreateCommand(this.Connection, query, cmdParams, dbTransaction ?? this.tr?.Value, commandTimeout ?? this.CommandTimeout ?? 30, commandType, this.Options.ParamPrefix);
            return (DbCommand)cmd;
        }

        /// <summary>
        /// Удаляет записи из базы данных, соответствующие заданному условию.
        /// </summary>
        /// <typeparam name="T">Тип сущности, из таблицы которой выполняется удаление.</typeparam>
        /// <param name="whereExpression">Лямбда-выражение, задающее условие отбора записей для удаления.</param>
        /// <param name="dbTransaction">Транзакция.</param>
        /// <returns>Количество удалённых строк.</returns>
        public int Delete<T>(Expression<Func<T, bool>> whereExpression, IDbTransaction dbTransaction = null)
            where T : class
        {
            var query = (SqlQueryHelper.GetDeleteQuery<T>(this.Options) + " " + SqlQueryHelper.GetWhereClause(
                    this.Options,
                    whereExpression,
                    true,
                    out var cmdParam))
                .Trim();
            return this.ExecuteNonQuery(query, cmdParam, dbTransaction);
        }

        /// <summary>
        /// Удаляет запись из базы данных на основании значений ключевых полей объекта.
        /// </summary>
        /// <typeparam name="T">Тип сущности, из таблицы которой выполняется удаление.</typeparam>
        /// <param name="item">Объект, содержащий значения ключевых полей, используемых в условии удаления.</param>
        /// <param name="dbTransaction">Транзакция.</param>
        /// <returns>Количество удалённых строк.</returns>
        public int Delete<T>(T item, IDbTransaction dbTransaction = null)
            where T : class
        {
            var query = (SqlQueryHelper.GetDeleteQuery<T>(this.Options) + " " +
                         SqlQueryHelper.GetWhereClause<T>(this.Options, out _)).Trim();
            return this.ExecuteNonQuery(query, Obj.GetValues(item), dbTransaction);
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
            var query = (SqlQueryHelper.GetDeleteQuery<T>(this.Options) + " " +
                         SqlQueryHelper.GetWhereClause<T>(this.Options, out _)).Trim();
            return this.ExecuteNonQueryAsync(query, Obj.GetValues(item), dbTransaction, token);
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
            var query = (SqlQueryHelper.GetDeleteQuery<T>(this.Options) + " " + SqlQueryHelper.GetWhereClause(
                    this.Options,
                    whereExpression,
                    true,
                    out var cmdParams))
                .Trim();
            return this.ExecuteNonQueryAsync(query, cmdParams, dbTransaction, token);
        }

        /// <summary>
        /// Синхронно удаляет несколько записей из базы данных в рамках одной транзакции.
        /// </summary>
        /// <typeparam name="T">Тип сущности, из таблицы которой выполняется удаление.</typeparam>
        /// <param name="list">Коллекция объектов, содержащих значения ключевых полей удаляемых записей.</param>
        /// <param name="dbTransaction">Транзакция, в рамках которой выполняется удаление. Если <c>null</c>, создаётся новая транзакция.</param>
        /// <param name="token">Токен отмены асинхронной операции.</param>
        /// <returns>Задача, результатом которой является общее количество удалённых строк.</returns>
        /// <remarks>Все операции удаления выполняются в одной транзакции.
        /// В случае возникновения ошибки транзакция откатывается.</remarks>
        public int DeleteRange<T>(
            IEnumerable<T> list,
            IDbTransaction dbTransaction = null,
            CancellationToken token = default)
            where T : class
        {
            try
            {
                var count = 0;
                var autoCommit = dbTransaction == null && this.tr.Value == null;
                dbTransaction ??= this.BeginTransaction();
                {
                    foreach (var item in list)
                    {
                        count += this.Delete(item, dbTransaction);
                    }

                    if (autoCommit)
                    {
                        this.CommitTransaction();
                    }
                }

                return count;
            }
            catch (Exception ex)
            {
                this.RollbackTransaction();
                throw this.HandleDbException(ex, null);
            }
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
            IDbTransaction dbTransaction = null,
            CancellationToken token = default)
            where T : class
        {
            try
            {
                var count = 0;
                var autoCommit = dbTransaction == null && this.tr.Value == null;
                dbTransaction ??= this.BeginTransaction();
                {
                    foreach (var item in list)
                    {
                        count += await this.DeleteAsync(item, dbTransaction, token).ConfigureAwait(this.ConfigureAwait);
                    }

                    if (autoCommit)
                    {
                        this.CommitTransaction();
                    }
                }

                return count;
            }
            catch (Exception ex)
            {
                this.RollbackTransaction();
                throw this.HandleDbException(ex, null);
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
            var attempt = 0;
            while (true)
            {
                dbTransaction ??= this.tr?.Value;
                using (var cmd = this.CreateCommand(query, queryParams, dbTransaction))
                {
                    try
                    {
                        BeginConnection(this.Connection);

                        var i = cmd.ExecuteNonQuery();
                        this.CommandExecuted?.Invoke(cmd);
                        this.Log(cmd);
                        if (cmd.Transaction == null)
                        {
                            this.CloseConnection(this.Connection);
                        }

                        return i;
                    }
                    catch (Exception ex) when (IsTimeoutException(ex) && attempt < DbClient.RetryCount && dbTransaction == null)
                    {
                        attempt++;
                        this.HandleDbException(ex, cmd);
                        this.CloseConnection();
                    }
                    catch (Exception ex)
                    {
                        throw this.HandleDbException(ex, cmd);
                    }
                    finally
                    {
                        if (dbTransaction == null)
                        {
                            this.CloseConnection();
                        }
                    }
                }
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
            var attempt = 0;
            dbTransaction ??= this.tr?.Value;
            while (true)
            {
                token.ThrowIfCancellationRequested();
                using (var cmd = this.CreateCommand(query, cmdParams, dbTransaction))
                {
                    try
                    {
                        await this.BeginConnectionAsync(token).ConfigureAwait(this.ConfigureAwait);
                        var i = await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(this.ConfigureAwait);
                        this.CommandExecuted?.Invoke(cmd);
                        this.Log(cmd);
                        return i;
                    }
                    catch (Exception ex) when (IsTimeoutException(ex) && attempt < DbClient.RetryCount && dbTransaction == null)
                    {
                        attempt++;
                        this.HandleDbException(ex, cmd);
                        var delay = TimeSpan.FromMilliseconds(200 * attempt);
                        await Task.Delay(delay, token).ConfigureAwait(this.ConfigureAwait);
                        this.CloseConnection();
                    }
                    catch (OperationCanceledException)
                    {
                        // НЕ ретраим отмену
                        throw;
                    }
                    catch (Exception ex)
                    {
                        throw this.HandleDbException(ex, cmd);
                    }
                    finally
                    {
                        if (dbTransaction == null)
                        {
                            this.CloseConnection();
                        }
                    }
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
            var query = (SqlQueryHelper.GetSelectQuery(this.Options, this.UseFullNamesInQueries, propertySelector) + " " +
                         SqlQueryHelper.GetWhereClause(
                             this.Options,
                             whereExpression,
                             true,
                             out var cmdParam)).Trim();
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
            dbTransaction ??= this.tr?.Value;
            var cmd = this.CreateCommand(query, cmdParams, dbTransaction);
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
                    var v = cmd.ExecuteScalar();
                    this.CommandExecuted?.Invoke(cmd);
                    this.Log(cmd);
                    return ChangeType<T>(v);
                }
                catch (Exception ex)
                {
                    throw this.HandleDbException(ex, cmd);
                }
                finally
                {
                    if (cmd.Transaction == null)
                    {
                        this.CloseConnection(this.Connection);
                    }
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
        public Task<object> ExecuteScalarAsync(IDbCommand cmd, CancellationToken token = default) => this.ExecuteScalarAsync<object>((DbCommand)cmd, token);

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
            var query = (SqlQueryHelper.GetSelectQuery(this.Options, this.UseFullNamesInQueries, propertySelector) + " " +
                         SqlQueryHelper.GetWhereClause(
                             this.Options,
                             whereExpression,
                             true,
                             out var cmdParam)).Trim();
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
            dbTransaction ??= this.tr?.Value;
            var cmd = this.CreateCommand(query, cmdParams, dbTransaction);
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
                    var v = await cmd.ExecuteScalarAsync(token).ConfigureAwait(this.ConfigureAwait);
                    this.CommandExecuted?.Invoke(cmd);
                    this.Log(cmd);
                    return ChangeType<T>(v);
                }
                catch (Exception ex)
                {
                    throw this.HandleDbException(ex, cmd);
                }
                finally
                {
                    if (cmd.Transaction == null)
                    {
                        this.CloseConnection(this.Connection);
                    }
                }
            }
        }

        /// <summary>
        /// Выполняет выборку записи по ключу и заполняет переданный экземпляр сущности
        /// полученными данными.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="item">
        /// Экземпляр сущности, который будет заполнен значениями из базы данных.
        /// </param>
        /// <param name="id">
        /// Значения ключа (первичного или составного), используемые в условии WHERE.
        /// </param>
        /// <remarks>
        /// Формирует SELECT-запрос на основе конфигурации сопоставления сущности
        /// и добавляет условие WHERE по ключевым полям.
        /// Полученные данные проецируются в переданный экземпляр <paramref name="item"/>.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// Может быть выброшено, если <paramref name="item"/> равен <c>null</c>.
        /// </exception>
        public void Fill<T>(T item, params object[] id)
        {
            var query = SqlQueryHelper.GetSelectQuery<T>(this.Options, this.UseFullNamesInQueries) + " " +
                        SqlQueryHelper.GetWhereClause<T>(this.Options, out _);

            var pCmdParams = GetKeyParams(item, id);

            this.Query<List<T>, T>(
                query,
                pCmdParams,
                itemFactory: (objects, strings) => item);
        }

        /// <summary>
        /// Асинхронно выполняет выборку записи по ключу и заполняет переданный
        /// экземпляр сущности полученными данными.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="item">
        /// Экземпляр сущности, который будет заполнен значениями из базы данных.
        /// </param>
        /// <param name="id">
        /// Значения ключа (первичного или составного), используемые в условии WHERE.
        /// </param>
        /// <returns>
        /// Задача, представляющая асинхронную операцию заполнения сущности.
        /// </returns>
        /// <remarks>
        /// Формирует SELECT-запрос и WHERE-условие аналогично методу <see cref="Fill{T}(T, object[])"/>,
        /// но выполняет запрос асинхронно.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// Может быть выброшено, если <paramref name="item"/> равен <c>null</c>.
        /// </exception>
        public Task FillAsync<T>(T item, params object[] id)
        {
            var query = SqlQueryHelper.GetSelectQuery<T>(this.Options, this.UseFullNamesInQueries) + " " +
                        SqlQueryHelper.GetWhereClause<T>(this.Options, out _);

            var pCmdParams = GetKeyParams(item, id);

            return this.QueryAsync<List<T>, T>(
                query,
                pCmdParams,
                itemFactory: (objects, strings) => item);
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
            IEnumerable<(string ColumnName, string PropertyName)> columnToPropertyMap = null,
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
            IEnumerable<(string ColumnName, string PropertyName)> columnToPropertyMap = null,
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
            IEnumerable<(string ColumnName, string PropertyName)> columnToPropertyMap = null,
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
            IEnumerable<(string ColumnName, string PropertyName)> columnToPropertyMap = null,
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
            var colNames = columnSelector.Select(x => x.GetMemberCache().ColumnName).ToArray();
            var queryExpression =
                new List<(Expression<Func<TFrom, object>>, string)>();
            foreach (var cs in columnSelector)
            {
                queryExpression.Add((cs, "COUNT"));
                queryExpression.Add((cs, "MIN"));
                queryExpression.Add((cs, "MAX"));
                queryExpression.Add((cs, "SUM"));
                queryExpression.Add((cs, "AVG"));
            }

            var result = this.Agg(whereExpression, [.. queryExpression]);

            var dic = colNames.Select((x, i) => (x,
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
            var colNames = columnSelector.Select(x => x.GetMemberCache().ColumnName).ToArray();
            var queryExpression =
                new List<(Expression<Func<TFrom, object>>, string)>();
            foreach (var cs in columnSelector)
            {
                queryExpression.Add((cs, "COUNT"));
                queryExpression.Add((cs, "MIN"));
                queryExpression.Add((cs, "MAX"));
                queryExpression.Add((cs, "SUM"));
                queryExpression.Add((cs, "AVG"));
            }

            var result = await this.AggAsync(whereExpression, token, [.. queryExpression])
                .ConfigureAwait(this.ConfigureAwait);

            var dic = colNames.Select((x, i) => (x,
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
        public Dictionary<int, (int Offset, int Count)> GetPages<TFrom>(int pageSize)
            where TFrom : class
        {
            if (pageSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pageSize));
            }

            var total = this.Count<TFrom>();
            var pagesCount = (int)Math.Ceiling(total / (double)pageSize);

            var pages = new Dictionary<int, (int Offset, int Count)>(pagesCount);

            for (var page = 1; page <= pagesCount; page++)
            {
                var offset = (page - 1) * pageSize;
                var count = Math.Min(pageSize, total - offset);

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
        public async Task<Dictionary<int, (int Offset, int Count)>> GetPagesAsync<TFrom>(
            int pageSize,
            CancellationToken token = default)
            where TFrom : class
        {
            if (pageSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pageSize));
            }

            var total = await this.CountAsync<TFrom>(token: token).ConfigureAwait(this.ConfigureAwait);
            var pagesCount = (int)Math.Ceiling(total / (double)pageSize);

            var pages = new Dictionary<int, (int Offset, int Count)>(pagesCount);

            for (var page = 1; page <= pagesCount; page++)
            {
                var offset = (page - 1) * pageSize;
                var count = Math.Min(pageSize, total - offset);

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
            var numbers = this.Agg(whereExpression, (null, "count"));
            var rowsCount = Convert.ToInt32(numbers.Values.FirstOrDefault());
            var pagesCount = (int)Math.Ceiling((double)rowsCount / pageSize);
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
            var numbers = await this.AggAsync(whereExpression, token, (null, "count"))
                .ConfigureAwait(this.ConfigureAwait);
            var rowsCount = Convert.ToInt32(numbers.Values.FirstOrDefault());
            var pagesCount = (int)Math.Ceiling((double)rowsCount / pageSize);
            return pagesCount;
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

            var sql = command.CommandText;

            foreach (IDbDataParameter parameter in command.Parameters)
            {
                var paramToken = this.Options.ParamPrefix + parameter.ParameterName;
                var literal = this.Options.ValueFormatter.Format(parameter.Value);

                sql = ReplaceParameterToken(sql, paramToken, literal);
            }

            return sql;
        }

        /// <summary>
        /// Выполняет вставку строки в указанную таблицу без явной транзакции. Транзакцию можно начать через <see cref="BeginTransaction(IsolationLevel)"/>.
        /// </summary>
        /// <param name="tableName">Имя таблицы, в которую выполняется вставка.</param>
        /// <param name="values">Значения, которые будут вставлены в строку таблицы.</param>
        public void Insert(string tableName, params object[] values)
        {
            this.Insert(tableName, values, (IDbTransaction)null);
        }

        /// <summary>
        /// Выполняет вставку строки в указанную таблицу с возможностью использования транзакции.
        /// </summary>
        /// <param name="tableName">Имя таблицы, в которую выполняется вставка.</param>
        /// <param name="values">Значения, которые будут вставлены в строку таблицы. Порядок значений должен соответствовать порядку столбцов таблицы.</param>
        /// <param name="dbTransaction">Транзакция базы данных. Если <c>null</c>, вставка выполняется без транзакции.</param>
        public void Insert(string tableName, object[] values, IDbTransaction dbTransaction = null)
        {
            var sql = $"INSERT INTO {tableName} VALUES ({string.Join(", ", values.Select((x, i) => this.Options.ParamPrefix + i))})";
            this.ExecuteNonQuery(sql, values, dbTransaction);
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
            var item = Obj.New<T>();
            foreach (var a in columnSetters)
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
            var query = SqlQueryHelper.GetInsertQuery(item.GetType(), this.Options, insertColumns);
            if (string.IsNullOrWhiteSpace(this.Options.GetInsertedIdQuery))
            {
                this.ExecuteNonQuery(query, Obj.GetValues(item), dbTransaction);
            }
            else
            {
                query += $"{this.Options.StatementTerminator} {this.Options.GetInsertedIdQuery}";
                id = this.ExecuteScalar<object>(query, Obj.GetValues(item));
                var mi = MemberCache.Get(item?.GetType() ?? typeof(T));
                if (id != null && id != DBNull.Value && mi.PrimaryKeys.Length == 1)
                {
                    var pi = mi.PrimaryKeys[0];
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
            var item = Obj.New<T>();
            if (insertColumns == null)
            {
                return this.InsertAsync(item, null, dbTransaction, token);
            }

            foreach (var a in insertColumns)
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
            var query = SqlQueryHelper.GetInsertQuery(item.GetType(), this.Options, insertColumns);
            if (string.IsNullOrWhiteSpace(this.Options.GetInsertedIdQuery))
            {
                await this.ExecuteNonQueryAsync(query, Obj.GetValues(item), dbTransaction, token)
                    .ConfigureAwait(this.ConfigureAwait);
            }
            else
            {
                query += $"{this.Options.StatementTerminator} {this.Options.GetInsertedIdQuery}";
                id = await this.ExecuteScalarAsync<object>(query, Obj.GetValues(item), dbTransaction, token)
                    .ConfigureAwait(this.ConfigureAwait);
                var mi = MemberCache.Get(item?.GetType() ?? typeof(T));
                if (id != null && id != DBNull.Value && mi.PrimaryKeys.Length == 1)
                {
                    mi.PrimaryKeys[0].SetValue(
                        item,
                        ChangeType(id, mi.PrimaryKeys[0].PropertyType));
                }
            }

            return id;
        }

        /// <summary>
        /// Вставляет коллекцию объектов в базу данных в рамках одной транзакции.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="list">Коллекция объектов для вставки.</param>
        /// <param name="tableName">Имя таблицы в которую вставлять строки.</param>
        /// <param name="dbTransaction">Внешняя транзакция. Если не указана — создаётся новая.</param>
        /// <param name="insertColumns">Колонки, участвующие во вставке.</param>
        /// <returns>ID вставленных записей.</returns>
        public object[] InsertRange<T>(
            IEnumerable<T> list,
            string tableName,
            IDbTransaction dbTransaction = null,
            params Expression<Func<T, object>>[] insertColumns)
            where T : class
        {
            this.Options.Map.Table<T>(tableName);
            return this.InsertRange(list, dbTransaction, insertColumns);
        }

        /// <summary>
        /// Вставляет коллекцию объектов в базу данных в рамках одной транзакции.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="list">Коллекция объектов для вставки.</param>
        /// <param name="dbTransaction">Внешняя транзакция. Если не указана — создаётся новая.</param>
        /// <param name="insertColumns">Колонки, участвующие во вставке.</param>
        /// <returns>ID вставленных записей.</returns>
        public object[] InsertRange<T>(
            IEnumerable<T> list,
            IDbTransaction dbTransaction = null,
            params Expression<Func<T, object>>[] insertColumns)
            where T : class
        {
            var attempt = 0;
            IDbCommand cmd = null;
            while (true)
            {
                var autoCommit = dbTransaction == null && this.tr.Value == null;

                try
                {
                    var ids = new List<object>();
                    using (dbTransaction ?? this.BeginTransaction())
                    {
                        var query = SqlQueryHelper.GetInsertQuery(list.FirstOrDefault()?.GetType() ?? typeof(T), this.Options, insertColumns);
                        if (!string.IsNullOrWhiteSpace(this.Options.GetInsertedIdQuery))
                        {
                            query += $"{this.Options.StatementTerminator} {this.Options.GetInsertedIdQuery}";
                        }

                        var typeCache = MemberCache.Get(list.FirstOrDefault()?.GetType() ?? typeof(T));
                        var pk = typeCache.PrimaryKeys.FirstOrDefault();
                        var queryParams = new Dictionary<string, object>();
                        using (cmd = this.CreateCommand(query, dbTransaction))
                        {
                            if (cmd is not DbCommand dbCmd)
                            {
                                throw new InvalidCastException($"Cannot cast argument '{nameof(cmd)}' to type '{typeof(DbCommand).FullName}'.");
                            }

                            foreach (var item in list)
                            {
                                typeCache.ToDictionary(item, queryParams);
                                SetParameterCollection(cmd, queryParams);
                                var id = cmd.ExecuteScalar();
                                ids.Add(id);
                                this.CommandExecuted?.Invoke(cmd);
                                this.Log(cmd);
                                if (pk != null && id != null)
                                {
                                    pk.SetValue(item, ChangeType(id, pk.PropertyType));
                                }
                            }
                        }

                        if (autoCommit)
                        {
                            this.CommitTransaction();
                        }

                        return [.. ids];
                    }
                }
                catch (Exception ex) when (IsTimeoutException(ex) && attempt < DbClient.RetryCount && dbTransaction == null)
                {
                    attempt++;
                    this.HandleDbException(ex, cmd);
                    this.CloseConnection();
                }
                catch (Exception ex)
                {
                    this.RollbackTransaction();
                    throw this.HandleDbException(ex, null);
                }
            }
        }

        /// <summary>
        /// Асинхронно вставляет коллекцию объектов в базу данных
        /// в рамках одной транзакции.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="list">Коллекция объектов.</param>
        /// <param name="tableName">Имя таблицы в которую вставлять строки.</param>
        /// <param name="insertColumns">Колонки, участвующие во вставке.</param>
        /// <param name="dbTransaction">Внешняя транзакция.</param>
        /// <param name="token">Токен отмены.</param>
        /// <returns>Количество вставленных записей.</returns>
        /// <exception cref="System.NullReferenceException">dbCmd.</exception>
        public Task<object[]> InsertRangeAsync<T>(
            IEnumerable<T> list,
            string tableName,
            Expression<Func<T, object>>[] insertColumns = null,
            IDbTransaction dbTransaction = null,
            CancellationToken token = default)
            where T : class
        {
            this.Options.Map.Table<T>(tableName);
            return this.InsertRangeAsync(list, insertColumns, dbTransaction, token);
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
        public async Task<object[]> InsertRangeAsync<T>(
            IEnumerable<T> list,
            Expression<Func<T, object>>[] insertColumns = null,
            IDbTransaction dbTransaction = null,
            CancellationToken token = default)
            where T : class
        {
            var attempt = 0;
            IDbCommand cmd = null;
            while (true)
            {
                try
                {
                    var ids = new List<object>();
                    var autoCommit = dbTransaction == null && this.tr.Value == null;
                    dbTransaction ??= this.BeginTransaction();
                    {
                        var query = SqlQueryHelper.GetInsertQuery(list.FirstOrDefault()?.GetType() ?? typeof(T), this.Options, insertColumns);
                        if (!string.IsNullOrWhiteSpace(this.Options.GetInsertedIdQuery))
                        {
                            query += $"{this.Options.StatementTerminator} {this.Options.GetInsertedIdQuery}";
                        }

                        var typeCache = MemberCache.Get(list.FirstOrDefault()?.GetType() ?? typeof(T));
                        var pk = typeCache.PrimaryKeys.FirstOrDefault();
                        var queryParams = new Dictionary<string, object>();
                        using (cmd = this.CreateCommand(query, null, dbTransaction))
                        {
                            if (cmd is not DbCommand dbCmd)
                            {
                                throw new InvalidCastException($"Cannot cast argument '{nameof(cmd)}' to type '{typeof(DbCommand).FullName}'.");
                            }

                            foreach (var item in list)
                            {
                                typeCache.ToDictionary(item, queryParams);
                                SetParameterCollection(cmd, queryParams);

                                var id = await dbCmd.ExecuteScalarAsync(token).ConfigureAwait(this.ConfigureAwait);
                                ids.Add(id);
                                this.CommandExecuted?.Invoke(cmd);
                                this.Log(cmd);
                                if (pk != null && id != null)
                                {
                                    pk.SetValue(item, ChangeType(id, pk.PropertyType));
                                }
                            }
                        }

                        if (autoCommit)
                        {
                            this.CommitTransaction();
                        }
                    }

                    return [.. ids];
                }
                catch (Exception ex) when (IsTimeoutException(ex) && attempt < DbClient.RetryCount && dbTransaction == null)
                {
                    attempt++;
                    this.HandleDbException(ex, cmd);
                    this.CloseConnection();
                }
                catch (Exception ex)
                {
                    this.RollbackTransaction();
                    throw this.HandleDbException(ex, null);
                }
            }
        }

        /// <summary>
        /// Получает максимальное значение для указанного столбца.
        /// </summary>
        /// <typeparam name="TFrom">Тип сущности.</typeparam>
        /// <typeparam name="T">Тип данных.</typeparam>
        /// <param name="columnSelector">Выражение для выбора столбца, для которого будет вычислено максимальное значение.</param>
        /// <param name="whereExpression">The where expression.</param>
        /// <returns>Максимальное значение для указанного столбца.</returns>
        /// <remarks>Этот метод использует SQL-функцию MAX для получения максимального значения в столбце.</remarks>
        public T Max<TFrom, T>(
            Expression<Func<TFrom, T>> columnSelector,
            Expression<Func<TFrom, bool>> whereExpression = null)
            where TFrom : class => ChangeType<T>(this.Agg("MAX", whereExpression, columnSelector.ConvertExpression()).Values.FirstOrDefault());

        /// <summary>
        /// Асинхронно получает максимальное значение для указанного столбца.
        /// </summary>
        /// <typeparam name="TFrom">Тип сущности.</typeparam>
        /// <typeparam name="T">Тип данных.</typeparam>
        /// <param name="columnSelector">Выражение для выбора столбца, для которого будет вычислено максимальное значение.</param>
        /// <param name="whereExpression">The where expression.</param>
        /// <param name="token">Токен отмены асинхронной операции.</param>
        /// <returns>Задача, которая возвращает максимальное значение для указанного столбца.</returns>
        /// <remarks>Этот метод асинхронно использует SQL-функцию MAX для получения максимального значения в столбце.</remarks>
        public async Task<T> MaxAsync<TFrom, T>(
            Expression<Func<TFrom, T>> columnSelector,
            Expression<Func<TFrom, bool>> whereExpression = null,
            CancellationToken token = default)
            where TFrom : class
                => ChangeType<T>((await this.AggAsync("MAX", whereExpression, token, columnSelector.ConvertExpression()).ConfigureAwait(this.ConfigureAwait)).Values.FirstOrDefault());

        /// <summary>
        /// Получает минимальное значение для указанного столбца.
        /// </summary>
        /// <typeparam name="TFrom">Тип сущности.</typeparam>
        /// <typeparam name="T">Тип данных.</typeparam>
        /// <param name="columnSelector">Выражение для выбора столбца, для которого будет вычислено минимальное значение.</param>
        /// <param name="whereExpression">The where expression.</param>
        /// <returns>Минимальное значение для указанного столбца.</returns>
        /// <remarks>Этот метод использует SQL-функцию MIN для получения минимального значения в столбце.</remarks>
        public T Min<TFrom, T>(
            Expression<Func<TFrom, T>> columnSelector,
            Expression<Func<TFrom, bool>> whereExpression = null)
            where TFrom : class => ChangeType<T>(this.Agg("MIN", whereExpression, columnSelector.ConvertExpression()).Values.FirstOrDefault());

        /// <summary>
        /// Асинхронно получает минимальное значение для указанного столбца.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых вычисляется минимальное значение.</typeparam>
        /// <typeparam name="T">Тип данных.</typeparam>
        /// <param name="columnSelector">Выражение для выбора столбца, для которого будет вычислено минимальное значение.</param>
        /// <param name="whereExpression">The where expression.</param>
        /// <param name="token">Токен отмены асинхронной операции.</param>
        /// <returns>Задача, которая возвращает минимальное значение для указанного столбца.</returns>
        /// <remarks>Этот метод асинхронно использует SQL-функцию MIN для получения минимального значения в столбце.</remarks>
        public async Task<T> MinAsync<TFrom, T>(
            Expression<Func<TFrom, T>> columnSelector,
            Expression<Func<TFrom, bool>> whereExpression = null,
            CancellationToken token = default)
            where TFrom : class => ChangeType<T>((await this.AggAsync("MIN", whereExpression, token, columnSelector.ConvertExpression())
                .ConfigureAwait(this.ConfigureAwait)).Values.FirstOrDefault());

        /// <summary>
        /// Открывает соединение с базой данных.
        /// </summary>
        /// <returns>True, если соединение удалось открыть, false - иначе.</returns>
        public bool OpenConnection()
        {
            return this.Connection.TryOpen();
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
        /// <param name="dbTransaction">Транзакция.</param>
        /// <returns>Коллекция объектов типа <typeparamref name="T" />, которая содержит результат выполнения запроса.</returns>
        /// <remarks>Этот метод выполняет SQL-запрос синхронно и возвращает результат в виде коллекции объектов.</remarks>
        public TList Query<TList, T>(
            string query = null,
            object cmdParams = null,
            IEnumerable<string> columns = null,
            IEnumerable<(string ColumnName, string PropertyName)> columnToPropertyMap = null,
            DbValueConverter<T> converter = null,
            int fetchRows = -1,
            int offsetRows = 0,
            Func<object[], string[], T> itemFactory = null,
            IDbTransaction dbTransaction = null)
            where TList : ICollection<T>, IList, new()
        {
            if (string.IsNullOrEmpty(query))
            {
                query = SqlQueryHelper.GetSelectQuery<T>(this.Options, this.UseFullNamesInQueries);
            }

            query = SqlQueryHelper.AddLimitOffsetClauseToQuery(this.Options, fetchRows, offsetRows, query, typeof(T));
            dbTransaction ??= this.tr?.Value;
            var cache = MemberCache.Get(typeof(T));
            itemFactory ??= BuildItemFactory<T>(cache, columnToPropertyMap);

            var attempt = 0;
            while (true)
            {
                this.BeginConnection();
                using (var cmd = this.CreateCommand(query, cmdParams, dbTransaction, DefaultCommandTimeout + (RetryTimeoutStep * attempt)))
                {
                    try
                    {
                        var reader = cmd.ExecuteReader();
                        try
                        {
                            this.CommandExecuted?.Invoke(cmd);
                            this.Log(cmd);
                            var list = new TList();

                            if (list is ObservableCollectionEx<T> oce1)
                            {
                                oce1.SuspendNotifications(true);
                            }

                            this.ReadToListInternalAsync2(
                                list,
                                reader,
                                columns,
                                columnToPropertyMap,
                                converter,
                                fetchRows,
                                itemFactory,
                                CancellationToken.None).GetAwaiter().GetResult();

                            if (list is ObservableCollectionEx<T> oce2)
                            {
                                oce2.SuspendNotifications(false);
                            }

                            return list;
                        }
                        finally
                        {
                            reader.Dispose();
                        }
                    }
                    catch (Exception ex) when (IsTimeoutException(ex) && attempt < DbClient.RetryCount && dbTransaction == null)
                    {
                        attempt++;
                        this.HandleDbException(ex, cmd);
                        this.CloseConnection();
                    }
                    catch (OperationCanceledException)
                    {
                        // НЕ ретраим отмену
                        throw;
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
        }

        /// <summary>
        /// Выполняет SQL-запрос и возвращает результат в виде коллекции объектов указанного типа.
        /// </summary>
        /// <param name="returnType">
        /// Тип результата запроса. Обычно это тип коллекции
        /// (например, <see cref="List{T}"/> или массив), элементы которой соответствуют строкам результата.
        /// </param>
        /// <param name="query">
        /// SQL-запрос для выполнения.
        /// Если не указан или равен <c>null</c>, запрос будет сгенерирован автоматически
        /// на основе типа элементов результата.
        /// </param>
        /// <param name="cmdParams">
        /// Параметры команды базы данных.
        /// Может быть анонимным объектом или словарём параметров.
        /// </param>
        /// <param name="columns">
        /// Список имён колонок, которые требуется считать из результата запроса.
        /// Если <c>null</c>, используются все колонки.
        /// </param>
        /// <param name="columnToPropertyMap">
        /// Сопоставление имён колонок базы данных с именами свойств объекта результата
        /// в виде пар <c>(ColumnName, PropertyName)</c>.
        /// </param>
        /// <param name="converter">
        /// Пользовательский конвертер значений из базы данных в значения свойств объекта.
        /// </param>
        /// <param name="fetchRows">
        /// Максимальное количество строк для выборки.
        /// Значение меньше нуля означает отсутствие ограничения.
        /// </param>
        /// <param name="offsetRows">
        /// Количество строк, которые необходимо пропустить перед началом выборки.
        /// </param>
        /// <param name="itemFactory">
        /// Фабрика создания экземпляров элементов результата.
        /// Получает массив значений колонок и массив их имён.
        /// Если не указана, используется фабрика по умолчанию,
        /// построенная на основе отражения.
        /// </param>
        /// <param name="dbTransaction">
        /// Транзакция базы данных, в контексте которой должен быть выполнен запрос.
        /// </param>
        /// <returns>
        /// Коллекция объектов типа, заданного параметром <paramref name="returnType"/>,
        /// заполненная результатами выполнения запроса.
        /// </returns>
        /// <exception cref="Exception">
        /// Любое исключение, возникшее при выполнении запроса,
        /// будет обработано и преобразовано методом <c>HandleDbException</c>.
        /// </exception>
        /// <remarks>
        /// <para>
        /// Метод выполняет следующие шаги:
        /// </para>
        /// <list type="bullet">
        /// <item>
        /// <description>
        /// Создаёт или генерирует SQL-запрос (если он не был передан явно).
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// Добавляет в запрос ограничения <c>LIMIT</c>/<c>OFFSET</c> с учётом настроек провайдера.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// Создаёт и выполняет команду базы данных.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// Считывает данные из <see cref="IDataReader"/> и наполняет результирующую коллекцию.
        /// </description>
        /// </item>
        /// </list>
        /// <para>
        /// Несмотря на использование асинхронной логики чтения,
        /// метод является синхронным и блокирует текущий поток до завершения операции.
        /// </para>
        /// </remarks>
        public object Query(
            Type returnType,
            string query = null,
            object cmdParams = null,
            IEnumerable<string> columns = null,
            IEnumerable<(string ColumnName, string PropertyName)> columnToPropertyMap = null,
            DbValueConverter<object> converter = null,
            int fetchRows = -1,
            int offsetRows = 0,
            Func<object[], string[], object> itemFactory = null,
            IDbTransaction dbTransaction = null)
        {
            var returnTypeCache = MemberCache.Get(returnType);
            if (string.IsNullOrEmpty(query))
            {
                query = SqlQueryHelper.GetSelectQuery(this.Options, this.UseFullNamesInQueries, returnTypeCache.ElementType);
            }

            query = SqlQueryHelper.AddLimitOffsetClauseToQuery(
                this.Options,
                fetchRows,
                offsetRows,
                query,
                returnTypeCache.ElementType);
            itemFactory ??= BuildItemFactory<object>(returnTypeCache.ElementType, columnToPropertyMap);
            dbTransaction ??= this.tr?.Value;
            var attempt = 0;
            while (true)
            {
                using (var cmd = this.CreateCommand(query, cmdParams, dbTransaction))
                {
                    this.BeginConnection();
                    try
                    {
                        var reader = cmd.ExecuteReader();
                        try
                        {
                            this.CommandExecuted?.Invoke(cmd);
                            this.Log(cmd);
                            var list = Obj.New(returnType) as IList;
                            Obj.Set(list, "SuppressNotifyCollectionChange", true);

                            this.ReadToListInternalAsync(
                                list,
                                reader,
                                columns,
                                columnToPropertyMap,
                                converter,
                                fetchRows,
                                itemFactory,
                                CancellationToken.None).GetAwaiter().GetResult();

                            Obj.Set(list, "SuppressNotifyCollectionChange", false);
                            return list;
                        }
                        finally
                        {
                            reader.Dispose();
                        }
                    }
                    catch (Exception ex) when (IsTimeoutException(ex) && attempt < DbClient.RetryCount && dbTransaction == null)
                    {
                        attempt++;
                        this.HandleDbException(ex, cmd);
                        this.CloseConnection();
                    }
                    catch (OperationCanceledException)
                    {
                        // НЕ ретраим отмену
                        throw;
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
        }

        /// <summary>
        /// Выполняет SQL-запрос и возвращает результат в виде коллекции объектов указанного типа.
        /// </summary>
        /// <param name="returnType">
        /// Тип результата запроса. Обычно это тип коллекции
        /// (например, <see cref="List{T}"/> или массив), элементы которой соответствуют строкам результата.
        /// </param>
        /// <param name="query">
        /// SQL-запрос для выполнения.
        /// Если не указан или равен <c>null</c>, запрос будет сгенерирован автоматически
        /// на основе типа элементов результата.
        /// </param>
        /// <param name="cmdParams">
        /// Параметры команды базы данных.
        /// Может быть анонимным объектом или словарём параметров.
        /// </param>
        /// <param name="columns">
        /// Список имён колонок, которые требуется считать из результата запроса.
        /// Если <c>null</c>, используются все колонки.
        /// </param>
        /// <param name="columnToPropertyMap">
        /// Сопоставление имён колонок базы данных с именами свойств объекта результата
        /// в виде пар <c>(ColumnName, PropertyName)</c>.
        /// </param>
        /// <param name="converter">
        /// Пользовательский конвертер значений из базы данных в значения свойств объекта.
        /// </param>
        /// <param name="fetchRows">
        /// Максимальное количество строк для выборки.
        /// Значение меньше нуля означает отсутствие ограничения.
        /// </param>
        /// <param name="offsetRows">
        /// Количество строк, которые необходимо пропустить перед началом выборки.
        /// </param>
        /// <param name="itemFactory">
        /// Фабрика создания экземпляров элементов результата.
        /// Получает массив значений колонок и массив их имён.
        /// Если не указана, используется фабрика по умолчанию,
        /// построенная на основе отражения.
        /// </param>
        /// <param name="dbTransaction">
        /// Транзакция базы данных, в контексте которой должен быть выполнен запрос.
        /// </param>
        /// <param name="token">Токен отмены.</param>
        /// <returns>
        /// Коллекция объектов типа, заданного параметром <paramref name="returnType"/>,
        /// заполненная результатами выполнения запроса.
        /// </returns>
        /// <exception cref="Exception">
        /// Любое исключение, возникшее при выполнении запроса,
        /// будет обработано и преобразовано методом <c>HandleDbException</c>.
        /// </exception>
        /// <remarks>
        /// <para>
        /// Метод выполняет следующие шаги:
        /// </para>
        /// <list type="bullet">
        /// <item>
        /// <description>
        /// Создаёт или генерирует SQL-запрос (если он не был передан явно).
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// Добавляет в запрос ограничения <c>LIMIT</c>/<c>OFFSET</c> с учётом настроек провайдера.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// Создаёт и выполняет команду базы данных.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// Считывает данные из <see cref="IDataReader"/> и наполняет результирующую коллекцию.
        /// </description>
        /// </item>
        /// </list>
        /// <para>
        /// Несмотря на использование асинхронной логики чтения,
        /// метод является синхронным и блокирует текущий поток до завершения операции.
        /// </para>
        /// </remarks>
        public async Task<object> QueryAsync(
            Type returnType,
            string query = null,
            object cmdParams = null,
            IEnumerable<string> columns = null,
            IEnumerable<(string ColumnName, string PropertyName)> columnToPropertyMap = null,
            DbValueConverter<object> converter = null,
            int fetchRows = -1,
            int offsetRows = 0,
            Func<object[], string[], object> itemFactory = null,
            IDbTransaction dbTransaction = null,
            CancellationToken token = default)
        {
            var returnTypeCache = MemberCache.Get(returnType);
            if (string.IsNullOrEmpty(query))
            {
                query = SqlQueryHelper.GetSelectQuery(this.Options, this.UseFullNamesInQueries, returnTypeCache.ElementType);
            }

            query = SqlQueryHelper.AddLimitOffsetClauseToQuery(
                this.Options,
                fetchRows,
                offsetRows,
                query,
                returnTypeCache.ElementType);
            itemFactory ??= BuildItemFactory<object>(returnTypeCache.ElementType, columnToPropertyMap);
            dbTransaction ??= this.tr?.Value;
            var attempt = 0;
            while (true)
            {
                token.ThrowIfCancellationRequested();

                using (var cmd = this.CreateCommand(query, cmdParams, dbTransaction, DefaultCommandTimeout + (RetryTimeoutStep * attempt)))
                {
                    await this.BeginConnectionAsync(token).ConfigureAwait(this.ConfigureAwait);

                    try
                    {
                        var reader = await cmd.ExecuteReaderAsync(token);
                        try
                        {
                            this.CommandExecuted?.Invoke(cmd);
                            this.Log(cmd);
                            var list = Obj.New(returnType) as IList;
                            Obj.Set(list, "SuppressNotifyCollectionChange", true);

                            await this.ReadToListInternalAsync(
                                list,
                                reader,
                                columns,
                                columnToPropertyMap,
                                converter,
                                fetchRows,
                                itemFactory,
                                token);

                            Obj.Set(list, "SuppressNotifyCollectionChange", false);
                            return list;
                        }
                        finally
                        {
                            reader.Dispose();
                        }
                    }
                    catch (Exception ex) when (IsTimeoutException(ex) && attempt < DbClient.RetryCount && dbTransaction == null)
                    {
                        attempt++;
                        this.HandleDbException(ex, cmd);
                        var delay = TimeSpan.FromMilliseconds(200 * attempt);
                        await Task.Delay(delay, token).ConfigureAwait(this.ConfigureAwait);
                        this.CloseConnection();
                    }
                    catch (OperationCanceledException)
                    {
                        // НЕ ретраим отмену
                        throw;
                    }
                    catch (Exception ex)
                    {
                        throw this.HandleDbException(ex, cmd);
                    }
                    finally
                    {
                        if (dbTransaction != null)
                        {
                            this.CloseConnection();
                        }
                    }
                }
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
        /// <param name="dbTransaction">Транзакция для запроса.</param>
        /// <param name="token">The cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>Коллекция объектов типа <typeparamref name="T" />, которая содержит результат выполнения запроса.</returns>
        /// <remarks>Этот метод выполняет SQL-запрос синхронно и возвращает результат в виде коллекции объектов.</remarks>
        public async Task<TList> QueryAsync<TList, T>(
            string query = null,
            object cmdParams = null,
            IEnumerable<string> columns = null,
            IEnumerable<(string ColumnName, string PropertyName)> columnToPropertyMap = null,
            DbValueConverter<T> converter = null,
            int fetchRows = -1,
            int offsetRows = 0,
            Func<object[], string[], T> itemFactory = null,
            IDbTransaction dbTransaction = null,
            CancellationToken token = default)
            where TList : ICollection<T>, IList, new()
        {
            if (string.IsNullOrEmpty(query))
            {
                query = SqlQueryHelper.GetSelectQuery<T>(this.Options, this.UseFullNamesInQueries);
            }

            query = SqlQueryHelper.AddLimitOffsetClauseToQuery(this.Options, fetchRows, offsetRows, query, typeof(T));

            var cache = MemberCache.Get(typeof(T));
            itemFactory ??= BuildItemFactory<T>(cache, columnToPropertyMap);
            dbTransaction ??= this.tr?.Value;
            var attempt = 0;
            while (true)
            {
                token.ThrowIfCancellationRequested();

                using (var cmd = this.CreateCommand(query, cmdParams, dbTransaction, DefaultCommandTimeout + (RetryTimeoutStep * attempt)))
                {
                    try
                    {
                        await this.BeginConnectionAsync(token).ConfigureAwait(this.ConfigureAwait);

                        using (var reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(this.ConfigureAwait))
                        {
                            this.CommandExecuted?.Invoke(cmd);
                            this.Log(cmd);

                            var list = new TList();

                            if (list is ObservableCollectionEx<T> oce1)
                            {
                                oce1.SuspendNotifications(true);
                            }

                            await this.ReadToListInternalAsync(
                                list,
                                reader,
                                columns,
                                columnToPropertyMap,
                                converter,
                                fetchRows,
                                itemFactory,
                                token).ConfigureAwait(this.ConfigureAwait);

                            if (list is ObservableCollectionEx<T> oce2)
                            {
                                oce2.SuspendNotifications(false);
                            }

                            return list;
                        }
                    }
                    catch (Exception ex) when (IsTimeoutException(ex) && attempt < DbClient.RetryCount && dbTransaction == null)
                    {
                        attempt++;
                        this.HandleDbException(ex, cmd);
                        var delay = TimeSpan.FromMilliseconds(200 * attempt);
                        await Task.Delay(delay, token).ConfigureAwait(this.ConfigureAwait);
                        this.CloseConnection();
                    }
                    catch (OperationCanceledException)
                    {
                        // НЕ ретраим отмену
                        throw;
                    }
                    catch (Exception ex)
                    {
                        throw this.HandleDbException(ex, cmd);
                    }
                    finally
                    {
                        if (dbTransaction == null)
                        {
                            this.CloseConnection();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Отменяется текущая транзакция.
        /// </summary>
        public void RollbackTransaction()
        {
            if (this.tr.Value == null)
            {
                return;
            }

            this.tr.Value.Rollback();
            this.tr.Value.Dispose();
            this.tr.Value = null;
        }

        /// <summary>
        /// Получает сумму для указанного столбца.
        /// </summary>
        /// <typeparam name="TFrom">Тип сущности.</typeparam>
        /// <typeparam name="T">Тип данных.</typeparam>
        /// <param name="columnSelector">Выражение для выбора столбца, для которого будет вычислена сумма.</param>
        /// <param name="whereExpression">The where expression.</param>
        /// <returns>Сумма для указанного столбца.</returns>
        /// <remarks>Этот метод использует SQL-функцию SUM для получения суммы значений в столбце.</remarks>
        public T Sum<TFrom, T>(
            Expression<Func<TFrom, T>> columnSelector,
            Expression<Func<TFrom, bool>> whereExpression = null)
            where TFrom : class => ChangeType<T>(this.Agg("SUM", whereExpression, columnSelector.ConvertExpression()).Values.FirstOrDefault());

        /// <summary>
        /// Асинхронно получает сумму для указанного столбца.
        /// </summary>
        /// <typeparam name="TFrom">Тип сущности.</typeparam>
        /// <typeparam name="T">Тип данных.</typeparam>
        /// <param name="columnSelector">Выражение для выбора столбца, для которого будет вычислена сумма.</param>
        /// <param name="whereExpression">The where expression.</param>
        /// <param name="token">Токен отмены асинхронной операции.</param>
        /// <returns>Задача, которая возвращает сумму для указанного столбца.</returns>
        /// <remarks>Этот метод асинхронно использует SQL-функцию SUM для получения суммы значений в столбце.</remarks>
        public async Task<T> SumAsync<TFrom, T>(
            Expression<Func<TFrom, T>> columnSelector,
            Expression<Func<TFrom, bool>> whereExpression = null,
            CancellationToken token = default)
            where TFrom : class => ChangeType<T>((await this.AggAsync("SUM", whereExpression, token, columnSelector.ConvertExpression())
                .ConfigureAwait(this.ConfigureAwait)).Values.FirstOrDefault());

        /// <summary>
        /// Выполняет SQL-запрос и возвращает результат в виде коллекции объектов типа <typeparamref name="TItem" />.
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
        /// <remarks>Этот метод выполняет SQL-запрос синхронно и возвращает результат в виде коллекции объектов.
        /// Если <paramref name="itemFactory" /> не задан, используется стандартное преобразование данных в объекты.</remarks>
        public ObservableCollection<TItem> ToCollection<TItem>(
            string query = null,
            object cmdParams = null,
            IEnumerable<string> columns = null,
            IEnumerable<(string ColumnName, string PropertyName)> columnToPropertyMap = null,
            DbValueConverter<TItem> converter = null,
            int fetchRows = -1,
            int offsetRows = 0,
            Func<object[], string[], TItem> itemFactory = null)
        {
            var list = this.Query<ObservableCollection<TItem>, TItem>(
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
        /// Выполняет SQL-запрос с фильтрацией и возвращает результат в виде коллекции объектов типа <typeparamref name="TItem" />.
        /// </summary>
        /// <typeparam name="TItem">Тип объектов, которые будут содержаться в списке.</typeparam>
        /// <param name="whereExpression">Выражение для фильтрации данных.</param>
        /// <param name="columnToPropertyMap">Отображение столбцов SQL-запроса в свойства объектов. Может быть <c>null</c>.</param>
        /// <param name="converter">Конвертер для преобразования данных. Может быть <c>null</c>.</param>
        /// <param name="fetchRows">Количество строк для выборки. По умолчанию -1 (выбираются все строки).</param>
        /// <param name="offsetRows">Количество строк для пропуска перед выборкой. По умолчанию - 0.</param>
        /// <param name="itemFactory">Функция для создания объектов типа <typeparamref name="TItem" />. Может быть <c>null</c>.</param>
        /// <param name="orderByExpression">Выражение для сортировки. Может быть <c>null</c>.</param>
        /// <returns>Список объектов типа <typeparamref name="TItem" />.</returns>
        /// <remarks>Этот метод выполняет SQL-запрос синхронно с фильтрацией по выражению <paramref name="whereExpression" /> и
        /// возвращает результат в виде списка.</remarks>
        public ObservableCollection<TItem> ToCollection<TItem>(
            Expression<Func<TItem, bool>> whereExpression,
            IEnumerable<(string ColumnName, string PropertyName)> columnToPropertyMap = null,
            DbValueConverter<TItem> converter = null,
            int fetchRows = -1,
            int offsetRows = 0,
            Func<object[], string[], TItem> itemFactory = null,
            params (Expression<Func<TItem, object>>, bool)[] orderByExpression)
        {
            var query = (SqlQueryHelper.GetSelectQuery<TItem>(this.Options, this.UseFullNamesInQueries) + " " + SqlQueryHelper.GetWhereClause(
                             this.Options,
                             whereExpression,
                             true,
                             out var cmdParam) +
                         " " + SqlQueryHelper.GetOrderBy(this.Options, orderByExpression)).Trim();

            return this.ToCollection(
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
        /// Асинхронно выполняет SQL-запрос и возвращает результат в виде коллекции объектов типа <typeparamref name="T" />.
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
        /// <returns>Задача, которая возвращает коллекцию объектов типа <typeparamref name="T" />.</returns>
        /// <remarks>Этот метод выполняет SQL-запрос асинхронно и возвращает результат в виде коллекции объектов.
        /// Если <paramref name="itemFactory" /> не задан, используется стандартное преобразование данных в объекты.</remarks>
        public Task<ObservableCollection<T>> ToCollectionAsync<T>(
            string query = null,
            object cmdParams = null,
            IEnumerable<string> columns = null,
            IEnumerable<(string ColumnName, string PropertyName)> columnToPropertyMap = null,
            DbValueConverter<T> converter = null,
            int fetchRows = -1,
            int offsetRows = 0,
            Func<object[], string[], T> itemFactory = null,
            CancellationToken ct = default) => this.QueryAsync<ObservableCollection<T>, T>(
                query,
                cmdParams,
                columns,
                columnToPropertyMap,
                converter,
                fetchRows,
                offsetRows,
                itemFactory,
                null,
                ct);

        /// <summary>
        /// Асинхронно выполняет SQL-запрос с фильтрацией и возвращает результат в виде коллекции объектов типа
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
        /// <returns>Задача, которая возвращает коллекцию объектов типа <typeparamref name="T" />.</returns>
        /// <remarks>Этот метод выполняет SQL-запрос асинхронно с фильтрацией и сортировкой, и возвращает результат в виде коллекции.</remarks>
        public Task<ObservableCollection<T>> ToCollectionAsync<T>(
            Expression<Func<T, bool>> whereExpression,
            IEnumerable<(string ColumnName, string PropertyName)> columnToPropertyMap = null,
            DbValueConverter<T> converter = null,
            int fetchRows = -1,
            int offsetRows = 0,
            Func<object[], string[], T> itemFactory = null,
            CancellationToken ct = default,
            params (Expression<Func<T, object>>, bool)[] orderByExpression)
        {
            var query = (SqlQueryHelper.GetSelectQuery<T>(this.Options, this.UseFullNamesInQueries) + " " + SqlQueryHelper.GetWhereClause(
                             this.Options,
                             whereExpression,
                             true,
                             out var cmdParam) +
                         " " + SqlQueryHelper.GetOrderBy(this.Options, orderByExpression)).Trim();

            return this.ToCollectionAsync(
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
        /// Выполняет SQL-запрос и возвращает результат в виде коллекции объектов типа <typeparamref name="TItem" />.
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
        /// <remarks>Этот метод выполняет SQL-запрос синхронно и возвращает результат в виде коллекции объектов.
        /// Если <paramref name="itemFactory" /> не задан, используется стандартное преобразование данных в объекты.</remarks>
        public ObservableCollectionEx<TItem> ToCollectionEx<TItem>(
            string query = null,
            object cmdParams = null,
            IEnumerable<string> columns = null,
            IEnumerable<(string ColumnName, string PropertyName)> columnToPropertyMap = null,
            DbValueConverter<TItem> converter = null,
            int fetchRows = -1,
            int offsetRows = 0,
            Func<object[], string[], TItem> itemFactory = null)
        {
            var list = this.Query<ObservableCollectionEx<TItem>, TItem>(
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
        /// Выполняет SQL-запрос с фильтрацией и возвращает результат в виде коллекции объектов типа <typeparamref name="TItem" />.
        /// </summary>
        /// <typeparam name="TItem">Тип объектов, которые будут содержаться в списке.</typeparam>
        /// <param name="whereExpression">Выражение для фильтрации данных.</param>
        /// <param name="columnToPropertyMap">Отображение столбцов SQL-запроса в свойства объектов. Может быть <c>null</c>.</param>
        /// <param name="converter">Конвертер для преобразования данных. Может быть <c>null</c>.</param>
        /// <param name="fetchRows">Количество строк для выборки. По умолчанию -1 (выбираются все строки).</param>
        /// <param name="offsetRows">Количество строк для пропуска перед выборкой. По умолчанию - 0.</param>
        /// <param name="itemFactory">Функция для создания объектов типа <typeparamref name="TItem" />. Может быть <c>null</c>.</param>
        /// <param name="orderByExpression">Выражение для сортировки. Может быть <c>null</c>.</param>
        /// <returns>Список объектов типа <typeparamref name="TItem" />.</returns>
        /// <remarks>Этот метод выполняет SQL-запрос синхронно с фильтрацией по выражению <paramref name="whereExpression" /> и
        /// возвращает результат в виде списка.</remarks>
        public ObservableCollectionEx<TItem> ToCollectionEx<TItem>(
            Expression<Func<TItem, bool>> whereExpression,
            IEnumerable<(string ColumnName, string PropertyName)> columnToPropertyMap = null,
            DbValueConverter<TItem> converter = null,
            int fetchRows = -1,
            int offsetRows = 0,
            Func<object[], string[], TItem> itemFactory = null,
            params (Expression<Func<TItem, object>>, bool)[] orderByExpression)
        {
            var query = (SqlQueryHelper.GetSelectQuery<TItem>(this.Options, this.UseFullNamesInQueries) + " " + SqlQueryHelper.GetWhereClause(
                             this.Options,
                             whereExpression,
                             true,
                             out var cmdParam) +
                         " " + SqlQueryHelper.GetOrderBy(this.Options, orderByExpression)).Trim();

            return this.ToCollectionEx(
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
        /// Асинхронно выполняет SQL-запрос и возвращает результат в виде коллекции объектов типа <typeparamref name="T" />.
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
        /// <returns>Задача, которая возвращает коллекцию объектов типа <typeparamref name="T" />.</returns>
        /// <remarks>Этот метод выполняет SQL-запрос асинхронно и возвращает результат в виде коллекции объектов.
        /// Если <paramref name="itemFactory" /> не задан, используется стандартное преобразование данных в объекты.</remarks>
        public Task<ObservableCollectionEx<T>> ToCollectionExAsync<T>(
            string query = null,
            object cmdParams = null,
            IEnumerable<string> columns = null,
            IEnumerable<(string ColumnName, string PropertyName)> columnToPropertyMap = null,
            DbValueConverter<T> converter = null,
            int fetchRows = -1,
            int offsetRows = 0,
            Func<object[], string[], T> itemFactory = null,
            CancellationToken ct = default) => this.QueryAsync<ObservableCollectionEx<T>, T>(
                query,
                cmdParams,
                columns,
                columnToPropertyMap,
                converter,
                fetchRows,
                offsetRows,
                itemFactory,
                null,
                ct);

        /// <summary>
        /// Асинхронно выполняет SQL-запрос с фильтрацией и возвращает результат в виде коллекции объектов типа
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
        /// <returns>Задача, которая возвращает коллекцию объектов типа <typeparamref name="T" />.</returns>
        /// <remarks>Этот метод выполняет SQL-запрос асинхронно с фильтрацией и сортировкой, и возвращает результат в виде коллекции.</remarks>
        public Task<ObservableCollectionEx<T>> ToCollectionExAsync<T>(
            Expression<Func<T, bool>> whereExpression,
            IEnumerable<(string ColumnName, string PropertyName)> columnToPropertyMap = null,
            DbValueConverter<T> converter = null,
            int fetchRows = -1,
            int offsetRows = 0,
            Func<object[], string[], T> itemFactory = null,
            CancellationToken ct = default,
            params (Expression<Func<T, object>>, bool)[] orderByExpression)
        {
            var query = (SqlQueryHelper.GetSelectQuery<T>(this.Options, this.UseFullNamesInQueries) + " " + SqlQueryHelper.GetWhereClause(
                             this.Options,
                             whereExpression,
                             true,
                             out var cmdParam) +
                         " " + SqlQueryHelper.GetOrderBy(this.Options, orderByExpression)).Trim();

            return this.ToCollectionExAsync(
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
        /// Выполняет SQL-запрос и возвращает результат в виде <see cref="DataTable" />.
        /// </summary>
        /// <typeparam name="TFrom">Тип объектов, для которых будет выполняться запрос.</typeparam>
        /// <param name="whereExpression">Условие выборки данных в виде выражения. Может быть <c>null</c>.</param>
        /// <param name="fetchRows">Количество строк для выборки. По умолчанию -1 (выбираются все строки).</param>
        /// <param name="offsetRows">Количество строк для пропуска перед выборкой. По умолчанию - 0.</param>
        /// <param name="valueConverter">Конвертор значения из БД в тип данных колонки таблицы.</param>
        /// <param name="columnSelectors">Селекторы столбцов для выборки. Может быть <c>null</c>.</param>
        /// <returns><see cref="DataTable" />, содержащий результат выполнения запроса.</returns>
        /// <remarks>Этот метод выполняет SQL-запрос синхронно и возвращает результат в виде <see cref="DataTable" />.
        /// Если передан параметр <paramref name="columnSelectors" />, то выборка будет происходить только по указанным
        /// столбцам.</remarks>
        public DataTable ToDataTable<TFrom>(
            Expression<Func<TFrom, bool>> whereExpression = null,
            int fetchRows = -1,
            int offsetRows = 0,
            Func<string, object, DataColumn, object> valueConverter = null,
            params Expression<Func<TFrom, object>>[] columnSelectors)
        {
            var query = (SqlQueryHelper.GetSelectQuery(this.Options, this.UseFullNamesInQueries, columnSelectors) + " " +
                         SqlQueryHelper.GetWhereClause(
                             this.Options,
                             whereExpression,
                             true,
                             out var cmdParam)).Trim();
            query = SqlQueryHelper.AddLimitOffsetClauseToQuery(
                this.Options,
                fetchRows,
                offsetRows,
                query,
                typeof(TFrom));
            return this.ToDataTables(query, cmdParam, valueConverter).FirstOrDefault();
        }

        /// <summary>
        ///     Выполняет SQL-запрос и возвращает результат в виде <see cref="DataTable" />, с возможностью отображения столбцов в
        ///     соответствии с их именами.
        /// </summary>
        /// <param name="query">SQL-запрос для выполнения.</param>
        /// <param name="cmdParams">Параметры запроса.</param>
        /// <param name="valueConverter">Конвертор значения из БД в тип данных колонки таблицы.</param>
        /// <param name="columnMap">
        ///     Отображение столбцов запроса в имена свойств объектов. Каждый элемент содержит имя столбца и
        ///     имя свойства объекта.
        /// </param>
        /// <returns><see cref="DataTable" />, содержащий результат выполнения запроса.</returns>
        /// <remarks>
        ///     Этот метод выполняет SQL-запрос синхронно и возвращает результат в виде <see cref="DataTable" />, при этом
        ///     позволяет
        ///     отображать столбцы запроса в соответствии с их именами в объекте.
        /// </remarks>
        public DataTable ToDataTable(string query, object cmdParams = null, Func<string, object, DataColumn, object> valueConverter = null, params (string, string)[] columnMap)
            => this.ToDataTables(query, cmdParams, valueConverter, columnMap).FirstOrDefault();

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
            var query = (SqlQueryHelper.GetSelectQuery(this.Options, this.UseFullNamesInQueries, columnSelectors) + " " +
                         SqlQueryHelper.GetWhereClause(
                             this.Options,
                             whereExpression,
                             true,
                             out var cmdParam)).Trim();
            query = SqlQueryHelper.AddLimitOffsetClauseToQuery(
                this.Options,
                fetchRows,
                offsetRows,
                query,
                typeof(TFrom));
            return (await this.ToDataTablesAsync(query, cmdParam).ConfigureAwait(this.ConfigureAwait)).FirstOrDefault();
        }

        /// <summary>
        ///     Асинхронно выполняет SQL-запрос и возвращает результат в виде <see cref="DataTable" />, с возможностью отображения
        ///     столбцов в соответствии с их именами.
        /// </summary>
        /// <param name="query">SQL-запрос для выполнения.</param>
        /// <param name="cmdParams">Параметры запроса.</param>
        /// <param name="valueConverter">Конвертор значения из БД в тип данных колонки таблицы.</param>
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
        /// </remarks>
        public async Task<DataTable> ToDataTableAsync(
            string query,
            object cmdParams = null,
            Func<string, object, DataColumn, object> valueConverter = null,
            CancellationToken token = default,
            params (string, string)[] columnMap) => (await this.ToDataTablesAsync(query, cmdParams, valueConverter, token, columnMap)
                .ConfigureAwait(this.ConfigureAwait)).FirstOrDefault();

        /// <summary>
        ///     Выполняет SQL-запрос и возвращает результат в виде массива <see cref="DataTable" />.
        /// </summary>
        /// <param name="query">SQL-запрос для выполнения.</param>
        /// <param name="cmdParams">Параметры запроса.</param>
        /// <param name="valueConverter">Конвертор значения из БД в тип данных колонки таблицы.</param>
        /// <param name="columnMap">Отображение столбцов запроса в имена свойств объектов.</param>
        /// <returns>Массив <see cref="DataTable" />, содержащий результаты выполнения запроса.</returns>
        /// <remarks>
        ///     Этот метод выполняет SQL-запрос синхронно и возвращает результаты в виде массива <see cref="DataTable" />. Если
        ///     запрос
        ///     возвращает несколько наборов данных, они будут разделены в разные таблицы.
        /// </remarks>
        public DataTable[] ToDataTables(string query, object cmdParams = null, Func<string, object, DataColumn, object> valueConverter = null, params (string, string)[] columnMap)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                throw new ArgumentNullException(nameof(query));
            }

            var result = new List<DataTable>(2);
            var stringPool = this.EnableStringPool ? new StringPool() : null;
            using var cmd = this.CreateCommand(query, cmdParams, null);
            try
            {
                this.BeginConnection();
                using var r = cmd.ExecuteReader();
                this.CommandExecuted?.Invoke(cmd);
                this.Log(cmd);
                do
                {
                    var table = new DataTable(query);
                    table.BeginLoadData();
                    var fieldCount = r.FieldCount;
                    var map = GetReaderFieldToPropertyMap(r, columnMap)
                        .OrderBy(x => x.Key)
                        .ToArray();
                    var columns = new DataColumn[fieldCount];
                    var names = new string[fieldCount];

                    foreach (var kv in map)
                    {
                        var col = new DataColumn(
                            kv.Value,
                            r.GetFieldType(kv.Key) ?? typeof(object));

                        table.Columns.Add(col);

                        columns[kv.Key] = col;
                        names[kv.Key] = kv.Value;
                    }

                    var values = new object[fieldCount];
                    if (valueConverter != null || this.EnableStringPool)
                    {
                        while (r.Read())
                        {
                            r.GetValues(values);

                            for (var i = 0; i < fieldCount; i++)
                            {
                                var raw = values[i];

                                if (raw == null || raw == DBNull.Value)
                                {
                                    continue;
                                }

                                var col = columns[i];
                                if (col == null)
                                {
                                    continue;
                                }

                                if (this.EnableStringPool &&
                                    raw is string s &&
                                    this.PooledStringColumns.Contains(names[i]))
                                {
                                    raw = stringPool?.Intern(s);
                                }

                                values[i] = valueConverter?.Invoke(names[i], raw, col) ?? raw;
                            }

                            table.LoadDataRow(values, true);
                        }
                    }
                    else
                    {
                        while (r.Read())
                        {
                            r.GetValues(values);
                            table.LoadDataRow(values, true);
                        }
                    }

                    table.EndLoadData();
                    result.Add(table);
                }
                while (r.NextResult());

                return result.ToArray();
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

        /// <summary>
        ///     Асинхронно выполняет SQL-запрос и возвращает результат в виде массива <see cref="DataTable" />.
        /// </summary>
        /// <param name="query">SQL-запрос для выполнения.</param>
        /// <param name="cmdParams">Параметры запроса.</param>
        /// <param name="valueConverter">Конвертор значения из БД в тип данных колонки таблицы.</param>
        /// <param name="token">Токен отмены асинхронной операции.</param>
        /// <param name="columnMap">Отображение столбцов запроса в имена свойств объектов.</param>
        /// <returns>Задача, которая возвращает массив <see cref="DataTable" />, содержащий результаты выполнения запроса.</returns>
        /// <remarks>
        ///     Этот метод выполняет SQL-запрос асинхронно и возвращает результаты в виде массива <see cref="DataTable" />.
        ///     Если запрос возвращает несколько наборов данных, они будут разделены в разные таблицы.
        /// </remarks>
        public async Task<DataTable[]> ToDataTablesAsync(
            string query,
            object cmdParams = null,
            Func<string, object, DataColumn, object> valueConverter = null,
            CancellationToken token = default,
            params (string, string)[] columnMap)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                throw new ArgumentNullException(nameof(query));
            }

            var result = new List<DataTable>(2);
            var stringPool = this.EnableStringPool ? new StringPool() : null;
            using var cmd = this.CreateCommand(query, cmdParams, null);
            try
            {
                this.BeginConnection();
                using var r = await cmd.ExecuteReaderAsync(token);
                this.CommandExecuted?.Invoke(cmd);
                this.Log(cmd);
                do
                {
                    var table = new DataTable(query);
                    table.BeginLoadData();
                    var fieldCount = r.FieldCount;
                    var map = GetReaderFieldToPropertyMap(r, columnMap)
                        .OrderBy(x => x.Key)
                        .ToArray();
                    var columns = new DataColumn[fieldCount];
                    var names = new string[fieldCount];

                    foreach (var kv in map)
                    {
                        var col = new DataColumn(
                            kv.Value,
                            r.GetFieldType(kv.Key) ?? typeof(object));

                        table.Columns.Add(col);

                        columns[kv.Key] = col;
                        names[kv.Key] = kv.Value;
                    }

                    var values = new object[fieldCount];
                    if (valueConverter != null || this.EnableStringPool)
                    {
                        while (await r.ReadAsync(token))
                        {
                            r.GetValues(values);

                            for (var i = 0; i < fieldCount; i++)
                            {
                                var raw = values[i];

                                if (raw == null || raw == DBNull.Value)
                                {
                                    continue;
                                }

                                var col = columns[i];
                                if (col == null)
                                {
                                    continue;
                                }

                                if (this.EnableStringPool &&
                                    raw is string s &&
                                    this.PooledStringColumns.Contains(names[i]))
                                {
                                    raw = stringPool?.Intern(s);
                                }

                                values[i] = valueConverter?.Invoke(names[i], raw, col) ?? raw;
                            }

                            table.LoadDataRow(values, true);
                        }
                    }
                    else
                    {
                        while (await r.ReadAsync(token))
                        {
                            r.GetValues(values);
                            table.LoadDataRow(values, true);
                        }
                    }

                    table.EndLoadData();
                    result.Add(table);
                }
                while (await r.NextResultAsync());

                return result.ToArray();
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
            IEnumerable<(string ColumnName, string PropertyName)> columnToPropertyMap = null,
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
            var query = (SqlQueryHelper.GetSelectQuery(
                             this.Options,
                             this.UseFullNamesInQueries,
                             typeof(TFrom).GetMemberCache(),
                             keySelector.GetMemberCache(),
                             valueSelector.GetMemberCache()) + " " +
                         SqlQueryHelper.GetWhereClause(
                             this.Options,
                             whereExpression,
                             true,
                             out var cmdParam)).Trim();
            query = SqlQueryHelper.AddLimitOffsetClauseToQuery(
                this.Options,
                fetchRows,
                offsetRows,
                query,
                typeof(TFrom));
            var list = this.ToList(
                query,
                cmdParam,
                null,
                null,
                null,
                fetchRows,
                offsetRows,
                itemFactory);
            var dic = list.ToDictionary(x => x.Key, x => x.Value);
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
            IEnumerable<(string ColumnName, string PropertyName)> columnToPropertyMap = null,
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
            var query = (SqlQueryHelper.GetSelectQuery(
                             this.Options,
                             this.UseFullNamesInQueries,
                             typeof(TFrom).GetMemberCache(),
                             keySelector.GetMemberCache(),
                             valueSelector.GetMemberCache()) + " " +
                         SqlQueryHelper.GetWhereClause(
                             this.Options,
                             whereExpression,
                             true,
                             out var cmdParam)).Trim();
            query = SqlQueryHelper.AddLimitOffsetClauseToQuery(
                this.Options,
                fetchRows,
                offsetRows,
                query,
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
            IEnumerable<(string ColumnName, string PropertyName)> columnToPropertyMap = null,
            DbValueConverter<TItem> converter = null,
            int fetchRows = -1,
            int offsetRows = 0,
            Func<object[], string[], TItem> itemFactory = null)
        {
            var list = this.Query<List<TItem>, TItem>(
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
        /// Выполняет SQL-запрос с фильтрацией и возвращает результат в виде списка объектов типа <typeparamref name="TItem" />.
        /// </summary>
        /// <typeparam name="TItem">Тип объектов, которые будут содержаться в списке.</typeparam>
        /// <param name="whereExpression">Выражение для фильтрации данных.</param>
        /// <param name="columnToPropertyMap">Отображение столбцов SQL-запроса в свойства объектов. Может быть <c>null</c>.</param>
        /// <param name="converter">Конвертер для преобразования данных. Может быть <c>null</c>.</param>
        /// <param name="fetchRows">Количество строк для выборки. По умолчанию -1 (выбираются все строки).</param>
        /// <param name="offsetRows">Количество строк для пропуска перед выборкой. По умолчанию - 0.</param>
        /// <param name="itemFactory">Функция для создания объектов типа <typeparamref name="TItem" />. Может быть <c>null</c>.</param>
        /// <param name="orderByExpression">Выражение для сортировки. Может быть <c>null</c>.</param>
        /// <returns>Список объектов типа <typeparamref name="TItem" />.</returns>
        /// <remarks>Этот метод выполняет SQL-запрос синхронно с фильтрацией по выражению <paramref name="whereExpression" /> и
        /// возвращает результат в виде списка.</remarks>
        public List<TItem> ToList<TItem>(
            Expression<Func<TItem, bool>> whereExpression,
            IEnumerable<(string ColumnName, string PropertyName)> columnToPropertyMap = null,
            DbValueConverter<TItem> converter = null,
            int fetchRows = -1,
            int offsetRows = 0,
            Func<object[], string[], TItem> itemFactory = null,
            params (Expression<Func<TItem, object>>, bool)[] orderByExpression)
        {
            var query = (SqlQueryHelper.GetSelectQuery<TItem>(this.Options, this.UseFullNamesInQueries) + " " + SqlQueryHelper.GetWhereClause(
                             this.Options,
                             whereExpression,
                             true,
                             out var cmdParam) +
                         " " + SqlQueryHelper.GetOrderBy(this.Options, orderByExpression)).Trim();

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
            IEnumerable<(string ColumnName, string PropertyName)> columnToPropertyMap = null,
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
                null,
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
            IEnumerable<(string ColumnName, string PropertyName)> columnToPropertyMap = null,
            DbValueConverter<T> converter = null,
            int fetchRows = -1,
            int offsetRows = 0,
            Func<object[], string[], T> itemFactory = null,
            CancellationToken ct = default,
            params (Expression<Func<T, object>>, bool)[] orderByExpression)
        {
            var query = (SqlQueryHelper.GetSelectQuery<T>(this.Options, this.UseFullNamesInQueries) + " " + SqlQueryHelper.GetWhereClause(
                             this.Options,
                             whereExpression,
                             true,
                             out var cmdParam) +
                         " " + SqlQueryHelper.GetOrderBy(this.Options, orderByExpression)).Trim();

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
            IDbTransaction dbTransaction,
            params Expression<Func<T, object>>[] updateColumns)
            where T : class => this.Update(item, null, null, dbTransaction, updateColumns);

        /// <summary>
        /// Обновляет запись в базе данных на основе значений свойств объекта.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="item">Объект, содержащий обновляемые значения.</param>
        /// <param name="updateColumns">Список колонок, которые необходимо обновить.
        /// Если не указан, обновляются все сопоставленные свойства,
        /// за исключением первичных ключей.</param>
        /// <returns>Количество строк, затронутых операцией обновления.</returns>
        public int Update<T>(T item, params Expression<Func<T, object>>[] updateColumns)
            where T : class => this.Update(item, null, null, null, updateColumns);

        /// <summary>
        /// Обновляет записи в указанной таблице, используя объекты со значениями
        /// для секций <c>SET</c> и <c>WHERE</c>.
        /// </summary>
        /// <param name="tableName">Имя таблицы, в которой выполняется обновление.</param>
        /// <param name="updateValues">
        /// Объект со свойствами, значения которых будут использованы
        /// для формирования выражений секции <c>SET</c>.
        /// Имена свойств соответствуют именам столбцов.
        /// </param>
        /// <param name="whereValues">
        /// Объект со свойствами, значения которых будут использованы
        /// для формирования условий секции <c>WHERE</c>.
        /// Имена свойств соответствуют именам столбцов.
        /// </param>
        /// <returns>
        /// Количество строк, затронутых командой <c>UPDATE</c>.
        /// </returns>
        /// <remarks>
        /// Имена столбцов и параметры запроса формируются автоматически
        /// на основании переданных объектов и текущих настроек подключения.
        /// </remarks>
        public int Update(string tableName, object updateValues, object whereValues)
        {
            var updateMap = updateValues as IDictionary<string, object> ?? Obj.GetValues(updateValues);
            var whereMap = whereValues as IDictionary<string, object> ?? Obj.GetValues(whereValues);
            var mergedMap = new Dictionary<string, object>(updateMap);

            foreach (var kv in whereMap)
            {
                mergedMap[kv.Key] = kv.Value;
            }

            var sql = $"UPDATE {this.Options.NamePrefix}{tableName}{this.Options.NameSuffix} SET {string.Join(", ", updateMap.Select(x => $"{this.Options.NamePrefix}{x.Key}{this.Options.NameSuffix} = {this.Options.ParamPrefix}{x.Key}"))}";
            if (whereMap.Count > 0)
            {
                sql += $" WHERE {string.Join(", ", whereMap.Select(x => $"{this.Options.NamePrefix}{x.Key}{this.Options.NameSuffix} = {this.Options.ParamPrefix}{x.Key}"))}";
            }

            return this.ExecuteNonQuery(sql, mergedMap);
        }

        /// <summary>
        /// Обновляет запись в базе данных на основе значений свойств объекта.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="item">Объект, содержащий обновляемые значения.</param>
        /// <param name="tableName">Имя таблицы в которую вставляьб записи.</param>
        /// <param name="dbTransaction">Активная транзакция базы данных.
        /// Если не указана, используется текущая транзакция или соединение.</param>
        /// <param name="updateColumns">Список колонок, которые необходимо обновить.
        /// Если не указан, обновляются все сопоставленные свойства,
        /// за исключением первичных ключей.</param>
        /// <returns>Количество строк, затронутых операцией обновления.</returns>
        public int Update<T>(
            T item,
            string tableName,
            IDbTransaction dbTransaction,
            params Expression<Func<T, object>>[] updateColumns)
            where T : class => this.Update(item, tableName, null, dbTransaction, updateColumns);

        /// <summary>
        /// Обновляет запись в базе данных на основе значений свойств объекта.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="item">Объект, содержащий обновляемые значения.</param>
        /// <param name="tableName">Имя таблицы в которую вставляьб записи.</param>
        /// <param name="updateColumns">Список колонок, которые необходимо обновить.
        /// Если не указан, обновляются все сопоставленные свойства,
        /// за исключением первичных ключей.</param>
        /// <returns>Количество строк, затронутых операцией обновления.</returns>
        public int Update<T>(T item, string tableName, params Expression<Func<T, object>>[] updateColumns)
            where T : class => this.Update(item, tableName, null, null, updateColumns);

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
            return this.Update(item, null, whereExpression, dbTransaction, updateColumns);
        }

        /// <summary>
        /// Обновляет записи в базе данных на основе указанного условия.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="item">Объект, содержащий значения для обновления колонок.</param>
        /// <param name="tableName">Имя таблицы в которую вставлять записи.</param>
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
            string tableName,
            Expression<Func<T, bool>> whereExpression,
            IDbTransaction dbTransaction = null,
            params Expression<Func<T, object>>[] updateColumns)
            where T : class
        {
            if (!string.IsNullOrWhiteSpace(tableName))
            {
                this.Options.Map.Table<T>(tableName);
            }

            var query = SqlQueryHelper.GetUpdateQuery(this.Options, updateColumns);
            var cmdParams = Obj.GetValues(item);
            query += " " + (whereExpression != null
                ? SqlQueryHelper.GetWhereClause(this.Options, whereExpression, true, out cmdParams)
                : SqlQueryHelper.GetWhereClause<T>(this.Options, out _));

            return this.ExecuteNonQuery(query, cmdParams, dbTransaction);
        }

        /// <summary>
        /// Обновляет запись в базе данных на основе значений свойств объекта.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="item">Объект, содержащий обновляемые значения.</param>
        /// <param name="dbTransaction">Активная транзакция базы данных.
        /// Если не указана, используется текущая транзакция или соединение.</param>
        /// <param name="token">Токен отмены.</param>
        /// <param name="updateColumns">Список колонок, которые необходимо обновить.
        /// Если не указан, обновляются все сопоставленные свойства,
        /// за исключением первичных ключей.</param>
        /// <returns>Количество строк, затронутых операцией обновления.</returns>
        public Task<int> UpdateAsync<T>(
            T item,
            IDbTransaction dbTransaction,
            CancellationToken token = default,
            params Expression<Func<T, object>>[] updateColumns)
            where T : class => this.UpdateAsync(item, null, null, dbTransaction, token, updateColumns);

        /// <summary>
        /// Обновляет запись в базе данных на основе значений свойств объекта.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="item">Объект, содержащий обновляемые значения.</param>
        /// <param name="token">Токен отмены.</param>
        /// <param name="updateColumns">Список колонок, которые необходимо обновить.
        /// Если не указан, обновляются все сопоставленные свойства,
        /// за исключением первичных ключей.</param>
        /// <returns>Количество строк, затронутых операцией обновления.</returns>
        public Task<int> UpdateAsync<T>(T item, CancellationToken token = default, params Expression<Func<T, object>>[] updateColumns)
            where T : class => this.UpdateAsync(item, null, null, null, token, updateColumns);

        /// <summary>
        /// Обновляет записи в указанной таблице, используя объекты со значениями
        /// для секций <c>SET</c> и <c>WHERE</c>.
        /// </summary>
        /// <param name="tableName">Имя таблицы, в которой выполняется обновление.</param>
        /// <param name="updateValues">
        /// Объект со свойствами, значения которых будут использованы
        /// для формирования выражений секции <c>SET</c>.
        /// Имена свойств соответствуют именам столбцов.
        /// </param>
        /// <param name="whereValues">
        /// Объект со свойствами, значения которых будут использованы
        /// для формирования условий секции <c>WHERE</c>.
        /// Имена свойств соответствуют именам столбцов.
        /// </param>
        /// <param name="dbTransaction">Транзакция для операции.</param>
        /// <param name="token">Токен отмены.</param>
        /// <returns>
        /// Количество строк, затронутых командой <c>UPDATE</c>.
        /// </returns>
        /// <remarks>
        /// Имена столбцов и параметры запроса формируются автоматически
        /// на основании переданных объектов и текущих настроек подключения.
        /// </remarks>
        public Task<int> UpdateAsync(string tableName, object updateValues, object whereValues, IDbTransaction dbTransaction = null, CancellationToken token = default)
        {
            var updateMap = updateValues as IDictionary<string, object> ?? Obj.GetValues(updateValues);
            var whereMap = whereValues as IDictionary<string, object> ?? Obj.GetValues(whereValues);
            var mergedMap = new Dictionary<string, object>(updateMap);

            foreach (var kv in whereMap)
            {
                mergedMap[kv.Key] = kv.Value;
            }

            var sql = $"UPDATE {this.Options.NamePrefix}{tableName}{this.Options.NameSuffix} SET {string.Join(", ", updateMap.Select(x => $"{this.Options.NamePrefix}{x.Key}{this.Options.NameSuffix} = {this.Options.ParamPrefix}{x.Key}"))}";
            if (whereMap.Count > 0)
            {
                sql += $" WHERE {string.Join(", ", whereMap.Select(x => $"{this.Options.NamePrefix}{x.Key}{this.Options.NameSuffix} = {this.Options.ParamPrefix}{x.Key}"))}";
            }

            return this.ExecuteNonQueryAsync(sql, mergedMap, dbTransaction, token);
        }

        /// <summary>
        /// Обновляет запись в базе данных на основе значений свойств объекта.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="item">Объект, содержащий обновляемые значения.</param>
        /// <param name="tableName">Имя таблицы в которую вставляьб записи.</param>
        /// <param name="dbTransaction">Активная транзакция базы данных.
        /// Если не указана, используется текущая транзакция или соединение.</param>
        /// <param name="token">Токен отмены.</param>
        /// <param name="updateColumns">Список колонок, которые необходимо обновить.
        /// Если не указан, обновляются все сопоставленные свойства,
        /// за исключением первичных ключей.</param>
        /// <returns>Количество строк, затронутых операцией обновления.</returns>
        public Task<int> UpdateAsync<T>(
            T item,
            string tableName,
            IDbTransaction dbTransaction,
            CancellationToken token,
            params Expression<Func<T, object>>[] updateColumns)
            where T : class => this.UpdateAsync(item, tableName, null, dbTransaction, token, updateColumns);

        /// <summary>
        /// Обновляет запись в базе данных на основе значений свойств объекта.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="item">Объект, содержащий обновляемые значения.</param>
        /// <param name="tableName">Имя таблицы в которую вставляьб записи.</param>
        /// <param name="token">Токен отмены.</param>
        /// <param name="updateColumns">Список колонок, которые необходимо обновить.
        /// Если не указан, обновляются все сопоставленные свойства,
        /// за исключением первичных ключей.</param>
        /// <returns>Количество строк, затронутых операцией обновления.</returns>
        public Task<int> UpdateAsync<T>(T item, string tableName, CancellationToken token, params Expression<Func<T, object>>[] updateColumns)
            where T : class => this.UpdateAsync(item, tableName, null, null, token, updateColumns);

        /// <summary>
        /// Обновляет записи в базе данных на основе указанного условия.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="item">Объект, содержащий значения для обновления колонок.</param>
        /// <param name="whereExpression">Лямбда-выражение, определяющее условие <c>WHERE</c>.
        /// Если указано, первичный ключ объекта не используется.</param>
        /// <param name="dbTransaction">Активная транзакция базы данных.
        /// Если не указана, используется текущее соединение или транзакция.</param>
        /// <param name="token">Токен отмены.</param>
        /// <param name="updateColumns">Список колонок, которые необходимо обновить.
        /// Если не указан, обновляются все сопоставленные свойства,
        /// за исключением первичных ключей.</param>
        /// <returns>Количество строк, затронутых операцией обновления.</returns>
        public Task<int> UpdateAsync<T>(
            T item,
            Expression<Func<T, bool>> whereExpression,
            IDbTransaction dbTransaction = null,
            CancellationToken token = default,
            params Expression<Func<T, object>>[] updateColumns)
            where T : class
        {
            return this.UpdateAsync(item, null, whereExpression, dbTransaction, token, updateColumns);
        }

        /// <summary>
        /// Обновляет записи в базе данных на основе указанного условия.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="item">Объект, содержащий значения для обновления колонок.</param>
        /// <param name="tableName">Имя таблицы в которую вставлять записи.</param>
        /// <param name="whereExpression">Лямбда-выражение, определяющее условие <c>WHERE</c>.
        /// Если указано, первичный ключ объекта не используется.</param>
        /// <param name="dbTransaction">Активная транзакция базы данных.
        /// Если не указана, используется текущее соединение или транзакция.</param>
        /// <param name="token">Токен отмены.</param>
        /// <param name="updateColumns">Список колонок, которые необходимо обновить.
        /// Если не указан, обновляются все сопоставленные свойства,
        /// за исключением первичных ключей.</param>
        /// <returns>Количество строк, затронутых операцией обновления.</returns>
        public Task<int> UpdateAsync<T>(
            T item,
            string tableName,
            Expression<Func<T, bool>> whereExpression,
            IDbTransaction dbTransaction = null,
            CancellationToken token = default,
            params Expression<Func<T, object>>[] updateColumns)
            where T : class
        {
            if (!string.IsNullOrWhiteSpace(tableName))
            {
                this.Options.Map.Table<T>(tableName);
            }

            var query = SqlQueryHelper.GetUpdateQuery(this.Options, updateColumns);
            var cmdParams = Obj.GetValues(item);
            query += " " + (whereExpression != null
                ? SqlQueryHelper.GetWhereClause(this.Options, whereExpression, true, out cmdParams)
                : SqlQueryHelper.GetWhereClause<T>(this.Options, out _));

            return this.ExecuteNonQueryAsync(query, cmdParams, dbTransaction, token);
        }

        /// <summary>
        /// Обновляет несколько записей в базе данных в рамках одной транзакции.
        /// </summary>
        /// <typeparam name="T">Тип объекта, который будет обновлен.</typeparam>
        /// <param name="list">Список объектов, содержащих обновленные значения.</param>
        /// <param name="tableName">Имя таблицы в которую вставляьб записи.</param>
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
            string tableName,
            IDbTransaction dbTransaction = null,
            params Expression<Func<T, object>>[] updateColumns)
            where T : class
        {
            if (!string.IsNullOrWhiteSpace(tableName))
            {
                this.Options.Map.Table<T>(tableName);
            }

            return this.UpdateRange(list, dbTransaction, updateColumns);
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
                var count = 0;
                var autoCommit = dbTransaction == null && this.tr.Value == null;
                dbTransaction ??= this.BeginTransaction();
                {
                    var query = SqlQueryHelper.GetUpdateQuery(this.Options, updateColumns);
                    var typeCache = MemberCache.Get(list.FirstOrDefault()?.GetType() ?? typeof(T));
                    var queryParams = new Dictionary<string, object>();
                    using (var cmd = this.CreateCommand(query, dbTransaction))
                    {
                        foreach (var item in list)
                        {
                            typeCache.ToDictionary(item, d);
                            SetParameterCollection(cmd, d);
                            count += cmd.ExecuteNonQuery();
                            this.CommandExecuted?.Invoke(cmd);
                            this.Log(cmd);
                        }
                    }

                    if (autoCommit)
                    {
                        this.CommitTransaction();
                    }
                }

                return count;
            }
            catch (Exception ex)
            {
                this.RollbackTransaction();
                throw this.HandleDbException(ex, null);
            }
        }

        /// <summary>
        /// Асинхронно обновляет несколько записей в базе данных в рамках одной транзакции.
        /// </summary>
        /// <typeparam name="T">Тип объекта, который будет обновлен.</typeparam>
        /// <param name="list">Список объектов, содержащих обновленные значения.</param>
        /// <param name="tableName">Имя таблицы в которую вставляьб записи.</param>
        /// <param name="updateColumns">Массив столбцов для обновления. Если null, обновляются все столбцы объекта.</param>
        /// <param name="dbTransaction">Транзакция, в рамках которой будет выполнено обновление. Если null, создается новая
        /// транзакция.</param>
        /// <param name="token">Токен отмены асинхронной операции. Используется для отмены выполнения запроса.</param>
        /// <returns>Задача, которая возвращает количество обновленных строк в базе данных.</returns>
        /// <exception cref="System.NullReferenceException">dbCmd.</exception>
        /// <remarks>Этот метод асинхронно обновляет несколько записей в базе данных, используя переданный список объектов.
        /// Каждый объект в списке обрабатывается и обновляется в базе данных в рамках одной транзакции.
        /// При возникновении ошибки транзакция откатывается, а исключение обрабатывается и повторно выбрасывается.</remarks>
        public Task<int> UpdateRangeAsync<T>(
            IEnumerable<T> list,
            string tableName,
            Expression<Func<T, object>>[] updateColumns = null,
            IDbTransaction dbTransaction = null,
            CancellationToken token = default)
            where T : class
        {
            this.Options.Map.Table<T>(tableName);
            return this.UpdateRangeAsync(list, updateColumns, dbTransaction, token);
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
                var count = 0;
                var autoCommit = dbTransaction == null && this.tr.Value == null;
                dbTransaction ??= this.BeginTransaction();
                {
                    var query = SqlQueryHelper.GetUpdateQuery(this.Options, updateColumns);
                    var typeCache = MemberCache.Get(list.FirstOrDefault()?.GetType() ?? typeof(T));
                    var queryParams = new Dictionary<string, object>();
                    using (var cmd = this.CreateCommand(query, null, dbTransaction))
                    {
                        if (cmd is not DbCommand dbCmd)
                        {
                            throw new InvalidCastException($"Cannot cast argument '{nameof(cmd)}' to type '{typeof(DbCommand).FullName}'.");
                        }

                        foreach (var item in list)
                        {
                            typeCache.ToDictionary(item, queryParams);
                            SetParameterCollection(cmd, queryParams);
                            count += await dbCmd.ExecuteNonQueryAsync(token).ConfigureAwait(this.ConfigureAwait);
                            this.CommandExecuted?.Invoke(cmd);
                            this.Log(cmd);
                        }
                    }

                    if (autoCommit)
                    {
                        this.CommitTransaction();
                    }
                }

                return count;
            }
            catch (Exception ex)
            {
                this.RollbackTransaction();
                throw this.HandleDbException(ex, null);
            }
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (this.IsDisposed)
            {
                return;
            }

            try
            {
                this.Connection?.Dispose();
                this.Connection = null;
            }
            catch
            {
                // ignore
            }

            this.IsDisposed = true;
        }

        /// <summary>
        /// Begins the connection.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <exception cref="System.ArgumentNullException">connection.</exception>
        /// <exception cref="System.InvalidOperationException">Не удалось открыть соединение с базой данных.</exception>
        private static void BeginConnection(IDbConnection connection)
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
        /// Builds the item factory.
        /// </summary>
        /// <param name="itemTypeCache">The item type cache.</param>
        /// <param name="columnToPropertyMap">The column to property map.</param>
        /// <returns>Func&lt;System.Object[], System.String[], T&gt;.</returns>
        private static Func<object[], string[], T> BuildItemFactory<T>(
            MemberCache itemTypeCache,
            IEnumerable<(string ColumnName, string PropertyName)> columnToPropertyMap)
        {
            var ctor = itemTypeCache.Constructors.FirstOrDefault(x => x.IsPublic) ??
                       itemTypeCache.Constructors.FirstOrDefault();
            var ctorParams = ctor?.GetParameters() ?? Array.Empty<ParameterInfo>();

            if (ctorParams.Length == 0)
            {
                return (values, names) => (T)itemTypeCache.DefaultConstructor();
            }

            return (values, names) =>
            {
                if (ctorParams.Length > values.Length)
                {
                    throw new InvalidOperationException(
                        $"Недостаточно значений для вызова конструктора типа {typeof(T).FullName}.");
                }

                var args = new object[ctorParams.Length];

                var indexes = ctorParams
                    .Select(p =>
                        names.IndexOf(n =>
                            p.Name.Equals(
                                columnToPropertyMap?.FirstOrDefault(m => m.ColumnName == n).PropertyName ?? n,
                                StringComparison.OrdinalIgnoreCase)))
                    .ToArray();

                if (indexes.All(i => i >= 0))
                {
                    for (var i = 0; i < indexes.Length; i++)
                    {
                        args[i] = ChangeType(values[indexes[i]], ctorParams[i].ParameterType);
                    }
                }
                else
                {
                    for (var i = 0; i < ctorParams.Length; i++)
                    {
                        args[i] = ChangeType(values[i], ctorParams[i].ParameterType);
                    }
                }

                return (T)ctor?.Invoke(args);
            };
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
        private static object ChangeType(object value, Type targetType) => TypeHelper.ChangeType(value, targetType);

        /// <summary>
        /// the reader field to property map.
        /// </summary>
        /// <param name="reader">The reader.</param>
        /// <param name="customMap">The custom map.</param>
        /// <param name="onlyFromCustomMap">if set to <c>true</c> [only from custom map].</param>
        /// <returns>Dictionary&lt;System.Int32, System.String&gt;.</returns>
        private static Dictionary<int, string> GetReaderFieldToPropertyMap(
            DbDataReader reader,
            IEnumerable<(string FieldName, string PropertyName)> customMap = null,
            bool onlyFromCustomMap = true)
        {
            Dictionary<string, string> customMapDic = null;

            if (customMap != null)
            {
                customMapDic = customMap as Dictionary<string, string>
                               ?? customMap.ToDictionary(x => x.FieldName, x => x.PropertyName);
            }

            int fieldCount = reader.FieldCount;

            var result = new Dictionary<int, string>(fieldCount);

            bool hasCustomMap = customMapDic?.Count > 0;

            for (int i = 0; i < fieldCount; i++)
            {
                string name = reader.GetName(i);

                if (string.IsNullOrEmpty(name))
                {
                    result[i] = $"Column{i}";
                    continue;
                }

                if (hasCustomMap && customMapDic.TryGetValue(name, out string mapped))
                {
                    result[i] = mapped;

                    if (onlyFromCustomMap)
                    {
                        continue;
                    }
                }

                result[i] = TrimIfNeeded(name);
            }

            return result;
        }

        /// <summary>
        /// Logs the command.
        /// </summary>
        /// <param name="cmd">The command.</param>
        [Conditional("DEBUG")]
        private static void LogCommand(IDbCommand cmd)
        {
            Debug.WriteLine($"Executing SQL: {cmd.CommandText}");
            foreach (IDbDataParameter p in cmd.Parameters)
            {
                Debug.WriteLine($"  {p.ParameterName} = {p.Value}");
            }
        }

        /// <summary>
        /// Replaces the parameter token.
        /// </summary>
        /// <param name="sql">The SQL.</param>
        /// <param name="token">The token.</param>
        /// <param name="replacement">The replacement.</param>
        /// <returns>System.String.</returns>
        private static string ReplaceParameterToken(string sql, string token, string replacement) =>
            Regex.Replace(
                sql,
                $@"(?<==\s*){Regex.Escape(token)}(?!\w)",
                replacement,
                RegexOptions.CultureInvariant);

        private static string TrimIfNeeded(string value)
        {
            if (value.Length == 0)
            {
                return value;
            }

            return Array.IndexOf(StringHelper.SpecialChars, value[0]) >= 0
                ? value.TrimStart(StringHelper.SpecialChars)
                : value;
        }

        /// <summary>
        /// Begins the connection.
        /// </summary>
        private void BeginConnection() => BeginConnection(this.Connection);

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
            try
            {
                if (con == null)
                {
                    throw new ArgumentNullException(nameof(con));
                }

                if (con.State != ConnectionState.Closed)
                {
                    con.Close();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }

        /// <summary>
        /// the reader field to property map.
        /// </summary>
        /// <param name="itemType">Type of the item.</param>
        /// <param name="reader">The reader.</param>
        /// <param name="customMap">The custom map.</param>
        /// <param name="columns">The columns.</param>
        /// <returns>Dictionary&lt;System.Int32, MemberCache&gt;.</returns>
        private Dictionary<int, MemberCache> GetReaderFieldToPropertyMap(
            Type itemType,
            DbDataReader reader,
            IEnumerable<(string FieldName, string PropertyName)> customMap = null,
            IEnumerable<string> columns = null)
        {
            customMap ??= this.Options.Map?.GetColumnToPropertyMap(itemType);

            var customMapDic =
                customMap?.ToDictionary(k => k.FieldName, v => v.PropertyName) ?? [];
            var map = new Dictionary<int, MemberCache>();
            var typeInfoEx = MemberCache.Get(itemType);
            var columnsCount = reader.FieldCount;

            for (var i = 0; i < columnsCount; i++)
            {
                var colIndex = i;
                var colName = reader.GetName(i);
                MemberCache propInfoEx;
                if (customMap != null)
                {
                    propInfoEx = typeInfoEx.PublicBasicProperties.FirstOrDefault(x =>
                        x.Name.Equals(customMapDic.GetValueOrDefault(colName), StringComparison.OrdinalIgnoreCase));
                    if (propInfoEx != null)
                    {
                        map[colIndex] = propInfoEx;
                        continue;
                    }
                }

                propInfoEx = typeInfoEx.ColumnProperties
                    .FirstOrDefault(x => StringComparer.OrdinalIgnoreCase.Equals(x.ColumnName, colName));
                if (propInfoEx != null)
                {
                    map[colIndex] = propInfoEx;
                    continue;
                }

                propInfoEx = typeInfoEx.PublicBasicProperties
                    .FirstOrDefault(x => StringComparer.OrdinalIgnoreCase.Equals(x.ColumnName, colName));

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

            var itemsToRemove =
                map.Where(kv => !columns.Contains(kv.Value.ColumnName)).Select(kv => kv.Key).ToList();
            foreach (var item in itemsToRemove)
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
            var errorMessage = $"Ошибка в методе {methodName}.\r\n" +
                               $"Запрос: {cmd?.CommandText}.\r\n" +
                               $"Параметры: {string.Join(", ", cmd == null ? Array.Empty<string>() : cmd.Parameters.Cast<IDbDataParameter>().Select(p => $"{p.ParameterName}={p.Value}"))}\r\n" +
                               $"{ex}";

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

            var rawSql = this.GetRawSql(cmd);
            this.LastQuery = rawSql;
            this.Log(rawSql);
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

            var now = DateTimeHelper.ExactNow();
            this.queryLogs.Add($"{now:yyyy-MM-dd HH:mm:ss.ffff}" + ":   " + message);
        }

        private async Task ReadToListInternalAsync<T>(
            IList list,
            DbDataReader reader,
            IEnumerable<string> columns,
            IEnumerable<(string ColumnName, string PropertyName)> columnToPropertyMap,
            DbValueConverter<T> converter,
            int fetchRows,
            Func<object[], string[], T> itemFactory,
            CancellationToken ct)
        {
            var itemTypeCache = MemberCache.Get(list.FirstOrDefault()?.GetType() ?? typeof(T));
            var readerValues = new object[reader.FieldCount];
            var readerColumns = Enumerable.Range(0, reader.FieldCount)
                .Select(reader.GetName)
                .ToArray();

            StringPool stringPool = new();
            var rowCount = 0;

            if (itemTypeCache.IsBasic)
            {
                var colIndex = columns?.Select(reader.GetOrdinal).FirstOrDefault() ?? 0;

                while (await reader.ReadAsync(ct).ConfigureAwait(this.ConfigureAwait))
                {
                    if (fetchRows > 0 && rowCount >= fetchRows)
                    {
                        break;
                    }

                    var value = reader.GetValue(colIndex);
                    if (value == DBNull.Value)
                    {
                        value = null;
                    }

                    if (this.EnableStringPool && value is string s)
                    {
                        value = stringPool.Intern(s);
                    }

                    list.Add(converter == null ? value : converter(readerColumns[0], value, null, (T)value));
                    rowCount++;
                }

                return;
            }

            var map = this.GetReaderFieldToPropertyMap(
                list.FirstOrDefault()?.GetType() ?? typeof(T),
                reader,
                columnToPropertyMap,
                columns);
            var valueConverter = converter ?? this.ValueConverter.ToTypedConverter<T>();

            while (await reader.ReadAsync(ct).ConfigureAwait(this.ConfigureAwait))
            {
                try
                {
                    reader.GetValues(readerValues);
                }
                catch
                {
                    for (var i = 0; i < reader.FieldCount; i++)
                    {
                        try
                        {
                            readerValues[i] = reader.GetValue(i);
                        }
                        catch (Exception ex)
                        {
                            var fieldName = reader.GetName(i);
                            var dataType = reader.GetFieldType(i);
                            throw new InvalidOperationException(
                                $"Не удалось получить значение поля '{fieldName}' типа '{dataType?.FullName}'.",
                                ex);
                        }
                    }
                }

                var item = itemFactory(readerValues, readerColumns);

                if (!itemTypeCache.IsValueType)
                {
                    foreach (var kv in map)
                    {
                        var raw = readerValues[kv.Key];

                        if (raw == null || raw == DBNull.Value)
                        {
                            if (kv.Value.IsNullable)
                            {
                                kv.Value.Setter(item, null);
                            }

                            continue;
                        }

                        var value = valueConverter(readerColumns[kv.Key], raw, kv.Value, item);
                        if (this.EnableStringPool && value is string s)
                        {
                            value = stringPool.Intern(s);
                        }

                        kv.Value.Setter(item, value);
                    }
                }

                list.Add(item);
                rowCount++;
            }
        }

        private async Task ReadToListInternalAsync2<T>(
    IList list,
    DbDataReader reader,
    IEnumerable<string> columns,
    IEnumerable<(string ColumnName, string PropertyName)> columnToPropertyMap,
    DbValueConverter<T> converter,
    int fetchRows,
    Func<object[], string[], T> itemFactory,
    CancellationToken ct)
        {
            var type = list.FirstOrDefault()?.GetType() ?? typeof(T);
            var itemTypeCache = MemberCache.Get(type);
            var fieldCount = reader.FieldCount;
            var readerValues = new object[fieldCount];
            var readerColumns = new string[fieldCount];
            for (int i = 0; i < fieldCount; i++)
            {
                readerColumns[i] = reader.GetName(i);
            }

            var rowCount = 0;
            StringPool stringPool = this.EnableStringPool ? new StringPool() : null;

            if (itemTypeCache.IsBasic)
            {
                var colIndex = columns?.Select(reader.GetOrdinal).FirstOrDefault() ?? 0;

                var conv = converter;

                while (await reader.ReadAsync(ct).ConfigureAwait(this.ConfigureAwait))
                {
                    if (fetchRows > 0 && rowCount >= fetchRows)
                    {
                        break;
                    }

                    object value = reader.GetValue(colIndex);

                    if (value == DBNull.Value)
                    {
                        value = null;
                    }

                    if (stringPool != null && value is string s)
                    {
                        value = stringPool.Intern(s);
                    }

                    list.Add(conv == null
                        ? value
                        : conv(readerColumns[0], value, null, (T)value));

                    rowCount++;
                }

                return;
            }

            var map = this.GetReaderFieldToPropertyMap(
                type,
                reader,
                columnToPropertyMap,
                columns);

            var valueConverter = converter ?? this.ValueConverter.ToTypedConverter<T>();

            var localPool = stringPool;
            var usePool = localPool != null;

            while (await reader.ReadAsync(ct).ConfigureAwait(this.ConfigureAwait))
            {
                try
                {
                    reader.GetValues(readerValues);
                }
                catch
                {
                    for (int i = 0; i < fieldCount; i++)
                    {
                        try
                        {
                            readerValues[i] = reader.GetValue(i);
                        }
                        catch (Exception ex)
                        {
                            throw new InvalidOperationException(
                                $"Не удалось получить поле '{readerColumns[i]}' типа '{reader.GetFieldType(i)?.FullName}'.",
                                ex);
                        }
                    }
                }

                var item = itemFactory(readerValues, readerColumns);

                if (!itemTypeCache.IsValueType)
                {
                    foreach (var kv in map)
                    {
                        var index = kv.Key;
                        var meta = kv.Value;

                        var raw = readerValues[index];

                        if (raw == null || raw == DBNull.Value)
                        {
                            if (meta.IsNullable)
                            {
                                meta.Setter(item, null);
                            }

                            continue;
                        }

                        var value = valueConverter(readerColumns[index], raw, meta, item);

                        if (usePool && value is string s)
                        {
                            value = localPool.Intern(s);
                        }

                        meta.Setter(item, value);
                    }
                }

                list.Add(item);
                rowCount++;
            }
        }

        private DataTable[] ToDataTablesInternal(string query, object cmdParams = null, Func<string, object, DataColumn, object> valueConverter = null, params (string, string)[] columnMap)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                throw new ArgumentNullException(nameof(query));
            }

            valueConverter ??= (f, v, c) => v;

            var result = new List<DataTable>();

            StringPool stringPool = this.EnableStringPool ? new StringPool() : null;
            var usePool = stringPool != null;

            using var cmd = this.CreateCommand(query, cmdParams, null);

            try
            {
                this.BeginConnection();

                using var r = cmd.ExecuteReader();

                do
                {
                    this.CommandExecuted?.Invoke(cmd);
                    this.Log(cmd);

                    var map = GetReaderFieldToPropertyMap(r, columnMap);
                    var fieldCount = map.Count;

                    var table = new DataTable(query);
                    table.BeginLoadData();

                    // ----------------------------
                    // cache column metadata arrays
                    // ----------------------------
                    var colNames = new string[fieldCount];
                    var colTypes = new Type[fieldCount];
                    var dataColumns = new DataColumn[fieldCount];

                    int i = 0;
                    foreach (var kv in map)
                    {
                        var col = new DataColumn(
                            kv.Value,
                            r.GetFieldType(kv.Key) ?? typeof(object));

                        table.Columns.Add(col);

                        colNames[i] = kv.Value;
                        colTypes[i] = col.DataType;
                        dataColumns[i] = col;
                        i++;
                    }

                    // ----------------------------
                    // materialization loop
                    // ----------------------------
                    while (r.Read())
                    {
                        var row = table.NewRow();

                        for (int j = 0; j < fieldCount; j++)
                        {
                            var raw = r.GetValue(j);

                            if (raw == DBNull.Value)
                            {
                                continue;
                            }

                            if (usePool && raw is string s && this.PooledStringColumns.Contains(colNames[j]))
                            {
                                raw = stringPool.Intern(s);
                            }

                            row[colNames[j]] =
                                valueConverter(colNames[j], raw, dataColumns[j]);
                        }

                        table.Rows.Add(row);
                    }

                    table.AcceptChanges();
                    table.EndLoadData();

                    result.Add(table);
                }
                while (r.NextResult());

                return result.ToArray();
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
}