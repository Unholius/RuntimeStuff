using System;
using System.Collections.Generic;
using System.Linq;

namespace RuntimeStuff.Helpers
{
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
        /// Метод удаляет указанный суффикс с конца строки, если он существует
        /// </summary>
        /// <param name="s">Исходная строка, из которой нужно удалить суффикс</param>
        /// <param name="subStr">Строка-суффикс, которую нужно удалить с конца</param>
        /// <param name="comparison">Тип сравнения строк при проверке суффикса</param>
        /// <returns>Строка без указанного суффикса в конце, если он был найден</returns>
        /// <remarks>
        /// Метод проверяет заканчивается ли исходная строка указанным суффиксом.
        /// Если суффикс найден, возвращается строка без этого суффикса.
        /// Если суффикс не найден или параметры пустые, возвращается исходная строка.
        /// </remarks>
        public static string TrimEnd(string s, string subStr, StringComparison comparison = StringComparison.Ordinal)
        {
            // Проверка на пустые входные данные
            if (string.IsNullOrEmpty(s) || string.IsNullOrEmpty(subStr))
                return s;

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
        ///     Заменяет часть строки в диапазоне [startIndex..endIndex] на указанную строку.
        /// </summary>
        /// <param name="s">Исходная строка.</param>
        /// <param name="startIndex">Начальная позиция (включительно).</param>
        /// <param name="endIndex">Конечная позиция (включительно).</param>
        /// <param name="replaceString">Строка для замены.</param>
        /// <returns>Новая строка с заменой.</returns>
        public static string Replace(string s, int startIndex, int endIndex, string replaceString)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            if (startIndex < 0 || startIndex > s.Length)
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            if (endIndex < startIndex || endIndex > s.Length)
                throw new ArgumentOutOfRangeException(nameof(endIndex));

            return s.Substring(0, startIndex)
                   + replaceString
                   + s.Substring(endIndex + 1);
        }

        /// <summary>
        ///     Удаляет часть строки в диапазоне [startIndex..endIndex]. Работает как s.Substring(0, startIndex) +
        ///     s.Substring(endIndex + 1);
        /// </summary>
        /// <param name="s">Исходная строка.</param>
        /// <param name="startIndex">Начальная позиция (включительно).</param>
        /// <param name="endIndex">Конечная позиция (включительно).</param>
        public static string Cut(string s, int startIndex, int endIndex)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            if (startIndex < 0 || startIndex > s.Length)
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            if (endIndex < startIndex || endIndex > s.Length)
                throw new ArgumentOutOfRangeException(nameof(endIndex));

            return s.Substring(0, startIndex) + s.Substring(endIndex + 1);
        }

        /// <summary>
        ///     Возвращает часть строки в диапазоне [startIndex..endIndex]. Работает как string.Substring(s, startIndex, endIndex -
        ///     startIndex + 1)
        /// </summary>
        /// <param name="s">Исходная строка.</param>
        /// <param name="startIndex">Начальная позиция (включительно).</param>
        /// <param name="endIndex">Конечная позиция (включительно).</param>
        public static string Crop(string s, int startIndex, int endIndex)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            if (startIndex < 0 || startIndex > s.Length)
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            if (endIndex < startIndex || endIndex > s.Length)
                throw new ArgumentOutOfRangeException(nameof(endIndex));

            return s.Substring(startIndex, endIndex - startIndex + 1);
        }

        /// <summary>
        ///     Разворачивает иерархию токенов в плоский список.
        /// </summary>
        /// <param name="tokens">Корневые токены.</param>
        /// <param name="predicate">
        ///     Необязательный фильтр. Если указан — возвращаются только те токены, для которых predicate ==
        ///     true.
        /// </param>
        /// <returns>Плоский список токенов.</returns>
        /// <example>
        ///     Пример:
        ///     <code>
        /// var s = "Hello (one(two))";
        /// var tokens = StringTokenizer.GetTokens(s, ("(", ")")).Flatten();
        /// // tokens[0] -> "(one(two))"
        /// // tokens[1] -> "(two)"
        /// </code>
        /// </example>
        public static List<Token> Flatten(IEnumerable<Token> tokens, Func<Token, bool> predicate = null)
        {
            var result = new List<Token>();

            void Recurse(Token t)
            {
                if (predicate == null || predicate(t))
                    result.Add(t);

                foreach (var child in t.Children)
                    Recurse(child);
            }

            foreach (var t in tokens)
                Recurse(t);

            return result;
        }

        /// <summary>
        ///     Получает список токенов по нескольким маскам.
        ///     Маски задаются как кортеж (Prefix, Suffix, ContentTransformer).
        /// </summary>
        /// <param name="input">Входная строка.</param>
        /// <param name="prefix">Префикс токена.</param>
        /// <param name="suffix">Суффикс токена.</param>
        /// <returns>
        ///     Список корневых токенов. Для получения всех токенов в виде массива использовать
        ///     <see cref="Flatten(IEnumerable{Token}, Func{Token, bool})" />
        /// </returns>
        /// <example>
        ///     Пример:
        ///     <code>
        /// var s = "Hello ( one ( two ( three ) ) )";
        /// var tokens = StringTokenizer.GetTokens(
        ///     s,
        ///     ("(", ")", t => t.Text.Trim())
        /// ).Flatten();
        /// 
        /// var c1 = tokens[0].Content; // "one two three"
        /// var c2 = tokens[1].Content; // "two three"
        /// var c3 = tokens[2].Content; // "three"
        /// </code>
        /// </example>
        public static List<Token> GetTokens(string input, string prefix, string suffix, Func<Token, string> serializer = null)
        {
            return GetTokens(input, (prefix, suffix, serializer));
        }

        /// <summary>
        ///     Получает список токенов по нескольким маскам.
        ///     Маски задаются как кортеж (Prefix, Suffix, ContentTransformer).
        /// </summary>
        /// <param name="input">Входная строка.</param>
        /// <param name="tokenMasks">Маски токенов (префикс, суффикс, функция сериализации).</param>
        /// <returns>
        ///     Список корневых токенов. Для получения всех токенов в виде массива использовать
        ///     <see cref="Flatten(IEnumerable{Token}, Func{Token, bool})" />
        /// </returns>
        /// <example>
        ///     Пример:
        ///     <code>
        /// var s = "Hello ( one ( two ( three ) ) )";
        /// var tokens = StringTokenizer.GetTokens(
        ///     s,
        ///     ("(", ")", t => t.Text.Trim())
        /// ).Flatten();
        /// 
        /// var c1 = tokens[0].Content; // "one two three"
        /// var c2 = tokens[1].Content; // "two three"
        /// var c3 = tokens[2].Content; // "three"
        /// </code>
        /// </example>
        public static List<Token> GetTokens(string input, params (string Prefix, string Suffix)[] tokenMasks)
        {
            return GetTokens(input, tokenMasks.Select(x => (x.Prefix, x.Suffix, (Func<Token, string>)null)).ToArray());
        }

        /// <summary>
        ///     Получает список токенов по нескольким маскам.
        ///     Маски задаются как кортеж (Prefix, Suffix, ContentTransformer).
        /// </summary>
        /// <param name="input">Входная строка.</param>
        /// <param name="tokenMasks">Маски токенов (префикс, суффикс, функция сериализации).</param>
        /// <param name="flatten">
        ///     Разворачивает иерархию токенов в плоский список.
        ///     <see cref="Flatten(IEnumerable{Token}, Func{Token, bool})" />
        /// </param>
        /// <param name="ContentTransformer">
        ///     Пользовательская функция-трансформер для формирования поля <see cref="Token.Content" />
        ///     <returns>
        ///         Список корневых токенов. Для получения всех токенов в виде массива использовать
        ///         <see cref="Flatten(IEnumerable{Token}, Func{Token, bool})" />
        ///     </returns>
        ///     <example>
        ///         Пример:
        ///         <code>
        /// var s = "Hello ( one ( two ( three ) ) )";
        /// var tokens = StringTokenizer.GetTokens(
        ///     s,
        ///     ("(", ")", t => t.Text.Trim())
        /// ).Flatten();
        /// 
        /// var c1 = tokens[0].Content; // "one two three"
        /// var c2 = tokens[1].Content; // "two three"
        /// var c3 = tokens[2].Content; // "three"
        /// </code>
        ///     </example>
        public static List<Token> GetTokens(string input, bool flatten,
            params (string Prefix, string Suffix, Func<Token, string> ContentTransformer)[] tokenMasks)
        {
            var tokens = GetTokens(input, tokenMasks);
            return flatten ? Flatten(tokens) : tokens;
        }

        /// <summary>
        ///     Получает список токенов по нескольким маскам.
        ///     Маски задаются как кортеж (Prefix, Suffix, ContentTransformer).
        /// </summary>
        /// <param name="input">Входная строка.</param>
        /// <param name="tokenMasks">Маски токенов (префикс, суффикс, функция сериализации).</param>
        /// <param name="ContentTransformer">
        ///     Пользовательская функция-трансформер для формирования поля <see cref="Token.Content" />
        ///     <returns>
        ///         Список корневых токенов. Для получения всех токенов в виде массива использовать
        ///         <see cref="Flatten(IEnumerable{Token}, Func{Token, bool})" />
        ///     </returns>
        ///     <example>
        ///         Пример:
        ///         <code>
        /// var s = "Hello ( one ( two ( three ) ) )";
        /// var tokens = StringTokenizer.GetTokens(
        ///     s,
        ///     ("(", ")", t => t.Text.Trim())
        /// ).Flatten();
        /// 
        /// var c1 = tokens[0].Content; // "one two three"
        /// var c2 = tokens[1].Content; // "two three"
        /// var c3 = tokens[2].Content; // "three"
        /// </code>
        ///     </example>
        public static List<Token> GetTokens(string input,
            params (string Prefix, string Suffix, Func<Token, string> ContentTransformer)[] tokenMasks)
        {
            var result = new List<Token>();
            var stack = new Stack<(Token Token, string Prefix, string Suffix, Func<Token, string> ContentTransformer)>();

            var i = 0;
            while (i < input.Length)
            {
                var matched = false;
                // Сортируем маски по убыванию длины префикса, чтобы сначала проверять более длинные префиксы (например, "[[") перед более короткими (например, "[")
                tokenMasks = tokenMasks.OrderByDescending(m => m.Prefix.Length).ToArray();
                // Проверяем начало токена
                foreach (var mask in tokenMasks)
                    if (i + mask.Prefix.Length <= input.Length &&
                        input.Substring(i, mask.Prefix.Length) == mask.Prefix)
                    {
                        var token = new Token
                        {
                            SourceStart = i,
                            Parent = stack.Count == 0 ? null : stack.Peek().Token,
                            Source = input,
                            Prefix = mask.Prefix,
                            Suffix = mask.Suffix
                        };

                        token.ParentStart = token.Parent == null
                            ? i
                            : i - token.Parent.SourceStart;

                        if (mask.ContentTransformer != null)
                            token.ContentTransformers.Add(mask.ContentTransformer);

                        stack.Push((token, mask.Prefix, mask.Suffix, mask.ContentTransformer));
                        i += mask.Prefix.Length;
                        matched = true;
                        break;
                    }

                if (matched) continue;

                // Проверяем конец токена
                if (stack.Count > 0)
                {
                    var (topToken, topPrefix, topSuffix, _) = stack.Peek();
                    if (i + topSuffix.Length <= input.Length &&
                        input.Substring(i, topSuffix.Length) == topSuffix)
                    {
                        stack.Pop();
                        topToken.SourceEnd = i + topSuffix.Length - 1;
                        topToken.ParentEnd = topToken.Parent == null
                            ? i + 1
                            : topToken.SourceEnd - topToken.Parent.SourceStart;

                        topToken.Body =
                            input.Substring(topToken.SourceStart, topToken.SourceEnd - topToken.SourceStart + 1);
                        topToken.Text = topToken.Body.Substring(topPrefix.Length,
                            topToken.Body.Length - topPrefix.Length - topSuffix.Length);

                        if (topToken.Parent != null)
                            topToken.Parent._children.Add(topToken);
                        else
                            result.Add(topToken);

                        i += topSuffix.Length;
                        continue;
                    }
                }

                i++;
            }

            return result;
        }

        /// <summary>
        ///     Представляет токен (выделенную часть строки с учетом префикса и суффикса).
        ///     Поддерживает вложенность.
        /// </summary>
        public class Token
        {
            internal List<Token> _children = new List<Token>();

            internal Token()
            {
            }

            /// <summary>
            ///     Исходный текст токена, включая префикс и суффикс.
            /// </summary>
            public string Body { get; internal set; }

            /// <summary>
            ///     Дочерние токены.
            /// </summary>
            public Token[] Children => _children.ToArray();

            /// <summary>
            ///     Итоговое содержимое токена, формируется из Text с учетом вложенных токенов и применённых ContentTransformers.
            /// </summary>
            public string Content
            {
                get
                {
                    var result = Text ?? string.Empty;

                    if (Children != null && _children.Count > 0)
                        foreach (var child in Children.OrderByDescending(c => c.ParentStart))
                        {
                            var start = child.ParentStart - Prefix.Length;
                            var length = child.ParentEnd - child.ParentStart + 1;
                            result = result.Substring(0, start)
                                     + child.Content
                                     + result.Substring(start + length);
                        }

                    if (ContentTransformers != null && ContentTransformers.Count > 0)
                        foreach (var func in ContentTransformers)
                        {
                            var oldText = Text;
                            try
                            {
                                Text = result;
                                var r = func(this);
                                result = r ?? string.Empty;
                            }
                            finally
                            {
                                Text = oldText;
                            }
                        }

                    return result;
                }
            }

            /// <summary>
            ///     Пользовательские функции-трансформеры, применяемые к токену с учетом модели, если она указана. По умолчанию равно
            ///     значению <see cref="Text" />
            /// </summary>
            public List<Func<Token, string>> ContentTransformers { get; set; } = new List<Func<Token, string>>();

            /// <summary>
            ///     Уровень вложенности токена (0 = корень).
            /// </summary>
            public int Level => Parent == null ? 0 : Parent.Level + 1;

            /// <summary>
            ///     Родительский токен (null, если токен верхнего уровня).
            /// </summary>
            public Token Parent { get; internal set; }

            /// <summary>
            ///     Индекс начала токена (первый символ префикса <see cref="Prefix" />) в исходной строке <see cref="Source" />
            /// </summary>
            public int SourceStart { get; internal set; }

            /// <summary>
            ///     Индекс конца токена (последний символ суффикса <see cref="Suffix" />) в исходной строке <see cref="Source" />
            /// </summary>
            public int SourceEnd { get; internal set; }

            /// <summary>
            ///     Индекс начала токена (первый символ префикса <see cref="Prefix" />) относительно начала <see cref="Body" />
            ///     родительского токена <see cref="Parent" />.
            /// </summary>
            public int ParentStart { get; internal set; }

            /// <summary>
            ///     Индекс конца токена (последний символ суффикса <see cref="Suffix" />) относительно начала <see cref="Body" />
            ///     родительского токена <see cref="Parent" />.
            /// </summary>
            public int ParentEnd { get; internal set; }

            /// <summary>
            ///     Префикс токена (например "(").
            /// </summary>
            public string Prefix { get; internal set; }

            /// <summary>
            ///     Корневой токен.
            /// </summary>
            public Token Root => Parent == null ? this : Parent.Root;

            /// <summary>
            ///     Исходная строка.
            /// </summary>
            public string Source { get; internal set; }

            /// <summary>
            ///     Суффикс токена (например ")").
            /// </summary>
            public string Suffix { get; internal set; }

            /// <summary>
            ///     Внутренний текст токена без префикса и суффикса.
            /// </summary>
            public string Text { get; internal set; }

            public override string ToString()
            {
                return $"{Body} | '{Content}' | ({SourceStart}-{SourceEnd}) | Level={Level}";
            }
        }
    }
}