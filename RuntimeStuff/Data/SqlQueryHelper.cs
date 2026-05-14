// <copyright file="SqlQueryHelper.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace System.Data
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Data;
    using System.Helpers;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Text;

    /// <summary>
    /// Статический класс для генерации SQL-запросов (SELECT, INSERT, UPDATE, DELETE, JOIN, WHERE и т.д.).
    /// Поддерживает различные провайдеры SQL через <see cref="SqlOptions"/>.
    /// </summary>
    public static class SqlQueryHelper
    {
        /// <summary>
        /// Тип соединения для SQL JOIN.
        /// </summary>
        public enum JoinType
        {
            /// <summary>INNER JOIN.</summary>
            Inner,

            /// <summary>LEFT JOIN.</summary>
            Left,

            /// <summary>RIGHT JOIN.</summary>
            Right,

            /// <summary>FULL JOIN.</summary>
            Full,
        }

        /// <summary>
        /// Добавляет в SQL-запрос ограничения на количество строк и смещение (LIMIT/OFFSET).
        /// </summary>
        /// <param name="options">Параметры SQL-провайдера.</param>
        /// <param name="fetchRows">Количество строк для выборки.</param>
        /// <param name="offsetRows">Количество строк для пропуска (смещение).</param>
        /// <param name="query">Исходный SQL-запрос.</param>
        /// <param name="entityType">Тип сущности для генерации ORDER BY (если его нет).</param>
        /// <returns>SQL-запрос с добавленным LIMIT/OFFSET.</returns>
        public static string AddLimitOffsetClauseToQuery(SqlOptions options, int fetchRows, int offsetRows, string query, Type entityType = null)
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
                var mi = MemberCache.Get(entityType);

                clause.Append(" ORDER BY ");
                _ = clause.Append(string.Join(
                    ", ",
                    mi.PrimaryKeys.Length > 0 ? mi.PrimaryKeys.Select(x => options.GetColumnName(x)) : mi.GetColumns().Select(x => options.GetColumnName(x))));
                clause.Append(' ');
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
        public static string GetAggSelectClause<TFrom>(SqlOptions options, params (Expression<Func<TFrom, object>> Column, string AggFunction)[] columnSelectors)
            where TFrom : class
        {
            var query = "SELECT " + (columnSelectors.Length == 0 || columnSelectors.Any(x => x.Column == null)
                          ? "COUNT(*)"
                          : string.Join(
                                ", ",
                                columnSelectors.Select(c =>
                              {
                                  var col = c.Column?.GetMemberCache()?.ColumnName;
                                  var colName = $"{options.GetColumnName(c.Column?.GetPropertyInfo()) ?? col ?? "*"}";
                                  return $"{c.AggFunction}({colName}) {(string.IsNullOrWhiteSpace(col) ? string.Empty : col + c.AggFunction)}";
                              }))
                      + $" FROM {options.GetTableName(typeof(TFrom))}");

            return query;
        }

        /// <summary>
        /// Генерирует SQL-запрос DELETE для указанной сущности.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="options">Параметры SQL-провайдера.</param>
        /// <returns>SQL-запрос DELETE.</returns>
        public static string GetDeleteQuery<T>(SqlOptions options)
            where T : class
        {
            var mi = MemberCache.Get(typeof(T));
            var query = new StringBuilder("DELETE FROM ").Append(options.Map?.ResolveTableName(mi, options.NamePrefix, options.NameSuffix) ?? mi.GetTableName(options.NamePrefix, options.NameSuffix));
            return query.ToString();
        }

        /// <summary>
        /// Генерирует SQL-запрос INSERT для указанной сущности и колонок.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="options">Параметры SQL-провайдера.</param>
        /// <param name="insertColumns">Колонки для вставки. Если не указаны, вставляются все публичные свойства с сеттером.</param>
        /// <returns>SQL-запрос INSERT.</returns>
        public static string GetInsertQuery<T>(SqlOptions options, params Expression<Func<T, object>>[] insertColumns)
            where T : class
        {
            var query = new StringBuilder("INSERT INTO ");
            var mi = MemberCache.Get(typeof(T));
            query
                .Append(options.Map?.ResolveTableName(mi, options.NamePrefix, options.NameSuffix) ?? mi.GetTableName(options.NamePrefix, options.NameSuffix))
                .Append(" (");

            var insertCols = insertColumns?.Select(ExpressionHelper.GetPropertyName).ToArray() ?? Array.Empty<string>();
            if (insertCols.Length == 0)
            {
                insertCols = [.. mi.GetColumns(false, true).Where(x => x.IsSetterPublic).Select(x => x.Name)];
            }

            if (insertCols.Length == 0)
            {
                insertCols = [.. mi.PublicBasicProperties.Select(x => x.Name)];
            }

            if (insertCols.Length == 0)
            {
                throw new NotSupportedException("Не указаны колонки для генерации INSERT запроса!");
            }

            for (var i = 0; i < insertCols.Length; i++)
            {
                var col = insertCols[i];

                query
                    .Append(options.GetColumnName(mi[col]));

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

            query.Append(')');

            return query.ToString();
        }

        /// <summary>
        /// Формирует SQL-выражение JOIN для соединения таблиц на основе переданных выражений свойств.
        /// </summary>
        /// <typeparam name="TFrom">Тип сущности, из которой берётся таблица для JOIN.</typeparam>
        /// <typeparam name="TOn">Тип сущности, содержащей свойство для условия соединения.</typeparam>
        /// <param name="options">
        /// Параметры SQL-провайдера, содержащие префиксы и суффиксы имён таблиц и колонок.
        /// </param>
        /// <param name="fromPropertySelector">
        /// Выражение, указывающее на свойство в таблице, которая присоединяется (JOIN).
        /// </param>
        /// <param name="onPropertySelector">
        /// Выражение, указывающее на свойство, по которому выполняется условие соединения (ON).
        /// </param>
        /// <param name="joinType">
        /// Тип соединения (например, INNER, LEFT, RIGHT). По умолчанию используется INNER JOIN.
        /// </param>
        /// <returns>
        /// Строка SQL, представляющая собой выражение JOIN с условием ON.
        /// </returns>
        /// <remarks>
        /// Метод извлекает имена таблиц и колонок из выражений свойств,
        /// применяя настройки префиксов и суффиксов, указанных в <paramref name="options"/>.
        /// </remarks>
        public static string GetJoinClause<TFrom, TOn>(SqlOptions options, Expression<Func<TFrom, object>> fromPropertySelector, Expression<Func<TOn, object>> onPropertySelector, JoinType joinType = JoinType.Inner)
        {
            var np = options.NamePrefix;
            var ns = options.NameSuffix;
            var fromPropInfo = fromPropertySelector.GetMemberCache();
            var fromColumnName = fromPropInfo.GetColumnName(np, ns);

            var onPropInfo = onPropertySelector.GetMemberCache();
            var onColumnName = onPropInfo.GetColumnName(np, ns);

            var joinTable = onPropInfo.DeclaringType.GetMemberCache().GetTableName(np, ns);

            var joinClause = $"{joinType.ToString().ToUpper()} JOIN {joinTable} ON {onColumnName} = {fromColumnName}";
            return joinClause;
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
        public static string GetOrderBy<T>(SqlOptions options, params (Expression<Func<T, object>>, bool)[] orderBy)
        {
            if (orderBy == null)
            {
                return string.Empty;
            }

            var props = orderBy.Select(x => (ExpressionHelper.GetMemberInfo(x.Item1).GetMemberCache<T>(), x.Item2)).ToArray();
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
        public static string GetOrderBy(SqlOptions options, params (MemberCache, bool)[] orderBy)
        {
            if (orderBy == null || orderBy.Length == 0)
            {
                return string.Empty;
            }

            var query = new StringBuilder("ORDER BY ");

            foreach (var mi in orderBy)
            {
                query
                    .Append(options.GetColumnName(mi.Item1))
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
        /// <param name="useFullNames">Использовать полные имена: к колонкам добавляется имя таблицы.</param>
        /// <param name="selectColumns">
        /// Массив выражений для выбора свойств сущности, которые будут включены в SELECT.
        /// Если массив пустой или <c>null</c>, выбираются все колонки и первичные ключи.
        /// </param>
        /// <returns>Строка SQL-запроса SELECT.</returns>
        public static string GetSelectQuery<T>(SqlOptions options, bool useFullNames, params Expression<Func<T, object>>[] selectColumns)
        {
            var mi = MemberCache.Get(typeof(T));
            var propertyNames = selectColumns?.Select(x => x.Name).ToArray() ?? Array.Empty<string>();
            var members = propertyNames.Length > 0
                ? [.. mi.PublicBasicProperties.Where(mc => propertyNames.Contains(mc.Name))]
                : mi.GetColumns();

            if (members.Length == 0)
            {
                return $"SELECT * FROM {options.GetTableName(mi)}";
            }

            return GetSelectQuery(options, useFullNames, mi, members);
        }

        /// <summary>
        /// Генерирует SQL-запрос SELECT для указанной сущности с выборкой конкретных колонок.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <typeparam name="TProp">Тип свойств для выборки.</typeparam>
        /// <param name="options">Параметры SQL-провайдера, включая префикс/суффикс имен колонок и карту имен таблиц.</param>
        /// <param name="useFullNames">Использовать полные имена: к колонкам добавляется имя таблицы.</param>
        /// <param name="selectColumns">
        /// Массив выражений для выбора свойств сущности, которые будут включены в SELECT.
        /// </param>
        /// <returns>Строка SQL-запроса SELECT.</returns>
        public static string GetSelectQuery<T, TProp>(SqlOptions options, bool useFullNames, params Expression<Func<T, TProp>>[] selectColumns)
            => GetSelectQuery(options, useFullNames, MemberCache.Get(typeof(T)), [.. selectColumns.Select(x => x.GetMemberCache())]);

        /// <summary>
        /// Генерирует SQL-запрос SELECT для указанного типа сущности с выборкой конкретных колонок.
        /// </summary>
        /// <param name="options">Параметры SQL-провайдера, включая префикс/суффикс имен колонок и карту имен таблиц.</param>
        /// <param name="useFullNames">Использовать полные имена: к колонкам добавляется имя таблицы.</param>
        /// <param name="type">Метаданные сущности в виде <see cref="MemberCache"/>.</param>
        /// <param name="selectColumns">
        /// Массив колонок для выборки. Если массив пустой, выбираются все колонки сущности.
        /// </param>
        /// <returns>Строка SQL-запроса SELECT.</returns>
        public static string GetSelectQuery(SqlOptions options, bool useFullNames, Type type, params PropertyInfo[] selectColumns)
        {
            return GetSelectQuery(options, useFullNames, type.GetMemberCache(), selectColumns?.Select(x => x.GetMemberCache(type))?.ToArray());
        }

        /// <summary>
        /// Генерирует SQL-запрос SELECT для указанного типа сущности с выборкой конкретных колонок.
        /// </summary>
        /// <param name="options">Параметры SQL-провайдера, включая префикс/суффикс имен колонок и карту имен таблиц.</param>
        /// <param name="useFullNames">Использовать полные имена: к колонкам добавляется имя таблицы.</param>
        /// <param name="typeInfo">Метаданные сущности для подстановки имени таблицы после FROM.</param>
        /// <param name="selectColumns">
        /// Массив колонок для выборки. Если массив пустой, выбираются все колонки сущности <see cref="MemberCache.GetColumns"/>.
        /// </param>
        /// <returns>Строка SQL-запроса SELECT.</returns>
        public static string GetSelectQuery(SqlOptions options, bool useFullNames, MemberCache typeInfo, params MemberCache[] selectColumns)
        {
            if (typeInfo == null)
            {
                typeInfo = selectColumns?.FirstOrDefault()?.DeclaringType.GetMemberCache();
            }

            if (selectColumns == null || selectColumns.Length == 0)
            {
                selectColumns = typeInfo.GetColumns();
            }

            var query = new StringBuilder("SELECT ");

            foreach (var pi in selectColumns)
            {
                query
                    .Append(options.GetColumnName(pi, null, useFullNames))
                    .Append(", ");
            }

            if (query[query.Length - 2] == ',')
            {
                query.Remove(query.Length - 2, 2);
            }

            query.Append(" FROM ");
            query.Append(options.GetTableName(typeInfo));

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
        public static string GetUpdateQuery<T>(SqlOptions options, params Expression<Func<T, object>>[] updateColumns)
            where T : class
        {
            var mi = MemberCache.Get(typeof(T));
            var query = new StringBuilder("UPDATE ")
                .Append(options.Map?.ResolveTableName(mi, options.NamePrefix, options.NameSuffix) ?? mi.GetTableName(options.NamePrefix, options.NameSuffix))
                .Append(" SET ");

            var props = updateColumns?.Select(ExpressionHelper.GetPropertyName).ToList()
                        ?? [];

            if (props.Count == 0)
            {
                props.AddRange(mi.GetColumns(false)
                    .Where(x => x.IsSetterPublic)
                    .Select(x => x.Name));
            }

            if (props.Count == 0)
            {
                props.AddRange(mi.PublicBasicProperties
                    .Where(x => !x.Name.Equals("id", StringComparison.CurrentCultureIgnoreCase) && x.IsSetterPublic)
                    .Select(x => x.Name));
            }
            else
            {
                foreach (var p in props)
                {
                    var pi = mi[p];
                    query
                        .Append(options.GetColumnName(pi))
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
        /// <param name="options">Параметры SQL-провайдера, включая префикс/суффикс имен колонок и карту имен таблиц.</param>
        /// <param name="whereExpression">Лямбда-выражение для фильтрации строк (например, x => x.Id == 5).</param>
        /// <param name="useParams">Если <c>true</c>, значения будут подставлены как параметры, иначе как литералы SQL.</param>
        /// <param name="cmdParams">
        /// Словарь параметров, которые нужно будет передать вместе с SQL-запросом.
        /// Ключ — имя параметра, значение — его значение.
        /// </param>
        /// <returns>Строка SQL-клаузы WHERE.</returns>
        public static string GetWhereClause<T>(SqlOptions options, Expression<Func<T, bool>> whereExpression, bool useParams, out Dictionary<string, object> cmdParams)
        {
            var dic = new Dictionary<string, object>();
            var whereClause = whereExpression == null ? string.Empty : ("WHERE " + Visit(whereExpression.Body, options, useParams, dic)).Trim();
            cmdParams = dic;
            return whereClause;
        }

        /// <summary>
        /// Генерирует SQL-клауза WHERE на основе выражения для указанной сущности.
        /// </summary>
        /// <param name="options">Параметры SQL-провайдера, включая префикс/суффикс имен колонок и карту имен таблиц.</param>
        /// <param name="whereClause">Объект, используемый для построения WHERE. Пример: new { Id = 1 }.</param>
        /// <param name="useParams">Генерировать запрос с параметрами или со значениями.</param>
        /// <param name="cmdParams">Значения для передачи в запрос UPDATE.</param>
        /// <returns>SQL-клауза WHERE на основе выражения для указанной сущности.</returns>
        public static string GetWhereClause(SqlOptions options, object whereClause, bool useParams, out IReadOnlyDictionary<string, object> cmdParams)
        {
            var dic = new Dictionary<string, object>();
            cmdParams = dic;
            if (whereClause == null)
            {
                return string.Empty;
            }

            var mc = whereClause.GetType().GetMemberCache();

            var i = 0;
            foreach (var p in mc.PublicBasicProperties)
            {
                dic[p.GetColumnName()] = p.GetValue(whereClause);
                i++;
            }

            var where = string.Empty;
            where += "WHERE ";
            where += string.Join(" AND ", mc.PublicBasicProperties.Select((x, i) => $"{x.GetColumnName(options.NamePrefix, options.NameSuffix, false)} = {(useParams ? $"{options.ParamPrefix}{x.GetColumnName()}" : options.ValueFormatter.Format(x.GetValue(whereClause)))}"));

            return where;
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
        public static string GetWhereClause<T>(SqlOptions options, out Dictionary<string, object> cmdParams)
        {
            var mi = MemberCache.Get(typeof(T));
            var keys = mi.PrimaryKeys.ToArray();
            if (keys.Length == 0)
            {
                keys = [.. mi.PublicBasicProperties];
            }

            return GetWhereClause(options, true, keys, out cmdParams);
        }

        /// <summary>
        /// Генерирует SQL-клауза WHERE для указанного набора колонок.
        /// </summary>
        /// <param name="options">Параметры SQL-провайдера, включая префикс/суффикс имен колонок и карту имен таблиц.</param>
        /// <param name="and">Конкантенация условий через AND иначе через OR.</param>
        /// <param name="whereProperties">Массив колонок (MemberCache), по которым строится фильтр.</param>
        /// <param name="cmdParams">
        /// Словарь параметров, которые нужно будет передать вместе с SQL-запросом.
        /// Ключ — имя параметра, значение — его значение (инициализируется <c>null</c>).
        /// </param>
        /// <returns>Строка SQL-клаузы WHERE для указанных колонок.</returns>
        public static string GetWhereClause(SqlOptions options, bool and, MemberCache[] whereProperties, out Dictionary<string, object> cmdParams)
        {
            cmdParams = [];
            var whereClause = new StringBuilder("WHERE ");

            for (var i = 0; i < whereProperties.Length; i++)
            {
                var key = whereProperties[i];

                whereClause
                    .Append(options.GetColumnName(key))
                    .Append(" = ")
                    .Append(options.ParamPrefix)
                    .Append(key.Name);

                if (i < whereProperties.Length - 1)
                {
                    whereClause.Append(and ? " AND " : " OR ");
                }

                cmdParams[key.ColumnName] = null;
            }

            return whereClause.ToString();
        }

        private static string GetSqlOperator(ExpressionType type)
        {
            return type switch
            {
                ExpressionType.Equal => "=",
                ExpressionType.NotEqual => "<>",
                ExpressionType.GreaterThan => ">",
                ExpressionType.GreaterThanOrEqual => ">=",
                ExpressionType.LessThan => "<",
                ExpressionType.LessThanOrEqual => "<=",
                ExpressionType.AndAlso => "AND",
                ExpressionType.OrElse => "OR",
                _ => throw new NotSupportedException($"Operator '{type}' not supported."),
            };
        }

        private static string Visit(Expression exp, SqlOptions options, bool useParams, Dictionary<string, object> cmdParams)
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

                case MethodCallExpression mce:
                    if (mce.Arguments.Count >= 2)
                    {
                        return VisitMethodCall(mce, options, useParams, cmdParams);
                    }

                    break;
            }

            throw new NotSupportedException($"Expression '{exp.NodeType}' is not supported.");
        }

        private static string VisitMethodCall(MethodCallExpression mce, SqlOptions options, bool useParams, Dictionary<string, object> cmdParams)
        {
            var methodName = mce.Method.Name.ToLower();
            MemberExpression propertyExpression = null;
            Expression valueExpression = null;

            switch (methodName)
            {
                case "contains":
                    valueExpression = mce.Arguments[0];
                    propertyExpression = mce.Arguments[1] as MemberExpression;
                    break;

                case "op_implicit":
                case "in":
                    if (mce.Arguments[0] is not MemberExpression)
                    {
                        break;
                    }

                    propertyExpression = mce.Arguments[0] as MemberExpression;
                    valueExpression = mce.Arguments.Skip(1).FirstOrDefault(x => x is MemberExpression || x is ConstantExpression || x is NewArrayExpression);
                    break;
            }

            if (propertyExpression == null || valueExpression == null)
            {
                throw new NotImplementedException();
            }

            var member = VisitMember(propertyExpression, options, useParams, cmdParams);
            var vals = (ExpressionHelper.GetValue(valueExpression) as IEnumerable)?.Cast<object>().ToArray() ?? Array.Empty<object>();
            if (useParams)
            {
                var sb = new StringBuilder($"{member} IN (");
                foreach (var val in vals)
                {
                    var paramName = mce.Method.Name + "_" + (cmdParams.Count + 1);
                    cmdParams[paramName] = val;
                    sb.Append($"{options.ParamPrefix}{paramName}, ");
                }

                sb.Remove(sb.Length - 2, 2);
                sb.Append(')');
                return sb.ToString();
            }
            else
            {
                return $"{member} IN ({string.Join(", ", vals.Select(x => options.ValueFormatter.Format(x)))})";
            }
        }

        private static string VisitBinary(BinaryExpression be, SqlOptions options, bool useParams, Dictionary<string, object> cmdParams)
        {
            var left = Visit(be.Left, options, useParams, cmdParams);
            var right = Visit(be.Right, options, useParams, cmdParams);
            var op = GetSqlOperator(be.NodeType);

            if (be.Left is MemberExpression me && useParams)
            {
                var paramName = me.Member.GetColumnName() + "_" + (cmdParams.Count + 1);
                if (be.Right.NodeType == ExpressionType.Constant || be.Right.NodeType == ExpressionType.Convert)
                {
                    right = options.ParamPrefix + paramName;
                    cmdParams[paramName] = ExpressionHelper.GetValue(be.Right);
                }
                else
                {
                    if (be.Right is MemberExpression rme)
                    {
                        if (rme.Member.GetMemberCache(rme.Type)?.IsProperty == true)
                        {
                            right = options.NamePrefix + rme.Member.GetColumnName() + options.NameSuffix;
                        }
                        else
                        {
                            right = options.ParamPrefix + paramName;
                            cmdParams[paramName] = ExpressionHelper.GetValue(be.Right);
                        }
                    }
                }
            }

            return $"({left} {op} {right})";
        }

        private static string VisitConstant(ConstantExpression ce, SqlOptions options) => options.ValueFormatter.Format(ce.Value);

        private static string VisitMember(MemberExpression me, SqlOptions options, bool useParams, Dictionary<string, object> cmdParams)
        {
            var mi = MemberCache.Get(me.Member.DeclaringType, me.Member);
            if (me.Expression != null && me.Expression.NodeType == ExpressionType.Parameter)
            {
                return options.GetColumnName(mi);
            }

            var value = ExpressionHelper.GetValue(me);
            var paramName = (mi.ColumnName ?? mi.Name) + "_" + (cmdParams.Count + 1);
            return useParams ? options.ParamPrefix + paramName : options.ValueFormatter.Format(value);
        }

        private static string VisitUnary(UnaryExpression ue, SqlOptions options, bool useParams, Dictionary<string, object> cmdParams)
        {
            return ue.NodeType switch
            {
                ExpressionType.Not => $"(NOT {Visit(ue.Operand, options, useParams, cmdParams)})",
                ExpressionType.Convert => Visit(ue.Operand, options, useParams, cmdParams),
                _ => throw new NotSupportedException($"Unary '{ue.NodeType}' not supported."),
            };
        }
    }
}