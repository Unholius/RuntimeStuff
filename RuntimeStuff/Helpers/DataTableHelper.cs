// ***********************************************************************
// Assembly         : RuntimeStuff
// Author           : RS
// Created          : 01-06-2026
//
// Last Modified By : RS
// Last Modified On : 01-07-2026
// ***********************************************************************
// <copyright file="DataTableHelper.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************
namespace RuntimeStuff.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Linq.Expressions;

    /// <summary>
    /// Предоставляет вспомогательные методы для работы с
    /// <see cref="DataTable" />, включая добавление колонок и строк,
    /// а также преобразование данных в коллекции объектов.
    /// </summary>
    /// <remarks>Класс предназначен для упрощения типовых операций с
    /// <see cref="DataTable" /> в сценариях сериализации,
    /// загрузки данных и преобразования табличных структур
    /// в объектные модели.
    /// <para>COPY-PASTE-READY: не зависит от других классов или библиотек.</para>
    /// </remarks>
    public static class DataTableHelper
    {
        /// <summary>
        /// Добавляет колонку в таблицу данных.
        /// </summary>
        /// <param name="table">Таблица, в которую добавляется колонка.</param>
        /// <param name="columnName">Имя добавляемой колонки.</param>
        /// <param name="columnType">Тип данных колонки.</param>
        /// <param name="isPrimaryKey">Указывает, должна ли колонка быть частью первичного ключа.</param>
        /// <returns>Созданный экземпляр <see cref="DataColumn" />.</returns>
        /// <exception cref="System.ArgumentNullException">table.</exception>
        /// <exception cref="System.ArgumentNullException">columnType.</exception>
        /// <exception cref="System.ArgumentException">Column name is required - columnName.</exception>
        /// <exception cref="System.ArgumentException">Column '{columnName}' already exists.</exception>
        /// <remarks>Если колонка помечена как первичный ключ,
        /// она автоматически добавляется в массив
        /// <see cref="DataTable.PrimaryKey" />.</remarks>
        public static DataColumn AddCol(
            DataTable table,
            string columnName,
            Type columnType = null,
            bool isPrimaryKey = false)
        {
            if (table == null)
            {
                throw new ArgumentNullException(nameof(table));
            }

            if (string.IsNullOrWhiteSpace(columnName))
            {
                throw new ArgumentException(@"Column name is required", nameof(columnName));
            }

            if (columnType == null)
            {
                columnType = typeof(string);
            }

            if (table.Columns.Contains(columnName))
            {
                throw new ArgumentException($"Column '{columnName}' already exists");
            }

            var column = new DataColumn(columnName, columnType)
            {
                AllowDBNull = !isPrimaryKey,
            };

            table.Columns.Add(column);

            if (!isPrimaryKey)
            {
                return column;
            }

            var keys = table.PrimaryKey.ToList();
            keys.Add(column);
            table.PrimaryKey = keys.ToArray();

            return column;
        }

        /// <summary>
        /// Добавляет строку в таблицу данных из массива значений.
        /// </summary>
        /// <typeparam name="T">Тип объекта, значения свойств которого используются
        /// для заполнения строки.</typeparam>
        /// <param name="table">Таблица, в которую добавляется строка.</param>
        /// <param name="rowData">Массив значений строки, соответствующий порядку колонок таблицы.</param>
        /// <returns>Добавленная строка <see cref="DataRow" />.</returns>
        /// <exception cref="System.ArgumentNullException">table.</exception>
        /// <exception cref="System.ArgumentNullException">rowData.</exception>
        /// <exception cref="System.ArgumentException">Row data length does not match table columns count.</exception>
        /// <remarks>Значения <see langword="null" /> автоматически преобразуются
        /// в <see cref="DBNull.Value" />.</remarks>
        public static DataTable AddRow<T>(DataTable table, T rowData)
            where T : class, new()
        {
            return AddRows(table, new[] { rowData });
        }

        /// <summary>
        /// Добавляет строку в таблицу данных из массива значений.
        /// </summary>
        /// <param name="table">Таблица, в которую добавляется строка.</param>
        /// <param name="rowData">Массив значений строки, соответствующий порядку колонок таблицы.</param>
        /// <returns>Добавленная строка <see cref="DataRow" />.</returns>
        /// <exception cref="System.ArgumentNullException">table.</exception>
        /// <exception cref="System.ArgumentNullException">rowData.</exception>
        /// <exception cref="System.ArgumentException">Row data length does not match table columns count.</exception>
        /// <remarks>Значения <see langword="null" /> автоматически преобразуются
        /// в <see cref="DBNull.Value" />.</remarks>
        public static DataRow AddRow(DataTable table, object[] rowData)
        {
            if (table == null)
            {
                throw new ArgumentNullException(nameof(table));
            }

            if (rowData == null)
            {
                throw new ArgumentNullException(nameof(rowData));
            }

            if (rowData.Length != table.Columns.Count)
            {
                throw new ArgumentException(
                    "Row data length does not match table columns count");
            }

            var row = table.NewRow();

            for (var i = 0; i < rowData.Length; i++)
            {
                row[i] = rowData[i] ?? DBNull.Value;
            }

            table.Rows.Add(row);
            return row;
        }

        /// <summary>
        /// Добавляет строку в таблицу данных на основе свойств объекта.
        /// </summary>
        /// <typeparam name="T">Тип объекта, значения свойств которого используются
        /// для заполнения строки.</typeparam>
        /// <param name="table">Таблица, в которую добавляется строка.</param>
        /// <param name="items">Объект-источник значений.</param>
        /// <param name="addMissingColumns">Добавлять колонку, если нет с именем свойства в таблице.</param>
        /// <param name="propertyToColumnMapper">Маппер имени свойства на имя колонки в таблице.</param>
        /// <param name="valueConverter">Конвертер значения свойства в тип колонки.</param>
        /// <returns>Добавленная строка <see cref="DataRow" />.</returns>
        /// <exception cref="System.ArgumentNullException">table.</exception>
        /// <exception cref="System.ArgumentNullException">items.</exception>
        /// <remarks>Значения берутся из свойств объекта по имени,
        /// совпадающему с именем колонки таблицы.</remarks>
        public static DataTable AddRows<T>(DataTable table, IEnumerable<T> items, bool addMissingColumns = true, Dictionary<string, string> propertyToColumnMapper = null, Func<object, Type, object> valueConverter = null)
            where T : class, new()
        {
            if (table == null)
            {
                throw new ArgumentNullException(nameof(table));
            }

            var row = table.NewRow();

            var typeCache = MemberCache.Create(typeof(T));

            if (typeCache.IsBasic)
            {
                foreach (var item in items)
                {
                    row[0] = valueConverter == null ? Convert.ChangeType(item, table.Columns[0].DataType) : valueConverter(item, table.Columns[0].DataType);
                    table.Rows.Add(row);
                }
            }
            else
            {
                var props = typeCache.PublicProperties.Where(x => !x.IsCollection).ToArray();
                var propsMap = new List<(MemberCache Prop, DataColumn Col)>();
                foreach (var prop in props)
                {
                    if (propertyToColumnMapper?.TryGetValue(prop.Name, out var columnName) != true)
                        columnName = prop.ColumnName;
                    DataColumn col = null;
                    if (!table.Columns.Contains(columnName))
                    {
                        if (addMissingColumns)
                        {
                            col = AddCol(table, columnName, prop.PropertyType);
                        }
                        else
                        {
                            continue;
                        }
                    }
                    else
                    {
                        col = table.Columns[columnName];
                    }

                    if (col == null)
                        continue;
                    propsMap.Add((prop, col));
                }

                foreach (var item in items)
                {
                    foreach (var map in propsMap)
                    {
                        var value = valueConverter == null ? Convert.ChangeType(map.Prop.Getter(item), map.Col.DataType) : valueConverter(map.Prop.Getter(item), map.Col.DataType);
                        if (value == null)
                        {
                            continue;
                        }

                        row[map.Col] = value;
                    }

                    table.Rows.Add(row);
                }
            }

            return table;
        }

        /// <summary>
        /// Проверяет добавлена ли строка в таблицу.
        /// </summary>
        /// <param name="dt">Таблица.</param>
        /// <param name="row">Строка.</param>
        /// <returns><c>true</c> if the specified dt contains row; otherwise, <c>false</c>.</returns>
        public static bool ContainsRow(DataTable dt, object row)
        {
            var dr = row as DataRow ?? (row as DataRowView)?.Row;
            return dr != null && dr.Table == dt && dr.RowState != DataRowState.Detached;
        }

        /// <summary>
        /// Преобразует коллекцию объектов указанного типа в таблицу данных, где каждая строка соответствует одному элементу
        /// коллекции, а столбцы — публичным свойствам типа.
        /// </summary>
        /// <typeparam name="T">Тип объектов, элементы которых будут представлены в таблице. Должен быть ссылочным типом.</typeparam>
        /// <param name="list">Коллекция объектов, которые необходимо преобразовать в таблицу данных. Не может быть равна null.</param>
        /// <param name="tableName">Имя таблицы, если не указано, то берется имя класса.</param>
        /// <param name="propertySelectors">Выбор свойств, которые добавить в таблицу, если не указаны, то все публичные свойства.</param>
        /// <returns>Экземпляр <see cref="DataTable" />, содержащий данные из коллекции. Если коллекция пуста, возвращается таблица
        /// только с определёнными столбцами.</returns>
        /// <exception cref="ArgumentNullException">Возникает, если параметр <paramref name="list" /> равен null.</exception>
        /// <remarks>Каждое публичное свойство типа <typeparamref name="T" /> становится отдельным столбцом
        /// таблицы. Значения свойств, равные null, записываются как <see cref="DBNull.Value" />. Название таблицы
        /// соответствует имени типа <typeparamref name="T" />.</remarks>
        public static DataTable ToDataTable<T>(IEnumerable<T> list, string tableName = null, params Expression<Func<T, object>>[] propertySelectors)
            where T : class
        {
            return ToDataTable(list, tableName, propertySelectors.Select(x => (x, (string)null)).ToArray());
        }

        /// <summary>
        /// Преобразует коллекцию объектов указанного типа в таблицу данных, где каждая строка соответствует одному элементу
        /// коллекции, а столбцы — публичным свойствам типа.
        /// </summary>
        /// <typeparam name="T">Тип объектов, элементы которых будут представлены в таблице. Должен быть ссылочным типом.</typeparam>
        /// <param name="list">Коллекция объектов, которые необходимо преобразовать в таблицу данных. Не может быть равна null.</param>
        /// <param name="tableName">Имя таблицы, если не указано, то берется имя класса.</param>
        /// <param name="propertySelectors">Выбор свойств, которые добавить в таблицу, если не указаны, то все публичные свойства.</param>
        /// <returns>Экземпляр <see cref="DataTable" />, содержащий данные из коллекции. Если коллекция пуста, возвращается таблица
        /// только с определёнными столбцами.</returns>
        /// <exception cref="System.ArgumentNullException">list.</exception>
        /// <remarks>Каждое публичное свойство типа <typeparamref name="T" /> становится отдельным столбцом
        /// таблицы. Значения свойств, равные null, записываются как <see cref="DBNull.Value" />. Название таблицы
        /// соответствует имени типа <typeparamref name="T" />.</remarks>
        public static DataTable ToDataTable<T>(IEnumerable<T> list, string tableName, params (Expression<Func<T, object>> propSelector, string columnName)[] propertySelectors)
            where T : class
        {
            if (list == null)
            {
                throw new ArgumentNullException(nameof(list));
            }

            var table = new DataTable(tableName ?? typeof(T).Name);
            var props = propertySelectors.Any() ? propertySelectors.Select(x => (ExpressionHelper.GetMemberCache(x.propSelector), x.columnName)).ToArray() : MemberCache.Create(typeof(T)).Properties.Select(x => (x, x.ColumnName)).ToArray();
            var pks = new List<DataColumn>();
            foreach (var prop in props)
            {
                var colType = Nullable.GetUnderlyingType(prop.Item1.PropertyType) ?? prop.Item1.PropertyType;
                AddCol(table, prop.Item2 ?? prop.Item1.ColumnName, colType);
                if (prop.Item1.IsPrimaryKey)
                {
                    pks.Add(table.Columns[prop.Item2 ?? prop.Item1.ColumnName]);
                }
            }

            table.PrimaryKey = pks.ToArray();
            foreach (var item in list)
            {
                var row = table.NewRow();
                foreach (var prop in props)
                {
                    var value = prop.Item1.GetValue(item);
                    row[prop.Item2 ?? prop.Item1.ColumnName] = value ?? DBNull.Value;
                }

                table.Rows.Add(row);
            }

            return table;
        }

        /// <summary>
        /// Преобразует значения указанной колонки таблицы
        /// в список заданного типа.
        /// </summary>
        /// <typeparam name="T">Тип элементов результирующего списка.</typeparam>
        /// <param name="table">Исходная таблица данных.</param>
        /// <param name="columnName">Имя колонки, значения которой будут извлечены.</param>
        /// <param name="valueConverter">Конвертер значения ячейки из DataColumn в тип {T}. Если не указан, то используется Convert.ChangeType.</param>
        /// <returns>Список значений указанной колонки.</returns>
        /// <exception cref="System.ArgumentNullException">table.</exception>
        /// <exception cref="System.ArgumentException">columnName.</exception>
        /// <exception cref="System.ArgumentException">Column '{columnName}' not found.</exception>
        /// <remarks>Строки со значением <see cref="DBNull.Value" />
        /// пропускаются.</remarks>
        public static List<T> ToList<T>(DataTable table, string columnName, Func<object, T> valueConverter = null)
        {
            if (table == null)
            {
                throw new ArgumentNullException(nameof(table));
            }

            if (string.IsNullOrWhiteSpace(columnName))
            {
                throw new ArgumentException(@"Column name cannot be null or whitespace.", nameof(columnName));
            }

            if (!table.Columns.Contains(columnName))
            {
                throw new ArgumentException($"Column '{columnName}' not found");
            }

            var result = new List<T>(table.Rows.Count);
            var toType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

            foreach (DataRow row in table.Rows)
            {
                var value = row[columnName];
                if (value == DBNull.Value)
                    continue;

                result.Add(valueConverter != null ? valueConverter(value) : (T)Convert.ChangeType(value, toType));
            }

            return result;
        }

        /// <summary>
        /// Преобразует строки таблицы данных в список объектов
        /// заданного типа.
        /// </summary>
        /// <typeparam name="T">Тип создаваемых объектов.</typeparam>
        /// <param name="table">Исходная таблица данных.</param>
        /// <param name="columnToPropertyMapper">Сопоставление имен колонок с именами свойств в объекте.</param>
        /// <param name="valueToPropertyTypeConverter">Конвертер значения в тип свойства. Если не указан используется Convert.ChangeType.</param>
        /// <returns>Список объектов, заполненных значениями из таблицы.</returns>
        /// <exception cref="System.ArgumentNullException">table.</exception>
        /// <remarks>Свойства объекта сопоставляются с колонками таблицы
        /// по имени. Значения <see cref="DBNull.Value" /> игнорируются.</remarks>
        public static List<T> ToList<T>(DataTable table, Dictionary<string, string> columnToPropertyMapper = null, Func<object, Type, object> valueToPropertyTypeConverter = null)
            where T : class, new()
        {
            if (table == null)
            {
                throw new ArgumentNullException(nameof(table));
            }

            var result = new List<T>(table.Rows.Count);
            var typeCache = MemberCache.Create(typeof(T));
            var propsMap = new List<(DataColumn Col, MemberCache Prop)>();
            foreach (DataColumn col in table.Columns)
            {
                if (columnToPropertyMapper?.TryGetValue(col.ColumnName, out var propName) != true)
                    propName = col.ColumnName;
                var propCache = typeCache[propName];
                if (propCache == null)
                    continue;
                propsMap.Add((col, propCache));
            }

            foreach (DataRow row in table.Rows)
            {
                var item = new T();

                foreach (var map in propsMap)
                {
                    var value = row[map.Col];
                    if (value == DBNull.Value)
                    {
                        continue;
                    }

                    map.Prop.SetValue(item, valueToPropertyTypeConverter == null ? Convert.ChangeType(value, map.Prop.PropertyType) : valueToPropertyTypeConverter(value, map.Prop.PropertyType));
                }

                result.Add(item);
            }

            return result;
        }
    }
}