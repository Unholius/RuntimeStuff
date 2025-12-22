//using System;
//using System.Collections.Generic;
//using System.Data;
//using System.Linq.Expressions;
//using System.Threading;
//using System.Threading.Tasks;

//namespace RuntimeStuff.Extensions
//{
//    public static class DbConnectionExtensions
//    {
//        public static DbClient GetDbClient(this IDbConnection connection)
//        {
//            return DbClient.Create(connection);
//        }

//        #region Delete

//        public static int Delete<T>(this IDbConnection connection, Expression<Func<T, bool>> whereExpression) where T : class
//        {
//            return DbClient.Create(connection).Delete(whereExpression);
//        }

//        public static int Delete<T>(this IDbConnection connection, T item) where T : class
//        {
//            return DbClient.Create(connection).Delete(item);
//        }

//        public static Task<int> DeleteAsync<T>(this IDbConnection connection, T item, IDbTransaction dbTransaction = null, CancellationToken token = default) where T : class
//        {
//            return DbClient.Create(connection).DeleteAsync(item, dbTransaction, token);
//        }

//        public static Task<int> DeleteAsync<T>(this IDbConnection connection, Expression<Func<T, bool>> whereExpression, IDbTransaction dbTransaction, CancellationToken token = default) where T : class
//        {
//            return DbClient.Create(connection).DeleteAsync(whereExpression, dbTransaction, token);
//        }

//        public static Task<int> DeleteRangeAsync<T>(this IDbConnection connection, IEnumerable<T> list, IDbTransaction dbTransaction, CancellationToken token = default) where T : class
//        {
//            return DbClient.Create(connection).DeleteRangeAsync(list, dbTransaction, token);
//        }

//        #endregion Delete

//        #region ExecuteNonQuery

//        public static int ExecuteNonQuery(this IDbConnection connection, string query, object queryParams, IDbTransaction dbTransaction = null)
//        {
//            return DbClient.Create(connection).ExecuteNonQuery(query, queryParams, dbTransaction);
//        }

//        public static int ExecuteNonQuery(this IDbConnection connection, string query, IEnumerable<KeyValuePair<string, object>> cmdParams, IDbTransaction dbTransaction = null)
//        {
//            return DbClient.Create(connection).ExecuteNonQuery(query, cmdParams, dbTransaction);
//        }

//        public static int ExecuteNonQuery(this IDbConnection connection, string query, IDbTransaction dbTransaction = null, params (string, object)[] cmdParams)
//        {
//            return DbClient.Create(connection).ExecuteNonQuery(query, dbTransaction, cmdParams);
//        }

//        public static int ExecuteNonQuery(this IDbConnection connection, string query, params (string, object)[] cmdParams)
//        {
//            return DbClient.Create(connection).ExecuteNonQuery(query, cmdParams);
//        }

//        public static Task<int> ExecuteNonQueryAsync(this IDbConnection connection, string query, object cmdParams, IDbTransaction dbTransaction, CancellationToken token = default)
//        {
//            return DbClient.Create(connection).ExecuteNonQueryAsync(query, cmdParams, dbTransaction, token);
//        }

//        public static Task<int> ExecuteNonQueryAsync(this IDbConnection connection, string query, IEnumerable<KeyValuePair<string, object>> cmdParams, IDbTransaction dbTransaction, CancellationToken token = default)
//        {
//            return DbClient.Create(connection).ExecuteNonQueryAsync(query, cmdParams, dbTransaction, token);
//        }

//        public static Task<int> ExecuteNonQueryAsync(this IDbConnection connection, string query, (string, object)[] cmdParams = null, IDbTransaction dbTransaction = null, CancellationToken token = default)
//        {
//            return DbClient.Create(connection).ExecuteNonQueryAsync(query, cmdParams, dbTransaction, token);
//        }

//        #endregion ExecuteNonQuery

//        #region ExecuteScalar

//        public static object ExecuteScalar(this IDbConnection connection, string query, IDbTransaction dbTransaction = null, params (string, object)[] cmdParams)
//        {
//            return DbClient.Create(connection).ExecuteScalar(query, dbTransaction, cmdParams);
//        }

//        public static object ExecuteScalar(this IDbConnection connection, string query, object cmdParams, IDbTransaction dbTransaction = null)
//        {
//            return DbClient.Create(connection).ExecuteScalar(query, cmdParams, dbTransaction);
//        }

//        public static object ExecuteScalar(this IDbConnection connection, string query, IEnumerable<KeyValuePair<string, object>> cmdParams, IDbTransaction dbTransaction = null)
//        {
//            return DbClient.Create(connection).ExecuteScalar(query, cmdParams, dbTransaction);
//        }

//        public static T ExecuteScalar<T>(this IDbConnection connection, string query, IDbTransaction dbTransaction = null, params (string, object)[] cmdParams)
//        {
//            return DbClient.Create(connection).ExecuteScalar<T>(query, dbTransaction, cmdParams);
//        }

//        public static TProp ExecuteScalar<T, TProp>(this IDbConnection connection, Expression<Func<T, TProp>> propertySelector, Expression<Func<T, bool>> whereExpression)
//        {
//            return DbClient.Create(connection).ExecuteScalar(propertySelector, whereExpression);
//        }

//        public static T ExecuteScalar<T>(this IDbConnection connection, string query, object cmdParams, IDbTransaction dbTransaction = null)
//        {
//            return DbClient.Create(connection).ExecuteScalar<T>(query, cmdParams, dbTransaction);
//        }

//        public static T ExecuteScalar<T>(this IDbConnection connection, string query, IEnumerable<KeyValuePair<string, object>> cmdParams, IDbTransaction dbTransaction = null)
//        {
//            return DbClient.Create(connection).ExecuteScalar<T>(query, cmdParams, dbTransaction);
//        }

//        public static Task<object> ExecuteScalarAsync(this IDbConnection connection, string query, object cmdParams, IDbTransaction dbTransaction = null)
//        {
//            return DbClient.Create(connection).ExecuteScalarAsync(query, cmdParams, dbTransaction);
//        }

//        public static Task<T> ExecuteScalarAsync<T>(this IDbConnection connection, string query, object cmdParams, IDbTransaction dbTransaction = null)
//        {
//            return DbClient.Create(connection).ExecuteScalarAsync<T>(query, cmdParams, dbTransaction);
//        }

//        public static Task<T> ExecuteScalarAsync<T>(this IDbConnection connection, string query, IEnumerable<KeyValuePair<string, object>> cmdParams, IDbTransaction dbTransaction, CancellationToken token = default)
//        {
//            return DbClient.Create(connection).ExecuteScalarAsync<T>(query, cmdParams, dbTransaction, token);
//        }

//        public static Task<T> ExecuteScalarAsync<T>(this IDbConnection connection, string query, object cmdParams, IDbTransaction dbTransaction, CancellationToken token = default)
//        {
//            return DbClient.Create(connection).ExecuteScalarAsync<T>(query, cmdParams, dbTransaction, token);
//        }

//        public static Task<T> ExecuteScalarAsync<T>(this IDbConnection connection, string query, IEnumerable<(string, object)> cmdParams = null, IDbTransaction dbTransaction = null, CancellationToken token = default)
//        {
//            return DbClient.Create(connection).ExecuteScalarAsync<T>(query, cmdParams, dbTransaction, token);
//        }

//        #endregion ExecuteScalar

//        #region First

//        public static T First<T>(this IDbConnection connection, string query, IEnumerable<KeyValuePair<string, object>> cmdParams,
//            IEnumerable<KeyValuePair<string, string>> columnToPropertyMap = null,
//            DbClient.DbValueConverter<T> converter = null)
//        {
//            return DbClient.Create(connection).First(query, cmdParams, columnToPropertyMap, converter);
//        }

//        public static T First<T>(this IDbConnection connection, string query, object cmdParams,
//            IEnumerable<KeyValuePair<string, string>> columnToPropertyMap = null,
//            DbClient.DbValueConverter<T> converter = null)
//        {
//            return DbClient.Create(connection).First(query, cmdParams, columnToPropertyMap, converter);
//        }

//        public static T First<T>(this IDbConnection connection, Expression<Func<T, bool>> whereExpression, DbClient.DbValueConverter<T> converter = null, params (Expression<Func<T, object>>, bool)[] orderByExpression)
//        {
//            return DbClient.Create(connection).First(whereExpression, converter, orderByExpression);
//        }

//        public static T First<T>(this IDbConnection connection, string query = null, IEnumerable<(string, object)> cmdParams = null, IEnumerable<(string, string)> columnToPropertyMap = null,
//            DbClient.DbValueConverter<T> converter = null)
//        {
//            return DbClient.Create(connection).First(query, cmdParams, columnToPropertyMap, converter);
//        }

//        public static Task<T> FirstAsync<T>(this IDbConnection connection, string query,
//            IEnumerable<KeyValuePair<string, object>> cmdParams,
//            IEnumerable<KeyValuePair<string, string>> columnToPropertyMap = null,
//            DbClient.DbValueConverter<T> converter = null,
//            CancellationToken token = default)
//        {
//            return DbClient.Create(connection).FirstAsync(query, cmdParams, columnToPropertyMap, converter, token);
//        }

//        public static Task<T> FirstAsync<T>(this IDbConnection connection, string query,
//            object cmdParams,
//            IEnumerable<KeyValuePair<string, string>> columnToPropertyMap = null,
//            DbClient.DbValueConverter<T> converter = null,
//            CancellationToken token = default)
//        {
//            return DbClient.Create(connection).FirstAsync(query, cmdParams, columnToPropertyMap, converter, token);
//        }

//        public static Task<T> FirstAsync<T>(this IDbConnection connection, Expression<Func<T, bool>> whereExpression, DbClient.DbValueConverter<T> converter = null, (Expression<Func<T, object>>, bool)[] orderByExpression = null, int offsetRows = 0, Func<T> itemFactory = null, CancellationToken token = default)
//        {
//            return DbClient.Create(connection).FirstAsync(whereExpression, converter, orderByExpression, offsetRows, itemFactory, token);
//        }

//        public static Task<T> FirstAsync<T>(this IDbConnection connection, string query = null,
//            IEnumerable<(string, object)> cmdParams = null,
//            IEnumerable<(string, string)> columnToPropertyMap = null,
//            DbClient.DbValueConverter<T> converter = null,
//            Func<T> itemFactory = null,
//            CancellationToken token = default)
//        {
//            return DbClient.Create(connection).FirstAsync(query, cmdParams, columnToPropertyMap, converter, itemFactory, token);
//        }

//        #endregion First

//        #region Insert

//        public static T Insert<T>(this IDbConnection connection, IDbTransaction dbTransaction = null, params Action<T>[] insertColumns) where T : class
//        {
//            return DbClient.Create(connection).Insert(dbTransaction, insertColumns);
//        }

//        public static object Insert<T>(this IDbConnection connection, T item, string queryGetId = "SELECT SCOPE_IDENTITY()", IDbTransaction dbTransaction = null, params Expression<Func<T, object>>[] insertColumns) where T : class
//        {
//            return DbClient.Create(connection).Insert(item, queryGetId, dbTransaction, insertColumns);
//        }

//        public static Task<object> InsertAsync<T>(this IDbConnection connection, string queryGetId = "SELECT SCOPE_IDENTITY()", Action<T>[] insertColumns = null, IDbTransaction dbTransaction = null, CancellationToken token = default) where T : class
//        {
//            return DbClient.Create(connection).InsertAsync(queryGetId, insertColumns, dbTransaction, token);
//        }

//        public static Task<object> InsertAsync<T>(this IDbConnection connection, T item, string queryGetId = "SELECT SCOPE_IDENTITY()", Expression<Func<T, object>>[] insertColumns = null, IDbTransaction dbTransaction = null, CancellationToken token = default) where T : class
//        {
//            return DbClient.Create(connection).InsertAsync(item, queryGetId, insertColumns, dbTransaction, token);
//        }

//        public static int InsertRange<T>(this IDbConnection connection, IEnumerable<T> list, string queryGetId = "SELECT SCOPE_IDENTITY()", IDbTransaction dbTransaction = null, params Expression<Func<T, object>>[] insertColumns) where T : class
//        {
//            return DbClient.Create(connection).InsertRange(list, queryGetId, dbTransaction, insertColumns);
//        }

//        public static Task<int> InsertRangeAsync<T>(this IDbConnection connection, IEnumerable<T> list, string queryGetId = "SELECT SCOPE_IDENTITY()", Expression<Func<T, object>>[] insertColumns = null, IDbTransaction dbTransaction = null, CancellationToken token = default) where T : class
//        {
//            return DbClient.Create(connection).InsertRangeAsync(list, queryGetId, insertColumns, dbTransaction, token);
//        }

//        #endregion Insert

//        #region Query

//        public static TList ToCollection<TList, TItem>(this IDbConnection connection, string query, object cmdParams, IEnumerable<(string, string)> columnToPropertyMap = null, DbClient.DbValueConverter<TItem> converter = null, int fetchRows = -1) where TList : ICollection<TItem>, new()
//        {
//            return DbClient.Create(connection).Query<TList, TItem>(query, cmdParams, columnToPropertyMap, converter, fetchRows);
//        }

//        public static Task<TList> QueryAsync<TList, TItem>(this IDbConnection connection, string query,
//            object cmdParams,
//            IEnumerable<(string, string)> columnToPropertyMap = null, DbClient.DbValueConverter<TItem> converter = null,
//            Action<string, object, MemberCache, TItem> setter = null, int fetchRows = -1)
//            where TList : ICollection<TItem>, new() where TItem : class, new()
//        {
//            return DbClient.Create(connection).QueryAsync<TList, TItem>(query, cmdParams,
//                columnToPropertyMap, converter, setter, fetchRows);
//        }

//        public static TList ToCollection<TList, TItem>(this IDbConnection connection, string query, IEnumerable<KeyValuePair<string, object>> cmdParams, IEnumerable<(string, string)> columnToPropertyMap = null, DbClient.DbValueConverter<TItem> converter = null, int fetchRows = -1) where TList : ICollection<TItem>, new() where TItem : class, new()
//        {
//            return DbClient.Create(connection).Query<TList, TItem>(query, cmdParams, columnToPropertyMap, converter, fetchRows);
//        }

//        public static TList ToCollection<TList, TItem>(this IDbConnection connection, string query = null, IEnumerable<(string, object)> cmdParams = null, IEnumerable<(string, string)> columnToPropertyMap = null, DbClient.DbValueConverter<TItem> converter = null, int fetchRows = -1, int offsetRows = 0, Func<TItem> itemFactory = null) where TList : ICollection<TItem>, new()
//        {
//            return DbClient.Create(connection).Query<TList, TItem>(query, cmdParams, columnToPropertyMap, converter, fetchRows, offsetRows, itemFactory);
//        }

//        public static Task<TList> QueryAsync<TList, TItem>(this IDbConnection connection, string query = null, IEnumerable<(string, object)> cmdParams = null, IEnumerable<(string, string)> columnToPropertyMap = null, DbClient.DbValueConverter<TItem> converter = null, int fetchRows = -1, int offsetRows = -1, Func<TItem> itemFactory = null, CancellationToken ct = default) where TList : ICollection<TItem>, new()
//        {
//            return DbClient.Create(connection).QueryAsync<TList, TItem>(query, cmdParams, columnToPropertyMap, converter, fetchRows, offsetRows, itemFactory, ct);
//        }

//        #endregion Query

//        #region ToDataTables

//        public static DataTable ToDataTable(this IDbConnection connection, string query, object cmdParams,
//            IEnumerable<(string, string)> columnMap = null, int maxRows = -1)
//        {
//            return DbClient.Create(connection).ToDataTables(query, cmdParams, columnMap, maxRows);
//        }

//        public static DataTable ToDataTable(this IDbConnection connection, string query, IEnumerable<KeyValuePair<string, object>> cmdParams,
//            IEnumerable<(string, string)> columnMap = null, int maxRows = -1)
//        {
//            return DbClient.Create(connection).ToDataTables(query, cmdParams, columnMap, maxRows);
//        }

//        public static DataTable ToDataTable(this IDbConnection connection, string query, IEnumerable<(string, object)> cmdParams = null, IEnumerable<(string, string)> columnMap = null, int maxRows = -1)
//        {
//            return DbClient.Create(connection).ToDataTables(query, cmdParams, columnMap, maxRows);
//        }

//        public static Task<DataTable> ToDataTableAsync(this IDbConnection connection, string query, object cmdParams,
//            IEnumerable<(string, string)> columnMap = null, int maxRows = -1)
//        {
//            return DbClient.Create(connection).ToDataTableAsync(query, cmdParams, columnMap, maxRows);
//        }

//        public static Task<DataTable> ToDataTableAsync(this IDbConnection connection, string query, IEnumerable<KeyValuePair<string, object>> cmdParams,
//            IEnumerable<(string, string)> columnMap = null, int maxRows = -1)
//        {
//            return DbClient.Create(connection).ToDataTableAsync(query, cmdParams, columnMap, maxRows);
//        }

//        public static Task<DataTable> ToDataTableAsync(this IDbConnection connection, string query, IEnumerable<(string, object)> cmdParams = null, IEnumerable<(string, string)> columnMap = null, int maxRows = -1, CancellationToken token = default)
//        {
//            return DbClient.Create(connection).ToDataTableAsync(query, cmdParams, columnMap, maxRows, token);
//        }

//        #endregion ToDataTables

//        #region ToDictionary

//        public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(this IDbConnection connection, string query, object cmdParams)
//        {
//            return DbClient.Create(connection).ToDictionary<TKey, TValue>(query, cmdParams);
//        }

//        public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(this IDbConnection connection, string query, IEnumerable<KeyValuePair<string, object>> cmdParams)
//        {
//            return DbClient.Create(connection).ToDictionary<TKey, TValue>(query, cmdParams);
//        }

//        public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue, T>(this IDbConnection connection, Expression<Func<T, TKey>> keySelector, Expression<Func<T, TValue>> valueSelector, Expression<Func<T, bool>> whereExpression = null)
//        {
//            return DbClient.Create(connection).ToDictionary(keySelector, valueSelector, whereExpression);
//        }

//        public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(this IDbConnection connection, string query, params (string, object)[] cmdParams)
//        {
//            return DbClient.Create(connection).ToDictionary<TKey, TValue>(query, cmdParams);
//        }

//        public static Task<Dictionary<TKey, TValue>> ToDictionaryAsync<TKey, TValue>(this IDbConnection connection, string query, object cmdParams)
//        {
//            return DbClient.Create(connection).ToDictionaryAsync<TKey, TValue>(query, cmdParams);
//        }

//        public static Task<Dictionary<TKey, TValue>> ToDictionaryAsync<T, TKey, TValue>(this IDbConnection connection, Expression<Func<T, TKey>> keySelector, Expression<Func<T, TValue>> valueSelector, Expression<Func<T, bool>> whereExpression = null, CancellationToken token = default)
//        {
//            return DbClient.Create(connection).ToDictionaryAsync(keySelector, valueSelector, whereExpression, token);
//        }

//        public static Task<Dictionary<TKey, TValue>> ToDictionaryAsync<TKey, TValue>(this IDbConnection connection, string query, IEnumerable<KeyValuePair<string, object>> cmdParams, CancellationToken ct = default)
//        {
//            return DbClient.Create(connection).ToDictionaryAsync<TKey, TValue>(query, cmdParams, ct);
//        }

//        public static Task<Dictionary<TKey, TValue>> ToDictionaryAsync<TKey, TValue>(this IDbConnection connection, string query, IEnumerable<(string, object)> cmdParams = null, CancellationToken ct = default)
//        {
//            return DbClient.Create(connection).ToDictionaryAsync<TKey, TValue>(query, cmdParams, ct);
//        }

//        #endregion ToDictionary

//        #region ToList

//        public static List<T> ToList<T>(this IDbConnection connection, string query, object cmdParams,
//            IEnumerable<KeyValuePair<string, string>> columnToPropertyMap = null,
//            DbClient.DbValueConverter<T> converter = null,
//            int maxRows = -1)
//        {
//            return DbClient.Create(connection).ToList(query, cmdParams, columnToPropertyMap, converter, maxRows);
//        }

//        public static List<T> ToList<T>(this IDbConnection connection, string query, IEnumerable<KeyValuePair<string, object>> cmdParams, IEnumerable<KeyValuePair<string, string>> columnToPropertyMap = null, DbClient.DbValueConverter<T> converter = null, int maxRows = -1)
//        {
//            return DbClient.Create(connection).ToList(query, cmdParams, columnToPropertyMap, converter, maxRows);
//        }

//        public static List<T> ToList<T>(this IDbConnection connection, Expression<Func<T, bool>> whereExpression, DbClient.DbValueConverter<T> converter = null, int maxRows = -1, params (Expression<Func<T, object>>, bool)[] orderByExpression)
//        {
//            return DbClient.Create(connection).ToList(whereExpression, converter, maxRows, orderByExpression);
//        }

//        public static List<T> ToList<T>(this IDbConnection connection, string query = null, IEnumerable<(string, object)> cmdParams = null, IEnumerable<(string, string)> columnToPropertyMap = null, DbClient.DbValueConverter<T> converter = null, int maxRows = -1)
//        {
//            return DbClient.Create(connection).ToList(query, cmdParams, columnToPropertyMap, converter, maxRows);
//        }

//        public static Task<List<T>> ToListAsync<T>(this IDbConnection connection, string query, object cmdParams, IEnumerable<KeyValuePair<string, string>> columnToPropertyMap = null, DbClient.DbValueConverter<T> converter = null, int maxRows = -1, CancellationToken ct = default)
//        {
//            return DbClient.Create(connection).ToListAsync(query, cmdParams, columnToPropertyMap, converter, maxRows, ct);
//        }

//        public static Task<List<T>> ToListAsync<T>(this IDbConnection connection, string query, IEnumerable<KeyValuePair<string, object>> cmdParams, IEnumerable<KeyValuePair<string, string>> columnToPropertyMap = null, DbClient.DbValueConverter<T> converter = null, int maxRows = -1, int offsetRows = 0, Func<T> itemFactory = null, CancellationToken ct = default)
//        {
//            return DbClient.Create(connection).ToListAsync(query, cmdParams, columnToPropertyMap, converter, maxRows, offsetRows, itemFactory, ct);
//        }

//        public static Task<List<T>> ToListAsync<T>(this IDbConnection connection, string query = null, IEnumerable<(string, object)> cmdParams = null, IEnumerable<(string, string)> columnToPropertyMap = null, DbClient.DbValueConverter<T> converter = null, int maxRows = -1, int offsetRows = 0, Func<T> itemFactory = null, CancellationToken ct = default)
//        {
//            return DbClient.Create(connection).ToListAsync(query, cmdParams, columnToPropertyMap, converter, maxRows, offsetRows, itemFactory, ct);
//        }

//        public static Task<List<T>> ToListAsync<T>(this IDbConnection connection, Expression<Func<T, bool>> whereExpression, DbClient.DbValueConverter<T> converter = null, int fetchRows = -1, int offsetRows = 0, (Expression<Func<T, object>>, bool)[] orderByExpression = null, Func<T> itemFactory = null, CancellationToken token = default)
//        {
//            return DbClient.Create(connection).ToListAsync(whereExpression, converter, fetchRows, offsetRows, orderByExpression, itemFactory, token);
//        }

//        #endregion ToList

//        #region Update

//        public static int Update<T>(this IDbConnection connection, T item, IDbTransaction dbTransaction = null, params Expression<Func<T, object>>[] updateColumns) where T : class
//        {
//            return DbClient.Create(connection).Update(item, dbTransaction, updateColumns);
//        }

//        public static int Update<T>(this IDbConnection connection, T item, Expression<Func<T, bool>> whereExpression, params Expression<Func<T, object>>[] updateColumns) where T : class
//        {
//            return DbClient.Create(connection).Update(item, whereExpression, updateColumns);
//        }

//        public static Task<int> UpdateAsync<T>(this IDbConnection connection, T item, Expression<Func<T, object>>[] updateColumns = null, IDbTransaction dbTransaction = null, CancellationToken token = default) where T : class
//        {
//            return DbClient.Create(connection).UpdateAsync(item, updateColumns, dbTransaction, token);
//        }

//        public static Task<int> UpdateAsync<T>(this IDbConnection connection, T item, Expression<Func<T, bool>> whereExpression, Expression<Func<T, object>>[] updateColumns = null, IDbTransaction dbTransaction = null, CancellationToken token = default) where T : class
//        {
//            return DbClient.Create(connection).UpdateAsync(item, whereExpression, updateColumns, dbTransaction, token);
//        }

//        public static int UpdateRange<T>(this IDbConnection connection, IEnumerable<T> list, IDbTransaction dbTransaction = null, params Expression<Func<T, object>>[] updateColumns) where T : class
//        {
//            return DbClient.Create(connection).UpdateRange(list, dbTransaction, updateColumns);
//        }

//        public static Task<int> UpdateRangeAsync<T>(this IDbConnection connection, IEnumerable<T> list, Expression<Func<T, object>>[] updateColumns = null, IDbTransaction dbTransaction = null, CancellationToken token = default) where T : class
//        {
//            return DbClient.Create(connection).UpdateRangeAsync(list, updateColumns, dbTransaction, token);
//        }

//        #endregion Update

//        #region Transaction

//        public static IDbTransaction BeginTransaction(this IDbConnection connection, IsolationLevel level = IsolationLevel.ReadCommitted)
//        {
//            return DbClient.Create(connection).BeginTransaction(level);
//        }

//        public static void EndTransaction(this IDbConnection connection)
//        {
//            DbClient.Create(connection).EndTransaction();
//        }

//        #endregion Transaction
//    }
//}