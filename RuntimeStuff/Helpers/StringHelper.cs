// ***********************************************************************
// Assembly         : RuntimeStuff
// Author           : RS
// Created          : 01-06-2026
//
// Last Modified By : RS
// Last Modified On : 01-07-2026
// ***********************************************************************
// <copyright file="StringHelper.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************

namespace RuntimeStuff.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Text;

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
        private static string[] columnSeparators = new string[] { "\t", ";", "|" };

        private static string[] lineSeparators = new string[] { Environment.NewLine, "\r", "\n" };

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
        /// Gets whitespace chars.
        /// Набор пробельных символов, используемых при разборе строк.
        /// Включает пробел, перевод строки, табуляция, пустой символ.
        /// </summary>
        public static char[] WhitespaceChars { get; } = new char[]
        {
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
        };

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
        /// Возвращает часть строки в диапазоне [startIndex..endIndex]. Работает как string.Substring(s, startIndex, endIndex -
        /// startIndex + 1).
        /// </summary>
        /// <param name="s">Исходная строка.</param>
        /// <param name="startIndex">Начальная позиция (включительно).</param>
        /// <param name="endIndex">Конечная позиция (включительно).</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">s.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">startIndex.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">endIndex.</exception>
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
        /// <exception cref="System.ArgumentNullException">s.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">startIndex.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">endIndex.</exception>
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
        /// Gets the tokens.
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
        /// <exception cref="System.InvalidOperationException">Token with Prefix='{tm.Prefix}' and Suffix='{tm.Suffix}' is not allowed to be a child of another token.</exception>
        /// <exception cref="System.InvalidOperationException">Token with Prefix='{tm.Prefix}' and Suffix='{tm.Suffix}' is not allowed to be a next of {prevToken} token.</exception>
        public static List<Token> GetTokens(string input, IEnumerable<TokenMask> tokenMasks, bool notMatchedAsTokens = false, Func<Token, object> notMatchedTokenSetTag = null, Func<Token, string> notMatchedContentTransformer = null)
        {
            Token.IdInternal = 1;
            var result = new List<Token>();
            var stack = new Stack<(Token Token, string Prefix, string Suffix, Func<Token, string> ContentTransformer)>();

            // Сортируем маски один раз по убыванию длины префикса
            var masks = tokenMasks.OrderByDescending(m => m.Prefix.Length)
                .Concat(tokenMasks.SelectMany(x => x.AllowedChildrenMasks))
                .Distinct()
                .ToArray();

            var span = input.AsSpan();
            var i = 0;

            while (i < span.Length)
            {
                var matched = false;
                var curChar = input[i];

                foreach (var tm in masks)
                {
                    if (curChar != tm.Prefix[0])
                    {
                        continue;
                    }

                    if (i + tm.Prefix.Length <= span.Length && span.Slice(i, tm.Prefix.Length).SequenceEqual(tm.Prefix.AsSpan()))
                    {
                        // Если prefix == suffix и токен уже открыт этим же префиксом — НЕ открываем новый
                        if (stack.Count > 0 && tm.Prefix == tm.Suffix && stack.Peek().Prefix == tm.Prefix)
                        {
                            break;
                        }

                        // Обработка запрета на добавление дочерних токенов
                        if (stack.Count > 0)
                        {
                            var topToken = stack.Last().Token;

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

                            if (topToken.Mask.AllowedChildrenMasks?.Any() == true && !topToken.Mask.AllowedChildrenMasks.Contains(tm))
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
                        if (prevToken?.Mask?.AllowedNextMasks?.Any() == true && !prevToken.Mask.AllowedNextMasks.Contains(tm))
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

                        // === МГНОВЕННЫЙ ТОКЕН (Suffix == null) ===
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

                            // Добавление Previous/Next и добавление в дерево
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

                        token.Mask = tm;
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

                // Проверка конца токена
                if (stack.Count > 0)
                {
                    var (topToken, topPrefix, topSuffix, _) = stack.Peek();

                    if (i + topSuffix.Length <= span.Length && span.Slice(i, topSuffix.Length).SequenceEqual(topSuffix.AsSpan()))
                    {
                        stack.Pop();
                        topToken.SourceEnd = i + topSuffix.Length - 1;
                        topToken.ParentEnd = topToken.Parent == null
                            ? i + 1
                            : topToken.SourceEnd - topToken.Parent.SourceStart;

                        var bodySpan = span.Slice(topToken.SourceStart, topToken.SourceEnd - topToken.SourceStart + 1);
                        topToken.Body = bodySpan.ToString();
                        topToken.Text = bodySpan.Slice(topPrefix.Length, bodySpan.Length - topPrefix.Length - topSuffix.Length).ToString();

                        // Добавление Previous / Next
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
                return false;

            s = TrimWhiteChars(s);

            // JSON всегда начинается с { или [
            if (s.Length < 2)
                return false;

            var first = s[0];
            var last = s[s.Length - 1];

            if (!((first == '{' && last == '}') ||
                  (first == '[' && last == ']')))
                return false;

            return true;
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
        /// <see cref="System.Xml.XmlReader"/> или <see cref="System.Xml.Linq.XDocument"/>.
        /// </remarks>
        public static bool IsXml(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return false;

            s = TrimWhiteChars(s);

            // XML всегда начинается с '<'
            if (s[0] != '<')
                return false;

            // Минимальная длина: <a/>
            if (s.Length < 4)
                return false;

            // Явно отсекаем HTML-комментарии и DOCTYPE без корневого элемента
            if (s.StartsWith("<!--", StringComparison.Ordinal) ||
                s.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase))
                return false;

            // Проверка наличия закрывающего '>'
            var close = s.IndexOf('>');
            if (close < 0)
                return false;

            return true;
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
        public static string RemoveLongSpaces(string s, bool includeNewLines = true, bool includeTabs = true)
        {
            if (string.IsNullOrEmpty(s))
            {
                return s;
            }

            var sb = new StringBuilder(s.Length);
            char? lastChar = null;

            foreach (var c in s)
            {
                switch (c)
                {
                    case ' ':
                        if (lastChar != ' ')
                        {
                            sb.Append(c);
                        }

                        break;

                    case '\t':
                        if (includeTabs)
                        {
                            if (lastChar != '\t')
                            {
                                sb.Append(c);
                            }
                        }
                        else
                        {
                            sb.Append(c);
                        }

                        break;

                    case '\r':
                    case '\n':
                        if (includeNewLines)
                        {
                            if (lastChar != '\r' && lastChar != '\n')
                            {
                                sb.Append(c);
                            }
                        }
                        else
                        {
                            sb.Append(c);
                        }

                        break;

                    default:
                        sb.Append(c);
                        break;
                }

                lastChar = c;
            }

            return sb.ToString();
        }

        /// <summary>
        /// Возвращает строку, повторенную указанное количество раз.
        /// </summary>
        /// <param name="str">Исходная строка.</param>
        /// <param name="count">Количество повторений.</param>
        /// <returns>Новая строка, состоящая из повторений исходной строки.</returns>
        /// <exception cref="System.ArgumentNullException">str.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">count - Количество повторений не может быть отрицательным.</exception>
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
            var sb = new System.Text.StringBuilder(str.Length * count);
            for (var i = 0; i < count; i++)
            {
                sb.Append(str);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Заменяет часть строки в диапазоне [startIndex..endIndex] на указанную строку.
        /// </summary>
        /// <param name="s">Исходная строка.</param>
        /// <param name="startIndex">Начальная позиция (включительно).</param>
        /// <param name="endIndex">Конечная позиция (включительно).</param>
        /// <param name="replaceString">Строка для замены.</param>
        /// <returns>Новая строка с заменой.</returns>
        /// <exception cref="System.ArgumentNullException">s.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">startIndex.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">endIndex.</exception>
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
        /// <returns>
        /// Массив подстрок, полученных после разбиения. Если строка <c>null</c> или пустая, возвращается пустой массив.
        /// Если <paramref name="splitBy"/> пустой или <c>null</c>, возвращается массив, содержащий исходную строку.
        /// </returns>
        /// <remarks>
        /// <para>Метод выполняет последовательный поиск ближайшего разделителя и делит строку по нему.</para>
        /// <para>Подстроки между разделителями включаются в результат, разделители сами не включаются.</para>
        /// <para>Поддерживается несколько разделителей произвольной длины.</para>
        /// </remarks>
        public static string[] SplitBy(string s, StringSplitOptions options, params string[] splitBy)
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
            return SplitToList<T>(s, propertyMap, columnSeparators, lineSeparators);
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

            var typeCache = MemberCache.Create(typeof(T));
            var lines = SplitBy(s, StringSplitOptions.RemoveEmptyEntries, lineSeparators);
            var props = propertyMap?.Any() == true ? typeCache.Properties.Where(x => propertyMap.Contains(x.Name)).ToArray() : typeCache.PublicBasicProperties.ToArray();
            foreach (var line in lines)
            {
                var columns = SplitBy(line, StringSplitOptions.None, columnSeparators);
                if (!columns.Any())
                    continue;
                var item = (T)typeCache.CreateInstance();
                for (int i = 0; i < columns.Length; i++)
                {
                    if (i >= props.Length) continue;
                    props[i].SetValue(item, columns[i]);
                }

                result.Add(item);
            }

            return result;
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
        /// Метод удаляет указанный суффикс с конца строки, если он существует.
        /// </summary>
        /// <param name="s">Исходная строка, из которой нужно удалить суффикс.</param>
        /// <param name="subStr">Строка-суффикс, которую нужно удалить с конца.</param>
        /// <param name="comparison">Тип сравнения строк при проверке суффикса.</param>
        /// <returns>Строка без указанного суффикса в конце, если он был найден.</returns>
        /// <remarks>Метод проверяет, заканчивается ли исходная строка указанным суффиксом.
        /// Если суффикс найден, возвращается строка без этого суффикса.
        /// Если суффикс не найден или параметры пустые, возвращается исходная строка.</remarks>
        public static string TrimEnd(string s, string subStr, StringComparison comparison = StringComparison.Ordinal)
        {
            // Проверка на пустые входные данные
            if (string.IsNullOrEmpty(s) || string.IsNullOrEmpty(subStr))
            {
                return s;
            }

            // Проверка наличия суффикса в конце строки
            if (s.EndsWith(subStr, comparison))
            {
                // Возвращаем строку без суффикса
                return s.Substring(0, s.Length - subStr.Length);
            }

            // Если суффикс не найден, возвращаем исходную строку
            return s;
        }

        /// <summary>
        /// Trims the white chars.
        /// </summary>
        /// <param name="s">The s.</param>
        /// <returns>System.String.</returns>
        public static string TrimWhiteChars(string s)
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

        private static int FindMinimum(params int[] p)
        {
            if (p == null) return int.MinValue;
            var min = int.MaxValue;
            for (var i = 0; i < p.Length; i++)
            {
                if (min > p[i]) min = p[i];
            }

            return min;
        }

        private static int GetPrefixLength(string s1, string s2, int maxPrefixLength = 4)
        {
            var n = Math.Min(Math.Min(s1.Length, s2.Length), maxPrefixLength);

            for (var i = 0; i < n; i++)
            {
                if (s1[i] != s2[i])
                    return i;
            }

            return n;
        }

        private static double JaroDistance(string s1, string s2)
        {
            if (s1 == s2)
                return 1.0;

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
                    if (s2Matches[j]) continue;
                    if (s1[i] != s2[j]) continue;
                    s1Matches[i] = true;
                    s2Matches[j] = true;
                    matches++;
                    break;
                }
            }

            if (matches == 0)
                return 0.0;

            var k = 0;
            for (var i = 0; i < s1Len; i++)
            {
                if (!s1Matches[i]) continue;
                while (!s2Matches[k]) k++;
                if (s1[i] != s2[k])
                    transpositions++;
                k++;
            }

            transpositions /= 2.0;

            return ((matches / (double)s1Len) +
                    (matches / (double)s2Len) +
                    ((matches - transpositions) / matches)) / 3.0;
        }

        private static double JaroWinklerDistance(this string s1, string s2, bool caseSensitive = false)
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
            if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(comparedTo)) return -1;
            if (!caseSensitive)
            {
                input = input.ToLower();
                comparedTo = comparedTo.ToLower();
            }

            var matrix = new int[input.Length + 1, comparedTo.Length + 1];

            for (var i = 0; i <= matrix.GetUpperBound(0); i++) matrix[i, 0] = i;
            for (var i = 0; i <= matrix.GetUpperBound(1); i++) matrix[0, i] = i;

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
                        if (input[i - 2] != comparedTo[j - 1]) trans++;
                        if (input[i - 1] != comparedTo[j - 2]) trans++;
                        if (cell > trans) cell = trans;
                    }

                    matrix[i, j] = cell;
                }
            }

            return matrix[matrix.GetUpperBound(0), matrix.GetUpperBound(1)];
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
            /// Gets исходный текст токена, включая префикс и суффикс.
            /// </summary>
            /// <value>The body.</value>
            public string Body { get; internal set; }

            /// <summary>
            /// Gets дочерние токены.
            /// </summary>
            /// <value>The children.</value>
            public IEnumerable<Token> Children => this.ChildrenInternal;

            /// <summary>
            /// Gets итоговое содержимое токена, формируется из Text с учетом вложенных токенов и применённых
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
            /// Gets or sets пользовательские функции-трансформеры, применяемые к токену с учетом модели, если она
            /// указана. По умолчанию равно значению <see cref="Text"/>.
            /// </summary>
            /// <value>The content transformers.</value>
            public List<Func<Token, string>> ContentTransformers { get; set; } = new List<Func<Token, string>>();

            /// <summary>
            /// Gets первый токен в цепочке предыдущих токенов на том же уровне вложенности.
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
            /// Gets порядковый идентификатор токена.
            /// </summary>
            /// <value>The identifier.</value>
            public int Id { get; internal set; }

            /// <summary>
            /// Gets позиция токена среди соседей (0 = первый).
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
            /// Gets a value indicating whether токен без маски (не соответствует ни одной из заданных масок).
            /// </summary>
            /// <value><c>true</c> if this instance is not matched; otherwise, <c>false</c>.</value>
            public bool IsNotMatched => this.Mask == null;

            /// <summary>
            /// Gets последний токен в цепочке следующих токенов на том же уровне вложенности.
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
            /// Gets уровень вложенности токена (0 = корень).
            /// </summary>
            /// <value>The level.</value>
            public int Level
            {
                get
                {
                    var level = 0;
                    var node = Parent;

                    while (node != null)
                    {
                        level++;
                        node = node.Parent;
                    }

                    return level;
                }
            }

            /// <summary>
            /// Gets маска токена.
            /// </summary>
            /// <value>The mask.</value>
            public TokenMask Mask { get; internal set; }

            /// <summary>
            /// Gets следующий токен на том же уровне вложенности.
            /// </summary>
            /// <value>The next.</value>
            public Token Next { get; internal set; }

            /// <summary>
            /// Gets родительский токен (null, если токен верхнего уровня).
            /// </summary>
            /// <value>The parent.</value>
            public Token Parent { get; internal set; }

            /// <summary>
            /// Gets индекс конца токена (последний символ суффикса <see cref="Suffix"/>) относительно начала
            /// родительского <see cref="Body"/> родительского токена <see cref="Parent"/>.
            /// </summary>
            /// <value>The parent end.</value>
            public int ParentEnd { get; internal set; }

            /// <summary>
            /// Gets индекс начала токена (первый символ префикса <see cref="Prefix"/>) относительно начала
            /// родительского <see cref="Body"/> родительского токена <see cref="Parent"/>.
            /// </summary>
            /// <value>The parent start.</value>
            public int ParentStart { get; internal set; }

            /// <summary>
            /// Gets префикс токена (например "(").
            /// </summary>
            /// <value>The prefix.</value>
            public string Prefix { get; internal set; }

            /// <summary>
            /// Gets предыдущий токен на том же уровне вложенности.
            /// </summary>
            /// <value>The previous.</value>
            public Token Previous { get; internal set; }

            /// <summary>
            /// Gets корневой токен.
            /// </summary>
            /// <value>The root.</value>
            public Token Root
            {
                get
                {
                    var node = this;
                    while (node.Parent != null)
                        node = node.Parent;

                    return node;
                }
            }

            /// <summary>
            /// Gets исходная строка.
            /// </summary>
            /// <value>The source.</value>
            public string Source { get; internal set; }

            /// <summary>
            /// Gets индекс конца токена (последний символ суффикса <see cref="Suffix"/>) в исходной строке <see
            /// cref="Source"/>.
            /// </summary>
            /// <value>The source end.</value>
            public int SourceEnd { get; internal set; }

            /// <summary>
            /// Gets индекс начала токена (первый символ префикса <see cref="Prefix"/>) в исходной строке <see
            /// cref="Source"/>.
            /// </summary>
            /// <value>The source start.</value>
            public int SourceStart { get; internal set; }

            /// <summary>
            /// Gets суффикс токена (например ")").
            /// </summary>
            /// <value>The suffix.</value>
            public string Suffix { get; internal set; }

            /// <summary>
            /// Gets or sets тег для хранения пользовательских данных.
            /// </summary>
            /// <value>The tag.</value>
            public object Tag { get; set; }

            /// <summary>
            /// Gets внутренний текст токена без префикса и суффикса.
            /// </summary>
            /// <value>The text.</value>
            public string Text { get; internal set; }

            /// <summary>
            /// Gets or sets the identifier internal.
            /// </summary>
            internal static int IdInternal { get; set; } = 1;

            /// <summary>
            /// Gets the children internal.
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

                if (this.Next != null)
                {
                    this.Next.Previous = newToken;
                }

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

                if (this.Previous != null)
                {
                    this.Previous.Next = newToken;
                }

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
                if (this.Previous != null)
                {
                    this.Previous.Next = this.Next;
                }

                if (this.Next != null)
                {
                    this.Next.Previous = this.Previous;
                }

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
            /// Gets or sets a value indicating whether разрешает ли данная маска иметь вложенные токены.
            /// </summary>
            /// <value><c>true</c> if [allow children tokens]; otherwise, <c>false</c>.</value>
            public bool AllowChildrenTokens { get; set; } = true;

            /// <summary>
            /// Gets or sets разрешённые маски для вложенных токенов.
            /// </summary>
            /// <value>The allowed children masks.</value>
            public List<TokenMask> AllowedChildrenMasks { get; set; } = new List<TokenMask>();

            /// <summary>
            /// Gets or sets разрешённые маски для следующих соседних токенов.
            /// </summary>
            /// <value>The allowed next masks.</value>
            public List<TokenMask> AllowedNextMasks { get; set; } = new List<TokenMask>();

            /// <summary>
            /// Gets or sets функция для трансформации содержимого токена.
            /// </summary>
            /// <value>The content transformer.</value>
            public Func<Token, string> ContentTransformer { get; set; }

            /// <summary>
            /// Gets or sets префикс токена.
            /// </summary>
            /// <value>The prefix.</value>
            public string Prefix { get; set; }

            /// <summary>
            /// Gets or sets функция для установки пользовательского тега токена.
            /// </summary>
            /// <value>The set tag.</value>
            public Func<Token, object> SetTag { get; set; }

            /// <summary>
            /// Gets or sets суффикс токена.
            /// </summary>
            /// <value>The suffix.</value>
            public string Suffix { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether выбрасывать ли исключение при попытке добавить неразрешённый вложенный токен.
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