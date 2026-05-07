// <copyright file="StringExtensions.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace System
{
    using System.Collections.Generic;
    using System.Globalization;
    using System.Helpers;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Text;
    using static System.Helpers.StringHelper;

    /// <summary>
    /// Предоставляет набор методов-расширений для работы со строками, включая замену, удаление и обрезку подстрок, а
    /// также удаление суффикса.
    /// </summary>
    /// <remarks>Класс содержит статические методы-расширения для типа <see cref="string" />, позволяющие
    /// выполнять типовые операции над строками с использованием диапазонов индексов и сравнения суффиксов. Методы
    /// предназначены для упрощения манипуляций со строками в пользовательском коде. Все методы не изменяют исходную
    /// строку, а возвращают новую строку с применёнными изменениями.</remarks>
    public static class StringExtensions
    {
        /// <summary>
        /// Преобразует входную строку в указанный регистр.
        /// </summary>
        /// <param name="s">Исходная строка (например: "hello world").</param>
        /// <param name="stringCase">Тип регистра, в который необходимо преобразовать строку.</param>
        /// <returns>Строка, преобразованная в указанный регистр.</returns>
        /// <remarks>
        /// Примеры:
        /// <code>
        /// ConvertCase("hello world", StringCase.Lower)       => "hello world"
        /// ConvertCase("hello world", StringCase.Upper)       => "HELLO WORLD"
        /// ConvertCase("hello world", StringCase.Pascal)      => "HelloWorld"
        /// ConvertCase("hello world", StringCase.Camel)       => "helloWorld"
        /// ConvertCase("hello world", StringCase.Kebab)       => "hello-world"
        /// ConvertCase("hello world", StringCase.UpperKebab)  => "HELLO-WORLD"
        /// ConvertCase("hello world", StringCase.Snake)       => "hello_world"
        /// ConvertCase("hello world", StringCase.UpperSnake)  => "HELLO_WORLD"
        /// </code>
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">
        /// Может быть выброшено, если <paramref name="s"/> равно <c>null</c>.
        /// </exception>
        public static string ConvertCase(this string s, StringCase stringCase) => StringHelper.ConvertCase(s, stringCase);

        /// <summary>
        /// Преобразует строку в формат <c>PascalCase</c> (UpperCamelCase).
        /// </summary>
        /// <param name="s">Исходная строка.</param>
        /// <returns>Строка в формате <c>PascalCase</c>.</returns>
        public static string ToPascalCase(this string s) => StringHelper.ToPascalCase(s);

        /// <summary>
        /// Преобразует строку в формат <c>kebab-case</c>.
        /// </summary>
        /// <param name="s">Исходная строка.</param>
        /// <returns>Строка в формате <c>kebab-case</c>.</returns>
        public static string ToKebabCase(this string s) => StringHelper.ToKebabCase(s);

        /// <summary>
        /// Преобразует строку в формат <c>camelCase</c> (lowerCamelCase).
        /// </summary>
        /// <param name="s">Исходная строка.</param>
        /// <returns>Строка в формате <c>camelCase</c>.
        /// Если входная строка пуста или не содержит слов, возвращается пустая строка.</returns>
        public static string ToCamelCase(this string s) => StringHelper.ToCamelCase(s);

        /// <summary>
        /// Преобразует строку в формат <c>snake_case</c>.
        /// </summary>
        /// <param name="s">Исходная строка.</param>
        /// <returns>Строка в формате <c>snake_case</c>.</returns>
        public static string ToSnakeCase(this string s) => StringHelper.ToSnakeCase(s);

        /// <summary>
        /// Преобразует строку в формат <c>UPPER_SNAKE_CASE</c>.
        /// </summary>
        /// <param name="s">Исходная строка.</param>
        /// <returns>Строка в формате <c>UPPER_SNAKE_CASE</c>.</returns>
        public static string ToUpperSnakeCase(this string s) => StringHelper.ToUpperSnakeCase(s);

        /// <summary>
        /// Преобразует первую текстовую единицу строки (графему) в верхний регистр с учётом указанной культуры.
        /// </summary>
        /// <param name="s">Исходная строка.</param>
        /// <param name="culture">
        /// Культура, используемая для преобразования регистра.
        /// Если не указана, используется <see cref="CultureInfo.CurrentCulture"/>.
        /// </param>
        /// <returns>
        /// Строка, в которой первая текстовая единица преобразована в верхний регистр,
        /// а остальная часть остаётся без изменений.
        /// Если строка равна <c>null</c> или пустая, возвращается исходное значение.
        /// </returns>
        /// <remarks>
        /// В отличие от простого преобразования первого символа, метод корректно обрабатывает
        /// составные Unicode-символы (например, символы с диакритическими знаками или эмодзи),
        /// используя <see cref="StringInfo.GetTextElementEnumerator(string)"/>.
        /// </remarks>
        /// <example>
        /// <code>
        /// "hello".Capitalize() → "Hello"
        /// "ǆuro".Capitalize(new CultureInfo("hr-HR")) → "ǅuro"
        /// </code>
        /// </example>
        public static string Capitalize(this string s, CultureInfo culture = null) => StringHelper.Capitalize(s, culture);

        /// <summary>
        /// Возвращает первую непустую строку, не состоящую только из пробельных символов.
        /// </summary>
        /// <param name="str">
        /// Исходная строка, проверяемая в первую очередь.
        /// </param>
        /// <param name="strings">
        /// Дополнительные строки для проверки, используемые в случае,
        /// если <paramref name="str"/> равна <c>null</c>, пуста или содержит только пробельные символы.
        /// </param>
        /// <returns>
        /// Первую строку, которая не равна <c>null</c>, не пуста и не состоит только из пробельных символов;
        /// либо <c>null</c>, если все переданные строки не удовлетворяют этому условию.
        /// </returns>
        /// <remarks>
        /// Метод является строковым аналогом оператора <c>COALESCE</c>
        /// и удобен для выбора значения по умолчанию из набора строк.
        /// </remarks>
        public static string FirstNotEmpty(this string str, params string[] strings) => StringHelper.FirstNotEmpty(str, strings);

        /// <summary>
        /// Проверяет, содержит ли исходная строка указанную подстроку,
        /// используя заданный способ сравнения строк.
        /// </summary>
        /// <param name="source">Исходная строка, в которой выполняется поиск.</param>
        /// <param name="value">Подстрока, которую необходимо найти.</param>
        /// <param name="comparison">Параметр, определяющий способ сравнения строк
        /// (<see cref="StringComparison" />), например <see cref="StringComparison.OrdinalIgnoreCase" />.</param>
        /// <returns>Значение <c>true</c>, если подстрока найдена в исходной строке;
        /// в противном случае — <c>false</c>.
        /// Также возвращает <c>false</c>, если <paramref name="source" /> или <paramref name="value" /> равны <c>null</c>.</returns>
        public static bool Contains(this string source, string value, StringComparison comparison) => StringHelper.Contains(source, value, comparison);

        /// <summary>
        /// Проверяет, содержит ли исходная строка указанную подстроку,
        /// используя заданный способ сравнения строк.
        /// </summary>
        /// <param name="source">Исходная строка, в которой выполняется поиск.</param>
        /// <param name="comparison">Параметр, определяющий способ сравнения строк
        /// (<see cref="StringComparison" />), например <see cref="StringComparison.OrdinalIgnoreCase" />.</param>
        /// <param name="values">Подстрока, которую необходимо найти.</param>
        /// <returns>Значение <c>true</c>, если подстрока найдена в исходной строке;
        /// в противном случае — <c>false</c>.
        /// Также возвращает <c>false</c>, если <paramref name="source" /> или <paramref name="values" /> равны <c>null</c>.</returns>
        public static bool ContainsAny(this string source, StringComparison comparison, params string[] values) =>
            StringHelper.ContainsAny(source, comparison, values);

        /// <summary>
        /// Возвращает часть строки в диапазоне [startIndex..endIndex]. Работает как string.Substring(s, startIndex, endIndex -
        /// startIndex + 1).
        /// </summary>
        /// <param name="s">Исходная строка.</param>
        /// <param name="startIndex">Начальная позиция (включительно).</param>
        /// <param name="endIndex">Конечная позиция (включительно).</param>
        /// <returns>System.String.</returns>
        public static string Crop(this string s, int startIndex, int endIndex) => StringHelper.Crop(s, startIndex, endIndex);

        /// <summary>
        /// Удаляет часть строки в диапазоне [startIndex..endIndex]. Работает как s.Substring(0, startIndex) +
        /// s.Substring(endIndex + 1);.
        /// </summary>
        /// <param name="s">Исходная строка.</param>
        /// <param name="startIndex">Начальная позиция (включительно).</param>
        /// <param name="endIndex">Конечная позиция (включительно).</param>
        /// <returns>System.String.</returns>
        public static string Cut(this string s, int startIndex, int endIndex) => StringHelper.Cut(s, startIndex, endIndex);

        /// <summary>
        /// Преобразует строку Base64 обратно в обычную строку с использованием кодировки UTF-8.
        /// </summary>
        /// <param name="s">Строка в формате Base64.</param>
        /// <param name="encoding">Кодировка. По умолчанию - UTF8.</param>
        /// <returns>Декодированная исходная строка.</returns>
        /// <remarks>
        /// Метод декодирует строку Base64 в массив байтов и затем преобразует его
        /// в строку UTF-8. Если строка Base64 некорректна, будет выброшено
        /// <see cref="FormatException"/>.
        /// </remarks>
        public static string FromBase64(this string s, Encoding encoding = null) => (encoding ?? Encoding.UTF8).GetString(Convert.FromBase64String(s));

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
        /// <param name="objectProperties">Маппер колонок из csv на свойства объекта в порядке следования колонок в csv.</param>
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
        public static T[] FromCsv<T>(this string csv, bool? hasColumnsHeader = null, string[] columnSeparators = null, string[] lineSeparators = null, Func<string, object> valueParser = null, params Expression<Func<T, object>>[] objectProperties)
    where T : class, new()
        {
            return CsvHelper.FromCsv<T>(csv, objectProperties.Select(x => x.GetPropertyInfo()).ToArray(), hasColumnsHeader, columnSeparators, lineSeparators, valueParser);
        }

        /// <summary>
        /// Преобразует CSV-строку в массив объектов указанного класса с возможностью настройки разделителей и парсера значений.
        /// </summary>
        /// <typeparam name="T">Тип объектов для создания. Должен быть классом с публичным конструктором без параметров.</typeparam>
        /// <param name="csv">CSV-строка для обработки.</param>
        /// <param name="propertyNames">Маппер колонок из csv на свойства объекта в порядке следования колонок в csv.</param>
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
        public static T[] FromCsv<T>(this string csv, string[] propertyNames, bool? hasColumnsHeader = null, string[] columnSeparators = null, string[] lineSeparators = null, Func<string, object> valueParser = null)
    where T : class, new()
        {
            return CsvHelper.FromCsv<T>(csv, propertyNames, hasColumnsHeader, columnSeparators, lineSeparators, valueParser);
        }

        /// <summary>
        /// Выполняет экранирование строки в соответствии с указанным режимом.
        /// </summary>
        /// <param name="value">
        /// Исходная строка для экранирования.
        /// Если значение равно <c>null</c>, метод возвращает <c>null</c>.
        /// </param>
        /// <param name="mode">
        /// Режим экранирования, определяющий правила преобразования строки:
        /// <list type="bullet">
        /// <item>
        /// <description><see cref="StringHelper.EscapeMode.None"/> — строка возвращается без изменений.</description>
        /// </item>
        /// <item>
        /// <description><see cref="StringHelper.EscapeMode.Url"/> — URL-кодирование с использованием <see cref="Uri.EscapeDataString(string)"/>.</description>
        /// </item>
        /// <item>
        /// <description><see cref="StringHelper.EscapeMode.Sql"/> — экранирование одинарных кавычек для SQL (замена <c>'</c> на <c>''</c>).</description>
        /// </item>
        /// <item>
        /// <description><see cref="StringHelper.EscapeMode.Json"/> — экранирование специальных символов согласно спецификации JSON.</description>
        /// </item>
        /// <item>
        /// <description><see cref="StringHelper.EscapeMode.XmlText"/> — экранирование строки для использования в тексте XML-узла.</description>
        /// </item>
        /// <item>
        /// <description><see cref="StringHelper.EscapeMode.XmlAttribute"/> — экранирование строки для использования в значении XML-атрибута.</description>
        /// </item>
        /// <item>
        /// <description><see cref="StringHelper.EscapeMode.Csv"/> — экранирование строки по правилам CSV (RFC 4180).</description>
        /// </item>
        /// <item>
        /// <description><see cref="StringHelper.EscapeMode.CSharp"/> — экранирование строки для безопасного использования в строковом литерале C#.</description>
        /// </item>
        /// <item>
        /// <description><see cref="StringHelper.EscapeMode.Base64"/> — преобразование строки в Base64 (кодировка UTF-8).</description>
        /// </item>
        /// </list>
        /// </param>
        /// <returns>
        /// Экранированная строка в соответствии с выбранным режимом.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Выбрасывается, если передано неподдерживаемое значение <paramref name="mode"/>.
        /// </exception>
        public static string EscapeString(string value, StringHelper.EscapeMode mode) => StringHelper.EscapeString(value, mode);

        /// <summary>
        /// Выполняет поиск подстроки в строке, начиная с указанной позиции и
        /// перемещаясь по строке с заданным шагом.
        /// </summary>
        /// <param name="s">Строка, в которой выполняется поиск.</param>
        /// <param name="subString">Подстрока, которую необходимо найти.</param>
        /// <param name="startIndex">
        /// Индекс, с которого начинается поиск.
        /// </param>
        /// <param name="step">
        /// Шаг перемещения по строке.
        /// Положительное значение выполняет поиск слева направо,
        /// отрицательное — справа налево. Значение не может быть равно <c>0</c>.
        /// </param>
        /// <param name="comparison">
        /// Правило сравнения строк (<see cref="StringComparison"/>),
        /// определяющее чувствительность к регистру и культуру сравнения.
        /// </param>
        /// <returns>
        /// Индекс первого найденного вхождения <paramref name="subString"/>,
        /// удовлетворяющего заданному шагу поиска.
        /// Если совпадение не найдено или <paramref name="startIndex"/> выходит
        /// за границы строки, возвращается <c>-1</c>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Выбрасывается, если <paramref name="s"/> или <paramref name="subString"/> равны <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Выбрасывается, если <paramref name="step"/> равен <c>0</c>.
        /// </exception>
        /// <remarks>
        /// Метод позволяет выполнять поиск с произвольным шагом.
        /// Например:
        /// <list type="bullet">
        /// <item>
        /// <description>
        /// <c>step = 1</c> — обычный последовательный поиск слева направо.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// <c>step = -1</c> — поиск справа налево.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// <c>step = n</c> — проверка каждой <c>n</c>-й позиции строки.
        /// </description>
        /// </item>
        /// </list>
        /// Если <paramref name="subString"/> — пустая строка, возвращается <paramref name="startIndex"/>.
        /// </remarks>
        public static int IndexOf(this string s, string subString, int startIndex, int step, StringComparison comparison = StringComparison.Ordinal) =>
            StringHelper.IndexOf(s, subString, startIndex, step, comparison);

        /// <summary>
        /// Расширение для string.IsNullOrWhiteSpace(s).
        /// </summary>
        /// <param name="s">Строка.</param>
        /// <returns>Возвращает значение string.IsNullOrWhiteSpace(s).</returns>
        public static bool IsEmpty(this string s) => string.IsNullOrWhiteSpace(s);

        /// <summary>
        /// Проверяет, является ли строка потенциально корректным JSON-фрагментом.
        /// </summary>
        /// <param name="s">
        /// Проверяемая строка.
        /// </param>
        /// <returns>
        /// <c>true</c>, если строка по базовым синтаксическим признакам может быть JSON;
        /// <c>false</c> — если строка пуста, состоит из пробельных символов
        /// или явно не соответствует формату JSON.
        /// </returns>
        /// <remarks>
        /// Метод выполняет только быструю эвристическую проверку и
        /// <b>не гарантирует</b> синтаксическую корректность JSON.
        /// Проверяются следующие условия:
        /// <list type="bullet">
        /// <item><description>Строка не равна <c>null</c> и не пуста;</description></item>
        /// <item><description>После обрезки пробельных символов длина строки не менее 2 символов;</description></item>
        /// <item><description>Строка начинается с символа '{' и заканчивается '}', либо начинается с '[' и заканчивается ']'.</description></item>
        /// </list>
        /// Метод не проверяет корректность структуры, экранирование строк,
        /// соответствие стандарту JSON и вложенность элементов.
        /// Для полноценной проверки рекомендуется использовать сторонние JSON-парсеры.
        /// </remarks>
        public static bool IsJson(this string s) => StringHelper.IsJson(s);

        /// <summary>
        /// Проверяет, является ли строка числовым значением и преобразует её в <see cref="decimal"/>.
        /// </summary>
        /// <param name="s">Строка для проверки.</param>
        /// <param name="d">Выходной параметр, содержащий значение <see cref="decimal"/>, если строка является числом.</param>
        /// <returns><c>true</c>, если строка успешно распознана как число; иначе <c>false</c>.</returns>
        /// <remarks>
        /// Используется <see cref="NumberStyles.Any"/> и <see cref="NumberFormatInfo.InvariantInfo"/>
        /// для корректного парсинга чисел в стандартном формате.
        /// </remarks>
        public static bool IsNumber(this string s, out decimal d)
            => StringHelper.IsNumber(s, out d);

        /// <summary>
        /// Проверяет, является ли строка числовым значением.
        /// </summary>
        /// <param name="s">Строка для проверки.</param>
        /// <returns><c>true</c>, если строка является числом; иначе <c>false</c>.</returns>
        /// <remarks>
        /// Метод является перегрузкой для удобства и игнорирует само значение числа.
        /// </remarks>
        public static bool IsNumber(this string s)
            => StringHelper.IsNumber(s);

        /// <summary>
        /// Проверяет, является ли строка потенциально корректным XML-фрагментом.
        /// </summary>
        /// <param name="s">
        /// Проверяемая строка.
        /// </param>
        /// <returns>
        /// <c>true</c>, если строка по базовым синтаксическим признакам может быть XML;
        /// <c>false</c> — если строка пуста, состоит из пробельных символов
        /// или явно не соответствует формату XML.
        /// </returns>
        /// <remarks>
        /// Метод выполняет только быструю предварительную проверку и
        /// <b>не гарантирует</b> синтаксическую корректность XML.
        /// Проверяются следующие условия:
        /// <list type="bullet">
        /// <item><description>Строка не равна <c>null</c> и не пуста;</description></item>
        /// <item><description>После обрезки пробельных символов строка начинается с символа '&lt;';</description></item>
        /// <item><description>Минимальная допустимая длина XML (&lt;a/&gt;);</description></item>
        /// <item><description>Исключаются HTML-комментарии и объявления DOCTYPE без корневого элемента;</description></item>
        /// <item><description>Наличие закрывающего символа '&gt;'.</description></item>
        /// </list>
        /// Для полной проверки корректности XML рекомендуется использовать
        /// <see cref="Xml.XmlReader"/> или <see cref="Xml.Linq.XDocument"/>.
        /// </remarks>
        public static bool IsXml(this string s) => StringHelper.IsXml(s);

        /// <summary>
        /// Нормализует пробельные символы в строке:
        /// заменяет все последовательности пробелов, табуляций и переносов строк на один пробел,
        /// а также удаляет пробелы с начала и конца строки.
        /// </summary>
        /// <param name="s">Строка для нормализации. Может быть <c>null</c> или пустой.</param>
        /// <returns>
        /// Строка с нормализованными пробелами.
        /// Если входная строка <c>null</c> или пустая, возвращается исходное значение.
        /// </returns>
        public static string NormalizeWhitespace(this string s) => StringHelper.NormalizeWhiteSpaces(s);

        /// <summary>
        /// Возвращает строку, повторенную указанное количество раз.
        /// </summary>
        /// <param name="s">Исходная строка.</param>
        /// <param name="count">Количество повторений.</param>
        /// <returns>Новая строка, состоящая из повторений исходной строки.</returns>
        public static string RepeatString(this string s, int count) => StringHelper.RepeatString(s, count);

        /// <summary>
        /// Заменяет часть строки в диапазоне [startIndex..endIndex] на указанную строку.
        /// </summary>
        /// <param name="s">Исходная строка.</param>
        /// <param name="startIndex">Начальная позиция (включительно).</param>
        /// <param name="endIndex">Конечная позиция (включительно).</param>
        /// <param name="replaceString">Строка для замены.</param>
        /// <returns>Новая строка с заменой.</returns>
        public static string Replace(this string s, int startIndex, int endIndex, string replaceString) => StringHelper.Replace(s, startIndex, endIndex, replaceString);

        /// <summary>
        /// Заменяет недопустимые символы в имени файла на указанный заменяющий текст.
        /// </summary>
        /// <param name="filename">Исходное имя файла для обработки.</param>
        /// <param name="replaceString">
        /// Строка, на которую будут заменены все недопустимые символы.
        /// По умолчанию используется символ подчёркивания ("_").
        /// </param>
        /// <returns>Имя файла с заменёнными недопустимыми символами.</returns>
        /// <remarks>
        /// Метод использует <see cref="Path.GetInvalidFileNameChars"/> для определения символов,
        /// которые не могут присутствовать в имени файла, и заменяет их на <paramref name="replaceString"/>.
        /// </remarks>
        public static string ReplaceFileNameInvalidChars(this string filename, string replaceString = "_")
        {
            if (string.IsNullOrEmpty(filename))
            {
                return filename;
            }

            return string.Join(replaceString, filename.Split(Path.GetInvalidFileNameChars()));
        }

        /// <summary>
        /// Разбивает строку на подстроки по одному или нескольким указанным разделителям.
        /// </summary>
        /// <param name="s">Исходная строка для разбиения.</param>
        /// <param name="options">Настройки.</param>
        /// <param name="splitBy">Массив строк-разделителей. Порядок важен, выбирается ближайший к текущей позиции.</param>
        /// <returns>
        /// Массив подстрок, полученных после разбиения. Если строка <c>null</c> или пустая, возвращается пустой массив.
        /// Если <paramref name="splitBy"/> пустой или <c>null</c>, возвращается массив, содержащий исходную строку.
        /// </returns>
        /// <remarks>
        /// <para>Метод выполняет последовательный поиск ближайшего разделителя и делит строку по нему.</para>
        /// <para>Подстроки между разделителями включаются в результат, разделители сами не включаются.</para>
        /// <para>Поддерживается несколько разделителей произвольной длины.</para>
        /// </remarks>
        public static string[] SplitBy(this string s, StringSplitOptions options, params string[] splitBy) => StringHelper.SplitBy(s, options, splitBy);

        /// <summary>
        /// Разбивает входную строку на список объектов указанного типа,
        /// используя разделители колонок и строк по умолчанию.
        /// </summary>
        /// <typeparam name="T">
        /// Тип объекта, в который будут маппиться данные строк.
        /// </typeparam>
        /// <param name="s">
        /// Исходная строка, содержащая данные.
        /// </param>
        /// <param name="propertyMap">
        /// Массив имён свойств типа <typeparamref name="T"/>,
        /// определяющий порядок маппинга колонок.
        /// Если не задан, используются все публичные базовые свойства.
        /// </param>
        /// <returns>
        /// Список объектов типа <typeparamref name="T"/>,
        /// заполненных данными из строки.
        /// </returns>
        public static List<T> SplitToList<T>(this string s, params string[] propertyMap) => StringHelper.SplitToList<T>(s, propertyMap);

        /// <summary>
        /// Разбивает входную строку на список объектов указанного типа
        /// с возможностью указать собственные разделители колонок и строк.
        /// </summary>
        /// <typeparam name="T">
        /// Тип объекта, в который будут маппиться данные строк.
        /// </typeparam>
        /// <param name="s">
        /// Исходная строка, содержащая данные.
        /// </param>
        /// <param name="propertyMap">
        /// Массив имён свойств типа <typeparamref name="T"/>,
        /// определяющий порядок маппинга колонок.
        /// Если не задан или пуст, используются все публичные базовые свойства.
        /// </param>
        /// <param name="columnSeparators">
        /// Массив разделителей колонок.
        /// </param>
        /// <param name="lineSeparators">
        /// Массив разделителей строк.
        /// </param>
        /// <returns>
        /// Список объектов типа <typeparamref name="T"/>,
        /// заполненных данными из строки.
        /// </returns>
        public static List<T> SplitToList<T>(this string s, string[] propertyMap, string[] columnSeparators, string[] lineSeparators) => StringHelper.SplitToList<T>(s, propertyMap, columnSeparators, lineSeparators);

        /// <summary>
        /// Проверяет, начинается ли строка хотя бы одной из указанных подстрок.
        /// </summary>
        /// <param name="s">Исходная строка, в которой выполняется поиск.</param>
        /// <param name="comparison">
        /// Тип сравнения строк, используемый при поиске подстрок
        /// (например, <see cref="StringComparison.OrdinalIgnoreCase"/>).
        /// </param>
        /// <param name="values">Массив подстрок, наличие которых необходимо проверить.</param>
        /// <returns>
        /// <c>true</c>, если строка начинается хотя бы одной из указанных подстрок;
        /// иначе <c>false</c>.
        /// Если исходная строка пуста, массив подстрок равен <c>null</c> или пуст,
        /// метод возвращает <c>false</c>.
        /// </returns>
        public static bool StartsWithAny(this string s, StringComparison comparison, params string[] values) => StringHelper.StartsWithAny(s, comparison, values);

        /// <summary>
        /// Проверяет, заканчивается ли строка хотя бы одной из указанных подстрок.
        /// </summary>
        /// <param name="s">Исходная строка, для которой выполняется проверка.</param>
        /// <param name="comparison">
        /// Тип сравнения строк, используемый при проверке окончания строки
        /// (например, <see cref="StringComparison.OrdinalIgnoreCase"/>).
        /// </param>
        /// <param name="values">Массив подстрок, окончания которых необходимо проверить.</param>
        /// <returns>
        /// <c>true</c>, если строка заканчивается хотя бы одной из указанных подстрок;
        /// иначе <c>false</c>.
        /// Если исходная строка пуста, массив подстрок равен <c>null</c> или пуст,
        /// метод возвращает <c>false</c>.
        /// </returns>
        public static bool EndsWithAny(this string s, StringComparison comparison, params string[] values)
        {
            if (string.IsNullOrEmpty(s) || values == null || values.Length == 0)
            {
                return false;
            }

            foreach (var value in values)
            {
                if (string.IsNullOrEmpty(value))
                {
                    continue;
                }

                if (s.EndsWith(value, comparison))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Преобразует строку в строку Base64 с использованием кодировки UTF-8.
        /// </summary>
        /// <param name="s">Исходная строка.</param>
        /// <param name="encoding">Кодировка. По умолчанию - UTF8.</param>
        /// <returns>Строка в формате Base64.</returns>
        /// <remarks>
        /// Метод кодирует исходную строку в массив байтов UTF-8 и затем преобразует
        /// его в строку Base64. Используется для безопасной передачи бинарных данных
        /// в текстовом виде.
        /// </remarks>
        public static string ToBase64(this string s, Encoding encoding = null) => Convert.ToBase64String((encoding ?? Encoding.UTF8).GetBytes(s));

        /// <summary>
        /// Удаляет указанную подстроку из начала и конца строки.
        /// </summary>
        /// <param name="s">Исходная строка.</param>
        /// <param name="trimString">Подстрока, которую необходимо удалить.</param>
        /// <param name="comparison">
        /// Тип сравнения строк при поиске подстроки.
        /// По умолчанию используется <see cref="StringComparison.OrdinalIgnoreCase"/>.
        /// </param>
        /// <returns>
        /// Строка без указанной подстроки в начале и конце.
        /// Если исходная строка или подстрока пустые, возвращается исходная строка.
        /// </returns>
        public static string Trim(this string s, string trimString, StringComparison comparison = StringComparison.Ordinal) => StringHelper.Trim(s, trimString, comparison);

        /// <summary>
        /// Удаляет указанную подстроку из начала строки.
        /// </summary>
        /// <param name="s">Исходная строка.</param>
        /// <param name="trimString">Подстрока, которую необходимо удалить из начала строки.</param>
        /// <param name="comparison">
        /// Тип сравнения строк при проверке начала строки.
        /// По умолчанию используется <see cref="StringComparison.OrdinalIgnoreCase"/>.
        /// </param>
        /// <returns>
        /// Строка без указанной подстроки в начале.
        /// Если исходная строка или подстрока пустые, возвращается исходная строка.
        /// </returns>
        public static string TrimStart(this string s, string trimString, StringComparison comparison = StringComparison.Ordinal) => StringHelper.TrimStart(s, trimString, comparison);

        /// <summary>
        /// Метод удаляет указанный суффикс с конца строки, если он существует.
        /// </summary>
        /// <param name="s">Исходная строка, из которой нужно удалить суффикс.</param>
        /// <param name="subStr">Строка-суффикс, которую нужно удалить с конца.</param>
        /// <param name="comparison">Тип сравнения строк при проверке суффикса.</param>
        /// <returns>Строка без указанного суффикса в конце, если он был найден.</returns>
        /// <remarks>Метод проверяет заканчивается ли исходная строка указанным суффиксом.
        /// Если суффикс найден, возвращается строка без этого суффикса.
        /// Если суффикс не найден или параметры пустые, возвращается исходная строка.</remarks>
        public static string TrimEnd(this string s, string subStr, StringComparison comparison = StringComparison.Ordinal) => StringHelper.TrimEnd(s, subStr, comparison);

        /// <summary>
        /// Trimes the white chars.
        /// </summary>
        /// <param name="s">The s.</param>
        /// <returns>System.String.</returns>
        public static string TrimWhiteChars(this string s) => StringHelper.TrimWhitespaces(s);

        /// <summary>
        /// Распаковывает строку, сжатую с помощью <see cref="Zip"/>, из формата Base64 обратно в исходный текст.
        /// </summary>
        /// <param name="s">Сжатая строка в формате Base64.</param>
        /// <returns>Исходная строка, или <c>null</c>/пустая строка, если входная строка пустая.</returns>
        /// <remarks>
        /// Метод декодирует строку из Base64, затем распаковывает данные с помощью <see cref="GZipStream"/>
        /// и интерпретирует их как UTF-8.
        /// </remarks>
        public static string UnZip(this string s) => StringHelper.UnZip(s);

        /// <summary>
        /// Сжимает строку с помощью GZip и возвращает результат в виде строки в формате Base64.
        /// </summary>
        /// <param name="s">Исходная строка для сжатия.</param>
        /// <returns>
        /// Сжатая строка в формате Base64, или исходная строка, если она пустая или <c>null</c>.
        /// </returns>
        /// <remarks>
        /// Метод кодирует строку в UTF-8, затем сжимает её с помощью <see cref="GZipStream"/>.
        /// </remarks>
        public static string Zip(this string s) => StringHelper.Zip(s);
    }
}