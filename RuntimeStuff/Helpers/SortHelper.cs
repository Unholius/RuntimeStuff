using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace RuntimeStuff.Helpers
{
    /// <summary>
    /// Предоставляет статические методы для сортировки коллекций объектов по одному или нескольким свойствам с указанием
    /// порядка сортировки.
    /// </summary>
    /// <remarks>Класс предназначен для упрощения сортировки последовательностей объектов по именам их свойств,
    /// поддерживает сортировку по возрастанию и убыванию, а также последовательную сортировку по нескольким критериям. Все
    /// методы работают с коллекциями, реализующими интерфейс IEnumerable{T}, и возвращают отсортированные коллекции, не
    /// изменяя исходные данные. Для корректной работы сортировки свойства должны быть доступны для чтения и поддерживать
    /// сравнение значений. Класс потокобезопасен, так как не хранит состояние.</remarks>
    public static class SortHelper
    {
        /// <summary>
        ///     Сортирует последовательность по указанным свойствам в заданном порядке.
        /// </summary>
        /// <typeparam name="T">Тип элементов последовательности.</typeparam>
        /// <param name="source">Исходная коллекция.</param>
        /// <param name="order">Порядок сортировки.</param>
        /// <param name="propertyNames">Имена свойств для сортировки.</param>
        /// <returns>Отсортированная коллекция.</returns>
        public static IOrderedEnumerable<T> Sort<T>(IEnumerable<T> source, ListSortDirection order, params string[] propertyNames) where T : class
        {
            return ApplySort(source, propertyNames.Select(x => (x, order)).ToArray());
        }

        public static IOrderedEnumerable<T> Sort<T>(IEnumerable<T> source, ListSortDescriptionCollection sorts) where T : class
        {
            return ApplySort(source, sorts.Cast<ListSortDescription>().Select(x => (x.PropertyDescriptor.Name, x.SortDirection)).ToArray());
        }

        /// <summary>
        ///     Выполняет дополнительную сортировку (ThenBy/ThenByDescending) для уже отсортированной последовательности.
        /// </summary>
        /// <typeparam name="T">Тип элементов последовательности.</typeparam>
        /// <param name="source">Отсортированная коллекция.</param>
        /// <param name="order">Порядок сортировки.</param>
        /// <param name="propertyNames">Имена свойств для сортировки.</param>
        /// <returns>Повторно отсортированная коллекция.</returns>
        public static IOrderedEnumerable<T> Sort<T>(IOrderedEnumerable<T> source, ListSortDirection order,
            params string[] propertyNames) where T : class
        {
            return ApplySort(source, propertyNames.Select(x => (x, order)).ToArray());
        }

        /// <summary>
        ///     Сортирует последовательность по указанным свойствам по возрастанию.
        /// </summary>
        public static IOrderedEnumerable<T> SortAsc<T>(IEnumerable<T> source, params string[] propertyNames) where T : class
        {
            return Sort(source, ListSortDirection.Ascending, propertyNames);
        }

        /// <summary>
        ///     Дополняет сортировку (ThenBy) по указанным свойствам по возрастанию.
        /// </summary>
        public static IOrderedEnumerable<T> SortAsc<T>(IOrderedEnumerable<T> source, params string[] propertyNames)
            where T : class
        {
            return Sort(source, ListSortDirection.Ascending, propertyNames);
        }

        /// <summary>
        ///     Сортирует последовательность по указанным свойствам по убыванию.
        /// </summary>
        public static IOrderedEnumerable<T> SortDesc<T>(IEnumerable<T> source, params string[] propertyNames)
            where T : class
        {
            return Sort(source, ListSortDirection.Descending, propertyNames);
        }

        /// <summary>
        ///     Дополняет сортировку (ThenByDescending) по указанным свойствам по убыванию.
        /// </summary>
        public static IOrderedEnumerable<T> SortDesc<T>(IOrderedEnumerable<T> source, params string[] propertyNames)
            where T : class
        {
            return Sort(source, ListSortDirection.Descending, propertyNames);
        }

        /// <summary>
        ///     Внутренний метод сортировки по нескольким свойствам.
        /// </summary>
        private static IOrderedEnumerable<T> ApplySort<T>(IEnumerable<T> source, params (string propertyName, ListSortDirection order)[] sorts) where T : class
        {
            if (sorts == null || sorts.Length == 0)
                throw new ArgumentException("Нужно указать хотя бы одно свойство.");

            IOrderedEnumerable<T> result = null;

            var accessor = sorts.Select((x, i) => Obj.PropertyGetterCache.Get(Obj.FindMember(typeof(T), sorts[i].propertyName) as PropertyInfo)).ToArray<Func<T, object>>();

            for (var i = 0; i < sorts.Length; i++)
            {
                if (i == 0 && !(source is IOrderedEnumerable<T>))
                {
                    result = sorts[i].order == ListSortDirection.Ascending
                        ? source.OrderBy(accessor[i])
                        : source.OrderByDescending(accessor[i]);
                }
                else
                {
                    result = sorts[i].order == ListSortDirection.Ascending
                        ? (result ?? (IOrderedEnumerable<T>)source).ThenBy(accessor[i])
                        : (result ?? (IOrderedEnumerable<T>)source).ThenByDescending(accessor[i]);
                }
            }

            return result;
        }
    }
}