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
namespace System.Data
{
    using System;
    using System.Collections.Generic;
    using System.Helpers;

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

        /// <summary>
        /// Преобразует объект <see cref="DataTable"/> в строковое представление в формате CSV.
        /// </summary>
        /// <param name="data">
        /// Таблица <see cref="DataTable"/>, содержащая данные для экспорта.
        /// </param>
        /// <param name="writeColumnHeaders">
        /// Флаг, указывающий, нужно ли записывать строку заголовков столбцов в начало результата.
        /// </param>
        /// <param name="columnSeparator">
        /// Разделитель столбцов. По умолчанию используется запятая (<c>,</c>).
        /// </param>
        /// <param name="lineSeparator">
        /// Разделитель строк. По умолчанию используется <c>";\r\n"</c>.
        /// </param>
        /// <param name="valueSerializer">
        /// Пользовательская функция сериализации значений ячеек.
        /// Первый параметр — имя столбца, второй — значение ячейки.
        /// Функция должна вернуть строковое представление значения.
        /// Если параметр равен <c>null</c>, используется стандартное преобразование через <see cref="Convert.ToString(object)"/>.
        /// </param>
        /// <param name="columnNames">
        /// Необязательный список имен столбцов для экспорта.
        /// Если указаны, в результат будут включены только эти столбцы и в заданном порядке.
        /// Если список пуст, экспортируются все столбцы таблицы в их исходном порядке.
        /// </param>
        /// <returns>
        /// Строка, содержащая данные таблицы в формате CSV.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Выбрасывается, если параметр <paramref name="data"/> равен <c>null</c>.
        /// </exception>
        public static string ToCsv(this DataTable data, bool writeColumnHeaders = true, string columnSeparator = ",", string lineSeparator = ";\r\n", Func<string, object, string> valueSerializer = null, params string[] columnNames)
            => CsvHelper.ToCsv(data, writeColumnHeaders, columnSeparator, lineSeparator, valueSerializer, columnNames);
    }
}