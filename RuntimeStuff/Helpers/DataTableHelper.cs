using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using RuntimeStuff;
using RuntimeStuff.Helpers;

/// <summary>
/// Предоставляет вспомогательные методы для работы с
/// <see cref="DataTable"/>, включая добавление колонок и строк,
/// а также преобразование данных в коллекции объектов.
/// </summary>
/// <remarks>
/// Класс предназначен для упрощения типовых операций с
/// <see cref="DataTable"/> в сценариях сериализации,
/// загрузки данных и преобразования табличных структур
/// в объектные модели.
/// </remarks>
public static class DataTableHelper
{
    /// <summary>
    /// Добавляет колонку в таблицу данных.
    /// </summary>
    /// <param name="table">
    /// Таблица, в которую добавляется колонка.
    /// </param>
    /// <param name="columnName">
    /// Имя добавляемой колонки.
    /// </param>
    /// <param name="columnType">
    /// Тип данных колонки.
    /// </param>
    /// <param name="isPrimaryKey">
    /// Указывает, должна ли колонка быть частью первичного ключа.
    /// </param>
    /// <returns>
    /// Созданный экземпляр <see cref="DataColumn"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Выбрасывается, если <paramref name="table"/> или
    /// <paramref name="columnType"/> равны <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Выбрасывается, если имя колонки пустое
    /// или колонка с таким именем уже существует.
    /// </exception>
    /// <remarks>
    /// Если колонка помечена как первичный ключ,
    /// она автоматически добавляется в массив
    /// <see cref="DataTable.PrimaryKey"/>.
    /// </remarks>
    public static DataColumn AddCol(
        DataTable table,
        string columnName,
        Type columnType,
        bool isPrimaryKey = false)
    {
        if (table == null) throw new ArgumentNullException(nameof(table));
        if (string.IsNullOrWhiteSpace(columnName))
            throw new ArgumentException(@"Column name is required", nameof(columnName));
        if (columnType == null) throw new ArgumentNullException(nameof(columnType));

        if (table.Columns.Contains(columnName))
            throw new ArgumentException($"Column '{columnName}' already exists");

        var column = new DataColumn(columnName, columnType)
        {
            AllowDBNull = !isPrimaryKey
        };

        table.Columns.Add(column);

        if (!isPrimaryKey) return column;

        var keys = table.PrimaryKey.ToList();
        keys.Add(column);
        table.PrimaryKey = keys.ToArray();

        return column;
    }

    /// <summary>
    /// Добавляет строку в таблицу данных из массива значений.
    /// </summary>
    /// <param name="table">
    /// Таблица, в которую добавляется строка.
    /// </param>
    /// <param name="rowData">
    /// Массив значений строки, соответствующий порядку колонок таблицы.
    /// </param>
    /// <returns>
    /// Добавленная строка <see cref="DataRow"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Выбрасывается, если <paramref name="table"/> или
    /// <paramref name="rowData"/> равны <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Выбрасывается, если количество элементов в массиве
    /// не совпадает с количеством колонок таблицы.
    /// </exception>
    /// <remarks>
    /// Значения <see langword="null"/> автоматически преобразуются
    /// в <see cref="DBNull.Value"/>.
    /// </remarks>
    public static DataRow AddRow(DataTable table, object[] rowData)
    {
        if (table == null) throw new ArgumentNullException(nameof(table));
        if (rowData == null) throw new ArgumentNullException(nameof(rowData));

        if (rowData.Length != table.Columns.Count)
            throw new ArgumentException(
                "Row data length does not match table columns count");

        var row = table.NewRow();

        for (int i = 0; i < rowData.Length; i++)
            row[i] = rowData[i] ?? DBNull.Value;

        table.Rows.Add(row);
        return row;
    }

    /// <summary>
    /// Добавляет строку в таблицу данных на основе свойств объекта.
    /// </summary>
    /// <typeparam name="T">
    /// Тип объекта, значения свойств которого используются
    /// для заполнения строки.
    /// </typeparam>
    /// <param name="table">
    /// Таблица, в которую добавляется строка.
    /// </param>
    /// <param name="item">
    /// Объект-источник значений.
    /// </param>
    /// <returns>
    /// Добавленная строка <see cref="DataRow"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Выбрасывается, если <paramref name="table"/> или
    /// <paramref name="item"/> равны <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// Значения берутся из свойств объекта по имени,
    /// совпадающему с именем колонки таблицы.
    /// </remarks>
    public static DataRow AddRow<T>(DataTable table, T item)
    {
        if (table == null) throw new ArgumentNullException(nameof(table));
        if (item == null) throw new ArgumentNullException(nameof(item));

        var row = table.NewRow();

        foreach (DataColumn col in table.Columns)
            row[col] = Obj.Get(item, col.ColumnName, col.DataType) ?? DBNull.Value;

        table.Rows.Add(row);
        return row;
    }

    /// <summary>
    /// Преобразует значения указанной колонки таблицы
    /// в список заданного типа.
    /// </summary>
    /// <typeparam name="T">
    /// Тип элементов результирующего списка.
    /// </typeparam>
    /// <param name="table">
    /// Исходная таблица данных.
    /// </param>
    /// <param name="columnName">
    /// Имя колонки, значения которой будут извлечены.
    /// </param>
    /// <returns>
    /// Список значений указанной колонки.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Выбрасывается, если <paramref name="table"/> равен <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Выбрасывается, если колонка не найдена.
    /// </exception>
    /// <remarks>
    /// Строки со значением <see cref="DBNull.Value"/>
    /// пропускаются.
    /// </remarks>
    public static List<T> ToList<T>(DataTable table, string columnName)
    {
        if (table == null) throw new ArgumentNullException(nameof(table));
        if (string.IsNullOrWhiteSpace(columnName))
            throw new ArgumentException(nameof(columnName));

        if (!table.Columns.Contains(columnName))
            throw new ArgumentException($"Column '{columnName}' not found");

        var result = new List<T>(table.Rows.Count);

        foreach (DataRow row in table.Rows)
        {
            if (row[columnName] == DBNull.Value)
                continue;

            result.Add(Obj.ChangeType<T>(row[columnName]));
        }

        return result;
    }

    /// <summary>
    /// Преобразует строки таблицы данных в список объектов
    /// заданного типа.
    /// </summary>
    /// <typeparam name="T">
    /// Тип создаваемых объектов.
    /// </typeparam>
    /// <param name="table">
    /// Исходная таблица данных.
    /// </param>
    /// <returns>
    /// Список объектов, заполненных значениями из таблицы.
    /// </returns>
    /// <remarks>
    /// Свойства объекта сопоставляются с колонками таблицы
    /// по имени. Значения <see cref="DBNull.Value"/> игнорируются.
    /// </remarks>
    public static List<T> ToList<T>(DataTable table) where T : class, new()
    {
        if (table == null) throw new ArgumentNullException(nameof(table));

        var result = new List<T>(table.Rows.Count);
        var props = MemberCache.Create(typeof(T)).Properties;

        foreach (DataRow row in table.Rows)
        {
            var item = new T();

            foreach (DataColumn col in table.Columns)
            {
                if (!props.TryGetValue(col.ColumnName, out var prop))
                    continue;

                var value = row[col];
                if (value == DBNull.Value)
                    continue;

                prop.SetValue(item, Obj.ChangeType(value, prop.PropertyType));
            }

            result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Преобразует коллекцию объектов указанного типа в таблицу данных, где каждая строка соответствует одному элементу
    /// коллекции, а столбцы — публичным свойствам типа.
    /// </summary>
    /// <remarks>Каждое публичное свойство типа <typeparamref name="T"/> становится отдельным столбцом
    /// таблицы. Значения свойств, равные null, записываются как <see cref="DBNull.Value"/>. Название таблицы
    /// соответствует имени типа <typeparamref name="T"/>.</remarks>
    /// <typeparam name="T">Тип объектов, элементы которых будут представлены в таблице. Должен быть ссылочным типом.</typeparam>
    /// <param name="list">Коллекция объектов, которые необходимо преобразовать в таблицу данных. Не может быть равна null.</param>
    /// <param name="tableName">Имя таблицы, если не указано, то берется имя класса</param>
    /// <param name="propertySelectors">Выбор свойств, которые добавить в таблицу, если не указаны, то все публичные свойства</param>
    /// <returns>Экземпляр <see cref="DataTable"/>, содержащий данные из коллекции. Если коллекция пуста, возвращается таблица
    /// только с определёнными столбцами.</returns>
    /// <exception cref="ArgumentNullException">Возникает, если параметр <paramref name="list"/> равен null.</exception>
    public static DataTable ToDataTable<T>(IEnumerable<T> list, string tableName = null, params Expression<Func<T, object>>[] propertySelectors) where T : class
    {
        return  ToDataTable<T>(list, tableName, propertySelectors.Select(x=>(x, (string)null)).ToArray());
    }

    /// <summary>
    /// Проверяет добавлена ли строка в таблицу
    /// </summary>
    /// <param name="dt">Таблица</param>
    /// <param name="row">Строка</param>
    /// <returns></returns>
    public static bool ContainsRow(DataTable dt, object row)
    {
        var dr = row as DataRow ?? (row as DataRowView)?.Row;
        return dr != null && dr.Table == dt && dr.RowState != DataRowState.Detached;
    }

    /// <summary>
    /// Преобразует коллекцию объектов указанного типа в таблицу данных, где каждая строка соответствует одному элементу
    /// коллекции, а столбцы — публичным свойствам типа.
    /// </summary>
    /// <remarks>Каждое публичное свойство типа <typeparamref name="T"/> становится отдельным столбцом
    /// таблицы. Значения свойств, равные null, записываются как <see cref="DBNull.Value"/>. Название таблицы
    /// соответствует имени типа <typeparamref name="T"/>.</remarks>
    /// <typeparam name="T">Тип объектов, элементы которых будут представлены в таблице. Должен быть ссылочным типом.</typeparam>
    /// <param name="list">Коллекция объектов, которые необходимо преобразовать в таблицу данных. Не может быть равна null.</param>
    /// <param name="tableName">Имя таблицы, если не указано, то берется имя класса</param>
    /// <param name="propertySelectors">Выбор свойств, которые добавить в таблицу, если не указаны, то все публичные свойства</param>
    /// <returns>Экземпляр <see cref="DataTable"/>, содержащий данные из коллекции. Если коллекция пуста, возвращается таблица
    /// только с определёнными столбцами.</returns>
    /// <exception cref="ArgumentNullException">Возникает, если параметр <paramref name="list"/> равен null.</exception>
    public static DataTable ToDataTable<T>(IEnumerable<T> list, string tableName, params (Expression<Func<T, object>> propSelector, string columnName)[] propertySelectors) where T: class
    {
        if (list == null) throw new ArgumentNullException(nameof(list));
        var table =  new DataTable(tableName ?? typeof(T).Name);
        var props = propertySelectors.Any() ? propertySelectors.Select(x=>(ExpressionHelper.GetMemberCache(x.propSelector), x.columnName)).ToArray() : MemberCache.Create(typeof(T)).Properties.Select(x => (x.Value, x.Value.ColumnName)).ToArray();
        var pks = new List<DataColumn>();
        foreach (var prop in props)
        {
            var colType = Nullable.GetUnderlyingType(prop.Item1.PropertyType) ?? prop.Item1.PropertyType;
            var col = AddCol(table, prop.Item2 ?? prop.Item1.ColumnName, colType);
            if (prop.Item1.IsPrimaryKey)
                pks.Add(table.Columns[prop.Item2 ?? prop.Item1.ColumnName]);
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
}
