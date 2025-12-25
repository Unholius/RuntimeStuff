using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using RuntimeStuff.Extensions;
using RuntimeStuff.Helpers;

namespace RuntimeStuff.Builders
{
    public static class SqlQueryBuilder
    {
        public static string GetWhereClause<T>(Expression<Func<T, bool>> whereExpression, SqlProviderOptions options, bool useParams, out Dictionary<string, object> cmdParams)
        {
            cmdParams = new Dictionary<string, object>();
            if (whereExpression == null)
            {
                return null;
            }

            return ("WHERE " + Visit(whereExpression.Body, options, useParams, cmdParams)).Trim();
        }

        public static string GetWhereClause<T>(SqlProviderOptions options)
        {
            var mi = MemberCache.Create(typeof(T));
            var keys = mi.PrimaryKeys.Values.ToArray();
            if (keys.Length == 0)
                keys = mi.PublicBasicProperties.Values.ToArray();

            return GetWhereClause(keys, options);
        }

        public static string GetWhereClause(MemberCache[] whereProperties, SqlProviderOptions options)
        {
            var whereClause = new StringBuilder("WHERE ");

            for (var i = 0; i < whereProperties.Length; i++)
            {
                var key = whereProperties[i];

                whereClause
                    .Append(options.NamePrefix)
                    .Append(key.ColumnName)
                    .Append(options.NameSuffix)
                    .Append(" = ")
                    .Append(options.ParamPrefix)
                    .Append(key.Name);

                if (i < whereProperties.Length - 1)
                    whereClause.Append(" AND ");
            }

            return whereClause.ToString();
        }

        public static string GetSelectQuery<T>(SqlProviderOptions options, params Expression<Func<T, object>>[] selectColumns)
        {
            return GetSelectQuery(options.NamePrefix, options.NameSuffix, selectColumns);
        }

        public static string GetSelectQuery<T, TProp>(SqlProviderOptions options, params Expression<Func<T, TProp>>[] selectColumns)
        {
            return GetSelectQuery(options.NamePrefix, options.NameSuffix, MemberCache<T>.Create(), selectColumns.Select(x=>x.GetMemberCache()).ToArray());
        }

        public static string GetSelectQuery<T>(string namePrefix, string nameSuffix, params Expression<Func<T, object>>[] selectColumns)
        {
            var mi = MemberCache<T>.Create();
            var members = selectColumns?.Select(ExpressionHelper.GetMemberInfo).Select(x=>x.GetMemberCache()).ToArray() ?? Array.Empty<MemberCache>();
            if (members.Length == 0)
                members = mi.ColumnProperties.Values.ToArray().Concat(mi.PrimaryKeys.Values).ToArray();
            if (members.Length == 0)
                return $"SELECT * FROM {namePrefix}{mi.TableName}{nameSuffix}";

            return GetSelectQuery(namePrefix, nameSuffix, mi, members);
        }

        public static string GetSelectQuery(SqlProviderOptions options, MemberCache typeInfo, params MemberCache[] selectColumns)
        {
            return GetSelectQuery(options.NamePrefix, options.NameSuffix, typeInfo, selectColumns);
        }

        public static string GetSelectQuery(string namePrefix, string nameSuffix, MemberCache typeInfo, params MemberCache[] selectColumns)
        {
            if (selectColumns.Length == 0)
                return "";

            var query = new StringBuilder("SELECT ");

            foreach (var pi in selectColumns)
            {
                query.Append(namePrefix)
                    .Append(pi.ColumnName)
                    .Append(nameSuffix)
                    .Append(", ");
            }

            if (query[query.Length - 2] == ',')
                query.Remove(query.Length - 2, 2);

            query.Append(" FROM ")
                .Append(typeInfo.GetFullTableName(namePrefix, nameSuffix));

            return query.ToString();
        }

        public static string GetUpdateQuery<T>(SqlProviderOptions options, params Expression<Func<T, object>>[] updateColumns) where T : class
        {
            return GetUpdateQuery(options.NamePrefix, options.NameSuffix, options.ParamPrefix, updateColumns);
        }

        public static string GetUpdateQuery<T>(string namePrefix, string nameSuffix, string paramPrefix, params Expression<Func<T, object>>[] updateColumns) where T : class
        {
            var mi = MemberCache.Create(typeof(T));
            var query = new StringBuilder("UPDATE ")
                .Append(mi.GetFullTableName(namePrefix, nameSuffix, null))
                .Append(" SET ");

            var props = updateColumns?.Select(ExpressionHelper.GetPropertyName).ToList()
                        ?? new List<string>();

            if (props.Count == 0)
                props.AddRange(mi.ColumnProperties
                    .Where(x => !x.Value.IsPrimaryKey && x.Value.IsSetterPublic)
                    .Select(x => x.Value.ColumnName));

            if (props.Count == 0)
                props.AddRange(mi.PublicBasicProperties
                    .Where(x => x.Key.ToLower() != "id" && x.Value.IsSetterPublic)
                    .Select(x => x.Value.ColumnName));
            else
            {
                foreach (var p in props)
                {
                    var pi = mi[p];
                    query.Append(namePrefix)
                        .Append(pi.ColumnName)
                        .Append(nameSuffix)
                        .Append(" = ")
                        .Append(paramPrefix)
                        .Append(pi.Name)
                        .Append(", ");
                }
            }

            if (query[query.Length - 2] == ',')
                query.Remove(query.Length - 2, 2);

            return query.ToString();
        }

        public static string GetInsertQuery<T>(SqlProviderOptions options, params Expression<Func<T, object>>[] insertColumns) where T : class
        {
            return GetInsertQuery(options.NamePrefix, options.NameSuffix, options.ParamPrefix, insertColumns);
        }

        public static string GetInsertQuery<T>(string namePrefix, string nameSuffix, string paramPrefix, params Expression<Func<T, object>>[] insertColumns) where T : class
        {
            var query = new StringBuilder("INSERT INTO ");
            var mi = MemberCache<T>.Create();
            query
                .Append(mi.GetFullTableName(namePrefix, nameSuffix))
                .Append(" (");

            var insertCols = insertColumns?.Select(ExpressionHelper.GetPropertyName)?.ToArray() ?? Array.Empty<string>();
            if (insertCols.Length == 0)
                insertCols = mi.ColumnProperties.Values.Where(x => x.IsSetterPublic).Select(x => x.Name).ToArray();

            for (var i = 0; i < insertCols.Length; i++)
            {
                var col = insertCols[i];

                query
                    .Append(namePrefix)
                    .Append(mi[col].ColumnName)
                    .Append(nameSuffix);

                if (i < insertCols.Length - 1)
                    query.Append(", ");
            }

            query
                .Append(") VALUES (");

            for (var i = 0; i < insertCols.Length; i++)
            {
                var col = insertCols[i];

                query
                    .Append(paramPrefix)
                    .Append(mi[col].Name);

                if (i < insertCols.Length - 1)
                    query.Append(", ");
            }

            query.Append(")");

            return query.ToString();
        }

        public static string GetDeleteQuery<T>(SqlProviderOptions options) where T : class
        {
            return GetDeleteQuery<T>(options.NamePrefix, options.NameSuffix);
        }

        public static string GetDeleteQuery<T>(string namePrefix, string nameSuffix) where T : class
        {
            var mi = MemberCache<T>.Create();
            var query = new StringBuilder("DELETE FROM ").Append(mi.GetFullTableName(namePrefix, nameSuffix));
            return query.ToString();
        }

        public static string GetOrderBy<T>(SqlProviderOptions options, params (Expression<Func<T, object>>, bool)[] orderBy)
        {
            return GetOrderBy(options.NamePrefix, options.NameSuffix, orderBy);
        }

        public static string GetOrderBy<T>(string namePrefix, string nameSuffix, params (Expression<Func<T, object>>, bool)[] orderBy)
        {
            if (orderBy == null)
                return "";
            var props = orderBy.Select(x => (ExpressionHelper.GetMemberInfo(x.Item1).GetMemberCache(), x.Item2)).ToArray();
            return GetOrderBy(namePrefix, nameSuffix, props);
        }

        public static string GetOrderBy(string namePrefix, string nameSuffix, params (MemberCache, bool)[] orderBy)
        {
            if (orderBy == null || orderBy.Length == 0)
                return "";

            var query = new StringBuilder("ORDER BY ");

            foreach (var mi in orderBy)
            {
                query
                    .Append(namePrefix)
                    .Append(mi.Item1.ColumnName)
                    .Append(nameSuffix)
                    .Append(mi.Item2 ? " ASC, " : " DESC, ");
            }

            if (query[query.Length - 2] == ',')
                query.Remove(query.Length - 2, 2);

            return query.ToString();
        }

        public static string OverrideOffsetRowsTemplate { get; set; }

        public static string AddLimitOffsetClauseToQuery(int fetchRows, int offsetRows, string query, Type connectionType = null, Type entityType = null, string namePrefix="\"", string nameSuffix="\"")
        {
            if (fetchRows < 0 || offsetRows < 0)
                return query;

            var offsetRowsFetchNextRowsOnly =
                OverrideOffsetRowsTemplate ?? "OFFSET {0} ROWS FETCH NEXT {1} ROWS ONLY";

            var clause = new StringBuilder(query);

            switch (connectionType?.Name.ToLower())
            {
                case "mysqlconnection":
                    offsetRowsFetchNextRowsOnly = "LIMIT {1} OFFSET {0}";
                    break;
            }

            if (query?.Contains("ORDER BY", StringComparison.OrdinalIgnoreCase) != true)
            {
                var mi = MemberCache.Create(entityType);

                clause.Append(" ORDER BY ");
                clause.Append(string.Join(", ",
                    mi.PrimaryKeys.Count > 0
                        ? mi.PrimaryKeys.Values.Select(x => namePrefix + x.ColumnName + nameSuffix)
                        : mi.ColumnProperties.Values.Select(x => namePrefix + x.ColumnName + nameSuffix)));
                clause.Append(" ");
            }

            clause.Append(string.Format(
                offsetRowsFetchNextRowsOnly,
                offsetRows,
                fetchRows));

            return clause.ToString().Trim();
        }

        #region PRIVATE

        private static string Visit(Expression exp, SqlProviderOptions options, bool useParams, Dictionary<string, object> cmdParams)
        {

            switch (exp)
            {
                case BinaryExpression be:
                    return VisitBinary(be, options, useParams, cmdParams);

                case MemberExpression me:
                    return VisitMember(me, options, useParams, cmdParams);

                case ConstantExpression ce:
                    return VisitConstant(ce, options, useParams, cmdParams);

                case UnaryExpression ue:
                    return VisitUnary(ue, options, useParams, cmdParams);

                default:
                    throw new NotSupportedException($"Expression '{exp.NodeType}' is not supported.");
            }
        }

        private static string VisitBinary(BinaryExpression be, SqlProviderOptions options, bool useParams, Dictionary<string, object> cmdParams)
        {
            var left = Visit(be.Left, options, useParams, cmdParams);
            var right = Visit(be.Right, options, useParams, cmdParams);
            var op = GetSqlOperator(be.NodeType);

            if (be.Left is MemberExpression me && be.Right is UnaryExpression ue && ue.NodeType == ExpressionType.Convert && useParams)
            {
                var paramName = me.Member.Name + "_" + (cmdParams.Count + 1);
                right = options.ParamPrefix + paramName;
                cmdParams[paramName] = (ue.Operand as ConstantExpression)?.Value;
            }

            return $"({left} {op} {right})";
        }

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

        private static string VisitMember(MemberExpression me, SqlProviderOptions options, bool useParams, Dictionary<string, object> cmdParams)
        {
            var mi = MemberCache.Create(me.Member);
            if (me.Expression != null && me.Expression.NodeType == ExpressionType.Parameter)
            {
                return options.NamePrefix + mi.ColumnName + options.NameSuffix;
            }

            var value = GetValue(me);
            return useParams ? options.ParamPrefix + mi.ColumnName : options.ToSqlLiteral(value); //FormatValue(value, useParams, cmdParams);
        }

        private static string VisitConstant(ConstantExpression ce, SqlProviderOptions options, bool useParams, Dictionary<string, object> cmdParams)
        {
            return options.ToSqlLiteral(ce.Value); //FormatValue(ce.Value);
        }

        private static object GetValue(MemberExpression me)
        {
            var lambda = Expression.Lambda<Func<object>>(
                Expression.Convert(me, typeof(object))
            );
            return lambda.Compile().Invoke();
        }

        //private static string FormatValue(object value, SqlProviderOptions options, bool useParams, Dictionary<string, object> cmdParams)
        //{
        //    if (Obj.NullValues.Contains(value))
        //        return options.NullValue;

        //    if (value is string s)
        //        return options.StringPrefix + s.Replace("'", "''") + options.StringSuffix;

        //    if (value is DateTime dt)
        //        return options.StringPrefix + dt.ToString(options.DateFormat) + options.StringSuffix;

        //    if (value is bool b)
        //        return b ? options.TrueValue : options.FalseValue;

        //    if (value is Enum)
        //        return Convert.ToInt32(value).ToString();

        //    return value.ToString();
        //}

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

    #endregion PRIVATE
}