using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace RuntimeStuff.Extensions
{
    /// <summary>
    /// Набор расширений LINQ и коллекций для удобной работы с объектами, типами и данными.
    /// Поддерживает преобразования, фильтрацию, проверку значений и упрощение операций с коллекциями.
    /// </summary>
    public static class LinqExtensions
    {
#if NETFRAMEWORK

        public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dic, TKey key)
        {
            return dic.TryGetValue(key, out var val) ? val : default;
        }

        public static bool Remove<TKey, TValue>(this IDictionary<TKey, TValue> dic, TKey key, out TValue val)
        {
            var result = dic.TryGetValue(key, out val);
            if (result)
                dic.Remove(key);
            return result;
        }

#endif

        /// <summary>
        /// Преобразовать последовательность в массив если convertType == false: методом Cast<T>().ToArray(), иначе <see cref="TypeHelper.ChangeType{T}(object, IFormatProvider)"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <returns>Всегда возвращает не null массив</returns>
        public static T[] ToArray<T>(this IEnumerable list, bool convertType = false)
        {
            if (list == null || !list.Any())
                return Array.Empty<T>();

            if (!convertType)
                return list.Cast<T>().ToArray();
            return list.Select().Select(x => Obj.ChangeType<T>(x)).ToArray();
        }

        /// <summary>
        /// Проверка содержит ли последовательность хотя бы один элемент
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <returns></returns>
        public static bool Any(this IEnumerable list)
        {
            if (list == null)
                return false;
            foreach (var item in list)
                return true;
            return false;
        }

        /// <summary>
        /// Итератор по последовательности
        /// </summary>
        /// <param name="list"></param>
        /// <returns></returns>
        public static IEnumerable<object> Select(this IEnumerable list)
        {
            foreach (var item in list)
                yield return item;
        }

        /// <summary>
        /// Преобразует массив байтов в строку с использованием указанной кодировки.
        /// Если кодировка не указана, используется UTF8.
        /// </summary>
        /// <param name="bytes">Массив байтов для преобразования.</param>
        /// <param name="encoding">Кодировка (опционально). По умолчанию используется Encoding.UTF8.</param>
        /// <returns>Результат преобразования в строку.</returns>
        public static string GetString(this byte[] bytes, Encoding encoding = null)
        {
            var s = encoding != null ? encoding.GetString(bytes) : Encoding.UTF8.GetString(bytes);
            s = s.Replace("\ufeff", "").Replace("\u200B", "").Replace("п»ї", "");
            return s;
        }

        /// <summary>
        /// Добавить значения в словарь
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="d"></param>
        /// <param name="values"></param>
        public static IDictionary<TKey, TValue> AddRange<TKey, TValue>(this IDictionary<TKey, TValue> d, IEnumerable<KeyValuePair<TKey, TValue>> values)
        {
            foreach (var kv in values)
            {
                d[kv.Key] = kv.Value;
            }
            return d;
        }

        /// <summary>
        /// <see cref="IEnumerable{T}.All"/>
        /// </summary>
        /// <typeparam name="TSource"></typeparam>
        /// <param name="source"></param>
        /// <param name="predicate"></param>
        /// <returns></returns>
        /// <exception cref="NullReferenceException"></exception>
        public static bool All<TSource>(this IEnumerable<TSource> source, Func<TSource, int, bool> predicate)
        {
            if (source == null)
            {
                throw new NullReferenceException("source");
            }

            if (predicate == null)
            {
                throw new NullReferenceException("predicate");
            }

            var i = 0;
            foreach (TSource item in source)
            {
                if (!predicate(item, i))
                {
                    return false;
                }
                i++;
            }

            return true;
        }

        /// <summary>
        /// Конвертировать элементы списка в другой тип
        /// </summary>
        /// <param name="list">Список</param>
        /// <param name="elementType">Новый тип элемента списка</param>
        /// <returns></returns>
        public static IEnumerable<object> Cast(this IEnumerable list, Type elementType)
        {
            foreach (var item in list)
                yield return Obj.ChangeType(item, elementType);
        }

        /// <summary>
        /// Возвращает количество элементов в коллекции.
        /// </summary>
        /// <param name="enumerable"></param>
        /// <returns></returns>
        public static int CountItems(this IEnumerable enumerable)
        {
            if (enumerable == null)
                return 0;

            switch (enumerable)
            {
                case Array array:
                    return array.Length;

                case IList l:
                    return l.Count;

                case ICollection c:
                    return c.Count;
            }

            var count = 0;
            foreach (var _ in enumerable)
                count++;
            return count;
        }

        /// <summary>
        /// Обертка вокруг GroupBy
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <param name="propertySelector"></param>
        /// <returns></returns>
        public static IEnumerable<T> DistinctBy<T>(this IEnumerable<T> list, Func<T, object> propertySelector)
        {
            return list.GroupBy(propertySelector).Select(x => x.FirstOrDefault());
        }

        /// <summary>
        ///     Получить элемент коллекции по указанному индексу
        /// </summary>
        /// <param name="enumerable"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        public static object GetElementAt(this IEnumerable enumerable, int index)
        {
            return enumerable.Cast<object>().ElementAt(index);
        }

#if (NETSTANDARD || NETFRAMEWORK) && !(NET5_0_OR_GREATER || NET)

        /// <summary>
        /// Получить значение из словаря, преобразовав в указанный тип
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="d"></param>
        /// <param name="key"></param>
        /// <param name="defaultValue">Значение по умолчанию, если ключ отсутствует в словаре</param>
        /// <returns></returns>
        public static T GetValueOrDefault<T>(this IDictionary d, object key, T defaultValue = default)
        {
            if (d.Contains(key))
                return Obj.Cast<T>(d[key]);
            return defaultValue;
        }

        /// <summary>
        /// Получить значение из словаря, преобразовав в указанный тип
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="d"></param>
        /// <param name="key"></param>
        /// <param name="defaultValue">Значение по умолчанию, если ключ отсутствует в словаре</param>
        /// <returns></returns>
        public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> d, TKey key, TValue defaultValue = default)
        {
            if (d.TryGetValue(key, out var v))
                return v;
            return defaultValue;
        }

#endif

        /// <summary>
        ///     Получить индекс первого элемента, который соответствует условию
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="e">Коллекция элементов</param>
        /// <param name="match">Условие</param>
        /// <param name="reverseSearch">Искать с конца коллекции</param>
        /// <returns></returns>
        public static int IndexOf<T>(this IEnumerable<T> e, Predicate<T> match, bool reverseSearch = false)
        {
            return e.IndexOf((x, _) => match(x), reverseSearch);
        }

        /// <summary>
        /// Получить дополнительную информацию о типе элемента списка.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <returns></returns>
        public static MemberInfoEx GetElementType<T>(this IEnumerable<T> list)
        {
            var listItemType = typeof(T);
            if (listItemType != null && listItemType != typeof(object))
                return listItemType.GetMemberInfoEx();

            if (list == null)
                return null;

            listItemType = list.GetType().GetMemberInfoEx().ElementType;
            if (listItemType != null && listItemType != typeof(object))
                return listItemType.GetMemberInfoEx();

            listItemType = list.FirstOrDefault()?.GetType();
            if (listItemType != null && listItemType != typeof(object))
                return listItemType.GetMemberInfoEx();

            return null;
        }

        /// <summary>
        ///     Получить индекс первого элемента, который соответствует условию
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="e">Коллекция элементов</param>
        /// <param name="match">Условие</param>
        /// <param name="reverseSearch">Искать с конца коллекции</param>
        /// <returns></returns>
        public static int IndexOf<T>(this IEnumerable<T> e, Func<T, int, bool> match, bool reverseSearch = false)
        {
            if (e == null)
                return -1;
            int i = 0;
            T[] arr = e as T[] ?? e.ToArray();
            if (reverseSearch)
                arr = arr.Reverse().ToArray();
            foreach (T item in arr)
                if (match(item, i))
                {
                    return reverseSearch ? arr.Length - i - 1 : i;
                }
                else
                    i++;
            return -1;
        }

        /// <summary>
        ///     Получить индекс первого элемента, который соответствует условию
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="e">Коллекция элементов</param>
        /// <param name="match">Условие</param>
        /// <param name="reverseSearch">Искать с конца коллекции</param>
        /// <returns></returns>
        public static int IndexOf(this IEnumerable e, Func<object, int, bool> match)
        {
            if (e == null)
                return -1;
            int i = 0;
            foreach (var item in e)
            {
                if (match(item, i))
                {
                    return i;
                }
                else
                    i++;
            }
            return -1;
        }

        /// <summary>
        ///     Получить индекс первого элемента, который соответствует условию
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="e">Коллекция элементов</param>
        /// <param name="match">Условие</param>
        /// <param name="reverseSearch">Искать с конца коллекции</param>
        /// <returns></returns>
        public static int IndexOf(this IEnumerable e, object item)
        {
            return e.IndexOf((x, i) => x == item);
        }

        /// <summary>
        /// Добавляет только те элементы из списка 2, которых нет в списке 1
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list1">The list1.</param>
        /// <param name="list2">The list2.</param>
        public static void Merge<T>(this ICollection<T> list1, IEnumerable<T> list2)
        {
            var items = list2.Where(x => !list1.Contains(x));
            foreach (var item in items)
                list1.Add(item);
        }

        /// <summary>
        /// Переместить элемент внутри списка
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list">Список</param>
        /// <param name="fromIndex">С какого индекса переместить элемент</param>
        /// <param name="toIndex">В какой индекс переместить элемент</param>
        public static void Move<T>(this IList<T> list, int fromIndex, int toIndex)
        {
            T item = list[fromIndex];
            list.RemoveAt(fromIndex);
            list.Insert(toIndex, item);
        }

        /// <summary>
        /// <see cref="IEnumerable{T}.OfType()"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="predicate"></param>
        /// <returns></returns>
        public static IEnumerable<T> OfType<T>(this IEnumerable<T> source, Func<T, bool> predicate)
        {
            foreach (object item in source)
            {
                if (item is T it && (predicate == null || predicate(it)))
                {
                    yield return it;
                }
            }
        }

        /// <summary>
        /// Удалить значения из словаря
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="d"></param>
        /// <param name="values"></param>
        public static IDictionary<TKey, TValue> RemoveRange<TKey, TValue>(this IDictionary<TKey, TValue> d, Func<TValue, bool> predicate)
        {
            foreach (var kv in d)
            {
                if (predicate(kv.Value))
                    d.Remove(kv);
            }
            return d;
        }

        /// <summary>
        /// Преобразовать последовательность в массив
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <returns></returns>
        public static T[] ToArray<T>(this IEnumerable list)
        {
            if (list is T[] l)
                return l;
            var result = new List<T>();
            foreach (var item in list)
                result.Add(Obj.Cast<T>(item, false));
            return result.ToArray();
        }

        /// <summary>
        /// Создать новый элемент списка и добавить его в коллекцию
        /// </summary>
        /// <param name="list"></param>
        /// <returns>Созданный элемент списка</returns>
        public static object CreateAndAddItem(this IList list, params object[] newItemCtorArgs)
        {
            if (list == null)
                return null;
            var listItem = CreateItem(list, newItemCtorArgs);
            list.Add(listItem);
            return listItem;
        }

        /// <summary>
        /// Создать новый элемент списка
        /// </summary>
        /// <param name="list"></param>
        /// <returns>Созданный элемент списка</returns>
        public static object CreateItem(this IList list, params object[] newItemCtorArgs)
        {
            if (list == null)
                return null;
            var listType = list.GetType().GetMemberInfoEx();
            var listItem = Obj.New(listType.ElementType, newItemCtorArgs);
            return listItem;
        }

        /// <summary>
        /// Создать новый элемент списка
        /// </summary>
        /// <param name="list"></param>
        /// <returns>Созданный элемент списка</returns>
        public static T CreateItem<T>(this IList<T> list, params object[] newItemCtorArgs)
        {
            if (list == null)
                throw new NullReferenceException($"{nameof(CreateItem)}.{nameof(list)}");
            var listItem = Obj.New<T>(newItemCtorArgs);
            return listItem;
        }

        /// <summary>
        /// Создать новый элемент списка и добавить его в коллекцию
        /// </summary>
        /// <param name="list"></param>
        /// <returns>Созданный элемент списка</returns>
        public static T CreateAndAddItem<T>(this IList<T> list, params object[] newItemCtorArgs)
        {
            if (list == null)
                throw new NullReferenceException(nameof(list));
            var listItem = Obj.New<T>(newItemCtorArgs);
            list.Add(listItem);
            return listItem;
        }

        //TODO: fix!
        /// <summary>
        /// Преобразовать список в строку с разделителями
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list">Список</param>
        /// <param name="keySelector">Выбор свойства элемента списка. Если не указано, то берется сам элемент</param>
        /// <param name="valueSeparator">Разделитель</param>
        /// <param name="formatProvider">The format provider.</param>
        /// <returns></returns>
        public static string ToCsv<T>(this IEnumerable<T> list, Expression<Func<T, object>> keySelector = null, string valueSeparator = ",", string lineTerminator = "\r\n", IFormatProvider formatProvider = null)
        {
            if (list == null)
                return "";
            var listTypeInfo = list.GetType().GetMemberInfoEx();

            if (keySelector == null && listTypeInfo.IsBasicCollection)
                return string.Join(valueSeparator, list.Select(x => string.Format("{0}", x, formatProvider)));

            var sb = new StringBuilder();

            foreach (var item in list)
            {
                sb.Append(keySelector == null ? item.ToString() + lineTerminator : string.Join(valueSeparator, Obj.GetValues(item)) + lineTerminator);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Преобразовать последовательность в <see cref="DataTable"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list">The list.</param>
        /// <returns></returns>
        public static DataTable ToDataTable<T>(this IEnumerable<T> list) where T : class
        {
            var dt = new DataTable();
            dt.ImportData(list);
            return dt;
        }

        /// <summary>
        /// Преобразовать последовательность в список
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <returns></returns>
        public static List<T> ToList<T>(this IEnumerable list)
        {
            if (list is List<T> l)
                return l;
            var result = new List<T>();
            foreach (var item in list)
                result.Add(Obj.Cast<T>(item, false));
            return result;
        }

        /// <summary>
        /// Конвертировать элементы списка в другой тип
        /// </summary>
        /// <param name="list">Список</param>
        /// <param name="elementType">Новый тип элемента списка</param>
        /// <returns></returns>
        public static IEnumerable<object> TryCast(this IEnumerable list, Type elementType)
        {
            foreach (var item in list)
            {
                var result = Obj.TryChangeType(item, elementType, out var i);
                if (!result)
                    continue;
                yield return i;
            }
        }

#if NETSTANDARD || NETFRAMEWORK
#endif

#if NETFRAMEWORK
        /// <summary>
        /// Получить значение по ключу или значение по умолчанию, если ключ отсутствует в словаре
        /// </summary>
        /// <typeparam name="TKey">Тип ключа</typeparam>
        /// <typeparam name="TValue">Тип значения</typeparam>
        /// <param name="dic">Словарь</param>
        /// <param name="key">Значение ключа</param>
        /// <returns></returns>
        public static TValue TryGetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dic, TKey key)
        {
            if (dic.TryGetValue(key, out var value))
                return value;
            else
#pragma warning disable CS8603 // Possible null reference return.
                return default;
#pragma warning restore CS8603 // Possible null reference return.
        }
#endif

        /// <summary>
        ///     Проверка на leftValue >= value >= rightValue или leftValue <= value <= rightValue
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <param name="leftValue"></param>
        /// <param name="rightValue"></param>
        /// <returns></returns>
        public static bool Between<T>(this T value, T leftValue, T rightValue) where T : struct
        {
            return
                Comparer<T>.Default.Compare(value, leftValue) >= 0 && Comparer<T>.Default.Compare(value, rightValue) <= 0 ||
                Comparer<T>.Default.Compare(value, leftValue) <= 0 && Comparer<T>.Default.Compare(value, rightValue) >= 0;
        }

        /// <summary>
        ///     Вернуть первое не Null значение из текущего или из массива
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="objects"></param>
        /// <returns></returns>
        public static T Coalesce<T>(this T obj, params T[] objects)
        {
            var objArr = new T[] { obj };
            objects = objArr.Concat(objects).ToArray();
            return objects.FirstOrDefault(x => !TypeExtensions.NullValues.Contains(x));
        }

        /// <summary>
        ///     Вернуть первое не Null значение из текущего или из массива
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="objects"></param>
        /// <returns></returns>
        public static object Coalesce(this object obj, params object[] objects)
        {
            return Coalesce<object>(obj, objects);
        }

        /// <summary>
        ///     Вернуть первый не Null элемент массива или NullReferenceException, если все элементы null
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="objects"></param>
        /// <returns></returns>
        public static object CoalesceThrow(this object obj, params object[] objects)
        {
            return Coalesce<object>(obj, objects) ?? throw new NullReferenceException("В массиве нет не Null элемента!");
        }

        /// <summary>
        ///     Выполнить действие над каждым элементом коллекции
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="collection"></param>
        /// <param name="action"></param>
        public static IEnumerable<T> ForEach<T>(this IEnumerable<T> collection, Action<T> action)
        {
            foreach (T item in collection)
                action(item);
            return collection;
        }

        /// <summary>
        ///     Выполнить действие над каждым элементом коллекции с передачей индекса элемента
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="collection"></param>
        /// <param name="action"></param>
        public static void ForEach<T>(this IEnumerable<T> collection, Action<T, int> action)
        {
            int idx = 0;
            foreach (T item in collection)
            {
                action(item, idx);
                idx++;
            }
        }

        /// <summary>
        ///     Проверка на obj1 больше obj2
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj1"></param>
        /// <param name="obj2"></param>
        public static bool Greater<T>(this T obj1, T obj2) where T : IComparable
        {
            return Comparer<T>.Default.Compare(obj1, obj2) > 0;
        }

        /// <summary>
        ///     Проверка на obj1 больше или равен obj2
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj1"></param>
        /// <param name="obj2"></param>
        public static bool GreaterOrEqual<T>(this T obj1, T obj2) where T : IComparable
        {
            return Comparer<T>.Default.Compare(obj1, obj2) >= 0;
        }

        /// <summary>
        ///     Вернуть значение, в зависимости от условия
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <param name="condition"></param>
        /// <param name="thenValue"></param>
        /// <returns></returns>
        public static T2 If<T1, T2>(this T1 value, Func<T1, bool> condition, T2 thenValue, T2 elseValue) => condition(value) ? thenValue : elseValue;

        /// <summary>
        ///     Проверка содержит ли массив текущее значение
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        public static bool In<T>(this T obj, params T[] values)
        {
            return values.Any(x => x?.Equals(obj) == true || (obj == null && x == null));
        }

        /// <summary>
        ///     Проверка содержит ли массив текущее значение
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <param name="comparer"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        public static bool In<T>(this T obj, IComparer<T> comparer, params T[] values)
        {
            return values.Any(x => comparer == null ? x?.Equals(obj) == true || (obj == null && x == null) : comparer.Compare(obj, x) == 0);
        }

        /// <summary>
        ///     Проверка содержит ли массив текущее значение
        /// </summary>
        /// <param name="s"></param>
        /// <param name="comparer"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        public static bool In(this string s, StringComparison comparer, params string[] values)
        {
            return values.Any(x => x?.Equals(s, comparer) == true || (s == null && x == null));
        }

        /// <summary>
        /// Проверка значения на null <see cref="TypeHelper.NullValues"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool IsNotNull<T>(this T value)
        {
            return !IsNull(value);
        }

        /// <summary>
        /// Проверка значения на null <see cref="TypeHelper.NullValues"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool IsNull<T>(this T value)
        {
            return TypeExtensions.NullValues.Contains(value);
        }

        /// <summary>
        /// Является ли значение числом
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <param name="number"></param>
        /// <returns></returns>
        public static bool IsNumber<T>(this T value, out object number)
        {
            number = null;
            if (typeof(T).IsNumeric())
            {
                number = value;
                return true;
            }

            if (value is string s)
            {
                decimal d;
                var result = decimal.TryParse(s, out d) || decimal.TryParse(s.Replace(",", "."), out d);
                number = d;
                return result;
            }

            try
            {
                number = Obj.ChangeType<decimal>(value);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        ///     Проверка на obj1 меньше obj2
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj1"></param>
        /// <param name="obj2"></param>
        public static bool Less<T>(this T obj1, T obj2) where T : IComparable
        {
            return Comparer<T>.Default.Compare(obj1, obj2) < 0;
        }

        /// <summary>
        ///     Проверка на obj1 меньше или равен obj2
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj1"></param>
        /// <param name="obj2"></param>
        public static bool LessOrEqual<T>(this T obj1, T obj2) where T : IComparable
        {
            return Comparer<T>.Default.Compare(obj1, obj2) <= 0;
        }

        /// <summary>
        ///     Проверка на leftValue <= value >= rightValue или rightValue <= value >=leftValue
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        public static bool NotBetween<T>(this T obj, T min, T max) where T : struct
        {
            return Comparer<T>.Default.Compare(obj, min) <= 0 || Comparer<T>.Default.Compare(obj, max) >= 0;
        }

        /// <summary>
        /// Проверка не содержит ли массив текущее значение
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        public static bool NotIn<T>(this T obj, params T[] values) where T : IComparable
        {
            return !In(obj, values);
        }

        /// <summary>
        /// Выполняет действие на объекте, если одно из условий выполнено.
        /// </summary>
        /// <typeparam name="TWith">Тип объекта, с которым будет работать метод.</typeparam>
        /// <param name="obj">Объект, к которому будут применяться действия.</param>
        /// <param name="options">Массив кортежей, содержащих условие и действие.</param>
        /// <returns>Объект после выполнения действия.</returns>
        /// <example>
        /// <code>
        /// var result = myObject.WithWhen(
        ///     ( () => condition1, obj => { /* действие 1 */ } ),
        ///     ( () => condition2, obj => { /* действие 2 */ } )
        /// );
        /// </code>
        /// </example>
        public static TWith WithWhen<TWith>(this TWith obj, params (Func<bool> when, Action action)[] options)
        {
            foreach (var opt in options)
            {
                if (!opt.when())
                    continue;
                opt.action();
                return obj;
            }

            return obj;
        }

        /// <summary>
        /// Выполняет действие на объекте, если одно из условий выполнено.
        /// </summary>
        /// <typeparam name="TWith">Тип объекта, с которым будет работать метод.</typeparam>
        /// <param name="obj">Объект, к которому будут применяться действия.</param>
        /// <param name="options">Массив кортежей, содержащих условие и действие.</param>
        /// <returns>Объект после выполнения действия.</returns>
        /// <example>
        /// <code>
        /// var result = myObject.WithWhen(
        ///     ( () => condition1, obj => { /* действие 1 */ } ),
        ///     ( () => condition2, obj => { /* действие 2 */ } )
        /// );
        /// </code>
        /// </example>
        public static TWith WithWhen<TWith>(this TWith obj, params (Func<bool> when, Action<TWith> action)[] options)
        {
            return WithWhen(obj, null, options);
        }

        /// <summary>
        /// Выполняет действие на объекте, если одно из условий выполнено,
        /// или выполняет действие по умолчанию, если ни одно из условий не выполнено.
        /// </summary>
        /// <typeparam name="TWith">Тип объекта, с которым будет работать метод.</typeparam>
        /// <param name="obj">Объект, к которому будут применяться действия.</param>
        /// <param name="defaultAction">Действие по умолчанию, которое будет выполнено, если ни одно из условий не выполнено.</param>
        /// <param name="options">Массив кортежей, содержащих условие и действие.</param>
        /// <returns>Объект после выполнения действия.</returns>
        /// <example>
        /// <code>
        /// var result = myObject.WithWhen(
        ///     obj => { /* действие по умолчанию */ },
        ///     ( () => condition1, obj => { /* действие 1 */ } ),
        ///     ( () => condition2, obj => { /* действие 2 */ } )
        /// );
        /// </code>
        /// </example>
        public static TWith WithWhen<TWith>(this TWith obj, Action<TWith> defaultAction, params (Func<bool> when, Action<TWith> action)[] options)
        {
            foreach (var opt in options)
            {
                if (!opt.when())
                    continue;
                opt.action(obj);
                return obj;
            }

            defaultAction?.Invoke(obj);
            return obj;
        }

        /// <summary>
        /// Выполняет действие на объекте, если условие выполнено.
        /// </summary>
        /// <typeparam name="TWith">Тип объекта, с которым будет работать метод.</typeparam>
        /// <param name="obj">Объект, к которому будет применяться действие.</param>
        /// <param name="when">Условие, при выполнении которого будет выполнено действие.</param>
        /// <param name="action">Действие, которое будет выполнено, если условие выполнено.</param>
        /// <returns>Объект после выполнения действия.</returns>
        /// <example>
        /// <code>
        /// var result = myObject.WithWhen(
        ///     () => condition,
        ///     obj => { /* действие */ }
        /// );
        /// </code>
        /// </example>
        public static TWith WithWhen<TWith>(this TWith obj, Func<bool> when, Action<TWith> action)
        {
            return WithWhen(obj, (when, action));
        }

        /// <summary>
        /// Возвращает значение на основе указанного случая или значение по умолчанию, если ни один случай не совпадает.
        /// </summary>
        /// <typeparam name="TThen">Тип возвращаемого значения.</typeparam>
        /// <typeparam name="TWhen">Тип объекта сравнения.</typeparam>
        /// <param name="obj">Объект сравнения.</param>
        /// <param name="defaultValue">Значение по умолчанию.</param>
        /// <param name="cases">Массив пар (когда, значение).</param>
        /// <returns>Возвращает значение, соответствующее случаю, или значение по умолчанию, если совпадений нет.</returns>
        public static TThen Case<TWhen, TThen>(this TWhen obj, TThen defaultValue, params (TWhen when, TThen then)[] cases)
        {
            foreach (var c in cases)
                if (obj?.Equals(c.when) == true)
                    return c.then;

            return defaultValue;
        }

        /// <summary>
        /// Возвращает значение на основе указанного случая.
        /// </summary>
        /// <typeparam name="TThen">Тип возвращаемого значения.</typeparam>
        /// <typeparam name="TWhen">Тип объекта сравнения.</typeparam>
        /// <param name="obj">Объект сравнения.</param>
        /// <param name="when">Значение для сравнения.</param>
        /// <param name="then">Значение, возвращаемое при совпадении.</param>
        /// <param name="andSoOnWhenThenParams">Дополнительные параметры (когда, значение).</param>
        /// <returns>Возвращает значение, соответствующее случаю.</returns>
        public static TThen Case<TWhen, TThen>(this TWhen obj, TWhen when, TThen then, params object[] andSoOnWhenThenParams)
        {
            return Case(obj, default, when, then, andSoOnWhenThenParams);
        }

        /// <summary>
        /// Возвращает значение на основе указанного случая или значение по умолчанию, если ни один случай не совпадает.
        /// </summary>
        /// <typeparam name="TThen">Тип возвращаемого значения.</typeparam>
        /// <typeparam name="TWhen">Тип объекта сравнения.</typeparam>
        /// <param name="obj">Объект сравнения.</param>
        /// <param name="defaultValue">Значение по умолчанию.</param>
        /// <param name="when">Значение для сравнения.</param>
        /// <param name="then">Значение, возвращаемое при совпадении.</param>
        /// <param name="andSoOnWhenThenParams">Дополнительные параметры (когда, значение).</param>
        /// <returns>Возвращает значение, соответствующее случаю, или значение по умолчанию, если совпадений нет.</returns>
        public static TThen Case<TWhen, TThen>(this TWhen obj, TThen defaultValue, TWhen when, TThen then, params object[] andSoOnWhenThenParams)
        {
            var cases = new List<(TWhen, TThen)> { (when, then) };
            for (var i = 0; i < andSoOnWhenThenParams.Length / 2; i += 2)
            {
                cases.Add(((TWhen)andSoOnWhenThenParams[i], (TThen)andSoOnWhenThenParams[i + 1]));
            }

            return Case(obj, defaultValue, cases.ToArray());
        }

        /// <summary>
        /// Возвращает значение на основе указанного случая или значение по умолчанию, если ни один случай не совпадает.
        /// </summary>
        /// <typeparam name="TThen">Тип возвращаемого значения.</typeparam>
        /// <typeparam name="TWhen">Тип объекта сравнения.</typeparam>
        /// <param name="obj">Объект сравнения.</param>
        /// <param name="cases">Массив пар (когда, значение).</param>
        /// <returns>Возвращает значение, соответствующее случаю, или значение по умолчанию, если совпадений нет.</returns>
        public static TThen Case<TWhen, TThen>(this TWhen obj, params (TWhen when, TThen then)[] cases)
        {
            return Case(obj, default(TThen), cases);
        }

        /// <summary>
        /// Возвращает значение на основе указанного случая с функцией условия или значение по умолчанию, если ни один случай не совпадает.
        /// </summary>
        /// <typeparam name="TThen">Тип возвращаемого значения.</typeparam>
        /// <typeparam name="TWhen">Тип объекта сравнения.</typeparam>
        /// <param name="obj">Объект сравнения.</param>
        /// <param name="defaultValue">Значение по умолчанию.</param>
        /// <param name="cases">Массив пар (функция условия, значение).</param>
        /// <returns>Возвращает значение, соответствующее случаю, или значение по умолчанию, если совпадений нет.</returns>
        public static TThen Case<TWhen, TThen>(this TWhen obj, TThen defaultValue, params (Func<TWhen, bool> when, TThen value)[] cases)
        {
            foreach (var c in cases)
                if (c.when(obj))
                    return c.value;

            return defaultValue;
        }

        /// <summary>
        /// Возвращает значение на основе указанного случая с функцией условия.
        /// </summary>
        /// <typeparam name="TThen">Тип возвращаемого значения.</typeparam>
        /// <typeparam name="TWhen">Тип объекта сравнения.</typeparam>
        /// <param name="obj">Объект сравнения.</param>
        /// <param name="cases">Массив пар (функция условия, значение).</param>
        /// <returns>Возвращает значение, соответствующее случаю.</returns>
        public static TThen Case<TWhen, TThen>(this TWhen obj, params (Func<TWhen, bool> when, TThen value)[] cases)
        {
            return Case(obj, default(TThen), cases);
        }
    }
}
