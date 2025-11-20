using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using RuntimeStuff.Helpers;

namespace RuntimeStuff.Extensions
{
    /// <summary>
    /// Предоставляет методы расширения для работы с последовательностями <see cref="IEnumerable{T}"/>,
    /// включая итерацию, объединение, сортировку, преобразование иерархических данных,
    /// поиск индексов элементов и проверку условий для элементов.
    /// </summary>
    /// <remarks>
    /// Эти методы расширяют возможности LINQ и операций с коллекциями, предоставляя дополнительный функционал,
    /// такой как сортировка по свойствам, иерархическое "сплющивание" данных и предикаты на основе индексов.
    /// Все методы являются статическими и предназначены для использования в качестве методов расширения
    /// для типов, реализующих IEnumerable.
    /// Потокобезопасность зависит от используемой коллекции и переданных делегатов.
    /// Передача null в качестве аргументов может вызвать исключения; подробности см. в документации к каждому методу.
    /// </remarks>
    public static class EnumerableExtensions
    {
        public static IEnumerable<T> ForEach<T>(this IEnumerable<T> source, Action<T> action)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (action == null) throw new ArgumentNullException(nameof(action));
            foreach (var item in source) action(item);
            return source;
        }

        public static string ToJoinedString<T>(this IEnumerable<T> source, string separator = ", ")
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var sb = new StringBuilder();
            var first = true;
            foreach (var item in source)
            {
                if (!first)
                    sb.Append(separator);
                sb.Append(item);
                first = false;
            }

            return sb.ToString();
        }

        public static void Add(this IEnumerable e, object item)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));

            // Проверяем, поддерживает ли коллекция добавление
            if (e is IList list)
            {
                list.Add(item);
            }
            else if (e is IList<object> genericList)
            {
                genericList.Add(item);
            }
            else
            {
                throw new InvalidOperationException("Коллекция не поддерживает добавление элементов.");
            }
        }

        public static void Clear(this IEnumerable e)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));

            switch (e)
            {
                case IList list:
                    list.Clear();
                    break;
                case IList<object> genericList:
                    genericList.Clear();
                    break;
                case ICollection collection:
                    // ICollection не имеет Clear(), только Count, но IList наследует ICollection
                    throw new InvalidOperationException("Коллекция не поддерживает Clear.");
                default:
                    throw new InvalidOperationException("Коллекция не поддерживает Clear.");
            }
        }

        /// <summary>
        ///     Сортирует последовательность по указанным свойствам в заданном порядке.
        /// </summary>
        /// <typeparam name="T">Тип элементов последовательности.</typeparam>
        /// <param name="source">Исходная коллекция.</param>
        /// <param name="order">Порядок сортировки.</param>
        /// <param name="propertyNames">Имена свойств для сортировки.</param>
        /// <returns>Отсортированная коллекция.</returns>
        public static IOrderedEnumerable<T> Sort<T>(this IEnumerable<T> source, ListSortDirection order, params string[] propertyNames) where T : class
        {
            return SortHelper.Sort(source, order, propertyNames);
        }

        public static IOrderedEnumerable<T> Sort<T>(this IEnumerable<T> source, ListSortDescriptionCollection sortDescription) where T : class
        {
            return SortHelper.Sort(source, sortDescription);
        }

        public static IEnumerable<T> Filter<T>(this IEnumerable<T> source, string filterExpression) where T : class
        {
            return FilterHelper.Filter(source, filterExpression);
        }

        public static IEnumerable<T> FilterByText<T>(this IEnumerable<T> source, string text) where T : class
        {
            return FilterHelper.FilterByText(source, text);
        }

        /// <summary>
        ///     Сортирует последовательность по указанным свойствам по возрастанию.
        /// </summary>
        public static IOrderedEnumerable<T> SortAsc<T>(this IEnumerable<T> source, params string[] propertyNames)
            where T : class
        {
            return Sort(source, ListSortDirection.Ascending, propertyNames);
        }

        /// <summary>
        ///     Дополняет сортировку (ThenBy) по указанным свойствам по возрастанию.
        /// </summary>
        public static IOrderedEnumerable<T> SortAsc<T>(this IOrderedEnumerable<T> source, params string[] propertyNames)
            where T : class
        {
            return Sort(source, ListSortDirection.Ascending, propertyNames);
        }

        /// <summary>
        ///     Сортирует последовательность по указанным свойствам по убыванию.
        /// </summary>
        public static IOrderedEnumerable<T> SortDesc<T>(this IEnumerable<T> source, params string[] propertyNames)
            where T : class
        {
            return Sort(source, ListSortDirection.Descending, propertyNames);
        }

        /// <summary>
        ///     Дополняет сортировку (ThenByDescending) по указанным свойствам по убыванию.
        /// </summary>
        public static IOrderedEnumerable<T> SortDesc<T>(this IOrderedEnumerable<T> source, params string[] propertyNames)
            where T : class
        {
            return Sort(source, ListSortDirection.Descending, propertyNames);
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
        public static List<StringHelper.Token> Flatten(this IEnumerable<StringHelper.Token> tokens, Func<StringHelper.Token, bool> predicate = null)
        {
            return StringHelper.Flatten(tokens, predicate);
        }

        /// <summary>
        ///     Возвращает индекс первого элемента, удовлетворяющего условию.
        /// </summary>
        /// <typeparam name="T">Тип элементов коллекции.</typeparam>
        /// <param name="e">Исходная коллекция.</param>
        /// <param name="match">Условие поиска.</param>
        /// <param name="reverseSearch">Если true — поиск с конца.</param>
        /// <returns>Индекс найденного элемента или -1, если не найден.</returns>
        public static int IndexOf<T>(this IEnumerable<T> e, Func<T, bool> match, bool reverseSearch = false)
        {
            return e.IndexOf((x, _) => match(x), reverseSearch);
        }

        /// <summary>
        /// Определяет, содержит ли последовательность элемент, удовлетворяющий указанному условию.
        /// </summary>
        /// <remarks>Если параметр <paramref name="match"/> равен null, будет выброшено исключение <see
        /// cref="ArgumentNullException"/>. Метод перебирает все элементы последовательности до первого
        /// совпадения.</remarks>
        /// <typeparam name="T">Тип элементов в последовательности.</typeparam>
        /// <param name="e">Последовательность элементов, в которой выполняется поиск.</param>
        /// <param name="match">Функция условия, определяющая, какой элемент считается подходящим. Не должна быть равна null.</param>
        /// <returns>Значение <see langword="true"/>, если хотя бы один элемент последовательности удовлетворяет условию; в противном
        /// случае — <see langword="false"/>.</returns>
        public static bool Contains<T>(this IEnumerable<T> e, Func<T, bool> match)
        {
            return e.FirstOrDefault(match) != null;
        }

        /// <summary>
        /// Возвращает индекс первого вхождения указанного элемента в последовательности, 
        /// начиная с заданной позиции.
        /// </summary>
        /// <typeparam name="T">Тип элементов в последовательности.</typeparam>
        /// <param name="e">Последовательность, в которой выполняется поиск.</param>
        /// <param name="item">Элемент, индекс которого необходимо найти.</param>
        /// <param name="fromIndex">Начальный индекс, с которого начинается поиск (нумерация с нуля).</param>
        /// <returns>
        /// Индекс первого вхождения элемента, если он найден; 
        /// в противном случае — <c>-1</c>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Если <paramref name="e"/> имеет значение <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Если <paramref name="fromIndex"/> меньше нуля.
        /// </exception>
        public static int IndexOf<T>(this IEnumerable<T> e, T item, int fromIndex)
        {
            return IndexOf(e, item, fromIndex, EqualityComparer<T>.Default);
        }

        /// <summary>
        /// Возвращает индекс первого вхождения указанного элемента в последовательности, 
        /// начиная с заданной позиции и используя заданный компаратор для сравнения элементов.
        /// </summary>
        /// <typeparam name="T">Тип элементов в последовательности.</typeparam>
        /// <param name="e">Последовательность, в которой выполняется поиск.</param>
        /// <param name="item">Элемент, индекс которого необходимо найти.</param>
        /// <param name="fromIndex">Начальный индекс, с которого начинается поиск (нумерация с нуля).</param>
        /// <param name="comparer">
        /// Объект <see cref="IEqualityComparer{T}"/>, используемый для сравнения элементов.  
        /// Если значение <c>null</c>, используется <see cref="EqualityComparer{T}.Default"/>.
        /// </param>
        /// <returns>
        /// Индекс первого вхождения элемента, если он найден; 
        /// в противном случае — <c>-1</c>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Если <paramref name="e"/> имеет значение <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Если <paramref name="fromIndex"/> меньше нуля.
        /// </exception>
        public static int IndexOf<T>(this IEnumerable<T> e, T item, int fromIndex, IEqualityComparer<T> comparer)
        {
            if (e == null)
                throw new ArgumentNullException(nameof(e));

            if (fromIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(fromIndex), "fromIndex не может быть меньше нуля.");

            if (comparer == null)
                comparer = EqualityComparer<T>.Default;

            int index = 0;
            foreach (var element in e)
            {
                if (index >= fromIndex && comparer.Equals(element, item))
                    return index;

                index++;
            }

            return -1;
        }

        /// <summary>
        /// Преобразует каждый элемент последовательности, выполняя дополнительное действие.
        /// </summary>
        /// <typeparam name="TSource">Тип элементов исходной последовательности.</typeparam>
        /// <typeparam name="TResult">Тип элементов результирующей последовательности.</typeparam>
        /// <param name="source">Исходная последовательность.</param>
        /// <param name="selector">Функция преобразования элемента в результат.</param>
        /// <param name="doAction">
        /// Действие, выполняемое для каждого элемента (элемент и его индекс).
        /// </param>
        /// <returns>Новая последовательность с результатами преобразования.</returns>
        /// <exception cref="ArgumentNullException">
        /// Если <paramref name="source"/> или <paramref name="selector"/> равен <c>null</c>.
        /// </exception>
        public static IEnumerable<TResult> SelectDo<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TResult> selector, Action<TResult, int> doAction = null)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            return selector == null ? throw new ArgumentNullException(nameof(selector)) : SelectDoIterator(source, selector, doAction);
        }

        private static IEnumerable<TResult> SelectDoIterator<TSource, TResult>(IEnumerable<TSource> source, Func<TSource, TResult> selector, Action<TResult, int> doAction)
        {
            var index = 0;
            foreach (var item in source)
            {
                var result = selector(item);
                doAction?.Invoke(result, index);
                yield return selector(item);
                index++;
            }
        }

        /// <summary>
        ///     Возвращает индекс первого элемента, удовлетворяющего условию.
        /// </summary>
        /// <typeparam name="T">Тип элементов коллекции.</typeparam>
        /// <param name="e">Исходная коллекция.</param>
        /// <param name="match">Функция условия, принимающая элемент и его индекс.</param>
        /// <param name="reverseSearch">Если true — поиск с конца.</param>
        /// <returns>Индекс найденного элемента или -1, если не найден.</returns>
        public static int IndexOf<T>(this IEnumerable<T> e, Func<T, int, bool> match, bool reverseSearch = false)
        {
            if (e == null)
                return -1;

            // Если исходная коллекция - массив или IList<T>, используем индексацию
            if (e is IList<T> list)
            {
                if (!reverseSearch)
                {
                    for (var i = 0; i < list.Count; i++)
                        if (match(list[i], i))
                            return i;
                }
                else
                {
                    for (var i = list.Count - 1; i >= 0; i--)
                        if (match(list[i], i))
                            return i;
                }

                return -1;
            }

            // Для остальных IEnumerable<T>
            if (!reverseSearch)
            {
                var i = 0;
                foreach (var item in e)
                {
                    if (match(item, i))
                        return i;
                    i++;
                }
            }
            else
            {
                // К сожалению, для IEnumerable<T> без индексации придётся материализовать в список
                var arr = e.ToArray();
                for (var i = arr.Length - 1; i >= 0; i--)
                    if (match(arr[i], i))
                        return i;
            }

            return -1;
        }

        /// <summary>
        ///     Определяет, удовлетворяют ли все элементы коллекции условию.
        /// </summary>
        /// <typeparam name="TSource">Тип элементов коллекции.</typeparam>
        /// <param name="source">Исходная коллекция.</param>
        /// <param name="predicate">Функция условия, принимающая элемент и его индекс.</param>
        /// <returns>True, если все элементы удовлетворяют условию; иначе — false.</returns>
        public static bool All<TSource>(this IEnumerable<TSource> source, Func<TSource, int, bool> predicate)
        {
            if (source == null) throw new NullReferenceException("source");

            if (predicate == null) throw new NullReferenceException("predicate");

            var i = 0;
            foreach (var item in source)
            {
                if (!predicate(item, i)) return false;
                i++;
            }

            return true;
        }
    }
}