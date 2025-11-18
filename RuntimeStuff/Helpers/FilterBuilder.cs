using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace RuntimeStuff.Helpers
{
    public class FilterBuilder
    {
        public enum Operation
        {
            Equal,
            NotEqual,
            GreaterThan,
            GreaterThanOrEqual,
            LessThan,
            LessThanOrEqual,
            Like,
            NotLike,
            In,
            NotIn,
            Between,
        }

        private Dictionary<Operation, string>  _operations = new Dictionary<Operation, string>
        {
            { Operation.Equal, "==" },
            { Operation.NotEqual, "!=" },
            { Operation.GreaterThan, ">" },
            { Operation.GreaterThanOrEqual, ">=" },
            { Operation.LessThan, "<" },
            { Operation.LessThanOrEqual, "<=" },
            { Operation.Like, "LIKE" },
            { Operation.NotLike, "NOT LIKE" },
            { Operation.In, "IN" },
            { Operation.NotIn, "NOT IN" },
            { Operation.Between, "BETWEEN" },
        };

        private readonly StringBuilder _sb = new StringBuilder();

        private bool _needsOp;

        private FilterBuilder Append(string text)
        {
            _sb.Append(text);
            return this;
        }

        public override string ToString()
        {
            return _sb.ToString();
        }

        public FilterBuilder OpenGroup()
        {
            if (_needsOp)
                throw new InvalidOperationException("Перед группой нужен оператор AND/OR.");
            return Append("(");
        }

        public FilterBuilder CloseGroup()
        {
            Append(")");
            _needsOp = true;
            return this;
        }

        public FilterBuilder Clear()
        {
            _sb.Clear();
            _needsOp = false;
            return this;
        }

        public FilterBuilder And()
        {
            Append(" && ");
            _needsOp = false;
            return this;
        }

        public FilterBuilder Or()
        {
            Append(" || ");
            _needsOp = false;
            return this;
        }

        public FilterBuilder Not()
        {
            Append("!");
            return this;
        }


        public FilterBuilder Property(string name)
        {
            if (_needsOp)
                throw new InvalidOperationException("Перед операцией требуется логический оператор.");

            return Append($"[{name}]");
        }

        public FilterBuilder Property<T>(Expression<Func<T, object>> propertySelector) where T : class
        {
            return Property(propertySelector.GetPropertyName());
        }

        public FilterBuilder Where<T>(Expression<Func<T, bool>> predicate) where T : class
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            var text = FilterExpressionStringBuilder.ConvertExpression(predicate);

            Append(text);
            _needsOp = true;

            return this;
        }

        public FilterBuilder AndWhere<T>(Expression<Func<T, bool>> predicate) where T : class
        {
            return And().Where(predicate);
        }

        public FilterBuilder OrWhere<T>(Expression<Func<T, bool>> predicate) where T : class
        {
            return Or().Where(predicate);
        }

        public FilterBuilder Equal(object value)
        {
            return Binary("==", value);
        }

        public FilterBuilder NotEqual(object value)
        {
            return Binary("!=", value);
        }

        public FilterBuilder GreaterThan(object value)
        {
            return Binary(">", value);
        }

        public FilterBuilder LowerThan(object value)
        {
            return Binary("<", value);
        }

        public FilterBuilder GreaterOrEqual(object value)
        {
            return Binary(">=", value);
        }

        public FilterBuilder LowerOrEqual(object value)
        {
            return Binary("<=", value);
        }

        private FilterBuilder Binary(string op, object value)
        {
            Append($" {op} {Format(value)}");
            _needsOp = true;
            return this;
        }

        public FilterBuilder Add<T>(Expression<Func<T, object>> propertySelector, Operation operation, object value)
        {
            return  Add(propertySelector.GetPropertyName(), operation, value);
        }

        public FilterBuilder Add(string propertyName, Operation operation, object value)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
                throw new ArgumentException("Property name cannot be null or empty.", nameof(propertyName));

            Property(propertyName);

            switch (operation)
            {
                case Operation.Between:
                    if (value is Array arr && arr.Length >= 2)
                    {
                        return Between(arr.GetValue(0), arr.GetValue(1));
                    }

                    if (value is IEnumerable e && !(value is string))
                    {
                        var list = e.Cast<object>().ToList();
                        if (list.Count < 2)
                            throw new ArgumentException("Between operation requires at least two values.", nameof(value));

                        return Between(list[0], list[1]);
                    }

                    throw new ArgumentException("Between operation requires an array or IEnumerable with at least two elements.", nameof(value));

                case Operation.In:
                    if (value is IEnumerable inValues && !(value is string))
                        return In(inValues.Cast<object>());
                    throw new ArgumentException("In operation requires an IEnumerable.", nameof(value));

                case Operation.NotIn:
                    if (value is IEnumerable notInValues && !(value is string))
                        return NotIn(notInValues.Cast<object>());
                    throw new ArgumentException("NotIn operation requires an IEnumerable.", nameof(value));

                case Operation.Like:
                    return Like(value?.ToString() ?? throw new ArgumentNullException(nameof(value)));

                case Operation.NotLike:
                    return NotLike(value?.ToString() ?? throw new ArgumentNullException(nameof(value)));

                default:
                    if (!_operations.TryGetValue(operation, out var opString))
                        throw new NotSupportedException($"Operation {operation} is not supported.");

                    return Binary(opString, value);
            }
        }


        public FilterBuilder Like(string pattern)
        {
            Append(" LIKE ").Append(Format(pattern));
            _needsOp = true;
            return this;
        }

        public FilterBuilder NotLike(string pattern)
        {
            Append(" NOT LIKE ").Append(Format(pattern));
            _needsOp = true;
            return this;
        }


        public FilterBuilder In(IEnumerable<object> values)
        {
            Append(" IN { ").Append(string.Join(", ", values.Select(Format))).Append(" }");
            _needsOp = true;
            return this;
        }

        public FilterBuilder NotIn(IEnumerable<object> values)
        {
            Append(" NOT IN { ").Append(string.Join(", ", values.Select(Format))).Append(" }");
            _needsOp = true;
            return this;
        }


        public FilterBuilder Between(object low, object high)
        {
            Append($" BETWEEN {Format(low)} AND {Format(high)}");
            _needsOp = true;
            return this;
        }

        public FilterBuilder NotBetween(object low, object high)
        {
            Append($" NOT BETWEEN {Format(low)} AND {Format(high)}");
            _needsOp = true;
            return this;
        }


        private static string Format(object value)
        {
            if (value == null)
                return "null";

            if (value is string s)
                return $"'{s.Replace("'", "''")}'";

            if (value is DateTime dt)
                return $"'{dt:yyyy-MM-dd HH:mm:ss}'";

            if (value is bool b)
                return b ? "1" : "0";

            if (value is Enum e)
                return Convert.ToInt32(e).ToString();

            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }
    }

    internal class FilterExpressionStringBuilder : ExpressionVisitor
    {
        private readonly StringBuilder _sb = new StringBuilder();

        public static string ConvertExpression(Expression expr)
        {
            var visitor = new FilterExpressionStringBuilder();
            visitor.Visit(expr);
            return visitor._sb.ToString();
        }

        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            Visit(node.Body);
            return node;
        }

        protected override Expression VisitBinary(BinaryExpression node)
        {
            _sb.Append("(");

            Visit(node.Left);

            switch (node.NodeType)
            {
                case ExpressionType.Equal: _sb.Append(" == "); break;
                case ExpressionType.NotEqual: _sb.Append(" != "); break;
                case ExpressionType.GreaterThan: _sb.Append(" > "); break;
                case ExpressionType.GreaterThanOrEqual: _sb.Append(" >= "); break;
                case ExpressionType.LessThan: _sb.Append(" < "); break;
                case ExpressionType.LessThanOrEqual: _sb.Append(" <= "); break;
                case ExpressionType.AndAlso: _sb.Append(" && "); break;
                case ExpressionType.OrElse: _sb.Append(" || "); break;
                default: throw new NotSupportedException(node.NodeType.ToString());
            }

            Visit(node.Right);

            _sb.Append(")");

            return node;
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression != null && node.Expression.NodeType == ExpressionType.Parameter)
            {
                _sb.Append($"[{node.Member.Name}]");
                return node;
            }

            var value = Expression.Lambda(node).Compile().DynamicInvoke();
            AppendConstant(value);
            return node;
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            AppendConstant(node.Value);
            return node;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.Name == nameof(string.Contains) &&
                node.Object != null &&
                node.Object.Type == typeof(string))
            {
                Visit(node.Object);
                _sb.Append(" LIKE ");
                var val = Expression.Lambda(node.Arguments[0]).Compile().DynamicInvoke()?.ToString();

                _sb.Append($"'%{val}%'");
                return node;
            }

            if (node.Method.Name == nameof(string.StartsWith))
            {
                Visit(node.Object);
                _sb.Append(" LIKE ");
                var val = Expression.Lambda(node.Arguments[0]).Compile().DynamicInvoke()?.ToString();

                _sb.Append($"'{val}%'");
                return node;
            }

            if (node.Method.Name == nameof(string.EndsWith))
            {
                Visit(node.Object);
                _sb.Append(" LIKE ");
                var val = Expression.Lambda(node.Arguments[0]).Compile().DynamicInvoke()?.ToString();

                _sb.Append($"'%{val}'");
                return node;
            }

            throw new NotSupportedException($"Method call {node.Method.Name} not supported.");
        }

        private void AppendConstant(object value)
        {
            if (value == null)
            {
                _sb.Append("null");
                return;
            }

            switch (value)
            {
                case string s:
                    _sb.Append($"'{s.Replace("'", "''")}'");
                    return;
                case DateTime dt:
                    _sb.Append($"'{dt:yyyy-MM-dd HH:mm:ss}'");
                    return;
                case bool b:
                    _sb.Append(b ? "1" : "0");
                    return;
                default:
                    _sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                    return;
            }
        }
    }
}