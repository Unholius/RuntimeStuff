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

namespace RuntimeStuff
{
    /// <summary>
    /// Универсальный клиент доступа к базе данных, типизированный по конкретному
    /// типу соединения (<typeparamref name="T"/>).
    /// </summary>
    /// <typeparam name="T">
    /// Тип соединения с базой данных, реализующий <see cref="IDbConnection"/>
    /// и имеющий конструктор без параметров.
    /// </typeparam>
    public class DbClient<T> : DbClient where T : IDbConnection, new()
    {
        private static readonly Cache<IDbConnection, DbClient<T>> ClientCache =
            new Cache<IDbConnection, DbClient<T>>(con => new DbClient<T>((T)con));

        /// <summary>
        /// Создаёт новый экземпляр клиента с автоматически созданным соединением.
        /// </summary>
        public DbClient() : base(new T())
        {
        }

        /// <summary>
        /// Создаёт новый экземпляр клиента на основе переданного соединения.
        /// </summary>
        /// <param name="con">Открытое или закрытое соединение с БД.</param>
        public DbClient(T con) : base(con)
        {
        }

        /// <summary>
        /// Создаёт новый экземпляр клиента и инициализирует строку подключения.
        /// </summary>
        /// <param name="connectionString">Строка подключения к базе данных.</param>
        public DbClient(string connectionString)
        {
            Connection = new T
            {
                ConnectionString = connectionString
            };
        }

        /// <summary>
        /// Типизированное соединение с базой данных.
        /// </summary>
        public new T Connection
        {
            get => (T)base.Connection;
            set => base.Connection = value;
        }

        /// <summary>
        /// Получает или создаёт кэшированный экземпляр клиента по строке подключения.
        /// </summary>
        /// <param name="connectionString">Строка подключения.</param>
        /// <returns>Экземпляр <see cref="DbClient{T}"/>.</returns>
        public static DbClient<T> Create(string connectionString)
        {
            var con = new T { ConnectionString = connectionString };
            var dbClient = ClientCache.Get(con);
            return dbClient;
        }

        /// <summary>
        /// Получает или создаёт кэшированный экземпляр клиента по соединению.
        /// </summary>
        /// <param name="con">Соединение с базой данных.</param>
        /// <returns>Экземпляр <see cref="DbClient{T}"/>.</returns>
        public static DbClient<T> Create(T con)
        {
            var dbClient = ClientCache.Get(con);
            return dbClient;
        }
    }

    /// <summary>
    /// Универсальный клиент доступа к базе данных с поддержкой CRUD-операций,
    /// транзакций, агрегаций и асинхронного выполнения команд.
    /// </summary>
    /// <remarks>
    /// Предназначен для использования как легковесная альтернатива ORM.
    /// </remarks>
    public class DbClient : IDisposable, IHaveOptions<SqlProviderOptions>
    {
        public static char[] TrimChars = new char[] { '\uFEFF', '\u200B', ' ', '\r', '\n', '\t' };

        /// <summary>
        /// Делегат для преобразования значения из БД в значение свойства объекта.
        /// </summary>
        /// <param name="fieldName">Имя поля в результирующем наборе.</param>
        /// <param name="fieldValue">Сырое значение из БД.</param>
        /// <param name="propertyInfo">Информация о свойстве назначения.</param>
        /// <param name="item">Экземпляр объекта.</param>
        /// <returns>Преобразованное значение.</returns>
        public delegate object DbValueConverter(string fieldName, object fieldValue, PropertyInfo propertyInfo,
            object item);

        /// <summary>
        /// Типизированная версия делегата преобразования значений.
        /// </summary>
        public delegate object DbValueConverter<in T>(string fieldName, object fieldValue, PropertyInfo propertyInfo, T item);

        public static DbValueConverter TrimStringSpaces = (name, value, info, item) => value is string s ? s.Trim(TrimChars) : ChangeType(value, info.PropertyType);

        private static readonly StringComparer IgnoreCaseComparer = StringComparer.OrdinalIgnoreCase;

        private static readonly Cache<IDbConnection, DbClient> ClientCache = new Cache<IDbConnection, DbClient>(con => new DbClient(con));

        private readonly AsyncLocal<IDbTransaction> _tr = new AsyncLocal<IDbTransaction>();

        public DbClient()
        {
            ValueConverter = (fieldName, fieldValue, propInfo, item) =>
                ChangeType(fieldValue is string s ? s.Trim() : fieldValue, propInfo.PropertyType);
        }

        public DbClient(IDbConnection con) : this()
        {
            Connection = con ?? throw new ArgumentNullException(nameof(con));
        }

        /// <summary>
        /// Активное соединение с базой данных.
        /// </summary>
        public IDbConnection Connection { get; set; }

        /// <summary>
        /// Параметры SQL-провайдера (кавычки, префиксы параметров, синтаксис LIMIT/OFFSET и т.п.).
        /// </summary>
        public SqlProviderOptions Options { get; set; } = new SqlProviderOptions();

        /// <summary>
        /// Функция преобразования значений, полученных из БД, в значения свойств объектов.
        /// </summary>
        public DbValueConverter<object> ValueConverter { get; set; }

        /// <summary>
        /// Определяет, использовать ли ConfigureAwait(false) для асинхронных операций.
        /// </summary>
        public bool ConfigureAwait { get; set; } = false;

        /// <summary>
        /// Таймаут выполнения SQL-команд по умолчанию (в секундах).
        /// </summary>
        public int DefaultCommandTimeout { get; set; } = 30;

        /// <summary>
        /// Признак того, что экземпляр <see cref="DbClient"/> был освобождён.
        /// </summary>
        /// <remarks>
        /// Устанавливается в <c>true</c> после вызова метода <see cref="Dispose()"/>.
        /// </remarks>
        public bool IsDisposed { get; private set; }

        /// <summary>
        /// Базовые опции клиента базы данных.
        /// </summary>
        /// <remarks>
        /// Реализация интерфейса <see cref="IHaveOptions"/>. 
        /// Фактически использует <see cref="SqlProviderOptions"/>.
        /// </remarks>
        OptionsBase IHaveOptions.Options
        {
            get => Options;
            set => Options = (SqlProviderOptions)value;
        }

        /// <summary>
        /// Освобождает все ресурсы, используемые текущим экземпляром <see cref="DbClient"/>.
        /// </summary>
        /// <remarks>
        /// Вызывает защищённый метод <see cref="Dispose(bool)"/> и подавляет финализацию объекта.
        /// </remarks>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Событие, возникающее после успешного выполнения SQL-команды.
        /// </summary>
        /// <remarks>
        /// Вызывается после выполнения команды, но до закрытия соединения.
        /// </remarks>
        public event Action<IDbCommand> CommandExecuted;

        /// <summary>
        /// Событие, возникающее при ошибке выполнения SQL-команды.
        /// </summary>
        /// <remarks>
        /// Позволяет перехватывать исключения и анализировать команду, вызвавшую ошибку.
        /// </remarks>
        public event Action<IDbCommand, Exception> CommandFailed;

        /// <summary>
        /// Финализатор класса <see cref="DbClient"/>.
        /// </summary>
        /// <remarks>
        /// Вызывается сборщиком мусора, если объект не был явно освобождён.
        /// </remarks>
        ~DbClient()
        {
            Dispose(false);
        }

        /// <summary>
        /// Создаёт новый экземпляр <see cref="DbClient{T}"/> по строке подключения.
        /// </summary>
        /// <typeparam name="T">
        /// Тип соединения с базой данных, реализующий <see cref="IDbConnection"/> 
        /// и имеющий публичный конструктор без параметров.
        /// </typeparam>
        /// <param name="connectionString">Строка подключения к базе данных.</param>
        /// <returns>Экземпляр <see cref="DbClient{T}"/>.</returns>
        public static DbClient<T> Create<T>(string connectionString)
            where T : IDbConnection, new()
        {
            var dbClient = DbClient<T>.Create(connectionString);
            return dbClient;
        }

        /// <summary>
        /// Создаёт или возвращает кэшированный экземпляр <see cref="DbClient"/>
        /// для указанного соединения.
        /// </summary>
        /// <param name="connection">Соединение с базой данных.</param>
        /// <returns>Экземпляр <see cref="DbClient"/>.</returns>
        public static DbClient Create(IDbConnection connection)
        {
            var dbClient = ClientCache.Get(connection);
            return dbClient;
        }

        /// <summary>
        /// Создаёт новый экземпляр <see cref="DbClient"/> без привязанного соединения.
        /// </summary>
        /// <returns>Новый экземпляр <see cref="DbClient"/>.</returns>
        public static DbClient Create()
        {
            return new DbClient();
        }

        #region Insert

        /// <summary>
        /// Создаёт новый экземпляр сущности, инициализирует его и вставляет в базу данных.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="dbTransaction">
        /// Активная транзакция базы данных. 
        /// Если не указана, используется текущая или создаётся новая.
        /// </param>
        /// <param name="insertColumns">
        /// Делегаты инициализации свойств сущности перед вставкой.
        /// </param>
        /// <returns>Созданный и сохранённый объект.</returns>
        public T Insert<T>(IDbTransaction dbTransaction = null, params Action<T>[] insertColumns) where T : class
        {
            var item = Obj.New<T>();
            foreach (var a in insertColumns)
                a(item);
            Insert(item, dbTransaction: dbTransaction);
            return item;
        }

        /// <summary>
        /// Вставляет объект в базу данных.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="item">Объект для вставки.</param>
        /// <param name="dbTransaction">Транзакция базы данных.</param>
        /// <param name="insertColumns">
        /// Список колонок, участвующих во вставке.
        /// Если не указан, используются все сопоставленные свойства.
        /// </param>
        /// <returns>
        /// Значение сгенерированного первичного ключа,
        /// либо <c>null</c>, если провайдер не поддерживает его получение.
        /// </returns>
        public object Insert<T>(T item, IDbTransaction dbTransaction = null, params Expression<Func<T, object>>[] insertColumns) where T : class
        {
            object id = null;
            var query = SqlQueryBuilder.GetInsertQuery(Options, insertColumns);
            if (string.IsNullOrWhiteSpace(Options.GetInsertedIdQuery))
            {
                ExecuteNonQuery(query, GetParams(item), dbTransaction);
            }
            else
            {
                query += $"{Options.StatementTerminator} {Options.GetInsertedIdQuery}";
                id = ExecuteScalar<object>(query, GetParams(item));
                var mi = MemberCache<T>.Create();
                if (id != null && id != DBNull.Value && mi.PrimaryKeys.Count == 1)
                {
                    var pi = mi.PrimaryKeys.First().Value;
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
        /// <returns>
        /// Значение первичного ключа, либо <c>null</c>.
        /// </returns>
        public Task<object> InsertAsync<T>(Action<T>[] insertColumns = null, IDbTransaction dbTransaction = null, CancellationToken token = default) where T : class
        {
            var item = Obj.New<T>();
            if (insertColumns == null) return InsertAsync(item, null, dbTransaction, token);
            foreach (var a in insertColumns)
                a(item);
            return InsertAsync(item, null, dbTransaction, token);
        }

        /// <summary>
        /// Асинхронно вставляет объект в базу данных.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="item">Объект для вставки.</param>
        /// <param name="insertColumns">
        /// Список колонок, участвующих во вставке.
        /// </param>
        /// <param name="dbTransaction">Транзакция базы данных.</param>
        /// <param name="token">Токен отмены операции.</param>
        /// <returns>
        /// Значение сгенерированного первичного ключа,
        /// либо <c>null</c>.
        /// </returns>
        public async Task<object> InsertAsync<T>(T item, Expression<Func<T, object>>[] insertColumns = null, IDbTransaction dbTransaction = null, CancellationToken token = default) where T : class
        {
            object id = null;
            var query = SqlQueryBuilder.GetInsertQuery(Options, insertColumns);
            if (string.IsNullOrWhiteSpace(Options.GetInsertedIdQuery))
            {
                await ExecuteNonQueryAsync(query, GetParams(item), dbTransaction, token).ConfigureAwait(ConfigureAwait);
            }
            else
            {
                query += $"{Options.StatementTerminator} {Options.GetInsertedIdQuery}";
                id = await ExecuteScalarAsync<object>(query, GetParams(item), dbTransaction, token).ConfigureAwait(ConfigureAwait);
                var mi = MemberCache<T>.Create();
                if (id != null && id != DBNull.Value && mi.PrimaryKeys.Count == 1)
                    mi.PrimaryKeys.First().Value.SetValue(item,
                        ChangeType(id, mi.PrimaryKeys.First().Value.PropertyType));
            }

            return id;
        }

        /// <summary>
        /// Вставляет коллекцию объектов в базу данных в рамках одной транзакции.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="list">Коллекция объектов для вставки.</param>
        /// <param name="dbTransaction">
        /// Внешняя транзакция. Если не указана — создаётся новая.
        /// </param>
        /// <param name="insertColumns">Колонки, участвующие во вставке.</param>
        /// <returns>Количество вставленных записей.</returns>
        public int InsertRange<T>(IEnumerable<T> list, IDbTransaction dbTransaction = null, params Expression<Func<T, object>>[] insertColumns) where T : class
        {
            try
            {
                var count = 0;
                using (dbTransaction ?? BeginTransaction())
                {
                    var query = SqlQueryBuilder.GetInsertQuery(Options, insertColumns);
                    if (!string.IsNullOrWhiteSpace(Options.GetInsertedIdQuery))
                        query += $"{Options.StatementTerminator} {Options.GetInsertedIdQuery}";
                    var typeCache = MemberCache<T>.Create();
                    var pk = typeCache.PrimaryKeys.FirstOrDefault().Value;
                    var queryParams = new Dictionary<string, object>();
                    using (var cmd = CreateCommand(query, dbTransaction))
                    {
                        foreach (var item in list)
                        {
                            typeCache.ToDictionary(item, queryParams);
                            SetParameterCollection(cmd, queryParams);
                            var id = cmd.ExecuteScalar();
                            CommandExecuted?.Invoke(cmd);
                            if (pk != null && id != null) pk.SetValue(item, ChangeType(id, pk.PropertyType));

                            count++;
                        }
                    }

                    EndTransaction();
                }

                return count;
            }
            catch (Exception ex)
            {
                RollbackTransaction();
                throw HandleDbException(ex, null);
            }
            finally
            {
                CloseConnection();
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
        public async Task<int> InsertRangeAsync<T>(IEnumerable<T> list, Expression<Func<T, object>>[] insertColumns = null, IDbTransaction dbTransaction = null,
            CancellationToken token = default) where T : class
        {
            try
            {
                var count = 0;
                using (dbTransaction ?? BeginTransaction())
                {
                    var query = SqlQueryBuilder.GetInsertQuery(Options, insertColumns);
                    if (!string.IsNullOrWhiteSpace(Options.GetInsertedIdQuery))
                        query += $"{Options.StatementTerminator} {Options.GetInsertedIdQuery}";
                    var typeCache = MemberCache<T>.Create();
                    var pk = typeCache.PrimaryKeys.FirstOrDefault().Value;
                    var queryParams = new Dictionary<string, object>();
                    using (var cmd = CreateCommand(query, dbTransaction))
                    {
                        if (!(cmd is DbCommand dbCmd))
                            throw new NullReferenceException(nameof(dbCmd));
                        foreach (var item in list)
                        {
                            typeCache.ToDictionary(item, queryParams);
                            SetParameterCollection(cmd, queryParams);

                            var id = await dbCmd.ExecuteScalarAsync(token).ConfigureAwait(ConfigureAwait);
                            CommandExecuted?.Invoke(cmd);
                            if (pk != null && id != null) pk.SetValue(item, ChangeType(id, pk.PropertyType));

                            count++;
                        }
                    }

                    EndTransaction();
                }

                return count;
            }
            catch (Exception ex)
            {
                RollbackTransaction();
                throw HandleDbException(ex, null);
            }
            finally
            {
                CloseConnection();
            }
        }

        #endregion Insert

        #region Update

        /// <summary>
        /// Обновляет запись в базе данных на основе значений свойств объекта.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="item">Объект, содержащий обновляемые значения.</param>
        /// <param name="dbTransaction">
        /// Активная транзакция базы данных.
        /// Если не указана, используется текущая транзакция или соединение.
        /// </param>
        /// <param name="updateColumns">
        /// Список колонок, которые необходимо обновить.
        /// Если не указан, обновляются все сопоставленные свойства,
        /// за исключением первичных ключей.
        /// </param>
        /// <returns>
        /// Количество строк, затронутых операцией обновления.
        /// </returns>
        public int Update<T>(T item, IDbTransaction dbTransaction = null, params Expression<Func<T, object>>[] updateColumns) where T : class
        {
            return Update(item, null, dbTransaction, updateColumns);
        }

        /// <summary>
        /// Обновляет записи в базе данных на основе указанного условия.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="item">
        /// Объект, содержащий значения для обновления колонок.
        /// </param>
        /// <param name="whereExpression">
        /// Лямбда-выражение, определяющее условие <c>WHERE</c>.
        /// Если указано, первичный ключ объекта не используется.
        /// </param>
        /// <param name="dbTransaction">
        /// Активная транзакция базы данных.
        /// Если не указана, используется текущее соединение или транзакция.
        /// </param>
        /// <param name="updateColumns">
        /// Список колонок, которые необходимо обновить.
        /// Если не указан, обновляются все сопоставленные свойства,
        /// за исключением первичных ключей.
        /// </param>
        /// <returns>
        /// Количество строк, затронутых операцией обновления.
        /// </returns>
        public int Update<T>(T item, Expression<Func<T, bool>> whereExpression, IDbTransaction dbTransaction = null, params Expression<Func<T, object>>[] updateColumns) where T : class
        {
            var query = SqlQueryBuilder.GetUpdateQuery(Options, updateColumns);
            var cmdParams = GetParams(item);
            query += " " + (whereExpression != null
                ? SqlQueryBuilder.GetWhereClause(whereExpression, Options, true, out cmdParams)
                : SqlQueryBuilder.GetWhereClause<T>(Options, out _));
                
            return ExecuteNonQuery(query, cmdParams, dbTransaction);
        }

        /// <summary>
        /// Асинхронно обновляет запись в базе данных на основе значений свойств объекта.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="item">
        /// Объект, содержащий обновляемые значения.
        /// </param>
        /// <param name="updateColumns">
        /// Список колонок, которые необходимо обновить.
        /// Если не указан, обновляются все сопоставленные свойства,
        /// за исключением первичных ключей.
        /// </param>
        /// <param name="dbTransaction">
        /// Активная транзакция базы данных.
        /// Если не указана, используется текущее соединение или транзакция.
        /// </param>
        /// <param name="token">
        /// Токен отмены асинхронной операции.
        /// </param>
        /// <returns>
        /// Задача, результатом которой является количество строк,
        /// затронутых операцией обновления.
        /// </returns>
        public Task<int> UpdateAsync<T>(T item, Expression<Func<T, object>>[] updateColumns = null, IDbTransaction dbTransaction = null, CancellationToken token = default) where T : class
        {
            return UpdateAsync(item, null, updateColumns ?? Array.Empty<Expression<Func<T, object>>>(), dbTransaction,
                token);
        }

        /// <summary>
        /// Асинхронно обновляет записи в базе данных на основе переданных данных.
        /// </summary>
        /// <typeparam name="T">Тип объекта, который будет обновлен.</typeparam>
        /// <param name="item">Объект, содержащий обновленные значения.</param>
        /// <param name="whereExpression">Условие для фильтрации записей для обновления. Может быть null, если условие не требуется.</param>
        /// <param name="updateColumns">Массив столбцов для обновления. Если null, обновляются все столбцы объекта.</param>
        /// <param name="dbTransaction">Транзакция, в рамках которой будет выполнено обновление. Может быть null.</param>
        /// <param name="token">Токен для отмены операции, может быть использован для отмены выполнения операции.</param>
        /// <returns>Задача, которая возвращает количество затронутых строк в базе данных.</returns>
        /// <remarks>
        /// Этот метод создает SQL-запрос для обновления, используя параметры, переданные в аргументах метода. 
        /// Если условие whereExpression не задано, обновляются все записи в таблице. Метод возвращает количество строк, которые были обновлены.
        /// </remarks>
        public Task<int> UpdateAsync<T>(T item, Expression<Func<T, bool>> whereExpression, Expression<Func<T, object>>[] updateColumns = null, IDbTransaction dbTransaction = null, CancellationToken token = default) where T : class
        {
            var cmdParams = GetParams(item);
            var query = SqlQueryBuilder.GetUpdateQuery(Options, updateColumns);
            query += " " + (whereExpression != null
                ? SqlQueryBuilder.GetWhereClause(whereExpression, Options, true, out cmdParams)
                : SqlQueryBuilder.GetWhereClause<T>(Options, out _));

            return ExecuteNonQueryAsync(query, cmdParams, dbTransaction, token);
        }

        /// <summary>
        /// Обновляет несколько записей в базе данных в рамках одной транзакции.
        /// </summary>
        /// <typeparam name="T">Тип объекта, который будет обновлен.</typeparam>
        /// <param name="list">Список объектов, содержащих обновленные значения.</param>
        /// <param name="dbTransaction">Транзакция, в рамках которой будет выполнено обновление. Если null, создается новая транзакция.</param>
        /// <param name="updateColumns">Массив столбцов для обновления. Если null, обновляются все столбцы объекта.</param>
        /// <returns>Количество обновленных строк в базе данных.</returns>
        /// <exception cref="Exception">Вызывается в случае ошибки при выполнении операции обновления.</exception>
        /// <remarks>
        /// Этот метод обновляет несколько записей в базе данных, используя переданный список объектов. 
        /// Каждый объект в списке обрабатывается и обновляется в базе данных в рамках одной транзакции.
        /// При возникновении ошибки транзакция откатывается, а исключение обрабатывается и повторно выбрасывается.
        /// </remarks>
        public int UpdateRange<T>(IEnumerable<T> list, IDbTransaction dbTransaction = null, params Expression<Func<T, object>>[] updateColumns) where T : class
        {
            try
            {
                var count = 0;
                using (dbTransaction ?? BeginTransaction())
                {
                    var query = SqlQueryBuilder.GetUpdateQuery(Options, updateColumns);
                    var typeCache = MemberCache<T>.Create();
                    var queryParams = new Dictionary<string, object>();
                    using (var cmd = CreateCommand(query, dbTransaction))
                    {
                        foreach (var item in list)
                        {
                            typeCache.ToDictionary(item, queryParams);
                            SetParameterCollection(cmd, queryParams);
                            count += cmd.ExecuteNonQuery();
                            CommandExecuted?.Invoke(cmd);
                        }
                    }

                    EndTransaction();
                }

                return count;
            }
            catch (Exception ex)
            {
                RollbackTransaction();
                throw HandleDbException(ex, null);
            }
            finally
            {
                CloseConnection();
            }
        }

        /// <summary>
        /// Асинхронно обновляет несколько записей в базе данных в рамках одной транзакции.
        /// </summary>
        /// <typeparam name="T">Тип объекта, который будет обновлен.</typeparam>
        /// <param name="list">Список объектов, содержащих обновленные значения.</param>
        /// <param name="updateColumns">Массив столбцов для обновления. Если null, обновляются все столбцы объекта.</param>
        /// <param name="dbTransaction">Транзакция, в рамках которой будет выполнено обновление. Если null, создается новая транзакция.</param>
        /// <param name="token">Токен отмены асинхронной операции. Используется для отмены выполнения запроса.</param>
        /// <returns>Задача, которая возвращает количество обновленных строк в базе данных.</returns>
        /// <exception cref="Exception">Вызывается в случае ошибки при выполнении операции обновления.</exception>
        /// <remarks>
        /// Этот метод асинхронно обновляет несколько записей в базе данных, используя переданный список объектов. 
        /// Каждый объект в списке обрабатывается и обновляется в базе данных в рамках одной транзакции.
        /// При возникновении ошибки транзакция откатывается, а исключение обрабатывается и повторно выбрасывается.
        /// </remarks>
        public async Task<int> UpdateRangeAsync<T>(IEnumerable<T> list, Expression<Func<T, object>>[] updateColumns = null, IDbTransaction dbTransaction = null, CancellationToken token = default) where T : class
        {
            try
            {
                var count = 0;
                using (dbTransaction ?? BeginTransaction())
                {
                    var query = SqlQueryBuilder.GetUpdateQuery(Options, updateColumns);
                    var typeCache = MemberCache<T>.Create();
                    var queryParams = new Dictionary<string, object>();
                    using (var cmd = CreateCommand(query, dbTransaction))
                    {
                        if (!(cmd is DbCommand dbCmd))
                            throw new NullReferenceException(nameof(dbCmd));

                        foreach (var item in list)
                        {
                            typeCache.ToDictionary(item, queryParams);
                            SetParameterCollection(cmd, queryParams);
                            count += await dbCmd.ExecuteNonQueryAsync(token).ConfigureAwait(ConfigureAwait);
                            CommandExecuted?.Invoke(cmd);
                        }
                    }

                    EndTransaction();
                }

                return count;
            }
            catch (Exception ex)
            {
                RollbackTransaction();
                throw HandleDbException(ex, null);
            }
            finally
            {
                CloseConnection();
            }
        }

        #endregion Update

        #region Delete

        /// <summary>
        /// Удаляет записи из базы данных, соответствующие заданному условию.
        /// </summary>
        /// <typeparam name="T">Тип сущности, из таблицы которой выполняется удаление.</typeparam>
        /// <param name="whereExpression">
        /// Лямбда-выражение, задающее условие отбора записей для удаления.
        /// </param>
        /// <returns>Количество удалённых строк.</returns>
        public int Delete<T>(Expression<Func<T, bool>> whereExpression) where T : class
        {
            var query = (SqlQueryBuilder.GetDeleteQuery<T>(Options) + " " + SqlQueryBuilder.GetWhereClause(whereExpression, Options, true, out var cmdParam))
                .Trim();
            return ExecuteNonQuery(query, cmdParam);
        }

        /// <summary>
        /// Удаляет запись из базы данных на основании значений ключевых полей объекта.
        /// </summary>
        /// <typeparam name="T">Тип сущности, из таблицы которой выполняется удаление.</typeparam>
        /// <param name="item">
        /// Объект, содержащий значения ключевых полей, используемых в условии удаления.
        /// </param>
        /// <returns>Количество удалённых строк.</returns>
        public int Delete<T>(T item) where T : class
        {
            var query = (SqlQueryBuilder.GetDeleteQuery<T>(Options) + " " + SqlQueryBuilder.GetWhereClause<T>(Options, out _)).Trim();
            return ExecuteNonQuery(query, GetParams(item));
        }

        /// <summary>
        /// Асинхронно удаляет запись из базы данных на основании значений ключевых полей объекта.
        /// </summary>
        /// <typeparam name="T">Тип сущности, из таблицы которой выполняется удаление.</typeparam>
        /// <param name="item">
        /// Объект, содержащий значения ключевых полей, используемых в условии удаления.
        /// </param>
        /// <param name="dbTransaction">
        /// Транзакция, в рамках которой выполняется операция удаления. Может быть <c>null</c>.
        /// </param>
        /// <param name="token">Токен отмены асинхронной операции.</param>
        /// <returns>
        /// Задача, результатом которой является количество удалённых строк.
        /// </returns>
        public Task<int> DeleteAsync<T>(T item, IDbTransaction dbTransaction = null, CancellationToken token = default)
            where T : class
        {
            var query = (SqlQueryBuilder.GetDeleteQuery<T>(Options) + " " + SqlQueryBuilder.GetWhereClause<T>(Options, out _)).Trim();
            return ExecuteNonQueryAsync(query, GetParams(item), dbTransaction, token);
        }

        /// <summary>
        /// Асинхронно удаляет записи из базы данных, соответствующие заданному условию.
        /// </summary>
        /// <typeparam name="T">Тип сущности, из таблицы которой выполняется удаление.</typeparam>
        /// <param name="whereExpression">
        /// Лямбда-выражение, задающее условие отбора записей для удаления.
        /// </param>
        /// <param name="dbTransaction">
        /// Транзакция, в рамках которой выполняется операция удаления.
        /// </param>
        /// <param name="token">Токен отмены асинхронной операции.</param>
        /// <returns>
        /// Задача, результатом которой является количество удалённых строк.
        /// </returns>
        public Task<int> DeleteAsync<T>(Expression<Func<T, bool>> whereExpression, IDbTransaction dbTransaction, CancellationToken token = default) where T : class
        {
            var query = (SqlQueryBuilder.GetDeleteQuery<T>(Options) + " " + SqlQueryBuilder.GetWhereClause(whereExpression, Options, true, out var cmdParams))
                .Trim();
            return ExecuteNonQueryAsync(query, cmdParams, dbTransaction, token);
        }

        /// <summary>
        /// Асинхронно удаляет несколько записей из базы данных в рамках одной транзакции.
        /// </summary>
        /// <typeparam name="T">Тип сущности, из таблицы которой выполняется удаление.</typeparam>
        /// <param name="list">
        /// Коллекция объектов, содержащих значения ключевых полей удаляемых записей.
        /// </param>
        /// <param name="dbTransaction">
        /// Транзакция, в рамках которой выполняется удаление. Если <c>null</c>, создаётся новая транзакция.
        /// </param>
        /// <param name="token">Токен отмены асинхронной операции.</param>
        /// <returns>
        /// Задача, результатом которой является общее количество удалённых строк.
        /// </returns>
        /// <remarks>
        /// Все операции удаления выполняются в одной транзакции.
        /// В случае возникновения ошибки транзакция откатывается.
        /// </remarks>
        public async Task<int> DeleteRangeAsync<T>(IEnumerable<T> list, IDbTransaction dbTransaction, CancellationToken token = default) where T : class
        {
            try
            {
                var count = 0;
                using (dbTransaction ?? BeginTransaction())
                {
                    foreach (var item in list) count += await DeleteAsync(item, dbTransaction, token).ConfigureAwait(ConfigureAwait);

                    EndTransaction();
                }

                return count;
            }
            catch (Exception ex)
            {
                RollbackTransaction();
                throw HandleDbException(ex, null);
            }
            finally
            {
                CloseConnection();
            }
        }

        #endregion Delete

        #region Transaction

        /// <summary>
        /// Инициирует начало транзакции с заданным уровнем изоляции.
        /// </summary>
        /// <param name="level">Уровень изоляции транзакции. По умолчанию используется <see cref="IsolationLevel.ReadCommitted"/>.</param>
        /// <returns>Объект транзакции, который можно использовать для дальнейших операций в рамках транзакции.</returns>
        /// <exception cref="InvalidOperationException">Вызывается, если транзакция уже была начата.</exception>
        /// <remarks>
        /// Этот метод открывает соединение с базой данных и начинает транзакцию с указанным уровнем изоляции.
        /// Если транзакция уже была начата, будет выброшено исключение.
        /// </remarks>
        public IDbTransaction BeginTransaction(IsolationLevel level = IsolationLevel.ReadCommitted)
        {
            if (_tr.Value != null)
                throw new InvalidOperationException("Транзакция уже была начата.");

            BeginConnection();
            _tr.Value = Connection.BeginTransaction(level);
            return _tr.Value;
        }

        /// <summary>
        /// Завершается текущая транзакция и закрывает соединение с базой данных.
        /// </summary>
        /// <exception cref="InvalidOperationException">Вызывается, если транзакция не была начата.</exception>
        /// <remarks>
        /// Этот метод коммитит текущую транзакцию и очищает ресурсы, связанные с ней.
        /// После завершения транзакции соединение с базой данных закрывается.
        /// </remarks>
        public void EndTransaction()
        {
            if (_tr.Value == null)
                throw new InvalidOperationException("Транзакция не была начата.");

            _tr.Value.Commit();
            _tr.Value.Dispose();
            _tr.Value = null;
            CloseConnection();
        }

        #endregion Transaction

        #region ExecuteNonQuery

        /// <summary>
        /// Выполняет SQL-запрос, который не возвращает результатов (например, INSERT, UPDATE, DELETE).
        /// </summary>
        /// <param name="query">SQL-запрос, который будет выполнен.</param>
        /// <param name="queryParams">Параметры запроса. Может быть <c>null</c>, если параметры не требуются.</param>
        /// <param name="dbTransaction">Транзакция, в рамках которой будет выполнен запрос. Может быть <c>null</c>.</param>
        /// <returns>Количество затронутых строк в базе данных.</returns>
        /// <exception cref="Exception">Вызывается в случае ошибки при выполнении запроса.</exception>
        /// <remarks>
        /// Этот метод выполняет запрос синхронно и возвращает количество затронутых строк в базе данных.
        /// В случае ошибки будет выброшено исключение.
        /// </remarks>
        public int ExecuteNonQuery(string query, object queryParams = null, IDbTransaction dbTransaction = null)
        {
            using (var cmd = CreateCommand(query, queryParams, dbTransaction))
            {
                BeginConnection(Connection);

                var i = cmd.ExecuteNonQuery();
                CommandExecuted?.Invoke(cmd);
                CloseConnection(Connection);
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
        /// <remarks>
        /// Этот метод выполняет запрос асинхронно и возвращает количество затронутых строк в базе данных.
        /// В случае ошибки будет выброшено исключение.
        /// </remarks>
        public async Task<int> ExecuteNonQueryAsync(string query, object cmdParams = null, IDbTransaction dbTransaction = null, CancellationToken token = default)
        {
            using (var cmd = CreateCommand(query, cmdParams, dbTransaction))
            {
                try
                {
                    await BeginConnectionAsync(token).ConfigureAwait(ConfigureAwait);
                    var i = await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(ConfigureAwait);
                    CommandExecuted?.Invoke(cmd);
                    return i;
                }
                catch (Exception ex)
                {
                    throw HandleDbException(ex, cmd);
                }
                finally
                {
                    CloseConnection();
                }
            }
        }

        #endregion

        #region ExecuteScalar

        /// <summary>
        /// Выполняет SQL-запрос и возвращает первое значение из первого столбца результата (например, COUNT, SUM, AVG).
        /// </summary>
        /// <param name="query">SQL-запрос, который будет выполнен.</param>
        /// <param name="cmdParams">Параметры запроса. Может быть <c>null</c>, если параметры не требуются.</param>
        /// <param name="dbTransaction">Транзакция, в рамках которой выполняется запрос. Может быть <c>null</c>.</param>
        /// <returns>Результат выполнения запроса в виде объекта.</returns>
        /// <exception cref="Exception">Вызывается в случае ошибки при выполнении запроса.</exception>
        /// <remarks>
        /// Этот метод выполняет запрос синхронно и возвращает первое значение из первого столбца результата.
        /// Если запрос не возвращает значений, будет возвращено <c>null</c>.
        /// </remarks>
        public object ExecuteScalar(string query, object cmdParams = null, IDbTransaction dbTransaction = null)
        {
            return ExecuteScalar<object>(query, cmdParams, dbTransaction);
        }

        /// <summary>
        /// Выполняет SQL-запрос и возвращает первое значение из первого столбца результата (например, COUNT, SUM, AVG).
        /// </summary>
        /// <param name="cmd">Команда, которая будет выполнена.</param>
        /// <returns>Результат выполнения запроса в виде объекта.</returns>
        /// <exception cref="Exception">Вызывается в случае ошибки при выполнении запроса.</exception>
        /// <remarks>
        /// Этот метод выполняет запрос синхронно и возвращает первое значение из первого столбца результата.
        /// Если запрос не возвращает значений, будет возвращено <c>null</c>.
        /// </remarks>
        public object ExecuteScalar(IDbCommand cmd)
        {
            return ExecuteScalar<object>(cmd);
        }

        /// <summary>
        /// Выполняет SQL-запрос с выбором значения по указанному выражению и условию, возвращая первое значение.
        /// </summary>
        /// <typeparam name="T">Тип сущности, из которой выполняется выборка.</typeparam>
        /// <typeparam name="TProp">Тип свойства, которое выбирается.</typeparam>
        /// <param name="propertySelector">Выражение, определяющее свойство для выбора.</param>
        /// <param name="whereExpression">Условие для фильтрации записей.</param>
        /// <returns>Результат выполнения запроса в виде выбранного свойства.</returns>
        /// <exception cref="Exception">Вызывается в случае ошибки при выполнении запроса.</exception>
        /// <remarks>
        /// Этот метод выполняет SQL-запрос, выбирает значение для указанного свойства и возвращает результат.
        /// </remarks>
        public TProp ExecuteScalar<T, TProp>(Expression<Func<T, TProp>> propertySelector, Expression<Func<T, bool>> whereExpression)
        {
            var query = (SqlQueryBuilder.GetSelectQuery(Options, propertySelector) + " " +
                         SqlQueryBuilder.GetWhereClause(whereExpression, Options, true, out var cmdParam)).Trim();
            return ExecuteScalar<TProp>(query, cmdParam);
        }

        /// <summary>
        /// Выполняет SQL-запрос и возвращает результат как объект указанного типа.
        /// </summary>
        /// <typeparam name="T">Тип результата, в который будет преобразован результат запроса.</typeparam>
        /// <param name="query">SQL-запрос, который будет выполнен.</param>
        /// <param name="cmdParams">Параметры запроса. Может быть <c>null</c>, если параметры не требуются.</param>
        /// <param name="dbTransaction">Транзакция, в рамках которой выполняется запрос. Может быть <c>null</c>.</param>
        /// <returns>Результат выполнения запроса, приведённый к типу <typeparamref name="T"/>.</returns>
        /// <exception cref="Exception">Вызывается в случае ошибки при выполнении запроса.</exception>
        /// <remarks>
        /// Этот метод выполняет запрос синхронно и преобразует результат в указанный тип.
        /// </remarks>
        public T ExecuteScalar<T>(string query, object cmdParams = null, IDbTransaction dbTransaction = null)
        {
            var cmd = CreateCommand(query, cmdParams, dbTransaction);
            return ExecuteScalar<T>(cmd);
        }

        /// <summary>
        /// Выполняет SQL-запрос и возвращает результат как объект указанного типа.
        /// </summary>
        /// <typeparam name="T">Тип результата, в который будет преобразован результат запроса.</typeparam>
        /// <param name="cmd">Команда, которая будет выполнена.</param>
        /// <returns>Результат выполнения запроса, приведённый к типу <typeparamref name="T"/>.</returns>
        /// <exception cref="Exception">Вызывается в случае ошибки при выполнении запроса.</exception>
        /// <remarks>
        /// Этот метод выполняет запрос синхронно и преобразует результат в указанный тип.
        /// </remarks>
        public T ExecuteScalar<T>(IDbCommand cmd)
        {
            using (cmd)
            {
                try
                {
                    BeginConnection();
                    var v = cmd.ExecuteScalar();
                    CommandExecuted?.Invoke(cmd);
                    return (T)ChangeType(v, typeof(T));
                }
                catch (Exception ex)
                {
                    throw HandleDbException(ex, cmd);
                }
                finally
                {
                    CloseConnection();
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
        /// <remarks>
        /// Этот метод выполняет запрос асинхронно и возвращает первое значение из первого столбца результата.
        /// Если запрос не возвращает значений, будет возвращено <c>null</c>.
        /// </remarks>
        public Task<object> ExecuteScalarAsync(string query, object cmdParams = null, IDbTransaction dbTransaction = null, CancellationToken token = default)
        {
            return ExecuteScalarAsync<object>(query, cmdParams, dbTransaction, token);
        }

        /// <summary>
        /// Асинхронно выполняет SQL-запрос и возвращает результат как объект указанного типа.
        /// </summary>
        /// <param name="cmd">Команда, которая будет выполнена.</param>
        /// <param name="token">Токен отмены асинхронной операции.</param>
        /// <returns>Задача, которая возвращает результат выполнения запроса как объект указанного типа.</returns>
        /// <exception cref="Exception">Вызывается в случае ошибки при выполнении запроса.</exception>
        /// <remarks>
        /// Этот метод выполняет запрос асинхронно и преобразует результат в указанный тип.
        /// </remarks>
        public Task<object> ExecuteScalarAsync(IDbCommand cmd, CancellationToken token = default)
        {
            return ExecuteScalarAsync<object>(cmd as DbCommand, token);
        }

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
        /// <remarks>
        /// Этот метод выполняет SQL-запрос, выбирает значение для указанного свойства и возвращает результат.
        /// </remarks>
        public Task<TProp> ExecuteScalarAsync<T, TProp>(Expression<Func<T, TProp>> propertySelector, Expression<Func<T, bool>> whereExpression, CancellationToken token = default)
        {
            var query = (SqlQueryBuilder.GetSelectQuery(Options, propertySelector) + " " +
                         SqlQueryBuilder.GetWhereClause(whereExpression, Options, true, out var cmdParam)).Trim();
            return ExecuteScalarAsync<TProp>(query, cmdParam, token: token);
        }

        /// <summary>
        /// Асинхронно выполняет SQL-запрос и возвращает результат как объект указанного типа.
        /// </summary>
        /// <typeparam name="T">Тип результата, в который будет преобразован результат запроса.</typeparam>
        /// <param name="query">SQL-запрос, который будет выполнен.</param>
        /// <param name="cmdParams">Параметры запроса. Может быть <c>null</c>, если параметры не требуются.</param>
        /// <param name="dbTransaction">Транзакция, в рамках которой выполняется запрос. Может быть <c>null</c>.</param>
        /// <param name="token">Токен отмены асинхронной операции.</param>
        /// <returns>Задача, которая возвращает результат выполнения запроса, приведённый к типу <typeparamref name="T"/>.</returns>
        /// <exception cref="Exception">Вызывается в случае ошибки при выполнении запроса.</exception>
        /// <remarks>
        /// Этот метод выполняет запрос асинхронно и преобразует результат в указанный тип.
        /// </remarks>
        public Task<T> ExecuteScalarAsync<T>(string query, object cmdParams = null, IDbTransaction dbTransaction = null, CancellationToken token = default)
        {
            var cmd = CreateCommand(query, cmdParams, dbTransaction);
            return ExecuteScalarAsync<T>(cmd, token);
        }

        /// <summary>
        /// Асинхронно выполняет SQL-запрос и возвращает результат как объект указанного типа.
        /// </summary>
        /// <typeparam name="T">Тип результата, в который будет преобразован результат запроса.</typeparam>
        /// <param name="cmd">Команда, которая будет выполнена.</param>
        /// <param name="token">Токен отмены асинхронной операции.</param>
        /// <returns>Задача, которая возвращает результат выполнения запроса, приведённый к типу <typeparamref name="T"/>.</returns>
        /// <exception cref="Exception">Вызывается в случае ошибки при выполнении запроса.</exception>
        /// <remarks>
        /// Этот метод выполняет запрос асинхронно и преобразует результат в указанный тип.
        /// </remarks>
        public async Task<T> ExecuteScalarAsync<T>(DbCommand cmd, CancellationToken token = default)
        {
            using (cmd)
            {
                try
                {
                    await BeginConnectionAsync(token).ConfigureAwait(ConfigureAwait);
                    var v = await cmd.ExecuteScalarAsync(token).ConfigureAwait(ConfigureAwait);
                    CommandExecuted?.Invoke(cmd);
                    return (T)ChangeType(v, typeof(T));
                }
                catch (Exception ex)
                {
                    throw HandleDbException(ex, cmd);
                }
                finally
                {
                    CloseConnection();
                }
            }
        }

        #endregion

        #region First

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
        /// <param name="itemFactory">Фабрика для создания объекта типа <typeparamref name="T"/> из данных строки. Может быть <c>null</c>.</param>
        /// <returns>Первый элемент результата выборки или <c>null</c>, если результат пуст.</returns>
        /// <remarks>
        /// Этот метод выполняет запрос синхронно и возвращает первый элемент результата или <c>null</c>, если данные отсутствуют.
        /// </remarks>
        public T First<T>(string query = null, object cmdParams = null, IEnumerable<string> columns = null,
            IEnumerable<(string, string)> columnToPropertyMap = null, DbValueConverter<T> converter = null,
            int offsetRows = 0, Func<object[], string[], T> itemFactory = null)
        {
            return ToList(query, cmdParams, columns, columnToPropertyMap, converter, 1, offsetRows, itemFactory)
                .FirstOrDefault();
        }

        /// <summary>
        /// Возвращает первый элемент, соответствующий условию, из результата запроса (или <c>null</c>, если результат пуст).
        /// </summary>
        /// <typeparam name="T">Тип объекта, который будет возвращён.</typeparam>
        /// <param name="whereExpression">Лямбда-выражение, задающее условие для выборки.</param>
        /// <param name="columnToPropertyMap">Отображение столбцов в свойства объекта. Может быть <c>null</c>.</param>
        /// <param name="converter">Конвертер значений для преобразования типов. Может быть <c>null</c>.</param>
        /// <param name="offsetRows">Количество строк для пропуска перед выборкой. По умолчанию <c>0</c>.</param>
        /// <param name="itemFactory">Фабрика для создания объекта типа <typeparamref name="T"/> из данных строки. Может быть <c>null</c>.</param>
        /// <param name="orderByExpression">Условия сортировки. Если переданы, запрос будет отсортирован.</param>
        /// <returns>Первый элемент, соответствующий условию, или <c>null</c>, если результат пуст.</returns>
        /// <remarks>
        /// Этот метод выполняет запрос синхронно с учётом условия выборки и сортировки, и возвращает первый элемент результата или <c>null</c>, если данные отсутствуют.
        /// </remarks>
        public T First<T>(Expression<Func<T, bool>> whereExpression, IEnumerable<(string, string)> columnToPropertyMap = null, DbValueConverter<T> converter = null, int offsetRows = 0, Func<object[], string[], T> itemFactory = null, params (Expression<Func<T, object>>, bool)[] orderByExpression)
        {
            return ToList(whereExpression, columnToPropertyMap, converter, 1, offsetRows, itemFactory, orderByExpression).FirstOrDefault();
        }

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
        /// <param name="itemFactory">Фабрика для создания объекта типа <typeparamref name="T"/> из данных строки. Может быть <c>null</c>.</param>
        /// <returns>Задача, которая возвращает первый элемент результата выборки или <c>null</c>, если результат пуст.</returns>
        /// <remarks>
        /// Этот метод выполняет запрос асинхронно и возвращает первый элемент результата или <c>null</c>, если данные отсутствуют.
        /// </remarks>
        public async Task<T> FirstAsync<T>(string query = null, object cmdParams = null,
            IEnumerable<string> columns = null, IEnumerable<(string, string)> columnToPropertyMap = null,
            DbValueConverter<T> converter = null, int offsetRows = 0, Func<object[], string[], T> itemFactory = null)
        {
            return (await ToListAsync(query, cmdParams, columns, columnToPropertyMap, converter, 1, offsetRows,
                itemFactory).ConfigureAwait(ConfigureAwait)).FirstOrDefault();
        }

        /// <summary>
        /// Асинхронно возвращает первый элемент, соответствующий условию, из результата запроса (или <c>null</c>, если результат пуст).
        /// </summary>
        /// <typeparam name="T">Тип объекта, который будет возвращён.</typeparam>
        /// <param name="whereExpression">Лямбда-выражение, задающее условие для выборки.</param>
        /// <param name="columnToPropertyMap">Отображение столбцов в свойства объекта. Может быть <c>null</c>.</param>
        /// <param name="converter">Конвертер значений для преобразования типов. Может быть <c>null</c>.</param>
        /// <param name="offsetRows">Количество строк для пропуска перед выборкой. По умолчанию <c>0</c>.</param>
        /// <param name="itemFactory">Фабрика для создания объекта типа <typeparamref name="T"/> из данных строки. Может быть <c>null</c>.</param>
        /// <param name="ct">Токен отмены асинхронной операции.</param>
        /// <param name="orderByExpression">Условия сортировки. Если переданы, запрос будет отсортирован.</param>
        /// <returns>Задача, которая возвращает первый элемент, соответствующий условию, или <c>null</c>, если результат пуст.</returns>
        /// <remarks>
        /// Этот метод выполняет запрос асинхронно с учётом условия выборки и сортировки, и возвращает первый элемент результата или <c>null</c>, если данные отсутствуют.
        /// </remarks>
        public async Task<T> FirstAsync<T>(Expression<Func<T, bool>> whereExpression,
            IEnumerable<(string, string)> columnToPropertyMap = null, DbValueConverter<T> converter = null,
            int offsetRows = 0, Func<object[], string[], T> itemFactory = null, CancellationToken ct = default,
            params (Expression<Func<T, object>>, bool)[] orderByExpression)
        {
            return (await ToListAsync(whereExpression, columnToPropertyMap, converter, 1, offsetRows, itemFactory, ct,
                orderByExpression).ConfigureAwait(ConfigureAwait)).FirstOrDefault();
        }

        #endregion First

        #region Command

        /// <summary>
        /// Создаёт и настраивает команду для выполнения SQL-запроса.
        /// </summary>
        /// <param name="query">SQL-запрос, который будет выполнен.</param>
        /// <param name="cmdParams">Параметры запроса. Может быть <c>null</c>, если параметры не требуются.</param>
        /// <param name="dbTransaction">Транзакция, в рамках которой выполняется запрос. Может быть <c>null</c>.</param>
        /// <param name="commandTimeOut">Тайм-аут выполнения команды в секундах. Если не задано, используется значение по умолчанию.</param>
        /// <returns>Объект <see cref="DbCommand"/>, готовый к выполнению запроса.</returns>
        /// <remarks>
        /// Этот метод создаёт команду для выполнения SQL-запроса, назначает ей параметры и устанавливает тайм-аут выполнения.
        /// Если параметры не указаны, команда будет выполнена без них.
        /// </remarks>
        public DbCommand CreateCommand(string query, object cmdParams, IDbTransaction dbTransaction = null, int? commandTimeOut = null)
        {
            var cmd = Connection.CreateCommand();
            cmd.CommandText = query;
            cmd.CommandTimeout = commandTimeOut ?? DefaultCommandTimeout;
            cmd.CommandType = CommandType.Text;
            cmd.Transaction = dbTransaction;

            var parameters = GetParams(cmdParams);

            if (cmdParams != null)
                foreach (var cp in parameters)
                {
                    var p = cmd.CreateParameter();
                    p.ParameterName = cp.Key;
                    p.Value = cp.Value ?? DBNull.Value;
                    cmd.Parameters.Add(p);
                }

            if (_tr != null)
                cmd.Transaction = _tr.Value;

            LogCommand(cmd);

            return cmd as DbCommand;
        }

        /// <summary>
        /// Устанавливает коллекцию параметров для команды.
        /// </summary>
        /// <param name="cmd">Команда, для которой устанавливаются параметры.</param>
        /// <param name="cmdParams">Коллекция параметров в виде словаря, где ключ — имя параметра, а значение — его значение.</param>
        /// <remarks>
        /// Этот метод устанавливает параметры для команды. Если параметр уже существует, его значение обновляется.
        /// </remarks>
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

        private readonly IReadOnlyDictionary<string, object> _emptyParams = new Dictionary<string, object>();

        /// <summary>
        /// Получает параметры для запроса на основе переданного объекта.
        /// </summary>
        /// <param name="cmdParams">Объект, содержащий параметры запроса. Может быть коллекцией или одиночной парой ключ-значение.</param>
        /// <param name="propertyNames">Список имён свойств для выборки из объекта. Если не указано, выбираются все свойства.</param>
        /// <returns>Коллекция параметров в виде словаря, где ключ — имя параметра, а значение — его значение.</returns>
        /// <remarks>
        /// Этот метод извлекает параметры из переданного объекта. Он поддерживает работу с различными типами данных, такими как:
        /// <list type="bullet">
        /// <item><description>Словари (Dictionary)</description></item>
        /// <item><description>Коллекции (IEnumerable)</description></item>
        /// <item><description>Кортежи (Tuple)</description></item>
        /// </list>
        /// Если объект представляет собой кортеж, параметры будут извлечены из его элементов.
        /// </remarks>
        public IReadOnlyDictionary<string, object> GetParams(object cmdParams, params string[] propertyNames)
        {
            var parameters = new Dictionary<string, object>();
            if (cmdParams == null)
                return _emptyParams;
            var memberCache = MemberCache.Create(cmdParams.GetType());
            switch (cmdParams)
            {
                case KeyValuePair<string, object> kvp:
                    return new Dictionary<string, object>() { kvp };

                case Dictionary<string, object> dic:
                    return dic;

                case IDictionary<string, object> idic:
                    return idic.ToDictionary(x => x.Key, x => x.Value);

                case IEnumerable e:
                    {
                        var elementCache = MemberCache.Create(memberCache.ElementType);

                        var key = elementCache.GetMember("Key", MemberNameType.Name) ?? elementCache.GetMember("Item1", MemberNameType.Name);
                        var val = elementCache.GetMember("Value", MemberNameType.Name) ?? elementCache.GetMember("Item2", MemberNameType.Name);
                        if (key == null || val == null)
                            break;
                        foreach (var i in e)
                        {
                            parameters[key.GetValue<string>(i)] = val.GetValue(i);
                        }
                    }

                    break;

                default:
                {
                    if (memberCache.IsTuple)
                    {
                        var key = memberCache.GetMember("Key", MemberNameType.Name) ?? memberCache.GetMember("Item1", MemberNameType.Name);
                        var val = memberCache.GetMember("Value", MemberNameType.Name) ?? memberCache.GetMember("Item2", MemberNameType.Name);

                        parameters[key.GetValue<string>(cmdParams)] = val.GetValue(cmdParams);
                    }
                    else
                    {
                        parameters = memberCache.ToDictionary(cmdParams, propertyNames);
                    }
                }
                    break;
            }

            return parameters;
        }

        #endregion Command

        #region Query

        /// <summary>
        /// Выполняет SQL-запрос и возвращает результат в виде коллекции объектов.
        /// </summary>
        /// <typeparam name="TList">Тип коллекции, которая будет возвращена (например, <see cref="List{T}"/>).</typeparam>
        /// <typeparam name="T">Тип объектов, которые содержатся в коллекции.</typeparam>
        /// <param name="query">SQL-запрос для выборки данных. Если <c>null</c> или пустой, используется запрос по умолчанию.</param>
        /// <param name="cmdParams">Параметры для SQL-запроса.</param>
        /// <param name="columns">Список столбцов для выборки. Если <c>null</c>, выбираются все столбцы.</param>
        /// <param name="columnToPropertyMap">Отображение столбцов в свойства объектов. Может быть <c>null</c>.</param>
        /// <param name="converter">Конвертер для преобразования значений. Может быть <c>null</c>.</param>
        /// <param name="fetchRows">Количество строк для выборки. По умолчанию —1 (выбираются все строки).</param>
        /// <param name="offsetRows">Количество строк для пропуска перед выборкой. По умолчанию — 0.</param>
        /// <param name="itemFactory">Фабрика для создания объектов типа <typeparamref name="T"/>. Может быть <c>null</c>.</param>
        /// <returns>Коллекция объектов типа <typeparamref name="T"/>, которая содержит результат выполнения запроса.</returns>
        /// <remarks>
        /// Этот метод выполняет SQL-запрос синхронно и возвращает результат в виде коллекции объектов.
        /// </remarks>
        public TList Query<TList, T>(string query = null, object cmdParams = null, IEnumerable<string> columns = null, IEnumerable<(string, string)> columnToPropertyMap = null, DbValueConverter<T> converter = null, int fetchRows = -1, int offsetRows = 0, Func<object[], string[], T> itemFactory = null) where TList : ICollection<T>, new()
        {
            if (string.IsNullOrEmpty(query))
                query = SqlQueryBuilder.GetSelectQuery<T>(Options);

            query = SqlQueryBuilder.AddLimitOffsetClauseToQuery(fetchRows, offsetRows, query, Options, typeof(T));

            var cache = MemberCache<T>.Create();
            if (itemFactory == null)
                itemFactory = BuildItemFactory(cache, columnToPropertyMap);

            var cmd = CreateCommand(query, cmdParams);
            try
            {
                BeginConnection();

                var reader = cmd.ExecuteReader();
                try
                {
                    CommandExecuted?.Invoke(cmd);

                    return ReadCoreAsync<TList, T>(reader, columns, columnToPropertyMap, converter, fetchRows, itemFactory, false, CancellationToken.None).GetAwaiter().GetResult();
                }
                finally
                {
                    reader.Dispose();
                }
            }
            catch (Exception ex)
            {
                throw HandleDbException(ex, cmd);
            }
            finally
            {
                cmd.Dispose();
                CloseConnection();
            }
        }

        public object Query(Type returnType, string query = null, object cmdParams = null, IEnumerable<string> columns = null, IEnumerable<(string, string)> columnToPropertyMap = null, DbValueConverter<object> converter = null, int fetchRows = -1, int offsetRows = 0, Func<object[], string[], object> itemFactory = null)
        {
            var mc = MemberCache.Create(returnType);
            if (string.IsNullOrEmpty(query))
                query = SqlQueryBuilder.GetSelectQuery(Options, mc.ElementType);

            query = SqlQueryBuilder.AddLimitOffsetClauseToQuery(fetchRows, offsetRows, query, Options, mc.ElementType);
            if (itemFactory == null)
                itemFactory = BuildItemFactory(mc.ElementType, columnToPropertyMap);
            var cmd = CreateCommand(query, cmdParams);
            try
            {
                BeginConnection();

                var reader = cmd.ExecuteReader();
                try
                {
                    CommandExecuted?.Invoke(cmd);

                    return ReadCoreAsync(mc, reader, columns, columnToPropertyMap, converter, fetchRows, itemFactory, false, CancellationToken.None).GetAwaiter().GetResult();
                }
                finally
                {
                    reader.Dispose();
                }
            }
            catch (Exception ex)
            {
                throw HandleDbException(ex, cmd);
            }
            finally
            {
                cmd.Dispose();
                CloseConnection();
            }
        }

        /// <summary>
        /// Выполняет SQL-запрос и возвращает результат в виде коллекции объектов.
        /// </summary>
        /// <typeparam name="TList">Тип коллекции, которая будет возвращена (например, <see cref="List{T}"/>).</typeparam>
        /// <typeparam name="T">Тип объектов, которые содержатся в коллекции.</typeparam>
        /// <param name="query">SQL-запрос для выборки данных. Если <c>null</c> или пустой, используется запрос по умолчанию.</param>
        /// <param name="cmdParams">Параметры для SQL-запроса.</param>
        /// <param name="columns">Список столбцов для выборки. Если <c>null</c>, выбираются все столбцы.</param>
        /// <param name="columnToPropertyMap">Отображение столбцов в свойства объектов. Может быть <c>null</c>.</param>
        /// <param name="converter">Конвертер для преобразования значений. Может быть <c>null</c>.</param>
        /// <param name="fetchRows">Количество строк для выборки. По умолчанию —1 (выбираются все строки).</param>
        /// <param name="offsetRows">Количество строк для пропуска перед выборкой. По умолчанию — 0.</param>
        /// <param name="itemFactory">Фабрика для создания объектов типа <typeparamref name="T"/>. Может быть <c>null</c>.</param>
        /// <returns>Коллекция объектов типа <typeparamref name="T"/>, которая содержит результат выполнения запроса.</returns>
        /// <remarks>
        /// Этот метод выполняет SQL-запрос синхронно и возвращает результат в виде коллекции объектов.
        /// </remarks>
        public async Task<TList> QueryAsync<TList, T>(string query = null, object cmdParams = null, IEnumerable<string> columns = null, IEnumerable<(string, string)> columnToPropertyMap = null, DbValueConverter<T> converter = null, int fetchRows = -1, int offsetRows = 0, Func<object[], string[], T> itemFactory = null, CancellationToken ct = default) where TList : ICollection<T>, new()
        {
            if (string.IsNullOrEmpty(query))
                query = SqlQueryBuilder.GetSelectQuery<T>(Options);

            query = SqlQueryBuilder.AddLimitOffsetClauseToQuery(fetchRows, offsetRows, query, Options, typeof(T));

            var cache = MemberCache<T>.Create();
            if (itemFactory == null)
                itemFactory = BuildItemFactory(cache, columnToPropertyMap);

            var cmd = CreateCommand(query, cmdParams);
            try
            {
                await BeginConnectionAsync(ct).ConfigureAwait(ConfigureAwait);

                var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(ConfigureAwait);
                try
                {
                    CommandExecuted?.Invoke(cmd);

                    return await ReadCoreAsync<TList, T>(reader, columns, columnToPropertyMap, converter, fetchRows, itemFactory, true, ct).ConfigureAwait(ConfigureAwait);
                }
                finally
                {
                    reader.Dispose();
                }
            }
            catch (Exception ex)
            {
                throw HandleDbException(ex, cmd);
            }
            finally
            {
                cmd.Dispose();
                CloseConnection();
            }
        }

        #endregion Query

        #region ToDataTables

        /// <summary>
        /// Выполняет SQL-запрос и возвращает результат в виде <see cref="DataTable"/>.
        /// </summary>
        /// <typeparam name="TFrom">Тип объектов, для которых будет выполняться запрос.</typeparam>
        /// <param name="whereExpression">Условие выборки данных в виде выражения. Может быть <c>null</c>.</param>
        /// <param name="fetchRows">Количество строк для выборки. По умолчанию -1 (выбираются все строки).</param>
        /// <param name="offsetRows">Количество строк для пропуска перед выборкой. По умолчанию - 0.</param>
        /// <param name="columnSelectors">Селекторы столбцов для выборки. Может быть <c>null</c>.</param>
        /// <returns><see cref="DataTable"/>, содержащий результат выполнения запроса.</returns>
        /// <remarks>
        /// Этот метод выполняет SQL-запрос синхронно и возвращает результат в виде <see cref="DataTable"/>.
        /// Если передан параметр <paramref name="columnSelectors"/>, то выборка будет происходить только по указанным столбцам.
        /// </remarks>
        public DataTable ToDataTable<TFrom>(Expression<Func<TFrom, bool>> whereExpression = null, int fetchRows = -1, int offsetRows = 0, params Expression<Func<TFrom, object>>[] columnSelectors)
        {
            var query = (SqlQueryBuilder.GetSelectQuery(Options, columnSelectors) + " " +
                         SqlQueryBuilder.GetWhereClause(whereExpression, Options, true, out var cmdParam)).Trim();
            query = SqlQueryBuilder.AddLimitOffsetClauseToQuery(fetchRows, offsetRows, query, Options,
                typeof(TFrom));
            return ToDataTables(query, cmdParam).FirstOrDefault();
        }

        /// <summary>
        /// Асинхронно выполняет SQL-запрос и возвращает результат в виде <see cref="DataTable"/>.
        /// </summary>
        /// <typeparam name="TFrom">Тип объектов, для которых будет выполняться запрос.</typeparam>
        /// <param name="whereExpression">Условие выборки данных в виде выражения. Может быть <c>null</c>.</param>
        /// <param name="fetchRows">Количество строк для выборки. По умолчанию -1 (выбираются все строки).</param>
        /// <param name="offsetRows">Количество строк для пропуска перед выборкой. По умолчанию - 0.</param>
        /// <param name="columnSelectors">Селекторы столбцов для выборки. Может быть <c>null</c>.</param>
        /// <param name="ct">Токен отмены асинхронной операции.</param>
        /// <returns>Задача, которая возвращает <see cref="DataTable"/>, содержащий результат выполнения запроса.</returns>
        /// <remarks>
        /// Этот метод выполняет SQL-запрос асинхронно и возвращает результат в виде <see cref="DataTable"/>.
        /// </remarks>
        public async Task<DataTable> ToDataTableAsync<TFrom>(Expression<Func<TFrom, bool>> whereExpression = null, int fetchRows = -1, int offsetRows = 0, params Expression<Func<TFrom, object>>[] columnSelectors)
        {
            var query = (SqlQueryBuilder.GetSelectQuery(Options, columnSelectors) + " " +
                         SqlQueryBuilder.GetWhereClause(whereExpression, Options, true, out var cmdParam)).Trim();
            query = SqlQueryBuilder.AddLimitOffsetClauseToQuery(fetchRows, offsetRows, query, Options,
                typeof(TFrom));
            return (await ToDataTablesAsync(query,cmdParam).ConfigureAwait(ConfigureAwait)).FirstOrDefault();
        }

        /// <summary>
        /// Выполняет SQL-запрос и возвращает результат в виде <see cref="DataTable"/>, с возможностью отображения столбцов в соответствии с их именами.
        /// </summary>
        /// <param name="query">SQL-запрос для выполнения.</param>
        /// <param name="cmdParams">Параметры запроса.</param>
        /// <param name="columnMap">Отображение столбцов запроса в имена свойств объектов. Каждый элемент содержит имя столбца и имя свойства объекта.</param>
        /// <returns><see cref="DataTable"/>, содержащий результат выполнения запроса.</returns>
        /// <remarks>
        /// Этот метод выполняет SQL-запрос синхронно и возвращает результат в виде <see cref="DataTable"/>, при этом позволяет
        /// отображать столбцы запроса в соответствии с их именами в объекте.
        public DataTable ToDataTable(string query, object cmdParams = null, params (string, string)[] columnMap)
        {
            return ToDataTables(query, cmdParams, columnMap).FirstOrDefault();
        }

        /// <summary>
        /// Асинхронно выполняет SQL-запрос и возвращает результат в виде <see cref="DataTable"/>, с возможностью отображения столбцов в соответствии с их именами.
        /// </summary>
        /// <param name="query">SQL-запрос для выполнения.</param>
        /// <param name="cmdParams">Параметры запроса.</param>
        /// <param name="token">Токен отмены асинхронной операции.</param>
        /// <param name="columnMap">Отображение столбцов запроса в имена свойств объектов. Каждый элемент содержит имя столбца и имя свойства объекта.</param>
        /// <returns>Задача, которая возвращает <see cref="DataTable"/>, содержащий результат выполнения запроса.</returns>
        /// <remarks>
        /// Этот метод выполняет SQL-запрос асинхронно и возвращает результат в виде <see cref="DataTable"/>, при этом позволяет
        /// отображать столбцы запроса в соответствии с их именами в объекте.
        public async Task<DataTable> ToDataTableAsync(string query, object cmdParams = null, CancellationToken token = default, params (string, string)[] columnMap)
        {
            return (await ToDataTablesAsync(query, cmdParams, token, columnMap).ConfigureAwait(ConfigureAwait)).FirstOrDefault();
        }

        /// <summary>
        /// Выполняет SQL-запрос и возвращает результат в виде массива <see cref="DataTable"/>.
        /// </summary>
        /// <param name="query">SQL-запрос для выполнения.</param>
        /// <param name="cmdParams">Параметры запроса.</param>
        /// <param name="columnMap">Отображение столбцов запроса в имена свойств объектов.</param>
        /// <returns>Массив <see cref="DataTable"/>, содержащий результаты выполнения запроса.</returns>
        /// <remarks>
        /// Этот метод выполняет SQL-запрос синхронно и возвращает результаты в виде массива <see cref="DataTable"/>. Если запрос
        /// возвращает несколько наборов данных, они будут разделены в разные таблицы.
        public DataTable[] ToDataTables(string query, object cmdParams = null, params (string, string)[] columnMap)
        {
            if (string.IsNullOrWhiteSpace(query))
                throw new NullReferenceException(nameof(query));

            var result = new List<DataTable>();

            using (var cmd = CreateCommand(query, cmdParams))
            {
                try
                {
                    BeginConnection();

                    var dataTable = new DataTable(query);
                    dataTable.BeginLoadData();

                    using (var r = cmd.ExecuteReader())
                    {
                        do
                        {
                            CommandExecuted?.Invoke(cmd);
                            var map = GetReaderFieldToPropertyMap(r, columnMap);
                            foreach (var kv in map)
                            {
                                var col = new DataColumn(kv.Value, r.GetFieldType(kv.Key) ?? typeof(object));
                                dataTable.Columns.Add(col);
                            }

                            while (r.Read())
                            {
                                var item = dataTable.NewRow();

                                foreach (var kv in map)
                                {
                                    var colIndex = kv.Key;
                                    var raw = r.GetValue(colIndex);
                                    if (raw == null || raw == DBNull.Value) continue;

                                    item[kv.Value] = raw;
                                }

                                dataTable.Rows.Add(item);
                            }

                            dataTable.AcceptChanges();
                            dataTable.EndLoadData();
                            result.Add(dataTable);
                        } while (r.NextResult());

                        return result.ToArray();
                    }
                }
                catch (Exception ex)
                {
                    throw HandleDbException(ex, cmd);
                }
                finally
                {
                    CloseConnection();
                }
            }
        }

        /// <summary>
        /// Асинхронно выполняет SQL-запрос и возвращает результат в виде массива <see cref="DataTable"/>.
        /// </summary>
        /// <param name="query">SQL-запрос для выполнения.</param>
        /// <param name="cmdParams">Параметры запроса.</param>
        /// <param name="token">Токен отмены асинхронной операции.</param>
        /// <param name="columnMap">Отображение столбцов запроса в имена свойств объектов.</param>
        /// <returns>Задача, которая возвращает массив <see cref="DataTable"/>, содержащий результаты выполнения запроса.</returns>
        /// <remarks>
        /// Этот метод выполняет SQL-запрос асинхронно и возвращает результаты в виде массива <see cref="DataTable"/>. Если запрос
        /// возвращает несколько наборов данных, они будут разделены в разные таблицы.
        public async Task<DataTable[]> ToDataTablesAsync(string query, object cmdParams = null, CancellationToken token = default, params (string, string)[] columnMap)
        {
            if (string.IsNullOrWhiteSpace(query))
                throw new NullReferenceException(nameof(query));

            var result = new List<DataTable>();

            using (var cmd = CreateCommand(query, cmdParams))
            {
                try
                {
                    await BeginConnectionAsync(token).ConfigureAwait(ConfigureAwait);

                    var dataTable = new DataTable(query);
                    dataTable.BeginLoadData();

                    using (var r = await cmd.ExecuteReaderAsync(token).ConfigureAwait(ConfigureAwait))
                    {
                        do
                        {
                            CommandExecuted?.Invoke(cmd);
                            var map = GetReaderFieldToPropertyMap(r, columnMap);
                            foreach (var kv in map)
                            {
                                var col = new DataColumn(kv.Value, r.GetFieldType(kv.Key) ?? typeof(object));
                                dataTable.Columns.Add(col);
                            }

                            while (await r.ReadAsync(token).ConfigureAwait(ConfigureAwait))
                            {
                                var item = dataTable.NewRow();

                                foreach (var kv in map)
                                {
                                    var colIndex = kv.Key;
                                    var raw = r.GetValue(colIndex);
                                    if (raw == null || raw == DBNull.Value) continue;

                                    item[kv.Value] = raw;
                                }

                                dataTable.Rows.Add(item);
                            }

                            dataTable.AcceptChanges();
                            dataTable.EndLoadData();
                            result.Add(dataTable);
                        } while (await r.NextResultAsync(token).ConfigureAwait(ConfigureAwait));

                        return result.ToArray();
                    }
                }
                catch (Exception ex)
                {
                    throw HandleDbException(ex, cmd);
                }
                finally
                {
                    CloseConnection();
                }
            }
        }

        #endregion ToDataTables

        #region ToDictionary

        /// <summary>
        /// Выполняет SQL-запрос и возвращает результат в виде <see cref="Dictionary{TKey, TValue}"/>.
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
        /// <remarks>
        /// Этот метод выполняет SQL-запрос синхронно и преобразует результат в словарь <see cref="Dictionary{TKey, TValue}"/>.
        /// Если <paramref name="itemFactory"/> не задан, то результат будет преобразован в словарь по умолчанию.
        /// </remarks>
        public Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(string query, object cmdParams = null, IEnumerable<string> columns = null, IEnumerable<(string, string)> columnToPropertyMap = null, int fetchRows = -1, int offsetRows = 0, Func<object[], string[], KeyValuePair<TKey, TValue>> itemFactory = null) 
        {
            return ToList(query, cmdParams, columns, columnToPropertyMap, null, fetchRows,
                offsetRows, itemFactory).ToDictionary(x=>x.Key, x=>x.Value);
        }

        /// <summary>
        /// Асинхронно выполняет SQL-запрос и возвращает результат в виде <see cref="Dictionary{TKey, TValue}"/>.
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
        /// <returns>Задача, которая возвращает <see cref="Dictionary{TKey, TValue}"/>, содержащий результат выполнения запроса.</returns>
        /// <remarks>
        /// Этот метод выполняет SQL-запрос асинхронно и преобразует результат в словарь <see cref="Dictionary{TKey, TValue}"/>.
        /// </remarks>
        public async Task<Dictionary<TKey, TValue>> ToDictionaryAsync<TKey, TValue>(string query, object cmdParams = null, IEnumerable<string> columns = null, IEnumerable<(string, string)> columnToPropertyMap = null, int fetchRows = -1, int offsetRows = 0, Func<object[], string[], KeyValuePair<TKey, TValue>> itemFactory = null)
        {
            return (await ToListAsync(query, cmdParams, columns, columnToPropertyMap, null, fetchRows,
                offsetRows, itemFactory).ConfigureAwait(ConfigureAwait)).ToDictionary(x => x.Key, x => x.Value);
        }

        /// <summary>
        /// Выполняет SQL-запрос и возвращает результат в виде <see cref="Dictionary{TKey, TValue}"/> с использованием селекторов ключа и значения.
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
        /// <remarks>
        /// Этот метод выполняет SQL-запрос синхронно и преобразует результат в словарь <see cref="Dictionary{TKey, TValue}"/>.
        /// </remarks>
        public Dictionary<TKey, TValue> ToDictionary<TKey, TValue, TFrom>(Expression<Func<TFrom, TKey>> keySelector, Expression<Func<TFrom, TValue>> valueSelector, Expression<Func<TFrom, bool>> whereExpression = null, int fetchRows = -1, int offsetRows = 0, Func<object[], string[], KeyValuePair<TKey, TValue>> itemFactory = null)
        {
            var query = (SqlQueryBuilder.GetSelectQuery(Options, typeof(TFrom).GetMemberCache(), keySelector.GetMemberCache(), valueSelector.GetMemberCache()) + " " +
                         SqlQueryBuilder.GetWhereClause(whereExpression, Options, true, out var cmdParam)).Trim();
            query = SqlQueryBuilder.AddLimitOffsetClauseToQuery(fetchRows, offsetRows, query, Options, typeof(TFrom));
            return ToList(query, cmdParam, null, null, null, fetchRows,
                offsetRows, itemFactory).ToDictionary(x => x.Key, x => x.Value);
        }

        /// <summary>
        /// Асинхронно выполняет SQL-запрос и возвращает результат в виде <see cref="Dictionary{TKey, TValue}"/> с использованием селекторов ключа и значения.
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
        /// <returns>Задача, которая возвращает <see cref="Dictionary{TKey, TValue}"/>, содержащий результат выполнения запроса.</returns>
        /// <remarks>
        /// Этот метод выполняет SQL-запрос асинхронно и преобразует результат в словарь <see cref="Dictionary{TKey, TValue}"/>.
        /// </remarks>
        public async Task<Dictionary<TKey, TValue>> ToDictionaryAsync<TKey, TValue, TFrom>(Expression<Func<TFrom, TKey>> keySelector, Expression<Func<TFrom, TValue>> valueSelector, Expression<Func<TFrom, bool>> whereExpression = null, int fetchRows = -1, int offsetRows = 0, Func<object[], string[], KeyValuePair<TKey, TValue>> itemFactory = null)
        {
            var query = (SqlQueryBuilder.GetSelectQuery(Options, typeof(TFrom).GetMemberCache(), keySelector.GetMemberCache(), valueSelector.GetMemberCache()) + " " +
                         SqlQueryBuilder.GetWhereClause(whereExpression, Options, true, out var cmdParam)).Trim();
            query = SqlQueryBuilder.AddLimitOffsetClauseToQuery(fetchRows, offsetRows, query, Options, typeof(TFrom));
            return (await ToListAsync(query, cmdParam, null, null, null, fetchRows,
                offsetRows, itemFactory).ConfigureAwait(ConfigureAwait)).ToDictionary(x => x.Key, x => x.Value);
        }

        #endregion ToDictionary

        #region ToList

        /// <summary>
        /// Выполняет SQL-запрос и возвращает результат в виде списка объектов типа <typeparamref name="TItem"/>.
        /// </summary>
        /// <typeparam name="TItem">Тип объектов, которые будут содержаться в списке.</typeparam>
        /// <param name="query">SQL-запрос для выполнения. Если <c>null</c>, будет использован стандартный запрос.</param>
        /// <param name="cmdParams">Параметры запроса.</param>
        /// <param name="columns">Список столбцов для выборки. Может быть <c>null</c>.</param>
        /// <param name="columnToPropertyMap">Отображение столбцов SQL-запроса в свойства объектов. Может быть <c>null</c>.</param>
        /// <param name="converter">Конвертер для преобразования данных. Может быть <c>null</c>.</param>
        /// <param name="fetchRows">Количество строк для выборки. По умолчанию -1 (выбираются все строки).</param>
        /// <param name="offsetRows">Количество строк для пропуска перед выборкой. По умолчанию - 0.</param>
        /// <param name="itemFactory">Функция для создания объектов типа <typeparamref name="TItem"/>. Может быть <c>null</c>.</param>
        /// <returns>Список объектов типа <typeparamref name="TItem"/>.</returns>
        /// <remarks>
        /// Этот метод выполняет SQL-запрос синхронно и возвращает результат в виде списка объектов.
        /// Если <paramref name="itemFactory"/> не задан, используется стандартное преобразование данных в объекты.
        /// </remarks>
        public List<TItem> ToList<TItem>(string query = null, object cmdParams = null,
            IEnumerable<string> columns = null, IEnumerable<(string, string)> columnToPropertyMap = null,
            DbValueConverter<TItem> converter = null, int fetchRows = -1, int offsetRows = 0,
            Func<object[], string[], TItem> itemFactory = null)
        {
            var list = Query<List<TItem>, TItem>(query, cmdParams, columns, columnToPropertyMap, converter, fetchRows, offsetRows, itemFactory);
            var itemTypeCache = MemberCache<TItem>.Create();
            var tables = itemTypeCache.GetTables();
            if (tables.Length > 0)
            {
                foreach (var i in list)
                {
                    foreach (var t in tables)
                    {
                        var fk = Obj.FindMember(t.ElementType, t.ForeignColumnName) as PropertyInfo;
                        var tableQuery = SqlQueryBuilder.GetSelectQuery(Options, t.ElementType) + " " + SqlQueryBuilder.GetWhereClause(new[] { (MemberCache)fk }, Options, out var d);
                        d[d.Keys.First()] = Obj.Get(i, itemTypeCache.PrimaryKeys.Keys.First());
                        var tableValue = Query(t.Type, tableQuery, d);
                        //Obj.Set(i, t.Name)
                    }
                }
            }
            return list;
        }

        /// <summary>
        /// Выполняет SQL-запрос с фильтрацией и возвращает результат в виде списка объектов типа <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Тип объектов, которые будут содержаться в списке.</typeparam>
        /// <param name="whereExpression">Выражение для фильтрации данных.</param>
        /// <param name="columnToPropertyMap">Отображение столбцов SQL-запроса в свойства объектов. Может быть <c>null</c>.</param>
        /// <param name="converter">Конвертер для преобразования данных. Может быть <c>null</c>.</param>
        /// <param name="fetchRows">Количество строк для выборки. По умолчанию -1 (выбираются все строки).</param>
        /// <param name="offsetRows">Количество строк для пропуска перед выборкой. По умолчанию - 0.</param>
        /// <param name="itemFactory">Функция для создания объектов типа <typeparamref name="T"/>. Может быть <c>null</c>.</param>
        /// <param name="orderByExpression">Выражение для сортировки. Может быть <c>null</c>.</param>
        /// <returns>Список объектов типа <typeparamref name="T"/>.</returns>
        /// <remarks>
        /// Этот метод выполняет SQL-запрос синхронно с фильтрацией по выражению <paramref name="whereExpression"/> и возвращает результат в виде списка.
        /// </remarks>
        public List<T> ToList<T>(Expression<Func<T, bool>> whereExpression,
            IEnumerable<(string, string)> columnToPropertyMap = null, DbValueConverter<T> converter = null,
            int fetchRows = -1, int offsetRows = 0, Func<object[], string[], T> itemFactory = null,
            params (Expression<Func<T, object>>, bool)[] orderByExpression)
        {
            var query = (SqlQueryBuilder.GetSelectQuery<T>(Options) + " " + SqlQueryBuilder.GetWhereClause(whereExpression, Options, true, out var cmdParam) +
                         " " + SqlQueryBuilder.GetOrderBy(Options, orderByExpression)).Trim();

            return ToList(query, cmdParam, null, columnToPropertyMap, converter, fetchRows, offsetRows, itemFactory);
        }

        /// <summary>
        /// Асинхронно выполняет SQL-запрос и возвращает результат в виде списка объектов типа <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Тип объектов, которые будут содержаться в списке.</typeparam>
        /// <param name="query">SQL-запрос для выполнения. Если <c>null</c>, будет использован стандартный запрос.</param>
        /// <param name="cmdParams">Параметры запроса.</param>
        /// <param name="columns">Список столбцов для выборки. Может быть <c>null</c>.</param>
        /// <param name="columnToPropertyMap">Отображение столбцов SQL-запроса в свойства объектов. Может быть <c>null</c>.</param>
        /// <param name="converter">Конвертер для преобразования данных. Может быть <c>null</c>.</param>
        /// <param name="fetchRows">Количество строк для выборки. По умолчанию -1 (выбираются все строки).</param>
        /// <param name="offsetRows">Количество строк для пропуска перед выборкой. По умолчанию - 0.</param>
        /// <param name="itemFactory">Функция для создания объектов типа <typeparamref name="T"/>. Может быть <c>null</c>.</param>
        /// <param name="ct">Токен отмены операции.</param>
        /// <returns>Задача, которая возвращает список объектов типа <typeparamref name="T"/>.</returns>
        /// <remarks>
        /// Этот метод выполняет SQL-запрос асинхронно и возвращает результат в виде списка объектов.
        /// Если <paramref name="itemFactory"/> не задан, используется стандартное преобразование данных в объекты.
        /// </remarks>
        public Task<List<T>> ToListAsync<T>(string query = null, object cmdParams = null,
            IEnumerable<string> columns = null, IEnumerable<(string, string)> columnToPropertyMap = null,
            DbValueConverter<T> converter = null, int fetchRows = -1, int offsetRows = 0, Func<object[], string[], T> itemFactory = null,
            CancellationToken ct = default)
        {
            return QueryAsync<List<T>, T>(query, cmdParams, columns, columnToPropertyMap, converter, fetchRows,
                offsetRows, itemFactory, ct);
        }

        /// <summary>
        /// Асинхронно выполняет SQL-запрос с фильтрацией и возвращает результат в виде списка объектов типа <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Тип объектов, которые будут содержаться в списке.</typeparam>
        /// <param name="whereExpression">Выражение для фильтрации данных.</param>
        /// <param name="columnToPropertyMap">Отображение столбцов SQL-запроса в свойства объектов. Может быть <c>null</c>.</param>
        /// <param name="converter">Конвертер для преобразования данных. Может быть <c>null</c>.</param>
        /// <param name="fetchRows">Количество строк для выборки. По умолчанию -1 (выбираются все строки).</param>
        /// <param name="offsetRows">Количество строк для пропуска перед выборкой. По умолчанию - 0.</param>
        /// <param name="itemFactory">Функция для создания объектов типа <typeparamref name="T"/>. Может быть <c>null</c>.</param>
        /// <param name="ct">Токен отмены операции.</param>
        /// <param name="orderByExpression">Выражение для сортировки. Может быть <c>null</c>.</param>
        /// <returns>Задача, которая возвращает список объектов типа <typeparamref name="T"/>.</returns>
        /// <remarks>
        /// Этот метод выполняет SQL-запрос асинхронно с фильтрацией и сортировкой, и возвращает результат в виде списка.
        /// </remarks>
        public Task<List<T>> ToListAsync<T>(Expression<Func<T, bool>> whereExpression,
            IEnumerable<(string, string)> columnToPropertyMap = null, DbValueConverter<T> converter = null,
            int fetchRows = -1, int offsetRows = 0, Func<object[], string[], T> itemFactory = null, CancellationToken ct = default,
            params (Expression<Func<T, object>>, bool)[] orderByExpression)
        {
            var query = (SqlQueryBuilder.GetSelectQuery<T>(Options) + " " + SqlQueryBuilder.GetWhereClause(whereExpression, Options, true, out var cmdParam) +
                         " " + SqlQueryBuilder.GetOrderBy(Options, orderByExpression)).Trim();

            return ToListAsync(query, cmdParam, null, columnToPropertyMap, converter, fetchRows, offsetRows, itemFactory,
                ct);
        }

        #endregion ToList

        #region Aggs

        /// <summary>
        /// Получает количество страниц для данных с учетом заданного размера страницы.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых будет вычислено количество страниц.</typeparam>
        /// <param name="pageSize">Размер страницы (количество элементов на странице).</param>
        /// <returns>Общее количество страниц.</returns>
        /// <remarks>
        /// Этот метод выполняет подсчет общего числа строк и делит их на страницы в зависимости от заданного размера страницы.
        /// </remarks>
        public int GetPagesCount<TFrom>(int pageSize) where TFrom : class
        {
            var numbers = Agg<TFrom>((null, "count"));
            var rowsCount = Convert.ToInt32(numbers.Values.FirstOrDefault());
            var pagesCount = (int)Math.Ceiling((double)rowsCount / pageSize);
            return pagesCount;
        }

        /// <summary>
        /// Асинхронно получает количество страниц для данных с учетом заданного размера страницы.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых будет вычислено количество страниц.</typeparam>
        /// <param name="pageSize">Размер страницы (количество элементов на странице).</param>
        /// <param name="token">Токен отмены асинхронной операции.</param>
        /// <returns>Задача, которая возвращает количество страниц.</returns>
        /// <remarks>
        /// Этот метод выполняет асинхронный подсчет общего числа строк и делит их на страницы в зависимости от заданного размера страницы.
        /// </remarks>
        public async Task<int> GetPagesCountAsync<TFrom>(int pageSize, CancellationToken token = default) where TFrom : class
        {
            var numbers = await AggAsync<TFrom>(token, (null, "count")).ConfigureAwait(ConfigureAwait);
            var rowsCount = Convert.ToInt32(numbers.Values.FirstOrDefault());
            var pagesCount = (int)Math.Ceiling((double)rowsCount / pageSize);
            return pagesCount;
        }

        /// <summary>
        /// Получает словарь страниц с информацией о смещении и количестве элементов для каждой страницы.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых будет получен список страниц.</typeparam>
        /// <param name="pageSize">Размер страницы (количество элементов на странице).</param>
        /// <returns>Словарь с ключом — номер страницы, значением — кортеж с смещением и количеством элементов на странице.</returns>
        /// <remarks>
        /// Этот метод разбивает данные на страницы с учетом заданного размера страницы.
        /// </remarks>
        public Dictionary<int, (int offset, int count)> GetPages<TFrom>(int pageSize) where TFrom : class
        {
            if (pageSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(pageSize));

            var total = Count<TFrom, long>();
            var pagesCount = (int)Math.Ceiling(total / (double)pageSize);

            var pages = new Dictionary<int, (int offset, int count)>(pagesCount);

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
        /// <returns>Задача, которая возвращает словарь с номером страницы в качестве ключа и кортежем с смещением и количеством элементов.</returns>
        /// <remarks>
        /// Этот метод асинхронно разбивает данные на страницы с учетом заданного размера страницы.
        /// </remarks>
        public async Task<Dictionary<int, (int offset, int count)>> GetPagesAsync<TFrom>(int pageSize, CancellationToken token = default) where TFrom : class
        {
            if (pageSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(pageSize));

            var total = await CountAsync<TFrom, long>(token: token).ConfigureAwait(ConfigureAwait);
            var pagesCount = (int)Math.Ceiling(total / (double)pageSize);

            var pages = new Dictionary<int, (int offset, int count)>(pagesCount);

            for (var page = 1; page <= pagesCount; page++)
            {
                var offset = (page - 1) * pageSize;
                var count = Math.Min(pageSize, total - offset);

                pages[page] = (offset, (int)count);
            }

            return pages;
        }

        /// <summary>
        /// Получает общее количество строк для выполненного SQL-запроса.
        /// </summary>
        /// <param name="cmd">Команда, для которой будет выполнен подсчет строк.</param>
        /// <returns>Общее количество строк в результате запроса.</returns>
        /// <remarks>
        /// Этот метод выполняет подсчет количества строк в запросе, оборачивая его в подзапрос с использованием SQL-функции COUNT.
        /// </remarks>
        public object Count(IDbCommand cmd)
        {
            cmd.CommandText = $"SELECT COUNT(*) FROM ({cmd.CommandText}) AS CountTable";
            return ExecuteScalar(cmd);
        }

        /// <summary>
        /// Асинхронно выполняет подсчет строк в выполненном SQL-запросе.
        /// </summary>
        /// <param name="cmd">Команда, для которой будет выполнен подсчет строк.</param>
        /// <returns>Задача, которая возвращает количество строк в результате запроса.</returns>
        /// <remarks>
        /// Этот метод асинхронно выполняет подсчет количества строк в запросе, оборачивая его в подзапрос с использованием SQL-функции COUNT.
        /// </remarks>
        public Task<object> CountAsync(IDbCommand cmd)
        {
            cmd.CommandText = $"SELECT COUNT(*) FROM ({cmd.CommandText}) AS CountTable";
            return ExecuteScalarAsync(cmd);
        }

        /// <summary>
        /// Асинхронно выполняет подсчет строк в SQL-запросе.
        /// </summary>
        /// <param name="query">SQL-запрос для выполнения.</param>
        /// <param name="token">Токен отмены операции.</param>
        /// <returns>Задача, которая возвращает количество строк в результате запроса.</returns>
        /// <remarks>
        /// Этот метод выполняет асинхронный подсчет строк в SQL-запросе, оборачивая его в подзапрос с использованием SQL-функции COUNT.
        /// </remarks>
        public Task<object> CountAsync(string query, CancellationToken token = default)
        {
            query = $"SELECT COUNT(*) FROM ({query}) AS {Options.NamePrefix}CountTable{Options.NameSuffix}";
            return ExecuteScalarAsync(query, token: token);
        }

        /// <summary>
        /// Получает количество строк для данных в сущности с указанной колонкой.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых будет подсчитано количество строк.</typeparam>
        /// <param name="columnSelector">Выражение для выбора колонки для подсчета.</param>
        /// <returns>Общее количество строк в указанной колонке.</returns>
        /// <remarks>
        /// Этот метод выполняет агрегацию данных с использованием SQL-функции COUNT для конкретной колонки в сущности.
        /// </remarks>
        public object Count(string query)
        {
            query = $"SELECT COUNT(*) FROM ({query}) AS CountTable";
            return  ExecuteScalar(query);
        }

        /// <summary>
        /// Получает количество строк для данных в сущности с указанной колонкой.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых будет подсчитано количество строк.</typeparam>
        /// <typeparam name="T">Тип возвращаемого значения.</typeparam>
        /// <param name="columnSelector">Выражение для выбора колонки для подсчета.</param>
        /// <returns>Общее количество строк в указанной колонке, приведенное к типу <typeparamref name="T"/>.</returns>
        /// <remarks>
        /// Этот метод выполняет агрегацию данных с использованием SQL-функции COUNT для конкретной колонки в сущности и преобразует результат в тип <typeparamref name="T"/>.
        /// </remarks>
        public object Count<TFrom>(Expression<Func<TFrom, object>> columnSelector = null) where TFrom : class
        {
            var total = Agg("count", columnSelector).Values.FirstOrDefault();
            return total;
        }

        /// <summary>
        /// Получает количество строк для данных в сущности с указанной колонкой.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых будет подсчитано количество строк.</typeparam>
        /// <typeparam name="T">Тип возвращаемого значения.</typeparam>
        /// <param name="columnSelector">Выражение для выбора колонки для подсчета.</param>
        /// <returns>Общее количество строк в указанной колонке, приведенное к типу <typeparamref name="T"/>.</returns>
        /// <remarks>
        /// Этот метод выполняет агрегацию данных с использованием SQL-функции COUNT для конкретной колонки в сущности и преобразует результат в тип <typeparamref name="T"/>.
        /// </remarks>
        public T Count<TFrom, T>(Expression<Func<TFrom, object>> columnSelector = null) where TFrom : class
        {
            var total = Count(columnSelector);
            return ChangeType<T>(total);
        }

        /// <summary>
        /// Асинхронно получает количество строк для данных в сущности с указанной колонкой.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых будет подсчитано количество строк.</typeparam>
        /// <param name="columnSelector">Выражение для выбора колонки для подсчета.</param>
        /// <param name="token">Токен отмены операции.</param>
        /// <returns>Задача, которая возвращает общее количество строк в указанной колонке.</returns>
        /// <remarks>
        /// Этот метод выполняет асинхронный подсчет строк для конкретной колонки с использованием SQL-функции COUNT.
        /// </remarks>
        public async Task<object> CountAsync<TFrom>(Expression<Func<TFrom, object>> columnSelector = null, CancellationToken token = default) where TFrom : class
        {
            return (await AggAsync("count", token, columnSelector).ConfigureAwait(ConfigureAwait)).Values.FirstOrDefault();
        }

        /// <summary>
        /// Асинхронно получает количество строк для данных в сущности с указанной колонкой и преобразует результат в тип <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых будет подсчитано количество строк.</typeparam>
        /// <typeparam name="T">Тип возвращаемого значения.</typeparam>
        /// <param name="columnSelector">Выражение для выбора колонки для подсчета.</param>
        /// <param name="token">Токен отмены операции.</param>
        /// <returns>Задача, которая возвращает количество строк в указанной колонке, приведенное к типу <typeparamref name="T"/>.</returns>
        /// <remarks>
        /// Этот метод выполняет асинхронный подсчет строк для конкретной колонки с использованием SQL-функции COUNT и преобразует результат в тип <typeparamref name="T"/>.
        /// </remarks>
        public async Task<T> CountAsync<TFrom, T>(Expression<Func<TFrom, object>> columnSelector = null, CancellationToken token = default) where TFrom : class
        {
            var total = await CountAsync(columnSelector, token).ConfigureAwait(ConfigureAwait);
            return ChangeType<T>(total);
        }

        /// <summary>
        /// Получает максимальное значение для указанного столбца.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых вычисляется максимальное значение.</typeparam>
        /// <typeparam name="T">Тип, в который будет преобразовано максимальное значение.</typeparam>
        /// <param name="columnSelector">Выражение для выбора столбца, для которого будет вычислено максимальное значение.</param>
        /// <returns>Максимальное значение для указанного столбца, преобразованное в тип <typeparamref name="T"/>.</returns>
        /// <remarks>
        /// Этот метод использует SQL-функцию MAX для получения максимального значения в столбце, а затем преобразует результат в тип <typeparamref name="T"/>.
        /// </remarks>
        public T Max<TFrom, T>(Expression<Func<TFrom, object>> columnSelector) where TFrom : class
        {
            var total = Max(columnSelector);
            return ChangeType<T>(total);
        }

        /// <summary>
        /// Получает максимальное значение для указанного столбца.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых вычисляется максимальное значение.</typeparam>
        /// <param name="columnSelector">Выражение для выбора столбца, для которого будет вычислено максимальное значение.</param>
        /// <returns>Максимальное значение для указанного столбца.</returns>
        /// <remarks>
        /// Этот метод использует SQL-функцию MAX для получения максимального значения в столбце.
        /// </remarks>
        public object Max<TFrom>(Expression<Func<TFrom, object>> columnSelector) where TFrom : class
        {
            return Agg("MAX", columnSelector).Values.FirstOrDefault();
        }

        /// <summary>
        /// Асинхронно получает максимальное значение для указанного столбца.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых вычисляется максимальное значение.</typeparam>
        /// <typeparam name="T">Тип, в который будет преобразовано максимальное значение.</typeparam>
        /// <param name="columnSelector">Выражение для выбора столбца, для которого будет вычислено максимальное значение.</param>
        /// <param name="token">Токен отмены асинхронной операции.</param>
        /// <returns>Задача, которая возвращает максимальное значение для указанного столбца, преобразованное в тип <typeparamref name="T"/>.</returns>
        /// <remarks>
        /// Этот метод асинхронно использует SQL-функцию MAX для получения максимального значения и преобразует результат в тип <typeparamref name="T"/>.
        /// </remarks>
        public async Task<T> MaxAsync<TFrom, T>(Expression<Func<TFrom, object>> columnSelector, CancellationToken token = default) where TFrom : class
        {
            var total = await MaxAsync(columnSelector, token).ConfigureAwait(ConfigureAwait);
            return ChangeType<T>(total);
        }

        /// <summary>
        /// Асинхронно получает максимальное значение для указанного столбца.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых вычисляется максимальное значение.</typeparam>
        /// <param name="columnSelector">Выражение для выбора столбца, для которого будет вычислено максимальное значение.</param>
        /// <param name="token">Токен отмены асинхронной операции.</param>
        /// <returns>Задача, которая возвращает максимальное значение для указанного столбца.</returns>
        /// <remarks>
        /// Этот метод асинхронно использует SQL-функцию MAX для получения максимального значения в столбце.
        /// </remarks>
        public async Task<object> MaxAsync<TFrom>(Expression<Func<TFrom, object>> columnSelector, CancellationToken token = default) where TFrom : class
        {
            return (await AggAsync("MAX", token, columnSelector).ConfigureAwait(ConfigureAwait)).Values.FirstOrDefault();
        }

        /// <summary>
        /// Получает минимальное значение для указанного столбца.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых вычисляется минимальное значение.</typeparam>
        /// <typeparam name="T">Тип, в который будет преобразовано минимальное значение.</typeparam>
        /// <param name="columnSelector">Выражение для выбора столбца, для которого будет вычислено минимальное значение.</param>
        /// <returns>Минимальное значение для указанного столбца, преобразованное в тип <typeparamref name="T"/>.</returns>
        /// <remarks>
        /// Этот метод использует SQL-функцию MIN для получения минимального значения в столбце, а затем преобразует результат в тип <typeparamref name="T"/>.
        /// </remarks>
        public T Min<TFrom, T>(Expression<Func<TFrom, object>> columnSelector) where TFrom : class
        {
            var total = Min(columnSelector);
            return ChangeType<T>(total);
        }

        /// <summary>
        /// Получает минимальное значение для указанного столбца.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых вычисляется минимальное значение.</typeparam>
        /// <param name="columnSelector">Выражение для выбора столбца, для которого будет вычислено минимальное значение.</param>
        /// <returns>Минимальное значение для указанного столбца.</returns>
        /// <remarks>
        /// Этот метод использует SQL-функцию MIN для получения минимального значения в столбце.
        /// </remarks>
        public object Min<TFrom>(Expression<Func<TFrom, object>> columnSelector) where TFrom : class
        {
            return Agg("MIN", columnSelector).Values.FirstOrDefault();
        }

        /// <summary>
        /// Асинхронно получает минимальное значение для указанного столбца.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых вычисляется минимальное значение.</typeparam>
        /// <typeparam name="T">Тип, в который будет преобразовано минимальное значение.</typeparam>
        /// <param name="columnSelector">Выражение для выбора столбца, для которого будет вычислено минимальное значение.</param>
        /// <param name="token">Токен отмены асинхронной операции.</param>
        /// <returns>Задача, которая возвращает минимальное значение для указанного столбца, преобразованное в тип <typeparamref name="T"/>.</returns>
        /// <remarks>
        /// Этот метод асинхронно использует SQL-функцию MIN для получения минимального значения и преобразует результат в тип <typeparamref name="T"/>.
        /// </remarks>
        public async Task<T> MinAsync<TFrom, T>(Expression<Func<TFrom, object>> columnSelector, CancellationToken token = default) where TFrom : class
        {
            var total = await MinAsync(columnSelector, token).ConfigureAwait(ConfigureAwait);
            return ChangeType<T>(total);
        }

        /// <summary>
        /// Асинхронно получает минимальное значение для указанного столбца.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых вычисляется минимальное значение.</typeparam>
        /// <param name="columnSelector">Выражение для выбора столбца, для которого будет вычислено минимальное значение.</param>
        /// <param name="token">Токен отмены асинхронной операции.</param>
        /// <returns>Задача, которая возвращает минимальное значение для указанного столбца.</returns>
        /// <remarks>
        /// Этот метод асинхронно использует SQL-функцию MIN для получения минимального значения в столбце.
        /// </remarks>
        public async Task<object> MinAsync<TFrom>(Expression<Func<TFrom, object>> columnSelector, CancellationToken token = default) where TFrom : class
        {
            return (await AggAsync("MIN", token, columnSelector).ConfigureAwait(ConfigureAwait)).Values.FirstOrDefault();
        }

        /// <summary>
        /// Получает сумму для указанного столбца.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых вычисляется сумма.</typeparam>
        /// <typeparam name="T">Тип, в который будет преобразована сумма.</typeparam>
        /// <param name="columnSelector">Выражение для выбора столбца, для которого будет вычислена сумма.</param>
        /// <returns>Сумма для указанного столбца, преобразованная в тип <typeparamref name="T"/>.</returns>
        /// <remarks>
        /// Этот метод использует SQL-функцию SUM для получения суммы значений в столбце, а затем преобразует результат в тип <typeparamref name="T"/>.
        /// </remarks>
        public T Sum<TFrom, T>(Expression<Func<TFrom, object>> columnSelector) where TFrom : class
        {
            var total = Sum(columnSelector);
            return ChangeType<T>(total);
        }

        /// <summary>
        /// Получает сумму для указанного столбца.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых вычисляется сумма.</typeparam>
        /// <param name="columnSelector">Выражение для выбора столбца, для которого будет вычислена сумма.</param>
        /// <returns>Сумма для указанного столбца.</returns>
        /// <remarks>
        /// Этот метод использует SQL-функцию SUM для получения суммы значений в столбце.
        /// </remarks>
        public object Sum<TFrom>(Expression<Func<TFrom, object>> columnSelector) where TFrom : class
        {
            return Agg("SUM", columnSelector).Values.FirstOrDefault();
        }

        /// <summary>
        /// Асинхронно получает сумму для указанного столбца.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых вычисляется сумма.</typeparam>
        /// <typeparam name="T">Тип, в который будет преобразована сумма.</typeparam>
        /// <param name="columnSelector">Выражение для выбора столбца, для которого будет вычислена сумма.</param>
        /// <param name="token">Токен отмены асинхронной операции.</param>
        /// <returns>Задача, которая возвращает сумму для указанного столбца, преобразованную в тип <typeparamref name="T"/>.</returns>
        /// <remarks>
        /// Этот метод асинхронно использует SQL-функцию SUM для получения суммы значений в столбце и преобразует результат в тип <typeparamref name="T"/>.
        /// </remarks>
        public async Task<T> SumAsync<TFrom, T>(Expression<Func<TFrom, object>> columnSelector, CancellationToken token = default) where TFrom : class
        {
            var total = await SumAsync(columnSelector, token).ConfigureAwait(ConfigureAwait);
            return ChangeType<T>(total);
        }

        /// <summary>
        /// Асинхронно получает сумму для указанного столбца.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых вычисляется сумма.</typeparam>
        /// <param name="columnSelector">Выражение для выбора столбца, для которого будет вычислена сумма.</param>
        /// <param name="token">Токен отмены асинхронной операции.</param>
        /// <returns>Задача, которая возвращает сумму для указанного столбца.</returns>
        /// <remarks>
        /// Этот метод асинхронно использует SQL-функцию SUM для получения суммы значений в столбце.
        /// </remarks>
        public async Task<object> SumAsync<TFrom>(Expression<Func<TFrom, object>> columnSelector, CancellationToken token = default) where TFrom : class
        {
            return (await AggAsync("SUM", token, columnSelector).ConfigureAwait(ConfigureAwait)).Values.FirstOrDefault();
        }

        /// <summary>
        /// Получает среднее значение для указанного столбца.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых вычисляется среднее значение.</typeparam>
        /// <typeparam name="T">Тип, в который будет преобразовано среднее значение.</typeparam>
        /// <param name="columnSelector">Выражение для выбора столбца, для которого будет вычислено среднее значение.</param>
        /// <returns>Среднее значение для указанного столбца, преобразованное в тип <typeparamref name="T"/>.</returns>
        /// <remarks>
        /// Этот метод использует SQL-функцию AVG для получения среднего значения в столбце, а затем преобразует результат в тип <typeparamref name="T"/>.
        /// </remarks>
        public T Avg<TFrom, T>(Expression<Func<TFrom, object>> columnSelector) where TFrom : class
        {
            var total = Avg(columnSelector);
            return ChangeType<T>(total);
        }

        /// <summary>
        /// Получает среднее значение для указанного столбца.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых вычисляется среднее значение.</typeparam>
        /// <param name="columnSelector">Выражение для выбора столбца, для которого будет вычислено среднее значение.</param>
        /// <returns>Среднее значение для указанного столбца.</returns>
        /// <remarks>
        /// Этот метод использует SQL-функцию AVG для получения среднего значения в столбце.
        /// </remarks>
        public object Avg<TFrom>(Expression<Func<TFrom, object>> columnSelector) where TFrom : class
        {
            return Agg("AVG", columnSelector).Values.FirstOrDefault();
        }

        /// <summary>
        /// Асинхронно получает среднее значение для указанного столбца.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых вычисляется среднее значение.</typeparam>
        /// <param name="columnSelector">Выражение для выбора столбца, для которого будет вычислено среднее значение.</param>
        /// <param name="token">Токен отмены асинхронной операции.</param>
        /// <returns>Задача, которая возвращает среднее значение для указанного столбца.</returns>
        /// <remarks>
        /// Этот метод асинхронно использует SQL-функцию AVG для получения среднего значения в столбце.
        /// </remarks>
        public async Task<object> AvgAsync<TFrom>(Expression<Func<TFrom, object>> columnSelector, CancellationToken token = default) where TFrom : class
        {
            return (await AggAsync("AVG", token, columnSelector).ConfigureAwait(ConfigureAwait)).Values.FirstOrDefault();
        }

        /// <summary>
        /// Получает агрегационные значения для указанных столбцов: количество, минимальное, максимальное, сумма и среднее.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых вычисляются агрегации.</typeparam>
        /// <param name="columnSelector">Выражения для выбора столбцов, для которых будут вычислены агрегации.</param>
        /// <returns>Словарь, где ключом является имя столбца, а значением кортеж с результатами агрегаций (Count, Min, Max, Sum, Avg).</returns>
        /// <remarks>
        /// Этот метод выполняет несколько агрегационных операций (COUNT, MIN, MAX, SUM, AVG) для каждого указанного столбца и возвращает результаты в виде словаря.
        /// </remarks>
        public Dictionary<string, (long Count, long Min, long Max, long Sum, decimal Avg)> GetAggs<TFrom>(params Expression<Func<TFrom, object>>[] columnSelector) where TFrom : class
        {
            var colNames = columnSelector.Select(x => x.GetMemberCache().ColumnName).ToArray();
            var queryExpression = new List<(Expression<Func<TFrom, object>>, string)>();
            foreach (var cs in columnSelector)
            {
                queryExpression.Add((cs, "COUNT"));
                queryExpression.Add((cs, "MIN"));
                queryExpression.Add((cs, "MAX"));
                queryExpression.Add((cs, "SUM"));
                queryExpression.Add((cs, "AVG"));
            }

            var result = Agg(queryExpression.ToArray());

            var dic = colNames.Select((x, i)=> (x,
                (
                    ChangeType<long>(result[$"{x}COUNT"]),
                    ChangeType<long>(result[$"{x}MIN"]),
                    ChangeType<long>(result[$"{x}MAX"]),
                    ChangeType<long>(result[$"{x}SUM"]),
                    ChangeType<decimal>(result[$"{x}AVG"])))).ToDictionary(key => key.x, val => val.Item2);

            return dic;
        }

        /// <summary>
        /// Асинхронно получает агрегационные значения для указанных столбцов: количество, минимальное, максимальное, сумма и среднее.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых вычисляются агрегации.</typeparam>
        /// <param name="token">Токен отмены асинхронной операции.</param>
        /// <param name="columnSelector">Выражения для выбора столбцов, для которых будут вычислены агрегации.</param>
        /// <returns>Задача, которая возвращает словарь с агрегационными значениями для столбцов.</returns>
        /// <remarks>
        /// Этот метод асинхронно выполняет несколько агрегационных операций (COUNT, MIN, MAX, SUM, AVG) для каждого указанного столбца.
        /// </remarks>
        public async Task<Dictionary<string, (long Count, long Min, long Max, long Sum, decimal Avg)>> GetAggsAsync<TFrom>(CancellationToken token = default, params Expression<Func<TFrom, object>>[] columnSelector) where TFrom : class
        {
            var colNames = columnSelector.Select(x => x.GetMemberCache().ColumnName).ToArray();
            var queryExpression = new List<(Expression<Func<TFrom, object>>, string)>();
            foreach (var cs in columnSelector)
            {
                queryExpression.Add((cs, "COUNT"));
                queryExpression.Add((cs, "MIN"));
                queryExpression.Add((cs, "MAX"));
                queryExpression.Add((cs, "SUM"));
                queryExpression.Add((cs, "AVG"));
            }

            var result = await AggAsync(token, queryExpression.ToArray()).ConfigureAwait(ConfigureAwait);

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
        /// Выполняет агрегационные функции для указанных столбцов с одним агрегатным выражением (например, COUNT).
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых выполняются агрегации.</typeparam>
        /// <param name="aggFunction">Агрегатная функция (например, COUNT, MIN, MAX, SUM, AVG).</param>
        /// <param name="columnSelectors">Выражения для выбора столбцов, для которых будет выполнена агрегация.</param>
        /// <returns>Словарь с результатами агрегации для указанных столбцов.</returns>
        /// <remarks>
        /// Этот метод выполняет одну агрегационную функцию (например, COUNT) для каждого указанного столбца и возвращает результат в виде словаря.
        /// </remarks>
        public Dictionary<string, object> Agg<TFrom>(string aggFunction, params Expression<Func<TFrom, object>>[] columnSelectors) where TFrom : class
        {
            return Agg(columnSelectors?.Any() == true
                ? columnSelectors.Select(c => (c, aggFunction)).ToArray()
                : new[] { ((Expression<Func<TFrom, object>>)null, aggFunction) });
        }

        /// <summary>
        /// Асинхронно выполняет агрегационные функции для указанных столбцов с одним агрегатным выражением (например, COUNT).
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых выполняются агрегации.</typeparam>
        /// <param name="aggFunction">Агрегатная функция (например, COUNT, MIN, MAX, SUM, AVG).</param>
        /// <param name="token">Токен отмены асинхронной операции.</param>
        /// <param name="columnSelectors">Выражения для выбора столбцов, для которых будет выполнена агрегация.</param>
        /// <returns>Задача, которая возвращает словарь с результатами агрегации для указанных столбцов.</returns>
        /// <remarks>
        /// Этот метод асинхронно выполняет одну агрегационную функцию (например, COUNT) для каждого указанного столбца.
        /// </remarks>
        public Task<Dictionary<string, object>> AggAsync<TFrom>(string aggFunction, CancellationToken token = default, params Expression<Func<TFrom, object>>[] columnSelectors) where TFrom : class
        {
            return AggAsync(token, columnSelectors?.Any() == true
                ? columnSelectors.Select(c => (c, aggFunction)).ToArray()
                : new[] { ((Expression<Func<TFrom, object>>)null, aggFunction) });
        }

        /// <summary>
        /// Выполняет агрегацию с несколькими агрегационными функциями для выбранных столбцов.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых выполняется агрегация.</typeparam>
        /// <param name="columnSelectors">Выражения для выбора столбцов, для которых будет выполнена агрегация.</param>
        /// <returns>Словарь с результатами агрегации.</returns>
        /// <remarks>
        /// Этот метод позволяет выбрать несколько столбцов и применить различные агрегационные функции (например, COUNT, MIN, MAX, SUM, AVG).
        /// </remarks>
        public Dictionary<string, object> Agg<TFrom>(params (Expression<Func<TFrom, object>> column, string aggFunction)[] columnSelectors) where TFrom : class
        {
            var query = "SELECT " + (columnSelectors.Length == 0
                                      ? "COUNT(*)"
                                      : string.Join(", ",
                                          columnSelectors.Select(c =>
                                              $"{c.aggFunction}(\"{c.column?.GetMemberCache()?.ColumnName ?? "*"}\") AS \"{c.column?.GetMemberCache()?.ColumnName ?? "Total"}{c.aggFunction.ToUpper()}\""
                                                  .Replace("\"*\"", "*"))))
                                  + $" FROM \"{typeof(TFrom).GetMemberCache().TableName}\"";

            var table = ToDataTable(query);
            var result = new Dictionary<string, object>(IgnoreCaseComparer);
            foreach (DataColumn dc in table.Columns)
            {
                var value = table.Rows[0][dc.ColumnName];
                result[dc.ColumnName] = value;
            }

            return result;
        }

        /// <summary>
        /// Асинхронно выполняет агрегацию с несколькими агрегационными функциями для выбранных столбцов.
        /// </summary>
        /// <typeparam name="TFrom">Тип данных, для которых выполняется агрегация.</typeparam>
        /// <param name="token">Токен отмены асинхронной операции.</param>
        /// <param name="columnSelectors">Выражения для выбора столбцов, для которых будет выполнена агрегация.</param>
        /// <returns>Задача, которая возвращает словарь с результатами агрегации для выбранных столбцов.</returns>
        /// <remarks>
        /// Этот метод асинхронно выполняет агрегацию с несколькими агрегационными функциями для выбранных столбцов.
        /// </remarks>
        public async Task<Dictionary<string, object>> AggAsync<TFrom>(CancellationToken token = default, params(Expression<Func<TFrom, object>> column, string aggFunction)[] columnSelectors) where TFrom : class
        {
            var query = "SELECT " + (columnSelectors.Length == 0
                                      ? "COUNT(*)"
                                      : string.Join(", ",
                                          columnSelectors.Select(c =>
                                              $"{c.aggFunction}(\"{c.column?.GetMemberCache()?.ColumnName ?? "*"}\") AS \"{c.column?.GetMemberCache()?.ColumnName ?? "Total"}{c.aggFunction.ToUpper()}\""
                                                  .Replace("\"*\"", "*"))))
                                  + $" FROM \"{typeof(TFrom).GetMemberCache().TableName}\"";

            var table = await ToDataTableAsync(query, token: token).ConfigureAwait(ConfigureAwait);
            var result = new Dictionary<string, object>(IgnoreCaseComparer);
            foreach (DataColumn dc in table.Columns)
            {
                var value = table.Rows[0][dc.ColumnName];
                result[dc.ColumnName] = value;
            }

            return result;
        }

        /// <summary>
        /// Получает строку SQL-запроса с заменой всех параметров на их значения.
        /// </summary>
        /// <param name="command">Команда, содержащая SQL-запрос и параметры.</param>
        /// <returns>Строка SQL-запроса с подставленными значениями параметров.</returns>
        /// <exception cref="ArgumentNullException">Выбрасывается, если команду передано значение null.</exception>
        /// <remarks>
        /// Этот метод заменяет все параметры в SQL-запросе на их фактические значения, 
        /// что полезно для отладки или логирования SQL-запросов с параметрами.
        /// </remarks>
        public string GetRawSql(IDbCommand command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            var sql = command.CommandText;

            foreach (IDbDataParameter parameter in command.Parameters)
            {
                var paramToken = Options.ParamPrefix + parameter.ParameterName;
                var literal = Options.ToSqlLiteral(parameter.Value);

                sql = ReplaceParameterToken(sql, paramToken, literal);
            }

            return sql;
        }

        #endregion

        #region Privates
        private static string ReplaceParameterToken(string sql, string token, string replacement)
        {
            return Regex.Replace(
                sql,
                $@"(?<![\w@]){Regex.Escape(token)}(?!\w)",
                replacement,
                RegexOptions.CultureInvariant);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!IsDisposed && disposing)
            {
                CloseConnection();
                Connection?.Dispose();
                IsDisposed = true;
            }
        }

        private void BeginConnection()
        {
            BeginConnection(Connection);
        }

        private void BeginConnection(IDbConnection connection)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            try
            {
                if (connection.State == ConnectionState.Broken) connection.Close();

                if (connection.State != ConnectionState.Open) connection.Open();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Не удалось открыть соединение с базой данных.", ex);
            }
        }

        private Task<IDbConnection> BeginConnectionAsync(CancellationToken token = default)
        {
            return BeginConnectionAsync(Connection, token);
        }

        private async Task<IDbConnection> BeginConnectionAsync(IDbConnection connection,
            CancellationToken token = default)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            try
            {
                if (connection.State == ConnectionState.Broken) connection.Close();

                if (connection.State == ConnectionState.Open) return connection;
                if (connection is DbConnection dc)
                    await dc.OpenAsync(token).ConfigureAwait(ConfigureAwait);
                else
                    connection.Open();

                return connection;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Не удалось открыть соединение с базой данных.", ex);
            }
        }

        private static T ChangeType<T>(object value)
        {
            return (T)ChangeType(value, typeof(T));
        }

        private static object ChangeType(object value, Type targetType)
        {
            return Obj.ChangeType(value, targetType);
        }

        private void CloseConnection()
        {
            CloseConnection(Connection);
        }

        private void CloseConnection(IDbConnection con)
        {
            if (_tr.Value != null)
                return;

            if (con == null)
                throw new NullReferenceException(nameof(con));

            if (con.State != ConnectionState.Closed)
                con.Close();
        }

        private Dictionary<int, string> GetReaderFieldToPropertyMap(IDataReader reader, IEnumerable<(string, string)> customMap = null, bool onlyFromCustomMap = true)
        {
            var customMapDic = customMap?.ToDictionary(k => k.Item1, v => v.Item2) ?? new Dictionary<string, string>();
            var map = new Dictionary<int, string>();

            var columnsCount = reader.FieldCount;

            for (var i = 0; i < columnsCount; i++)
            {
                var colIndex = i;
                var colName = reader.GetName(i);

                if (customMapDic.Count > 0 && customMapDic.TryGetValue(colName, out var mappedColumn))
                {
                    map[colIndex] = mappedColumn;
                    if (onlyFromCustomMap)
                        continue;
                }

                map[colIndex] = colName;
            }

            return map;
        }

        private Dictionary<int, MemberCache> GetReaderFieldToPropertyMap(Type itemType, IDataReader reader, IEnumerable<(string, string)> customMap = null, IEnumerable<string> columns = null)
        {
            var customMapDic = customMap?.ToDictionary(k => k.Item1, v => v.Item2) ?? new Dictionary<string, string>();
            var map = new Dictionary<int, MemberCache>();
            var typeInfoEx = MemberCache.Create(itemType);
            var columnsCount = reader.FieldCount;

            for (var i = 0; i < columnsCount; i++)
            {
                var colIndex = i;
                var colName = reader.GetName(i);
                MemberCache propInfoEx;
                if (customMap != null)
                {
                    propInfoEx = typeInfoEx.PublicBasicProperties.GetValueOrDefault(
                        customMapDic.GetValueOrDefault(colName, IgnoreCaseComparer), IgnoreCaseComparer);
                    if (propInfoEx == null)
                        continue;
                    map[colIndex] = propInfoEx;
                }

                propInfoEx = typeInfoEx.ColumnProperties.FirstOrDefault(x=>IgnoreCaseComparer.Equals(x.Value.ColumnName, colName)).Value;
                if (propInfoEx != null)
                {
                    map[colIndex] = propInfoEx;
                    continue;
                }

                propInfoEx = typeInfoEx.PublicBasicProperties.FirstOrDefault(x => IgnoreCaseComparer.Equals(x.Value.ColumnName, colName)).Value;

                if (propInfoEx != null)
                {
                    map[colIndex] = propInfoEx;
                    continue;
                }

                map.Remove(colIndex);
            }

            if (columns?.Any() != true) return map;
            var itemsToRemove = map.Where(kv => !columns.Contains(kv.Value.ColumnName)).Select(kv => kv.Key).ToList();
            foreach (var item in itemsToRemove)
            {
                map.Remove(item);
            }

            return map;
        }

        private InvalidOperationException HandleDbException(Exception ex, IDbCommand cmd,
            [CallerMemberName] string methodName = "")
        {
            var errorMessage = $"Ошибка в методе {methodName}. " +
                               $"Запрос: {cmd?.CommandText}. " +
                               $"Параметры: {string.Join(", ", cmd == null ? Array.Empty<string>() : cmd.Parameters.Cast<IDbDataParameter>().Select(p => $"{p.ParameterName}={p.Value}"))}";

            CommandFailed?.Invoke(cmd, ex);
            return new InvalidOperationException(errorMessage, ex);
        }

        [Conditional("DEBUG")]
        private void LogCommand(IDbCommand cmd)
        {
            Debug.WriteLine($"Executing SQL: {cmd.CommandText}");
            foreach (IDbDataParameter p in cmd.Parameters) Debug.WriteLine($"  {p.ParameterName} = {p.Value}");
        }

        private void RollbackTransaction()
        {
            if (_tr.Value == null)
                throw new InvalidOperationException("Транзакция не была начата.");

            _tr.Value?.Rollback();
        }

        private Func<object[], string[], T> BuildItemFactory<T>(MemberCache<T> itemTypeCache, IEnumerable<(string, string)> columnToPropertyMap)
        {
            var ctor = itemTypeCache.Constructors[0];
            var ctorParams = ctor.GetParameters();

            if (ctorParams.Length == 0)
                return (values, names) => itemTypeCache.DefaultConstructor();

            return (values, names) =>
            {
                if (ctorParams.Length > values.Length)
                    throw new InvalidOperationException(
                        $"Недостаточно значений для вызова конструктора типа {typeof(T).FullName}.");

                var args = new object[ctorParams.Length];

                var indexes = ctorParams
                    .Select(p =>
                        names.IndexOf(n =>
                            p.Name.Equals(
                                columnToPropertyMap?.FirstOrDefault(m => m.Item1 == n).Item2 ?? n,
                                StringComparison.OrdinalIgnoreCase)))
                    .ToArray();

                if (indexes.All(i => i >= 0))
                {
                    for (var i = 0; i < indexes.Length; i++)
                        args[i] = ChangeType(values[indexes[i]], ctorParams[i].ParameterType);
                }
                else
                {
                    for (var i = 0; i < ctorParams.Length; i++)
                        args[i] = ChangeType(values[i], ctorParams[i].ParameterType);
                }

                return (T)ctor.Invoke(args);
            };
        }

        private Func<object[], string[], object> BuildItemFactory(MemberCache itemTypeCache, IEnumerable<(string, string)> columnToPropertyMap)
        {
            var ctor = itemTypeCache.Constructors[0];
            var ctorParams = ctor.GetParameters();

            if (ctorParams.Length == 0)
                return (values, names) => itemTypeCache.DefaultConstructor();

            return (values, names) =>
            {
                if (ctorParams.Length > values.Length)
                    throw new InvalidOperationException(
                        $"Недостаточно значений для вызова конструктора типа {itemTypeCache.Type.FullName}.");

                var args = new object[ctorParams.Length];

                var indexes = ctorParams
                    .Select(p =>
                        names.IndexOf(n =>
                            p.Name.Equals(
                                columnToPropertyMap?.FirstOrDefault(m => m.Item1 == n).Item2 ?? n,
                                StringComparison.OrdinalIgnoreCase)))
                    .ToArray();

                if (indexes.All(i => i >= 0))
                {
                    for (var i = 0; i < indexes.Length; i++)
                        args[i] = ChangeType(values[indexes[i]], ctorParams[i].ParameterType);
                }
                else
                {
                    for (var i = 0; i < ctorParams.Length; i++)
                        args[i] = ChangeType(values[i], ctorParams[i].ParameterType);
                }

                return ctor.Invoke(args);
            };
        }

        private async Task<TList> ReadCoreAsync<TList, T>(DbDataReader reader, IEnumerable<string> columns, IEnumerable<(string, string)> columnToPropertyMap, DbValueConverter<T> converter, int fetchRows, Func<object[], string[], T> itemFactory, bool isAsync, CancellationToken ct) where TList : ICollection<T>, new()
        {
            var list = new TList();

            var itemTypeCache = MemberCache<T>.Create();
            var readerValues = new object[reader.FieldCount];
            var readerColumns = Enumerable.Range(0, reader.FieldCount)
                                          .Select(reader.GetName)
                                          .ToArray();

            var rowCount = 0;

            if (itemTypeCache.IsBasic)
            {
                var colIndex = columns?.Select(reader.GetOrdinal).FirstOrDefault() ?? 0;

                while (isAsync ? await reader.ReadAsync(ct).ConfigureAwait(ConfigureAwait) : reader.Read())
                {
                    if (fetchRows > 0 && rowCount >= fetchRows)
                        break;

                    var value = isAsync
                        ? await reader.GetFieldValueAsync<T>(colIndex, ct).ConfigureAwait(ConfigureAwait)
                        : reader.GetFieldValue<T>(colIndex);

                    list.Add(converter(readerColumns[0], value, null, value));
                    rowCount++;
                }

                return list;
            }

            var map = GetReaderFieldToPropertyMap(typeof(T), reader, columnToPropertyMap, columns);
            var valueConverter = converter ?? ValueConverter.ToTypedConverter<T>();

            while (isAsync ? await reader.ReadAsync(ct).ConfigureAwait(ConfigureAwait) : reader.Read())
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
                                $"Не удалось получить значение поля '{fieldName}' типа '{dataType?.FullName}'.", ex);
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
                            kv.Value.Setter(item, null);
                            continue;
                        }

                        var value = valueConverter(reader.GetName(kv.Key), raw, kv.Value, item);
                        kv.Value.Setter(item, value);
                    }
                }

                list.Add(item);
                rowCount++;
            }

            return list;
        }

        private async Task<IEnumerable<object>> ReadCoreAsync(Type returnType, DbDataReader reader, IEnumerable<string> columns, IEnumerable<(string, string)> columnToPropertyMap, DbValueConverter<object> converter, int fetchRows, Func<object[], string[], object> itemFactory, bool isAsync, CancellationToken ct)
        {
            var list = Obj.New(returnType) as IList;

            var itemTypeCache = (MemberCache)MemberCache.Create(returnType).ElementType;
            var readerValues = new object[reader.FieldCount];
            var readerColumns = Enumerable.Range(0, reader.FieldCount)
                                          .Select(reader.GetName)
                                          .ToArray();

            var rowCount = 0;

            if (itemTypeCache.IsBasic)
            {
                var colIndex = columns?.Select(reader.GetOrdinal).FirstOrDefault() ?? 0;

                while (isAsync ? await reader.ReadAsync(ct).ConfigureAwait(ConfigureAwait) : reader.Read())
                {
                    if (fetchRows > 0 && rowCount >= fetchRows)
                        break;

                    var value = isAsync
                        ? await reader.GetFieldValueAsync<object>(colIndex, ct).ConfigureAwait(ConfigureAwait)
                        : reader.GetFieldValue<object>(colIndex);

                    list.Add(converter(readerColumns[0], value, null, value));
                    rowCount++;
                }

                return (IEnumerable<object>)list;
            }

            var map = GetReaderFieldToPropertyMap(itemTypeCache, reader, columnToPropertyMap, columns);
            var valueConverter = converter ?? ValueConverter;

            while (isAsync ? await reader.ReadAsync(ct).ConfigureAwait(ConfigureAwait) : reader.Read())
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
                                $"Не удалось получить значение поля '{fieldName}' типа '{dataType?.FullName}'.", ex);
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
                            kv.Value.Setter(item, null);
                            continue;
                        }

                        var value = valueConverter(reader.GetName(kv.Key), raw, kv.Value, item);
                        kv.Value.Setter(item, value);
                    }
                }

                list.Add(item);
                rowCount++;
            }

            return (IEnumerable<object>)list;
        }

        #endregion Privates
    }
}