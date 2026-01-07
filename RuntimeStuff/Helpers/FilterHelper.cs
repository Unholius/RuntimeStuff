// ***********************************************************************
// Assembly         : RuntimeStuff
// Author           : RS
// Created          : 01-06-2026
//
// Last Modified By : RS
// Last Modified On : 01-07-2026
// ***********************************************************************
// <copyright file="FilterHelper.cs" company="Rudnev Sergey">
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************
namespace RuntimeStuff.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Предоставляет методы для фильтрации коллекций по строковым выражениям и тексту, позволяя гибко выбирать элементы на
    /// основе значений их свойств.
    /// </summary>
    /// <remarks>Класс предназначен для динамической фильтрации объектов по заданным условиям, которые задаются в виде
    /// строковых выражений, аналогичных SQL-выражениям. Поддерживаются операции сравнения, логические операторы, поиск по
    /// тексту, а также фильтрация по нескольким свойствам. Все методы реализованы как статические и не требуют создания
    /// экземпляра класса. Класс потокобезопасен при использовании в многопоточных сценариях.</remarks>
    public static class FilterHelper
    {
        /// <summary>
        /// The number regex.
        /// </summary>
        private static readonly Regex NumberRegex = new Regex(@"^\d+(\.\d+)?$", RegexOptions.Compiled);

        /// <summary>
        /// The property regex.
        /// </summary>
        private static readonly Regex PropertyRegex = new Regex(@"^\[(.+)\]$", RegexOptions.Compiled);

        /// <summary>
        /// The string regex.
        /// </summary>
        private static readonly Regex StringRegex = new Regex(@"^'(.*)'$", RegexOptions.Compiled);

        /// <summary>
        ///     Фильтрует элементы последовательности на основе заданного строкового выражения.
        /// </summary>
        /// <typeparam name="T">Type.</typeparam>
        /// <param name="source">Исходная коллекция.</param>
        /// <param name="filterExpression">
        ///     Выражение, имена свойств задаются в квадратных скобках, строковые значения в одинарных
        ///     кавычках. Пример: [EventId] >= 100 && [Name] like '%hello%'.
        /// </param>
        /// <returns>Отфильтрованный список элементов.</returns>
        public static IEnumerable<T> Filter<T>(IEnumerable<T> source, string filterExpression)
        {
            if (string.IsNullOrWhiteSpace(filterExpression))
            {
                return source;
            }

            // Парсим в дерево
            var tree = Parse(filterExpression);
            try
            {
                // Компилируем в Func<T,bool>
                var lambda = ToLambda<T>(tree);
                var predicate = lambda.Compile();

                // Используем одним Where
                return source.Where(predicate);
            }
            catch (Exception ex)
            {
                throw new FormatException($"Ошибка обработки фильтра '{filterExpression}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Фильтрует элементы последовательности, оставляя только те,
        /// где хотя бы одно указанное свойство содержит заданный текст. Если свойства не указаны фильтруется по всем публичным
        /// свойствам.
        /// </summary>
        /// <typeparam name="T">Тип элементов последовательности.</typeparam>
        /// <param name="source">Исходная коллекция.</param>
        /// <param name="text">Фильтрующий текст.</param>
        /// <param name="propertyNames">Список свойств, в которых выполняется поиск.
        /// Если не указан, берутся все публичные свойства.</param>
        /// <returns>Отфильтрованная коллекция.</returns>
        public static IEnumerable<T> FilterByText<T>(IEnumerable<T> source, string text, string[] propertyNames = null)
            where T : class
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return source;
            }

            // если свойства не заданы — берем все публичные
            if (propertyNames == null || propertyNames.Length == 0)
            {
                propertyNames = Obj.GetProperties<T>()
                    .Select(p => p.Name)
                    .ToArray();
            }

            text = text.ToLower();

            return source.Where(item =>
            {
                if (item == null)
                {
                    return false;
                }

                foreach (var propName in propertyNames)
                {
                    var value = Obj.Get(item, propName);
                    if (value == null)
                    {
                        continue;
                    }

                    // быстрее, чем ToString(): используем IsAssignableFrom + каст
                    if (value is string s)
                    {
                        if (s.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return true;
                        }
                    }
                    else
                    {
                        var str = value.ToString();
                        if (!string.IsNullOrEmpty(str) &&
                            str.ToLowerInvariant().Contains(text))
                        {
                            return true;
                        }
                    }
                }

                return false;
            });
        }

        /// <summary>
        /// Преобразовать текстовый фильтр в выражение.
        /// </summary>
        /// <typeparam name="T">Type.</typeparam>
        /// <param name="filter">Текстовый фильтр.</param>
        /// <returns>Expression&lt;Func&lt;T, System.Boolean&gt;&gt;.</returns>
        public static Expression<Func<T, bool>> ToExpression<T>(string filter)
        {
            if (string.IsNullOrEmpty(filter))
            {
                return x => true;
            }

            return ToLambda<T>(Parse(filter));
        }

        /// <summary>
        /// Скомпилировать текстовый фильтр в предикат.
        /// </summary>
        /// <typeparam name="T">Type.</typeparam>
        /// <param name="filter">Текстовый фильтр, например: [EventId] &gt;= 1000 || [name] like '%h%l%o%'.</param>
        /// <returns>Func&lt;T, System.Boolean&gt;.</returns>
        public static Func<T, bool> ToPredicate<T>(string filter) => ToExpression<T>(filter).Compile();

        /// <summary>
        /// Разбирает текстовое выражение в синтаксическое дерево.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <returns>Expr.</returns>
        private static Expr Parse(string input)
        {
            var tokens = Tokenize(input);
            var pos = 0;
            return ParseOr(tokens, ref pos);
        }

        /// <summary>
        /// Parses the add sub.
        /// </summary>
        /// <param name="tokens">The tokens.</param>
        /// <param name="pos">The position.</param>
        /// <returns>Expr.</returns>
        private static Expr ParseAddSub(List<string> tokens, ref int pos)
        {
            var left = ParseMulDiv(tokens, ref pos);
            while (pos < tokens.Count && (string.Compare(tokens[pos], "+", StringComparison.Ordinal) == 0 ||
                                          string.Compare(tokens[pos], "-", StringComparison.Ordinal) == 0))
            {
                var op = tokens[pos++];
                var right = ParseMulDiv(tokens, ref pos);
                left = new BinaryExpr(left, op, right);
            }

            return left;
        }

        /// <summary>
        /// Parses the and.
        /// </summary>
        /// <param name="tokens">The tokens.</param>
        /// <param name="pos">The position.</param>
        /// <returns>Expr.</returns>
        private static Expr ParseAnd(List<string> tokens, ref int pos)
        {
            var left = ParseComparison(tokens, ref pos);
            while (pos < tokens.Count && tokens[pos].ToLower() == "&&")
            {
                var op = tokens[pos++];
                var right = ParseComparison(tokens, ref pos);
                left = new BinaryExpr(left, op, right);
            }

            return left;
        }

        /// <summary>
        /// Parses the comparison.
        /// </summary>
        /// <param name="tokens">The tokens.</param>
        /// <param name="pos">The position.</param>
        /// <returns>Expr.</returns>
        /// <exception cref="System.Exception">Ожидалась {.</exception>
        private static Expr ParseComparison(List<string> tokens, ref int pos)
        {
            var left = ParseAddSub(tokens, ref pos);

            if (pos < tokens.Count)
            {
                var op = tokens[pos].ToUpper();
                if (op == "IN" || op == "NOT IN")
                {
                    var not = op == "NOT IN";
                    pos++;
                    if (tokens[pos++] != "{")
                    {
                        throw new Exception("Ожидалась {");
                    }

                    var values = new List<Expr>();
                    while (tokens[pos] != "}")
                    {
                        values.Add(ParseValue(tokens[pos++]));
                        if (tokens[pos] == ",")
                        {
                            pos++;
                        }
                    }

                    pos++; // }
                    return new InExpr(left, values, not);
                }

                if (op == "LIKE" || op == "NOT LIKE")
                {
                    var not = op == "NOT LIKE";
                    pos++;
                    var right = ParseTerm(tokens, ref pos);
                    Expr expr = new BinaryExpr(left, "Like", right);
                    if (not)
                    {
                        expr = new UnaryExpr("!", expr);
                    }

                    return expr;
                }

                if (op == "==" || op == "!=" || op == ">" || op == "<" || op == ">=" || op == "<=")
                {
                    pos++;
                    var right = ParseAddSub(tokens, ref pos);
                    return new BinaryExpr(left, op, right);
                }

                if (op == "BETWEEN" || op == "NOT BETWEEN")
                {
                    var not = op == "NOT BETWEEN";
                    pos += 2;

                    // Ожидаем: value1 AND value2
                    var lower = ParseAddSub(tokens, ref pos);

                    if (tokens[pos].Equals("AND", StringComparison.OrdinalIgnoreCase))
                    {
                        pos++; // пропускаем AND
                    }

                    pos++;
                    var upper = ParseAddSub(tokens, ref pos);

                    return new BetweenExpr(left, lower, upper, not);
                }

                if (op == "IS" && pos + 1 < tokens.Count)
                {
                    var next = tokens[pos + 1].ToUpper();
                    if (next == "NULL")
                    {
                        pos += 2; // IS NULL
                        return new BinaryExpr(left, "IS NULL", new ConstantExpr(null));
                    }

                    if (next == "NOT" && pos + 2 < tokens.Count && tokens[pos + 2].ToUpper() == "NULL")
                    {
                        pos += 3; // IS NOT NULL
                        return new BinaryExpr(left, "IS NOT NULL", new ConstantExpr(null));
                    }
                }

                // = NULL  →  IS NULL
                if (op == "=" || op == "==")
                {
                    if (pos + 1 < tokens.Count && tokens[pos].Equals("NULL", StringComparison.OrdinalIgnoreCase))
                    {
                        pos++; // пропускаем NULL
                        return new BinaryExpr(left, "IS NULL", new ConstantExpr(null));
                    }
                }

                // != NULL  →  IS NOT NULL
                if (op == "!=")
                {
                    if (pos + 1 < tokens.Count && tokens[pos].Equals("NULL", StringComparison.OrdinalIgnoreCase))
                    {
                        pos++;
                        return new BinaryExpr(left, "IS NOT NULL", new ConstantExpr(null));
                    }
                }

                // IS EMPTY / IS NOT EMPTY
                if (op == "IS" && pos + 1 < tokens.Count)
                {
                    var next = tokens[pos + 1].ToUpper();

                    if (next == "EMPTY")
                    {
                        pos += 2;
                        return new BinaryExpr(left, "IS EMPTY", null);
                    }

                    if (next == "NOT" && pos + 2 < tokens.Count && tokens[pos + 2].ToUpper() == "EMPTY")
                    {
                        pos += 3;
                        return new BinaryExpr(left, "IS NOT EMPTY", null);
                    }
                }
            }

            return left;
        }

        /// <summary>
        /// Parses the mul div.
        /// </summary>
        /// <param name="tokens">The tokens.</param>
        /// <param name="pos">The position.</param>
        /// <returns>Expr.</returns>
        private static Expr ParseMulDiv(List<string> tokens, ref int pos)
        {
            var left = ParseTerm(tokens, ref pos);
            while (pos < tokens.Count && (tokens[pos] == "*" || tokens[pos] == "/"))
            {
                var op = tokens[pos++];
                var right = ParseTerm(tokens, ref pos);
                left = new BinaryExpr(left, op, right);
            }

            return left;
        }

        /// <summary>
        /// Parses the or.
        /// </summary>
        /// <param name="tokens">The tokens.</param>
        /// <param name="pos">The position.</param>
        /// <returns>Expr.</returns>
        private static Expr ParseOr(List<string> tokens, ref int pos)
        {
            var left = ParseAnd(tokens, ref pos);
            while (pos < tokens.Count && tokens[pos].ToLower() == "||")
            {
                var op = tokens[pos++];
                var right = ParseAnd(tokens, ref pos);
                left = new BinaryExpr(left, op, right);
            }

            return left;
        }

        /// <summary>
        /// Parses the term.
        /// </summary>
        /// <param name="tokens">The tokens.</param>
        /// <param name="pos">The position.</param>
        /// <returns>Expr.</returns>
        /// <exception cref="System.FormatException">Ошибка обработки фильтра.</exception>
        /// <exception cref="System.Exception">Ожидалась ).</exception>
        private static Expr ParseTerm(List<string> tokens, ref int pos)
        {
            if (pos >= tokens.Count)
            {
                throw new FormatException("Ошибка обработки фильтра");
            }

            if (tokens[pos] == "!" || tokens[pos] == "-" || tokens[pos] == "+")
            {
                var op = tokens[pos++];
                var operand = ParseTerm(tokens, ref pos);
                return new UnaryExpr(op, operand);
            }

            if (tokens[pos] == "(")
            {
                pos++;
                var expr = ParseOr(tokens, ref pos);
                if (tokens[pos++] != ")")
                {
                    throw new Exception("Ожидалась )");
                }

                return expr;
            }

            return ParseValue(tokens[pos++]);
        }

        /// <summary>
        /// Parses the value.
        /// </summary>
        /// <param name="token">The token.</param>
        /// <returns>Expr.</returns>
        /// <exception cref="System.FormatException">Неизвестный токен {token}.</exception>
        private static Expr ParseValue(string token)
        {
            // число decimal
            if (NumberRegex.IsMatch(token))
            {
                return new ConstantExpr(decimal.Parse(token, CultureInfo.InvariantCulture));
            }

            // строка
            if (StringRegex.IsMatch(token))
            {
                return new ConstantExpr(StringRegex.Match(token).Groups[1].Value);
            }

            // property
            if (PropertyRegex.IsMatch(token))
            {
                return new PropertyExpr(PropertyRegex.Match(token).Groups[1].Value);
            }

            // дата
            if (DateTime.TryParse(token, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                return new ConstantExpr(dt);
            }

            // NULL literal
            if (string.Equals(token, "NULL", StringComparison.OrdinalIgnoreCase))
            {
                return new ConstantExpr(null);
            }

            // NULL literal
            if (string.Equals(token, "NULL", StringComparison.OrdinalIgnoreCase))
            {
                return new ConstantExpr(null);
            }

            throw new FormatException($"Неизвестный токен {token}");
        }

        /// <summary>
        /// Converts to expression.
        /// </summary>
        /// <param name="expr">The expr.</param>
        /// <param name="param">The parameter.</param>
        /// <returns>Expression.</returns>
        /// <exception cref="System.FormatException">Свойство '{p.Name}' не существует в типе '{param.Type}'.</exception>
        /// <exception cref="System.FormatException">Оператор BETWEEN не подходит для строкового параметра {left}.</exception>
        /// <exception cref="System.NotSupportedException">IS EMPTY применим только к строкам или коллекциям.</exception>
        /// <exception cref="System.NotSupportedException">IS NOT EMPTY применим только к строкам или коллекциям.</exception>
        private static Expression ToExpression(Expr expr, ParameterExpression param)
        {
            if (expr is ConstantExpr c)
            {
                return Expression.Constant(c.Value, c.Value?.GetType() ?? typeof(object));
            }

            if (expr is PropertyExpr p)
            {
                if (Obj.GetProperty(param.Type, p.Name) == null)
                {
                    throw new FormatException($"Свойство '{p.Name}' не существует в типе '{param.Type}'");
                }

                return Expression.PropertyOrField(param, p.Name);
            }

            if (expr is UnaryExpr u)
            {
                var operand = ToExpression(u.Operand, param);
                switch (u.Op)
                {
                    case "!": return Expression.Not(operand);
                    case "-": return Expression.Negate(operand);
                    case "+": return operand;
                    default: throw new NotSupportedException(u.Op);
                }
            }

            if (expr is BinaryExpr b)
            {
                var left = ToExpression(b.Left, param);
                var right = ToExpression(b.Right, param);

                // --- Авто-приведение константы к типу свойства ---
                if (b.Op != "Like" && right is ConstantExpression rc && left.Type != rc.Type)
                {
                    var converted = Obj.ChangeType(rc.Value, left.Type);
                    right = Expression.Constant(converted, left.Type);
                }

                switch (b.Op.ToUpper())
                {
                    case "IS NULL":
                        return Expression.Equal(left, Expression.Constant(null, left.Type));

                    case "IS NOT NULL":
                        return Expression.NotEqual(left, Expression.Constant(null, left.Type));

                    case "IS EMPTY":
                        {
                            if (left.Type == typeof(string))
                            {
                                return Expression.Equal(left, Expression.Constant(string.Empty, typeof(string)));
                            }

                            if (typeof(System.Collections.IEnumerable).IsAssignableFrom(left.Type))
                            {
                                var countProp = left.Type.GetProperty("Count");
                                if (countProp != null)
                                {
                                    return Expression.Equal(Expression.Property(left, countProp), Expression.Constant(0));
                                }

                                var anyMethod = typeof(Enumerable).GetMethods()
                                    .First(m => m.Name == "Any" && m.GetParameters().Length == 1)
                                    .MakeGenericMethod(left.Type.GetGenericArguments()[0]);

                                return Expression.Not(Expression.Call(anyMethod, left));
                            }

                            throw new NotSupportedException("IS EMPTY применим только к строкам или коллекциям.");
                        }

                    case "IS NOT EMPTY":
                        {
                            if (left.Type == typeof(string))
                            {
                                return Expression.NotEqual(left, Expression.Constant(string.Empty, typeof(string)));
                            }

                            if (typeof(System.Collections.IEnumerable).IsAssignableFrom(left.Type))
                            {
                                var countProp = left.Type.GetProperty("Count");
                                if (countProp != null)
                                {
                                    return Expression.GreaterThan(Expression.Property(left, countProp), Expression.Constant(0));
                                }

                                var anyMethod = typeof(Enumerable).GetMethods()
                                    .First(m => m.Name == "Any" && m.GetParameters().Length == 1)
                                    .MakeGenericMethod(left.Type.GetGenericArguments()[0]);

                                return Expression.Call(anyMethod, left);
                            }

                            throw new NotSupportedException("IS NOT EMPTY применим только к строкам или коллекциям.");
                        }

                    case "+": return Expression.Add(left, right);
                    case "-": return Expression.Subtract(left, right);
                    case "*": return Expression.Multiply(left, right);
                    case "/": return Expression.Divide(left, right);
                    case "=":
                    case "==": return Expression.Equal(left, right);
                    case "!=": return Expression.NotEqual(left, right);
                    case ">": return Expression.GreaterThan(left, right);
                    case "<": return Expression.LessThan(left, right);
                    case ">=": return Expression.GreaterThanOrEqual(left, right);
                    case "<=": return Expression.LessThanOrEqual(left, right);
                    case "&&": return Expression.AndAlso(left, right);
                    case "||": return Expression.OrElse(left, right);
                    case "LIKE":
                        var pattern = ((ConstantExpr)b.Right).Value.ToString();
                        pattern = $"^{Regex.Escape(pattern).Replace("%", ".*").Replace("_", ".")}$";
                        var regexConst = Expression.Constant(new Regex(pattern, RegexOptions.IgnoreCase));
                        if (left?.Type != null && left.Type != typeof(string))
                        {
                            left = Expression.Call(left, left.Type.GetMethod("ToString", Type.EmptyTypes) ?? throw new NullReferenceException());
                        }

                        return Expression.Call(regexConst, "IsMatch", null, left);

                    default: throw new NotSupportedException(b.Op);
                }
            }

            if (expr is InExpr i)
            {
                var leftExpr = ToExpression(i.Left, param);
                var convertedValues = i.Values.Cast<ConstantExpr>()
                    .Select(v => Convert.ChangeType(v.Value, leftExpr.Type))
                    .ToArray();

                var array = Array.CreateInstance(leftExpr.Type, convertedValues.Length);
                for (var idx = 0; idx < convertedValues.Length; idx++)
                {
                    array.SetValue(convertedValues[idx], idx);
                }

                var listType = typeof(List<>).MakeGenericType(leftExpr.Type);
                var ctor = listType.GetConstructor(new[] { typeof(IEnumerable<>).MakeGenericType(leftExpr.Type) });
                var listExpr = Expression.New(ctor ?? throw new InvalidOperationException(), Expression.Constant(array));

                var containsMethod = typeof(Enumerable)
                    .GetMethods()
                    .First(m => m.Name == "Contains" && m.GetParameters().Length == 2)
                    .MakeGenericMethod(leftExpr.Type);

                var containsCall = Expression.Call(containsMethod, listExpr, leftExpr);

                if (i.Not)
                {
                    return Expression.Not(containsCall);
                }

                return containsCall;
            }

            if (expr is BetweenExpr be)
            {
                var left = ToExpression(be.Left, param);
                var lower = ToExpression(be.Lower, param);
                var upper = ToExpression(be.Upper, param);

                if (left.Type == typeof(string))
                {
                    throw new FormatException($"Оператор BETWEEN не подходит для строкового параметра {left}");
                }

                // Приводим типы нижней и верхней границы
                if (lower is ConstantExpression lc && left.Type != lc.Type)
                {
                    var converted = Obj.ChangeType(lc.Value, left.Type);
                    lower = Expression.Constant(converted, left.Type);
                }

                if (upper is ConstantExpression uc && left.Type != uc.Type)
                {
                    var converted = Obj.ChangeType(uc.Value, left.Type);
                    upper = Expression.Constant(converted, left.Type);
                }

                // (left >= lower && left <= upper)
                var ge = Expression.GreaterThanOrEqual(left, lower);
                var le = Expression.LessThanOrEqual(left, upper);
                var betweenExpr = Expression.AndAlso(ge, le);

                if (be.Not)
                {
                    return Expression.Not(betweenExpr);
                }

                return betweenExpr;
            }

            throw new NotSupportedException(expr.GetType().Name);
        }

        /// <summary>
        /// Tokenizes the specified input.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <returns>List&lt;System.String&gt;.</returns>
        private static List<string> Tokenize(string input)
        {
            var tokens = new List<string>();
            var pattern =
                @"(is not empty\b|is empty\b|is null\b|is not null\b|\|\||&&|==|!=|<=|>=|>|<|not in\b|in\b|like\b|not like\b|\[[^\]]+\]|[()\{\}\+\-\*/]|,|NULL\b|'[^']*'|\d+(\.\d+)?|\w+)";

            foreach (Match m in Regex.Matches(input, pattern, RegexOptions.IgnoreCase))
            {
                tokens.Add(m.Value);
            }

            return tokens;
        }

        /// <summary>
        /// Converts to lambda.
        /// </summary>
        /// <typeparam name="T">Type.</typeparam>
        /// <param name="expr">The expr.</param>
        /// <returns>Expression&lt;Func&lt;T, System.Boolean&gt;&gt;.</returns>
        private static Expression<Func<T, bool>> ToLambda<T>(Expr expr)
        {
            var param = Expression.Parameter(typeof(T), "x");
            var body = ToExpression(expr, param);
            return Expression.Lambda<Func<T, bool>>(body, param);
        }

        /// <summary>
        /// Оператор BETWEEN: [prop] BETWEEN a AND b.
        /// </summary>
        internal class BetweenExpr : Expr
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="BetweenExpr" /> class.
            /// </summary>
            /// <param name="left">The left.</param>
            /// <param name="lower">The lower.</param>
            /// <param name="upper">The upper.</param>
            /// <param name="not">if set to <c>true</c> [not].</param>
            public BetweenExpr(Expr left, Expr lower, Expr upper, bool not = false)
            {
                this.Left = left;
                this.Lower = lower;
                this.Upper = upper;
                this.Not = not;
            }

            /// <summary>
            /// Gets the left.
            /// </summary>
            /// <value>The left.</value>
            public Expr Left { get; }

            /// <summary>
            /// Gets the lower.
            /// </summary>
            /// <value>The lower.</value>
            public Expr Lower { get; }

            /// <summary>
            /// Gets a value indicating whether this <see cref="BetweenExpr"/> is not.
            /// </summary>
            /// <value><c>true</c> if not; otherwise, <c>false</c>.</value>
            public bool Not { get; }

            /// <summary>
            /// Gets the upper.
            /// </summary>
            /// <value>The upper.</value>
            public Expr Upper { get; }

            /// <summary>
            /// Returns a <see cref="string" /> that represents this instance.
            /// </summary>
            /// <returns>A <see cref="string" /> that represents this instance.</returns>
            public override string ToString() => $"{this.Left} {(this.Not ? "NOT " : string.Empty)}BETWEEN {this.Lower} AND {this.Upper}";
        }

        /// <summary>
        /// Бинарное выражение: арифметика, сравнения, логика.
        /// </summary>
        internal class BinaryExpr : Expr
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="BinaryExpr" /> class.
            /// </summary>
            /// <param name="left">The left.</param>
            /// <param name="op">The op.</param>
            /// <param name="right">The right.</param>
            public BinaryExpr(Expr left, string op, Expr right)
            {
                this.Left = left;
                this.Op = op;
                this.Right = right;
            }

            /// <summary>
            /// Gets the left.
            /// </summary>
            /// <value>The left.</value>
            public Expr Left { get; }

            /// <summary>
            /// Gets the op.
            /// </summary>
            /// <value>The op.</value>
            public string Op { get; }

            /// <summary>
            /// Gets the right.
            /// </summary>
            /// <value>The right.</value>
            public Expr Right { get; }

            /// <summary>
            /// Returns a <see cref="string" /> that represents this instance.
            /// </summary>
            /// <returns>A <see cref="string" /> that represents this instance.</returns>
            public override string ToString() => $"{this.Left} {this.Op} {this.Right}";
        }

        /// <summary>
        /// Константное значение (число, строка, дата).
        /// </summary>
        internal class ConstantExpr : Expr
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="ConstantExpr" /> class.
            /// </summary>
            /// <param name="value">The value.</param>
            public ConstantExpr(object value)
            {
                this.Value = value;
            }

            /// <summary>
            /// Gets the value.
            /// </summary>
            /// <value>The value.</value>
            public object Value { get; }

            /// <summary>
            /// Returns a <see cref="string" /> that represents this instance.
            /// </summary>
            /// <returns>A <see cref="string" /> that represents this instance.</returns>
            public override string ToString() => $"{this.Value}";
        }

        /// <summary>
        /// Базовый класс для всех узлов синтаксического дерева.
        /// </summary>
        internal abstract class Expr
        {
        }

        /// <summary>
        /// Оператор "IN": [prop] in {1,2,3}.
        /// </summary>
        internal class InExpr : Expr
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="InExpr" /> class.
            /// </summary>
            /// <param name="left">The left.</param>
            /// <param name="values">The values.</param>
            /// <param name="not">if set to <c>true</c> [not].</param>
            public InExpr(Expr left, List<Expr> values, bool not = false)
            {
                this.Left = left;
                this.Values = values;
                this.Not = not;
            }

            /// <summary>
            /// Gets the left.
            /// </summary>
            /// <value>The left.</value>
            public Expr Left { get; }

            /// <summary>
            /// Gets a value indicating whether this <see cref="InExpr"/> is not.
            /// </summary>
            /// <value><c>true</c> if not; otherwise, <c>false</c>.</value>
            public bool Not { get; }

            /// <summary>
            /// Gets the values.
            /// </summary>
            /// <value>The values.</value>
            public List<Expr> Values { get; }

            /// <summary>
            /// Returns a <see cref="string" /> that represents this instance.
            /// </summary>
            /// <returns>A <see cref="string" /> that represents this instance.</returns>
            public override string ToString() => $"{this.Left} {(this.Not ? "NOT " : string.Empty)}IN {string.Join(", ", this.Values)}";
        }

        /// <summary>
        /// Ссылка на свойство вида [Name].
        /// </summary>
        internal class PropertyExpr : Expr
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="PropertyExpr" /> class.
            /// </summary>
            /// <param name="name">The name.</param>
            public PropertyExpr(string name)
            {
                this.Name = name;
            }

            /// <summary>
            /// Gets the name.
            /// </summary>
            /// <value>The name.</value>
            public string Name { get; }

            /// <summary>
            /// Returns a <see cref="string" /> that represents this instance.
            /// </summary>
            /// <returns>A <see cref="string" /> that represents this instance.</returns>
            public override string ToString() => $"[{this.Name}]";
        }

        /// <summary>
        /// Унарное выражение (!expr, -expr, +expr).
        /// </summary>
        internal class UnaryExpr : Expr
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="UnaryExpr" /> class.
            /// </summary>
            /// <param name="op">The op.</param>
            /// <param name="operand">The operand.</param>
            public UnaryExpr(string op, Expr operand)
            {
                this.Op = op;
                this.Operand = operand;
            }

            /// <summary>
            /// Gets the op.
            /// </summary>
            /// <value>The op.</value>
            public string Op { get; }

            /// <summary>
            /// Gets the operand.
            /// </summary>
            /// <value>The operand.</value>
            public Expr Operand { get; }

            /// <summary>
            /// Returns a <see cref="string" /> that represents this instance.
            /// </summary>
            /// <returns>A <see cref="string" /> that represents this instance.</returns>
            public override string ToString() => $"{this.Op} {this.Operand}";
        }
    }
}