// ***********************************************************************
// Assembly         : RuntimeStuff
// Author           : RS
// Created          : 01-06-2026
//
// Last Modified By : RS
// Last Modified On : 01-07-2026
// ***********************************************************************
// <copyright file="StringExtensions.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************

namespace RuntimeStuff.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Text;
    using RuntimeStuff.Helpers;

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
        /// <see cref="System.Xml.XmlReader"/> или <see cref="System.Xml.Linq.XDocument"/>.
        /// </remarks>
        public static bool IsXml(this string s) => StringHelper.IsXml(s);

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
        /// Удаляет повторяющиеся пробелы, табуляции и/или переносы строк из строки,
        /// оставляя только один символ подряд для каждого типа.
        /// </summary>
        /// <param name="s">Исходная строка для обработки.</param>
        /// <param name="includeNewLines">
        /// Если <c>true</c>, последовательности символов переноса строки (<c>\r</c>, <c>\n</c>) будут сокращены до одного.
        /// Если <c>false</c>, переносы строк сохраняются без изменений.
        /// </param>
        /// <param name="includeTabs">
        /// Если <c>true</c>, последовательности табуляций (<c>\t</c>) будут сокращены до одного.
        /// Если <c>false</c>, табуляции сохраняются без изменений.
        /// </param>
        /// <returns>Строка с сокращёнными последовательностями пробелов, табуляций и переносов строк.</returns>
        /// <remarks>
        /// Метод полезен для нормализации текста, когда необходимо удалить лишние пробелы или пустые строки,
        /// сохраняя при этом читаемость и структуру текста.
        /// </remarks>
        public static string RemoveLongSpaces(this string s, bool includeNewLines = true, bool includeTabs = true) => StringHelper.RemoveLongSpaces(s, includeNewLines, includeTabs);

        /// <summary>
        /// Trimes the white chars.
        /// </summary>
        /// <param name="s">The s.</param>
        /// <returns>System.String.</returns>
        public static string TrimWhiteChars(this string s) => StringHelper.TrimWhiteChars(s);

        /// <summary>
        /// Расширение для string.IsNullOrWhiteSpace(s).
        /// </summary>
        /// <param name="s">Строка.</param>
        /// <returns>Возвращает значение string.IsNullOrWhiteSpace(s).</returns>
        public static bool IsEmpty(this string s) => string.IsNullOrWhiteSpace(s);

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
        {
            return decimal.TryParse(s, NumberStyles.Any, NumberFormatInfo.InvariantInfo, out d);
        }

        /// <summary>
        /// Проверяет, является ли строка числовым значением.
        /// </summary>
        /// <param name="s">Строка для проверки.</param>
        /// <returns><c>true</c>, если строка является числом; иначе <c>false</c>.</returns>
        /// <remarks>
        /// Метод является перегрузкой для удобства и игнорирует само значение числа.
        /// </remarks>
        public static bool IsNumber(this string s)
        {
            return s.IsNumber(out _);
        }

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
        public static string Coalesce(this string str, params string[] strings) => StringHelper.Coalesce(str, strings);

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
        /// Заменяет часть строки в диапазоне [startIndex..endIndex] на указанную строку.
        /// </summary>
        /// <param name="s">Исходная строка.</param>
        /// <param name="startIndex">Начальная позиция (включительно).</param>
        /// <param name="endIndex">Конечная позиция (включительно).</param>
        /// <param name="replaceString">Строка для замены.</param>
        /// <returns>Новая строка с заменой.</returns>
        public static string Replace(this string s, int startIndex, int endIndex, string replaceString) => StringHelper.Replace(s, startIndex, endIndex, replaceString);

        /// <summary>
        /// Возвращает строку, повторенную указанное количество раз.
        /// </summary>
        /// <param name="s">Исходная строка.</param>
        /// <param name="count">Количество повторений.</param>
        /// <returns>Новая строка, состоящая из повторений исходной строки.</returns>
        public static string RepeatString(this string s, int count) => StringHelper.RepeatString(s, count);

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
        /// Возвращает часть строки в диапазоне [startIndex..endIndex]. Работает как string.Substring(s, startIndex, endIndex -
        /// startIndex + 1).
        /// </summary>
        /// <param name="s">Исходная строка.</param>
        /// <param name="startIndex">Начальная позиция (включительно).</param>
        /// <param name="endIndex">Конечная позиция (включительно).</param>
        /// <returns>System.String.</returns>
        public static string Crop(this string s, int startIndex, int endIndex) => StringHelper.Crop(s, startIndex, endIndex);

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
    }
}