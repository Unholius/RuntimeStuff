// ***********************************************************************
// Assembly         : RuntimeStuff
// Author           : RS
// Created          : 11-19-2025
//
// Last Modified By : RS
// Last Modified On : 01-07-2026
// ***********************************************************************
// <copyright file="FormatValueOptions.cs" company="Rudnev Sergey">
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************
namespace RuntimeStuff.Options
{
    /// <summary>
    /// Определяет параметры форматирования значений
    /// при приведении их к строковому представлению.
    /// </summary>
    /// <remarks>Экземпляр данного класса используется для управления тем,
    /// как значения различных типов (даты, логические значения, строки)
    /// преобразуются в строку, например при сериализации,
    /// генерации SQL или логировании.</remarks>
    public class FormatValueOptions : OptionsBase<FormatValueOptions>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FormatValueOptions"/> class.
        /// Инициализирует новый экземпляр класса
        /// <see cref="FormatValueOptions" /> со значениями по умолчанию.
        /// </summary>
        public FormatValueOptions()
        {
        }

        /// <summary>
        /// Gets or sets получает или задаёт формат даты и времени.
        /// </summary>
        /// <value>The date format.</value>
        /// <remarks>Используется стандартный формат .NET для
        /// <see cref="System.DateTime.ToString(string)" />.
        /// Значение по умолчанию: <c>yyyy-MM-dd HH:mm:ss</c>.</remarks>
        public string DateFormat { get; set; } = "yyyy-MM-dd HH:mm:ss";

        /// <summary>
        /// Gets or sets получает или задаёт строковое представление логического значения
        /// <see langword="true" />.
        /// </summary>
        /// <value>The true string.</value>
        /// <remarks>Значение по умолчанию: <c>1</c>.</remarks>
        public string TrueString { get; set; } = "1";

        /// <summary>
        /// Gets or sets получает или задаёт строковое представление логического значения
        /// <see langword="false" />.
        /// </summary>
        /// <value>The false string.</value>
        /// <remarks>Значение по умолчанию: <c>0</c>.</remarks>
        public string FalseString { get; set; } = "0";

        /// <summary>
        /// Gets or sets получает или задаёт префикс строкового значения.
        /// </summary>
        /// <value>The string value prefix.</value>
        /// <remarks>Обычно используется для обрамления строк,
        /// например одинарной кавычкой при генерации SQL.
        /// Значение по умолчанию: <c>'</c>.</remarks>
        public string StringValuePrefix { get; set; } = "'";

        /// <summary>
        /// Gets or sets получает или задаёт суффикс строкового значения.
        /// </summary>
        /// <value>The string value suffix.</value>
        /// <remarks>Обычно используется совместно с
        /// <see cref="StringValuePrefix" /> для обрамления строк.
        /// Значение по умолчанию: <c>'</c>.</remarks>
        public string StringValueSuffix { get; set; } = "'";
    }
}