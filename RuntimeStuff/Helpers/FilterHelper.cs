using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;

namespace RuntimeStuff.Helpers
{
    /// <summary>
    ///     Предоставляет методы для фильтрации коллекций по строковым выражениям и тексту, позволяя гибко выбирать элементы на
    ///     основе значений их свойств.
    /// </summary>
    /// <remarks>
    ///     Класс предназначен для динамической фильтрации объектов по заданным условиям, которые задаются в виде
    ///     строковых выражений, аналогичных SQL-выражениям. Поддерживаются операции сравнения, логические операторы, поиск по
    ///     тексту, а также фильтрация по нескольким свойствам. Все методы реализованы как статические и не требуют создания
    ///     экземпляра класса. Класс потокобезопасен при использовании в многопоточных сценариях.
    /// </remarks>
    public static class FilterHelper
    {
        #region Public

        /// <summary>
        ///     Фильтрует элементы последовательности на основе заданного строкового выражения.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">Исходная коллекция</param>
        /// <param name="filterExpression">
        ///     Выражение, имена свойств задаются в квадратных скобках, строковые значения в одинарных
        ///     кавычках. Пример: [Id] >= 100 && [Name] like '%hello%'
        /// </param>
        /// <returns></returns>
        public static IEnumerable<T> Filter<T>(IEnumerable<T> source, string filterExpression)
        {
            if (string.IsNullOrWhiteSpace(filterExpression))
                return source;
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
        ///     Фильтрует элементы последовательности, оставляя только те,
        ///     где хотя бы одно указанное свойство содержит заданный текст. Если свойства не указаны фильтруется по всем публичным
        ///     свойствам.
        /// </summary>
        /// <typeparam name="T">Тип элементов последовательности.</typeparam>
        /// <param name="source">Исходная коллекция.</param>
        /// <param name="text">Фильтрующий текст.</param>
        /// <param name="propertyNames">
        ///     Список свойств, в которых выполняется поиск.
        ///     Если не указан, берутся все публичные свойства.
        /// </param>
        /// <returns>Отфильтрованная коллекция.</returns>
        public static IEnumerable<T> FilterByText<T>(IEnumerable<T> source, string text, string[] propertyNames = null)
            where T : class
        {
            if (string.IsNullOrWhiteSpace(text))
                return source;

            // если свойства не заданы — берем все публичные
            if (propertyNames == null || propertyNames.Length == 0)
                propertyNames = TypeHelper.GetProperties<T>()
                    .Select(p => p.Name)
                    .ToArray();

            text = text.ToLower();

            return source.Where(item =>
            {
                foreach (var propName in propertyNames)
                {
                    var value = TypeHelper.Getter<T>(propName)(item);
                    if (value != null && value.ToString().ToLower().Contains(text))
                        return true;
                }

                return false;
            });
        }

        #endregion Public

        #region Internal logic

        private static readonly Regex NumberRegex = new Regex(@"^\d+(\.\d+)?$", RegexOptions.Compiled);
        private static readonly Regex PropertyRegex = new Regex(@"^\[(.+)\]$", RegexOptions.Compiled);
        private static readonly Regex StringRegex = new Regex(@"^'(.*)'$", RegexOptions.Compiled);

        /// <summary>
        ///     Разбирает текстовое выражение в синтаксическое дерево.
        /// </summary>
        private static Expr Parse(string input)
        {
            var tokens = Tokenize(input);
            var pos = 0;
            return ParseOr(tokens, ref pos);
        }

        #region Лексер

        private static List<string> Tokenize(string input)
        {
            var tokens = new List<string>();
            var pattern =
                @"(is not empty\b|is empty\b|is null\b|is not null\b|\|\||&&|==|!=|<=|>=|>|<|not in\b|in\b|like\b|not like\b|\[[^\]]+\]|[()\{\}\+\-\*/]|,|NULL\b|'[^']*'|\d+(\.\d+)?|\w+)";

            foreach (Match m in Regex.Matches(input, pattern, RegexOptions.IgnoreCase))
                tokens.Add(m.Value);
            return tokens;
        }

        #endregion Лексер

        #region Парсер (рекурсивный спуск)

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
                    if (tokens[pos++] != "{") throw new Exception("Ожидалась {");
                    var values = new List<Expr>();
                    while (tokens[pos] != "}")
                    {
                        values.Add(ParseValue(tokens[pos++]));
                        if (tokens[pos] == ",") pos++;
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
                    if (not) expr = new UnaryExpr("!", expr);
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
                        pos++; // пропускаем AND

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

        private static Expr ParseTerm(List<string> tokens, ref int pos)
        {
            if (pos >= tokens.Count)
                throw new FormatException("Ошибка обработки фильтра");

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
                if (tokens[pos++] != ")") throw new Exception("Ожидалась )");
                return expr;
            }

            return ParseValue(tokens[pos++]);
        }

        private static Expr ParseValue(string token)
        {
            // число decimal
            if (NumberRegex.IsMatch(token))
                return new ConstantExpr(decimal.Parse(token, CultureInfo.InvariantCulture));

            // строка
            if (StringRegex.IsMatch(token))
                return new ConstantExpr(StringRegex.Match(token).Groups[1].Value);

            // property
            if (PropertyRegex.IsMatch(token))
                return new PropertyExpr(PropertyRegex.Match(token).Groups[1].Value);

            // дата
            if (DateTime.TryParse(token, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return new ConstantExpr(dt);

            // NULL literal
            if (string.Equals(token, "NULL", StringComparison.OrdinalIgnoreCase))
                return new ConstantExpr(null);
            
            // NULL literal
            if (string.Equals(token, "NULL", StringComparison.OrdinalIgnoreCase))
                return new ConstantExpr(null);

            throw new FormatException($"Неизвестный токен {token}");
        }

        #endregion Парсер (рекурсивный спуск)

        #region AST → Expression

        /// <summary>
        ///     Скомпилировать текстовый фильтр в предикат
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="filter">Текстовый фильтр, например: [Id] >= 1000 || [name] like '%h%l%o%'</param>
        /// <returns></returns>
        public static Func<T, bool> ToPredicate<T>(string filter)
        {
            return ToExpression<T>(filter).Compile();
        }

        /// <summary>
        ///     Преобразовать текстовый фильтр в выражение
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="filter">Текстовый фильтр</param>
        /// <returns></returns>
        public static Expression<Func<T, bool>> ToExpression<T>(string filter)
        {
            if (string.IsNullOrEmpty(filter))
                return x => true;
            return ToLambda<T>(Parse(filter));
        }

        private static Expression<Func<T, bool>> ToLambda<T>(Expr expr)
        {
            var param = Expression.Parameter(typeof(T), "x");
            var body = ToExpression(expr, param);
            return Expression.Lambda<Func<T, bool>>(body, param);
        }

        private static Expression ToExpression(Expr expr, ParameterExpression param)
        {
            if (expr is ConstantExpr c)
                return Expression.Constant(c.Value, c.Value?.GetType() ?? typeof(object));

            if (expr is PropertyExpr p)
            {
                if (TypeHelper.GetProperty(param.Type, p.Name) == null)
                    throw new FormatException($"Свойство '{p.Name}' не существует в типе '{param.Type}'");
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
                    var converted = TypeHelper.ChangeType(rc.Value, left.Type);
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
                            return Expression.Equal(left, Expression.Constant("", typeof(string)));

                        if (typeof(System.Collections.IEnumerable).IsAssignableFrom(left.Type))
                        {
                            var countProp = left.Type.GetProperty("Count");
                            if (countProp != null)
                                return Expression.Equal(Expression.Property(left, countProp),
                                    Expression.Constant(0));

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
                            return Expression.NotEqual(left, Expression.Constant("", typeof(string)));

                        if (typeof(System.Collections.IEnumerable).IsAssignableFrom(left.Type))
                        {
                            var countProp = left.Type.GetProperty("Count");
                            if (countProp != null)
                                return Expression.GreaterThan(Expression.Property(left, countProp),
                                    Expression.Constant(0));

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
                        if (left?.Type != null && left.Type != typeof(string)) left = Expression.Call(left, left.Type.GetMethod("ToString", Type.EmptyTypes) ?? throw new NullReferenceException());
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
                    array.SetValue(convertedValues[idx], idx);

                var listType = typeof(List<>).MakeGenericType(leftExpr.Type);
                var ctor = listType.GetConstructor(new[] { typeof(IEnumerable<>).MakeGenericType(leftExpr.Type) });
                var listExpr = Expression.New(ctor ?? throw new InvalidOperationException(), Expression.Constant(array));

                var containsMethod = typeof(Enumerable)
                    .GetMethods()
                    .First(m => m.Name == "Contains" && m.GetParameters().Length == 2)
                    .MakeGenericMethod(leftExpr.Type);

                var containsCall = Expression.Call(containsMethod, listExpr, leftExpr);

                if (i.Not) return Expression.Not(containsCall);
                return containsCall;
            }

            if (expr is BetweenExpr be)
            {
                var left = ToExpression(be.Left, param);
                var lower = ToExpression(be.Lower, param);
                var upper = ToExpression(be.Upper, param);

                if (left.Type == typeof(string))
                    throw new FormatException($"Оператор BETWEEN не подходит для строкового параметра {left}");

                // Приводим типы нижней и верхней границы
                if (lower is ConstantExpression lc && left.Type != lc.Type)
                {
                    var converted = TypeHelper.ChangeType(lc.Value, left.Type);
                    lower = Expression.Constant(converted, left.Type);
                }

                if (upper is ConstantExpression uc && left.Type != uc.Type)
                {
                    var converted = TypeHelper.ChangeType(uc.Value, left.Type);
                    upper = Expression.Constant(converted, left.Type);
                }

                // (left >= lower && left <= upper)
                var ge = Expression.GreaterThanOrEqual(left, lower);
                var le = Expression.LessThanOrEqual(left, upper);
                var betweenExpr = Expression.AndAlso(ge, le);

                if (be.Not)
                    return Expression.Not(betweenExpr);

                return betweenExpr;
            }

            throw new NotSupportedException(expr.GetType().Name);
        }

        #endregion AST → Expression

        /// <summary>
        ///     Базовый класс для всех узлов синтаксического дерева.
        /// </summary>
        internal abstract class Expr
        {
        }

        /// <summary>
        ///     Оператор BETWEEN: [prop] BETWEEN a AND b.
        /// </summary>
        internal class BetweenExpr : Expr
        {
            public BetweenExpr(Expr left, Expr lower, Expr upper, bool not = false)
            {
                Left = left;
                Lower = lower;
                Upper = upper;
                Not = not;
            }

            public Expr Left { get; }
            public Expr Lower { get; }
            public Expr Upper { get; }
            public bool Not { get; }

            public override string ToString()
            {
                return $"{Left} {(Not ? "NOT " : "")}BETWEEN {Lower} AND {Upper}";
            }
        }

        /// <summary>
        ///     Бинарное выражение: арифметика, сравнения, логика.
        /// </summary>
        internal class BinaryExpr : Expr
        {
            public BinaryExpr(Expr left, string op, Expr right)
            {
                Left = left;
                Op = op;
                Right = right;
            }

            public Expr Left { get; }
            public string Op { get; }
            public Expr Right { get; }

            public override string ToString()
            {
                return $"{Left} {Op} {Right}";
            }
        }

        /// <summary>
        ///     Константное значение (число, строка, дата).
        /// </summary>
        internal class ConstantExpr : Expr
        {
            public ConstantExpr(object value)
            {
                Value = value;
            }

            public object Value { get; }

            public override string ToString()
            {
                return $"{Value}";
            }
        }

        /// <summary>
        ///     Оператор "IN": [prop] in {1,2,3}.
        /// </summary>
        internal class InExpr : Expr
        {
            public InExpr(Expr left, List<Expr> values, bool not = false)
            {
                Left = left;
                Values = values;
                Not = not;
            }

            public Expr Left { get; }
            public bool Not { get; }
            public List<Expr> Values { get; }

            public override string ToString()
            {
                return $"{Left} {(Not ? "NOT " : "")}IN {string.Join(", ", Values)}";
            }
        }

        /// <summary>
        ///     Ссылка на свойство вида [Name].
        /// </summary>
        internal class PropertyExpr : Expr
        {
            public PropertyExpr(string name)
            {
                Name = name;
            }

            public string Name { get; }

            public override string ToString()
            {
                return $"[{Name}]";
            }
        }

        /// <summary>
        ///     Унарное выражение (!expr, -expr, +expr).
        /// </summary>
        internal class UnaryExpr : Expr
        {
            public UnaryExpr(string op, Expr operand)
            {
                Op = op;
                Operand = operand;
            }

            public string Op { get; }
            public Expr Operand { get; }

            public override string ToString()
            {
                return $"{Op} {Operand}";
            }
        }

        #endregion Internal logic
    }
}