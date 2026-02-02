// <copyright file="CsvHelper.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace RuntimeStuff.Helpers
{
    using System;
    using System.Collections.Generic;
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
            var typeCache = MemberCache.Create(typeof(T));
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
                columnSeparators = new string[] { ";" };
            }

            if (lineSeparators == null)
            {
                lineSeparators = new string[] { "\r", "\n", Environment.NewLine };
            }

            if (valueParser == null)
            {
                valueParser = s => s;
            }

            var lines = SplitBy(csv, StringSplitOptions.RemoveEmptyEntries, lineSeparators);
            if (lines.Length == 0)
            {
                return Array.Empty<T>();
            }

            var typeCache = MemberCache.Create(typeof(T));

            if (hasColumnsHeader == null)
            {
                hasColumnsHeader = SplitBy(lines[0], StringSplitOptions.None, columnSeparators).Any(x => typeCache[x.Replace(" ", string.Empty)] != null);
            }

            MemberCache[] columnNames;
            if (hasColumnsHeader.Value)
            {
                columnNames = SplitBy(lines[0], StringSplitOptions.None, columnSeparators).Select(x => typeCache[x.Replace(" ", string.Empty)]).ToArray();
            }
            else
            {
                columnNames = objectProperties?.Any() == true ? objectProperties.Select(x => (MemberCache)x).ToArray() : typeCache.PublicBasicProperties.ToArray();
            }

            var result = new List<T>();
            for (var i = hasColumnsHeader.Value ? 1 : 0; i < lines.Length; i++)
            {
                var values = SplitBy(lines[i], StringSplitOptions.None, columnSeparators).Select(x => valueParser(x)).ToArray();
                var obj = new T();
                for (var j = 0; j < columnNames.Length && j < values.Length; j++)
                {
                    if (j >= values.Length || columnNames[j] == null)
                    {
                        continue;
                    }

                    if (string.IsNullOrEmpty($"{values[j]}"))
                        continue;

                    columnNames[j].SetValue(obj, values[j]);
                }

                result.Add(obj);
            }

            return result.ToArray();
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
            return ToCsv(data, columnSelectors.Select(GetPropertyInfo).ToArray(), writeColumnHeaders, columnSeparator, lineSeparator, valueSerializer);
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
            var typeCache = MemberCache.Create(typeof(T));
            MemberCache[] props = null;
            props = columns?.Any() != true ? typeCache.PublicBasicProperties.ToArray() : columns.Select(c => typeCache[c]).Where(m => m != null).ToArray();

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
            var typeCache = MemberCache.Create(typeof(T));

            if (columns == null || columns.Length == 0)
            {
                columns = typeCache.PublicBasicProperties.Select(x => (PropertyInfo)x)
                    .ToArray();
            }

            if (valueSerializer == null)
            {
                valueSerializer = (member, value) =>
                    string.Format(CultureInfo.InvariantCulture, "{0}", value ?? string.Empty);
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

        // copy-paste helper method
        private static PropertyInfo GetPropertyInfo<T>(Expression<Func<T, object>> propertySelector)
        {
            if (propertySelector == null)
                throw new ArgumentNullException(nameof(propertySelector));

            var body = propertySelector.Body;

            // value-type -> object (boxing)
            if (body is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
                body = unary.Operand;

            if (body is MemberExpression member && member.Member is PropertyInfo pi)
                return pi;

            throw new ArgumentException(@"Expression must be a property access expression.", nameof(propertySelector));
        }

        // copy-paste helper method
        private static string[] SplitBy(string s, StringSplitOptions options, params string[] splitBy)
        {
            if (string.IsNullOrEmpty(s))
                return Array.Empty<string>();

            if (splitBy == null || splitBy.Length == 0)
                return new[] { s };

            var result = new List<string>(8);
            var pos = 0;
            var len = s.Length;

            while (pos < len)
            {
                var nextPos = -1;
                var sepLen = 0;
                foreach (var sep in splitBy)
                {
                    if (string.IsNullOrEmpty(sep))
                        continue;

                    var idx = s.IndexOf(sep, pos, StringComparison.Ordinal);
                    if (idx < 0 || (nextPos >= 0 && idx >= nextPos)) continue;
                    nextPos = idx;
                    sepLen = sep.Length;
                }

                var partLen = (nextPos < 0 ? len : nextPos) - pos;

                if (partLen > 0 || options != StringSplitOptions.RemoveEmptyEntries)
                    result.Add(s.Substring(pos, partLen));

                if (nextPos < 0)
                    break;

                pos = nextPos + sepLen;
            }

            return result.ToArray();
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
    }
}
