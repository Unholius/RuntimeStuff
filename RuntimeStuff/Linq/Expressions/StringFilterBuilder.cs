// <copyright file="StringFilterBuilder.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace System.Linq.Expressions
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Построитель строковых фильтров для выражений SQL-подобного формата.
    /// Позволяет создавать сложные фильтры с операциями сравнения, логическими операторами и группировками.
    /// </summary>
    public class StringFilterBuilder
    {
        private static readonly ReadOnlyDictionary<Token, string> DefaultSyntax = new(new Dictionary<Token, string>()
        {
            { Token.Equal, "=" },
            { Token.NotEqual, "<>" },
            { Token.GreaterThan, ">" },
            { Token.GreaterThanOrEqual, ">=" },
            { Token.LessThan, "<" },
            { Token.LessThanOrEqual, "<=" },
            { Token.Like, "LIKE" },
            { Token.NotLike, "NOT LIKE" },
            { Token.In, "IN" },
            { Token.Between, "BETWEEN" },
            { Token.And, "AND" },
            { Token.Or, "OR" },
            { Token.Not, "NOT" },
            { Token.BeginGroup, "(" },
            { Token.EndGroup, ")" },
            { Token.NamePrefix, string.Empty },
            { Token.NameSuffix, string.Empty },
        });

        private readonly ValueFormatter formatter = new()
        {
            TrueValue = "1",
            FalseValue = "0",
            NullValue = "null",
            StringPrefix = "'",
            StringSuffix = "'",
            DatePrefix = "'",
            DateSuffix = "'",
            DateFormat = "yyyy-MM-dd",
        };

        private readonly StringBuilder sb = new();
        private bool needsOp;
        private List<int> tokenIndexes = new(new[] { 0 });

        /// <summary>
        /// Initializes a new instance of the <see cref="StringFilterBuilder"/> class.
        /// </summary>
        public StringFilterBuilder()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StringFilterBuilder"/> class.
        /// </summary>
        /// <param name="formatter">Настройки.</param>
        public StringFilterBuilder(ValueFormatter formatter)
        {
            this.formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
        }

        /// <summary>
        /// Типы операций фильтрации, которые можно использовать в <see cref="StringFilterBuilder"/>.
        /// </summary>
        public enum Token
        {
            /// <summary>
            /// Оператор равенства.
            /// </summary>
            Equal,

            /// <summary>
            /// Не равно.
            /// </summary>
            NotEqual,

            /// <summary>
            /// Больше чем.
            /// </summary>
            GreaterThan,

            /// <summary>
            /// Больше или равно.
            /// </summary>
            GreaterThanOrEqual,

            /// <summary>
            /// Меньше чем.
            /// </summary>
            LessThan,

            /// <summary>
            /// Меньше или равно.
            /// </summary>
            LessThanOrEqual,

            /// <summary>
            /// Шаблонное сравнение LIKE.
            /// </summary>
            Like,

            /// <summary>
            /// Отрицание шаблонного сравнения NOT LIKE.
            /// </summary>
            NotLike,

            /// <summary>
            /// Принадлежность множеству IN.
            /// </summary>
            In,

            /// <summary>
            /// Диапазон BETWEEN.
            /// </summary>
            Between,

            /// <summary>
            /// Логическое И.
            /// </summary>
            And,

            /// <summary>
            /// Логическое ИЛИ.
            /// </summary>
            Or,

            /// <summary>
            /// Логическое НЕ.
            /// </summary>
            Not,

            /// <summary>
            /// Начало группы.
            /// </summary>
            BeginGroup,

            /// <summary>
            /// Окончание группы.
            /// </summary>
            EndGroup,

            /// <summary>
            /// Префикс перед именами свойств/полей.
            /// </summary>
            NamePrefix,

            /// <summary>
            /// Суфикс после имен свойств.
            /// </summary>
            NameSuffix,
        }

        /// <summary>
        /// Построитель текстовых фильтров с синтаксисом, адаптированным для использования в DataView.
        /// </summary>
        public static StringFilterBuilder DataTableFilterBuilder => new(new ValueFormatter()
        {
            TrueValue = "True",
            FalseValue = "False",
            NullValue = "DBNull.Value",
            StringPrefix = "\'",
            StringSuffix = "\'",
            DatePrefix = "#",
            DateSuffix = "#",
            DateFormat = "MM/dd/yyyy",
            ObjectPrefix = "'",
            ObjectSuffix = "'",
        });

        /// <summary>
        /// Настройки токенов синтаксиса.
        /// </summary>
        public Dictionary<Token, string> Syntax { get; set; } = new Dictionary<Token, string>(DefaultSyntax);

        /// <summary>
        /// Добавляет фильтр по указанному свойству и операции.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="propertySelector">Выражение для выбора свойства сущности.</param>
        /// <param name="operation">Операция фильтрации.</param>
        /// <param name="value">Значение для сравнения.</param>
        /// <returns>Текущий <see cref="StringFilterBuilder"/> для цепочки вызовов.</returns>
        public StringFilterBuilder Add<T>(Expression<Func<T, object>> propertySelector, Token operation, object value) => this.Add(propertySelector.GetPropertyName(), operation, value);

        /// <summary>
        /// Добавляет фильтр по имени свойства и операции.
        /// </summary>
        /// <param name="propertyName">Имя свойства.</param>
        /// <param name="operation">Операция фильтрации.</param>
        /// <param name="value">Значение для сравнения.</param>
        /// <returns>Текущий <see cref="StringFilterBuilder"/> для цепочки вызовов.</returns>
        public StringFilterBuilder Add(string propertyName, Token operation, object value)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                throw new ArgumentException(@"Property name cannot be null or empty.", nameof(propertyName));
            }

            this.Property(propertyName);

            switch (operation)
            {
                case Token.Between:
                    if (value is IEnumerable e && value is not string)
                    {
                        var list = e.Cast<object>().ToList();
                        if (list.Count < 2)
                        {
                            throw new ArgumentException(@"Between operation requires at least two values.", nameof(value));
                        }

                        return operation == Token.Between ? this.Between(list[0], list[1]) : this.NotBetween(list[0], list[1]);
                    }

                    throw new ArgumentException(@"Between operation requires an array or IEnumerable with at least two elements.", nameof(value));

                case Token.In:
                    if (value is IEnumerable inValues && value is not string)
                    {
                        return operation == Token.In ? this.In(inValues.Cast<object>()) : this.NotIn(inValues.Cast<object>());
                    }

                    throw new ArgumentException(@"NotIn operation requires an IEnumerable.", nameof(value));

                case Token.Like:
                    return this.Like(value?.ToString() ?? throw new ArgumentNullException(nameof(value)));

                case Token.NotLike:
                    return this.NotLike(value?.ToString() ?? throw new ArgumentNullException(nameof(value)));

                default:
                    if (!this.Syntax.TryGetValue(operation, out var opString))
                    {
                        throw new NotSupportedException($"Operation {operation} is not supported.");
                    }

                    return this.Binary(opString, value);
            }
        }

        /// <summary>
        /// Добавляет логический оператор AND.
        /// </summary>
        /// <returns>Текущий <see cref="StringFilterBuilder"/> для цепочки вызовов.</returns>
        public StringFilterBuilder And()
        {
            this.Append(" " + this.Syntax[Token.And] + " ");
            this.needsOp = false;
            return this;
        }

        /// <summary>
        /// Добавляет фильтр с AND на основе предиката.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="predicate">Лямбда-выражение предиката.</param>
        /// <returns>Текущий <see cref="StringFilterBuilder"/>.</returns>
        public StringFilterBuilder AndWhere<T>(Expression<Func<T, bool>> predicate)
            where T : class => this.And().Where(predicate);

        /// <summary>
        /// Открывает новую группу условий скобкой "(". Перед вызовом требуется логический оператор AND/OR, если группа не первая.
        /// </summary>
        /// <returns>Текущий <see cref="StringFilterBuilder"/>.</returns>
        /// <exception cref="InvalidOperationException">Если перед группой отсутствует логический оператор.</exception>
        public StringFilterBuilder BeginGroup()
        {
            if (this.needsOp)
            {
                throw new InvalidOperationException("Перед группой нужен оператор AND/OR.");
            }

            return this.Append($" {this.Syntax[Token.BeginGroup]} ");
        }

        /// <summary>
        /// Добавляет фильтр BETWEEN для диапазона значений.
        /// </summary>
        /// <param name="low">Нижняя граница.</param>
        /// <param name="high">Верхняя граница.</param>
        /// <returns>Текущий <see cref="StringFilterBuilder"/>.</returns>
        public StringFilterBuilder Between(object low, object high)
        {
            this.Append($" {this.Syntax[Token.Between]} {this.formatter.Format(low)} {this.Syntax[Token.And]} {this.formatter.Format(high)}");
            this.needsOp = true;
            return this;
        }

        /// <summary>
        /// Очищает текущий фильтр.
        /// </summary>
        /// <returns>Текущий <see cref="StringFilterBuilder"/>.</returns>
        public StringFilterBuilder Clear()
        {
            this.sb.Clear();
            this.needsOp = false;
            return this;
        }

        /// <summary>
        /// Закрывает группу фильтров скобкой ")".
        /// </summary>
        /// <returns>Текущий <see cref="StringFilterBuilder"/>.</returns>
        public StringFilterBuilder EndGroup()
        {
            this.Append($" {this.Syntax[Token.EndGroup]} ");
            this.needsOp = true;
            return this;
        }

        /// <summary>
        /// Добавляет фильтр равенства "==".
        /// </summary>
        /// <param name="value">Значение для сравнения.</param>
        /// <returns>Текущий <see cref="StringFilterBuilder"/>.</returns>
        public StringFilterBuilder Equal(object value) => this.Binary(this.Syntax[Token.Equal], value);

        /// <summary>
        /// Добавляет фильтр с операцией "больше или равно" (>=) для указанного значения.
        /// </summary>
        /// <param name="value">Значение для сравнения.</param>
        /// <returns>Текущий <see cref="StringFilterBuilder"/> для цепочки вызовов.</returns>
        public StringFilterBuilder GreaterOrEqual(object value) => this.Binary(this.Syntax[Token.GreaterThanOrEqual], value);

        /// <summary>
        /// Добавляет фильтр с операцией "больше" (>) для указанного значения.
        /// </summary>
        /// <param name="value">Значение для сравнения.</param>
        /// <returns>Текущий <see cref="StringFilterBuilder"/> для цепочки вызовов.</returns>
        public StringFilterBuilder GreaterThan(object value) => this.Binary(this.Syntax[Token.GreaterThan], value);

        /// <summary>
        /// Добавляет фильтр с операцией "IN" для коллекции значений.
        /// </summary>
        /// <param name="values">Коллекция значений.</param>
        /// <returns>Текущий <see cref="StringFilterBuilder"/>.</returns>
        public StringFilterBuilder In(IEnumerable<object> values)
        {
            this.Append($" {this.Syntax[Token.In]} {this.Syntax[Token.BeginGroup]} ").Append(string.Join(", ", values.Select(this.formatter.Format))).Append($" {this.Syntax[Token.EndGroup]}");
            this.needsOp = true;
            return this;
        }

        /// <summary>
        /// Добавляет фильтр с операцией "меньше или равно" для указанного значения.
        /// </summary>
        /// <param name="value">Значение для сравнения.</param>
        /// <returns>Текущий <see cref="StringFilterBuilder"/> для цепочки вызовов.</returns>
        public StringFilterBuilder LessOrEqual(object value) => this.Binary(this.Syntax[Token.LessThanOrEqual], value);

        /// <summary>
        /// Добавляет фильтр с операцией "меньше" для указанного значения.
        /// </summary>
        /// <param name="value">Значение для сравнения.</param>
        /// <returns>Текущий <see cref="StringFilterBuilder"/> для цепочки вызовов.</returns>
        public StringFilterBuilder LessThan(object value) => this.Binary(this.Syntax[Token.LessThan], value);

        /// <summary>
        /// Добавляет фильтр с операцией LIKE.
        /// </summary>
        /// <param name="pattern">Шаблон для сравнения.</param>
        /// <returns>Текущий <see cref="StringFilterBuilder"/>.</returns>
        public StringFilterBuilder Like(string pattern)
        {
            this.Append($" {this.Syntax[Token.Like]} ").Append(this.formatter.Format(pattern));
            this.needsOp = true;
            return this;
        }

        /// <summary>
        /// Добавляет логическое отрицание "NOT" к следующему условию.
        /// </summary>
        /// <returns>Текущий <see cref="StringFilterBuilder"/> для цепочки вызовов.</returns>
        public StringFilterBuilder Not()
        {
            this.Append(" " + this.Syntax[Token.Not] + " ");
            return this;
        }

        /// <summary>
        /// Добавляет фильтр с отрицанием диапазона значений "NOT BETWEEN".
        /// </summary>
        /// <param name="low">Нижняя граница диапазона.</param>
        /// <param name="high">Верхняя граница диапазона.</param>
        /// <returns>Текущий <see cref="StringFilterBuilder"/>.</returns>
        public StringFilterBuilder NotBetween(object low, object high)
        {
            this.Append($" {this.Syntax[Token.Not]} {this.Syntax[Token.Between]} {this.formatter.Format(low)} {this.Syntax[Token.And]} {this.formatter.Format(high)}");
            this.needsOp = true;
            return this;
        }

        /// <summary>
        /// Добавляет фильтр с операцией "не равно" (!=) для указанного значения.
        /// </summary>
        /// <param name="value">Значение для сравнения.</param>
        /// <returns>Текущий <see cref="StringFilterBuilder"/> для цепочки вызовов.</returns>
        public StringFilterBuilder NotEqual(object value) => this.Binary(this.Syntax[Token.NotEqual], value);

        /// <summary>
        /// Добавляет фильтр с отрицанием множества значений "NOT IN".
        /// </summary>
        /// <param name="values">Коллекция значений.</param>
        /// <returns>Текущий <see cref="StringFilterBuilder"/>.</returns>
        public StringFilterBuilder NotIn(IEnumerable<object> values)
        {
            this.Append($" {this.Syntax[Token.Not]} {this.Syntax[Token.In]} {this.Syntax[Token.BeginGroup]} ").Append(string.Join(", ", values.Select(this.formatter.Format))).Append($" {this.Syntax[Token.EndGroup]}");
            this.needsOp = true;
            return this;
        }

        /// <summary>
        /// Добавляет фильтр с отрицанием шаблона "NOT LIKE".
        /// </summary>
        /// <param name="pattern">Шаблон для сравнения.</param>
        /// <returns>Текущий <see cref="StringFilterBuilder"/>.</returns>
        public StringFilterBuilder NotLike(string pattern)
        {
            this.Append($" {this.Syntax[Token.Not]} {this.Syntax[Token.Like]} ").Append(this.formatter.Format(pattern));
            this.needsOp = true;
            return this;
        }

        /// <summary>
        /// Добавляет логический оператор OR.
        /// </summary>
        /// <returns>Текущий <see cref="StringFilterBuilder"/>.</returns>
        public StringFilterBuilder Or()
        {
            this.Append($" {this.Syntax[Token.Or]} ");
            this.needsOp = false;
            return this;
        }

        /// <summary>
        /// Добавляет фильтр с OR на основе предиката.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="predicate">Лямбда-выражение предиката.</param>
        /// <returns>Текущий <see cref="StringFilterBuilder"/>.</returns>
        public StringFilterBuilder OrWhere<T>(Expression<Func<T, bool>> predicate)
            where T : class => this.Or().Where(predicate);

        /// <summary>
        /// Добавляет свойство в фильтр.
        /// </summary>
        /// <param name="name">Имя свойства.</param>
        /// <returns>Текущий <see cref="StringFilterBuilder"/>.</returns>
        public StringFilterBuilder Property(string name)
        {
            if (this.needsOp)
            {
                throw new InvalidOperationException("Перед операцией требуется логический оператор.");
            }

            return this.Append($"[{name}]");
        }

        /// <summary>
        /// Добавляет свойство в фильтр по выражению.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="propertySelector">Выражение для свойства.</param>
        /// <returns>Текущий <see cref="StringFilterBuilder"/>.</returns>
        public StringFilterBuilder Property<T>(Expression<Func<T, object>> propertySelector)
            where T : class => this.Property(propertySelector.GetPropertyName());

        /// <summary>
        /// Удалить последний добавленный токен фильтра. Полезно для корректировки построенного фильтра при динамическом формировании условий.
        /// </summary>
        /// <returns>Текущий <see cref="StringFilterBuilder"/>.</returns>
        public StringFilterBuilder RemoveLast()
        {
            if (this.tokenIndexes.Count <= 1)
            {
                return this;
            }

            this.sb.Remove(this.tokenIndexes.Last(), this.sb.Length - this.tokenIndexes.Last());
            this.tokenIndexes.Remove(this.tokenIndexes.Count - 1);
            return this;
        }

        /// <inheritdoc/>
        public override string ToString() => this.sb.ToString().Trim();

        /// <summary>
        /// Добавляет фильтр на основе предиката.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="predicate">Лямбда-выражение предиката.</param>
        /// <returns>Текущий <see cref="StringFilterBuilder"/>.</returns>
        public StringFilterBuilder Where<T>(Expression<Func<T, bool>> predicate)
            where T : class
        {
            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            var text = FilterExpressionStringBuilder.ConvertExpression(predicate);

            this.Append(text);
            this.needsOp = true;

            return this;
        }

        private StringFilterBuilder Append(string text)
        {
            this.tokenIndexes.Add(this.sb.Length);
            this.sb.Append(text);
            return this;
        }

        private StringFilterBuilder Binary(string op, object value)
        {
            this.Append($" {op} {this.formatter.Format(value)}");
            this.needsOp = true;
            return this;
        }

        private class FilterToken
        {
            public Token Token { get; set; }

            public List<object> Values { get; set; } = [];
        }
    }
}