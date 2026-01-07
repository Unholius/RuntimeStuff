// ***********************************************************************
// Assembly         : RuntimeStuff
// Author           : RS
// Created          : 01-06-2026
//
// Last Modified By : RS
// Last Modified On : 01-07-2026
// ***********************************************************************
// <copyright file="StringFilterBuilder.cs" company="Rudnev Sergey">
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************
namespace RuntimeStuff.Builders
{
    using System;
    using System.Globalization;
    using System.Linq.Expressions;
    using System.Text;

    /// <summary>
    /// Вспомогательный класс для преобразования linq-выражений в строковое представление фильтра.
    /// </summary>
    internal class FilterExpressionStringBuilder : ExpressionVisitor
    {
        /// <summary>
        /// The sb.
        /// </summary>
        private readonly StringBuilder sb = new StringBuilder();

        /// <summary>
        /// Преобразует выражение в строку фильтра.
        /// </summary>
        /// <param name="expr">Выражение (обычно лямбда-предикат).</param>
        /// <returns>Строковое представление выражения в виде фильтра.</returns>
        public static string ConvertExpression(Expression expr)
        {
            var visitor = new FilterExpressionStringBuilder();
            visitor.Visit(expr);
            return visitor.sb.ToString();
        }

        /// <summary>
        /// Visits the children of the <see cref="T:System.Linq.Expressions.BinaryExpression"></see>.
        /// </summary>
        /// <param name="node">The expression to visit.</param>
        /// <returns>The modified expression, if it or any subexpression was modified; otherwise, returns the original expression.</returns>
        protected override Expression VisitBinary(BinaryExpression node)
        {
            this.sb.Append("(");

            this.Visit(node.Left);

            switch (node.NodeType)
            {
                case ExpressionType.Equal: this.sb.Append(" == "); break;
                case ExpressionType.NotEqual: this.sb.Append(" != "); break;
                case ExpressionType.GreaterThan: this.sb.Append(" > "); break;
                case ExpressionType.GreaterThanOrEqual: this.sb.Append(" >= "); break;
                case ExpressionType.LessThan: this.sb.Append(" < "); break;
                case ExpressionType.LessThanOrEqual: this.sb.Append(" <= "); break;
                case ExpressionType.AndAlso:
                    this.sb.Append(" && "); break;
                case ExpressionType.OrElse: this.sb.Append(" || "); break;
                default: throw new NotSupportedException(node.NodeType.ToString());
            }

            this.Visit(node.Right);

            this.sb.Append(")");

            return node;
        }

        /// <summary>
        /// Visits the <see cref="T:System.Linq.Expressions.ConstantExpression"></see>.
        /// </summary>
        /// <param name="node">The expression to visit.</param>
        /// <returns>The modified expression, if it or any subexpression was modified; otherwise, returns the original expression.</returns>
        protected override Expression VisitConstant(ConstantExpression node)
        {
            this.AppendConstant(node.Value);
            return node;
        }

        /// <summary>
        /// Visits the children of the <see cref="T:System.Linq.Expressions.Expression`1"></see>.
        /// </summary>
        /// <typeparam name="T">The type of the delegate.</typeparam>
        /// <param name="node">The expression to visit.</param>
        /// <returns>The modified expression, if it or any subexpression was modified; otherwise, returns the original expression.</returns>
        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            this.Visit(node.Body);
            return node;
        }

        /// <summary>
        /// Visits the children of the <see cref="T:System.Linq.Expressions.MemberExpression"></see>.
        /// </summary>
        /// <param name="node">The expression to visit.</param>
        /// <returns>The modified expression, if it or any subexpression was modified; otherwise, returns the original expression.</returns>
        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression != null && node.Expression.NodeType == ExpressionType.Parameter)
            {
                this.sb.Append($"[{node.Member.Name}]");
                return node;
            }

            var value = Expression.Lambda(node).Compile().DynamicInvoke();
            this.AppendConstant(value);
            return node;
        }

        /// <summary>
        /// Visits the children of the <see cref="T:System.Linq.Expressions.MethodCallExpression"></see>.
        /// </summary>
        /// <param name="node">The expression to visit.</param>
        /// <returns>The modified expression, if it or any subexpression was modified; otherwise, returns the original expression.</returns>
        /// <exception cref="System.NotSupportedException">Method call {node.Method.Name} not supported.</exception>
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.Name == "Contains" &&
                node.Object != null &&
                node.Object.Type == typeof(string))
            {
                this.Visit(node.Object);
                this.sb.Append(" LIKE ");
                var val = Expression.Lambda(node.Arguments[0]).Compile().DynamicInvoke()?.ToString();

                this.sb.Append($"'%{val}%'");
                return node;
            }

            if (node.Method.Name == nameof(string.StartsWith))
            {
                this.Visit(node.Object);
                this.sb.Append(" LIKE ");
                var val = Expression.Lambda(node.Arguments[0]).Compile().DynamicInvoke()?.ToString();

                this.sb.Append($"'{val}%'");
                return node;
            }

            if (node.Method.Name == nameof(string.EndsWith))
            {
                this.Visit(node.Object);
                this.sb.Append(" LIKE ");
                var val = Expression.Lambda(node.Arguments[0]).Compile().DynamicInvoke()?.ToString();

                this.sb.Append($"'%{val}'");
                return node;
            }

            throw new NotSupportedException($"Method call {node.Method.Name} not supported.");
        }

        /// <summary>
        /// Добавляет константу в строковое представление, корректно форматируя её в зависимости от типа.
        /// </summary>
        /// <param name="value">Значение константы.</param>
        private void AppendConstant(object value)
        {
            if (value == null)
            {
                this.sb.Append("null");
                return;
            }

            switch (value)
            {
                case string s:
                    this.sb.Append($"'{s.Replace("'", "''")}'");
                    return;

                case DateTime dt:
                    this.sb.Append($"'{dt:yyyy-MM-dd HH:mm:ss}'");
                    return;

                case bool b:
                    this.sb.Append(b ? "1" : "0");
                    return;

                default:
                    this.sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                    return;
            }
        }
    }
}