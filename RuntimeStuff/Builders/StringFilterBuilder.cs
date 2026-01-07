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
    ///     Построитель текстового фильтра. Позволяет удобно составлять выражения фильтра в виде строки.
    /// </summary>
    public class StringFilterBuilder : IHaveOptions<FilterBuilderOptions>
    {
        /// <summary>
        ///     Операции, поддерживаемые строителем фильтра.
        /// </summary>
        public enum Operation
        {
            /// <summary>Равно</summary>
            Equal,

            /// <summary>Не равно</summary>
            NotEqual,

            /// <summary>Больше</summary>
            GreaterThan,

            /// <summary>Больше или равно</summary>
            GreaterThanOrEqual,

            /// <summary>Меньше</summary>
            LessThan,

            /// <summary>Меньше или равно</summary>
            LessThanOrEqual,

            /// <summary>Похожесть (LIKE)</summary>
            Like,

            /// <summary>Не похож (NOT LIKE)</summary>
            NotLike,

            /// <summary>В списке (IN)</summary>
            In,

            /// <summary>Не в списке (NOT IN)</summary>
            NotIn,

            /// <summary>Между (BETWEEN)</summary>
            Between,

            /// <summary>Не между (NOT BETWEEN)</summary>
            NotBetween
        }

        private readonly Dictionary<Operation, string> _operations = new Dictionary<Operation, string>
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
            { Operation.NotBetween, "NOT BETWEEN" }
        };

        private readonly StringBuilder _sb = new StringBuilder();

        private bool _needsOp;

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
        /// <param name="options"></param>
        public StringFilterBuilder(FilterBuilderOptions options)
        {
            this.Options = options ?? new FilterBuilderOptions();
        }

        public FilterBuilderOptions Options { get; set; }

        /// <summary>
        ///     Вспомогательный метод для добавления текста в строящийся фильтр.
        /// </summary>
        /// <param name="text">Текст для добавления.</param>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" /> для цепочек вызовов.</returns>
        private StringFilterBuilder Append(string text)
        {
            this._sb.Append(text);
            return this;
        }

        /// <summary>
        ///     Возвращает итоговое строковое представление фильтра.
        /// </summary>
        /// <returns>Строка фильтра.</returns>
        public override string ToString() => this._sb.ToString();

        /// <summary>
        ///     Открывает логическую группу (добавляет "(").
        /// </summary>
        /// <remarks>Если перед группой ожидается логический оператор, выбрасывается исключение.</remarks>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" />.</returns>
        /// <exception cref="InvalidOperationException">Если перед группой требуется оператор AND/OR.</exception>
        public StringFilterBuilder OpenGroup()
        {
            if (this._needsOp)
            {
                throw new InvalidOperationException("Перед группой нужен оператор AND/OR.");
            }

            return this.Append("(");
        }

        /// <summary>
        ///     Закрывает логическую группу (добавляет ")").
        /// </summary>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" />.</returns>
        public StringFilterBuilder CloseGroup()
        {
            this.Append(")");
            this._needsOp = true;
            return this;
        }

        /// <summary>
        ///     Очищает текущее состояние строителя.
        /// </summary>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" />.</returns>
        public StringFilterBuilder Clear()
        {
            this._sb.Clear();
            this._needsOp = false;
            return this;
        }

        /// <summary>
        ///     Добавляет логический оператор AND ("&&").
        /// </summary>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" />.</returns>
        public StringFilterBuilder And()
        {
            this.Append(" && ");
            this._needsOp = false;
            return this;
        }

        /// <summary>
        ///     Добавляет логический оператор OR ("||").
        /// </summary>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" />.</returns>
        public StringFilterBuilder Or()
        {
            this.Append(" || ");
            this._needsOp = false;
            return this;
        }

        /// <summary>
        ///     Добавляет логическое отрицание (!) перед выражением.
        /// </summary>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" />.</returns>
        public StringFilterBuilder Not()
        {
            this.Append("!");
            return this;
        }

        /// <summary>
        ///     Указывает свойство (имя) для следующей операции фильтра.
        /// </summary>
        /// <param name="name">Имя свойства.</param>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" />.</returns>
        /// <exception cref="InvalidOperationException">Если требуется логический оператор перед операцией.</exception>
        public StringFilterBuilder Property(string name)
        {
            if (this._needsOp)
            {
                throw new InvalidOperationException("Перед операцией требуется логический оператор.");
            }

            return this.Append($"[{name}]");
        }

        /// <summary>
        ///     Указывает свойство через селектор выражения.
        /// </summary>
        /// <typeparam name="T">Тип объекта, содержащего свойство.</typeparam>
        /// <param name="propertySelector">Выражение выбора свойства.</param>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" />.</returns>
        public StringFilterBuilder Property<T>(Expression<Func<T, object>> propertySelector) where T : class => this.Property(propertySelector.GetPropertyName());

        /// <summary>
        ///     Преобразует предикат в строковое представление фильтра и добавляет его.
        /// </summary>
        /// <typeparam name="T">Тип параметра лямбда-выражения.</typeparam>
        /// <param name="predicate">Лямбда-предикат.</param>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" />.</returns>
        /// <exception cref="ArgumentNullException">Если <paramref name="predicate" /> равен null.</exception>
        public StringFilterBuilder Where<T>(Expression<Func<T, bool>> predicate) where T : class
        {
            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            var text = FilterExpressionStringBuilder.ConvertExpression(predicate);

            this.Append(text);
            this._needsOp = true;

            return this;
        }

        /// <summary>
        ///     Добавляет оператор AND, затем применяет предикат.
        /// </summary>
        /// <typeparam name="T">Тип параметра лямбда-выражения.</typeparam>
        /// <param name="predicate">Лямбда-предикат.</param>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" />.</returns>
        public StringFilterBuilder AndWhere<T>(Expression<Func<T, bool>> predicate) where T : class => this.And().Where(predicate);

        /// <summary>
        ///     Добавляет оператор OR, затем применяет предикат.
        /// </summary>
        /// <typeparam name="T">Тип параметра лямбда-выражения.</typeparam>
        /// <param name="predicate">Лямбда-предикат.</param>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" />.</returns>
        public StringFilterBuilder OrWhere<T>(Expression<Func<T, bool>> predicate) where T : class => this.Or().Where(predicate);

        /// <summary>
        ///     Добавляет операцию равенства с указанным значением.
        /// </summary>
        /// <param name="value">Значение для сравнения.</param>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" />.</returns>
        public StringFilterBuilder Equal(object value) => this.Binary("==", value);

        /// <summary>
        ///     Добавляет операцию неравенства с указанным значением.
        /// </summary>
        /// <param name="value">Значение для сравнения.</param>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" />.</returns>
        public StringFilterBuilder NotEqual(object value) => this.Binary("!=", value);

        /// <summary>
        ///     Добавляет операцию "больше".
        /// </summary>
        /// <param name="value">Значение для сравнения.</param>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" />.</returns>
        public StringFilterBuilder GreaterThan(object value) => this.Binary(">", value);

        /// <summary>
        ///     Добавляет операцию "меньше" (LowerThan).
        /// </summary>
        /// <param name="value">Значение для сравнения.</param>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" />.</returns>
        public StringFilterBuilder LowerThan(object value) => this.Binary("<", value);

        /// <summary>
        ///     Добавляет операцию "больше или равно".
        /// </summary>
        /// <param name="value">Значение для сравнения.</param>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" />.</returns>
        public StringFilterBuilder GreaterOrEqual(object value) => this.Binary(">=", value);

        /// <summary>
        ///     Добавляет операцию "меньше или равно".
        /// </summary>
        /// <param name="value">Значение для сравнения.</param>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" />.</returns>
        public StringFilterBuilder LowerOrEqual(object value) => this.Binary("<=", value);

        /// <summary>
        ///     Вспомогательный метод для бинарных операций — добавляет оператор и форматированное значение.
        /// </summary>
        /// <param name="op">Текст оператора (например, "==").</param>
        /// <param name="value">Значение для форматирования.</param>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" />.</returns>
        private StringFilterBuilder Binary(string op, object value)
        {
            this.Append($" {op} {this.Format(value)}");
            this._needsOp = true;
            return this;
        }

        /// <summary>
        ///     Добавляет запись специфицированной операции для свойства через выражение-селектор.
        /// </summary>
        /// <typeparam name="T">Тип объекта, содержащего свойство.</typeparam>
        /// <param name="propertySelector">Селектор свойства.</param>
        /// <param name="operation">Операция (<see cref="Operation" />).</param>
        /// <param name="value">Значение операции.</param>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" />.</returns>
        public StringFilterBuilder Add<T>(Expression<Func<T, object>> propertySelector, Operation operation, object value) => this.Add(propertySelector.GetPropertyName(), operation, value);

        /// <summary>
        ///     Добавляет запись операции для свойства по имени.
        /// </summary>
        /// <param name="propertyName">Имя свойства.</param>
        /// <param name="operation">Операция (<see cref="Operation" />).</param>
        /// <param name="value">Значение (может быть IEnumerable для IN/BETWEEN и т.д.).</param>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" />.</returns>
        /// <exception cref="ArgumentException">Если имя свойства пустое или аргументы не подходят для операции.</exception>
        /// <exception cref="NotSupportedException">Если операция не поддерживается.</exception>
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
                    if (!this._operations.TryGetValue(operation, out var opString))
                    {
                        throw new NotSupportedException($"Operation {operation} is not supported.");
                    }

                    return this.Binary(opString, value);
            }
        }

        /// <summary>
        ///     Добавляет оператор LIKE с указанным шаблоном.
        /// </summary>
        /// <param name="pattern">Шаблон (без кавычек).</param>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" />.</returns>
        public StringFilterBuilder Like(string pattern)
        {
            this.Append(" LIKE ").Append(this.Format(pattern));
            this._needsOp = true;
            return this;
        }

        /// <summary>
        ///     Добавляет оператор NOT LIKE с указанным шаблоном.
        /// </summary>
        /// <param name="pattern">Шаблон (без кавычек).</param>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" />.</returns>
        public StringFilterBuilder NotLike(string pattern)
        {
            this.Append(" NOT LIKE ").Append(this.Format(pattern));
            this._needsOp = true;
            return this;
        }

        /// <summary>
        ///     Добавляет оператор IN с перечислением значений.
        /// </summary>
        /// <param name="values">Список значений.</param>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" />.</returns>
        public StringFilterBuilder In(IEnumerable<object> values)
        {
            this.Append(" IN { ").Append(string.Join(", ", values.Select(this.Format))).Append(" }");
            this._needsOp = true;
            return this;
        }

        /// <summary>
        ///     Добавляет оператор NOT IN с перечислением значений.
        /// </summary>
        /// <param name="values">Список значений.</param>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" />.</returns>
        public StringFilterBuilder NotIn(IEnumerable<object> values)
        {
            this.Append(" NOT IN { ").Append(string.Join(", ", values.Select(this.Format))).Append(" }");
            this._needsOp = true;
            return this;
        }

        /// <summary>
        ///     Добавляет оператор BETWEEN с нижней и верхней границей.
        /// </summary>
        /// <param name="low">Нижняя граница.</param>
        /// <param name="high">Верхняя граница.</param>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" />.</returns>
        public StringFilterBuilder Between(object low, object high)
        {
            this.Append($" BETWEEN {this.Format(low)} AND {this.Format(high)}");
            this._needsOp = true;
            return this;
        }

        /// <summary>
        ///     Добавляет оператор NOT BETWEEN с нижней и верхней границей.
        /// </summary>
        /// <param name="low">Нижняя граница.</param>
        /// <param name="high">Верхняя граница.</param>
        /// <returns>Текущий экземпляр <see cref="StringFilterBuilder" />.</returns>
        public StringFilterBuilder NotBetween(object low, object high)
        {
            this.Append($" NOT BETWEEN {this.Format(low)} AND {this.Format(high)}");
            this._needsOp = true;
            return this;
        }

        /// <summary>
        ///     Форматирует значение в строковое представление, понятное в фильтре.
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

        OptionsBase IHaveOptions.Options
        {
            get => this.Options;
            set => this.Options.Merge(value);
        }
    }

    /// <summary>
    ///     Вспомогательный класс для преобразования linq-выражений в строковое представление фильтра.
    /// </summary>
    internal class FilterExpressionStringBuilder : ExpressionVisitor
    {
        private readonly StringBuilder _sb = new StringBuilder();

        /// <summary>
        ///     Преобразует выражение в строку фильтра.
        /// </summary>
        /// <param name="expr">Выражение (обычно лямбда-предикат).</param>
        /// <returns>Строковое представление выражения в виде фильтра.</returns>
        public static string ConvertExpression(Expression expr)
        {
            var visitor = new FilterExpressionStringBuilder();
            visitor.Visit(expr);
            return visitor._sb.ToString();
        }

        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            this.Visit(node.Body);
            return node;
        }

        protected override Expression VisitBinary(BinaryExpression node)
        {
            this._sb.Append("(");

            this.Visit(node.Left);

            switch (node.NodeType)
            {
                case ExpressionType.Equal: this._sb.Append(" == "); break;
                case ExpressionType.NotEqual: this._sb.Append(" != "); break;
                case ExpressionType.GreaterThan: this._sb.Append(" > "); break;
                case ExpressionType.GreaterThanOrEqual: this._sb.Append(" >= "); break;
                case ExpressionType.LessThan: this._sb.Append(" < "); break;
                case ExpressionType.LessThanOrEqual: this._sb.Append(" <= "); break;
                case ExpressionType.AndAlso:
                    this._sb.Append(" && "); break;
                case ExpressionType.OrElse: this._sb.Append(" || "); break;
                default: throw new NotSupportedException(node.NodeType.ToString());
            }

            this.Visit(node.Right);

            this._sb.Append(")");

            return node;
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression != null && node.Expression.NodeType == ExpressionType.Parameter)
            {
                this._sb.Append($"[{node.Member.Name}]");
                return node;
            }

            var value = Expression.Lambda(node).Compile().DynamicInvoke();
            this.AppendConstant(value);
            return node;
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            this.AppendConstant(node.Value);
            return node;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.Name == "Contains" &&
                node.Object != null &&
                node.Object.Type == typeof(string))
            {
                this.Visit(node.Object);
                this._sb.Append(" LIKE ");
                var val = Expression.Lambda(node.Arguments[0]).Compile().DynamicInvoke()?.ToString();

                this._sb.Append($"'%{val}%'");
                return node;
            }

            if (node.Method.Name == nameof(string.StartsWith))
            {
                this.Visit(node.Object);
                this._sb.Append(" LIKE ");
                var val = Expression.Lambda(node.Arguments[0]).Compile().DynamicInvoke()?.ToString();

                this._sb.Append($"'{val}%'");
                return node;
            }

            if (node.Method.Name == nameof(string.EndsWith))
            {
                this.Visit(node.Object);
                this._sb.Append(" LIKE ");
                var val = Expression.Lambda(node.Arguments[0]).Compile().DynamicInvoke()?.ToString();

                this._sb.Append($"'%{val}'");
                return node;
            }

            throw new NotSupportedException($"Method call {node.Method.Name} not supported.");
        }

        /// <summary>
        ///     Добавляет константу в строковое представление, корректно форматируя её в зависимости от типа.
        /// </summary>
        /// <param name="value">Значение константы.</param>
        private void AppendConstant(object value)
        {
            if (value == null)
            {
                this._sb.Append("null");
                return;
            }

            switch (value)
            {
                case string s:
                    this._sb.Append($"'{s.Replace("'", "''")}'");
                    return;

                case DateTime dt:
                    this._sb.Append($"'{dt:yyyy-MM-dd HH:mm:ss}'");
                    return;

                case bool b:
                    this._sb.Append(b ? "1" : "0");
                    return;

                default:
                    this._sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                    return;
            }
        }
    }
}