using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace RuntimeStuff.Extensions
{
    public static class DbConnectionExtensions
    {
        public static DbClient<T> AsDbClient<T>(this T connection) where T : IDbConnection, new()
        {
            return DbClient<T>.Create(connection);
        }

        public static DbClient AsDbClient(this IDbConnection connection)
        {
            return DbClient.Create(connection);
        }

        #region Insert

        public static T Insert<T>(this IDbConnection connection, IDbTransaction dbTransaction = null, params Action<T>[] insertColumns) where T : class
        {
            return connection.AsDbClient().Insert(dbTransaction, insertColumns);
        }

        public static object Insert<T>(this IDbConnection connection, T item, IDbTransaction dbTransaction = null, params Expression<Func<T, object>>[] insertColumns) where T : class
        {
            return connection.AsDbClient().Insert(item, dbTransaction, insertColumns);
        }

        public static Task<object> InsertAsync<T>(this IDbConnection connection, Action<T>[] insertColumns = null, IDbTransaction dbTransaction = null, CancellationToken token = default)
            where T : class
        {
            return connection.AsDbClient().InsertAsync(insertColumns, dbTransaction, token);
        }

        public static Task<object> InsertAsync<T>(this IDbConnection connection, T item, Expression<Func<T, object>>[] insertColumns = null, IDbTransaction dbTransaction = null,
            CancellationToken token = default) where T : class
        {
            return connection.AsDbClient().InsertAsync(item, insertColumns, dbTransaction, token);
        }

        public static int InsertRange<T>(this IDbConnection connection, IEnumerable<T> list, IDbTransaction dbTransaction = null, params Expression<Func<T, object>>[] insertColumns) where T : class
        {
            return connection.AsDbClient().InsertRange(list, dbTransaction, insertColumns);
        }

        public static Task<int> InsertRangeAsync<T>(this IDbConnection connection, IEnumerable<T> list, Expression<Func<T, object>>[] insertColumns = null, IDbTransaction dbTransaction = null,
            CancellationToken token = default) where T : class
        {
            return connection.AsDbClient().InsertRangeAsync(list, insertColumns, dbTransaction, token);
        }

        #endregion Insert

        #region Update

        public static int Update<T>(this IDbConnection connection, T item, IDbTransaction dbTransaction = null, params Expression<Func<T, object>>[] updateColumns) where T : class
        {
            return connection.AsDbClient().Update(item, dbTransaction, updateColumns);
        }

        public static int Update<T>(this IDbConnection connection, T item, Expression<Func<T, bool>> whereExpression, IDbTransaction dbTransaction = null, params Expression<Func<T, object>>[] updateColumns) where T : class
        {
            return connection.AsDbClient().Update(item, whereExpression, dbTransaction, updateColumns);
        }

        public static Task<int> UpdateAsync<T>(this IDbConnection connection, T item, Expression<Func<T, object>>[] updateColumns = null,
            IDbTransaction dbTransaction = null, CancellationToken token = default) where T : class
        {
            return connection.AsDbClient().UpdateAsync(item, updateColumns, dbTransaction, token);
        }

        public static Task<int> UpdateAsync<T>(this IDbConnection connection, T item, Expression<Func<T, bool>> whereExpression,
            Expression<Func<T, object>>[] updateColumns = null, IDbTransaction dbTransaction = null,
            CancellationToken token = default) where T : class
        {
            return connection.AsDbClient().UpdateAsync(item, whereExpression, updateColumns, dbTransaction, token);
        }

        public static int UpdateRange<T>(this IDbConnection connection, IEnumerable<T> list, IDbTransaction dbTransaction = null,
            params Expression<Func<T, object>>[] updateColumns) where T : class
        {
            return connection.AsDbClient().UpdateRange(list, dbTransaction, updateColumns);
        }

        public static Task<int> UpdateRangeAsync<T>(this IDbConnection connection, IEnumerable<T> list,
            Expression<Func<T, object>>[] updateColumns = null, IDbTransaction dbTransaction = null,
            CancellationToken token = default) where T : class
        {
            return connection.AsDbClient().UpdateRangeAsync(list, updateColumns, dbTransaction, token);
        }

        #endregion Update

        #region Delete

        public static int Delete<T>(this IDbConnection connection, Expression<Func<T, bool>> whereExpression) where T : class
        {
            return connection.AsDbClient().Delete(whereExpression);
        }

        public static int Delete<T>(this IDbConnection connection, T item) where T : class
        {
            return connection.AsDbClient().Delete(item);
        }

        public static Task<int> DeleteAsync<T>(this IDbConnection connection, T item, IDbTransaction dbTransaction = null, CancellationToken token = default)
            where T : class
        {
            return connection.AsDbClient().DeleteAsync(item, dbTransaction, token);
        }

        public static Task<int> DeleteAsync<T>(this IDbConnection connection, Expression<Func<T, bool>> whereExpression, IDbTransaction dbTransaction,
            CancellationToken token = default) where T : class
        {
            return connection.AsDbClient().DeleteAsync(whereExpression, dbTransaction, token);
        }

        public static Task<int> DeleteRangeAsync<T>(this IDbConnection connection, IEnumerable<T> list, IDbTransaction dbTransaction,
            CancellationToken token = default) where T : class
        {
            return connection.AsDbClient().DeleteRangeAsync(list, dbTransaction, token);
        }

        #endregion Delete

        #region Transaction

        public static IDbTransaction BeginTransaction(this IDbConnection connection, IsolationLevel level = IsolationLevel.ReadCommitted)
        {
            return connection.AsDbClient().BeginTransaction(level);
        }

        public static void EndTransaction(this IDbConnection connection)
        {
            connection.AsDbClient().EndTransaction();
        }

        #endregion Transaction

        #region ExecuteNonQuery

        public static int ExecuteNonQuery(this IDbConnection connection, string query, object queryParams = null, IDbTransaction dbTransaction = null)
        {
            return connection.AsDbClient().ExecuteNonQuery(query, queryParams, dbTransaction);
        }

        public static Task<int> ExecuteNonQueryAsync(this IDbConnection connection, string query, object cmdParams = null,
            IDbTransaction dbTransaction = null, CancellationToken token = default)
        {
            return connection.AsDbClient().ExecuteNonQueryAsync(query, cmdParams, dbTransaction, token);
        }

        #endregion

        #region ExecuteScalar

        public static object ExecuteScalar(this IDbConnection connection, string query, object cmdParams = null, IDbTransaction dbTransaction = null)
        {
            return connection.AsDbClient().ExecuteScalar(query, cmdParams, dbTransaction);
        }

        public static object ExecuteScalar(this IDbConnection connection, IDbCommand cmd)
        {
            return connection.AsDbClient().ExecuteScalar(cmd);
        }

        public static TProp ExecuteScalar<T, TProp>(this IDbConnection connection, Expression<Func<T, TProp>> propertySelector, Expression<Func<T, bool>> whereExpression)
        {
            return connection.AsDbClient().ExecuteScalar(propertySelector, whereExpression);
        }

        public static T ExecuteScalar<T>(this IDbConnection connection, string query, object cmdParams = null, IDbTransaction dbTransaction = null)
        {
            return connection.AsDbClient().ExecuteScalar<T>(query, cmdParams, dbTransaction);
        }

        public static T ExecuteScalar<T>(this IDbConnection connection, IDbCommand cmd)
        {
            return connection.AsDbClient().ExecuteScalar<T>(cmd);
        }

        public static Task<object> ExecuteScalarAsync(this IDbConnection connection, string query, object cmdParams = null, IDbTransaction dbTransaction = null, CancellationToken token = default)
        {
            return connection.AsDbClient().ExecuteScalarAsync(query, cmdParams, dbTransaction, token);
        }

        public static Task<object> ExecuteScalarAsync(this IDbConnection connection, IDbCommand cmd, CancellationToken token = default)
        {
            return connection.AsDbClient().ExecuteScalarAsync(cmd, token);
        }

        public static Task<TProp> ExecuteScalarAsync<T, TProp>(this IDbConnection connection, Expression<Func<T, TProp>> propertySelector, Expression<Func<T, bool>> whereExpression, CancellationToken token = default)
        {
            return connection.AsDbClient().ExecuteScalarAsync(propertySelector, whereExpression, token);
        }

        public static Task<T> ExecuteScalarAsync<T>(this IDbConnection connection, string query, object cmdParams = null, IDbTransaction dbTransaction = null, CancellationToken token = default)
        {
            return connection.AsDbClient().ExecuteScalarAsync<T>(query, cmdParams, dbTransaction, token);
        }

        #endregion

        #region First

        public static T First<T>(this IDbConnection connection, string query = null, object cmdParams = null, IEnumerable<string> columns = null,
            IEnumerable<(string, string)> columnToPropertyMap = null, DbClient.DbValueConverter<T> converter = null,
            int offsetRows = 0, Func<T> itemFactory = null)
        {
            return connection.AsDbClient().First(query, cmdParams, columns, columnToPropertyMap, converter, offsetRows, itemFactory);
        }

        public static T First<T>(this IDbConnection connection, Expression<Func<T, bool>> whereExpression,
            IEnumerable<(string, string)> columnToPropertyMap = null, DbClient.DbValueConverter<T> converter = null,
            int offsetRows = 0, Func<T> itemFactory = null,
            params (Expression<Func<T, object>>, bool)[] orderByExpression)
        {
            return connection.AsDbClient().First(whereExpression, columnToPropertyMap, converter, offsetRows, itemFactory, orderByExpression);
        }

        public static async Task<T> FirstAsync<T>(this IDbConnection connection, string query = null, object cmdParams = null,
            IEnumerable<string> columns = null, IEnumerable<(string, string)> columnToPropertyMap = null,
            DbClient.DbValueConverter<T> converter = null, int offsetRows = 0, Func<T> itemFactory = null)
        {
            return await connection.AsDbClient().FirstAsync(query, cmdParams, columns, columnToPropertyMap, converter, offsetRows, itemFactory);
        }

        public static async Task<T> FirstAsync<T>(this IDbConnection connection, Expression<Func<T, bool>> whereExpression,
            IEnumerable<(string, string)> columnToPropertyMap = null, DbClient.DbValueConverter<T> converter = null,
            int offsetRows = 0, Func<T> itemFactory = null, CancellationToken ct = default,
            params (Expression<Func<T, object>>, bool)[] orderByExpression)
        {
            return await connection.AsDbClient().FirstAsync(whereExpression, columnToPropertyMap, converter, offsetRows, itemFactory, ct, orderByExpression);
        }

        #endregion First

        #region Query

        public static TList Query<TList, TItem>(this IDbConnection connection, string query = null, object cmdParams = null,
            IEnumerable<string> columns = null, IEnumerable<(string, string)> columnToPropertyMap = null,
            DbClient.DbValueConverter<TItem> converter = null, int fetchRows = -1, int offsetRows = 0,
            Func<TItem> itemFactory = null) where TList : ICollection<TItem>, new()
        {
            return connection.AsDbClient().Query<TList, TItem>(query, cmdParams, columns, columnToPropertyMap, converter, fetchRows, offsetRows, itemFactory);
        }

        public static Task<TList> QueryAsync<TList, TItem>(this IDbConnection connection, string query = null, object cmdParams = null,
            IEnumerable<string> columns = null, IEnumerable<(string, string)> columnToPropertyMap = null,
            DbClient.DbValueConverter<TItem> converter = null, int fetchRows = -1, int offsetRows = -1,
            Func<TItem> itemFactory = null, CancellationToken ct = default) where TList : ICollection<TItem>, new()
        {
            return connection.AsDbClient().QueryAsync<TList, TItem>(query, cmdParams, columns, columnToPropertyMap, converter, fetchRows, offsetRows, itemFactory, ct);
        }

        #endregion Query

        #region ToDataTables

        public static DataTable ToDataTable<TFrom>(this IDbConnection connection, Expression<Func<TFrom, bool>> whereExpression = null, int fetchRows = -1, int offsetRows = 0, params Expression<Func<TFrom, object>>[] columnSelectors)
        {
            return connection.AsDbClient().ToDataTable(whereExpression, fetchRows, offsetRows, columnSelectors);
        }

        public static Task<DataTable> ToDataTableAsync<TFrom>(this IDbConnection connection, Expression<Func<TFrom, bool>> whereExpression = null, int fetchRows = -1, int offsetRows = 0, params Expression<Func<TFrom, object>>[] columnSelectors)
        {
            return connection.AsDbClient().ToDataTableAsync(whereExpression, fetchRows, offsetRows, columnSelectors);
        }

        public static DataTable ToDataTable(this IDbConnection connection, string query, object cmdParams = null, params (string, string)[] columnMap)
        {
            return connection.AsDbClient().ToDataTable(query, cmdParams, columnMap);
        }

        public static Task<DataTable> ToDataTableAsync(this IDbConnection connection, string query, object cmdParams = null, CancellationToken token = default, params (string, string)[] columnMap)
        {
            return connection.AsDbClient().ToDataTableAsync(query, cmdParams, token, columnMap);
        }

        public static DataTable[] ToDataTables(this IDbConnection connection, string query, object cmdParams = null, params (string, string)[] columnMap)
        {
            return connection.AsDbClient().ToDataTables(query, cmdParams, columnMap);
        }

        public static Task<DataTable[]> ToDataTablesAsync(this IDbConnection connection, string query, object cmdParams = null, CancellationToken token = default, params (string, string)[] columnMap)
        {
            return connection.AsDbClient().ToDataTablesAsync(query, cmdParams, token, columnMap);
        }

        #endregion ToDataTables

        #region ToList

        public static List<TItem> ToList<TItem>(this IDbConnection connection, string query = null, object cmdParams = null,
            IEnumerable<string> columns = null, IEnumerable<(string, string)> columnToPropertyMap = null,
            DbClient.DbValueConverter<TItem> converter = null, int fetchRows = -1, int offsetRows = 0,
            Func<TItem> itemFactory = null)
        {
            return connection.AsDbClient().ToList(query, cmdParams, columns, columnToPropertyMap, converter, fetchRows, offsetRows, itemFactory);
        }

        public static List<T> ToList<T>(this IDbConnection connection, Expression<Func<T, bool>> whereExpression,
            IEnumerable<(string, string)> columnToPropertyMap = null, DbClient.DbValueConverter<T> converter = null,
            int fetchRows = -1, int offsetRows = 0, Func<T> itemFactory = null,
            params (Expression<Func<T, object>>, bool)[] orderByExpression)
        {
            return connection.AsDbClient().ToList(whereExpression, columnToPropertyMap, converter, fetchRows, offsetRows, itemFactory, orderByExpression);
        }

        public static Task<List<T>> ToListAsync<T>(this IDbConnection connection, string query = null, object cmdParams = null,
            IEnumerable<string> columns = null, IEnumerable<(string, string)> columnToPropertyMap = null,
            DbClient.DbValueConverter<T> converter = null, int fetchRows = -1, int offsetRows = 0, Func<T> itemFactory = null,
            CancellationToken ct = default)
        {
            return connection.AsDbClient().ToListAsync(query, cmdParams, columns, columnToPropertyMap, converter, fetchRows, offsetRows, itemFactory, ct);
        }

        public static Task<List<T>> ToListAsync<T>(this IDbConnection connection, Expression<Func<T, bool>> whereExpression,
            IEnumerable<(string, string)> columnToPropertyMap = null, DbClient.DbValueConverter<T> converter = null,
            int fetchRows = -1, int offsetRows = 0, Func<T> itemFactory = null, CancellationToken ct = default,
            params (Expression<Func<T, object>>, bool)[] orderByExpression)
        {
            return connection.AsDbClient().ToListAsync(whereExpression, columnToPropertyMap, converter, fetchRows, offsetRows, itemFactory, ct, orderByExpression);
        }

        #endregion ToList

        #region Aggs

        public static int GetPagesCount<TFrom>(this IDbConnection connection, int pageSize) where TFrom : class
        {
            return connection.AsDbClient().GetPagesCount<TFrom>(pageSize);
        }

        public static Task<int> GetPagesCountAsync<TFrom>(this IDbConnection connection, int pageSize, CancellationToken token = default) where TFrom : class
        {
            return connection.AsDbClient().GetPagesCountAsync<TFrom>(pageSize, token);
        }

        public static Dictionary<int, (int offset, int count)> GetPages<TFrom>(this IDbConnection connection, int pageSize) where TFrom : class
        {
            return connection.AsDbClient().GetPages<TFrom>(pageSize);
        }

        public static Task<Dictionary<int, (int offset, int count)>> GetPagesAsync<TFrom>(this IDbConnection connection, int pageSize, CancellationToken token = default) where TFrom : class
        {
            return connection.AsDbClient().GetPagesAsync<TFrom>(pageSize, token);
        }

        public static object Count(this IDbConnection connection, IDbCommand cmd)
        {
            return connection.AsDbClient().Count(cmd);
        }

        public static Task<object> CountAsync(this IDbConnection connection, IDbCommand cmd)
        {
            return connection.AsDbClient().CountAsync(cmd);
        }

        public static Task<object> CountAsync(this IDbConnection connection, string query, CancellationToken token = default)
        {
            return connection.AsDbClient().CountAsync(query, token);
        }

        public static object Count(this IDbConnection connection, string query)
        {
            return connection.AsDbClient().Count(query);
        }

        public static object Count<TFrom>(this IDbConnection connection, Expression<Func<TFrom, object>> columnSelector = null) where TFrom : class
        {
            return connection.AsDbClient().Count(columnSelector);
        }

        public static T Count<TFrom, T>(this IDbConnection connection, Expression<Func<TFrom, object>> columnSelector = null) where TFrom : class
        {
            return connection.AsDbClient().Count<TFrom, T>(columnSelector);
        }

        public static Task<object> CountAsync<TFrom>(this IDbConnection connection, Expression<Func<TFrom, object>> columnSelector = null, CancellationToken token = default) where TFrom : class
        {
            return connection.AsDbClient().CountAsync(columnSelector, token);
        }

        public static Task<T> CountAsync<TFrom, T>(this IDbConnection connection, Expression<Func<TFrom, object>> columnSelector = null, CancellationToken token = default) where TFrom : class
        {
            return connection.AsDbClient().CountAsync<TFrom, T>(columnSelector, token);
        }

        public static T Max<TFrom, T>(this IDbConnection connection, Expression<Func<TFrom, object>> columnSelector) where TFrom : class
        {
            return connection.AsDbClient().Max<TFrom, T>(columnSelector);
        }

        public static object Max<TFrom>(this IDbConnection connection, Expression<Func<TFrom, object>> columnSelector) where TFrom : class
        {
            return connection.AsDbClient().Max(columnSelector);
        }

        public static Task<T> MaxAsync<TFrom, T>(this IDbConnection connection, Expression<Func<TFrom, object>> columnSelector, CancellationToken token = default) where TFrom : class
        {
            return connection.AsDbClient().MaxAsync<TFrom, T>(columnSelector, token);
        }

        public static Task<object> MaxAsync<TFrom>(this IDbConnection connection, Expression<Func<TFrom, object>> columnSelector, CancellationToken token = default) where TFrom : class
        {
            return connection.AsDbClient().MaxAsync(columnSelector, token);
        }

        public static T Min<TFrom, T>(this IDbConnection connection, Expression<Func<TFrom, object>> columnSelector) where TFrom : class
        {
            return connection.AsDbClient().Min<TFrom, T>(columnSelector);
        }

        public static object Min<TFrom>(this IDbConnection connection, Expression<Func<TFrom, object>> columnSelector) where TFrom : class
        {
            return connection.AsDbClient().Min(columnSelector);
        }

        public static Task<T> MinAsync<TFrom, T>(this IDbConnection connection, Expression<Func<TFrom, object>> columnSelector, CancellationToken token = default) where TFrom : class
        {
            return connection.AsDbClient().MinAsync<TFrom, T>(columnSelector, token);
        }

        public static Task<object> MinAsync<TFrom>(this IDbConnection connection, Expression<Func<TFrom, object>> columnSelector, CancellationToken token = default) where TFrom : class
        {
            return connection.AsDbClient().MinAsync(columnSelector, token);
        }

        public static T Sum<TFrom, T>(this IDbConnection connection, Expression<Func<TFrom, object>> columnSelector) where TFrom : class
        {
            return connection.AsDbClient().Sum<TFrom, T>(columnSelector);
        }

        public static object Sum<TFrom>(this IDbConnection connection, Expression<Func<TFrom, object>> columnSelector) where TFrom : class
        {
            return connection.AsDbClient().Sum(columnSelector);
        }

        public static Task<T> SumAsync<TFrom, T>(this IDbConnection connection, Expression<Func<TFrom, object>> columnSelector, CancellationToken token = default) where TFrom : class
        {
            return connection.AsDbClient().SumAsync<TFrom, T>(columnSelector, token);
        }

        public static Task<object> SumAsync<TFrom>(this IDbConnection connection, Expression<Func<TFrom, object>> columnSelector, CancellationToken token = default) where TFrom : class
        {
            return connection.AsDbClient().SumAsync(columnSelector, token);
        }

        public static T Avg<TFrom, T>(this IDbConnection connection, Expression<Func<TFrom, object>> columnSelector) where TFrom : class
        {
            return connection.AsDbClient().Avg<TFrom, T>(columnSelector);
        }

        public static object Avg<TFrom>(this IDbConnection connection, Expression<Func<TFrom, object>> columnSelector) where TFrom : class
        {
            return connection.AsDbClient().Avg(columnSelector);
        }

        public static Task<object> AvgAsync<TFrom>(this IDbConnection connection, Expression<Func<TFrom, object>> columnSelector, CancellationToken token = default) where TFrom : class
        {
            return connection.AsDbClient().AvgAsync(columnSelector, token);
        }

        public static Dictionary<string, (long Count, long Min, long Max, long Sum, decimal Avg)> GetAggs<TFrom>(this IDbConnection connection, params Expression<Func<TFrom, object>>[] columnSelector) where TFrom : class
        {
            return connection.AsDbClient().GetAggs(columnSelector);
        }

        public static Task<Dictionary<string, (long Count, long Min, long Max, long Sum, decimal Avg)>> GetAggsAsync<TFrom>(this IDbConnection connection, CancellationToken token = default, params Expression<Func<TFrom, object>>[] columnSelector) where TFrom : class
        {
            return connection.AsDbClient().GetAggsAsync(token, columnSelector);
        }

        public static Dictionary<string, object> Agg<TFrom>(this IDbConnection connection, string aggFunction, params Expression<Func<TFrom, object>>[] columnSelectors) where TFrom : class
        {
            return connection.AsDbClient().Agg(aggFunction, columnSelectors);
        }

        public static Task<Dictionary<string, object>> AggAsync<TFrom>(this IDbConnection connection, string aggFunction, CancellationToken token = default, params Expression<Func<TFrom, object>>[] columnSelectors) where TFrom : class
        {
            return connection.AsDbClient().AggAsync(aggFunction, token, columnSelectors);
        }

        public static Dictionary<string, object> Agg<TFrom>(this IDbConnection connection, params (Expression<Func<TFrom, object>> column, string aggFunction)[] columnSelectors) where TFrom : class
        {
            return connection.AsDbClient().Agg(columnSelectors);
        }

        public static Task<Dictionary<string, object>> AggAsync<TFrom>(this IDbConnection connection, CancellationToken token = default, params (Expression<Func<TFrom, object>> column, string aggFunction)[] columnSelectors) where TFrom : class
        {
            return connection.AsDbClient().AggAsync(token, columnSelectors);
        }

        public static string GetRawSql(this IDbConnection connection, IDbCommand command)
        {
            return connection.AsDbClient().GetRawSql(command);
        }

        #endregion

        #region Command

        public static DbCommand CreateCommand(this IDbConnection connection, string query, object cmdParams, IDbTransaction dbTransaction = null,
            int commandTimeOut = 30)
        {
            return connection.AsDbClient().CreateCommand(query, cmdParams, dbTransaction, commandTimeOut);
        }

        public static Dictionary<string, object> GetParams(this IDbConnection connection, object cmdParams, params string[] propertyNames)
        {
            return connection.AsDbClient().GetParams(cmdParams, propertyNames);
        }

        #endregion
    }
}