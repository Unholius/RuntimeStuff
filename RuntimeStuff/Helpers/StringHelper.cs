// <copyright file="StringHelper.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace System.Helpers
{
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Предоставляет набор статических методов для работы со строками и токенами, включая удаление суффикса, замену и
    /// обрезку частей строки, а также парсинг иерархии токенов по заданным маскам.
    /// </summary>
    /// <remarks>Класс предназначен для удобной обработки строк и выделения вложенных токенов по префиксу и суффиксу.
    /// Поддерживает работу с иерархическими структурами токенов, их разворачивание в плоский список, а также применение
    /// пользовательских функций-трансформеров к содержимому токенов. Все методы реализованы как статические и не требуют
    /// создания экземпляра класса. Класс потокобезопасен при условии корректного использования входных данных.</remarks>
    public static class StringHelper
    {
        private static readonly char[] Separator = ['_'];

        static StringHelper()
        {
            OpeningQuotes =
                [
                    '\u0022', // " Default
                    '\u00AB', // « French, Russian opening
                    '\u2039', // ‹ Single guillemet opening
                    '\u201C', // “ Left double quotation mark
                    '\u2018', // ‘ Left single quotation mark
                    '\u201E', // „ Double low-9 quotation mark (German opening)
                    '\u201A', // ‚ Single low-9 quotation mark
                    '\u300C', // 「 CJK corner bracket opening
                    '\u300E', // 『 CJK white corner bracket opening
                    '\u3008', // 〈 CJK angle bracket opening
                    '\u300A', // 《 CJK double angle bracket opening
                    '\u300C', // 「 Japanese opening quote
                    '\uFE41', // ﹁ Small corner bracket opening
                    '\uFE43', // ﹃ Small white corner bracket opening
                    '\uFF62', // ｢ Halfwidth corner bracket opening
                ];

            ClosingQuotes =
            [
                '\u0022', // " Default
                '\u00BB', // » French, Russian closing
                '\u203A', // › Single guillemet closing
                '\u201D', // ” Right double quotation mark
                '\u2019', // ’ Right single quotation mark
                '\u201F', // ‟ Double high-reversed-9 quotation mark
                '\u300F', // 』 CJK white corner bracket closing
                '\u300D', // 」 CJK corner bracket closing
                '\u3009', // 〉 CJK angle bracket closing
                '\u300B', // 》 CJK double angle bracket closing
                '\uFE42', // ﹂ Small corner bracket closing
                '\uFE44', // ﹄ Small white corner bracket closing
                '\uFF63', // ｣ Halfwidth corner bracket closing
            ];

            WhitespaceChars =
                    [
                        ' ',
                        '\t',
                        '\r',
                        '\n',
                        '\0',
                        '\v', // U+000B Vertical Tab
                        '\f', // U+000C Form Feed
                        '\u00A0', // NO-BREAK SPACE
                        '\u2007', // Figure Space
                        '\u202F', // Narrow No-Break Space
                        '\u2028', // Line Separator
                        '\u2029', // Paragraph Separator
                        '\u200B', // Zero Width Space
                        '\u200C', // Zero Width Non-Joiner
                        '\u200D', // Zero Width Joiner
                        '\u2060', // Word Joiner
                        '\uFEFF', // BOM (Zero Width No-Break Space)
                    ];

            AllQuotes =
                OpeningQuotes
                .Concat(ClosingQuotes)
                .Distinct()
                .ToArray();

            DefaultColumnSeparators = ["\t", ";", "|"];
            DefaultColumnSeparatorsAndSpace = [" ", "\t", ";", "|"];
            DefaultLineSeparators = [Environment.NewLine, "\r", "\n"];
        }

        /// <summary>
        /// Определяет стратегию экранирования (escaping) строки
        /// в зависимости от целевого формата или протокола передачи данных.
        /// </summary>
        public enum EscapeMode
        {
            /// <summary>
            /// Экранирование не выполняется.
            /// Строка возвращается без изменений.
            /// </summary>
            None,

            /// <summary>
            /// URL-кодирование (percent-encoding) в соответствии с RFC 3986.
            /// Применяется для параметров HTTP-запросов и query-строк.
            /// Пример: пробел преобразуется в "%20".
            /// </summary>
            Url,

            /// <summary>
            /// Экранирование строки для использования в SQL-литералах.
            /// Обычно заключается в удвоении одинарной кавычки:
            /// ' ? ''.
            /// </summary>
            Sql,

            /// <summary>
            /// Экранирование строки по правилам JSON.
            /// Спецсимволы (", \, \n, \r и др.) заменяются на escape-последовательности.
            /// </summary>
            Json,

            /// <summary>
            /// Экранирование текста для размещения внутри XML-узла (текстовое содержимое).
            /// </summary>
            XmlText,

            /// <summary>
            /// Экранирование строки для использования в значении XML-атрибута.
            /// Дополнительно к XmlText экранируются кавычки.
            /// </summary>
            XmlAttribute,

            /// <summary>
            /// Экранирование строки по правилам CSV.
            /// При наличии разделителей, кавычек или переводов строки
            /// значение заключается в двойные кавычки,
            /// а двойные кавычки удваиваются.
            /// </summary>
            Csv,

            /// <summary>
            /// Экранирование строки для использования в C#-строковом литерале.
            /// Экранируются ", \, \n, \r, \t и другие спецсимволы.
            /// </summary>
            CSharp,

            /// <summary>
            /// Кодирование строки в формат Base64.
            /// Применяется для безопасной передачи бинарных данных
            /// или произвольного текста через текстовые протоколы.
            /// </summary>
            Base64,
        }

        /// <summary>
        /// Определяет алгоритм нечеткого сравнения строк.
        /// </summary>
        public enum FuzzyCompareMethod
        {
            /// <summary>
            /// Алгоритм Левенштейна.
            /// Основан на подсчёте минимального количества операций
            /// (вставка, удаление, замена), необходимых для преобразования
            /// одной строки в другую.
            /// </summary>
            Levenshtein,

            /// <summary>
            /// Алгоритм Жаро–Винклера.
            /// Учитывает количество совпадающих символов, перестановки
            /// и общий префикс строк, что делает его более подходящим
            /// для сравнения коротких строк и имён.
            /// </summary>
            JaroWinkler,
        }

        /// <summary>
        /// Определяет стиль преобразования регистра строк.
        /// </summary>
        /// <remarks>
        /// Примеры:
        /// <code>
        /// "hello world" → Lower        => "hello world"
        /// "hello world" → Upper        => "HELLO WORLD"
        /// "hello world" → Pascal       => "HelloWorld"
        /// "hello world" → Camel        => "helloWorld"
        /// "hello world" → Snake        => "hello_world"
        /// "hello world" → UpperSnake   => "HELLO_WORLD"
        /// "hello world" → Kebab        => "hello-world"
        /// "hello world" → UpperKebab   => "HELLO-WORLD"
        /// </code>
        /// </remarks>
        public enum StringCase
        {
            /// <summary>
            /// Строка без изменений.
            /// </summary>
            None,

            /// <summary>
            /// lower case — вся строка в нижнем регистре без изменения разделителей.
            /// Пример: "hello world".
            /// </summary>
            Lower,

            /// <summary>
            /// UPPER CASE — вся строка в верхнем регистре без изменения разделителей.
            /// Пример: "HELLO WORLD".
            /// </summary>
            Upper,

            /// <summary>
            /// PascalCase — каждое слово начинается с заглавной буквы, разделители удаляются.
            /// Пример: "HelloWorld".
            /// </summary>
            Pascal,

            /// <summary>
            /// camelCase — первая буква строчная, каждое следующее слово с заглавной, разделители удаляются.
            /// Пример: "helloWorld".
            /// </summary>
            Camel,

            /// <summary>
            /// snake_case — слова разделяются символом подчеркивания, все буквы строчные.
            /// Пример: "hello_world".
            /// </summary>
            Snake,

            /// <summary>
            /// UPPER_SNAKE_CASE — слова разделяются символом подчеркивания, все буквы заглавные.
            /// Пример: "HELLO_WORLD".
            /// </summary>
            UpperSnake,

            /// <summary>
            /// kebab-case — слова разделяются дефисом, все буквы строчные.
            /// Пример: "hello-world".
            /// </summary>
            Kebab,

            /// <summary>
            /// UPPER-KEBAB-CASE — слова разделяются дефисом, все буквы заглавные.
            /// Пример: "HELLO-WORLD".
            /// </summary>
            UpperKebab,
        }

        /// <summary>
        /// Коллекция кавычек. Комбинация уникальных значений <see cref="OpeningQuotes"/> и <see cref="ClosingQuotes"/>.
        /// </summary>
        public static char[] AllQuotes { get; }

        /// <summary>
        /// Коллекция закрывающих кавычек.
        /// </summary>
        public static char[] ClosingQuotes { get; }

        /// <summary>
        /// Разделители для колонок. ("\t", ";", "|").
        /// </summary>
        public static string[] DefaultColumnSeparators { get; }

        /// <summary>
        /// Разделители для колонок. (" ", "\t", ";", "|").
        /// </summary>
        public static string[] DefaultColumnSeparatorsAndSpace { get; }

        /// <summary>
        /// Разделители для строк. (Environment.NewLine, "\r", "\n").
        /// </summary>
        public static string[] DefaultLineSeparators { get; }

        /// <summary>
        /// Коллекция открывающих кавычек.
        /// </summary>
        public static char[] OpeningQuotes { get; }

        /// <summary>
        /// whitespace chars.
        /// Набор пробельных символов, используемых при разборе строк.
        /// Включает пробел, перевод строки, табуляция, пустой символ.
        /// </summary>
        public static char[] WhitespaceChars { get; }

        /// <summary>
        /// Добавляет к строке префикс и/или суффикс.
        /// </summary>
        /// <param name="value">Исходная строка.</param>
        /// <param name="prefix">Префикс (добавляется в начало строки). Может быть <c>null</c>.</param>
        /// <param name="suffix">Суффикс (добавляется в конец строки). Может быть <c>null</c>.</param>
        /// <returns>
        /// Строка с добавленным префиксом и/или суффиксом.
        /// <para/>
        /// Если оба параметра <paramref name="prefix"/> и <paramref name="suffix"/> равны <c>null</c>,
        /// возвращается исходное значение <paramref name="value"/>.
        /// </returns>
        /// <remarks>
        /// Метод использует <see cref="string.Concat(string, string, string)"/>,
        /// поэтому значения <c>null</c> интерпретируются как пустые строки.
        /// </remarks>
        /// <example>
        /// <code>
        /// ApplyAffixes("test", "[", "]") → "[test]"
        /// ApplyAffixes("test", null, "!") → "test!"
        /// ApplyAffixes("test", null, null) → "test"
        /// </code>
        /// </example>
        public static string ApplyAffixes(string value, string prefix, string suffix)
        {
            if (prefix == null && suffix == null)
            {
                return value;
            }

            return string.Concat(prefix, value, suffix);
        }

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
        public static string Capitalize(string s, CultureInfo culture = null)
        {
            if (string.IsNullOrEmpty(s))
            {
                return s;
            }

            if (culture == null)
            {
                culture = CultureInfo.CurrentCulture;
            }

            var enumerator = StringInfo.GetTextElementEnumerator(s);
            if (!enumerator.MoveNext())
            {
                return s;
            }

            var first = enumerator.GetTextElement();
            var restIndex = enumerator.ElementIndex + first.Length;

            return culture.TextInfo.ToUpper(first) + s.Substring(restIndex);
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
        public static string Coalesce(string str, params string[] strings)
        {
            if (!string.IsNullOrWhiteSpace(str))
            {
                return str;
            }

            return strings.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));
        }

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
        public static bool Contains(string source, string value, StringComparison comparison)
        {
            if (source == null || value == null)
            {
                return false;
            }

            return source.IndexOf(value, comparison) >= 0;
        }

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
        public static bool ContainsAny(string source, StringComparison comparison, params string[] values)
        {
            if (source == null || values == null)
            {
                return false;
            }

            return values.Any(x => source.IndexOf(x, comparison) >= 0);
        }

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
        public static string ConvertCase(string s, StringCase stringCase)
        {
            return stringCase switch
            {
                StringCase.None => s,
                StringCase.Lower => s.ToLowerInvariant(),
                StringCase.Upper => s.ToUpperInvariant(),
                StringCase.Pascal => ToPascalCase(s),
                StringCase.Camel => ToCamelCase(s),
                StringCase.Kebab => ToKebabCase(s),
                StringCase.UpperKebab => ToKebabCase(s).ToUpperInvariant(),
                StringCase.Snake => ToSnakeCase(s),
                StringCase.UpperSnake => ToUpperSnakeCase(s),
                _ => throw new NotImplementedException(),
            };
        }

        /// <summary>
        /// Возвращает часть строки в диапазоне [startIndex..endIndex]. Работает как string.Substring(s, startIndex, endIndex -
        /// startIndex + 1).
        /// </summary>
        /// <param name="s">Исходная строка.</param>
        /// <param name="startIndex">Начальная позиция (включительно).</param>
        /// <param name="endIndex">Конечная позиция (включительно).</param>
        /// <returns>System.String.</returns>
        /// <exception cref="ArgumentNullException">s.</exception>
        /// <exception cref="ArgumentOutOfRangeException">startIndex.</exception>
        /// <exception cref="ArgumentOutOfRangeException">endIndex.</exception>
        public static string Crop(string s, int startIndex, int endIndex)
        {
            if (s == null)
            {
                throw new ArgumentNullException(nameof(s));
            }

            if (startIndex < 0 || startIndex > s.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            }

            if (endIndex < startIndex || endIndex > s.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(endIndex));
            }

            return s.Substring(startIndex, endIndex - startIndex + 1);
        }

        /// <summary>
        /// Удаляет часть строки в диапазоне [startIndex..endIndex]. Работает как s.Substring(0, startIndex) +
        /// s.Substring(endIndex + 1);.
        /// </summary>
        /// <param name="s">Исходная строка.</param>
        /// <param name="startIndex">Начальная позиция (включительно).</param>
        /// <param name="endIndex">Конечная позиция (включительно).</param>
        /// <returns>System.String.</returns>
        /// <exception cref="ArgumentNullException">s.</exception>
        /// <exception cref="ArgumentOutOfRangeException">startIndex.</exception>
        /// <exception cref="ArgumentOutOfRangeException">endIndex.</exception>
        public static string Cut(string s, int startIndex, int endIndex)
        {
            if (s == null)
            {
                throw new ArgumentNullException(nameof(s));
            }

            if (startIndex < 0 || startIndex > s.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            }

            if (endIndex < startIndex || endIndex > s.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(endIndex));
            }

            return s.Substring(0, startIndex) + s.Substring(endIndex + 1);
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
        /// <description><see cref="EscapeMode.None"/> — строка возвращается без изменений.</description>
        /// </item>
        /// <item>
        /// <description><see cref="EscapeMode.Url"/> — URL-кодирование с использованием <see cref="Uri.EscapeDataString(string)"/>.</description>
        /// </item>
        /// <item>
        /// <description><see cref="EscapeMode.Sql"/> — экранирование одинарных кавычек для SQL (замена <c>'</c> на <c>''</c>).</description>
        /// </item>
        /// <item>
        /// <description><see cref="EscapeMode.Json"/> — экранирование специальных символов согласно спецификации JSON.</description>
        /// </item>
        /// <item>
        /// <description><see cref="EscapeMode.XmlText"/> — экранирование строки для использования в тексте XML-узла.</description>
        /// </item>
        /// <item>
        /// <description><see cref="EscapeMode.XmlAttribute"/> — экранирование строки для использования в значении XML-атрибута.</description>
        /// </item>
        /// <item>
        /// <description><see cref="EscapeMode.Csv"/> — экранирование строки по правилам CSV (RFC 4180).</description>
        /// </item>
        /// <item>
        /// <description><see cref="EscapeMode.CSharp"/> — экранирование строки для безопасного использования в строковом литерале C#.</description>
        /// </item>
        /// <item>
        /// <description><see cref="EscapeMode.Base64"/> — преобразование строки в Base64 (кодировка UTF-8).</description>
        /// </item>
        /// </list>
        /// </param>
        /// <returns>
        /// Экранированная строка в соответствии с выбранным режимом.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Выбрасывается, если передано неподдерживаемое значение <paramref name="mode"/>.
        /// </exception>
        public static string EscapeString(string value, EscapeMode mode)
        {
            if (value == null)
            {
                return null;
            }

            return mode switch
            {
                EscapeMode.None => value,
                EscapeMode.Url => Uri.EscapeDataString(value),
                EscapeMode.Sql => value.Replace("'", "''"),
                EscapeMode.Json => EscapeJson(value),
                EscapeMode.XmlText => EscapeXmlText(value),
                EscapeMode.XmlAttribute => EscapeXmlAttribute(value),
                EscapeMode.Csv => EscapeCsv(value),
                EscapeMode.CSharp => EscapeCSharp(value),
                EscapeMode.Base64 => Convert.ToBase64String(Encoding.UTF8.GetBytes(value)),
                _ => throw new ArgumentOutOfRangeException(nameof(mode)),
            };
        }

        /// <summary>
        /// Извлекает строку формата из составного форматного выражения.
        /// </summary>
        /// <param name="format">
        /// Строка формата, например <c>"{0:yyyy-MM-dd}"</c> или уже готовый формат <c>"yyyy-MM-dd"</c>.
        /// </param>
        /// <returns>
        /// Строка формата без обёртки составного форматирования.
        /// <para/>
        /// Если входная строка имеет вид <c>"{0:...}"</c>, возвращается содержимое после двоеточия.
        /// В противном случае возвращается исходная строка.
        /// <para/>
        /// Если <paramref name="format"/> равен <c>null</c> или пустой строке — возвращается <c>null</c>.
        /// </returns>
        /// <remarks>
        /// Метод выполняет простое извлечение подстроки и не поддерживает сложные случаи,
        /// такие как вложенные форматные выражения или экранирование фигурных скобок.
        /// </remarks>
        /// <example>
        /// <code>
        /// ExtractFormat("{0:yyyy-MM-dd}") → "yyyy-MM-dd"
        /// ExtractFormat("HH:mm:ss") → "HH:mm:ss"
        /// ExtractFormat(null) → null
        /// </code>
        /// </example>
        public static string ExtractFormat(string format)
        {
            if (string.IsNullOrEmpty(format))
            {
                return null;
            }

            // "{0:yyyy-MM-dd}" → "yyyy-MM-dd"
            if (format.Length > 4 && format[0] == '{')
            {
                var colonIndex = format.IndexOf(':');
                var endIndex = format.LastIndexOf('}');

                if (colonIndex >= 0 && endIndex > colonIndex)
                {
                    return format.Substring(colonIndex + 1, endIndex - colonIndex - 1);
                }
            }

            return format;
        }

        /// <summary>
        /// Разворачивает иерархию токенов в плоский список.
        /// </summary>
        /// <param name="tokens">Корневые токены.</param>
        /// <param name="predicate">Необязательный фильтр. Если указан — возвращаются только те токены, для которых predicate ==
        /// true.</param>
        /// <returns>Плоский список токенов.</returns>
        /// <example>
        /// Пример:
        /// <code>
        /// var s = "Hello (one(two))";
        /// var tokens = StringTokenizer.GetTokens(s, ("(", ")")).Flatten();
        /// // tokens[0] -&gt; "(one(two))"
        /// // tokens[1] -&gt; "(two)"
        /// </code></example>
        public static List<Token> Flatten(IEnumerable<Token> tokens, Func<Token, bool> predicate = null)
        {
            var result = new List<Token>();

            void Recurse(Token t)
            {
                if (predicate == null || predicate(t))
                {
                    result.Add(t);
                }

                foreach (var child in t.Children)
                {
                    Recurse(child);
                }
            }

            foreach (var t in tokens)
            {
                Recurse(t);
            }

            return result;
        }

        /// <summary>
        /// Возвращает строку из заданного списка, наиболее похожую на входную строку,
        /// используя указанный метод нечеткого сравнения.
        /// </summary>
        /// <param name="input">Входная строка для сравнения.</param>
        /// <param name="predefinedStrings">Массив строк, с которыми выполняется сравнение.</param>
        /// <param name="compareMethod">Метод нечеткого сравнения строк.</param>
        /// <param name="caseSensitive">Определяет, учитывается ли регистр символов.</param>
        /// <param name="distanceThreshold">
        /// Порог допустимого расстояния. Если минимальное расстояние превышает это значение,
        /// будет возвращено <c>null</c>.
        /// </param>
        /// <returns>
        /// Наиболее похожая строка из массива или <c>null</c>, если подходящая строка не найдена.
        /// </returns>
        public static string GetClosestMatch(string input, string[] predefinedStrings, FuzzyCompareMethod compareMethod, bool caseSensitive = false, double distanceThreshold = int.MaxValue)
        {
            return GetClosestMatch(input, predefinedStrings, compareMethod, out _, caseSensitive, distanceThreshold);
        }

        /// <summary>
        /// Возвращает строку из заданного списка, наиболее похожую на входную строку,
        /// и дополнительно возвращает минимальное найденное расстояние.
        /// </summary>
        /// <param name="input">Входная строка для сравнения.</param>
        /// <param name="predefinedStrings">Массив строк, с которыми выполняется сравнение.</param>
        /// <param name="compareMethod">Метод нечеткого сравнения строк.</param>
        /// <param name="minDistance">Минимальное расстояние между входной строкой и найденным совпадением.</param>
        /// <param name="caseSensitive">Определяет, учитывается ли регистр символов.</param>
        /// <param name="distanceThreshold">
        /// Порог допустимого расстояния. Если минимальное расстояние превышает это значение,
        /// будет возвращено <c>null</c>.
        /// </param>
        /// <returns>
        /// Наиболее похожая строка из массива или <c>null</c>, если подходящая строка не найдена.
        /// </returns>
        public static string GetClosestMatch(string input, string[] predefinedStrings, FuzzyCompareMethod compareMethod, out double minDistance, bool caseSensitive = false, double distanceThreshold = int.MaxValue)
        {
            string closestMatch = null;
            minDistance = int.MaxValue;
            foreach (var str in predefinedStrings)
            {
                double distance = int.MaxValue;
                switch (compareMethod)
                {
                    case FuzzyCompareMethod.Levenshtein:
                        distance = LevenshteinDistance(input, str, caseSensitive);
                        break;

                    case FuzzyCompareMethod.JaroWinkler:
                        distance = JaroWinklerDistance(input, str, caseSensitive);
                        break;
                }

                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestMatch = str;
                }
            }

            return minDistance <= distanceThreshold ? closestMatch : null;
        }

        /// <summary>
        /// Находит участки исходного текста, не покрытые ни одним токеном,
        /// и добавляет для них специальные «незамапленные» (plain) токены.
        /// </summary>
        /// <param name="tokens">
        /// Коллекция токенов, для которых необходимо найти непокрытые участки текста.
        /// </param>
        /// <param name="setTag">
        /// Делегат, используемый для установки тега создаваемым токенам.
        /// </param>
        /// <param name="transformer">
        /// Делегат для преобразования текстового содержимого токена.
        /// </param>
        /// <remarks>
        /// Метод рекурсивно обрабатывает дерево токенов.
        /// Для каждого уровня анализируются разрывы между соседними токенами
        /// и границы родительского текста. Если обнаруживается участок,
        /// не принадлежащий ни одному токену, создаётся новый токен
        /// и вставляется в соответствующее место.
        /// </remarks>
        public static void GetNotMatchedTokens(IEnumerable<Token> tokens, Func<Token, object> setTag, Func<Token, string> transformer)
        {
            if (tokens == null)
            {
                return;
            }

            var tokensArray = tokens.ToList();
            foreach (var t in tokensArray)
            {
                if (t.Parent == null)
                {
                    if (t.Children.Any())
                    {
                        GetNotMatchedTokens(t.Children, setTag, transformer);
                    }

                    if (t.SourceStart > 0 && t.Previous == null)
                    {
                        var plainToken = new Token(t.Source, 0, t.SourceStart - 1, setTag, transformer);
                        t.InsertBefore(plainToken);
                        continue;
                    }

                    if (t.Previous != null && t.SourceStart - t.Previous.SourceEnd > 1)
                    {
                        var plainToken = new Token(t.Source, t.Previous.SourceEnd + 1, t.SourceStart - 1, setTag, transformer);
                        t.InsertBefore(plainToken);
                        continue;
                    }

                    if (t.Next == null && t.SourceEnd < t.Source.Length - 1)
                    {
                        var plainToken = new Token(t.Source, t.SourceEnd + 1, t.Source.Length - 1, setTag, transformer);
                        t.InsertAfter(plainToken);
                    }
                }
                else
                {
                    if (t.Children.Any())
                    {
                        GetNotMatchedTokens(t.Children, setTag, transformer);
                    }

                    if (t.SourceStart - t.Parent.ParentStart > 1 && t.Previous == null)
                    {
                        var plainToken = new Token(t.Parent.Body, t.Parent.Prefix.Length, t.ParentStart - 1, setTag, transformer);
                        t.InsertBefore(plainToken);
                    }

                    if (t.Previous != null && !t.Previous.IsNotMatched && t.ParentStart - t.Previous.ParentEnd > 1)
                    {
                        var plainToken = new Token(t.Parent.Body, t.Previous.ParentEnd + 1, t.ParentStart - 1, setTag, transformer);
                        t.InsertBefore(plainToken);
                    }

                    if (t.Next == null && t.Parent.ParentEnd - t.SourceEnd > 1)
                    {
                        var plainToken = new Token(t.Parent.Body, t.ParentEnd + 1, t.Parent.Body.Length - t.Parent.Suffix.Length - 1, setTag, transformer);
                        t.InsertAfter(plainToken);
                    }
                }
            }
        }

        /// <summary>
        /// Получает список токенов по нескольким маскам.
        /// Маски задаются как кортеж (Prefix, Suffix, ContentTransformer).
        /// </summary>
        /// <param name="input">Входная строка.</param>
        /// <param name="prefix">Префикс токена.</param>
        /// <param name="suffix">Суффикс токена.</param>
        /// <param name="notMatchedAsTokens">if set to <c>true</c> [not matched as tokens].</param>
        /// <param name="contentTransformer">The content transformer.</param>
        /// <returns>Список корневых токенов. Для получения всех токенов в виде массива использовать
        /// <see cref="Flatten(IEnumerable{Token}, Func{Token, bool})" />.</returns>
        /// <example>
        /// Пример:
        /// <code>
        /// var s = "Hello ( one ( two ( three ) ) )";
        /// var tokens = StringTokenizer.GetTokens(
        /// s,
        /// ("(", ")", t =&gt; t.Text.Trim())
        /// ).Flatten();
        /// var c1 = tokens[0].Content; // "one two three"
        /// var c2 = tokens[1].Content; // "two three"
        /// var c3 = tokens[2].Content; // "three"
        /// </code></example>
        public static List<Token> GetTokens(string input, string prefix, string suffix, bool notMatchedAsTokens, Func<Token, string> contentTransformer = null) => GetTokens(input, notMatchedAsTokens, (prefix, suffix, contentTransformer));

        /// <summary>
        /// Получает список токенов по нескольким маскам.
        /// Маски задаются как кортеж (Prefix, Suffix, ContentTransformer).
        /// </summary>
        /// <param name="input">Входная строка.</param>
        /// <param name="notMatchedAsTokens">if set to <c>true</c> [not matched as tokens].</param>
        /// <param name="tokenMasks">Маски токенов (префикс, суффикс, функция сериализации).</param>
        /// <returns>Список корневых токенов. Для получения всех токенов в виде массива использовать
        /// <see cref="Flatten(IEnumerable{Token}, Func{Token, bool})" />.</returns>
        /// <example>
        /// Пример:
        /// <code>
        /// var s = "Hello ( one ( two ( three ) ) )";
        /// var tokens = StringTokenizer.GetTokens(
        /// s,
        /// ("(", ")", t =&gt; t.Text.Trim())
        /// ).Flatten();
        /// var c1 = tokens[0].Content; // "one two three"
        /// var c2 = tokens[1].Content; // "two three"
        /// var c3 = tokens[2].Content; // "three"
        /// </code></example>
        public static List<Token> GetTokens(string input, bool notMatchedAsTokens, params (string Prefix, string Suffix)[] tokenMasks) => GetTokens(input, notMatchedAsTokens, tokenMasks.Select(x => (x.Prefix, x.Suffix, (Func<Token, string>)null)).ToArray());

        /// <summary>
        /// Получает список токенов по нескольким маскам.
        /// Маски задаются как кортеж (Prefix, Suffix, ContentTransformer).
        /// </summary>
        /// <param name="input">Входная строка.</param>
        /// <param name="flatten">if set to <c>true</c> [flatten].</param>
        /// <param name="notMatchedAsTokens">if set to <c>true</c> [not matched as tokens].</param>
        /// <param name="tokenMasks">The token masks.</param>
        /// <returns>Список корневых токенов. Для получения всех токенов в виде массива использовать
        /// <see cref="Flatten(IEnumerable{Token}, Func{Token, bool})" />.</returns>
        public static List<Token> GetTokens(string input, bool flatten, bool notMatchedAsTokens, params (string Prefix, string Suffix, Func<Token, string> ContentTransformer)[] tokenMasks)
        {
            var tokens = GetTokens(input, notMatchedAsTokens, tokenMasks);
            return flatten ? Flatten(tokens) : tokens;
        }

        /// <summary>
        /// the tokens.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <param name="notMatchedAsTokens">if set to <c>true</c> [not matched as tokens].</param>
        /// <param name="tokenMasks">The token masks.</param>
        /// <returns>List&lt;Token&gt;.</returns>
        public static List<Token> GetTokens(string input, bool notMatchedAsTokens, params (string Prefix, string Suffix, Func<Token, string> ContentTransformer)[] tokenMasks) => GetTokens(input, tokenMasks.Select(x => new TokenMask(x.Prefix, x.Suffix, null, x.ContentTransformer)).ToArray(), notMatchedAsTokens);

        /// <summary>
        /// Получает список токенов по нескольким маскам.
        /// Маски задаются как кортеж (Prefix, Suffix, ContentTransformer).
        /// </summary>
        /// <param name="input">Входная строка.</param>
        /// <param name="tokenMasks">Маски токенов (префикс, суффикс, функция сериализации).</param>
        /// <param name="notMatchedAsTokens">if set to <c>true</c> [not matched as tokens].</param>
        /// <param name="notMatchedTokenSetTag">The not matched token set tag.</param>
        /// <param name="notMatchedContentTransformer">Обработчик содержимого токена.</param>
        /// <returns>List&lt;Token&gt;.</returns>
        /// <exception cref="InvalidOperationException">Token with Prefix='{tm.Prefix}' and Suffix='{tm.Suffix}' is not allowed to be a child of another token.</exception>
        /// <exception cref="InvalidOperationException">Token with Prefix='{tm.Prefix}' and Suffix='{tm.Suffix}' is not allowed to be a next of {prevToken} token.</exception>
        public static List<Token> GetTokens(
            string input,
            IEnumerable<TokenMask> tokenMasks,
            bool notMatchedAsTokens = false,
            Func<Token, object> notMatchedTokenSetTag = null,
            Func<Token, string> notMatchedContentTransformer = null)
        {
            Token.IdInternal = 1;
            var result = new List<Token>();
            var stack = new Stack<(Token Token, string Prefix, string Suffix, Func<Token, string> ContentTransformer)>();

            var masks = tokenMasks.OrderByDescending(m => m.Prefix.Length)
                .Concat(tokenMasks.SelectMany(x => x.AllowedChildrenMasks))
                .Distinct()
                .ToArray();

            var i = 0;
            var length = input.Length;

            while (i < length)
            {
                var matched = false;
                var curChar = input[i];

                foreach (var tm in masks)
                {
                    if (curChar != tm.Prefix[0])
                    {
                        continue;
                    }

                    if (i + tm.Prefix.Length <= length &&
                        string.Compare(input, i, tm.Prefix, 0, tm.Prefix.Length, StringComparison.Ordinal) == 0)
                    {
                        if (stack.Count > 0 && tm.Prefix == tm.Suffix && stack.Peek().Prefix == tm.Prefix)
                        {
                            break;
                        }

                        if (stack.Count > 0)
                        {
                            var topToken = stack.Peek().Token;

                            if (!topToken.Mask.AllowChildrenTokens)
                            {
                                if (tm.ThrowExceptionOnNotAllowedToken)
                                {
                                    throw new InvalidOperationException($"Token with Prefix='{tm.Prefix}' and Suffix='{tm.Suffix}' is not allowed to be a child of another token.");
                                }
                                else
                                {
                                    break;
                                }
                            }

                            if (topToken.Mask.AllowedChildrenMasks.Count > 0 &&
                                !topToken.Mask.AllowedChildrenMasks.Contains(tm))
                            {
                                if (tm.ThrowExceptionOnNotAllowedToken)
                                {
                                    throw new InvalidOperationException($"Token with Prefix='{tm.Prefix}' and Suffix='{tm.Suffix}' is not allowed to be a child of another token.");
                                }
                                else
                                {
                                    break;
                                }
                            }
                        }

                        var prevToken = result.LastOrDefault();
                        if (prevToken?.Mask?.AllowedNextMasks.Count > 0 &&
                            !prevToken.Mask.AllowedNextMasks.Contains(tm))
                        {
                            if (tm.ThrowExceptionOnNotAllowedToken)
                            {
                                throw new InvalidOperationException($"Token with Prefix='{tm.Prefix}' and Suffix='{tm.Suffix}' is not allowed to be a next of {prevToken} token.");
                            }
                            else
                            {
                                break;
                            }
                        }

                        // === МГНОВЕННЫЙ ТОКЕН ===
                        if (tm.Suffix == null)
                        {
                            var instantToken = new Token
                            {
                                SourceStart = i,
                                SourceEnd = i + tm.Prefix.Length - 1,
                                Source = input,
                                Prefix = tm.Prefix,
                                Suffix = null,
                                Body = tm.Prefix,
                                Text = string.Empty,
                                Parent = stack.Count == 0 ? null : stack.Peek().Token,
                            };

                            instantToken.ParentStart = instantToken.Parent == null
                                ? 0
                                : instantToken.SourceStart - instantToken.Parent.SourceStart;

                            instantToken.ParentEnd = instantToken.Parent == null
                                ? tm.Prefix.Length
                                : instantToken.SourceEnd - instantToken.Parent.SourceStart;

                            if (tm.ContentTransformer != null)
                            {
                                instantToken.ContentTransformers.Add(tm.ContentTransformer);
                            }

                            if (instantToken.Parent != null)
                            {
                                var list = instantToken.Parent.ChildrenInternal;

                                if (list.Count > 0)
                                {
                                    var prev = list[list.Count - 1];
                                    prev.Next = instantToken;
                                    instantToken.Previous = prev;
                                }

                                list.Add(instantToken);
                            }
                            else
                            {
                                if (result.Count > 0)
                                {
                                    var prev = result[result.Count - 1];
                                    prev.Next = instantToken;
                                    instantToken.Previous = prev;
                                }

                                result.Add(instantToken);
                            }

                            instantToken.Tag = tm.SetTag?.Invoke(instantToken);
                            instantToken.Mask = tm;

                            i += tm.Prefix.Length;
                            matched = true;
                            break;
                        }

                        var token = new Token
                        {
                            SourceStart = i,
                            Parent = stack.Count == 0 ? null : stack.Peek().Token,
                            Source = input,
                            Prefix = tm.Prefix,
                            Suffix = tm.Suffix,
                            Mask = tm,
                        };

                        token.ParentStart = i - (token.Parent?.SourceStart ?? 0);

                        if (tm.ContentTransformer != null)
                        {
                            token.ContentTransformers.Add(tm.ContentTransformer);
                        }

                        stack.Push((token, tm.Prefix, tm.Suffix, tm.ContentTransformer));
                        i += tm.Prefix.Length;
                        matched = true;
                        break;
                    }
                }

                if (matched)
                {
                    continue;
                }

                // === Закрытие токена ===
                if (stack.Count > 0)
                {
                    var top = stack.Peek();
                    var topToken = top.Token;
                    var topPrefix = top.Prefix;
                    var topSuffix = top.Suffix;

                    if (i + topSuffix.Length <= length &&
                        string.Compare(input, i, topSuffix, 0, topSuffix.Length, StringComparison.Ordinal) == 0)
                    {
                        stack.Pop();

                        topToken.SourceEnd = i + topSuffix.Length - 1;
                        topToken.ParentEnd = topToken.Parent == null
                            ? i + 1
                            : topToken.SourceEnd - topToken.Parent.SourceStart;

                        var body = input.Substring(topToken.SourceStart, topToken.SourceEnd - topToken.SourceStart + 1);
                        topToken.Body = body;

                        var textStart = topToken.SourceStart + topPrefix.Length;
                        var textLength = body.Length - topPrefix.Length - topSuffix.Length;
                        topToken.Text = textLength > 0
                            ? input.Substring(textStart, textLength)
                            : string.Empty;

                        if (topToken.Parent != null)
                        {
                            var list = topToken.Parent.ChildrenInternal;

                            if (list.Count > 0)
                            {
                                var prev = list[list.Count - 1];
                                prev.Next = topToken;
                                topToken.Previous = prev;
                            }

                            list.Add(topToken);
                        }
                        else
                        {
                            if (result.Count > 0)
                            {
                                var prev = result[result.Count - 1];
                                prev.Next = topToken;
                                topToken.Previous = prev;
                            }

                            result.Add(topToken);
                        }

                        topToken.Tag = topToken.Mask.SetTag?.Invoke(topToken);

                        i += topSuffix.Length;
                        continue;
                    }
                }

                i++;
            }

            if (notMatchedAsTokens)
            {
                GetNotMatchedTokens(result, notMatchedTokenSetTag, notMatchedContentTransformer);
            }

            return result;
        }

        /// <summary>
        /// Выполняет поиск подстроки в строке, начиная с указанной позиции и
        /// перемещаясь по строке с заданным шагом с учетом правила сравнения строк.
        /// </summary>
        /// <param name="s">Строка, в которой выполняется поиск.</param>
        /// <param name="subString">Подстрока, которую необходимо найти.</param>
        /// <param name="startIndex">Индекс, с которого начинается поиск.</param>
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
        /// Индекс найденного вхождения <paramref name="subString"/>.
        /// Если совпадение не найдено или <paramref name="startIndex"/> находится
        /// вне диапазона строки, возвращается <c>-1</c>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Выбрасывается, если <paramref name="s"/> или <paramref name="subString"/> равны <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Выбрасывается, если <paramref name="step"/> равен <c>0</c>.
        /// </exception>
        /// <remarks>
        /// Позволяет выполнять поиск с произвольным шагом:
        /// <list type="bullet">
        /// <item><description><c>step = 1</c> — обычный поиск слева направо.</description></item>
        /// <item><description><c>step = -1</c> — поиск справа налево.</description></item>
        /// <item><description><c>step = n</c> — проверка каждой <c>n</c>-й позиции.</description></item>
        /// </list>
        /// Если <paramref name="subString"/> является пустой строкой,
        /// возвращается <paramref name="startIndex"/>.
        /// </remarks>
        public static int IndexOf(string s, string subString, int startIndex, int step, StringComparison comparison = StringComparison.Ordinal)
        {
            if (s == null)
            {
                throw new ArgumentNullException(nameof(s));
            }

            if (subString == null)
            {
                throw new ArgumentNullException(nameof(subString));
            }

            if (step == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(step));
            }

            if (subString.Length == 0)
            {
                return startIndex;
            }

            var len = s.Length;
            var subLen = subString.Length;

            if (startIndex < 0 || startIndex >= len)
            {
                return -1;
            }

            if (step > 0)
            {
                for (var i = startIndex; i <= len - subLen; i += step)
                {
                    if (string.Compare(s, i, subString, 0, subLen, comparison) == 0)
                    {
                        return i;
                    }
                }
            }
            else
            {
                for (var i = startIndex; i >= 0; i += step)
                {
                    if (i + subLen > len)
                    {
                        continue;
                    }

                    if (string.Compare(s, i, subString, 0, subLen, comparison) == 0)
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

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
        /// <item><description>строка не равна <c>null</c> и не пуста;</description></item>
        /// <item><description>после обрезки пробельных символов длина строки не менее 2 символов;</description></item>
        /// <item><description>строка начинается с символа '{' и заканчивается '}', либо начинается с '[' и заканчивается ']'.</description></item>
        /// </list>
        /// Метод не проверяет корректность структуры, экранирование строк,
        /// соответствие стандарту JSON и вложенность элементов.
        /// Для полноценной проверки рекомендуется использовать сторонние JSON-парсеры.
        /// </remarks>
        public static bool IsJson(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                return false;
            }

            s = TrimWhitespaces(s);

            // JSON всегда начинается с { или [
            if (s.Length < 2)
            {
                return false;
            }

            var first = s[0];
            var last = s[s.Length - 1];

            if (!((first == '{' && last == '}') ||
                  (first == '[' && last == ']')))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Определяет является ли символ переносом строки. Идет проверка на символы \r, \n, \u0085, \u2028, \u2029.
        /// </summary>
        /// <param name="c">Символ для проверки.</param>
        /// <returns>Результат проверки.</returns>
        public static bool IsNewLineChar(char c)
        {
            return c == '\r'
                || c == '\n'
                || c == '\u0085' // NEXT LINE
                || c == '\u2028' // LINE SEPARATOR
                || c == '\u2029'; // PARAGRAPH SEPARATOR
        }

        /// <summary>
        /// Является ли символ непечатаемым.
        /// </summary>
        /// <param name="c">Символ.</param>
        /// <returns>Результат проверки.</returns>
        public static bool IsWhiteSpaceChar(char c)
        {
            return char.IsWhiteSpace(c) || WhitespaceChars.Contains(c);
        }

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
        /// <item><description>строка не равна <c>null</c> и не пуста;</description></item>
        /// <item><description>после обрезки пробельных символов строка начинается с символа '&lt;';</description></item>
        /// <item><description>минимальная допустимая длина XML (&lt;a/&gt;);</description></item>
        /// <item><description>исключаются HTML-комментарии и объявления DOCTYPE без корневого элемента;</description></item>
        /// <item><description>наличие закрывающего символа '&gt;'.</description></item>
        /// </list>
        /// Для полной проверки корректности XML рекомендуется использовать
        /// <see cref="Xml.XmlReader"/> или <see cref="Xml.Linq.XDocument"/>.
        /// </remarks>
        public static bool IsXml(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                return false;
            }

            s = TrimWhitespaces(s);

            // XML всегда начинается с '<'
            if (s[0] != '<')
            {
                return false;
            }

            // Минимальная длина: <a/>
            if (s.Length < 4)
            {
                return false;
            }

            // Явно отсекаем HTML-комментарии и DOCTYPE без корневого элемента
            if (s.StartsWith("<!--", StringComparison.Ordinal) ||
                s.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Проверка наличия закрывающего '>'
            var close = s.IndexOf('>');
            if (close < 0)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Приводит строку формата к составному формату вида <c>"{index:format}"</c>.
        /// </summary>
        /// <param name="format">
        /// Строка формата (например, <c>"yyyy-MM-dd"</c> или <c>"{0:yyyy-MM-dd}"</c>).
        /// </param>
        /// <param name="index">
        /// Индекс аргумента в составной строке форматирования.
        /// По умолчанию — <c>0</c>.
        /// </param>
        /// <returns>
        /// Строка в виде составного форматного выражения.
        /// <para/>
        /// Если <paramref name="format"/> пустая или состоит только из пробелов,
        /// возвращается <c>"{index}"</c>.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Если входная строка уже содержит формат, но не начинается с <c>"{index:"</c>,
        /// префикс будет добавлен.
        /// </para>
        /// <para>
        /// Если строка не заканчивается символом <c>'}'</c>, он будет добавлен.
        /// </para>
        /// <para>
        /// Метод не выполняет строгую валидацию формата и может некорректно обработать
        /// сложные или вложенные форматные выражения.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// NormalizeFormat("yyyy-MM-dd") → "{0:yyyy-MM-dd}"
        /// NormalizeFormat("{0:HH:mm}") → "{0:HH:mm}"
        /// NormalizeFormat(null) → "{0}"
        /// NormalizeFormat("HH:mm", 1) → "{1:HH:mm}"
        /// </code>
        /// </example>
        public static string NormalizeFormat(string format, int index = 0)
        {
            if (string.IsNullOrWhiteSpace(format))
            {
                return $"{{{index}}}";
            }

            if (!format.StartsWith($"{{{index}:"))
            {
                format = $"{{{index}:" + format;
            }

            if (!format.EndsWith("}"))
            {
                format += "}";
            }

            return format;
        }

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
        public static string NormalizeWhiteSpaces(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return s;
            }

            var sb = new StringBuilder(s.Length);
            var inWhitespace = false;

            foreach (var c in s)
            {
                if (IsWhiteSpaceChar(c))
                {
                    inWhitespace = true;
                }
                else
                {
                    if (inWhitespace && sb.Length > 0)
                    {
                        sb.Append(' ');
                    }

                    sb.Append(c);
                    inWhitespace = false;
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Возвращает строку, повторенную указанное количество раз.
        /// </summary>
        /// <param name="str">Исходная строка.</param>
        /// <param name="count">Количество повторений.</param>
        /// <returns>Новая строка, состоящая из повторений исходной строки.</returns>
        /// <exception cref="ArgumentNullException">str.</exception>
        /// <exception cref="ArgumentOutOfRangeException">count - Количество повторений не может быть отрицательным.</exception>
        public static string RepeatString(string str, int count)
        {
            if (str == null)
            {
                throw new ArgumentNullException(nameof(str));
            }

            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), @"Количество повторений не может быть отрицательным.");
            }

            if (count == 0 || str.Length == 0)
            {
                return string.Empty;
            }

            // Можно оптимизировать через StringBuilder
            var sb = new StringBuilder(str.Length * count);
            for (var i = 0; i < count; i++)
            {
                sb.Append(str);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Заменяет все вхождения указанных подстрок в исходной строке
        /// на заданное значение с учетом правила сравнения.
        /// </summary>
        /// <param name="s">
        /// Исходная строка.
        /// Если значение равно <c>null</c>, метод возвращает <c>null</c>.
        /// </param>
        /// <param name="replacement">
        /// Строка, на которую выполняется замена.
        /// Если значение равно <c>null</c>, используется пустая строка.
        /// </param>
        /// <param name="comparison">
        /// Правило сравнения строк (<see cref="StringComparison"/>),
        /// определяющее чувствительность к регистру и культуру сравнения.
        /// </param>
        /// <param name="replaceText">
        /// Массив подстрок, которые необходимо заменить.
        /// Пустые или <c>null</c> элементы массива игнорируются.
        /// </param>
        /// <returns>
        /// Новая строка, в которой все вхождения каждой из указанных подстрок
        /// заменены на <paramref name="replacement"/>.
        /// Если <paramref name="replaceText"/> не задан или пуст,
        /// возвращается исходная строка.
        /// </returns>
        /// <remarks>
        /// Замена выполняется последовательно для каждой подстроки из <paramref name="replaceText"/>.
        /// Результат предыдущей замены используется как вход для следующей.
        /// </remarks>
        public static string Replace(string s, string replacement, StringComparison comparison, params string[] replaceText)
        {
            if (s == null)
            {
                return null;
            }

            if (replaceText == null || replaceText.Length == 0)
            {
                return s;
            }

            replacement = replacement ?? string.Empty;

            var result = s;

            foreach (var text in replaceText)
            {
                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                result = ReplaceInternal(result, text, replacement, comparison);
            }

            return result;
        }

        /// <summary>
        /// Заменяет часть строки в диапазоне [startIndex..endIndex] на указанную строку.
        /// </summary>
        /// <param name="s">Исходная строка.</param>
        /// <param name="startIndex">Начальная позиция (включительно).</param>
        /// <param name="endIndex">Конечная позиция (включительно).</param>
        /// <param name="replaceString">Строка для замены.</param>
        /// <returns>Новая строка с заменой.</returns>
        /// <exception cref="ArgumentNullException">s.</exception>
        /// <exception cref="ArgumentOutOfRangeException">startIndex.</exception>
        /// <exception cref="ArgumentOutOfRangeException">endIndex.</exception>
        public static string Replace(string s, int startIndex, int endIndex, string replaceString)
        {
            if (s == null)
            {
                throw new ArgumentNullException(nameof(s));
            }

            if (startIndex < 0 || startIndex > s.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            }

            if (endIndex < startIndex || endIndex > s.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(endIndex));
            }

            return s.Substring(0, startIndex)
                   + replaceString
                   + s.Substring(endIndex + 1);
        }

        /// <summary>
        /// Разбивает строку на подстроки по одному или нескольким указанным разделителям.
        /// </summary>
        /// <param name="s">Исходная строка для разбиения.</param>
        /// <param name="options">Настройки.</param>
        /// <param name="splitBy">Массив строк-разделителей. Порядок важен, выбирается ближайший к текущей позиции.</param>
        /// <param name="dontSplitBetween">Не разделять строку, между префиксом и суфиксом.</param>
        /// <returns>
        /// Массив подстрок, полученных после разбиения. Если строка <c>null</c> или пустая, возвращается пустой массив.
        /// Если <paramref name="splitBy"/> пустой или <c>null</c>, возвращается массив, содержащий исходную строку.
        /// </returns>
        /// <remarks>
        /// <para>Метод выполняет последовательный поиск ближайшего разделителя и делит строку по нему.</para>
        /// <para>Подстроки между разделителями включаются в результат, разделители сами не включаются.</para>
        /// <para>Поддерживается несколько разделителей произвольной длины.</para>
        /// </remarks>
        public static string[] SplitBy(string s, StringSplitOptions options, string[] splitBy, params (string Prefix, string Suffix)[] dontSplitBetween)
        {
            if (string.IsNullOrEmpty(s))
            {
                return Array.Empty<string>();
            }

            if (splitBy == null || splitBy.Length == 0)
            {
                return [s];
            }

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
                    {
                        continue;
                    }

                    var searchPos = pos;

                    while (true)
                    {
                        var idx = s.IndexOf(sep, searchPos, StringComparison.Ordinal);
                        if (idx < 0)
                        {
                            break;
                        }

                        if (!IsInsideProtectedRange(s, idx, dontSplitBetween))
                        {
                            if (nextPos < 0 || idx < nextPos)
                            {
                                nextPos = idx;
                                sepLen = sep.Length;
                            }

                            break;
                        }

                        searchPos = idx + sep.Length;
                    }
                }

                var partLen = (nextPos < 0 ? len : nextPos) - pos;

                if (partLen > 0 || options != StringSplitOptions.RemoveEmptyEntries)
                {
                    result.Add(s.Substring(pos, partLen));
                }

                if (nextPos < 0)
                {
                    break;
                }

                pos = nextPos + sepLen;
            }

            return result.ToArray();
        }

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
        public static List<T> SplitToList<T>(string s, params string[] propertyMap)
        {
            return SplitToList<T>(s, propertyMap, DefaultColumnSeparators, DefaultLineSeparators);
        }

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
        public static List<T> SplitToList<T>(string s, string[] propertyMap, string[] columnSeparators, string[] lineSeparators)
        {
            var result = new List<T>();

            if (columnSeparators == null)
            {
                columnSeparators = StringHelper.DefaultColumnSeparators;
            }

            if (lineSeparators == null)
            {
                lineSeparators = StringHelper.DefaultLineSeparators;
            }

            if (propertyMap == null)
            {
                propertyMap = Array.Empty<string>();
            }

            var typeCache = MemberCache.Get(typeof(T));
            var lines = SplitBy(s, StringSplitOptions.RemoveEmptyEntries, lineSeparators);
            var props = propertyMap.Length > 0 ? typeCache.Properties.Where(x => propertyMap.Contains(x.Name)).ToArray() : typeCache.PublicBasicProperties.ToArray();
            if (props.Length == 0)
            {
                props = propertyMap.Length > 0 ? typeCache.Fields.Where(x => propertyMap.Contains(x.Name)).ToArray() : typeCache.PublicFields.ToArray();
            }

            if (props.Length == 0)
            {
                throw new InvalidOperationException($"Не найдено публичных свойств или полей в типе {typeof(T).FullName}");
            }

            foreach (var line in lines)
            {
                var columns = SplitBy(line, StringSplitOptions.None, columnSeparators);
                if (columns.Length == 0)
                {
                    continue;
                }

                var item = typeCache.CreateInstance();
                for (var i = 0; i < columns.Length; i++)
                {
                    if (i >= props.Length)
                    {
                        continue;
                    }

                    props[i].SetValue(item, columns[i]);
                }

                result.Add((T)item);
            }

            return result;
        }

        /// <summary>
        /// Проверяет, содержит ли строка хотя бы одну из указанных подстрок.
        /// </summary>
        /// <param name="s">Исходная строка, в которой выполняется поиск.</param>
        /// <param name="comparison">
        /// Тип сравнения строк, используемый при поиске подстрок
        /// (например, <see cref="StringComparison.OrdinalIgnoreCase"/>).
        /// </param>
        /// <param name="values">Массив подстрок, наличие которых необходимо проверить.</param>
        /// <returns>
        /// <c>true</c>, если строка содержит хотя бы одну из указанных подстрок;
        /// иначе <c>false</c>.
        /// Если исходная строка пуста, массив подстрок равен <c>null</c> или пуст,
        /// метод возвращает <c>false</c>.
        /// </returns>
        public static bool StartsWithAny(string s, StringComparison comparison, params string[] values)
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

                if (s.StartsWith(value, comparison))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Преобразует строку в формат <c>camelCase</c> (lowerCamelCase).
        /// </summary>
        /// <param name="s">Исходная строка.</param>
        /// <returns>Строка в формате <c>camelCase</c>.
        /// Если входная строка пуста или не содержит слов, возвращается пустая строка.</returns>
        public static string ToCamelCase(string s)
        {
            var words = SplitWords(s);
            if (words.Length == 0)
            {
                return string.Empty;
            }

            var first = words[0];
            var rest = words.Skip(1)
                .Select(w => char.ToUpperInvariant(w[0]) + w.Substring(1));

            return first + string.Concat(rest);
        }

        /// <summary>
        /// Преобразует строку в формат <c>kebab-case</c>.
        /// </summary>
        /// <param name="s">Исходная строка.</param>
        /// <returns>Строка в формате <c>kebab-case</c>.</returns>
        public static string ToKebabCase(string s)
        {
            var words = SplitWords(s);
            return string.Join("-", words);
        }

        /// <summary>
        /// Преобразует строку в формат <c>PascalCase</c> (UpperCamelCase).
        /// </summary>
        /// <param name="s">Исходная строка.</param>
        /// <returns>Строка в формате <c>PascalCase</c>.</returns>
        public static string ToPascalCase(string s)
        {
            var words = SplitWords(s);
            return string.Concat(words.Select(w =>
                char.ToUpperInvariant(w[0]) + w.Substring(1)));
        }

        /// <summary>
        /// Преобразует строку в формат <c>snake_case</c>.
        /// </summary>
        /// <param name="s">Исходная строка.</param>
        /// <returns>Строка в формате <c>snake_case</c>.</returns>
        public static string ToSnakeCase(string s)
        {
            var words = SplitWords(s);
            return string.Join("_", words).ToLowerInvariant();
        }

        /// <summary>
        /// Преобразует строку в формат <c>UPPER_SNAKE_CASE</c>.
        /// </summary>
        /// <param name="s">Исходная строка.</param>
        /// <returns>Строка в формате <c>UPPER_SNAKE_CASE</c>.</returns>
        public static string ToUpperSnakeCase(string s)
        {
            var words = SplitWords(s);
            return string.Join("_", words).ToUpperInvariant();
        }

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
        public static string Trim(string s, string trimString, StringComparison comparison = StringComparison.Ordinal)
        {
            if (string.IsNullOrEmpty(s) || string.IsNullOrEmpty(trimString))
            {
                return s;
            }

            return TrimEnd(TrimStart(s, trimString, comparison), trimString, comparison);
        }

        /// <summary>
        /// Удаляет указанную подстроку из конца строки.
        /// </summary>
        /// <param name="s">Исходная строка.</param>
        /// <param name="trimString">Подстрока, которую необходимо удалить из конца строки.</param>
        /// <param name="comparison">
        /// Тип сравнения строк при проверке конца строки.
        /// По умолчанию используется <see cref="StringComparison.OrdinalIgnoreCase"/>.
        /// </param>
        /// <returns>
        /// Строка без указанной подстроки в конце.
        /// Если исходная строка или подстрока пустые, возвращается исходная строка.
        /// </returns>
        public static string TrimEnd(string s, string trimString, StringComparison comparison = StringComparison.Ordinal)
        {
            if (string.IsNullOrEmpty(s) || string.IsNullOrEmpty(trimString))
            {
                return s;
            }

            while (s.EndsWith(trimString, comparison))
            {
                s = s.Substring(0, s.Length - trimString.Length);
            }

            return s;
        }

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
        public static string TrimStart(string s, string trimString, StringComparison comparison = StringComparison.Ordinal)
        {
            if (string.IsNullOrEmpty(s) || string.IsNullOrEmpty(trimString))
            {
                return s;
            }

            while (s.StartsWith(trimString, comparison))
            {
                s = s.Substring(trimString.Length);
            }

            return s;
        }

        /// <summary>
        /// Удаляет пробельные символы с начала и конца строки.
        /// </summary>
        /// <param name="s">Строка для обработки. Может быть <c>null</c> или пустой.</param>
        /// <returns>
        /// Строка без ведущих и завершающих пробельных символов.
        /// Если входная строка <c>null</c> или пустая, возвращается исходное значение.
        /// </returns>
        public static string TrimWhitespaces(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return s;
            }

            return s.Trim(WhitespaceChars);
        }

        /// <summary>
        /// Распаковывает строку, сжатую с помощью <see cref="Zip"/>, из формата Base64 обратно в исходный текст.
        /// </summary>
        /// <param name="s">Сжатая строка в формате Base64.</param>
        /// <returns>Исходная строка, или <c>null</c>/пустая строка, если входная строка пустая.</returns>
        /// <remarks>
        /// Метод декодирует строку из Base64, затем распаковывает данные с помощью <see cref="GZipStream"/>
        /// и интерпретирует их как UTF-8.
        /// </remarks>
        public static string UnZip(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return s;
            }

            var bytes = Convert.FromBase64String(s);

            using (var input = new MemoryStream(bytes))
            {
                using (var gzip = new GZipStream(input, CompressionMode.Decompress))
                {
                    using (var output = new MemoryStream())
                    {
                        gzip.CopyTo(output);

                        return Encoding.UTF8.GetString(output.ToArray());
                    }
                }
            }
        }

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
        public static string Zip(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return s;
            }

            var bytes = Encoding.UTF8.GetBytes(s);

            using (var output = new MemoryStream())
            {
                using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
                {
                    gzip.Write(bytes, 0, bytes.Length);
                }

                return Convert.ToBase64String(output.ToArray());
            }
        }

        private static int Count<T>(IEnumerable<T> source, Func<T, int, bool> predicate)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            var count = 0;
            var index = 0;

            foreach (var item in source)
            {
                if (predicate(item, index))
                {
                    count++;
                }

                index++;
            }

            return count;
        }

        private static string EscapeCSharp(string value)
        {
            var sb = new StringBuilder(value.Length + 10);

            foreach (var c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\0': sb.Append("\\0"); break;
                    case '\a': sb.Append("\\a"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\v': sb.Append("\\v"); break;

                    default:
                        if (char.IsControl(c))
                        {
                            sb.Append("\\u" + ((int)c).ToString("x4"));
                        }
                        else
                        {
                            sb.Append(c);
                        }

                        break;
                }
            }

            return sb.ToString();
        }

        private static string EscapeCsv(string value)
        {
            var mustQuote =
                value.Contains(',') ||
                value.Contains('"') ||
                value.Contains('\n') ||
                value.Contains('\r');

            if (!mustQuote)
            {
                return value;
            }

            var escaped = value.Replace("\"", "\"\"");

            return $"\"{escaped}\"";
        }

        private static string EscapeJson(string value)
        {
            var sb = new StringBuilder(value.Length + 10);

            foreach (var c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;

                    default:
                        if (char.IsControl(c))
                        {
                            sb.Append("\\u" + ((int)c).ToString("x4"));
                        }
                        else
                        {
                            sb.Append(c);
                        }

                        break;
                }
            }

            return sb.ToString();
        }

        private static string EscapeXmlAttribute(string value)
        {
            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }

        private static string EscapeXmlText(string value)
        {
            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }

        private static int FindMinimum(params int[] p)
        {
            if (p == null)
            {
                return int.MinValue;
            }

            var min = int.MaxValue;
            for (var i = 0; i < p.Length; i++)
            {
                if (min > p[i])
                {
                    min = p[i];
                }
            }

            return min;
        }

        private static int GetPrefixLength(string s1, string s2, int maxPrefixLength = 4)
        {
            var n = Math.Min(Math.Min(s1.Length, s2.Length), maxPrefixLength);

            for (var i = 0; i < n; i++)
            {
                if (s1[i] != s2[i])
                {
                    return i;
                }
            }

            return n;
        }

        private static bool IsInsideProtectedRange(string s, int index, (string Prefix, string Suffix)[] ranges)
        {
            if (ranges == null || ranges.Length == 0)
            {
                return false;
            }

            foreach (var (prefix, suffix) in ranges)
            {
                if (string.IsNullOrEmpty(prefix) || string.IsNullOrEmpty(suffix))
                {
                    continue;
                }

                var start = IndexOf(s, prefix, index, -1);
                var count = Count(s, (x, i) => x == '"' && i < index);
                if (start < 0 || count % 2 == 0)
                {
                    continue;
                }

                var end = IndexOf(s, suffix, index, 1);
                count = Count(s, (x, i) => x == '"' && i > index);
                if (end >= 0 && index < end && count % 2 != 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static double JaroDistance(string s1, string s2)
        {
            if (s1 == s2)
            {
                return 1.0;
            }

            var s1Len = s1.Length;
            var s2Len = s2.Length;

            var matchDistance = (Math.Max(s1Len, s2Len) / 2) - 1;

            var s1Matches = new bool[s1Len];
            var s2Matches = new bool[s2Len];

            var matches = 0;
            var transpositions = 0.0;

            for (var i = 0; i < s1Len; i++)
            {
                var start = Math.Max(0, i - matchDistance);
                var end = Math.Min(s2Len - 1, i + matchDistance);

                for (var j = start; j <= end; j++)
                {
                    if (s2Matches[j])
                    {
                        continue;
                    }

                    if (s1[i] != s2[j])
                    {
                        continue;
                    }

                    s1Matches[i] = true;
                    s2Matches[j] = true;
                    matches++;
                    break;
                }
            }

            if (matches == 0)
            {
                return 0.0;
            }

            var k = 0;
            for (var i = 0; i < s1Len; i++)
            {
                if (!s1Matches[i])
                {
                    continue;
                }

                while (!s2Matches[k])
                {
                    k++;
                }

                if (s1[i] != s2[k])
                {
                    transpositions++;
                }

                k++;
            }

            transpositions /= 2.0;

            return ((matches / (double)s1Len) +
                    (matches / (double)s2Len) +
                    ((matches - transpositions) / matches)) / 3.0;
        }

        private static double JaroWinklerDistance(string s1, string s2, bool caseSensitive = false)
        {
            if (!caseSensitive)
            {
                s1 = s1.ToLower();
                s2 = s2.ToLower();
            }

            var jaroDistance = JaroDistance(s1, s2);

            var prefixLength = GetPrefixLength(s1, s2);
            const double scalingFactor = 0.1;

            return jaroDistance + (prefixLength * scalingFactor * (1 - jaroDistance));
        }

        private static int LevenshteinDistance(string input, string comparedTo, bool caseSensitive = false)
        {
            if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(comparedTo))
            {
                return -1;
            }

            if (!caseSensitive)
            {
                input = input.ToLower();
                comparedTo = comparedTo.ToLower();
            }

            var matrix = new int[input.Length + 1, comparedTo.Length + 1];

            for (var i = 0; i <= matrix.GetUpperBound(0); i++)
            {
                matrix[i, 0] = i;
            }

            for (var i = 0; i <= matrix.GetUpperBound(1); i++)
            {
                matrix[0, i] = i;
            }

            for (var i = 1; i <= matrix.GetUpperBound(0); i++)
            {
                var si = input[i - 1];
                for (var j = 1; j <= matrix.GetUpperBound(1); j++)
                {
                    var tj = comparedTo[j - 1];
                    var cost = (si == tj) ? 0 : 1;

                    var above = matrix[i - 1, j];
                    var left = matrix[i, j - 1];
                    var diag = matrix[i - 1, j - 1];
                    var cell = FindMinimum(above + 1, left + 1, diag + cost);

                    if (i > 1 && j > 1)
                    {
                        var trans = matrix[i - 2, j - 2] + 1;
                        if (input[i - 2] != comparedTo[j - 1])
                        {
                            trans++;
                        }

                        if (input[i - 1] != comparedTo[j - 2])
                        {
                            trans++;
                        }

                        if (cell > trans)
                        {
                            cell = trans;
                        }
                    }

                    matrix[i, j] = cell;
                }
            }

            return matrix[matrix.GetUpperBound(0), matrix.GetUpperBound(1)];
        }

        private static string ReplaceInternal(
                    string source,
                    string search,
                    string replacement,
                    StringComparison comparison)
        {
            var index = source.IndexOf(search, comparison);

            if (index < 0)
            {
                return source;
            }

            var sb = new StringBuilder(source.Length);
            var lastIndex = 0;

            while (index >= 0)
            {
                sb.Append(source, lastIndex, index - lastIndex);
                sb.Append(replacement);

                lastIndex = index + search.Length;
                index = source.IndexOf(search, lastIndex, comparison);
            }

            sb.Append(source, lastIndex, source.Length - lastIndex);
            return sb.ToString();
        }

        /// <summary>
        /// Разбивает входную строку на слова с учётом следующих правил:
        /// <list type="bullet">
        /// <item><description>Разделителями считаются символы <c>'-'</c>, <c>'_'</c> и пробел.</description></item>
        /// <item><description>Поддерживается разбиение строк в формате PascalCase и camelCase.</description></item>
        /// <item><description>Аббревиатуры (например, HTTPServer) корректно выделяются в отдельные слова.</description></item>
        /// <item><description>Числовые последовательности выделяются как отдельные слова.</description></item>
        /// </list>
        /// Все возвращаемые слова приводятся к нижнему регистру.
        /// </summary>
        /// <param name="input">Исходная строка для разбиения.</param>
        /// <returns>
        /// Массив слов в нижнем регистре.
        /// Если входная строка равна <c>null</c>, пустая или состоит только из пробелов,
        /// возвращается пустой массив.
        /// </returns>
        private static string[] SplitWords(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return Array.Empty<string>();
            }

            // Заменяем дефисы и пробелы на _
            input = input.Replace("-", "_").Replace(" ", "_");

            // Если уже есть разделители — просто делим
            if (input.Contains("_"))
            {
                return input
                    .Split(Separator, StringSplitOptions.RemoveEmptyEntries)
                    .Select(w => w.ToLowerInvariant())
                    .ToArray();
            }

            // Разбиваем PascalCase / camelCase / аббревиатуры
            var matches = Regex.Matches(
                input,
                @"([A-Z]+(?![a-z]))|([A-Z]?[a-z]+)|(\d+)");

            return matches.Cast<Match>()
                .Select(m => m.Value.ToLowerInvariant())
                .ToArray();
        }

        /// <summary>
        /// Представляет токен (выделенную часть строки с учетом префикса и суффикса). Поддерживает вложенность.
        /// </summary>
        public class Token
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="Token"/> class.
            /// </summary>
            /// <param name="source">The source.</param>
            /// <param name="start">The start.</param>
            /// <param name="end">The end.</param>
            /// <param name="setTag">The set tag.</param>
            /// <param name="contentTransformer">Content transformer.</param>
            public Token(
                string source,
                int start,
                int end,
                Func<Token, object> setTag = null,
                Func<Token, string> contentTransformer = null)
                : this()
            {
                var s = source.Substring(start, end - start + 1);
                this.Body = s;
                this.Text = s;
                this.Source = source;
                this.SourceStart = start;
                this.SourceEnd = end;
                this.Tag = setTag?.Invoke(this);
                if (contentTransformer != null)
                {
                    this.ContentTransformers.Add(contentTransformer);
                }
            }

            /// <summary>
            /// Initializes a new instance of the <see cref="Token"/> class.
            /// </summary>
            /// <param name="body">The body.</param>
            /// <param name="setTag">The set tag.</param>
            public Token(string body, Func<Token, object> setTag = null)
                : this()
            {
                this.Body = body;
                this.Text = body;
                this.Tag = setTag?.Invoke(this);
            }

            /// <summary>
            /// Initializes a new instance of the <see cref="Token"/> class.
            /// </summary>
            internal Token()
            {
                this.Id = IdInternal;
                IdInternal++;
            }

            /// <summary>
            /// исходный текст токена, включая префикс и суффикс.
            /// </summary>
            /// <value>The body.</value>
            public string Body { get; internal set; }

            /// <summary>
            /// дочерние токены.
            /// </summary>
            /// <value>The children.</value>
            public IEnumerable<Token> Children => this.ChildrenInternal;

            /// <summary>
            /// итоговое содержимое токена, формируется из Text с учетом вложенных токенов и применённых
            /// ContentTransformers.
            /// </summary>
            /// <value>The content.</value>
            public string Content
            {
                get
                {
                    var result = this.Text ?? string.Empty;
                    var children = this.ChildrenInternal.Where(x => x.Mask != null).ToArray();

                    if (children.Length > 0)
                    {
                        foreach (var child in children.OrderByDescending(c => c.ParentStart))
                        {
                            var start = child.ParentStart - this.Prefix.Length;
                            var length = child.ParentEnd - child.ParentStart + 1;
                            result = result.Substring(0, start) + child.Content + result.Substring(start + length);
                        }
                    }

                    if (this.ContentTransformers != null && this.ContentTransformers.Count > 0)
                    {
                        foreach (var func in this.ContentTransformers)
                        {
                            var oldText = this.Text;
                            try
                            {
                                this.Text = result;
                                var r = func(this);
                                result = r ?? string.Empty;
                            }
                            finally
                            {
                                this.Text = oldText;
                            }
                        }
                    }

                    return result;
                }
            }

            /// <summary>
            /// пользовательские функции-трансформеры, применяемые к токену с учетом модели, если она
            /// указана. По умолчанию равно значению <see cref="Text"/>.
            /// </summary>
            /// <value>The content transformers.</value>
            public List<Func<Token, string>> ContentTransformers { get; set; } = new List<Func<Token, string>>();

            /// <summary>
            /// первый токен в цепочке предыдущих токенов на том же уровне вложенности.
            /// </summary>
            /// <value>The first.</value>
            public Token First
            {
                get
                {
                    var t = this;
                    while (t.Previous != null)
                    {
                        t = t.Previous;
                    }

                    return t;
                }
            }

            /// <summary>
            /// порядковый идентификатор токена.
            /// </summary>
            /// <value>The identifier.</value>
            public int Id { get; internal set; }

            /// <summary>
            /// позиция токена среди соседей (0 = первый).
            /// </summary>
            /// <value>The index.</value>
            public int Index
            {
                get
                {
                    var i = 0;
                    var t = this;
                    while (t.Previous != null)
                    {
                        t = t.Previous;
                        i++;
                    }

                    return i;
                }
            }

            /// <summary>
            /// a value indicating whether токен без маски (не соответствует ни одной из заданных масок).
            /// </summary>
            /// <value><c>true</c> if this instance is not matched; otherwise, <c>false</c>.</value>
            public bool IsNotMatched => this.Mask == null;

            /// <summary>
            /// последний токен в цепочке следующих токенов на том же уровне вложенности.
            /// </summary>
            /// <value>The last.</value>
            public Token Last
            {
                get
                {
                    var t = this;
                    while (t.Next != null)
                    {
                        t = t.Next;
                    }

                    return t;
                }
            }

            /// <summary>
            /// уровень вложенности токена (0 = корень).
            /// </summary>
            /// <value>The level.</value>
            public int Level
            {
                get
                {
                    var level = 0;
                    var node = this.Parent;

                    while (node != null)
                    {
                        level++;
                        node = node.Parent;
                    }

                    return level;
                }
            }

            /// <summary>
            /// маска токена.
            /// </summary>
            /// <value>The mask.</value>
            public TokenMask Mask { get; internal set; }

            /// <summary>
            /// следующий токен на том же уровне вложенности.
            /// </summary>
            /// <value>The next.</value>
            public Token Next { get; internal set; }

            /// <summary>
            /// родительский токен (null, если токен верхнего уровня).
            /// </summary>
            /// <value>The parent.</value>
            public Token Parent { get; internal set; }

            /// <summary>
            /// индекс конца токена (последний символ суффикса <see cref="Suffix"/>) относительно начала
            /// родительского <see cref="Body"/> родительского токена <see cref="Parent"/>.
            /// </summary>
            /// <value>The parent end.</value>
            public int ParentEnd { get; internal set; }

            /// <summary>
            /// индекс начала токена (первый символ префикса <see cref="Prefix"/>) относительно начала
            /// родительского <see cref="Body"/> родительского токена <see cref="Parent"/>.
            /// </summary>
            /// <value>The parent start.</value>
            public int ParentStart { get; internal set; }

            /// <summary>
            /// префикс токена (например "(").
            /// </summary>
            /// <value>The prefix.</value>
            public string Prefix { get; internal set; }

            /// <summary>
            /// предыдущий токен на том же уровне вложенности.
            /// </summary>
            /// <value>The previous.</value>
            public Token Previous { get; internal set; }

            /// <summary>
            /// корневой токен.
            /// </summary>
            /// <value>The root.</value>
            public Token Root
            {
                get
                {
                    var node = this;
                    while (node.Parent != null)
                    {
                        node = node.Parent;
                    }

                    return node;
                }
            }

            /// <summary>
            /// исходная строка.
            /// </summary>
            /// <value>The source.</value>
            public string Source { get; internal set; }

            /// <summary>
            /// индекс конца токена (последний символ суффикса <see cref="Suffix"/>) в исходной строке <see
            /// cref="Source"/>.
            /// </summary>
            /// <value>The source end.</value>
            public int SourceEnd { get; internal set; }

            /// <summary>
            /// индекс начала токена (первый символ префикса <see cref="Prefix"/>) в исходной строке <see
            /// cref="Source"/>.
            /// </summary>
            /// <value>The source start.</value>
            public int SourceStart { get; internal set; }

            /// <summary>
            /// суффикс токена (например ")").
            /// </summary>
            /// <value>The suffix.</value>
            public string Suffix { get; internal set; }

            /// <summary>
            /// тег для хранения пользовательских данных.
            /// </summary>
            /// <value>The tag.</value>
            public object Tag { get; set; }

            /// <summary>
            /// внутренний текст токена без префикса и суффикса.
            /// </summary>
            /// <value>The text.</value>
            public string Text { get; internal set; }

            /// <summary>
            /// the identifier internal.
            /// </summary>
            internal static int IdInternal { get; set; } = 1;

            /// <summary>
            /// the children internal.
            /// </summary>
            internal List<Token> ChildrenInternal { get; } = new List<Token>();

            /// <summary>
            /// Returns an enumerable collection containing this token and all of its descendant tokens in depth-first
            /// order.
            /// </summary>
            /// <remarks>The returned sequence starts with the deepest descendants and ends with this
            /// token. This method is useful for traversing the entire token hierarchy.</remarks>
            /// <returns>An <see cref="IEnumerable{Token}"/> that includes this token followed by all descendant tokens. The
            /// collection is empty only if there are no tokens.</returns>
            public IEnumerable<Token> All()
            {
                var list = new List<Token>();
                foreach (var child in this.ChildrenInternal)
                {
                    foreach (var desc in child.All())
                    {
                        list.Add(desc);
                    }
                }

                list.Add(this);

                return list;
            }

            /// <summary>
            /// Все токены после текущего (по цепочке Next).
            /// </summary>
            /// <returns>IEnumerable&lt;Token&gt;.</returns>
            public IEnumerable<Token> AllAfter()
            {
                for (var t = this.Next; t != null; t = t.Next)
                {
                    yield return t;
                }
            }

            /// <summary>
            /// Все токены перед текущим (по цепочке Previous).
            /// </summary>
            /// <returns>IEnumerable&lt;Token&gt;.</returns>
            public IEnumerable<Token> AllBefore()
            {
                for (var t = this.Previous; t != null; t = t.Previous)
                {
                    yield return t;
                }
            }

            /// <summary>
            /// Возвращает последовательность токенов, следующих за текущим, которые удовлетворяют указанному условию.
            /// </summary>
            /// <param name="predicate">Функция, определяющая условие фильтрации токенов. Должна возвращать <see langword="true"/>, если токен
            /// соответствует критериям; иначе <see langword="false"/>.</param>
            /// <returns>Последовательность токенов, следующих за текущим, для которых функция <paramref name="predicate"/>
            /// возвращает <see langword="true"/>. Если ни один токен не соответствует условию, возвращается пустая
            /// последовательность.</returns>
            public IEnumerable<Token> FirstAfter(Func<Token, bool> predicate)
            {
                for (var t = this.Next; t != null; t = t.Next)
                {
                    if (predicate(t))
                    {
                        yield return t;
                    }
                }
            }

            /// <summary>
            /// Возвращает последовательность токенов, предшествующих текущему, которые удовлетворяют заданному условию.
            /// </summary>
            /// <param name="predicate">Функция-предикат для фильтрации токенов. Токен включается в результат, если предикат возвращает <c>true</c>.</param>
            /// <returns>Последовательность токенов до текущего, соответствующих условию <paramref name="predicate"/>.</returns>
            /// <remarks>
            /// Перебор начинается с токена <see cref="Previous"/> текущего токена и продолжается в обратном направлении,
            /// пока не достигнут конец цепочки (<c>Previous == null</c>).
            /// </remarks>
            public IEnumerable<Token> FirstBefore(Func<Token, bool> predicate)
            {
                for (var t = this.Previous; t != null; t = t.Previous)
                {
                    if (predicate(t))
                    {
                        yield return t;
                    }
                }
            }

            /// <summary>
            /// Вставляет токен после текущего.
            /// </summary>
            /// <param name="newToken">The new token.</param>
            public void InsertAfter(Token newToken)
            {
                newToken.Previous = this;
                newToken.Next = this.Next;

                this.Next?.Previous = newToken;

                this.Next = newToken;

                if (this.Parent != null)
                {
                    var list = this.Parent.ChildrenInternal;
                    var idx = this.Parent.ChildrenInternal.IndexOf(this);
                    list.Insert(idx + 1, newToken);
                    newToken.Parent = this.Parent;
                }
            }

            /// <summary>
            /// Вставляет токен перед текущим.
            /// </summary>
            /// <param name="newToken">The new token.</param>
            public void InsertBefore(Token newToken)
            {
                newToken.Next = this;
                newToken.Previous = this.Previous;

                this.Previous?.Next = newToken;

                this.Previous = newToken;

                // если есть родитель — вставляем в его список детей
                if (this.Parent != null)
                {
                    var list = this.Parent.ChildrenInternal;
                    var idx = this.Parent.ChildrenInternal.IndexOf(this);
                    list.Insert(idx, newToken);
                    newToken.Parent = this.Parent;
                }
            }

            /// <summary>
            /// Удаляет текущий элемент из двусвязного списка и из родительского списка дочерних элементов.
            /// </summary>
            /// <remarks>
            /// <para>Метод выполняет следующие действия:</para>
            /// <list type="bullet">
            /// <item>Корректирует ссылки <c>Previous</c> и <c>Next</c> соседних элементов, чтобы сохранить целостность списка.</item>
            /// <item>Удаляет элемент из внутреннего списка дочерних элементов родителя (<c>Parent.ChildrenInternal</c>).</item>
            /// <item>Обнуляет ссылки <c>Parent</c>, <c>Previous</c> и <c>Next</c>, чтобы полностью отсоединить элемент.</item>
            /// </list>
            /// <para>После вызова <c>Remove</c> элемент считается полностью удалённым и не связан с другими элементами списка.</para>
            /// </remarks>
            public void Remove()
            {
                // корректируем Previous/Next
                this.Previous?.Next = this.Next;

                this.Next?.Previous = this.Previous;

                // удаляем из родительского списка детей
                this.Parent?.ChildrenInternal.Remove(this);

                this.Parent = null;
                this.Previous = null;
                this.Next = null;
            }

            /// <summary>
            /// Все соседи (включая самого токена).
            /// </summary>
            /// <returns>IEnumerable&lt;Token&gt;.</returns>
            public IEnumerable<Token> Siblings()
            {
                for (var t = this.First; t != null; t = t.Next)
                {
                    yield return t;
                }
            }

            /// <summary>
            /// Returns a <see cref="string" /> that represents this instance.
            /// </summary>
            /// <returns>A <see cref="string" /> that represents this instance.</returns>
            public override string ToString() => $"ID={this.Id}{(this.IsNotMatched ? "*" : string.Empty)} B='{this.Body}' T='{this.Text}' C='{this.Content}' SSE=({this.SourceStart}-{this.SourceEnd}) Lv={this.Level} Tag='{this.Tag}'";
        }

        /// <summary>
        /// Маска токена, определяющая его префикс, суффикс и поведение.
        /// </summary>
        public sealed class TokenMask
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="TokenMask" /> class.
            /// </summary>
            public TokenMask()
            {
            }

            /// <summary>
            /// Initializes a new instance of the <see cref="TokenMask" /> class.
            /// </summary>
            /// <param name="prefix">The prefix.</param>
            /// <param name="suffix">The suffix.</param>
            /// <param name="setTag">The set tag.</param>
            /// <param name="contentTransformer">The content transformer.</param>
            public TokenMask(string prefix, string suffix, Func<Token, object> setTag = null, Func<Token, string> contentTransformer = null)
                : this()
            {
                this.Prefix = prefix;
                this.Suffix = suffix;
                this.SetTag = setTag;
                this.ContentTransformer = contentTransformer;
            }

            /// <summary>
            /// a value indicating whether разрешает ли данная маска иметь вложенные токены.
            /// </summary>
            /// <value><c>true</c> if [allow children tokens]; otherwise, <c>false</c>.</value>
            public bool AllowChildrenTokens { get; set; } = true;

            /// <summary>
            /// разрешённые маски для вложенных токенов.
            /// </summary>
            /// <value>The allowed children masks.</value>
            public List<TokenMask> AllowedChildrenMasks { get; set; } = new List<TokenMask>();

            /// <summary>
            /// разрешённые маски для следующих соседних токенов.
            /// </summary>
            /// <value>The allowed next masks.</value>
            public List<TokenMask> AllowedNextMasks { get; set; } = new List<TokenMask>();

            /// <summary>
            /// функция для трансформации содержимого токена.
            /// </summary>
            /// <value>The content transformer.</value>
            public Func<Token, string> ContentTransformer { get; set; }

            /// <summary>
            /// префикс токена.
            /// </summary>
            /// <value>The prefix.</value>
            public string Prefix { get; set; }

            /// <summary>
            /// функция для установки пользовательского тега токена.
            /// </summary>
            /// <value>The set tag.</value>
            public Func<Token, object> SetTag { get; set; }

            /// <summary>
            /// суффикс токена.
            /// </summary>
            /// <value>The suffix.</value>
            public string Suffix { get; set; }

            /// <summary>
            /// a value indicating whether выбрасывать ли исключение при попытке добавить неразрешённый вложенный токен.
            /// </summary>
            /// <value><c>true</c> if [throw exception on not allowed token]; otherwise, <c>false</c>.</value>
            public bool ThrowExceptionOnNotAllowedToken { get; set; } = false;

            /// <summary>
            /// Returns a <see cref="string" /> that represents this instance.
            /// </summary>
            /// <returns>A <see cref="string" /> that represents this instance.</returns>
            public override string ToString() => $"{this.Prefix}, {this.Suffix}";
        }
    }
}