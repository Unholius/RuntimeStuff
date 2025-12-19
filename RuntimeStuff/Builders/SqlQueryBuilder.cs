using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using RuntimeStuff.Helpers;

namespace RuntimeStuff.Builders
{
    /// <summary>
    /// Предоставляет методы для генерации SQL-подобных WHERE-условий
    /// на основе лямбда-выражений типа <see cref="Expression{TDelegate}"/>.
    /// </summary>
    /// <remarks>
    /// Класс выполняет разбор дерева выражений, поддерживая основные бинарные
    /// и логические операции, а также значения из замыканий.
    /// Подходит для генерации простых SQL-условий, однако не является
    /// полноценным механизмом LINQ-to-SQL.
    /// </remarks>
    public static class SqlQueryBuilder
    {
        private static string _namePrefix = "[";
        private static string _nameSuffix = "]";

        /// <summary>
        /// Генерирует SQL-подобное выражение WHERE на основе переданного лямбда-выражения.
        /// </summary>
        /// <typeparam name="T">
        /// Тип объекта, для которого создаётся условие фильтрации.
        /// </typeparam>
        /// <param name="whereExpression">
        /// Лямбда-выражение вида <c>Expression&lt;Func&lt;T, bool&gt;&gt;</c>, описывающее условие фильтрации.
        /// </param>
        /// <param name="namePrefix">
        /// Префикс, добавляемый к имени поля/свойства (например <c>"["</c> или <c>"`"</c>).
        /// По умолчанию используется <c>"["</c>.
        /// </param>
        /// <param name="nameSuffix">
        /// Суффикс, добавляемый к имени поля/свойства (например <c>"]"</c> или <c>"`"</c>).
        /// По умолчанию используется <c>"]"</c>.
        /// </param>
        /// <returns>
        /// Строка SQL-подобного WHERE-условия или <c>null</c>, если входное выражение равно <c>null</c>.
        /// </returns>
        /// <remarks>
        /// Метод выполняет разбор дерева выражения, преобразуя бинарные операции, сравнения,
        /// логические операторы и значения из замыканий в строковое SQL-условие.
        /// Подходит для генерации простых WHERE-клаузаов, но не является полноценным LINQ-to-SQL.
        /// </remarks>
        public static string GetWhereClause<T>(Expression<Func<T, bool>> whereExpression, string namePrefix = "[", string nameSuffix = "]")
        {
            if (whereExpression == null)
                return null;

            _namePrefix = namePrefix;
            _nameSuffix = nameSuffix;

            return ("WHERE " + Visit(whereExpression.Body)).Trim();
        }

        /// <summary>
        /// Создает SQL-условие WHERE на основе первичных ключей типа <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Тип сущности, по которой формируется условие WHERE.</typeparam>
        /// <param name="namePrefix">Префикс, добавляемый к имени столбца (например, "[").</param>
        /// <param name="nameSuffix">Суффикс, добавляемый к имени столбца (например, "]").</param>
        /// <param name="paramPrefix">Префикс для параметров SQL (например, "@").</param>
        /// <returns>
        /// Строку SQL-условия вида:
        /// <code>
        /// WHERE [Id] = @Id
        /// </code>
        /// Если первичные ключи отсутствуют — используются все публичные простые свойства.
        /// </returns>
        /// <remarks>
        /// Метод определяет список колонок, используемых в условии WHERE, следующим образом:
        /// <list type="number">
        /// <item>
        /// <description>Если у типа <typeparamref name="T"/> есть первичные ключи, используются только они.</description>
        /// </item>
        /// <item>
        /// <description>Если первичных ключей нет, используются все публичные базовые свойства.</description>
        /// </item>
        /// </list>
        ///
        /// Генерацию финальной строки выполняет перегрузка <c>GetWhereClause(MemberInfo[] ...)</c>.
        /// </remarks>
        public static string GetWhereClause<T>(string namePrefix = "[", string nameSuffix = "]", string paramPrefix = "@")
        {
            var mi = MemberCache.Create(typeof(T));
            var keys = mi.PrimaryKeys.Values.ToArray();
            if (keys.Length == 0)
                keys = mi.PublicBasicProperties.Values.ToArray();

            return GetWhereClause(keys, namePrefix, nameSuffix, paramPrefix);
        }

        /// <summary>
        /// Формирует SQL-условие WHERE на основе переданного набора свойств.
        /// </summary>
        /// <param name="whereProperties">
        /// Массив свойств, участвующих в формировании условий выборки.
        /// Каждое свойство должно содержать имя колонки и имя параметра.
        /// </param>
        /// <param name="namePrefix">
        /// Префикс, добавляемый перед именем колонки.
        /// По умолчанию: "[".
        /// </param>
        /// <param name="nameSuffix">
        /// Суффикс, добавляемый после имени колонки.
        /// По умолчанию: "]".
        /// </param>
        /// <param name="paramPrefix">
        /// Префикс параметров SQL.
        /// По умолчанию: "@".
        /// </param>
        /// <returns>
        /// Строка SQL, содержащая конструкцию WHERE с перечислением условий через AND.
        /// Пример результата: <c>WHERE [Id] = @Id AND [Name] = @Name</c>.
        /// </returns>
        /// <remarks>
        /// Метод не добавляет пробелов в начале или конце имён, а также не проверяет корректность
        /// переданных данных. Предполагается, что имена колонок и параметры уже валидированы.
        /// </remarks>
        public static string GetWhereClause(MemberCache[] whereProperties, string namePrefix = "[", string nameSuffix = "]", string paramPrefix = "@")
        {
            var whereClause = new StringBuilder("WHERE ");

            for (var i = 0; i < whereProperties.Length; i++)
            {
                var key = whereProperties[i];

                whereClause
                    .Append(namePrefix)
                    .Append(key.ColumnName)
                    .Append(nameSuffix)
                    .Append(" = ")
                    .Append(paramPrefix)
                    .Append(key.Name);

                if (i < whereProperties.Length - 1)
                    whereClause.Append(" AND ");
            }

            return whereClause.ToString();
        }

        /// <summary>
        /// Формирует SQL-запрос SELECT, используя выражения выбора колонок.
        /// Имена таблиц и колонок заключаются в <c>[]</c>.
        /// </summary>
        /// <typeparam name="T">
        /// Тип сущности, на основе которой строится запрос SELECT.
        /// </typeparam>
        /// <param name="selectColumns">
        /// Набор выражений <c>x => x.Property</c>, указывающих, какие колонки включить в SELECT.
        /// Если не указано ни одной колонки, используется полный набор колонок из <see cref="MemberCache"/>.
        /// </param>
        /// <returns>
        /// Строка SQL-запроса SELECT.
        /// </returns>
        public static string GetSelectQuery<T>(params Expression<Func<T, object>>[] selectColumns)
        {
            return GetSelectQuery("[", "]", selectColumns);
        }

        /// <summary>
        /// Формирует SQL-запрос SELECT с использованием пользовательских префикса и суффикса
        /// для имён колонок и таблицы.
        /// </summary>
        /// <typeparam name="T">
        /// Тип сущности, на основе которой строится запрос SELECT.
        /// </typeparam>
        /// <param name="namePrefix">
        /// Префикс, добавляемый к имени таблицы и колонок (например <c>"["</c>, <c>"`"</c>, <c>"\""</c>).
        /// </param>
        /// <param name="nameSuffix">
        /// Суффикс, добавляемый к имени таблицы и колонок (например <c>"]"</c>, <c>"`"</c>, <c>"\""</c>).
        /// </param>
        /// <param name="selectColumns">
        /// Набор выражений <c>x => x.Property</c>, указывающих колонки, которые следует включить в SELECT.
        /// Если список пуст — используются все колонки, описанные в <see cref="MemberCache"/>.
        /// </param>
        /// <returns>
        /// Строка SQL-запроса SELECT.
        /// </returns>
        /// <remarks>
        /// Если не найдено ни одной колонки, формируется запрос вида <c>SELECT * FROM ...</c>.
        /// </remarks>
        public static string GetSelectQuery<T>(string namePrefix, string nameSuffix, params Expression<Func<T, object>>[] selectColumns)
        {
            var mi = MemberCache.Create<T>();
            var members = selectColumns?.Select(ExpressionHelper.GetMemberInfo).Select(x=>x.GetMemberCache()).ToArray() ?? Array.Empty<MemberCache>();
            if (members.Length == 0)
                members = mi.ColumnProperties.Values.ToArray().Concat(mi.PrimaryKeys.Values).ToArray();
            if (members.Length == 0)
                return $"SELECT * FROM {namePrefix}{mi.TableName}{nameSuffix}";

            return GetSelectQuery(namePrefix, nameSuffix, mi, members);
        }

        /// <summary>
        /// Формирует SQL-запрос <c>SELECT</c> для указанных колонок с использованием стандартных скобок [ ].
        /// </summary>
        /// <param name="typeInfo">Информация о типе сущности, из которой выбираются данные.</param>
        /// <param name="selectColumns">Колонки, которые нужно выбрать.</param>
        /// <returns>Строка SQL-запроса <c>SELECT</c>.</returns>
        /// <remarks>
        /// Используется перегрузка метода <see cref="GetSelectQuery(string, string, MemberCache, MemberCache[])"/>,
        /// которая добавляет префикс и суффикс для имен колонок в виде квадратных скобок [ ].  
        /// Если список колонок пуст, возвращается пустая строка.
        /// </remarks>
        public static string GetSelectQuery(MemberCache typeInfo, params MemberCache[] selectColumns)
        {
            return GetSelectQuery("[", "]", typeInfo, selectColumns);
        }

        /// <summary>
        /// Формирует SQL-запрос <c>SELECT</c> для указанных колонок с кастомным префиксом и суффиксом имен.
        /// </summary>
        /// <param name="namePrefix">Префикс для каждого имени колонки (например, [).</param>
        /// <param name="nameSuffix">Суффикс для каждого имени колонки (например, ]).</param>
        /// <param name="typeInfo">Информация о типе сущности, из которой выбираются данные.</param>
        /// <param name="selectColumns">Колонки, которые нужно выбрать.</param>
        /// <returns>Строка SQL-запроса <c>SELECT</c>. Если список колонок пуст, возвращается пустая строка.</returns>
        /// <remarks>
        /// Метод формирует SQL-запрос вида:
        /// <c>SELECT [Column1], [Column2], ... FROM [TableName]</c>, где имена колонок и таблицы
        /// обрамляются указанными префиксом и суффиксом.
        /// </remarks>
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

        /// <summary>
        /// Создает SQL-запрос UPDATE для всех свойств класса T.
        /// </summary>
        /// <typeparam name="T">Тип сущности, для которой создается запрос.</typeparam>
        /// <param name="updateColumns">Свойства, которые нужно обновить. Если не указаны, обновляются все доступные свойства, кроме первичного ключа.</param>
        /// <returns>Строка с SQL-запросом UPDATE.</returns>
        public static string GetUpdateQuery<T>(params Expression<Func<T, object>>[] updateColumns) where T : class
        {
            return GetUpdateQuery("[", "]", "@", updateColumns);
        }

        /// <summary>
        /// Создает SQL-запрос UPDATE для указанных свойств класса T с настраиваемыми префиксами и суффиксами для имен таблиц и параметров.
        /// </summary>
        /// <typeparam name="T">Тип сущности, для которой создается запрос.</typeparam>
        /// <param name="namePrefix">Префикс для имен таблиц и колонок (например, "[").</param>
        /// <param name="nameSuffix">Суффикс для имен таблиц и колонок (например, "]").</param>
        /// <param name="paramPrefix">Префикс для параметров SQL (например, "@").</param>
        /// <param name="updateColumns">Свойства, которые нужно обновить. Если не указаны, обновляются все доступные свойства, кроме первичного ключа.</param>
        /// <returns>Строка с SQL-запросом UPDATE.</returns>
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

        /// <summary>
        /// Создает SQL-запрос INSERT для всех свойств класса T.
        /// </summary>
        /// <typeparam name="T">Тип сущности, для которой создается запрос.</typeparam>
        /// <param name="insertColumns">Свойства, которые нужно вставить. Если не указаны, используются все доступные свойства, кроме первичного ключа.</param>
        /// <returns>Строка с SQL-запросом INSERT.</returns>
        public static string GetInsertQuery<T>(params Expression<Func<T, object>>[] insertColumns) where T : class
        {
            return GetInsertQuery("[", "]", "@", insertColumns);
        }

        /// <summary>
        /// Создает SQL-запрос INSERT для указанных свойств класса T с настраиваемыми префиксами и суффиксами для имен таблиц и параметров.
        /// </summary>
        /// <typeparam name="T">Тип сущности, для которой создается запрос.</typeparam>
        /// <param name="namePrefix">Префикс для имен таблиц и колонок (например, "[").</param>
        /// <param name="nameSuffix">Суффикс для имен таблиц и колонок (например, "]").</param>
        /// <param name="paramPrefix">Префикс для параметров SQL (например, "@").</param>
        /// <param name="insertColumns">Свойства, которые нужно вставить. Если не указаны, используются все доступные свойства, кроме первичного ключа.</param>
        /// <returns>Строка с SQL-запросом INSERT.</returns>
        public static string GetInsertQuery<T>(string namePrefix, string nameSuffix, string paramPrefix, params Expression<Func<T, object>>[] insertColumns) where T : class
        {
            var query = new StringBuilder("INSERT INTO ");
            var mi = MemberCache.Create<T>();
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

        /// <summary>
        /// Создает SQL-запрос DELETE для таблицы, соответствующей типу <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Тип сущности, для которой формируется запрос.</typeparam>
        /// <returns>Строка с SQL-запросом DELETE без условия WHERE.</returns>
        /// <remarks>
        /// Метод формирует запрос вида:
        /// <c>DELETE FROM [TableName]</c>.
        ///
        /// Важно: метод не включает предложение WHERE — ответственность по добавлению условия
        /// лежит на вызывающем коде. Использование без WHERE может привести к удалению всех записей.
        /// </remarks>
        public static string GetDeleteQuery<T>() where T : class
        {
            return GetDeleteQuery<T>("[", "]");
        }

        /// <summary>
        /// Создает SQL-запрос DELETE для таблицы, соответствующей типу <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Тип сущности, для которой формируется запрос.</typeparam>
        /// <param name="namePrefix">Префикс, добавляемый к имени таблицы (например, "[").</param>
        /// <param name="nameSuffix">Суффикс, добавляемый к имени таблицы (например, "]").</param>
        /// <returns>Строка с SQL-запросом DELETE без условия WHERE.</returns>
        /// <remarks>
        /// Метод формирует запрос вида:
        /// <c>DELETE FROM [TableName]</c>.
        ///
        /// Важно: метод не включает предложение WHERE — ответственность по добавлению условия
        /// лежит на вызывающем коде. Использование без WHERE может привести к удалению всех записей.
        /// </remarks>
        public static string GetDeleteQuery<T>(string namePrefix, string nameSuffix) where T : class
        {
            var mi = MemberCache.Create<T>();
            var query = new StringBuilder("DELETE FROM ").Append(mi.GetFullTableName(namePrefix, nameSuffix));
            return query.ToString();
        }

        /// <summary>
        /// Формирует SQL-выражение ORDER BY на основе выражений выбора свойств и признаков сортировки.
        /// </summary>
        /// <typeparam name="T">
        /// Тип объекта, свойства которого используются для построения сортировки.
        /// </typeparam>
        /// <param name="orderBy">
        /// Набор кортежей, где первый элемент — выражение, указывающее на свойство,
        /// второй — направление сортировки (<c>true</c> = ASC, <c>false</c> = DESC).
        /// </param>
        /// <returns>
        /// Строка SQL вида <c>"ORDER BY ..."</c>.
        /// Если параметр <paramref name="orderBy"/> пуст, возвращается пустая строка.
        /// </returns>
        public static string GetOrderBy<T>(params (Expression<Func<T, object>>, bool)[] orderBy)
        {
            return GetOrderBy("[", "]", orderBy);
        }

        /// <summary>
        /// Формирует SQL-выражение ORDER BY на основе выражений выбора свойств и признаков сортировки.
        /// </summary>
        /// <typeparam name="T">
        /// Тип объекта, свойства которого используются для построения сортировки.
        /// </typeparam>
        /// <param name="namePrefix">
        /// Префикс, добавляемый перед именем столбца (например, алиас таблицы).
        /// </param>
        /// <param name="nameSuffix">
        /// Суффикс, добавляемый после имени столбца (например, закрывающая кавычка).
        /// </param>
        /// <param name="orderBy">
        /// Набор кортежей, где первый элемент — выражение, указывающее на свойство,
        /// второй — направление сортировки (<c>true</c> = ASC, <c>false</c> = DESC).
        /// </param>
        /// <returns>
        /// Строка SQL вида <c>"ORDER BY ..."</c>.
        /// Если параметр <paramref name="orderBy"/> пуст, возвращается пустая строка.
        /// </returns>
        public static string GetOrderBy<T>(string namePrefix, string nameSuffix, params (Expression<Func<T, object>>, bool)[] orderBy)
        {
            if (orderBy == null)
                return "";
            var props = orderBy.Select(x => (ExpressionHelper.GetMemberInfo(x.Item1).GetMemberCache(), x.Item2)).ToArray();
            return GetOrderBy(namePrefix, nameSuffix, props);
        }

        /// <summary>
        /// Формирует SQL-выражение ORDER BY на основе информации о полях и направления сортировки.
        /// </summary>
        /// <param name="namePrefix">
        /// Префикс, добавляемый перед именем столбца (например, алиас таблицы).
        /// </param>
        /// <param name="nameSuffix">
        /// Суффикс, добавляемый после имени столбца (например, закрывающая кавычка).
        /// </param>
        /// <param name="orderBy">
        /// Набор кортежей, где первый элемент — <see cref="MemberCache"/> с информацией о поле,
        /// второй — направление сортировки (<c>true</c> = ASC, <c>false</c> = DESC).
        /// </param>
        /// <returns>
        /// Строка SQL ORDER BY.
        /// Если коллекция пуста, возвращается пустая строка.
        /// </returns>
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

        #region PRIVATE

        private static string Visit(Expression exp)
        {
            switch (exp)
            {
                case BinaryExpression be:
                    return VisitBinary(be);

                case MemberExpression me:
                    return VisitMember(me);

                case ConstantExpression ce:
                    return VisitConstant(ce);

                case UnaryExpression ue:
                    return VisitUnary(ue);

                default:
                    throw new NotSupportedException($"Expression '{exp.NodeType}' is not supported.");
            }
        }

        private static string VisitBinary(BinaryExpression be)
        {
            var left = Visit(be.Left);
            var right = Visit(be.Right);
            var op = GetSqlOperator(be.NodeType);

            return $"({left} {op} {right})";
        }

        private static string VisitUnary(UnaryExpression ue)
        {
            switch (ue.NodeType)
            {
                case ExpressionType.Not:
                    return $"(NOT {Visit(ue.Operand)})";

                case ExpressionType.Convert:
                    return Visit(ue.Operand);

                default:
                    throw new NotSupportedException($"Unary '{ue.NodeType}' not supported.");
            }
        }

        private static string VisitMember(MemberExpression me)
        {
            var mi = MemberCache.Create(me.Member);
            // x => x.Prop
            if (me.Expression != null && me.Expression.NodeType == ExpressionType.Parameter)
                return _namePrefix + mi.ColumnName + _nameSuffix;

            // значение из замыкания: x => x.Prop == someValue
            var value = GetValue(me);
            return FormatValue(value);
        }

        private static string VisitConstant(ConstantExpression ce)
        {
            return FormatValue(ce.Value);
        }

        private static object GetValue(MemberExpression me)
        {
            // компилируем выражение для получения значения
            var lambda = Expression.Lambda<Func<object>>(
                Expression.Convert(me, typeof(object))
            );
            return lambda.Compile().Invoke();
        }

        private static string FormatValue(object value)
        {
            if (TypeHelper.NullValues.Contains(value))
                return "NULL";

            // string
            if (value is string s)
                return "'" + s.Replace("'", "''") + "'";

            // DateTime
            if (value is DateTime dt)
                return "'" + dt.ToString("yyyy-MM-dd HH:mm:ss") + "'";

            // bool
            if (value is bool b)
                return b ? "1" : "0";

            // Enum
            if (value is Enum)
                return Convert.ToInt32(value).ToString();

            // fallback
            return value.ToString();
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
    }

    #endregion PRIVATE
}