// ***********************************************************************
// Assembly         : RuntimeStuff
// Author           : RS
// Created          : 01-06-2026
//
// Last Modified By : RS
// Last Modified On : 01-07-2026
// ***********************************************************************
// <copyright file="FilterExpressionStringBuilder.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
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
    /// Класс для преобразования выражений LINQ в строковые SQL-подобные фильтры.
    /// Используется внутри <see cref="StringFilterBuilder"/> для конвертации Expression в текстовое представление.
    /// </summary>
    internal class FilterExpressionStringBuilder : ExpressionVisitor
    {
        private readonly StringBuilder sb = new StringBuilder();

        /// <summary>
        /// Преобразует <see cref="Expression"/> в строковое представление фильтра.
        /// </summary>
        /// <param name="expr">Выражение для конвертации.</param>
        /// <returns>Строковое представление фильтра.</returns>
        public static string ConvertExpression(Expression expr)
        {
            var visitor = new FilterExpressionStringBuilder();
            visitor.Visit(expr);
            return visitor.sb.ToString();
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        protected override Expression VisitConstant(ConstantExpression node)
        {
            this.AppendConstant(node.Value);
            return node;
        }

        /// <inheritdoc/>
        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            this.Visit(node.Body);
            return node;
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
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