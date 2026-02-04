// ***********************************************************************
// Assembly         : RuntimeStuff
// Author           : RS
// Created          : 01-06-2026
//
// Last Modified By : RS
// Last Modified On : 01-07-2026
// ***********************************************************************
// <copyright file="StringFilterBuilder.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************
namespace RuntimeStuff.Builders
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Text;
    using RuntimeStuff.Extensions;
    using RuntimeStuff.Options;

    /// <summary>
    /// Построитель строковых фильтров для выражений SQL-подобного формата.
    /// Позволяет создавать сложные фильтры с операциями сравнения, логическими операторами и группировками.
    /// </summary>
    public class StringFilterBuilder : IHaveOptions<FilterBuilderOptions>
    {
        private readonly Dictionary<Operation, string> operations = new Dictionary<Operation, string>
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
            { Operation.NotBetween, "NOT BETWEEN" },
        };

        private readonly StringBuilder sb = new StringBuilder();

        private bool needsOp;

        /// <summary>
        /// Initializes a new instance of the <see cref="StringFilterBuilder"/> class.
        /// </summary>
        public StringFilterBuilder()
        {
            this.Options = new FilterBuilderOptions();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StringFilterBuilder"/> class.
        /// </summary>
        /// <param name="options">Настройки.</param>
        public StringFilterBuilder(FilterBuilderOptions options)
        {
            this.Options = options ?? new FilterBuilderOptions();
        }

        /// <summary>
        /// Типы операций фильтрации, которые можно использовать в <see cref="StringFilterBuilder"/>.
        /// </summary>
        public enum Operation
        {
            /// <summary>
            /// Равно (==)
            /// </summary>
            Equal,

            /// <summary>
            /// Не равно (!=)
            /// </summary>
            NotEqual,

            /// <summary>
            /// Больше (>)
            /// </summary>
            GreaterThan,

            /// <summary>
            /// Больше или равно (>=)
            /// </summary>
            GreaterThanOrEqual,

            /// <summary>
            /// Меньше
            /// </summary>
            LessThan,

            /// <summary>
            /// Меньше или равно
            /// </summary>
            LessThanOrEqual,

            /// <summary>
            /// Шаблонное сравнение LIKE
            /// </summary>
            Like,

            /// <summary>
            /// Отрицание шаблонного сравнения NOT LIKE
            /// </summary>
            NotLike,

            /// <summary>
            /// Принадлежность множеству IN
            /// </summary>
            In,

            /// <summary>
            /// Отрицание принадлежности множеству NOT IN
            /// </summary>
            NotIn,

            /// <summary>
            /// Диапазон BETWEEN
            /// </summary>
            Between,

            /// <summary>
            /// Отрицание диапазона NOT BETWEEN
            /// </summary>
            NotBetween,
        }

        /// <summary>
        /// Опции форматирования фильтров.
        /// </summary>
        public FilterBuilderOptions Options { get; set; }

        /// <summary>
        /// Опции форматирования фильтров.
        /// </summary>
        OptionsBase IHaveOptions.Options
        {
            get => this.Options;
            set => this.Options.Merge(value);
        }

        /// <summary>
        /// Добавляет фильтр по указанному свойству и операции.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="propertySelector">Выражение для выбора свойства сущности.</param>
        /// <param name="operation">Операция фильтрации.</param>
        /// <param name="value">Значение для сравнения.</param>
        /// <returns>Текущий <see cref="StringFilterBuilder"/> для цепочки вызовов.</returns>
        public StringFilterBuilder Add<T>(Expression<Func<T, object>> propertySelector, Operation operation, object value) => this.Add(propertySelector.GetPropertyName(), operation, value);

        /// <summary>
        /// Добавляет фильтр по имени свойства и операции.
        /// </summary>
        /// <param name="propertyName">Имя свойства.</param>
        /// <param name="operation">Операция фильтрации.</param>
        /// <param name="value">Значение для сравнения.</param>
        /// <returns>Текущий <see cref="StringFilterBuilder"/> для цепочки вызовов.</returns>
        public StringFilterBuilder Add(string propertyName, Operation operation, object value)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                throw new ArgumentException(@"Property name cannot be null or empty.", nameof(propertyName));
            }

            this.Property(propertyName);

            switch (operation)
            {
                case Operation.Between:
                case Operation.NotBetween:
                    if (value is IEnumerable e && !(value is string))
                    {
                        var list = e.Cast<object>().ToList();
                        if (list.Count < 2)
                        {
                            throw new ArgumentException(@"Between operation requires at least two values.", nameof(value));
                        }

                        return operation == Operation.Between ? this.Between(list[0], list[1]) : this.NotBetween(list[0], list[1]);
                    }

                    throw new ArgumentException(@"Between operation requires an array or IEnumerable with at least two elements.", nameof(value));

                case Operation.In:
                case Operation.NotIn:
                    if (value is IEnumerable inValues && !(value is string))
                    {
                        return operation == Operation.In ? this.In(inValues.Cast<object>()) : this.NotIn(inValues.Cast<object>());
                    }

                    throw new ArgumentException(@"NotIn operation requires an IEnumerable.", nameof(value));

                case Operation.Like:
                    return this.Like(value?.ToString() ?? throw new ArgumentNullException(nameof(value)));

                case Operation.NotLike:
                    return this.NotLike(value?.ToString() ?? throw new ArgumentNullException(nameof(value)));

                default:
                    if (!this.operations.TryGetValue(operation, out var opString))
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
            this.Append(" && ");
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
        /// Добавляет фильтр BETWEEN для диапазона значений.
        /// </summary>
        /// <param name="low">Нижняя граница.</param>
        /// <param name="high">Верхняя граница.</param>
        /// <returns>Текущий <see cref="StringFilterBuilder"/>.</returns>
        public StringFilterBuilder Between(object low, object high)
        {
            this.Append($" BETWEEN {this.Format(low)} AND {this.Format(high)}");
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
        public StringFilterBuilder CloseGroup()
        {
            this.Append(")");
            this.needsOp = true;
            return this;
        }

        /// <summary>
        /// Добавляет фильтр равенства "==".
        /// </summary>
        /// <param name="value">Значение для сравнения.</param>
        /// <returns>Текущий <see cref="StringFilterBuilder"/>.</returns>
        public StringFilterBuilder Equal(object value) => this.Binary("==", value);

        /// <summary>
        /// Добавляет фильтр с операцией "больше или равно" (>=) для указанного значения.
        /// </summary>
        /// <param name="value">Значение для сравнения.</param>
        /// <returns>Текущий <see cref="StringFilterBuilder"/> для цепочки вызовов.</returns>
        public StringFilterBuilder GreaterOrEqual(object value) => this.Binary(">=", value);

        /// <summary>
        /// Добавляет фильтр с операцией "больше" (>) для указанного значения.
        /// </summary>
        /// <param name="value">Значение для сравнения.</param>
        /// <returns>Текущий <see cref="StringFilterBuilder"/> для цепочки вызовов.</returns>
        public StringFilterBuilder GreaterThan(object value) => this.Binary(">", value);

        /// <summary>
        /// Добавляет фильтр с операцией "IN" для коллекции значений.
        /// </summary>
        /// <param name="values">Коллекция значений.</param>
        /// <returns>Текущий <see cref="StringFilterBuilder"/>.</returns>
        public StringFilterBuilder In(IEnumerable<object> values)
        {
            this.Append(" IN { ").Append(string.Join(", ", values.Select(this.Format))).Append(" }");
            this.needsOp = true;
            return this;
        }

        /// <summary>
        /// Добавляет фильтр с операцией LIKE.
        /// </summary>
        /// <param name="pattern">Шаблон для сравнения.</param>
        /// <returns>Текущий <see cref="StringFilterBuilder"/>.</returns>
        public StringFilterBuilder Like(string pattern)
        {
            this.Append(" LIKE ").Append(this.Format(pattern));
            this.needsOp = true;
            return this;
        }

        /// <summary>
        /// Добавляет фильтр с операцией "меньше или равно" для указанного значения.
        /// </summary>
        /// <param name="value">Значение для сравнения.</param>
        /// <returns>Текущий <see cref="StringFilterBuilder"/> для цепочки вызовов.</returns>
        public StringFilterBuilder LowerOrEqual(object value) => this.Binary("<=", value);

        /// <summary>
        /// Добавляет фильтр с операцией "меньше" для указанного значения.
        /// </summary>
        /// <param name="value">Значение для сравнения.</param>
        /// <returns>Текущий <see cref="StringFilterBuilder"/> для цепочки вызовов.</returns>
        public StringFilterBuilder LowerThan(object value) => this.Binary("<", value);

        /// <summary>
        /// Добавляет логическое отрицание "NOT" к следующему условию.
        /// </summary>
        /// <returns>Текущий <see cref="StringFilterBuilder"/> для цепочки вызовов.</returns>
        public StringFilterBuilder Not()
        {
            this.Append("!");
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
            this.Append($" NOT BETWEEN {this.Format(low)} AND {this.Format(high)}");
            this.needsOp = true;
            return this;
        }

        /// <summary>
        /// Добавляет фильтр с операцией "не равно" (!=) для указанного значения.
        /// </summary>
        /// <param name="value">Значение для сравнения.</param>
        /// <returns>Текущий <see cref="StringFilterBuilder"/> для цепочки вызовов.</returns>
        public StringFilterBuilder NotEqual(object value) => this.Binary("!=", value);

        /// <summary>
        /// Добавляет фильтр с отрицанием множества значений "NOT IN".
        /// </summary>
        /// <param name="values">Коллекция значений.</param>
        /// <returns>Текущий <see cref="StringFilterBuilder"/>.</returns>
        public StringFilterBuilder NotIn(IEnumerable<object> values)
        {
            this.Append(" NOT IN { ").Append(string.Join(", ", values.Select(this.Format))).Append(" }");
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
            this.Append(" NOT LIKE ").Append(this.Format(pattern));
            this.needsOp = true;
            return this;
        }

        /// <summary>
        /// Открывает новую группу условий скобкой "(". Перед вызовом требуется логический оператор AND/OR, если группа не первая.
        /// </summary>
        /// <returns>Текущий <see cref="StringFilterBuilder"/>.</returns>
        /// <exception cref="InvalidOperationException">Если перед группой отсутствует логический оператор.</exception>
        public StringFilterBuilder OpenGroup()
        {
            if (this.needsOp)
            {
                throw new InvalidOperationException("Перед группой нужен оператор AND/OR.");
            }

            return this.Append("(");
        }

        /// <summary>
        /// Добавляет логический оператор OR.
        /// </summary>
        /// <returns>Текущий <see cref="StringFilterBuilder"/>.</returns>
        public StringFilterBuilder Or()
        {
            this.Append(" || ");
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

        /// <inheritdoc/>
        public override string ToString() => this.sb.ToString();

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
            this.sb.Append(text);
            return this;
        }

        private StringFilterBuilder Binary(string op, object value)
        {
            this.Append($" {op} {this.Format(value)}");
            this.needsOp = true;
            return this;
        }

        private string Format(object value)
        {
            if (value == null)
            {
                return "null";
            }

            if (value is string s)
            {
                return $"{this.Options.FormatOptions.StringValuePrefix}{s}{this.Options.FormatOptions.StringValueSuffix}";
            }

            if (value is DateTime dt)
            {
                return string.Format(this.Options.FormatOptions.StringValuePrefix + "{0:" + this.Options.FormatOptions.DateFormat + "}" + this.Options.FormatOptions.StringValueSuffix, dt);
            }

            if (value is bool b)
            {
                return b ? this.Options.FormatOptions.TrueString : this.Options.FormatOptions.FalseString;
            }

            if (value is Enum e)
            {
                return Convert.ToInt32(e).ToString();
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }
    }
}