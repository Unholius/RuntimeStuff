using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using RuntimeStuff.Helpers;

namespace RuntimeStuff.Extensions
{
    /// <summary>
    ///     Предоставляет методы расширения для работы с последовательностями <see cref="IEnumerable{T}" />,
    ///     включая итерацию, объединение, сортировку, преобразование иерархических данных,
    ///     поиск индексов элементов и проверку условий для элементов.
    /// </summary>
    /// <remarks>
    ///     Эти методы расширяют возможности LINQ и операций с коллекциями, предоставляя дополнительный функционал,
    ///     такой как сортировка по свойствам, иерархическое "сплющивание" данных и предикаты на основе индексов.
    ///     Все методы являются статическими и предназначены для использования в качестве методов расширения
    ///     для типов, реализующих IEnumerable.
    ///     Потокобезопасность зависит от используемой коллекции и переданных делегатов.
    ///     Передача null в качестве аргументов может вызвать исключения; подробности см. в документации к каждому методу.
    /// </remarks>
    public static class EnumerableExtensions
    {
        /// <summary>
        ///     Добавляет элемент в коллекцию.
        /// </summary>
        /// <param name="e">
        ///     Коллекция, в которую необходимо добавить элемент.
        /// </param>
        /// <param name="item">
        ///     Добавляемый элемент.
        /// </param>
        /// <param name="index">
        ///     Индекс, по которому необходимо вставить элемент.
        ///     Если значение равно <c>-1</c>, элемент добавляется в конец коллекции.
        /// </param>
        /// <exception cref="ArgumentNullException">
        ///     Выбрасывается, если <paramref name="e" /> равен <c>null</c>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        ///     Выбрасывается, если коллекция не поддерживает добавление элементов.
        /// </exception>
        public static void Add(this IEnumerable e, object item, int index = -1)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));

            // Проверяем, поддерживает ли коллекция добавление
            if (e is IList list)
            {
                if (index == -1)
                    list.Add(item);
                else
                    list.Insert(index, item);
            }
            else if (e is IList<object> genericList)
            {
                if (index == -1)
                    genericList.Add(item);
                else
                    genericList.Insert(index, item);
            }
            else
            {
                throw new InvalidOperationException("Коллекция не поддерживает добавление элементов.");
            }
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

        /// <summary>
        ///     Удаляет все элементы из коллекции.
        /// </summary>
        /// <param name="e">
        ///     Коллекция, которую необходимо очистить.
        /// </param>
        /// <exception cref="ArgumentNullException">
        ///     Выбрасывается, если <paramref name="e" /> равен <c>null</c>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        ///     Выбрасывается, если коллекция не поддерживает операцию очистки.
        /// </exception>
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
                case ICollection _:
                    // ICollection не имеет Clear(), только Count, но IList наследует ICollection
                    throw new InvalidOperationException("Коллекция не поддерживает Clear.");
                default:
                    throw new InvalidOperationException("Коллекция не поддерживает Clear.");
            }
        }

        /// <summary>
        ///     Определяет, содержит ли последовательность элемент, удовлетворяющий указанному условию.
        /// </summary>
        /// <remarks>
        ///     Если параметр <paramref name="match" /> равен null, будет выброшено исключение
        ///     <see
        ///         cref="ArgumentNullException" />
        ///     . Метод перебирает все элементы последовательности до первого
        ///     совпадения.
        /// </remarks>
        /// <typeparam name="T">Тип элементов в последовательности.</typeparam>
        /// <param name="e">Последовательность элементов, в которой выполняется поиск.</param>
        /// <param name="match">Функция условия, определяющая, какой элемент считается подходящим. Не должна быть равна null.</param>
        /// <returns>
        ///     Значение <see langword="true" />, если хотя бы один элемент последовательности удовлетворяет условию; в противном
        ///     случае — <see langword="false" />.
        /// </returns>
        public static bool Contains<T>(this IEnumerable<T> e, Func<T, bool> match)
        {
            return e.FirstOrDefault(match) != null;
        }

        /// <summary>
        ///     Возвращает уникальные элементы последовательности по заданному ключу.
        ///     Аналог LINQ DistinctBy из .NET 6+.
        /// </summary>
        public static IEnumerable<TSource> DistinctBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

            var seenKeys = new HashSet<TKey>();

            foreach (var element in source)
                if (seenKeys.Add(keySelector(element)))
                    yield return element;
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
        ///     Выполняет указанное действие для каждого элемента последовательности
        ///     и возвращает исходную последовательность.
        /// </summary>
        /// <typeparam name="T">
        ///     Тип элементов последовательности.
        /// </typeparam>
        /// <param name="source">
        ///     Исходная последовательность элементов.
        /// </param>
        /// <param name="action">
        ///     Действие, выполняемое для каждого элемента последовательности.
        /// </param>
        /// <returns>
        ///     Исходная последовательность <paramref name="source" />,
        ///     что позволяет использовать метод в цепочках вызовов.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///     Выбрасывается, если <paramref name="source" /> или <paramref name="action" /> равны <c>null</c>.
        /// </exception>
        public static IEnumerable<T> ForEach<T>(this IEnumerable<T> source, Action<T> action)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (action == null) throw new ArgumentNullException(nameof(action));
            foreach (var item in source) action(item);
            return source;
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
        ///     Возвращает индекс первого вхождения указанного элемента в последовательности,
        ///     начиная с заданной позиции.
        /// </summary>
        /// <typeparam name="T">Тип элементов в последовательности.</typeparam>
        /// <param name="e">Последовательность, в которой выполняется поиск.</param>
        /// <param name="item">Элемент, индекс которого необходимо найти.</param>
        /// <param name="fromIndex">Начальный индекс, с которого начинается поиск (нумерация с нуля).</param>
        /// <returns>
        ///     Индекс первого вхождения элемента, если он найден;
        ///     в противном случае — <c>-1</c>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///     Если <paramref name="e" /> имеет значение <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     Если <paramref name="fromIndex" /> меньше нуля.
        /// </exception>
        public static int IndexOf<T>(this IEnumerable<T> e, T item, int fromIndex)
        {
            return e.IndexOf(item, fromIndex, EqualityComparer<T>.Default);
        }

        /// <summary>
        ///     Возвращает индекс первого вхождения указанного элемента в последовательности,
        ///     начиная с заданной позиции и используя заданный компаратор для сравнения элементов.
        /// </summary>
        /// <typeparam name="T">Тип элементов в последовательности.</typeparam>
        /// <param name="e">Последовательность, в которой выполняется поиск.</param>
        /// <param name="item">Элемент, индекс которого необходимо найти.</param>
        /// <param name="fromIndex">Начальный индекс, с которого начинается поиск (нумерация с нуля).</param>
        /// <param name="comparer">
        ///     Объект <see cref="IEqualityComparer{T}" />, используемый для сравнения элементов.
        ///     Если значение <c>null</c>, используется <see cref="EqualityComparer{T}.Default" />.
        /// </param>
        /// <returns>
        ///     Индекс первого вхождения элемента, если он найден;
        ///     в противном случае — <c>-1</c>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///     Если <paramref name="e" /> имеет значение <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     Если <paramref name="fromIndex" /> меньше нуля.
        /// </exception>
        public static int IndexOf<T>(this IEnumerable<T> e, T item, int fromIndex, IEqualityComparer<T> comparer)
        {
            if (e == null)
                throw new ArgumentNullException(nameof(e));

            if (fromIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(fromIndex), "fromIndex не может быть меньше нуля.");

            if (comparer == null)
                comparer = EqualityComparer<T>.Default;

            var index = 0;
            foreach (var element in e)
            {
                if (index >= fromIndex && comparer.Equals(element, item))
                    return index;

                index++;
            }

            return -1;
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
        ///     Преобразует каждый элемент последовательности, выполняя дополнительное действие.
        /// </summary>
        /// <typeparam name="TSource">Тип элементов исходной последовательности.</typeparam>
        /// <typeparam name="TResult">Тип элементов результирующей последовательности.</typeparam>
        /// <param name="source">Исходная последовательность.</param>
        /// <param name="selector">Функция преобразования элемента в результат.</param>
        /// <param name="doAction">
        ///     Действие, выполняемое для каждого элемента (элемент и его индекс).
        /// </param>
        /// <returns>Новая последовательность с результатами преобразования.</returns>
        /// <exception cref="ArgumentNullException">
        ///     Если <paramref name="source" /> или <paramref name="selector" /> равен <c>null</c>.
        /// </exception>
        public static IEnumerable<TResult> SelectDo<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TResult> selector, Action<TResult, int> doAction = null)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            return selector == null ? throw new ArgumentNullException(nameof(selector)) : SelectDoIterator(source, selector, doAction);
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

        /// <summary>
        ///     Сортирует последовательность по указанным свойствам по возрастанию.
        /// </summary>
        public static IOrderedEnumerable<T> SortAsc<T>(this IEnumerable<T> source, params string[] propertyNames)
            where T : class
        {
            return source.Sort(ListSortDirection.Ascending, propertyNames);
        }

        /// <summary>
        ///     Дополняет сортировку (ThenBy) по указанным свойствам по возрастанию.
        /// </summary>
        public static IOrderedEnumerable<T> SortAsc<T>(this IOrderedEnumerable<T> source, params string[] propertyNames)
            where T : class
        {
            return source.Sort(ListSortDirection.Ascending, propertyNames);
        }

        /// <summary>
        ///     Сортирует последовательность по указанным свойствам по убыванию.
        /// </summary>
        public static IOrderedEnumerable<T> SortDesc<T>(this IEnumerable<T> source, params string[] propertyNames)
            where T : class
        {
            return source.Sort(ListSortDirection.Descending, propertyNames);
        }

        /// <summary>
        ///     Дополняет сортировку (ThenByDescending) по указанным свойствам по убыванию.
        /// </summary>
        public static IOrderedEnumerable<T> SortDesc<T>(this IOrderedEnumerable<T> source, params string[] propertyNames)
            where T : class
        {
            return source.Sort(ListSortDirection.Descending, propertyNames);
        }

        /// <summary>
        ///     Преобразует последовательность в словарь, игнорируя повторяющиеся ключи.
        /// </summary>
        /// <typeparam name="TSource">Тип элементов последовательности.</typeparam>
        /// <typeparam name="TKey">Тип ключа словаря.</typeparam>
        /// <typeparam name="TValue">Тип значения словаря.</typeparam>
        /// <param name="source">Исходная последовательность.</param>
        /// <param name="keySelector">Функция для получения ключа.</param>
        /// <param name="valueSelector">Функция для получения значения.</param>
        /// <returns>Словарь с уникальными ключами.</returns>
        public static Dictionary<TKey, TValue> ToDictionaryDistinct<TSource, TKey, TValue>(
            this IEnumerable<TSource> source,
            Func<TSource, TKey> keySelector,
            Func<TSource, TValue> valueSelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));
            if (valueSelector == null) throw new ArgumentNullException(nameof(valueSelector));

            var dict = new Dictionary<TKey, TValue>();
            foreach (var item in source)
            {
                var key = keySelector(item);
                if (!dict.ContainsKey(key)) dict[key] = valueSelector(item);
            }

            return dict;
        }

        /// <summary>
        ///     Преобразует последовательность в словарь, игнорируя повторяющиеся ключи.
        ///     Значение совпадает с элементом последовательности.
        /// </summary>
        public static Dictionary<TKey, TSource> ToDictionaryDistinct<TSource, TKey>(
            this IEnumerable<TSource> source,
            Func<TSource, TKey> keySelector)
        {
            return source.ToDictionaryDistinct(keySelector, x => x);
        }

        /// <summary>
        /// Преобразует последовательность объектов в экземпляр DataTable с возможностью выбора имени таблицы и набора
        /// столбцов.
        /// </summary>
        /// <remarks>Если параметр columnSelectors не задан, в таблицу будут включены все публичные
        /// свойства типа T. Метод полезен для экспорта коллекций в табличный вид, например, для последующей
        /// сериализации или отображения.</remarks>
        /// <typeparam name="T">Тип элементов в исходной последовательности. Должен быть ссылочным типом.</typeparam>
        /// <param name="source">Исходная последовательность объектов, которые будут преобразованы в строки таблицы.</param>
        /// <param name="tableName">Имя создаваемой таблицы. Если не указано, используется имя типа T.</param>
        /// <param name="columnSelectors">Массив выражений, определяющих, какие свойства или поля типа T будут включены в таблицу в качестве столбцов.
        /// Если не указано, включаются все публичные свойства.</param>
        /// <returns>Объект DataTable, содержащий данные из исходной последовательности. Если последовательность пуста,
        /// возвращается таблица только со структурой столбцов.</returns>
        public static DataTable ToDataTable<T>(this IEnumerable<T> source, string tableName = null, params Expression<Func<T, object>>[] columnSelectors) where T : class
        {
            return DataTableHelper.ToDataTable(source, tableName, columnSelectors);
        }

        /// <summary>
        ///     Преобразует элементы последовательности в строку, соединяя их
        ///     с использованием указанного разделителя.
        /// </summary>
        /// <typeparam name="T">
        ///     Тип элементов последовательности.
        /// </typeparam>
        /// <param name="source">
        ///     Последовательность элементов, которые будут объединены в строку.
        /// </param>
        /// <param name="separator">
        ///     Разделитель, который будет использоваться между элементами строки.
        ///     По умолчанию используется запятая и пробел (<c>", "</c>).
        /// </param>
        /// <returns>
        ///     Строка, содержащая элементы последовательности, разделённые указанным разделителем.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///     Выбрасывается, если <paramref name="source" /> равна <c>null</c>.
        /// </exception>
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
    }
}