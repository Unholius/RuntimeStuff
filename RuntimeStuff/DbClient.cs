using RuntimeStuff.Builders;
using RuntimeStuff.Extensions;
using RuntimeStuff.Helpers;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace RuntimeStuff
{
    public partial class DbClient<T> : DbClient where T : IDbConnection, new()
    {
        private static readonly Cache<IDbConnection, DbClient<T>> _clientCache =
            new Cache<IDbConnection, DbClient<T>>(con => new DbClient<T>((T)con));


        public DbClient() : base(new T())
        {
        }

        public DbClient(T con) : base(con)
        {
        }

        public DbClient(string connectionString)
        {
            Connection = new T
            {
                ConnectionString = connectionString
            };
        }

        public new T Connection
        {
            get => (T)base.Connection;
            set => base.Connection = value;
        }

        public static DbClient<T> Create(string connectionString)
        {
            var con = new T() { ConnectionString = connectionString };
            var dbClient = _clientCache.Get(con);
            return dbClient;
        }

        public static DbClient<T> Create(T con)
        {
            var dbClient = _clientCache.Get(con);
            return dbClient;
        }
    }

    public partial class DbClient : IDisposable
    {
        private static readonly StringComparer IgnoreCaseComparer = StringComparer.OrdinalIgnoreCase;
        private readonly AsyncLocal<IDbTransaction> _tr = new AsyncLocal<IDbTransaction>();

        private bool _disposed;

        private static readonly Cache<IDbConnection, DbClient> _clientCache =
            new Cache<IDbConnection, DbClient>(con => new DbClient(con));

        public event Action<IDbCommand> CommandExecuted;
        public event Action<IDbCommand, Exception> CommandFailed;

        public DbClient()
        {
            ValueConverter = (fieldName, fieldValue, propInfo, item) => ChangeType(fieldValue is string s ? s.Trim() : fieldValue, propInfo.PropertyType);
        }

        public DbClient(IDbConnection con) : this()
        {
            Connection = con ?? throw new ArgumentNullException(nameof(con));
        }

        ~DbClient()
        {
            Dispose(false);
        }

        public IDbConnection Connection { get; set; }

        public delegate object DbValueConverter(string fieldName, object fieldValue, PropertyInfo propertyInfo, object item);

        public delegate object DbValueConverter<in T>(string fieldName, object fieldValue, PropertyInfo propertyInfo, T item);

        public DbValueConverter<object> ValueConverter { get; set; }

        public int DefaultCommandTimeout { get; set; } = 30;

        public bool IsDisposed => _disposed;

        public static DbClient<T> Create<T>(string connectionString) where T : IDbConnection, new()
        {
            var dbClient = DbClient<T>.Create(connectionString);
            return dbClient;
        }

        public static DbClient Create(IDbConnection connection)
        {
            var dbClient = _clientCache.Get(connection);
            return dbClient;
        }

        public static DbClient Create()
        {
            return new DbClient();
        }

        #region Insert

        public T Insert<T>(IDbTransaction dbTransaction = null, params Action<T>[] insertColumns) where T : class
        {
            var item = TypeHelper.New<T>();
            foreach (var a in insertColumns)
                a(item);
            Insert(item, dbTransaction: dbTransaction);
            return item;
        }

        public object Insert<T>(T item, string queryGetId = "SELECT SCOPE_IDENTITY()", IDbTransaction dbTransaction = null, params Expression<Func<T, object>>[] insertColumns) where T : class
        {
            object id = null;
            var query = SqlQueryBuilder.GetInsertQuery(insertColumns);
            if (string.IsNullOrWhiteSpace(queryGetId))
            {
                ExecuteNonQuery(query, GetParams(item), dbTransaction);
            }
            else
            {
                query += $"; {queryGetId}";
                id = ExecuteScalar<object>(query, GetParams(item));
                var mi = MemberCache<T>.Create();
                if (id != null && id != DBNull.Value && mi.PrimaryKeys.Count == 1)
                    mi.PrimaryKeys.First().Value.SetValue(item, TypeHelper.ChangeType(id, mi.PrimaryKeys.First().Value.PropertyType));
            }

            return id;
        }

        public Task<object> InsertAsync<T>(string queryGetId = "SELECT SCOPE_IDENTITY()", Action<T>[] insertColumns = null, IDbTransaction dbTransaction = null, CancellationToken token = default) where T : class
        {
            var item = TypeHelper.New<T>();
            if (insertColumns == null) return InsertAsync(item, queryGetId, null, dbTransaction, token);
            foreach (var a in insertColumns)
                a(item);
            return InsertAsync(item, queryGetId, null, dbTransaction, token);
        }

        public async Task<object> InsertAsync<T>(T item, string queryGetId = "SELECT SCOPE_IDENTITY()", Expression<Func<T, object>>[] insertColumns = null, IDbTransaction dbTransaction = null, CancellationToken token = default) where T : class
        {
            object id = null;
            var query = SqlQueryBuilder.GetInsertQuery(insertColumns);
            if (string.IsNullOrWhiteSpace(queryGetId))
            {
                await ExecuteNonQueryAsync(query, GetParams(item), dbTransaction, token);
            }
            else
            {
                query += $"; {queryGetId}";
                id = await ExecuteScalarAsync<object>(query, GetParams(item), dbTransaction, token);
                var mi = MemberCache<T>.Create();
                if (id != null && id != DBNull.Value && mi.PrimaryKeys.Count == 1)
                    mi.PrimaryKeys.First().Value.SetValue(item, TypeHelper.ChangeType(id, mi.PrimaryKeys.First().Value.PropertyType));
            }

            return id;
        }

        public int InsertRange<T>(IEnumerable<T> list, string queryGetId = "SELECT SCOPE_IDENTITY()", IDbTransaction dbTransaction = null, params Expression<Func<T, object>>[] insertColumns) where T : class
        {
            try
            {
                var count = 0;
                using (BeginTransaction())
                {
                    var query = SqlQueryBuilder.GetInsertQuery(insertColumns);
                    if (!string.IsNullOrWhiteSpace(queryGetId))
                        query += $"; {queryGetId}";
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
                            if (pk != null && id != null)
                            {
                                pk.SetValue(item, TypeHelper.ChangeType(id, pk.PropertyType));
                            }

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

        public async Task<int> InsertRangeAsync<T>(IEnumerable<T> list, string queryGetId = "SELECT SCOPE_IDENTITY()", Expression<Func<T, object>>[] insertColumns = null, IDbTransaction dbTransaction = null, CancellationToken token = default) where T : class
        {
            try
            {
                var count = 0;
                using (BeginTransaction())
                {
                    var query = SqlQueryBuilder.GetInsertQuery(insertColumns);
                    if (!string.IsNullOrWhiteSpace(queryGetId))
                        query += $"; {queryGetId}";
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

                            var id = await dbCmd.ExecuteScalarAsync(token);
                            CommandExecuted?.Invoke(cmd);
                            if (pk != null && id != null)
                            {
                                pk.SetValue(item, TypeHelper.ChangeType(id, pk.PropertyType));
                            }

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

        public int Update<T>(T item, IDbTransaction dbTransaction = null, params Expression<Func<T, object>>[] updateColumns) where T : class
        {
            return Update(item, (Expression<Func<T, bool>>)null, updateColumns);
        }

        public int Update<T>(T item, Expression<Func<T, bool>> whereExpression, params Expression<Func<T, object>>[] updateColumns) where T : class
        {
            var query = SqlQueryBuilder.GetUpdateQuery(updateColumns);
            query += " " + (whereExpression != null ? SqlQueryBuilder.GetWhereClause(whereExpression)
                                                    : SqlQueryBuilder.GetWhereClause<T>());

            return ExecuteNonQuery(query, GetParams(item));
        }

        public Task<int> UpdateAsync<T>(T item, Expression<Func<T, object>>[] updateColumns = null, IDbTransaction dbTransaction = null, CancellationToken token = default) where T : class
        {
            return UpdateAsync(item, null, updateColumns ?? Array.Empty<Expression<Func<T, object>>>(), dbTransaction, token: token);
        }

        public Task<int> UpdateAsync<T>(T item, Expression<Func<T, bool>> whereExpression, Expression<Func<T, object>>[] updateColumns = null, IDbTransaction dbTransaction = null, CancellationToken token = default) where T : class
        {
            var query = SqlQueryBuilder.GetUpdateQuery(updateColumns);
            query += " " + (whereExpression != null ? SqlQueryBuilder.GetWhereClause(whereExpression)
                                                    : SqlQueryBuilder.GetWhereClause<T>());

            return ExecuteNonQueryAsync(query, GetParams(item), dbTransaction, token);
        }

        public int UpdateRange<T>(IEnumerable<T> list, IDbTransaction dbTransaction = null, params Expression<Func<T, object>>[] updateColumns) where T : class
        {
            try
            {
                var count = 0;
                using (BeginTransaction())
                {
                    var query = SqlQueryBuilder.GetUpdateQuery(updateColumns);
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

        public async Task<int> UpdateRangeAsync<T>(IEnumerable<T> list, Expression<Func<T, object>>[] updateColumns = null, IDbTransaction dbTransaction = null, CancellationToken token = default) where T : class
        {
            try
            {
                var count = 0;
                using (BeginTransaction())
                {
                    var query = SqlQueryBuilder.GetUpdateQuery(updateColumns);
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
                            count += await dbCmd.ExecuteNonQueryAsync(token);
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

        public int Delete<T>(Expression<Func<T, bool>> whereExpression) where T : class
        {
            var query = (SqlQueryBuilder.GetDeleteQuery<T>() + " " + SqlQueryBuilder.GetWhereClause(whereExpression)).Trim();
            return ExecuteNonQuery(query);
        }

        public int Delete<T>(T item) where T : class
        {
            var query = (SqlQueryBuilder.GetDeleteQuery<T>() + " " + SqlQueryBuilder.GetWhereClause<T>()).Trim();
            return ExecuteNonQuery(query, GetParams(item));
        }

        public Task<int> DeleteAsync<T>(T item, IDbTransaction dbTransaction = null, CancellationToken token = default) where T : class
        {
            var query = (SqlQueryBuilder.GetDeleteQuery<T>() + " " + SqlQueryBuilder.GetWhereClause<T>()).Trim();
            return ExecuteNonQueryAsync(query, GetParams(item), dbTransaction, token);
        }

        public Task<int> DeleteAsync<T>(Expression<Func<T, bool>> whereExpression, IDbTransaction dbTransaction, CancellationToken token = default) where T : class
        {
            var query = (SqlQueryBuilder.GetDeleteQuery<T>() + " " + SqlQueryBuilder.GetWhereClause(whereExpression)).Trim();
            return ExecuteNonQueryAsync(query, ((string, object)[])null, dbTransaction, token);
        }

        public async Task<int> DeleteRangeAsync<T>(IEnumerable<T> list, IDbTransaction dbTransaction, CancellationToken token = default) where T : class
        {
            try
            {
                var count = 0;
                using (BeginTransaction())
                {
                    foreach (var item in list)
                    {
                        count += await DeleteAsync(item, dbTransaction, token);
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

        #endregion Delete

        #region Transaction

        public IDbTransaction BeginTransaction(IsolationLevel level = IsolationLevel.ReadCommitted)
        {
            if (_tr.Value != null)
                throw new InvalidOperationException("Транзакция уже была начата.");

            BeginConnection();
            _tr.Value = Connection.BeginTransaction(level);
            return _tr.Value;
        }

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

        public async Task<int> ExecuteNonQueryAsync(string query, object cmdParams = null, IDbTransaction dbTransaction = null, CancellationToken token = default)
        {
            using (var cmd = (DbCommand)CreateCommand(query, cmdParams, dbTransaction))
            {
                try
                {
                    await BeginConnectionAsync(token);
                    var i = await cmd.ExecuteNonQueryAsync(token);
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

        public object ExecuteScalar(string query, object cmdParams = null, IDbTransaction dbTransaction = null)
        {
            return ExecuteScalar<object>(query, cmdParams, dbTransaction);
        }

        public TProp ExecuteScalar<T, TProp>(Expression<Func<T, TProp>> propertySelector, Expression<Func<T, bool>> whereExpression)
        {
            var query = (SqlQueryBuilder.GetSelectQuery<T, TProp>(propertySelector) + " " + SqlQueryBuilder.GetWhereClause(whereExpression)).Trim();
            return ExecuteScalar<TProp>(query);
        }

        public T ExecuteScalar<T>(string query, object cmdParams = null, IDbTransaction dbTransaction = null)
        {
            using (var cmd = CreateCommand(query, cmdParams, dbTransaction))
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

        public Task<object> ExecuteScalarAsync(string query, object cmdParams = null, IDbTransaction dbTransaction = null, CancellationToken token = default)
        {
            return ExecuteScalarAsync<object>(query, cmdParams, dbTransaction, token);
        }

        public Task<TProp> ExecuteScalarAsync<T, TProp>(Expression<Func<T, TProp>> propertySelector, Expression<Func<T, bool>> whereExpression, CancellationToken token = default)
        {
            var query = (SqlQueryBuilder.GetSelectQuery<T, TProp>(propertySelector) + " " + SqlQueryBuilder.GetWhereClause(whereExpression)).Trim();
            return ExecuteScalarAsync<TProp>(query, token: token);
        }

        public async Task<T> ExecuteScalarAsync<T>(string query, object cmdParams = null, IDbTransaction dbTransaction = null, CancellationToken token = default)
        {
            using (var cmd = CreateCommand(query, cmdParams, dbTransaction) as DbCommand)
            {
                try
                {
                    await BeginConnectionAsync(token);
                    var v = await cmd.ExecuteScalarAsync(token);
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

        public T First<T>(string query = null, object cmdParams = null, IEnumerable<string> columns = null, IEnumerable<(string, string)> columnToPropertyMap = null, DbValueConverter<T> converter = null, int offsetRows = 0, Func<T> itemFactory = null)
        {
            return ToList(query, cmdParams, columns, columnToPropertyMap, converter, 1, offsetRows, itemFactory).FirstOrDefault();
        }

        public T First<T>(Expression<Func<T, bool>> whereExpression, IEnumerable<(string, string)> columnToPropertyMap = null, DbValueConverter<T> converter = null, int offsetRows = 0, Func<T> itemFactory = null, params (Expression<Func<T, object>>, bool)[] orderByExpression)
        {
            return ToList(whereExpression, columnToPropertyMap, converter, 1, offsetRows, itemFactory, orderByExpression).FirstOrDefault();
        }


        public async Task<T> FirstAsync<T>(string query = null, object cmdParams = null, IEnumerable<string> columns = null, IEnumerable<(string, string)> columnToPropertyMap = null, DbValueConverter<T> converter = null, int offsetRows = 0, Func<T> itemFactory = null)
        {
            return (await ToListAsync(query, cmdParams, columns, columnToPropertyMap, converter, 1, offsetRows, itemFactory)).FirstOrDefault();
        }

        public async Task<T> FirstAsync<T>(Expression<Func<T, bool>> whereExpression, IEnumerable<(string, string)> columnToPropertyMap = null, DbValueConverter<T> converter = null, int offsetRows = 0, Func<T> itemFactory = null, CancellationToken ct = default, params (Expression<Func<T, object>>, bool)[] orderByExpression)
        {
            return (await ToListAsync(whereExpression, columnToPropertyMap, converter, 1, offsetRows, itemFactory, ct, orderByExpression)).FirstOrDefault();
        }

        #endregion First

        #region Command

        public DbCommand CreateCommand(string query, object cmdParams, IDbTransaction dbTransaction = null, int commandTimeOut = 30)
        {
            var cmd = Connection.CreateCommand();
            cmd.CommandText = query;
            cmd.CommandTimeout = commandTimeOut;
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

        public static void SetParameterCollection(IDbCommand cmd, Dictionary<string, object> cmdParams)
        {
            foreach (var cp in cmdParams)
            {

                IDbDataParameter p;
                if (cmd.Parameters.Contains(cp.Key))
                    p = (IDbDataParameter)cmd.Parameters[cp.Key];
                else
                {
                    p = cmd.CreateParameter();
                    cmd.Parameters.Add(p);
                }

                p.ParameterName = cp.Key;
                p.Value = cp.Value ?? DBNull.Value;
            }
        }

        public Dictionary<string, object> GetParams(object cmdParams, params string[] propertyNames)
        {
            var parameters = new Dictionary<string, object>();
            if (cmdParams == null)
                return parameters;

            switch (cmdParams)
            {
                case IEnumerable<(string, object)> tuple:
                    parameters = propertyNames?.Any() == true
                        ? tuple.Where(x => propertyNames.Contains(x.Item1)).ToDictionary(x => x.Item1, x => x.Item2)
                        : tuple.ToDictionary(x => x.Item1, x => x.Item2);
                    break;

                case Dictionary<string, object> dic:
                    parameters = propertyNames?.Any() == true
                        ? dic.Where(x => propertyNames.Contains(x.Key)).ToDictionary(x => x.Key, x => x.Value)
                        : dic;
                    break;

                case IEnumerable<KeyValuePair<string, object>> kvp:
                    parameters = propertyNames?.Any() == true
                        ? kvp.Where(x => propertyNames.Contains(x.Key)).ToDictionary(x => x.Key, x => x.Value)
                        : kvp.ToDictionary(x => x.Key, x => x.Value);
                    break;

                default:
                    parameters = MemberCache.Create(cmdParams.GetType()).ToDictionary(cmdParams, propertyNames);
                    break;
            }

            return parameters;
        }

        public static string GetRawSql(IDbCommand command, string paramNamePrefix = "@", string dateFormat = "yyyyMMdd", string stringPrefix = "'", string stringSuffix = "'", string nullValue = "NULL", string trueValue = "1", string falseValue = "0")
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            var sql = command.CommandText;

            foreach (IDbDataParameter parameter in command.Parameters)
            {
                var paramToken = paramNamePrefix + parameter.ParameterName;
                var literal = ToSqlLiteral(
                    parameter.Value,
                    dateFormat,
                    stringPrefix,
                    stringSuffix,
                    nullValue,
                    trueValue,
                    falseValue);

                sql = ReplaceParameterToken(sql, paramToken, literal);
            }

            return sql;
        }

        #endregion Command

        #region Query

        public TList Query<TList, TItem>(string query = null, object cmdParams = null, IEnumerable<string> columns = null, IEnumerable<(string, string)> columnToPropertyMap = null, DbValueConverter<TItem> converter = null, int fetchRows = -1, int offsetRows = 0, Func<TItem> itemFactory = null) where TList : ICollection<TItem>, new()
        {
            if (string.IsNullOrWhiteSpace(query))
                query = SqlQueryBuilder.GetSelectQuery<TItem>();

            query = SqlQueryBuilder.AddLimitOffsetClauseToQuery(fetchRows, offsetRows, query, Connection?.GetType(),
                typeof(TItem));

            var itemTypeCache = MemberCache<TItem>.Create();

            var parameters = GetParams(cmdParams);

            using (var cmd = CreateCommand(query, parameters))
            {
                try
                {
                    BeginConnection();

                    var list = new TList();

                    using (var r = cmd.ExecuteReader() as DbDataReader)
                    {
                        CommandExecuted?.Invoke(cmd);

                        var rowCount = 0;

                        var colIndex = columns?.Select(c => r.GetOrdinal(c)).FirstOrDefault() ?? 0;

                        if (itemTypeCache.IsBasic)
                        {
                            while (r.Read())
                            {
                                if (rowCount >= fetchRows && fetchRows > 0)
                                    break;
                                var raw = r.GetFieldValue<TItem>(colIndex);
                                list.Add(raw);
                                rowCount++;
                            }
                        }
                        else
                        {
                            var map = GetReaderFieldToPropertyMap<TItem>(r, columnToPropertyMap, columns);
                            var valueConverter = converter ?? ValueConverter.ToTypedConverter<TItem>();

                            while (r.Read())
                            {
                                var item = itemFactory != null ? itemFactory() : itemTypeCache.New();

                                foreach (var kv in map)
                                {
                                    var (propInfoEx, propSetter) = kv.Value;

                                    var raw = r.GetValue(kv.Key);

                                    if (raw == null || raw == DBNull.Value)
                                    {
                                        propSetter(item, null);
                                        continue;
                                    }

                                    var value = valueConverter(r.GetName(kv.Key), raw, (PropertyInfo)propInfoEx, item);
                                    try
                                    {
                                        propSetter(item, value);
                                    }
                                    catch (Exception ex)
                                    {
                                        throw HandleDbException(ex, cmd);
                                    }
                                }

                                list.Add(item);
                                rowCount++;
                            }
                        }

                        do
                        {
                        } while (r.NextResult());

                        return list;
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

        public async Task<TList> QueryAsync<TList, TItem>(string query = null, object cmdParams = null, IEnumerable<string> columns = null, IEnumerable<(string, string)> columnToPropertyMap = null, DbValueConverter<TItem> converter = null, int fetchRows = -1, int offsetRows = -1, Func<TItem> itemFactory = null, CancellationToken ct = default) where TList : ICollection<TItem>, new()
        {
            if (string.IsNullOrWhiteSpace(query))
                query = SqlQueryBuilder.GetSelectQuery<TItem>();

            query = SqlQueryBuilder.AddLimitOffsetClauseToQuery(fetchRows, offsetRows, query, Connection?.GetType(),
                typeof(TItem));

            var itemTypeCache = MemberCache<TItem>.Create();

            using (var cmd = CreateCommand(query, cmdParams) as DbCommand)
            {
                try
                {
                    await BeginConnectionAsync(ct);

                    var list = new TList();

                    using (var r = await cmd.ExecuteReaderAsync(ct))
                    {
                        CommandExecuted?.Invoke(cmd);

                        var rowCount = 0;

                        if (itemTypeCache.IsBasic)
                        {
                            while (await r.ReadAsync(ct))
                            {
                                if (rowCount >= fetchRows && fetchRows > 0)
                                    break;
                                var raw = await r.GetFieldValueAsync<TItem>(0, ct);
                                list.Add(raw);
                                rowCount++;
                            }
                        }
                        else
                        {
                            var map = GetReaderFieldToPropertyMap<TItem>(r, columnToPropertyMap, columns);
                            var valueConverter = converter ?? ValueConverter.ToTypedConverter<TItem>();

                            while (await r.ReadAsync(ct))
                            {
                                var item = itemFactory != null ? itemFactory() : itemTypeCache.New();

                                foreach (var kv in map)
                                {
                                    var (propInfoEx, propSetter) = kv.Value;

                                    var raw = await r.GetFieldValueAsync<object>(kv.Key, ct);

                                    if (raw == null || raw == DBNull.Value)
                                    {
                                        propSetter(item, null);
                                        continue;
                                    }

                                    var value = valueConverter(r.GetName(kv.Key), raw, (PropertyInfo)propInfoEx, item);
                                    try
                                    {
                                        propSetter(item, value);
                                    }
                                    catch (Exception ex)
                                    {
                                        throw HandleDbException(ex, cmd);
                                    }
                                }

                                list.Add(item);
                                rowCount++;
                            }
                        }

                        do
                        {
                        } while (await r.NextResultAsync(ct));

                        return list;
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

        #endregion Query

        #region ToDataTables

        public DataTable ToDataTable<TFrom>(Expression<Func<TFrom, bool>> whereExpression = null, int fetchRows = -1, int offsetRows = 0, params Expression<Func<TFrom, object>>[] columnSelectors)
        {
            var query = (SqlQueryBuilder.GetSelectQuery<TFrom>(columnSelectors) + " " + SqlQueryBuilder.GetWhereClause(whereExpression)).Trim();
            query = SqlQueryBuilder.AddLimitOffsetClauseToQuery(fetchRows, offsetRows, query, Connection?.GetType(), typeof(TFrom));
            return ToDataTables(query).FirstOrDefault();
        }

        public DataTable ToDataTable(string query, object cmdParams = null, params (string, string)[] columnMap)
        {
            return ToDataTables(query, cmdParams, columnMap).FirstOrDefault();
        }

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
                                var col = new DataColumn(kv.Value, r.GetFieldType(kv.Key));
                                dataTable.Columns.Add(col);
                            }

                            while (r.Read())
                            {
                                var item = dataTable.NewRow();

                                foreach (var kv in map)
                                {
                                    var colIndex = kv.Key;
                                    var raw = r.GetValue(colIndex);
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
                        } while (r.NextResult());
                        return  result.ToArray();
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

        public async Task<DataTable> ToDataTableAsync<TFrom>(Expression<Func<TFrom, bool>> whereExpression = null, int fetchRows = -1, int offsetRows = 0, params Expression<Func<TFrom, object>>[] columnSelectors)
        {
            var query = (SqlQueryBuilder.GetSelectQuery<TFrom>(columnSelectors) + " " + SqlQueryBuilder.GetWhereClause(whereExpression)).Trim();
            query = SqlQueryBuilder.AddLimitOffsetClauseToQuery(fetchRows, offsetRows, query, Connection?.GetType(), typeof(TFrom));
            return (await ToDataTablesAsync(query)).FirstOrDefault();
        }

        public async Task<DataTable[]> ToDataTablesAsync(string query, object cmdParams = null, CancellationToken token = default, params (string, string)[] columnMap)
        {
            if (string.IsNullOrWhiteSpace(query))
                throw new NullReferenceException(nameof(query));

            var result = new List<DataTable>();

            using (var cmd = CreateCommand(query, cmdParams))
            {
                try
                {
                    await BeginConnectionAsync(token);

                    var dataTable = new DataTable(query);
                    dataTable.BeginLoadData();

                    using (var r = await cmd.ExecuteReaderAsync(token))
                    {
                        do
                        {
                            CommandExecuted?.Invoke(cmd);
                            var map = GetReaderFieldToPropertyMap(r, columnMap);
                            foreach (var kv in map)
                            {
                                var col = new DataColumn(kv.Value, r.GetFieldType(kv.Key));
                                dataTable.Columns.Add(col);
                            }

                            while (await r.ReadAsync(token))
                            {
                                var item = dataTable.NewRow();

                                foreach (var kv in map)
                                {
                                    var colIndex = kv.Key;
                                    var raw = r.GetValue(colIndex);
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
                        } while (await r.NextResultAsync(token));
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

        #region ToList

        public List<TItem> ToList<TItem>(string query = null, object cmdParams = null, IEnumerable<string> columns = null, IEnumerable<(string, string)> columnToPropertyMap = null, DbValueConverter<TItem> converter = null, int fetchRows = -1, int offsetRows = 0, Func<TItem> itemFactory = null)
        {
            return Query<List<TItem>, TItem>(query, cmdParams, columns, columnToPropertyMap, converter, fetchRows, offsetRows, itemFactory);
        }

        public List<T> ToList<T>(Expression<Func<T, bool>> whereExpression, IEnumerable<(string, string)> columnToPropertyMap = null, DbValueConverter<T> converter = null,int fetchRows = -1, int offsetRows = 0, Func<T> itemFactory = null, params (Expression<Func<T, object>>, bool)[] orderByExpression)
        {
            var query = (SqlQueryBuilder.GetSelectQuery<T>() + " " + SqlQueryBuilder.GetWhereClause(whereExpression) + " " + SqlQueryBuilder.GetOrderBy(orderByExpression)).Trim();

            return ToList(query, null, null, columnToPropertyMap, converter, fetchRows, offsetRows, itemFactory);
        }

        public Task<List<T>> ToListAsync<T>(string query = null, object cmdParams = null, IEnumerable<string> columns = null, IEnumerable<(string, string)> columnToPropertyMap = null, DbValueConverter<T> converter = null, int fetchRows = -1, int offsetRows = 0, Func<T> itemFactory = null, CancellationToken ct = default)
        {
            return QueryAsync<List<T>,T>(query, cmdParams, columns, columnToPropertyMap, converter, fetchRows, offsetRows, itemFactory, ct);
        }

        public Task<List<T>> ToListAsync<T>(Expression<Func<T, bool>> whereExpression, IEnumerable<(string, string)> columnToPropertyMap = null, DbValueConverter<T> converter = null, int fetchRows = -1, int offsetRows = 0, Func<T> itemFactory = null, CancellationToken ct = default, params (Expression<Func<T, object>>, bool)[] orderByExpression)
        {
            var query = (SqlQueryBuilder.GetSelectQuery<T>() + " " + SqlQueryBuilder.GetWhereClause(whereExpression) + " " + SqlQueryBuilder.GetOrderBy(orderByExpression)).Trim();

            return ToListAsync(query, null, null, columnToPropertyMap, converter, fetchRows, offsetRows, itemFactory, ct);
        }

        #endregion ToList

        #region Aggs

        public int GetPagesCount<TFrom>(int pageSize) where TFrom : class
        {
            var pagesCount = 0;
            var numbers = Agg<TFrom>((null, "count"));
            var rowsCount = Convert.ToInt32(numbers.Values.FirstOrDefault());
            pagesCount = (int)Math.Ceiling((double)rowsCount / pageSize);
            return pagesCount;
        }

        /// <summary>
        /// Формирует словарь страниц для постраничной выборки данных.
        /// </summary>
        /// <typeparam name="TFrom">
        /// Тип сущности, для которой вычисляется пагинация.
        /// Используется для определения общего количества записей.
        /// </typeparam>
        /// <param name="pageSize">
        /// Размер страницы (количество элементов на странице).
        /// Должен быть больше нуля.
        /// </param>
        /// <returns>
        /// Словарь, где:
        /// <list type="bullet">
        /// <item>
        /// <description>Ключ — номер страницы (начиная с 1)</description>
        /// </item>
        /// <item>
        /// <description>
        /// Значение — кортеж:
        /// <c>(offset, count)</c>,
        /// где <c>offset</c> — смещение,
        /// <c>count</c> — количество элементов на странице
        /// </description>
        /// </item>
        /// </list>
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Выбрасывается, если <paramref name="pageSize"/> меньше либо равен нулю.
        /// </exception>
        public Dictionary<int, (int offset, int count)> GetPages<TFrom>(int pageSize) where TFrom : class
        {
            if (pageSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(pageSize));

            var total = Count<TFrom>();
            var pagesCount = (int)Math.Ceiling(total / (double)pageSize);

            var pages = new Dictionary<int, (int offset, int count)>(pagesCount);

            for (var page = 1; page <= pagesCount; page++)
            {
                var offset = (page - 1) * pageSize;
                var count = Math.Min(pageSize, total - offset);

                pages[page] = (offset, count);
            }

            return pages;
        }

        public int Count<TFrom>() where TFrom : class
        {
            return TypeHelper.ChangeType<int>(Agg<TFrom>("COUNT"));
        }
        public int Max<TFrom>() where TFrom : class
        {
            return TypeHelper.ChangeType<int>(Agg<TFrom>("MAX"));
        }

        public int Min<TFrom>() where TFrom : class
        {
            return TypeHelper.ChangeType<int>(Agg<TFrom>("MIN"));
        }
        public decimal Avg<TFrom>() where TFrom : class
        {
            return TypeHelper.ChangeType<decimal>(Agg<TFrom>("AVG"));
        }

        public Dictionary<string, object> Agg<TFrom>(string aggFunction, params Expression<Func<TFrom, object>>[] columnSelectors) where TFrom : class
        {
            return  Agg(columnSelectors?.Any() == true ? columnSelectors.Select(c => (c, aggFunction)).ToArray() : new [] {((Expression<Func<TFrom, object>>)null, aggFunction)});
        }

        public Dictionary<string, object> Agg<TFrom>(params (Expression<Func<TFrom, object>> column, string aggFunction)[] columnSelectors) where TFrom : class
        {
            var query = "SELECT " + (columnSelectors.Length == 0 ? "COUNT(*)" : string.Join(", ", columnSelectors.Select(c => $"{c.aggFunction}(\"{c.column?.GetMemberCache()?.ColumnName ?? "*"}\") AS \"{c.column?.GetMemberCache()?.ColumnName ?? "Total"}{c.aggFunction.ToUpper()}\"".Replace("\"*\"","*"))))
            +$" FROM \"{typeof(TFrom).GetMemberCache().TableName}\"";

            var table = ToDataTable(query);
            var result = new Dictionary<string, object>(IgnoreCaseComparer);
            foreach (DataColumn dc in table.Columns)
            {
                var value = table.Rows[0][dc.ColumnName];
                result[dc.ColumnName] = value;
            }

            return result;
        }

        #endregion

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #region Privates

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                CloseConnection();
                Connection?.Dispose();
                _disposed = true;
            }
        }

        private static string EscapeString(string value)
        {
            return value.Replace("'", "''");
        }

        private static string ReplaceParameterToken(string sql, string token, string replacement)
        {
            return Regex.Replace(
                sql,
                $@"(?<![\w@]){Regex.Escape(token)}(?!\w)",
                replacement,
                RegexOptions.CultureInvariant);
        }

        private static string ToSqlLiteral(
            object value,
            string dateFormat,
            string stringPrefix,
            string stringSuffix,
            string nullValue,
            string trueValue,
            string falseValue)
        {
            if (value == null || value == DBNull.Value)
                return nullValue;

            switch (value)
            {
                case string s:
                    return $"{stringPrefix}{EscapeString(s)}{stringSuffix}";

                case char c:
                    return $"{stringPrefix}{EscapeString(c.ToString())}{stringSuffix}";

                case bool b:
                    return b ? trueValue : falseValue;

                case DateTime dt:
                    return $"{stringPrefix}{dt.ToString(dateFormat, CultureInfo.InvariantCulture)}{stringSuffix}";

                case DateTimeOffset dto:
                    return $"{stringPrefix}{dto.ToString(dateFormat, CultureInfo.InvariantCulture)}{stringSuffix}";

                case Guid g:
                    return $"{stringPrefix}{g}{stringSuffix}";

                case Enum e:
                    return Convert.ToInt64(e).ToString(CultureInfo.InvariantCulture);

                case TimeSpan ts:
                    return $"{stringPrefix}{ts.ToString("c", CultureInfo.InvariantCulture)}{stringSuffix}";

                case IFormattable formattable:
                    return formattable.ToString(null, CultureInfo.InvariantCulture);

                default:
                    return $"{stringPrefix}{EscapeString(value.ToString())}{stringSuffix}";
            }
        }
        private IDbConnection BeginConnection()
        {
            return BeginConnection(Connection);
        }

        private IDbConnection BeginConnection(IDbConnection connection)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

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

                return connection;
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

        private async Task<IDbConnection> BeginConnectionAsync(IDbConnection connection, CancellationToken token = default)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            try
            {
                if (connection.State == ConnectionState.Broken)
                {
                    connection.Close();
                }

                if (connection.State == ConnectionState.Open) return connection;
                if (connection is DbConnection dc)
                    await dc.OpenAsync(token);
                else 
                    connection.Open();

                return connection;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Не удалось открыть соединение с базой данных.", ex);
            }
        }

        private object ChangeType(object value, Type targetType) => TypeHelper.ChangeType(value, targetType);

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

        private Dictionary<int, (MemberCache propInfoEx, Action<T, object> propSetter)> GetReaderFieldToPropertyMap<T>(IDataReader reader, IEnumerable<(string, string)> customMap = null, IEnumerable<string> columns = null)
        {
            var customMapDic = customMap?.ToDictionary(k => k.Item1, v => v.Item2) ?? new Dictionary<string, string>();
            var map = new Dictionary<int, (MemberCache propInfoEx, Action<T, object> propSetter)>();
            var typeInfoEx = MemberCache.Create(typeof(T));
            var columnsCount = reader.FieldCount;

            for (var i = 0; i < columnsCount; i++)
            {
                var colIndex = i;
                var colName = reader.GetName(i);
                MemberCache propInfoEx;
                if (customMap != null)
                {
                    propInfoEx = typeInfoEx.PublicBasicProperties.GetValueOrDefault(customMapDic.GetValueOrDefault(colName, IgnoreCaseComparer), IgnoreCaseComparer);
                    map[colIndex] = (propInfoEx, TypeHelper.GetMemberSetter<T>(propInfoEx.Name));
                    if (map[colIndex].propInfoEx != null)
                        continue;
                }

                propInfoEx = typeInfoEx.ColumnProperties.GetValueOrDefault(colName, IgnoreCaseComparer);
                if (propInfoEx != null)
                {
                    map[colIndex] = (propInfoEx, TypeHelper.GetMemberSetter<T>(propInfoEx.Name));
                    continue;
                }

                propInfoEx = typeInfoEx.PublicBasicProperties.GetValueOrDefault(colName, IgnoreCaseComparer);

                if (propInfoEx != null)
                {
                    map[colIndex] = (propInfoEx, TypeHelper.GetMemberSetter<T>(propInfoEx.Name));
                    continue;
                }

                map.Remove(colIndex);
            }

            return map;
        }

        private InvalidOperationException HandleDbException(Exception ex, IDbCommand cmd, [CallerMemberName] string methodName = "")
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
            foreach (IDbDataParameter p in cmd.Parameters)
            {
                Debug.WriteLine($"  {p.ParameterName} = {p.Value}");
            }
        }

        private void RollbackTransaction()
        {
            if (_tr.Value == null)
                throw new InvalidOperationException("Транзакция не была начата.");

            _tr.Value?.Rollback();
        }

        #endregion Privates
    }
}