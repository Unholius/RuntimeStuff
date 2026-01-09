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
    using System.Linq;

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
        /// <returns>List&lt;Token&gt;.</returns>
        /// <exception cref="System.InvalidOperationException">Token with Prefix='{tm.Prefix}' and Suffix='{tm.Suffix}' is not allowed to be a child of another token.</exception>
        /// <exception cref="System.InvalidOperationException">Token with Prefix='{tm.Prefix}' and Suffix='{tm.Suffix}' is not allowed to be a next of {prevToken} token.</exception>
        public static List<Token> GetTokens(string input, IEnumerable<TokenMask> tokenMasks, bool notMatchedAsTokens = false, Func<Token, object> notMatchedTokenSetTag = null)
        {
            Token.IdInternal = 1;
            var result = new List<Token>();
            var stack = new Stack<(Token Token, string Prefix, string Suffix, Func<Token, string> ContentTransformer)>();

            // Сортируем маски один раз по убыванию длины префикса
            var masks = tokenMasks.OrderByDescending(m => m.Prefix.Length).ToArray();

            var span = input.AsSpan();
            var i = 0;

            while (i < span.Length)
            {
                var matched = false;

                // Проверка начала токена
                foreach (var tm in masks)
                {
                    if (input[i] != tm.Prefix[0])
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
                            if (!tm.AllowChildrenTokens)
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

                            if (tm.AllowedChildrenMasks?.Any() == true && !tm.AllowedChildrenMasks.Contains(tm))
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
                        if (prevToken?.Mask?.AllowedNextMasks?.Any() == true)
                        {
                            if (!prevToken.Mask.AllowedNextMasks.Contains(tm))
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
                TokenizeNotMatched(result, notMatchedTokenSetTag);
            }

            return result;
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
        /// Tokenizes the not matched.
        /// </summary>
        /// <param name="tokens">The tokens.</param>
        /// <param name="setTag">The set tag.</param>
        public static void TokenizeNotMatched(IEnumerable<Token> tokens, Func<Token, object> setTag)
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
                        TokenizeNotMatched(t.Children, setTag);
                    }

                    if (t.SourceStart > 0 && t.Previous == null)
                    {
                        var plainToken = new Token(t.Source, 0, t.SourceStart - 1, setTag);
                        t.InsertBefore(plainToken);
                        continue;
                    }

                    if (t.Previous != null && t.SourceStart - t.Previous.SourceEnd > 1)
                    {
                        var plainToken = new Token(t.Source, t.Previous.SourceEnd + 1, t.SourceStart - 1, setTag);
                        t.InsertBefore(plainToken);
                        continue;
                    }

                    if (t.Next == null && t.SourceEnd < t.Source.Length - 1)
                    {
                        var plainToken = new Token(t.Source, t.SourceEnd + 1, t.Source.Length - 1, setTag);
                        t.InsertAfter(plainToken);
                    }
                }
                else
                {
                    if (t.Children.Any())
                    {
                        TokenizeNotMatched(t.Children, setTag);
                    }

                    if (t.SourceStart - t.Parent.ParentStart > 1 && t.Previous == null)
                    {
                        var plainToken = new Token(t.Parent.Body, 0 + t.Parent.Prefix.Length, t.ParentStart - 1, setTag);
                        t.InsertBefore(plainToken);
                    }

                    if (t.Previous != null && !t.Previous.IsNotMatched && t.ParentStart - t.Previous.ParentEnd > 1)
                    {
                        var plainToken = new Token(t.Parent.Body, t.Previous.ParentEnd + 1, t.ParentStart - 1, setTag);
                        t.InsertBefore(plainToken);
                    }

                    if (t.Next == null && t.Parent.ParentEnd - t.SourceEnd > 1)
                    {
                        var plainToken = new Token(t.Parent.Body, t.ParentEnd + 1, t.Parent.Body.Length - t.Parent.Suffix.Length - 1, setTag);
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
        /// Представляет токен (выделенную часть строки с учетом префикса и суффикса).
        /// Поддерживает вложенность.
        /// </summary>
        public class Token
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="Token" /> class.
            /// </summary>
            /// <param name="source">The source.</param>
            /// <param name="start">The start.</param>
            /// <param name="end">The end.</param>
            /// <param name="setTag">The set tag.</param>
            public Token(string source, int start, int end, Func<Token, object> setTag = null)
                : this()
            {
                var s = source.Substring(start, end - start + 1);
                this.Body = s;
                this.Text = s;
                this.Source = source;
                this.SourceStart = start;
                this.SourceEnd = end;
                this.Tag = setTag?.Invoke(this);
            }

            /// <summary>
            /// Initializes a new instance of the <see cref="Token" /> class.
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
            /// Initializes a new instance of the <see cref="Token" /> class.
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
            /// Gets итоговое содержимое токена, формируется из Text с учетом вложенных токенов и применённых ContentTransformers.
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
                            result = result.Substring(0, start)
                                     + child.Content
                                     + result.Substring(start + length);
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
            /// Gets or sets пользовательские функции-трансформеры, применяемые к токену с учетом модели, если она указана. По умолчанию равно
            /// значению <see cref="Text" />.
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
                    int i = 0;
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
            public int Level => this.Parent == null ? 0 : this.Parent.Level + 1;

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
            /// Gets индекс конца токена (последний символ суффикса <see cref="Suffix" />) относительно начала родительского <see cref="Body" />
            /// родительского токена <see cref="Parent" />.
            /// </summary>
            /// <value>The parent end.</value>
            public int ParentEnd { get; internal set; }

            /// <summary>
            /// Gets индекс начала токена (первый символ префикса <see cref="Prefix" />) относительно начала родительского <see cref="Body" />
            /// родительского токена <see cref="Parent" />.
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
            public Token Root => this.Parent == null ? this : this.Parent.Root;

            /// <summary>
            /// Gets исходная строка.
            /// </summary>
            /// <value>The source.</value>
            public string Source { get; internal set; }

            /// <summary>
            /// Gets индекс конца токена (последний символ суффикса <see cref="Suffix" />) в исходной строке <see cref="Source" />.
            /// </summary>
            /// <value>The source end.</value>
            public int SourceEnd { get; internal set; }

            /// <summary>
            /// Gets индекс начала токена (первый символ префикса <see cref="Prefix" />) в исходной строке <see cref="Source" />.
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
            public override string ToString() => $"[{this.Prefix}, {this.Suffix}]";
        }
    }
}