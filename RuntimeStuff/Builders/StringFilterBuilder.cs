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
    /// Построитель текстового фильтра. Позволяет удобно составлять выражения фильтра в виде строки.
    /// </summary>
    public class StringFilterBuilder : IHaveOptions<FilterBuilderOptions>
    {
        /// <summary>
        /// The operations.
        /// </summary>
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

        /// <summary>
        /// The sb.
        /// </summary>
        private readonly StringBuilder sb = new StringBuilder();

        /// <summary>
        /// The needs op.
        /// </summary>
        private bool needsOp;

        /// <summary>
        /// Initializes a new instance of the <see cref="StringFilterBuilder" /> class.
        /// </summary>
        public StringFilterBuilder()
        {
            this.Options = new FilterBuilderOptions();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StringFilterBuilder" /> class.
        /// </summary>
        /// <param name="options">The options.</param>
        public StringFilterBuilder(FilterBuilderOptions options)
        {
            this.Options = options ?? new FilterBuilderOptions();
        }

        /// <summary>
        /// Операции, поддерживаемые строителем фильтра.
        /// </summary>
        public enum Operation
        {
            /// <summary>
            /// Равно
            /// </summary>
            Equal,

            /// <summary>
            /// Не равно
            /// </summary>
            NotEqual,

            /// <summary>
            /// Больше
            /// </summary>
            GreaterThan,

            /// <summary>
            /// Больше или равно
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
            /// Похожесть (LIKE)
            /// </summary>
            Like,

            /// <summary>
            /// Не похож (NOT LIKE)
            /// </summary>
            NotLike,

            /// <summary>
            /// В списке (IN)
            /// </summary>
            In,

            /// <summary>
            /// Не в списке (NOT IN)
            /// </summary>
            NotIn,

            /// <summary>
            /// Между (BETWEEN)
            /// </summary>
            Between,

            /// <summary>
            /// Не между (NOT BETWEEN)
            /// </summary>
            NotBetween,
        }

        /// <summary>
        /// Gets or sets возвращает набор опций, ассоциированный с объектом.
        /// </summary>
        /// <value>The options.</value>
        /// <remarks>Свойство является ковариантным (<c>out T</c>) и предназначено
        /// только для чтения. Для изменения опций рекомендуется использовать
        /// методы самого объекта опций или создавать новый экземпляр.</remarks>
        public FilterBuilderOptions Options { get; set; }

        /// <summary>
        /// Gets or sets возвращает набор опций, ассоциированный с объектом.
        /// </summary>
        /// <value>The options.</value>
        /// <remarks>Свойство является ковариантным (<c>out T</c>) и предназначено
        /// только для чтения. Для изменения опций рекомендуется использовать
        /// методы самого объекта опций или создавать новый экземпляр.</remarks>
        OptionsBase IHaveOptions.Options
        {
            get => this.Options;
            set => this.Options.Merge(value);
        }

        /// <summary>
        /// Добавляет запись специфицированной операции для свойства через выражение-селектор.
        /// </summary>
        /// <typeparam name="T">Тип объекта, содержащего свойство.</typeparam>
        /// <param name="propertySelector">Селектор свойства.</param>
        /// <param name="operation">Операция (<see cref="Operation" />).</param>
        /// <param name="value">Значение операции.</param>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" />.</returns>
        public StringFilterBuilder Add<T>(Expression<Func<T, object>> propertySelector, Operation operation, object value) => this.Add(propertySelector.GetPropertyName(), operation, value);

        /// <summary>
        /// Добавляет запись операции для свойства по имени.
        /// </summary>
        /// <param name="propertyName">Имя свойства.</param>
        /// <param name="operation">Операция (<see cref="Operation" />).</param>
        /// <param name="value">Значение (может быть IEnumerable для IN/BETWEEN и т.д.).</param>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" />.</returns>
        /// <exception cref="System.ArgumentException">Property name cannot be null or empty. - propertyName.</exception>
        /// <exception cref="System.ArgumentException">Between operation requires at least two values. - value.</exception>
        /// <exception cref="System.ArgumentException">Between operation requires an array or IEnumerable with at least two elements. - value.</exception>
        /// <exception cref="System.ArgumentException">NotIn operation requires an IEnumerable. - value.</exception>
        /// <exception cref="System.ArgumentNullException">value.</exception>
        /// <exception cref="System.NotSupportedException">Operation {operation} is not supported.</exception>
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
        /// Добавляет логический оператор <c>AND</c> к текущему строковому фильтру.
        /// </summary>
        /// <returns>
        /// Текущий экземпляр <see cref="StringFilterBuilder"/>, позволяющий продолжить построение фильтра.
        /// </returns>
        /// <remarks>
        /// Устанавливает внутреннее состояние <see cref="needsOp"/> в <c>false</c>,
        /// чтобы указать, что оператор был добавлен и следующий вызов метода добавления условия
        /// не должен вставлять дополнительный оператор.
        /// </remarks>
        public StringFilterBuilder And()
        {
            this.Append(" && ");
            this.needsOp = false;
            return this;
        }

        /// <summary>
        /// Добавляет оператор AND, затем применяет предикат.
        /// </summary>
        /// <typeparam name="T">Тип параметра лямбда-выражения.</typeparam>
        /// <param name="predicate">Лямбда-предикат.</param>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" />.</returns>
        public StringFilterBuilder AndWhere<T>(Expression<Func<T, bool>> predicate)
            where T : class => this.And().Where(predicate);

        /// <summary>
        /// Добавляет оператор BETWEEN с нижней и верхней границей.
        /// </summary>
        /// <param name="low">Нижняя граница.</param>
        /// <param name="high">Верхняя граница.</param>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" />.</returns>
        public StringFilterBuilder Between(object low, object high)
        {
            this.Append($" BETWEEN {this.Format(low)} AND {this.Format(high)}");
            this.needsOp = true;
            return this;
        }

        /// <summary>
        /// Очищает текущее состояние строителя.
        /// </summary>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" />.</returns>
        public StringFilterBuilder Clear()
        {
            this.sb.Clear();
            this.needsOp = false;
            return this;
        }

        /// <summary>
        /// Закрывает логическую группу (добавляет ")").
        /// </summary>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" />.</returns>
        public StringFilterBuilder CloseGroup()
        {
            this.Append(")");
            this.needsOp = true;
            return this;
        }

        /// <summary>
        /// Добавляет операцию равенства с указанным значением.
        /// </summary>
        /// <param name="value">Значение для сравнения.</param>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" />.</returns>
        public StringFilterBuilder Equal(object value) => this.Binary("==", value);

        /// <summary>
        /// Добавляет операцию "больше или равно".
        /// </summary>
        /// <param name="value">Значение для сравнения.</param>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" />.</returns>
        public StringFilterBuilder GreaterOrEqual(object value) => this.Binary(">=", value);

        /// <summary>
        /// Добавляет операцию "больше".
        /// </summary>
        /// <param name="value">Значение для сравнения.</param>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" />.</returns>
        public StringFilterBuilder GreaterThan(object value) => this.Binary(">", value);

        /// <summary>
        /// Добавляет оператор IN с перечислением значений.
        /// </summary>
        /// <param name="values">Список значений.</param>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" />.</returns>
        public StringFilterBuilder In(IEnumerable<object> values)
        {
            this.Append(" IN { ").Append(string.Join(", ", values.Select(this.Format))).Append(" }");
            this.needsOp = true;
            return this;
        }

        /// <summary>
        /// Добавляет оператор LIKE с указанным шаблоном.
        /// </summary>
        /// <param name="pattern">Шаблон (без кавычек).</param>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" />.</returns>
        public StringFilterBuilder Like(string pattern)
        {
            this.Append(" LIKE ").Append(this.Format(pattern));
            this.needsOp = true;
            return this;
        }

        /// <summary>
        /// Добавляет операцию "меньше или равно".
        /// </summary>
        /// <param name="value">Значение для сравнения.</param>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" />.</returns>
        public StringFilterBuilder LowerOrEqual(object value) => this.Binary("<=", value);

        /// <summary>
        /// Добавляет операцию "меньше" (LowerThan).
        /// </summary>
        /// <param name="value">Значение для сравнения.</param>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" />.</returns>
        public StringFilterBuilder LowerThan(object value) => this.Binary("<", value);

        /// <summary>
        /// Добавляет логическое отрицание (!) перед выражением.
        /// </summary>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" />.</returns>
        public StringFilterBuilder Not()
        {
            this.Append("!");
            return this;
        }

        /// <summary>
        /// Добавляет оператор NOT BETWEEN с нижней и верхней границей.
        /// </summary>
        /// <param name="low">Нижняя граница.</param>
        /// <param name="high">Верхняя граница.</param>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" />.</returns>
        public StringFilterBuilder NotBetween(object low, object high)
        {
            this.Append($" NOT BETWEEN {this.Format(low)} AND {this.Format(high)}");
            this.needsOp = true;
            return this;
        }

        /// <summary>
        /// Добавляет операцию неравенства с указанным значением.
        /// </summary>
        /// <param name="value">Значение для сравнения.</param>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" />.</returns>
        public StringFilterBuilder NotEqual(object value) => this.Binary("!=", value);

        /// <summary>
        /// Добавляет оператор NOT IN с перечислением значений.
        /// </summary>
        /// <param name="values">Список значений.</param>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" />.</returns>
        public StringFilterBuilder NotIn(IEnumerable<object> values)
        {
            this.Append(" NOT IN { ").Append(string.Join(", ", values.Select(this.Format))).Append(" }");
            this.needsOp = true;
            return this;
        }

        /// <summary>
        /// Добавляет оператор NOT LIKE с указанным шаблоном.
        /// </summary>
        /// <param name="pattern">Шаблон (без кавычек).</param>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" />.</returns>
        public StringFilterBuilder NotLike(string pattern)
        {
            this.Append(" NOT LIKE ").Append(this.Format(pattern));
            this.needsOp = true;
            return this;
        }

        /// <summary>
        /// Открывает логическую группу (добавляет "(").
        /// </summary>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" />.</returns>
        /// <exception cref="System.InvalidOperationException">Перед группой нужен оператор AND/OR.</exception>
        /// <remarks>Если перед группой ожидается логический оператор, выбрасывается исключение.</remarks>
        public StringFilterBuilder OpenGroup()
        {
            if (this.needsOp)
            {
                throw new InvalidOperationException("Перед группой нужен оператор AND/OR.");
            }

            return this.Append("(");
        }

        /// <summary>
        /// Добавляет логический оператор OR ("||").
        /// </summary>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" />.</returns>
        public StringFilterBuilder Or()
        {
            this.Append(" || ");
            this.needsOp = false;
            return this;
        }

        /// <summary>
        /// Добавляет оператор OR, затем применяет предикат.
        /// </summary>
        /// <typeparam name="T">Тип параметра лямбда-выражения.</typeparam>
        /// <param name="predicate">Лямбда-предикат.</param>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" />.</returns>
        public StringFilterBuilder OrWhere<T>(Expression<Func<T, bool>> predicate)
            where T : class => this.Or().Where(predicate);

        /// <summary>
        /// Указывает свойство (имя) для следующей операции фильтра.
        /// </summary>
        /// <param name="name">Имя свойства.</param>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" />.</returns>
        /// <exception cref="System.InvalidOperationException">Перед операцией требуется логический оператор.</exception>
        public StringFilterBuilder Property(string name)
        {
            if (this.needsOp)
            {
                throw new InvalidOperationException("Перед операцией требуется логический оператор.");
            }

            return this.Append($"[{name}]");
        }

        /// <summary>
        /// Указывает свойство через селектор выражения.
        /// </summary>
        /// <typeparam name="T">Тип объекта, содержащего свойство.</typeparam>
        /// <param name="propertySelector">Выражение выбора свойства.</param>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" />.</returns>
        public StringFilterBuilder Property<T>(Expression<Func<T, object>> propertySelector)
            where T : class => this.Property(propertySelector.GetPropertyName());

        /// <summary>
        /// Возвращает итоговое строковое представление фильтра.
        /// </summary>
        /// <returns>Строка фильтра.</returns>
        public override string ToString() => this.sb.ToString();

        /// <summary>
        /// Преобразует предикат в строковое представление фильтра и добавляет его.
        /// </summary>
        /// <typeparam name="T">Тип параметра лямбда-выражения.</typeparam>
        /// <param name="predicate">Лямбда-предикат.</param>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" />.</returns>
        /// <exception cref="System.ArgumentNullException">predicate.</exception>
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

        /// <summary>
        /// Вспомогательный метод для добавления текста в строящийся фильтр.
        /// </summary>
        /// <param name="text">Текст для добавления.</param>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" /> для цепочек вызовов.</returns>
        private StringFilterBuilder Append(string text)
        {
            this.sb.Append(text);
            return this;
        }

        /// <summary>
        /// Вспомогательный метод для бинарных операций — добавляет оператор и форматированное значение.
        /// </summary>
        /// <param name="op">Текст оператора (например, "==").</param>
        /// <param name="value">Значение для форматирования.</param>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" />.</returns>
        private StringFilterBuilder Binary(string op, object value)
        {
            this.Append($" {op} {this.Format(value)}");
            this.needsOp = true;
            return this;
        }

        /// <summary>
        /// Форматирует значение в строковое представление, понятное в фильтре.
        /// </summary>
        /// <param name="value">Значение для форматирования.</param>
        /// <returns>Отформатированное строковое представление (включая кавычки для строк/дат).</returns>
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