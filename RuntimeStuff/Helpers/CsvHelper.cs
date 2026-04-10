// <copyright file="CsvHelper.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>
namespace System.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Globalization;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Text;

    /// <summary>
    /// Помощник для работы с CSV-данными.
    /// </summary>
    public static class CsvHelper
    {
        /// <summary>
        /// Преобразует CSV-строку в массив объектов указанного класса с возможностью настройки разделителей и парсера значений.
        /// </summary>
        /// <typeparam name="T">Тип объектов для создания. Должен быть классом с публичным конструктором без параметров.</typeparam>
        /// <param name="csv">CSV-строка для обработки.</param>
        /// <param name="hasColumnsHeader">
        /// <c>true</c>, если первая строка CSV содержит заголовки колонок, иначе <c>false. Если null, то определяем автоматически: есть ли в первой строке хоть одно имя совпадающее со простыми публичными свойствами класса</c>.
        /// </param>
        /// <param name="columnSeparators">Массив строк-разделителей колонок. По умолчанию { ";" }.</param>
        /// <param name="lineSeparators">Массив строк-разделителей строк. По умолчанию { "\r", "\n", Environment.NewLine }.</param>
        /// <param name="valueParser">
        /// Функция для преобразования текстового значения колонки в объект. По умолчанию возвращает строку без изменений.
        /// </param>
        /// <returns>Массив объектов <typeparamref name="T"/>, созданных из CSV-данных.</returns>
        /// <remarks>
        /// <para>Метод выполняет следующие шаги:</para>
        /// <list type="bullet">
        /// <item>Разбивает CSV по строкам с учётом <paramref name="lineSeparators"/> и игнорирует пустые строки.</item>
        /// <item>Если <paramref name="hasColumnsHeader"/> равен <c>true</c>, первая строка используется для сопоставления колонок с членами класса <typeparamref name="T"/> через <see cref="MemberCache"/>.</item>
        /// <item>Каждая последующая строка создаёт новый объект <typeparamref name="T"/>. Значения колонок преобразуются с помощью <paramref name="valueParser"/> и присваиваются соответствующим свойствам или полям.</item>
        /// <item>Если <paramref name="hasColumnsHeader"/> равен <c>false</c>, используются все публичные базовые свойства класса.</item>
        /// </list>
        /// <para>Количество колонок в строке может быть меньше или больше, чем количество свойств: лишние значения игнорируются, недостающие остаются без изменений.</para>
        /// <para>COPY-PASTE-READY: не зависит от других классов или библиотек.</para>
        /// </remarks>
        public static T[] FromCsv<T>(string csv, bool? hasColumnsHeader = null, string[] columnSeparators = null, string[] lineSeparators = null, Func<string, object> valueParser = null)
            where T : class, new()
        {
            return FromCsv<T>(csv, Array.Empty<string>(), hasColumnsHeader, columnSeparators, lineSeparators, valueParser);
        }

        /// <summary>
        /// Преобразует CSV-строку в массив объектов указанного класса с возможностью настройки разделителей и парсера значений.
        /// </summary>
        /// <typeparam name="T">Тип объектов для создания. Должен быть классом с публичным конструктором без параметров.</typeparam>
        /// <param name="csv">CSV-строка для обработки.</param>
        /// <param name="objectProperties">Маппер колонок из csv на свойства объекта в порядке следования колонок в csv.</param>
        /// <param name="hasColumnsHeader">
        /// <c>true</c>, если первая строка CSV содержит заголовки колонок, иначе <c>false. Если null, то определяем автоматически: есть ли в первой строке хоть одно имя совпадающее со простыми публичными свойствами класса</c>.
        /// </param>
        /// <param name="columnSeparators">Массив строк-разделителей колонок. По умолчанию { ";" }.</param>
        /// <param name="lineSeparators">Массив строк-разделителей строк. По умолчанию { "\r", "\n", Environment.NewLine }.</param>
        /// <param name="valueParser">
        /// Функция для преобразования текстового значения колонки в объект. По умолчанию возвращает строку без изменений.
        /// </param>
        /// <returns>Массив объектов <typeparamref name="T"/>, созданных из CSV-данных.</returns>
        /// <remarks>
        /// <para>Метод выполняет следующие шаги:</para>
        /// <list type="bullet">
        /// <item>Разбивает CSV по строкам с учётом <paramref name="lineSeparators"/> и игнорирует пустые строки.</item>
        /// <item>Если <paramref name="hasColumnsHeader"/> равен <c>true</c>, первая строка используется для сопоставления колонок с членами класса <typeparamref name="T"/> через <see cref="MemberCache"/>.</item>
        /// <item>Каждая последующая строка создаёт новый объект <typeparamref name="T"/>. Значения колонок преобразуются с помощью <paramref name="valueParser"/> и присваиваются соответствующим свойствам или полям.</item>
        /// <item>Если <paramref name="hasColumnsHeader"/> равен <c>false</c>, используются все публичные базовые свойства класса.</item>
        /// </list>
        /// <para>Количество колонок в строке может быть меньше или больше, чем количество свойств: лишние значения игнорируются, недостающие остаются без изменений.</para>
        /// </remarks>
        public static T[] FromCsv<T>(string csv, string[] objectProperties, bool? hasColumnsHeader = null, string[] columnSeparators = null, string[] lineSeparators = null, Func<string, object> valueParser = null)
            where T : class, new()
        {
            var typeCache = MemberCache.Get(typeof(T));
            var properties = objectProperties != null ? objectProperties.Select(x => typeCache[x].AsPropertyInfo()).ToArray() : Array.Empty<PropertyInfo>();
            return FromCsv<T>(csv, properties, hasColumnsHeader, columnSeparators, lineSeparators, valueParser);
        }

        /// <summary>
        /// Преобразует CSV-строку в массив объектов указанного класса с возможностью настройки разделителей и парсера значений.
        /// </summary>
        /// <typeparam name="T">Тип объектов для создания. Должен быть классом с публичным конструктором без параметров.</typeparam>
        /// <param name="csv">CSV-строка для обработки.</param>
        /// <param name="objectProperties">Маппер колонок из csv на свойства объекта в порядке следования колонок в csv.</param>
        /// <param name="hasColumnsHeader">
        /// <c>true</c>, если первая строка CSV содержит заголовки колонок, иначе <c>false. Если null, то определяем автоматически: есть ли в первой строке хоть одно имя совпадающее со простыми публичными свойствами класса</c>.
        /// </param>
        /// <param name="columnSeparator">Строка-разделителей колонок. По умолчанию { ";" }.</param>
        /// <param name="lineSeparator">Строка-разделителей строк. По умолчанию { "\r", "\n", Environment.NewLine }.</param>
        /// <param name="valueParser">
        /// Функция для преобразования текстового значения колонки в объект. По умолчанию возвращает строку без изменений.
        /// </param>
        /// <returns>Массив объектов <typeparamref name="T"/>, созданных из CSV-данных.</returns>
        /// <remarks>
        /// <para>Метод выполняет следующие шаги:</para>
        /// <list type="bullet">
        /// <item>Разбивает CSV по строкам с учётом <paramref name="lineSeparator"/> и игнорирует пустые строки.</item>
        /// <item>Если <paramref name="hasColumnsHeader"/> равен <c>true</c>, первая строка используется для сопоставления колонок с членами класса <typeparamref name="T"/> через <see cref="MemberCache"/>.</item>
        /// <item>Каждая последующая строка создаёт новый объект <typeparamref name="T"/>. Значения колонок преобразуются с помощью <paramref name="valueParser"/> и присваиваются соответствующим свойствам или полям.</item>
        /// <item>Если <paramref name="hasColumnsHeader"/> равен <c>false</c>, используются все публичные базовые свойства класса.</item>
        /// </list>
        /// <para>Количество колонок в строке может быть меньше или больше, чем количество свойств: лишние значения игнорируются, недостающие остаются без изменений.</para>
        /// </remarks>
        public static T[] FromCsv<T>(string csv, string[] objectProperties, bool hasColumnsHeader, string columnSeparator, string lineSeparator = null, Func<string, object> valueParser = null)
            where T : class, new()
        {
            return FromCsv<T>(csv, objectProperties, hasColumnsHeader, columnSeparator == null ? null : [columnSeparator], lineSeparator == null ? null : [lineSeparator], valueParser);
        }

        /// <summary>
        /// Преобразует CSV-строку в массив объектов указанного класса с возможностью настройки разделителей и парсера значений.
        /// </summary>
        /// <typeparam name="T">Тип объектов для создания. Должен быть классом с публичным конструктором без параметров.</typeparam>
        /// <param name="csv">CSV-строка для обработки.</param>
        /// <param name="objectProperties">Маппер колонок из csv на свойства объекта в порядке следования колонок в csv.</param>
        /// <param name="hasColumnsHeader">
        /// <c>true</c>, если первая строка CSV содержит заголовки колонок, иначе <c>false. Если null, то определяем автоматически: есть ли в первой строке хоть одно имя совпадающее со простыми публичными свойствами класса</c>.
        /// </param>
        /// <param name="columnSeparators">Массив строк-разделителей колонок. По умолчанию { ";" }.</param>
        /// <param name="lineSeparators">Массив строк-разделителей строк. По умолчанию { "\r", "\n", Environment.NewLine }.</param>
        /// <param name="valueParser">
        /// Функция для преобразования текстового значения колонки в объект. По умолчанию возвращает строку без изменений.
        /// </param>
        /// <returns>Массив объектов <typeparamref name="T"/>, созданных из CSV-данных.</returns>
        /// <remarks>
        /// <para>Метод выполняет следующие шаги:</para>
        /// <list type="bullet">
        /// <item>Разбивает CSV по строкам с учётом <paramref name="lineSeparators"/> и игнорирует пустые строки.</item>
        /// <item>Если <paramref name="hasColumnsHeader"/> равен <c>true</c>, первая строка используется для сопоставления колонок с членами класса <typeparamref name="T"/> через <see cref="MemberCache"/>.</item>
        /// <item>Каждая последующая строка создаёт новый объект <typeparamref name="T"/>. Значения колонок преобразуются с помощью <paramref name="valueParser"/> и присваиваются соответствующим свойствам или полям.</item>
        /// <item>Если <paramref name="hasColumnsHeader"/> равен <c>false</c>, используются все публичные базовые свойства класса.</item>
        /// </list>
        /// <para>Количество колонок в строке может быть меньше или больше, чем количество свойств: лишние значения игнорируются, недостающие остаются без изменений.</para>
        /// </remarks>
        public static T[] FromCsv<T>(string csv, PropertyInfo[] objectProperties, bool? hasColumnsHeader = null, string[] columnSeparators = null, string[] lineSeparators = null, Func<string, object> valueParser = null)
            where T : class, new()
        {
            if (string.IsNullOrWhiteSpace(csv))
            {
                return Array.Empty<T>();
            }

            if (columnSeparators == null)
            {
                columnSeparators = [";"];
            }

            if (lineSeparators == null)
            {
                lineSeparators = ["\r", "\n", Environment.NewLine];
            }

            if (valueParser == null)
            {
                valueParser = s => s;
            }

            var lines = StringHelper.SplitBy(csv, StringSplitOptions.RemoveEmptyEntries, lineSeparators);
            if (lines.Length == 0)
            {
                return Array.Empty<T>();
            }

            var typeCache = MemberCache.Get(typeof(T));

            if (hasColumnsHeader == null)
            {
                hasColumnsHeader = StringHelper.SplitBy(lines[0], StringSplitOptions.None, columnSeparators).Any(x => typeCache[x.Replace(" ", string.Empty)] != null);
            }

            MemberCache[] columnNames;
            if (hasColumnsHeader.Value)
            {
                columnNames = StringHelper.SplitBy(lines[0], StringSplitOptions.None, columnSeparators).Select(x => typeCache[x.Replace(" ", string.Empty)]).ToArray();
            }
            else
            {
                columnNames = objectProperties.Length > 0 ? objectProperties.Select(x => (MemberCache)x).ToArray() : typeCache.PublicBasicProperties.ToArray();
            }

            var result = new List<T>();
            for (var i = hasColumnsHeader.Value ? 1 : 0; i < lines.Length; i++)
            {
                var values = StringHelper.SplitBy(lines[i], StringSplitOptions.None, columnSeparators, ("\"", "\""))
                    .Select(x => valueParser(x)).ToArray();
                var obj = new T();
                var columnCount = Math.Min(columnNames.Length, values.Length);
                for (var j = 0; j < columnCount; j++)
                {
                    if (j >= values.Length || columnNames[j] == null)
                    {
                        continue;
                    }

                    if (string.IsNullOrEmpty($"{values[j]}"))
                    {
                        continue;
                    }

                    if (StringHelper.ContainsAny($"{values[j]}", StringComparison.Ordinal, columnSeparators))
                    {
                        values[j] = $"{values[j]}".Trim('"');
                    }

                    columnNames[j].SetValue(obj, values[j]);
                }

                result.Add(obj);
            }

            return result.ToArray();
        }

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
        public static string ToCsv(DataTable data, bool writeColumnHeaders = true, string columnSeparator = ",", string lineSeparator = ";\r\n", Func<string, object, string> valueSerializer = null, params string[] columnNames)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (string.IsNullOrEmpty(columnSeparator))
            {
                throw new ArgumentException(@"Column separator cannot be null or empty.", nameof(columnSeparator));
            }

            if (lineSeparator == null)
            {
                throw new ArgumentNullException(nameof(lineSeparator));
            }

            var sb = new StringBuilder();

            // Определяем набор колонок
            var columns = (columnNames != null && columnNames.Length > 0)
                ? columnNames
                : data.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToArray();

            // Проверка существования колонок
            foreach (var columnName in columns)
            {
                if (!data.Columns.Contains(columnName))
                {
                    throw new ArgumentException($"Column '{columnName}' does not exist in DataTable.");
                }
            }

            // Запись заголовков
            if (writeColumnHeaders)
            {
                sb.Append(string.Join(
                    columnSeparator,
                    columns.Select(x => EscapeCsv(x, columnSeparator))));
                sb.Append(lineSeparator);
            }

            // Запись строк
            foreach (DataRow row in data.Rows)
            {
                var values = columns.Select(columnName =>
                {
                    var rawValue = row[columnName];

                    if (rawValue == DBNull.Value)
                    {
                        return string.Empty;
                    }

                    var serialized = valueSerializer != null
                        ? valueSerializer(columnName, rawValue)
                        : DefaultSerialize(rawValue);

                    return EscapeCsv(serialized, columnSeparator);
                });

                sb.Append(string.Join(columnSeparator, values));
                sb.Append(lineSeparator);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Преобразует коллекцию объектов в строку в формате CSV,
        /// позволяя указать набор колонок с помощью лямбда-выражений.
        /// </summary>
        /// <typeparam name="T">
        /// Тип элементов коллекции.
        /// </typeparam>
        /// <param name="data">
        /// Коллекция объектов, данные которых будут сериализованы в CSV.
        /// </param>
        /// <param name="writeColumnHeaders">
        /// Признак необходимости записи строки заголовков.
        /// Если значение равно <see langword="true"/>, в первую строку CSV
        /// будут записаны имена выбранных свойств.
        /// </param>
        /// <param name="columnSeparator">
        /// Разделитель колонок (по умолчанию <c>","</c>).
        /// </param>
        /// <param name="lineSeparator">
        /// Разделитель строк (по умолчанию <c>";\r\n"</c>).
        /// </param>
        /// <param name="valueSerializer">
        /// Пользовательская функция сериализации значения свойства в строку.
        /// Принимает описание свойства и его значение.
        /// Если не задана, используется стандартная сериализация.
        /// </param>
        /// <returns>
        /// Строка, содержащая данные в формате CSV.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Выбрасывается, если параметр <paramref name="data"/> равен <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Выбрасывается, если выражение не указывает на свойство типа <typeparamref name="T"/>.
        /// </exception>
        public static string ToCsv<T>(IEnumerable<T> data, bool writeColumnHeaders = true, string columnSeparator = ",", string lineSeparator = ";\r\n", Func<PropertyInfo, object, string> valueSerializer = null)
        {
            return ToCsv<T>(data, Array.Empty<PropertyInfo>(), writeColumnHeaders, columnSeparator, lineSeparator, valueSerializer);
        }

        /// <summary>
        /// Преобразует коллекцию объектов в строку в формате CSV,
        /// позволяя указать набор колонок с помощью лямбда-выражений.
        /// </summary>
        /// <typeparam name="T">
        /// Тип элементов коллекции.
        /// </typeparam>
        /// <param name="data">
        /// Коллекция объектов, данные которых будут сериализованы в CSV.
        /// </param>
        /// <param name="writeColumnHeaders">
        /// Признак необходимости записи строки заголовков.
        /// Если значение равно <see langword="true"/>, в первую строку CSV
        /// будут записаны имена выбранных свойств.
        /// </param>
        /// <param name="columnSeparator">
        /// Разделитель колонок (по умолчанию <c>","</c>).
        /// </param>
        /// <param name="lineSeparator">
        /// Разделитель строк (по умолчанию <c>";\r\n"</c>).
        /// </param>
        /// <param name="valueSerializer">
        /// Пользовательская функция сериализации значения свойства в строку.
        /// Принимает описание свойства и его значение.
        /// Если не задана, используется стандартная сериализация.
        /// </param>
        /// <param name="columnSelectors">
        /// Выражения, указывающие свойства типа <typeparamref name="T"/>,
        /// которые необходимо включить в CSV (например: <c>x =&gt; x.Name</c>).
        /// Если массив не задан или пуст, используются все публичные простые свойства типа.
        /// </param>
        /// <returns>
        /// Строка, содержащая данные в формате CSV.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Выбрасывается, если параметр <paramref name="data"/> равен <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Выбрасывается, если выражение не указывает на свойство типа <typeparamref name="T"/>.
        /// </exception>
        public static string ToCsv<T>(IEnumerable<T> data, bool writeColumnHeaders = true, string columnSeparator = ",", string lineSeparator = ";\r\n", Func<PropertyInfo, object, string> valueSerializer = null, params Expression<Func<T, object>>[] columnSelectors)
        {
            return ToCsv(data, columnSelectors.Select(ExpressionHelper.GetPropertyInfo).ToArray(), writeColumnHeaders, columnSeparator, lineSeparator, valueSerializer);
        }

        /// <summary>
        /// Преобразует коллекцию объектов в строку в формате CSV,
        /// позволяя указать набор колонок по их именам.
        /// </summary>
        /// <typeparam name="T">
        /// Тип элементов коллекции.
        /// </typeparam>
        /// <param name="data">
        /// Коллекция объектов, данные которых будут сериализованы в CSV.
        /// </param>
        /// <param name="columns">
        /// Имена свойств типа <typeparamref name="T"/>, которые необходимо включить в CSV.
        /// Если массив не задан или пуст, используются все публичные простые свойства типа.
        /// Имена свойств, не найденные в типе, игнорируются.
        /// </param>
        /// <param name="writeColumnHeaders">
        /// Признак необходимости записи строки заголовков.
        /// Если значение равно <see langword="true"/>, в первую строку CSV
        /// будут записаны имена выбранных свойств.
        /// </param>
        /// <param name="columnSeparator">
        /// Разделитель колонок (по умолчанию <c>","</c>).
        /// </param>
        /// <param name="lineSeparator">
        /// Разделитель строк (по умолчанию <c>";\r\n"</c>).
        /// </param>
        /// <param name="valueSerializer">
        /// Пользовательская функция сериализации значения свойства в строку.
        /// Принимает описание свойства и его значение.
        /// Если не задана, используется стандартная сериализация.
        /// </param>
        /// <returns>
        /// Строка, содержащая данные в формате CSV.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Выбрасывается, если параметр <paramref name="data"/> равен <see langword="null"/>.
        /// </exception>
        public static string ToCsv<T>(IEnumerable<T> data, string[] columns, bool writeColumnHeaders = true, string columnSeparator = ",", string lineSeparator = ";\r\n", Func<PropertyInfo, object, string> valueSerializer = null)
        {
            var typeCache = MemberCache.Get(typeof(T));
            MemberCache[] props = null;
            props = columns.Length > 0 ? typeCache.PublicBasicProperties.ToArray() : columns.Select(c => typeCache[c]).Where(m => m != null).ToArray();

            return ToCsv(
                data,
                props.Select(x => (PropertyInfo)x).ToArray(),
                writeColumnHeaders,
                columnSeparator,
                lineSeparator,
                valueSerializer);
        }

        /// <summary>
        /// Преобразует коллекцию объектов в строку в формате CSV.
        /// </summary>
        /// <typeparam name="T">
        /// Тип элементов коллекции.
        /// </typeparam>
        /// <param name="data">
        /// Коллекция объектов, данные которых будут сериализованы в CSV.
        /// </param>
        /// <param name="columns">
        /// Набор свойств, которые необходимо включить в CSV.
        /// Если параметры не заданы, используются все публичные простые свойства типа <typeparamref name="T"/>.
        /// </param>
        /// <param name="writeColumnHeaders">
        /// Признак необходимости записи строки заголовков.
        /// Если значение равно <see langword="true"/>, в первую строку CSV
        /// будут записаны имена свойств.
        /// </param>
        /// <param name="columnSeparator">
        /// Разделитель колонок (по умолчанию <c>","</c>).
        /// </param>
        /// <param name="lineSeparator">
        /// Разделитель строк (по умолчанию <c>";\r\n"</c>).
        /// </param>
        /// <param name="valueSerializer">
        /// Пользовательская функция сериализации значения свойства в строку.
        /// Принимает описание свойства и его значение.
        /// Если не задана, используется стандартное преобразование через
        /// <see cref="CultureInfo.InvariantCulture"/>.
        /// </param>
        /// <returns>
        /// Строка, содержащая данные в формате CSV.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Выбрасывается, если параметр <paramref name="data"/> равен <see langword="null"/>.
        /// </exception>
        public static string ToCsv<T>(IEnumerable<T> data, PropertyInfo[] columns, bool writeColumnHeaders = true, string columnSeparator = ",", string lineSeparator = ";\r\n", Func<PropertyInfo, object, string> valueSerializer = null)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var sb = new StringBuilder();
            var typeCache = MemberCache.Get(typeof(T));

            if (columns == null || columns.Length == 0)
            {
                columns = typeCache.PublicBasicProperties.Select(x => (PropertyInfo)x)
                    .ToArray();
            }

            if (valueSerializer == null)
            {
                valueSerializer = (member, value) =>
                    DefaultSerialize(value);
            }

            if (writeColumnHeaders)
            {
                WriteLine(sb, columns.Select(c => c.Name), columnSeparator);
                sb.Append(lineSeparator);
            }

            foreach (var item in data)
            {
                WriteLine(
                    sb,
                    columns.Select(c => EscapeCsv(valueSerializer(c, c.GetValue(item)), columnSeparator)),
                    columnSeparator);

                sb.Append(lineSeparator);
            }

            return sb.ToString();
        }

        private static void WriteLine(StringBuilder sb, IEnumerable<string> values, string separator)
        {
            var first = true;

            foreach (var value in values)
            {
                if (!first)
                {
                    sb.Append(separator);
                }

                sb.Append(value);
                first = false;
            }
        }

        private static string EscapeCsv(string value, string separator)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var mustQuote =
                value.Contains('"') ||
                value.Contains(separator) ||
                value.Contains('\r') ||
                value.Contains('\n');

            if (!mustQuote)
            {
                return value;
            }

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static string DefaultSerialize(object value)
        {
            return value switch
            {
                null => string.Empty,
                DateTime dt => dt.TimeOfDay != TimeSpan.Zero ? dt.ToString("yyyy-MM-dd HH:mm:ss") : dt.ToString("yyyy-MM-dd"),
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString(),
            };
        }
    }
}