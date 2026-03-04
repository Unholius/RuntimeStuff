// ***********************************************************************
// Assembly         : RuntimeStuff
// Author           : RS
// Created          : 11-19-2025
//
// Last Modified By : RS
// Last Modified On : 01-07-2026
// ***********************************************************************
// <copyright file="FilterBuilderOptions.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************
namespace RuntimeStuff.Options
{
    /// <summary>
    /// Определяет параметры конфигурации построителя фильтров.
    /// </summary>
    /// <remarks>Данный класс инкапсулирует опции, используемые при формировании
    /// условий фильтрации, включая правила форматирования значений.
    /// Обычно применяется в сценариях динамического построения выражений,
    /// запросов или фильтров на основе пользовательского ввода.</remarks>
    public class FilterBuilderOptions : OptionsBase<FilterBuilderOptions>
    {
        private static readonly ValueFormatter DefaultValueFormatter = new ValueFormatter()
        {
            NonNumberValuePrefix = "'",
            NonNumberValueSuffix = "'",
            TrueValue = "1",
            FalseValue = "0",
            NullValue = "null",
            DateFormat = "'{0:yyyy.MM.dd}'",
            DateTimeFormat = "'{0:yyyy.MM.dd HH:mm:ss}'",
            TimeFormat = "'{0:HH:mm:ss}'",
            EnumAsString = true,
        };

        private ValueFormatter formatter;

        /// <summary>
        /// Initializes a new instance of the <see cref="FilterBuilderOptions"/> class.
        /// Инициализирует новый экземпляр класса
        /// <see cref="FilterBuilderOptions" /> со значениями по умолчанию.
        /// </summary>
        public FilterBuilderOptions()
        {
            this.Formatter = DefaultValueFormatter;
        }

        /// <summary>
        /// Gets or sets получает или задаёт параметры форматирования значений,
        /// используемые при построении фильтров.
        /// </summary>
        /// <value>The format options.</value>
        /// <remarks>Свойство управляет тем, как значения различных типов
        /// (например, даты, логические значения и строки)
        /// преобразуются в строковое представление.</remarks>
        public ValueFormatter Formatter
        {
            get => this.formatter ?? (this.formatter = DefaultValueFormatter);
            set => this.formatter = value;
        }
    }
}