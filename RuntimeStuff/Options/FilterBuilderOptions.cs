namespace RuntimeStuff.Options
{
    /// <summary>
    /// Определяет параметры конфигурации построителя фильтров.
    /// </summary>
    /// <remarks>
    /// Данный класс инкапсулирует опции, используемые при формировании
    /// условий фильтрации, включая правила форматирования значений.
    /// Обычно применяется в сценариях динамического построения выражений,
    /// запросов или фильтров на основе пользовательского ввода.
    /// </remarks>
    public class FilterBuilderOptions : OptionsBase<FilterBuilderOptions>
    {
        /// <summary>
        /// Инициализирует новый экземпляр класса
        /// <see cref="FilterBuilderOptions"/> со значениями по умолчанию.
        /// </summary>
        public FilterBuilderOptions()
        { }

        /// <summary>
        /// Gets or sets получает или задаёт параметры форматирования значений,
        /// используемые при построении фильтров.
        /// </summary>
        /// <remarks>
        /// Свойство управляет тем, как значения различных типов
        /// (например, даты, логические значения и строки)
        /// преобразуются в строковое представление.
        /// </remarks>
        public FormatValueOptions FormatOptions { get; set; } = new FormatValueOptions();
    }
}