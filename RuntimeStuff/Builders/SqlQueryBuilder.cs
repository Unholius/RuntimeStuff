// ***********************************************************************
// Assembly         : RuntimeStuff
// Author           : RS
// Created          : 01-06-2026
//
// Last Modified By : RS
// Last Modified On : 01-07-2026
// ***********************************************************************
// <copyright file="SqlQueryBuilder.cs" company="Rudnev Sergey">
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************
namespace RuntimeStuff.Builders
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Text;
    using RuntimeStuff.Extensions;
    using RuntimeStuff.Helpers;
    using RuntimeStuff.Options;

    /// <summary>
    /// Class SqlQueryBuilder.
    /// </summary>
    public static class SqlQueryBuilder
    {
        /// <summary>
        /// Enum JoinType.
        /// </summary>
        public enum JoinType
        {
            /// <summary>
            /// The inner
            /// </summary>
            Inner,

            /// <summary>
            /// The left
            /// </summary>
            Left,

            /// <summary>
            /// The right
            /// </summary>
            Right,

            /// <summary>
            /// The full
            /// </summary>
            Full,
        }

        /// <summary>
        /// The empty parameters.
        /// </summary>
        private static readonly IReadOnlyDictionary<string, object> EmptyParams = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>());

        /// <summary>
        /// Gets the join clause.
        /// </summary>
        /// <param name="from">From.</param>
        /// <param name="joinOn">The join on.</param>
        /// <param name="options">The options.</param>
        /// <param name="joinType">Type of the join.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">from.</exception>
        /// <exception cref="System.ArgumentNullException">joinOn.</exception>
        /// <exception cref="System.InvalidOperationException">Foreign key between {from.Name} and {joinOn.Name} not found.</exception>
        /// <exception cref="System.InvalidOperationException">Failed to determine join columns.</exception>
        public static string GetJoinClause(Type from, Type joinOn, SqlProviderOptions options, JoinType joinType = JoinType.Inner)
        {
            if (from == null)
            {
                throw new ArgumentNullException(nameof(from));
            }

            if (joinOn == null)
            {
                throw new ArgumentNullException(nameof(joinOn));
            }

            var parentCache = MemberCache.Create(from);
            var childrenCache = MemberCache.Create(joinOn);

            var parentTable = parentCache.TableName;
            var childTable = childrenCache.TableName;

            string parentColumn = null;
            string childColumn = null;

            // Попробуем найти FK в children → parent
            var fkInChildren = childrenCache.GetForeignKey(from);
            if (fkInChildren != null)
            {
                parentColumn = parentCache.PrimaryKeys.FirstOrDefault().Value?.ColumnName;
                childColumn = fkInChildren.ColumnName;
            }
            else
            {
                // Если FK в children не найден, ищем FK в parent → children
                var fkInParent = parentCache.GetForeignKey(joinOn);
                if (fkInParent != null)
                {
                    // FK в parent: столбцы меняем местами, чтобы parent остался FROM
                    parentColumn = fkInParent.ColumnName;
                    childColumn = childrenCache.PrimaryKeys.FirstOrDefault().Value?.ColumnName;
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Foreign key between {from.Name} and {joinOn.Name} not found");
                }
            }

            if (parentColumn == null || childColumn == null)
            {
                throw new InvalidOperationException("Failed to determine join columns.");
            }

            var np = options.NamePrefix;
            var ns = options.NameSuffix;

            return $"{joinType.ToString().ToUpper()} JOIN {np}{childTable}{ns} ON {np}{childTable}{ns}.{np}{childColumn}{ns} = {np}{parentTable}{ns}.{np}{parentColumn}{ns}";
        }

        /// <summary>
        /// Gets the aggregate select clause.
        /// </summary>
        /// <typeparam name="TFrom">The type of the t from.</typeparam>
        /// <param name="options">The options.</param>
        /// <param name="columnSelectors">The column selectors.</param>
        /// <returns>System.String.</returns>
        public static string GetAggSelectClause<TFrom>(SqlProviderOptions options, params (Expression<Func<TFrom, object>> column, string aggFunction)[] columnSelectors)
            where TFrom : class
        {
            var query = "SELECT " + (columnSelectors.Length == 0
                          ? "COUNT(*)"
                          : string.Join(
                                ", ",
                                columnSelectors.Select(c =>
                              {
                                  var colName = $"{options.NamePrefix}{options.Map?.ResolveColumnName(c.column?.GetPropertyInfo(), null, null) ?? c.column?.GetMemberCache()?.ColumnName ?? "*"}{options.NameSuffix}".Replace("\"*\"", "*");
                                  return $"{c.aggFunction}({colName})";
                              }))
                      + $" FROM {options.NamePrefix}{options.Map?.ResolveTableName(typeof(TFrom), null, null) ?? typeof(TFrom).GetMemberCache().TableName}{options.NameSuffix}");

            return query;
        }

        /// <summary>
        /// Gets the where clause.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="whereExpression">The where expression.</param>
        /// <param name="options">The options.</param>
        /// <param name="useParams">if set to <c>true</c> [use parameters].</param>
        /// <param name="cmdParams">The command parameters.</param>
        /// <returns>System.String.</returns>
        public static string GetWhereClause<T>(Expression<Func<T, bool>> whereExpression, SqlProviderOptions options, bool useParams, out IReadOnlyDictionary<string, object> cmdParams)
        {
            cmdParams = EmptyParams;
            var dic = new Dictionary<string, object>();
            var whereClause = whereExpression == null ? string.Empty : ("WHERE " + Visit(whereExpression.Body, options, useParams, dic)).Trim();
            cmdParams = dic;
            return whereClause;
        }

        /// <summary>
        /// Gets the where clause.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="options">The options.</param>
        /// <param name="cmdParams">The command parameters.</param>
        /// <returns>System.String.</returns>
        public static string GetWhereClause<T>(SqlProviderOptions options, out Dictionary<string, object> cmdParams)
        {
            var mi = MemberCache.Create(typeof(T));
            var keys = mi.PrimaryKeys.Values.ToArray();
            if (keys.Length == 0)
            {
                keys = mi.PublicBasicProperties.Values.ToArray();
            }

            return GetWhereClause(keys, options, out cmdParams);
        }

        /// <summary>
        /// Gets the where clause.
        /// </summary>
        /// <param name="whereProperties">The where properties.</param>
        /// <param name="options">The options.</param>
        /// <param name="cmdParams">The command parameters.</param>
        /// <returns>System.String.</returns>
        public static string GetWhereClause(MemberCache[] whereProperties, SqlProviderOptions options, out Dictionary<string, object> cmdParams)
        {
            cmdParams = new Dictionary<string, object>();
            var whereClause = new StringBuilder("WHERE ");

            for (var i = 0; i < whereProperties.Length; i++)
            {
                var key = whereProperties[i];

                whereClause
                    .Append(options.NamePrefix)
                    .Append(options.Map?.ResolveColumnName(key, null, null) ?? key.ColumnName)
                    .Append(options.NameSuffix)
                    .Append(" = ")
                    .Append(options.ParamPrefix)
                    .Append(key.Name);

                if (i < whereProperties.Length - 1)
                {
                    whereClause.Append(" AND ");
                }

                cmdParams[key.ColumnName] = null;
            }

            return whereClause.ToString();
        }

        /// <summary>
        /// Gets the select query.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="options">The options.</param>
        /// <param name="selectColumns">The select columns.</param>
        /// <returns>System.String.</returns>
        public static string GetSelectQuery<T>(SqlProviderOptions options, params Expression<Func<T, object>>[] selectColumns)
        {
            var mi = MemberCache<T>.Create();
            var members = selectColumns?.Select(ExpressionHelper.GetMemberInfo).Select(x => x.GetMemberCache()).ToArray() ?? Array.Empty<MemberCache>();
            if (members.Length == 0)
            {
                members = mi.ColumnProperties.Values.ToArray().Concat(mi.PrimaryKeys.Values).ToArray();
            }

            if (members.Length == 0)
            {
                return $"SELECT * FROM {options.NamePrefix}{options.Map?.ResolveTableName(mi, options.NamePrefix, options.NameSuffix) ?? mi.TableName}{options.NameSuffix}";
            }

            return GetSelectQuery(options, mi, members);
        }

        /// <summary>
        /// Gets the select query.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TProp">The type of the t property.</typeparam>
        /// <param name="options">The options.</param>
        /// <param name="selectColumns">The select columns.</param>
        /// <returns>System.String.</returns>
        public static string GetSelectQuery<T, TProp>(SqlProviderOptions options, params Expression<Func<T, TProp>>[] selectColumns) => GetSelectQuery(options, MemberCache<T>.Create(), selectColumns.Select(x => x.GetMemberCache()).ToArray());

        /// <summary>
        /// Gets the select query.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="typeInfo">The type information.</param>
        /// <param name="selectColumns">The select columns.</param>
        /// <returns>System.String.</returns>
        public static string GetSelectQuery(SqlProviderOptions options, MemberCache typeInfo, params MemberCache[] selectColumns)
        {
            if (selectColumns.Length == 0)
            {
                selectColumns = typeInfo.GetColumns();
            }

            var query = new StringBuilder("SELECT ");

            foreach (var pi in selectColumns)
            {
                query.Append(options.NamePrefix)
                    .Append(options.Map?.ResolveColumnName(pi, null, null) ?? pi.ColumnName)
                    .Append(options.NameSuffix)
                    .Append(", ");
            }

            if (query[query.Length - 2] == ',')
            {
                query.Remove(query.Length - 2, 2);
            }

            query.Append(" FROM ")
                .Append(options.Map?.ResolveTableName(typeInfo, options.NamePrefix, options.NameSuffix) ?? typeInfo.GetFullTableName(options.NamePrefix, options.NameSuffix));

            return query.ToString();
        }

        /// <summary>
        /// Gets the update query.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="options">The options.</param>
        /// <param name="updateColumns">The update columns.</param>
        /// <returns>System.String.</returns>
        public static string GetUpdateQuery<T>(SqlProviderOptions options, params Expression<Func<T, object>>[] updateColumns)
            where T : class
        {
            var mi = MemberCache.Create(typeof(T));
            var query = new StringBuilder("UPDATE ")
                .Append(options.Map?.ResolveTableName(mi, options.NamePrefix, options.NameSuffix) ?? mi.GetFullTableName(options.NamePrefix, options.NameSuffix))
                .Append(" SET ");

            var props = updateColumns?.Select(ExpressionHelper.GetPropertyName).ToList()
                        ?? new List<string>();

            if (props.Count == 0)
            {
                props.AddRange(mi.ColumnProperties
                    .Where(x => !x.Value.IsPrimaryKey && x.Value.IsSetterPublic)
                    .Select(x => x.Value.ColumnName));
            }

            if (props.Count == 0)
            {
                props.AddRange(mi.PublicBasicProperties
                    .Where(x => x.Key.ToLower() != "id" && x.Value.IsSetterPublic)
                    .Select(x => x.Value.ColumnName));
            }
            else
            {
                foreach (var p in props)
                {
                    var pi = mi[p];
                    query.Append(options.NamePrefix)
                        .Append(options.Map?.ResolveColumnName(pi, null, null) ?? pi.ColumnName)
                        .Append(options.NameSuffix)
                        .Append(" = ")
                        .Append(options.ParamPrefix)
                        .Append(pi.Name)
                        .Append(", ");
                }
            }

            if (query[query.Length - 2] == ',')
            {
                query.Remove(query.Length - 2, 2);
            }

            return query.ToString();
        }

        /// <summary>
        /// Gets the insert query.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="options">The options.</param>
        /// <param name="insertColumns">The insert columns.</param>
        /// <returns>System.String.</returns>
        public static string GetInsertQuery<T>(SqlProviderOptions options, params Expression<Func<T, object>>[] insertColumns)
            where T : class
        {
            var query = new StringBuilder("INSERT INTO ");
            var mi = MemberCache<T>.Create();
            query
                .Append(options.Map?.ResolveTableName(mi, options.NamePrefix, options.NameSuffix) ?? mi.GetFullTableName(options.NamePrefix, options.NameSuffix))
                .Append(" (");

            var insertCols = insertColumns?.Select(ExpressionHelper.GetPropertyName).ToArray() ?? Array.Empty<string>();
            if (insertCols.Length == 0)
            {
                insertCols = mi.ColumnProperties.Values.Where(x => x.IsSetterPublic).Select(x => x.Name).ToArray();
            }

            for (var i = 0; i < insertCols.Length; i++)
            {
                var col = insertCols[i];

                query
                    .Append(options.NamePrefix)
                    .Append(options.Map?.ResolveColumnName(mi[col], null, null) ?? mi[col].ColumnName)
                    .Append(options.NameSuffix);

                if (i < insertCols.Length - 1)
                {
                    query.Append(", ");
                }
            }

            query
                .Append(") VALUES (");

            for (var i = 0; i < insertCols.Length; i++)
            {
                var col = insertCols[i];

                query
                    .Append(options.ParamPrefix)
                    .Append(mi[col].Name);

                if (i < insertCols.Length - 1)
                {
                    query.Append(", ");
                }
            }

            query.Append(")");

            return query.ToString();
        }

        /// <summary>
        /// Gets the delete query.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="options">The options.</param>
        /// <returns>System.String.</returns>
        public static string GetDeleteQuery<T>(SqlProviderOptions options)
            where T : class
        {
            var mi = MemberCache<T>.Create();
            var query = new StringBuilder("DELETE FROM ").Append(options.Map?.ResolveTableName(mi, options.NamePrefix, options.NameSuffix) ?? mi.GetFullTableName(options.ParamPrefix, options.NameSuffix));
            return query.ToString();
        }

        /// <summary>
        /// Gets the order by.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="options">The options.</param>
        /// <param name="orderBy">The order by.</param>
        /// <returns>System.String.</returns>
        public static string GetOrderBy<T>(SqlProviderOptions options, params (Expression<Func<T, object>>, bool)[] orderBy)
        {
            if (orderBy == null)
            {
                return string.Empty;
            }

            var props = orderBy.Select(x => (ExpressionHelper.GetMemberInfo(x.Item1).GetMemberCache(), x.Item2)).ToArray();
            return GetOrderBy(options, props);
        }

        /// <summary>
        /// Gets the order by.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="orderBy">The order by.</param>
        /// <returns>System.String.</returns>
        public static string GetOrderBy(SqlProviderOptions options, params (MemberCache, bool)[] orderBy)
        {
            if (orderBy == null || orderBy.Length == 0)
            {
                return string.Empty;
            }

            var query = new StringBuilder("ORDER BY ");

            foreach (var mi in orderBy)
            {
                query
                    .Append(options.NamePrefix)
                    .Append(options.Map?.ResolveColumnName(mi.Item1, null, null) ?? mi.Item1.ColumnName)
                    .Append(options.NameSuffix)
                    .Append(mi.Item2 ? " ASC, " : " DESC, ");
            }

            if (query[query.Length - 2] == ',')
            {
                query.Remove(query.Length - 2, 2);
            }

            return query.ToString();
        }

        /// <summary>
        /// Adds the limit offset clause to query.
        /// </summary>
        /// <param name="fetchRows">The fetch rows.</param>
        /// <param name="offsetRows">The offset rows.</param>
        /// <param name="query">The query.</param>
        /// <param name="options">The options.</param>
        /// <param name="entityType">Type of the entity.</param>
        /// <returns>System.String.</returns>
        public static string AddLimitOffsetClauseToQuery(int fetchRows, int offsetRows, string query, SqlProviderOptions options, Type entityType = null)
        {
            if (fetchRows < 0 || offsetRows < 0)
            {
                return query;
            }

            var offsetRowsFetchNextRowsOnly =
                options.OverrideOffsetRowsTemplate ?? "OFFSET {0} ROWS FETCH NEXT {1} ROWS ONLY";

            var clause = new StringBuilder(query);

            if (query?.Contains("ORDER BY", StringComparison.OrdinalIgnoreCase) != true)
            {
                var mi = MemberCache.Create(entityType);

                clause.Append(" ORDER BY ");
                _ = clause.Append(string.Join(
                    ", ",
                    mi.PrimaryKeys.Count > 0 ? mi.PrimaryKeys.Values.Select(x => options.Map?.ResolveColumnName(x, options.NamePrefix, options.NameSuffix) ?? (options.NamePrefix + x.ColumnName + options.NameSuffix)) : mi.ColumnProperties.Values.Select(x => options.Map?.ResolveColumnName(x, options.NamePrefix, options.NameSuffix) ?? (options.NamePrefix + x.ColumnName + options.NameSuffix))));
                clause.Append(" ");
            }

            clause.Append(string.Format(
                offsetRowsFetchNextRowsOnly,
                offsetRows,
                fetchRows));

            return clause.ToString().Trim();
        }

        /// <summary>
        /// Visits the specified exp.
        /// </summary>
        /// <param name="exp">The exp.</param>
        /// <param name="options">The options.</param>
        /// <param name="useParams">if set to <c>true</c> [use parameters].</param>
        /// <param name="cmdParams">The command parameters.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.NotSupportedException">Expression '{exp.NodeType}' is not supported.</exception>
        private static string Visit(Expression exp, SqlProviderOptions options, bool useParams, Dictionary<string, object> cmdParams)
        {
            switch (exp)
            {
                case BinaryExpression be:
                    return VisitBinary(be, options, useParams, cmdParams);

                case MemberExpression me:
                    return VisitMember(me, options, useParams, cmdParams);

                case ConstantExpression ce:
                    return VisitConstant(ce, options);

                case UnaryExpression ue:
                    return VisitUnary(ue, options, useParams, cmdParams);

                default:
                    throw new NotSupportedException($"Expression '{exp.NodeType}' is not supported.");
            }
        }

        /// <summary>
        /// Visits the binary.
        /// </summary>
        /// <param name="be">The be.</param>
        /// <param name="options">The options.</param>
        /// <param name="useParams">if set to <c>true</c> [use parameters].</param>
        /// <param name="cmdParams">The command parameters.</param>
        /// <returns>System.String.</returns>
        private static string VisitBinary(BinaryExpression be, SqlProviderOptions options, bool useParams, Dictionary<string, object> cmdParams)
        {
            var left = Visit(be.Left, options, useParams, cmdParams);
            var right = Visit(be.Right, options, useParams, cmdParams);
            var op = GetSqlOperator(be.NodeType);

            if (be.Left is MemberExpression me && useParams)
            {
                if (be.Right is UnaryExpression ue && ue.NodeType == ExpressionType.Convert)
                {
                    var paramName = me.Member.Name + "_" + (cmdParams.Count + 1);
                    right = options.ParamPrefix + paramName;
                    if (ue.Operand is ConstantExpression ce)
                    {
                        cmdParams[paramName] = ce?.Value;
                    }

                    if (ue.Operand is MemberExpression me2)
                    {
                        cmdParams[paramName] = GetValue(me2);
                    }
                }

                if (be.Right is MemberExpression rme)
                {
                    var paramName = me.Member.Name + "_" + (cmdParams.Count + 1);
                    right = options.ParamPrefix + paramName;
                    cmdParams[paramName] = GetValue(rme);
                }
            }

            return $"({left} {op} {right})";
        }

        /// <summary>
        /// Visits the unary.
        /// </summary>
        /// <param name="ue">The ue.</param>
        /// <param name="options">The options.</param>
        /// <param name="useParams">if set to <c>true</c> [use parameters].</param>
        /// <param name="cmdParams">The command parameters.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.NotSupportedException">Unary '{ue.NodeType}' not supported.</exception>
        private static string VisitUnary(UnaryExpression ue, SqlProviderOptions options, bool useParams, Dictionary<string, object> cmdParams)
        {
            switch (ue.NodeType)
            {
                case ExpressionType.Not:
                    return $"(NOT {Visit(ue.Operand, options, useParams, cmdParams)})";

                case ExpressionType.Convert:
                    return Visit(ue.Operand, options, useParams, cmdParams);

                default:
                    throw new NotSupportedException($"Unary '{ue.NodeType}' not supported.");
            }
        }

        /// <summary>
        /// Visits the member.
        /// </summary>
        /// <param name="me">Me.</param>
        /// <param name="options">The options.</param>
        /// <param name="useParams">if set to <c>true</c> [use parameters].</param>
        /// <param name="cmdParams">The command parameters.</param>
        /// <returns>System.String.</returns>
        private static string VisitMember(MemberExpression me, SqlProviderOptions options, bool useParams, Dictionary<string, object> cmdParams)
        {
            var mi = MemberCache.Create(me.Member);
            if (me.Expression != null && me.Expression.NodeType == ExpressionType.Parameter)
            {
                return options.NamePrefix + (options.Map?.ResolveColumnName(mi, options.NamePrefix, options.NameSuffix) ?? mi.ColumnName) + options.NameSuffix;
            }

            var value = GetValue(me);
            var paramName = (mi.ColumnName ?? mi.Name) + "_" + (cmdParams.Count + 1);
            return useParams ? options.ParamPrefix + paramName : options.ToSqlLiteral(value);
        }

        /// <summary>
        /// Visits the constant.
        /// </summary>
        /// <param name="ce">The ce.</param>
        /// <param name="options">The options.</param>
        /// <returns>System.String.</returns>
        private static string VisitConstant(ConstantExpression ce, SqlProviderOptions options) => options.ToSqlLiteral(ce.Value);

        /// <summary>
        /// Gets the value.
        /// </summary>
        /// <param name="me">Me.</param>
        /// <returns>System.Object.</returns>
        private static object GetValue(MemberExpression me)
        {
            var lambda = Expression.Lambda<Func<object>>(
                Expression.Convert(me, typeof(object)));
            return lambda.Compile().Invoke();
        }

        /// <summary>
        /// Gets the SQL operator.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.NotSupportedException">Operator '{type}' not supported.</exception>
        private static string GetSqlOperator(ExpressionType type)
        {
            switch (type)
            {
                case ExpressionType.Equal:
                    return "=";

                case ExpressionType.NotEqual:
                    return "<>";

                case ExpressionType.GreaterThan:
                    return ">";

                case ExpressionType.GreaterThanOrEqual:
                    return ">=";

                case ExpressionType.LessThan:
                    return "<";

                case ExpressionType.LessThanOrEqual:
                    return "<=";

                case ExpressionType.AndAlso:
                    return "AND";

                case ExpressionType.OrElse:
                    return "OR";

                default:
                    throw new NotSupportedException($"Operator '{type}' not supported.");
            }
        }
    }
}