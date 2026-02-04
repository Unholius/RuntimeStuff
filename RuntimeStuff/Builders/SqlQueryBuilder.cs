// <copyright file="SqlQueryBuilder.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

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
    /// Статический класс для генерации SQL-запросов (SELECT, INSERT, UPDATE, DELETE, JOIN, WHERE и т.д.).
    /// Поддерживает различные провайдеры SQL через <see cref="SqlProviderOptions"/>.
    /// </summary>
    public static class SqlQueryBuilder
    {
        /// <summary>
        /// Тип соединения для SQL JOIN.
        /// </summary>
        public enum JoinType
        {
            /// <summary>INNER JOIN</summary>
            Inner,

            /// <summary>LEFT JOIN</summary>
            Left,

            /// <summary>RIGHT JOIN</summary>
            Right,

            /// <summary>FULL JOIN</summary>
            Full,
        }

        /// <summary>
        /// Добавляет в SQL-запрос ограничения на количество строк и смещение (LIMIT/OFFSET).
        /// </summary>
        /// <param name="fetchRows">Количество строк для выборки.</param>
        /// <param name="offsetRows">Количество строк для пропуска (смещение).</param>
        /// <param name="query">Исходный SQL-запрос.</param>
        /// <param name="options">Параметры SQL-провайдера.</param>
        /// <param name="entityType">Тип сущности для генерации ORDER BY (если его нет).</param>
        /// <returns>SQL-запрос с добавленным LIMIT/OFFSET.</returns>
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
                    mi.PrimaryKeys.Length > 0 ? mi.PrimaryKeys.Select(x => options.Map?.ResolveColumnName(x, options.NamePrefix, options.NameSuffix) ?? (options.NamePrefix + x.ColumnName + options.NameSuffix)) : mi.ColumnProperties.Select(x => options.Map?.ResolveColumnName(x, options.NamePrefix, options.NameSuffix) ?? (options.NamePrefix + x.ColumnName + options.NameSuffix))));
                clause.Append(" ");
            }

            clause.Append(string.Format(
                offsetRowsFetchNextRowsOnly,
                offsetRows,
                fetchRows));

            return clause.ToString().Trim();
        }

        /// <summary>
        /// Генерирует SELECT-запрос с агрегатными функциями (SUM, COUNT, AVG и т.д.).
        /// </summary>
        /// <typeparam name="TFrom">Тип сущности для выборки.</typeparam>
        /// <param name="options">Параметры SQL-провайдера.</param>
        /// <param name="columnSelectors">Список колонок и агрегатных функций.</param>
        /// <returns>SQL-запрос SELECT с агрегатными функциями.</returns>
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
        /// Генерирует SQL-запрос DELETE для указанной сущности.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="options">Параметры SQL-провайдера.</param>
        /// <returns>SQL-запрос DELETE.</returns>
        public static string GetDeleteQuery<T>(SqlProviderOptions options)
            where T : class
        {
            var mi = MemberCache.Create(typeof(T));
            var query = new StringBuilder("DELETE FROM ").Append(options.Map?.ResolveTableName(mi, options.NamePrefix, options.NameSuffix) ?? mi.GetFullTableName(options.ParamPrefix, options.NameSuffix));
            return query.ToString();
        }

        /// <summary>
        /// Генерирует SQL-запрос INSERT для указанной сущности и колонок.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="options">Параметры SQL-провайдера.</param>
        /// <param name="insertColumns">Колонки для вставки. Если не указаны, вставляются все публичные свойства с сеттером.</param>
        /// <returns>SQL-запрос INSERT.</returns>
        public static string GetInsertQuery<T>(SqlProviderOptions options, params Expression<Func<T, object>>[] insertColumns)
            where T : class
        {
            var query = new StringBuilder("INSERT INTO ");
            var mi = MemberCache.Create(typeof(T));
            query
                .Append(options.Map?.ResolveTableName(mi, options.NamePrefix, options.NameSuffix) ?? mi.GetFullTableName(options.NamePrefix, options.NameSuffix))
                .Append(" (");

            var insertCols = insertColumns?.Select(ExpressionHelper.GetPropertyName).ToArray() ?? Array.Empty<string>();
            if (insertCols.Length == 0)
            {
                insertCols = mi.ColumnProperties.Where(x => x.IsSetterPublic).Select(x => x.Name).ToArray();
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
        /// Генерирует SQL-клаузу JOIN между двумя сущностями.
        /// </summary>
        /// <param name="from">Тип основной сущности.</param>
        /// <param name="joinOn">Тип сущности для соединения.</param>
        /// <param name="options">Параметры SQL-провайдера.</param>
        /// <param name="joinType">Тип соединения (INNER, LEFT, RIGHT, FULL).</param>
        /// <returns>SQL-клауза JOIN.</returns>
        /// <exception cref="ArgumentNullException">Если один из типов равен null.</exception>
        /// <exception cref="InvalidOperationException">Если не удалось определить колонки для соединения.</exception>
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

            string parentColumn;
            string childColumn;

            var fkInChildren = childrenCache.GetForeignKey(from);
            if (fkInChildren != null)
            {
                parentColumn = parentCache.PrimaryKeys.FirstOrDefault()?.ColumnName;
                childColumn = fkInChildren.ColumnName;
            }
            else
            {
                var fkInParent = parentCache.GetForeignKey(joinOn);
                if (fkInParent != null)
                {
                    parentColumn = fkInParent.ColumnName;
                    childColumn = childrenCache.PrimaryKeys.FirstOrDefault()?.ColumnName;
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
        /// Генерирует SQL-клауза ORDER BY для указанной сущности.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="options">Параметры SQL-провайдера (например, префикс/суффикс имен колонок).</param>
        /// <param name="orderBy">
        /// Кортежи, где первый элемент — выражение для выбора свойства сущности,
        /// второй — направление сортировки: <c>true</c> для ASC, <c>false</c> для DESC.
        /// </param>
        /// <returns>Строка SQL-клаузы ORDER BY, либо пустая строка, если параметр <paramref name="orderBy"/> равен <c>null</c> или пуст.</returns>
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
        /// Генерирует SQL-клауза ORDER BY для указанных колонок с их направлением сортировки.
        /// </summary>
        /// <param name="options">Параметры SQL-провайдера, включая префикс/суффикс имен колонок и карту имен.</param>
        /// <param name="orderBy">
        /// Массив кортежей, где первый элемент — объект <see cref="MemberCache"/> для колонки,
        /// второй элемент — направление сортировки: <c>true</c> для ASC, <c>false</c> для DESC.
        /// </param>
        /// <returns>
        /// Строка SQL-клаузы ORDER BY. Если массив <paramref name="orderBy"/> пуст или равен <c>null</c>, возвращается пустая строка.
        /// </returns>
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
        /// Генерирует SQL-запрос SELECT для указанной сущности с выборкой конкретных колонок.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="options">Параметры SQL-провайдера, включая префикс/суффикс имен колонок и карту имен таблиц.</param>
        /// <param name="selectColumns">
        /// Массив выражений для выбора свойств сущности, которые будут включены в SELECT.
        /// Если массив пустой или <c>null</c>, выбираются все колонки и первичные ключи.
        /// </param>
        /// <returns>Строка SQL-запроса SELECT.</returns>
        public static string GetSelectQuery<T>(SqlProviderOptions options, params Expression<Func<T, object>>[] selectColumns)
        {
            var mi = MemberCache.Create(typeof(T));
            var members = selectColumns?.Select(ExpressionHelper.GetMemberInfo).Select(x => x.GetMemberCache()).ToArray() ?? Array.Empty<MemberCache>();
            if (members.Length == 0)
            {
                members = mi.ColumnProperties.Concat(mi.PrimaryKeys).ToArray();
            }

            if (members.Length == 0)
            {
                return $"SELECT * FROM {options.NamePrefix}{options.Map?.ResolveTableName(mi, options.NamePrefix, options.NameSuffix) ?? mi.TableName}{options.NameSuffix}";
            }

            return GetSelectQuery(options, mi, members);
        }

        /// <summary>
        /// Генерирует SQL-запрос SELECT для указанной сущности с выборкой конкретных колонок.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <typeparam name="TProp">Тип свойств для выборки.</typeparam>
        /// <param name="options">Параметры SQL-провайдера, включая префикс/суффикс имен колонок и карту имен таблиц.</param>
        /// <param name="selectColumns">
        /// Массив выражений для выбора свойств сущности, которые будут включены в SELECT.
        /// </param>
        /// <returns>Строка SQL-запроса SELECT.</returns>
        public static string GetSelectQuery<T, TProp>(SqlProviderOptions options, params Expression<Func<T, TProp>>[] selectColumns)
            => GetSelectQuery(options, MemberCache.Create(typeof(T)), selectColumns.Select(x => x.GetMemberCache()).ToArray());

        /// <summary>
        /// Генерирует SQL-запрос SELECT для указанного типа сущности с выборкой конкретных колонок.
        /// </summary>
        /// <param name="options">Параметры SQL-провайдера, включая префикс/суффикс имен колонок и карту имен таблиц.</param>
        /// <param name="typeInfo">Метаданные сущности в виде <see cref="MemberCache"/>.</param>
        /// <param name="selectColumns">
        /// Массив колонок для выборки. Если массив пустой, выбираются все колонки сущности.
        /// </param>
        /// <returns>Строка SQL-запроса SELECT.</returns>
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
        /// Генерирует SQL-запрос UPDATE для указанной сущности с обновлением конкретных колонок.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="options">Параметры SQL-провайдера, включая префикс/суффикс имен колонок и карту имен таблиц.</param>
        /// <param name="updateColumns">
        /// Массив выражений для выбора свойств сущности, которые будут обновлены.
        /// Если массив пустой, обновляются все публичные свойства с доступным сеттером, кроме первичных ключей.
        /// </param>
        /// <returns>Строка SQL-запроса UPDATE с указанием колонок и параметров для их значений.</returns>
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
                    .Where(x => !x.IsPrimaryKey && x.IsSetterPublic)
                    .Select(x => x.Name));
            }

            if (props.Count == 0)
            {
                props.AddRange(mi.PublicBasicProperties
                    .Where(x => x.Name.ToLower() != "id" && x.IsSetterPublic)
                    .Select(x => x.Name));
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
        /// Генерирует SQL-клауза WHERE на основе выражения для указанной сущности.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="whereExpression">Лямбда-выражение для фильтрации строк (например, x => x.Id == 5).</param>
        /// <param name="options">Параметры SQL-провайдера, включая префикс/суффикс имен колонок и карту имен таблиц.</param>
        /// <param name="useParams">Если <c>true</c>, значения будут подставлены как параметры, иначе как литералы SQL.</param>
        /// <param name="cmdParams">
        /// Словарь параметров, которые нужно будет передать вместе с SQL-запросом.
        /// Ключ — имя параметра, значение — его значение.
        /// </param>
        /// <returns>Строка SQL-клаузы WHERE.</returns>
        public static string GetWhereClause<T>(Expression<Func<T, bool>> whereExpression, SqlProviderOptions options, bool useParams, out IReadOnlyDictionary<string, object> cmdParams)
        {
            var dic = new Dictionary<string, object>();
            var whereClause = whereExpression == null ? string.Empty : ("WHERE " + Visit(whereExpression.Body, options, useParams, dic)).Trim();
            cmdParams = dic;
            return whereClause;
        }

        /// <summary>
        /// Генерирует SQL-клауза WHERE для указанной сущности на основе её первичных ключей.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="options">Параметры SQL-провайдера, включая префикс/суффикс имен колонок и карту имен таблиц.</param>
        /// <param name="cmdParams">
        /// Словарь параметров, которые нужно будет передать вместе с SQL-запросом.
        /// Ключ — имя параметра, значение — его значение.
        /// </param>
        /// <returns>Строка SQL-клаузы WHERE для первичных ключей или публичных свойств, если первичные ключи отсутствуют.</returns>
        public static string GetWhereClause<T>(SqlProviderOptions options, out Dictionary<string, object> cmdParams)
        {
            var mi = MemberCache.Create(typeof(T));
            var keys = mi.PrimaryKeys.ToArray();
            if (keys.Length == 0)
            {
                keys = mi.PublicBasicProperties.ToArray();
            }

            return GetWhereClause(keys, options, out cmdParams);
        }

        /// <summary>
        /// Генерирует SQL-клауза WHERE для указанного набора колонок.
        /// </summary>
        /// <param name="whereProperties">Массив колонок (MemberCache), по которым строится фильтр.</param>
        /// <param name="options">Параметры SQL-провайдера, включая префикс/суффикс имен колонок и карту имен таблиц.</param>
        /// <param name="cmdParams">
        /// Словарь параметров, которые нужно будет передать вместе с SQL-запросом.
        /// Ключ — имя параметра, значение — его значение (инициализируется <c>null</c>).
        /// </param>
        /// <returns>Строка SQL-клаузы WHERE для указанных колонок.</returns>
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

        private static object GetValue(MemberExpression me)
        {
            var lambda = Expression.Lambda<Func<object>>(
                Expression.Convert(me, typeof(object)));
            return lambda.Compile().Invoke();
        }

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

        private static string VisitConstant(ConstantExpression ce, SqlProviderOptions options) => options.ToSqlLiteral(ce.Value);

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
    }
}