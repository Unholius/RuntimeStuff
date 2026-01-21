// ***********************************************************************
// Assembly         : RuntimeStuff
// Author           : RS
// Created          : 01-06-2026
//
// Last Modified By : RS
// Last Modified On : 01-07-2026
// ***********************************************************************
// <copyright file="DataTableHelperExtensions.cs" company="Rudnev Sergey">
//     Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************
namespace RuntimeStuff.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using RuntimeStuff.Helpers;

    /// <summary>
    /// Предоставляет вспомогательные методы для работы с
    /// <see cref="DataTable" />, включая добавление колонок и строк,
    /// а также преобразование данных в коллекции объектов.
    /// </summary>
    /// <remarks>Класс предназначен для упрощения типовых операций с
    /// <see cref="DataTable" /> в сценариях сериализации,
    /// загрузки данных и преобразования табличных структур
    /// в объектные модели.</remarks>
    public static class DataTableHelperExtensions
    {
        /// <summary>
        /// Добавляет колонку в таблицу данных.
        /// </summary>
        /// <param name="table">Таблица, в которую добавляется колонка.</param>
        /// <param name="columnName">Имя добавляемой колонки.</param>
        /// <param name="columnType">Тип данных колонки.</param>
        /// <param name="isPrimaryKey">Указывает, должна ли колонка быть частью первичного ключа.</param>
        /// <returns>Созданный экземпляр <see cref="DataColumn" />.</returns>
        /// <exception cref="ArgumentNullException">Выбрасывается, если <paramref name="table" /> или
        /// <paramref name="columnType" /> равны <see langword="null" />.</exception>
        /// <exception cref="ArgumentException">Выбрасывается, если имя колонки пустое
        /// или колонка с таким именем уже существует.</exception>
        /// <remarks>Если колонка помечена как первичный ключ,
        /// она автоматически добавляется в массив
        /// <see cref="DataTable.PrimaryKey" />.</remarks>
        public static DataColumn AddCol(this DataTable table, string columnName, Type columnType = null, bool isPrimaryKey = false) => DataTableHelper.AddCol(table, columnName, columnType, isPrimaryKey);

        /// <summary>
        /// Добавляет строку в таблицу данных из массива значений.
        /// </summary>
        /// <param name="table">Таблица, в которую добавляется строка.</param>
        /// <param name="rowData">Массив значений строки, соответствующий порядку колонок таблицы.</param>
        /// <returns>Добавленная строка <see cref="DataRow" />.</returns>
        /// <exception cref="ArgumentNullException">Выбрасывается, если <paramref name="table" /> или
        /// <paramref name="rowData" /> равны <see langword="null" />.</exception>
        /// <exception cref="ArgumentException">Выбрасывается, если количество элементов в массиве
        /// не совпадает с количеством колонок таблицы.</exception>
        /// <remarks>Значения <see langword="null" /> автоматически преобразуются
        /// в <see cref="DBNull.Value" />.</remarks>
        public static DataTable AddRow(this DataTable table, params object[] rowData)
        {
            DataTableHelper.AddRow(table, rowData);
            return table;
        }

        /// <summary>
        /// Добавляет строку в таблицу данных на основе свойств объекта.
        /// </summary>
        /// <typeparam name="T">Тип объекта, значения свойств которого используются
        /// для заполнения строки.</typeparam>
        /// <param name="table">Таблица, в которую добавляется строка.</param>
        /// <param name="item">Объект-источник значений.</param>
        /// <returns>Добавленная строка <see cref="DataRow" />.</returns>
        /// <exception cref="ArgumentNullException">Выбрасывается, если <paramref name="table" /> или
        /// <paramref name="item" /> равны <see langword="null" />.</exception>
        /// <remarks>Значения берутся из свойств объекта по имени,
        /// совпадающему с именем колонки таблицы.</remarks>
        public static DataTable AddRow<T>(this DataTable table, T item)
            where T : class, new()
        {
            DataTableHelper.AddRow(table, item);
            return table;
        }

        /// <summary>
        /// Преобразует значения указанной колонки таблицы
        /// в список заданного типа.
        /// </summary>
        /// <typeparam name="T">Тип элементов результирующего списка.</typeparam>
        /// <param name="table">Исходная таблица данных.</param>
        /// <param name="columnName">Имя колонки, значения которой будут извлечены.</param>
        /// <returns>Список значений указанной колонки.</returns>
        /// <exception cref="ArgumentNullException">Выбрасывается, если <paramref name="table" /> равен <see langword="null" />.</exception>
        /// <exception cref="ArgumentException">Выбрасывается, если колонка не найдена.</exception>
        /// <remarks>Строки со значением <see cref="DBNull.Value" />
        /// пропускаются.</remarks>
        public static List<T> ToList<T>(this DataTable table, string columnName)
            where T : struct
                => DataTableHelper.ToList<T>(table, columnName);

        /// <summary>
        /// Преобразует значения указанной колонки таблицы
        /// в список заданного типа.
        /// </summary>
        /// <param name="table">Исходная таблица данных.</param>
        /// <param name="columnName">Имя колонки, значения которой будут извлечены.</param>
        /// <returns>Список значений указанной колонки.</returns>
        /// <exception cref="ArgumentNullException">Выбрасывается, если <paramref name="table" /> равен <see langword="null" />.</exception>
        /// <exception cref="ArgumentException">Выбрасывается, если колонка не найдена.</exception>
        /// <remarks>Строки со значением <see cref="DBNull.Value" />
        /// пропускаются.</remarks>
        public static List<string> ToList(this DataTable table, string columnName)
            => DataTableHelper.ToList<string>(table, columnName, null);

        /// <summary>
        /// Преобразует строки таблицы данных в список объектов
        /// заданного типа.
        /// </summary>
        /// <typeparam name="T">Тип создаваемых объектов.</typeparam>
        /// <param name="table">Исходная таблица данных.</param>
        /// <returns>Список объектов, заполненных значениями из таблицы.</returns>
        /// <remarks>Свойства объекта сопоставляются с колонками таблицы
        /// по имени. Значения <see cref="DBNull.Value" /> игнорируются.</remarks>
        public static List<T> ToList<T>(this DataTable table)
            where T : class, new() => DataTableHelper.ToList<T>(table);

        /// <summary>
        /// Проверяет добавлена ли строка в таблицу.
        /// </summary>
        /// <param name="dt">Таблица.</param>
        /// <param name="row">Строка.</param>
        /// <returns><c>true</c> if the specified row contains row; otherwise, <c>false</c>.</returns>
        public static bool ContainsRow(this DataTable dt, object row) => DataTableHelper.ContainsRow(dt, row);
    }
}