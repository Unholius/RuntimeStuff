namespace RuntimeStuff.MSTests.Beta
{
    using System.ComponentModel;

    /// <summary>
    /// Операторы сравнения и логические операторы, используемые в SQL-условиях.
    /// </summary>
    public enum SqlOperator
    {
        /// <summary>
        /// Равно (=).
        /// </summary>
        [Description("=")]
        Equal,

        /// <summary>
        /// Не равно (<>).
        /// </summary>
        [Description("<>")]
        NotEqual,

        /// <summary>
        /// Больше (>).
        /// </summary>
        [Description(">")]
        Greater,

        /// <summary>
        /// Больше или равно (>=).
        /// </summary>
        [Description(">=")]
        GreaterOrEqual,

        /// <summary>
        /// Меньше (<).
        /// </summary>
        [Description("<")]
        Less,

        /// <summary>
        /// Меньше или равно (<=).
        /// </summary>
        [Description("<=")]
        LessOrEqual,

        /// <summary>
        /// Логическое отрицание (NOT).
        /// </summary>
        [Description("NOT")]
        Not,

        /// <summary>
        /// Проверка принадлежности множеству значений (IN).
        /// </summary>
        [Description("IN")]
        In,

        /// <summary>
        /// Проверка попадания в диапазон значений (BETWEEN).
        /// </summary>
        [Description("BETWEEN")]
        Between,
    }
}