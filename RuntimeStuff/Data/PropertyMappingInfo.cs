// <copyright file="PropertyMappingInfo.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace RuntimeStuff.Data
{
    using System;
    using System.Reflection;

    /// <summary>
    /// Представляет метаданные сопоставления свойства сущности
    /// с колонкой или выражением в источнике данных.
    /// </summary>
    internal sealed class PropertyMappingInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PropertyMappingInfo"/> class.
        /// </summary>
        /// <param name="property">
        /// Рефлексивное описание свойства сущности.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Выбрасывается, если <paramref name="property"/> равен <c>null</c>.
        /// </exception>
        public PropertyMappingInfo(PropertyInfo property)
        {
            this.Property = property ?? throw new ArgumentNullException(nameof(property));
        }

        /// <summary>
        /// Получает или задаёт псевдоним (alias), используемый
        /// при формировании SQL-запроса или проекции.
        /// </summary>
        /// <remarks>
        /// Может применяться для задания имени результирующей колонки
        /// в SELECT-выражении.
        /// </remarks>
        public string Alias { get; set; }

        /// <summary>
        /// Получает или задаёт имя колонки в таблице базы данных,
        /// соответствующей данному свойству.
        /// </summary>
        public string ColumnName { get; set; }

        /// <summary>
        /// Получает или задаёт SQL-функцию или выражение,
        /// применяемое к колонке (например, COUNT, SUM, UPPER и т.д.).
        /// </summary>
        /// <remarks>
        /// Используется при построении вычисляемых или агрегатных
        /// выражений в запросе.
        /// </remarks>
        public string Function { get; set; }

        /// <summary>
        /// Получает рефлексивное описание свойства сущности,
        /// к которому относится данное сопоставление.
        /// </summary>
        public PropertyInfo Property { get; }
    }
}
