//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Data;
//using System.Linq;
//using System.Linq.Expressions;
//using System.Text;

//namespace RuntimeStuff.Extensions
//{
//    public static class RSLinqExtensions
//    {
//#if NETFRAMEWORK

//        /// <summary>
//        /// Возвращает значение по ключу из словаря или значение по умолчанию, если ключ отсутствует.
//        /// </summary>
//        /// <typeparam name="TKey">Тип ключа словаря.</typeparam>
//        /// <typeparam name="TValue">Тип значения словаря.</typeparam>
//        /// <param name="dic">Словарь для поиска значения.</param>
//        /// <param name="key">Ключ для поиска.</param>
//        /// <returns>Значение по ключу или значение по умолчанию для типа TValue.</returns>
//        public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dic, TKey key)
//        {
//            return dic.TryGetValue(key, out var val) ? val : default;
//        }

//        /// <summary>
//        /// Удаляет элемент по ключу из словаря и возвращает его значение.
//        /// </summary>
//        /// <typeparam name="TKey">Тип ключа словаря.</typeparam>
//        /// <typeparam name="TValue">Тип значения словаря.</typeparam>
//        /// <param name="dic">Словарь, из которого удаляется элемент.</param>
//        /// <param name="key">Ключ удаляемого элемента.</param>
//        /// <param name="val">Значение удалённого элемента, если он найден.</param>
//        /// <returns>True, если элемент был найден и удалён; иначе — false.</returns>
//        public static bool Remove<TKey, TValue>(this IDictionary<TKey, TValue> dic, TKey key, out TValue val)
//        {
//            var result = dic.TryGetValue(key, out val);
//            if (result)
//                dic.Remove(key);
//            return result;
//        }

//#endif

//        /// <summary>
//        /// Преобразует коллекцию в массив указанного типа, с возможностью конвертации типа элементов.
//        /// </summary>
//        /// <typeparam name="T">Тип элементов массива.</typeparam>
//        /// <param name="list">Исходная коллекция.</param>
//        /// <param name="convertType">Если true — элементы будут приведены к типу T.</param>
//        /// <returns>Массив элементов типа T.</returns>
//        public static T[] ToArray<T>(this IEnumerable list, bool convertType)
//        {
//            if (list == null)
//                return Array.Empty<T>();

//            // Fast path: if not converting and list is already T[]
//            if (!convertType)
//            {
//                if (list is T[] arr)
//                    return arr;
//                if (list is ICollection col)
//                {
//                    var result = new T[col.Count];
//                    int i = 0;
//                    foreach (var item in list)
//                        result[i++] = (T)item;
//                    return result;
//                }
//                // Fallback: enumerate
//                var temp = new List<T>();
//                foreach (var item in list)
//                    temp.Add((T)item);
//                return temp.ToArray();
//            }
//            else
//            {
//                // If ICollection, preallocate array
//                if (list is ICollection col)
//                {
//                    var result = new T[col.Count];
//                    int i = 0;
//                    foreach (var item in list)
//                        result[i++] = Obj.ChangeType<T>(item);
//                    return result;
//                }
//                // Fallback: enumerate and convert
//                var temp = new List<T>();
//                foreach (var item in list)
//                    temp.Add(Obj.ChangeType<T>(item));
//                return temp.ToArray();
//            }
//        }

//        /// <summary>
//        /// Проверяет, содержит ли коллекция хотя бы один элемент.
//        /// </summary>
//        /// <param name="list">Коллекция для проверки.</param>
//        /// <returns>True, если коллекция не пуста; иначе — false.</returns>
//        public static bool Any(this IEnumerable list)
//        {
//            if (list == null)
//                return false;
//            foreach (var _ in list)
//                return true;
//            return false;
//        }

//        /// <summary>
//        /// Преобразует элементы коллекции в коллекцию объектов.
//        /// </summary>
//        /// <param name="list">Исходная коллекция.</param>
//        /// <returns>Коллекция объектов.</returns>
//        public static IEnumerable<object> Select(this IEnumerable list)
//        {
//            foreach (var item in list)
//                yield return item;
//        }

//        /// <summary>
//        /// Преобразует массив байтов в строку с использованием указанной кодировки.
//        /// </summary>
//        /// <param name="bytes">Массив байтов.</param>
//        /// <param name="encoding">Кодировка (по умолчанию UTF8).</param>
//        /// <returns>Строка, полученная из массива байтов.</returns>
//        public static string GetString(this byte[] bytes, Encoding encoding = null)
//        {
//            var s = encoding != null ? encoding.GetString(bytes) : Encoding.UTF8.GetString(bytes);
//            return s.Replace("\ufeff", "").Replace("\u200B", "").Replace("п»ї", "");
//        }

//        /// <summary>
//        /// Добавляет элементы из другой коллекции пар ключ-значение в словарь.
//        /// </summary>
//        /// <typeparam name="TKey">Тип ключа.</typeparam>
//        /// <typeparam name="TValue">Тип значения.</typeparam>
//        /// <param name="d">Исходный словарь.</param>
//        /// <param name="values">Коллекция пар ключ-значение для добавления.</param>
//        /// <returns>Словарь с добавленными элементами.</returns>
//        public static IDictionary<TKey, TValue> AddRange<TKey, TValue>(this IDictionary<TKey, TValue> d, IEnumerable<KeyValuePair<TKey, TValue>> values)
//        {
//            foreach (var kv in values)
//            {
//                d[kv.Key] = kv.Item;
//            }
//            return d;
//        }

//        /// <summary>
//        /// Определяет, удовлетворяют ли все элементы коллекции условию.
//        /// </summary>
//        /// <typeparam name="TSource">Тип элементов коллекции.</typeparam>
//        /// <param name="source">Исходная коллекция.</param>
//        /// <param name="predicate">Функция условия, принимающая элемент и его индекс.</param>
//        /// <returns>True, если все элементы удовлетворяют условию; иначе — false.</returns>
//        public static bool All<TSource>(this IEnumerable<TSource> source, Func<TSource, int, bool> predicate)
//        {
//            if (source == null)
//            {
//                throw new NullReferenceException("source");
//            }

//            if (predicate == null)
//            {
//                throw new NullReferenceException("predicate");
//            }

//            var i = 0;
//            foreach (var item in source)
//            {
//                if (!predicate(item, i))
//                {
//                    return false;
//                }
//                i++;
//            }

//            return true;
//        }

//        /// <summary>
//        /// Преобразует элементы коллекции к указанному типу.
//        /// </summary>
//        /// <param name="list">Исходная коллекция.</param>
//        /// <param name="elementType">Тип, к которому нужно привести элементы.</param>
//        /// <returns>Коллекция элементов приведённого типа.</returns>
//        public static IEnumerable<object> Cast(this IEnumerable list, Type elementType)
//        {
//            foreach (var item in list)
//                yield return Obj.ChangeType(item, elementType);
//        }

//        /// <summary>
//        /// Возвращает количество элементов в коллекции.
//        /// </summary>
//        /// <param name="enumerable">Коллекция для подсчёта.</param>
//        /// <returns>Количество элементов.</returns>
//        public static int CountItems(this IEnumerable enumerable)
//        {
//            if (enumerable == null)
//                return 0;

//            switch (enumerable)
//            {
//                case Array array:
//                    return array.Length;

//                case IList l:
//                    return l.Count;

//                case ICollection c:
//                    return c.Count;
//            }

//            var count = 0;
//            foreach (var _ in enumerable)
//                count++;
//            return count;
//        }

//        /// <summary>
//        /// Возвращает уникальные элементы коллекции по выбранному свойству.
//        /// </summary>
//        /// <typeparam name="T">Тип элементов коллекции.</typeparam>
//        /// <param name="list">Исходная коллекция.</param>
//        /// <param name="propertySelector">Функция выбора свойства для сравнения.</param>
//        /// <returns>Коллекция уникальных элементов.</returns>
//        public static IEnumerable<T> DistinctBy<T>(this IEnumerable<T> list, Func<T, object> propertySelector)
//        {
//            return list.GroupBy(propertySelector).Select(x => x.FirstOrDefault());
//        }

//        /// <summary>
//        /// Возвращает элемент коллекции по индексу.
//        /// </summary>
//        /// <param name="enumerable">Исходная коллекция.</param>
//        /// <param name="index">Индекс элемента.</param>
//        /// <returns>Элемент по указанному индексу.</returns>
//        public static object GetElementAt(this IEnumerable enumerable, int index)
//        {
//            return enumerable.Cast<object>().ElementAt(index);
//        }

//#if (NETSTANDARD || NETFRAMEWORK) && !(NET5_0_OR_GREATER || NET)

//        /// <summary>
//        /// Возвращает значение по ключу из словаря или значение по умолчанию, если ключ отсутствует.
//        /// </summary>
//        /// <typeparam name="T">Тип значения.</typeparam>
//        /// <param name="d">Словарь.</param>
//        /// <param name="key">Ключ.</param>
//        /// <param name="defaultValue">Значение по умолчанию, если ключ не найден.</param>
//        /// <returns>Значение по ключу или значение по умолчанию.</returns>
//        public static T GetValueOrDefault<T>(this IDictionary d, object key, T defaultValue = default)
//        {
//            if (d.Contains(key))
//                return Obj.Cast<T>(d[key]);
//            return defaultValue;
//        }

//        /// <summary>
//        /// Возвращает значение по ключу из словаря или значение по умолчанию, если ключ отсутствует.
//        /// </summary>
//        /// <typeparam name="TKey">Тип ключа.</typeparam>
//        /// <typeparam name="TValue">Тип значения.</typeparam>
//        /// <param name="d">Словарь.</param>
//        /// <param name="key">Ключ.</param>
//        /// <param name="defaultValue">Значение по умолчанию, если ключ не найден.</param>
//        /// <returns>Значение по ключу или значение по умолчанию.</returns>
//        public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> d, TKey key, TValue defaultValue = default)
//        {
//            if (d.TryGetValue(key, out var v))
//                return v;
//            return defaultValue;
//        }

//#endif

//        /// <summary>
//        /// Возвращает индекс первого элемента, удовлетворяющего условию.
//        /// </summary>
//        /// <typeparam name="T">Тип элементов коллекции.</typeparam>
//        /// <param name="e">Исходная коллекция.</param>
//        /// <param name="match">Условие поиска.</param>
//        /// <param name="reverseSearch">Если true — поиск с конца.</param>
//        /// <returns>Индекс найденного элемента или -1, если не найден.</returns>
//        public static int IndexOf<T>(this IEnumerable<T> e, Predicate<T> match, bool reverseSearch = false)
//        {
//            return e.IndexOf((x, _) => match(x), reverseSearch);
//        }

//        /// <summary>
//        /// Получает расширенную информацию о типе элемента коллекции.
//        /// </summary>
//        /// <typeparam name="T">Тип элементов коллекции.</typeparam>
//        /// <param name="list">Исходная коллекция.</param>
//        /// <returns>Экземпляр MemberInfoEx для типа элемента или null.</returns>
//        public static MemberInfoEx GetElementType<T>(this IEnumerable<T> list)
//        {
//            var listItemType = typeof(T);
//            if (listItemType != typeof(object))
//                return listItemType.GetMemberInfoEx();

//            if (list == null)
//                return null;

//            listItemType = list.GetType().GetMemberInfoEx().ElementType;
//            if (listItemType != typeof(object))
//                return listItemType.GetMemberInfoEx();

//            var first = list.FirstOrDefault();
//            var type = first?.GetType() ?? typeof(T);
//            if (type != typeof(object))
//                return type.GetMemberInfoEx();

//            return null;
//        }

//        /// <summary>
//        /// Возвращает индекс первого элемента, удовлетворяющего условию.
//        /// </summary>
//        /// <typeparam name="T">Тип элементов коллекции.</typeparam>
//        /// <param name="e">Исходная коллекция.</param>
//        /// <param name="match">Функция условия, принимающая элемент и его индекс.</param>
//        /// <param name="reverseSearch">Если true — поиск с конца.</param>
//        /// <returns>Индекс найденного элемента или -1, если не найден.</returns>
//        public static int IndexOf<T>(this IEnumerable<T> e, Func<T, int, bool> match, bool reverseSearch = false)
//        {
//            if (e == null)
//                return -1;

//            // Если исходная коллекция - массив или IList<T>, используем индексацию
//            if (e is IList<T> list)
//            {
//                if (!reverseSearch)
//                {
//                    for (var i = 0; i < list.Count; i++)
//                    {
//                        if (match(list[i], i))
//                            return i;
//                    }
//                }
//                else
//                {
//                    for (var i = list.Count - 1; i >= 0; i--)
//                    {
//                        if (match(list[i], i))
//                            return i;
//                    }
//                }
//                return -1;
//            }

//            // Для остальных IEnumerable<T>
//            if (!reverseSearch)
//            {
//                var i = 0;
//                foreach (var item in e)
//                {
//                    if (match(item, i))
//                        return i;
//                    i++;
//                }
//            }
//            else
//            {
//                // К сожалению, для IEnumerable<T> без индексации придётся материализовать в список
//                var arr = e as T[] ?? e.ToArray();
//                for (var i = arr.Length - 1; i >= 0; i--)
//                {
//                    if (match(arr[i], i))
//                    {
//                        return i;
//                    }
//                }
//            }
//            return -1;
//        }

//        /// <summary>
//        /// Возвращает индекс первого вхождения элемента в коллекции.
//        /// </summary>
//        /// <param name="source">Исходная коллекция.</param>
//        /// <param name="item">Искомый элемент.</param>
//        /// <returns>Индекс найденного элемента или -1, если не найден.</returns>
//        public static int IndexOf(this IEnumerable source, object item)
//        {
//            if (source == null)
//                throw new ArgumentNullException(nameof(source));

//            int index = 0;
//            foreach (var element in source)
//            {
//                if (Equals(element, item))
//                    return index;
//                index++;
//            }

//            return -1;
//        }

//        /// <summary>
//        /// Добавляет в коллекцию элементы из другой коллекции, которых ещё нет в первой.
//        /// </summary>
//        /// <typeparam name="T">Тип элементов коллекции.</typeparam>
//        /// <param name="list1">Исходная коллекция.</param>
//        /// <param name="list2">Коллекция для добавления.</param>
//        public static void UnionWith<T>(this ICollection<T> list1, IEnumerable<T> list2)
//        {
//            if (list1 == null)
//                throw new ArgumentNullException(nameof(list1));
//            if (list2 == null)
//                throw new ArgumentNullException(nameof(list2));

//            var set = new HashSet<T>(list1); // для быстрого поиска

//            foreach (var item in list2)
//            {
//                if (set.Add(item)) // добавится в set только если уникален
//                    list1.Add(item);
//            }
//        }

//        /// <summary>
//        /// Перемещает элемент в списке с одной позиции на другую.
//        /// </summary>
//        /// <typeparam name="T">Тип элементов списка.</typeparam>
//        /// <param name="list">Список.</param>
//        /// <param name="fromIndex">Исходный индекс.</param>
//        /// <param name="toIndex">Новый индекс.</param>
//        public static void Move<T>(this IList<T> list, int fromIndex, int toIndex)
//        {
//            var item = list[fromIndex];
//            list.RemoveAt(fromIndex);
//            list.Insert(toIndex, item);
//        }

//        /// <summary>
//        /// Возвращает элементы коллекции, удовлетворяющие условию.
//        /// </summary>
//        /// <typeparam name="T">Тип элементов коллекции.</typeparam>
//        /// <param name="source">Исходная коллекция.</param>
//        /// <param name="predicate">Условие фильтрации.</param>
//        /// <returns>Коллекция элементов, удовлетворяющих условию.</returns>
//        public static IEnumerable<T> OfType<T>(this IEnumerable<T> source, Func<T, bool> predicate)
//        {
//            foreach (object item in source)
//            {
//                if (item is T it && (predicate == null || predicate(it)))
//                {
//                    yield return it;
//                }
//            }
//        }

//        /// <summary>
//        /// Удаляет из словаря элементы, удовлетворяющие условию.
//        /// </summary>
//        /// <typeparam name="TKey">Тип ключа.</typeparam>
//        /// <typeparam name="TValue">Тип значения.</typeparam>
//        /// <param name="d">Исходный словарь.</param>
//        /// <param name="predicate">Условие для удаления.</param>
//        /// <returns>Словарь без удалённых элементов.</returns>
//        public static IDictionary<TKey, TValue> RemoveRange<TKey, TValue>(this IDictionary<TKey, TValue> d, Func<TValue, bool> predicate)
//        {
//            var keysToRemove = d.Where(kv => predicate(kv.Item)).Select(kv => kv.Key).ToList();
//            foreach (var key in keysToRemove)
//                d.Remove(key);
//            return d;
//        }

//        /// <summary>
//        /// Преобразует коллекцию в массив указанного типа.
//        /// </summary>
//        /// <typeparam name="T">Тип элементов массива.</typeparam>
//        /// <param name="list">Исходная коллекция.</param>
//        /// <returns>Массив элементов типа T.</returns>
//        public static T[] ToArray<T>(this IEnumerable list)
//        {
//            if (list is T[] l)
//                return l;
//            var result = new List<T>();
//            foreach (var item in list)
//                result.Add(Obj.Cast<T>(item, false));
//            return result.ToArray();
//        }

//        /// <summary>
//        /// Создаёт новый элемент для списка, используя указанные аргументы конструктора.
//        /// </summary>
//        /// <param name="list">Список, для которого создаётся элемент.</param>
//        /// <param name="newItemCtorArgs">Аргументы конструктора нового элемента.</param>
//        /// <returns>Созданный элемент.</returns>
//        public static object CreateItem(this IList list, params object[] newItemCtorArgs)
//        {
//            if (list == null)
//                return null;
//            var listType = list.GetType().GetMemberInfoEx();
//            var listItem = Obj.New(listType.ElementType, newItemCtorArgs);
//            return listItem;
//        }

//        /// <summary>
//        /// Создаёт новый элемент и добавляет его в список.
//        /// </summary>
//        /// <param name="list">Список, в который добавляется элемент.</param>
//        /// <param name="newItemCtorArgs">Аргументы конструктора нового элемента.</param>
//        /// <returns>Созданный и добавленный элемент.</returns>
//        public static object CreateAndAddItem(this IList list, params object[] newItemCtorArgs)
//        {
//            if (list == null)
//                return null;
//            var listItem = CreateItem(list, newItemCtorArgs);
//            list.Add(listItem);
//            return listItem;
//        }

//        /// <summary>
//        /// Преобразует коллекцию в CSV-строку.
//        /// </summary>
//        /// <typeparam name="T">Тип элементов коллекции.</typeparam>
//        /// <param name="list">Исходная коллекция.</param>
//        /// <param name="keySelector">Функция выбора ключа (опционально).</param>
//        /// <param name="valueSeparator">Разделитель значений.</param>
//        /// <param name="lineTerminator">Разделитель строк.</param>
//        /// <param name="formatProvider">Провайдер форматирования (опционально).</param>
//        /// <returns>CSV-строка.</returns>
//        public static string ToCsv<T>(this IEnumerable<T> list, Expression<Func<T, object>> keySelector = null, string valueSeparator = ",", string lineTerminator = "\r\n", IFormatProvider formatProvider = null)
//        {
//            if (list == null)
//                return "";
//            var listTypeInfo = list.GetType().GetMemberInfoEx();

//            if (keySelector == null && listTypeInfo.IsBasicCollection)
//                return string.Join(valueSeparator, list.Select(x => string.Format(formatProvider, "{0}", x)));

//            var sb = new StringBuilder();

//            foreach (var item in list)
//            {
//                sb.Append(keySelector == null ? item + lineTerminator : string.Join(valueSeparator, Obj.GetValues(item)) + lineTerminator);
//            }
//            return sb.ToString();
//        }

//        /// <summary>
//        /// Преобразует коллекцию в DataTable.
//        /// </summary>
//        /// <typeparam name="T">Тип элементов коллекции.</typeparam>
//        /// <param name="list">Исходная коллекция.</param>
//        /// <returns>DataTable с импортированными данными.</returns>
//        public static DataTable ToDataTable<T>(this IEnumerable<T> list) where T : class
//        {
//            var dt = new DataTable();
//            dt.ImportData(list);
//            return dt;
//        }

//        /// <summary>
//        /// Преобразует коллекцию в список указанного типа.
//        /// </summary>
//        /// <typeparam name="T">Тип элементов списка.</typeparam>
//        /// <param name="list">Исходная коллекция.</param>
//        /// <returns>Список элементов типа T.</returns>
//        public static List<T> ToList<T>(this IEnumerable list)
//        {
//            if (list is List<T> l)
//                return l;
//            var result = new List<T>();
//            foreach (var item in list)
//                result.Add(Obj.Cast<T>(item, false));
//            return result;
//        }

//        /// <summary>
//        /// Пытается привести элементы коллекции к указанному типу.
//        /// </summary>
//        /// <param name="list">Исходная коллекция.</param>
//        /// <param name="elementType">Тип, к которому нужно привести элементы.</param>
//        /// <returns>Коллекция успешно приведённых элементов.</returns>
//        public static IEnumerable<object> TryCast(this IEnumerable list, Type elementType)
//        {
//            foreach (var item in list)
//            {
//                var result = Obj.TryChangeType(item, elementType, out var i);
//                if (!result)
//                    continue;
//                yield return i;
//            }
//        }

//#if NETSTANDARD || NETFRAMEWORK
//#endif

//#if NETFRAMEWORK
//        /// <summary>
//        /// Возвращает значение по ключу или значение по умолчанию, если ключ отсутствует в словаре.
//        /// </summary>
//        /// <typeparam name="TKey">Тип ключа.</typeparam>
//        /// <typeparam name="TValue">Тип значения.</typeparam>
//        /// <param name="dic">Словарь.</param>
//        /// <param name="key">Ключ.</param>
//        /// <returns>Значение по ключу или значение по умолчанию.</returns>
//        public static TValue TryGetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dic, TKey key)
//        {
//            if (dic.TryGetValue(key, out var value))
//                return value;
//            else
//#pragma warning disable CS8603 // Possible null reference return.
//                return default;
//#pragma warning restore CS8603 // Possible null reference return.
//        }
//#endif

//        /// <summary>
//        /// Проверяет, находится ли значение между двумя границами (включительно).
//        /// </summary>
//        /// <typeparam name="T">Тип значения (структура).</typeparam>
//        /// <param name="value">Проверяемое значение.</param>
//        /// <param name="leftValue">Левая граница.</param>
//        /// <param name="rightValue">Правая граница.</param>
//        /// <returns>True, если значение находится между границами; иначе — false.</returns>
//        public static bool Between<T>(this T value, T leftValue, T rightValue) where T : struct
//        {
//            return
//                (Comparer<T>.Default.Compare(value, leftValue) >= 0 && Comparer<T>.Default.Compare(value, rightValue) <= 0) ||
//                (Comparer<T>.Default.Compare(value, leftValue) <= 0 && Comparer<T>.Default.Compare(value, rightValue) >= 0);
//        }

//        /// <summary>
//        /// Возвращает первый не null элемент из переданных значений.
//        /// </summary>
//        /// <typeparam name="T">Тип элементов.</typeparam>
//        /// <param name="obj">Первое значение.</param>
//        /// <param name="objects">Остальные значения.</param>
//        /// <returns>Первый не null элемент или значение по умолчанию.</returns>
//        public static T Coalesce<T>(this T obj, params T[] objects)
//        {
//            var objArr = new T[] { obj };
//            objects = objArr.Concat(objects).ToArray();
//            return objects.FirstOrDefault(x => !RSTypeExtensions.NullValues.Contains(x));
//        }

//        /// <summary>
//        /// Возвращает первый не null элемент из переданных значений.
//        /// </summary>
//        /// <param name="obj">Первое значение.</param>
//        /// <param name="objects">Остальные значения.</param>
//        /// <returns>Первый не null элемент или значение по умолчанию.</returns>
//        public static object Coalesce(this object obj, params object[] objects)
//        {
//            return Coalesce<object>(obj, objects);
//        }

//        /// <summary>
//        /// Возвращает первый не null элемент из переданных значений или выбрасывает исключение, если все элементы null.
//        /// </summary>
//        /// <param name="obj">Первое значение.</param>
//        /// <param name="objects">Остальные значения.</param>
//        /// <returns>Первый не null элемент.</returns>
//        /// <exception cref="NullReferenceException">Если все элементы null.</exception>
//        public static object CoalesceThrow(this object obj, params object[] objects)
//        {
//            return Coalesce<object>(obj, objects) ?? throw new NullReferenceException("В массиве нет не Null элемента!");
//        }

//        /// <summary>
//        /// Выполняет действие для каждого элемента коллекции.
//        /// </summary>
//        /// <typeparam name="T">Тип элементов коллекции.</typeparam>
//        /// <param name="collection">Исходная коллекция.</param>
//        /// <param name="action">Действие для выполнения.</param>
//        /// <returns>Исходная коллекция.</returns>
//        public static IEnumerable<T> ForEach<T>(this IEnumerable<T> collection, Action<T> action)
//        {
//            foreach (var item in collection)
//                action(item);
//            return collection;
//        }

//        /// <summary>
//        /// Выполняет действие для каждого элемента коллекции, передавая индекс.
//        /// </summary>
//        /// <typeparam name="T">Тип элементов коллекции.</typeparam>
//        /// <param name="collection">Исходная коллекция.</param>
//        /// <param name="action">Действие для выполнения, принимающее элемент и его индекс.</param>
//        public static void ForEach<T>(this IEnumerable<T> collection, Action<T, int> action)
//        {
//            var idx = 0;
//            foreach (var item in collection)
//            {
//                action(item, idx);
//                idx++;
//            }
//        }

//        /// <summary>
//        /// Проверяет, больше ли первый объект второго.
//        /// </summary>
//        /// <typeparam name="T">Тип сравниваемых объектов.</typeparam>
//        /// <param name="obj1">Первый объект.</param>
//        /// <param name="obj2">Второй объект.</param>
//        /// <returns>True, если obj1 больше obj2; иначе — false.</returns>
//        public static bool Greater<T>(this T obj1, T obj2) where T : IComparable
//        {
//            return Comparer<T>.Default.Compare(obj1, obj2) > 0;
//        }

//        /// <summary>
//        /// Проверяет, больше или равен ли первый объект второму.
//        /// </summary>
//        /// <typeparam name="T">Тип сравниваемых объектов.</typeparam>
//        /// <param name="obj1">Первый объект.</param>
//        /// <param name="obj2">Второй объект.</param>
//        /// <returns>True, если obj1 больше или равен obj2; иначе — false.</returns>
//        public static bool GreaterOrEqual<T>(this T obj1, T obj2) where T : IComparable
//        {
//            return Comparer<T>.Default.Compare(obj1, obj2) >= 0;
//        }

//        /// <summary>
//        /// Возвращает одно из двух значений в зависимости от условия.
//        /// </summary>
//        /// <typeparam name="T1">Тип значения для проверки.</typeparam>
//        /// <typeparam name="T2">Тип возвращаемого значения.</typeparam>
//        /// <param name="value">Значение для проверки.</param>
//        /// <param name="condition">Условие.</param>
//        /// <param name="thenValue">Значение, если условие истинно.</param>
//        /// <param name="elseValue">Значение, если условие ложно.</param>
//        /// <returns>thenValue или elseValue.</returns>
//        public static T2 If<T1, T2>(this T1 value, Func<T1, bool> condition, T2 thenValue, T2 elseValue) => condition(value) ? thenValue : elseValue;

//        /// <summary>
//        /// Проверяет, содержится ли объект в списке значений.
//        /// </summary>
//        /// <typeparam name="T">Тип сравниваемых объектов.</typeparam>
//        /// <param name="obj">Объект для поиска.</param>
//        /// <param name="values">Список значений.</param>
//        /// <returns>True, если объект найден; иначе — false.</returns>
//        public static bool In<T>(this T obj, params T[] values)
//        {
//            //return values.Any(x => x?.Equals(obj) == true || (obj == null && x == null));
//            var comparer = EqualityComparer<T>.Default;
//            return values.Any(x => comparer.Equals(x, obj));
//        }

//        /// <summary>
//        /// Проверяет, содержится ли объект в списке значений с использованием компаратора.
//        /// </summary>
//        /// <typeparam name="T">Тип сравниваемых объектов.</typeparam>
//        /// <param name="obj">Объект для поиска.</param>
//        /// <param name="comparer">Компаратор для сравнения.</param>
//        /// <param name="values">Список значений.</param>
//        /// <returns>True, если объект найден; иначе — false.</returns>
//        public static bool In<T>(this T obj, IComparer<T> comparer, params T[] values)
//        {
//            return values.Any(x => comparer == null ? x?.Equals(obj) == true || (obj == null && x == null) : comparer.Compare(obj, x) == 0);
//        }

//        /// <summary>
//        /// Проверяет, содержится ли строка в списке строк с использованием сравнения.
//        /// </summary>
//        /// <param name="s">Строка для поиска.</param>
//        /// <param name="comparer">Тип сравнения строк.</param>
//        /// <param name="values">Список строк.</param>
//        /// <returns>True, если строка найдена; иначе — false.</returns>
//        public static bool In(this string s, StringComparison comparer, params string[] values)
//        {
//            return values.Any(x => x?.Equals(s, comparer) == true || (s == null && x == null));
//        }

//        /// <summary>
//        /// Проверяет, не является ли значение null.
//        /// </summary>
//        /// <typeparam name="T">Тип значения.</typeparam>
//        /// <param name="value">Проверяемое значение.</param>
//        /// <returns>True, если значение не null; иначе — false.</returns>
//        public static bool IsNotNull<T>(this T value)
//        {
//            return !IsNull(value);
//        }

//        /// <summary>
//        /// Проверяет, является ли значение null.
//        /// </summary>
//        /// <typeparam name="T">Тип значения.</typeparam>
//        /// <param name="value">Проверяемое значение.</param>
//        /// <returns>True, если значение null; иначе — false.</returns>
//        public static bool IsNull<T>(this T value)
//        {
//            return RSTypeExtensions.NullValues.Contains(value);
//        }

//        /// <summary>
//        /// Проверяет, является ли значение числом.
//        /// </summary>
//        /// <typeparam name="T">Тип значения.</typeparam>
//        /// <param name="value">Проверяемое значение.</param>
//        /// <param name="number">Результат преобразования в число.</param>
//        /// <returns>True, если значение является числом; иначе — false.</returns>
//        public static bool IsNumber<T>(this T value, out object number)
//        {
//            number = null;
//            if (typeof(T).IsNumeric())
//            {
//                number = value;
//                return true;
//            }

//            if (value is string s)
//            {
//                var result = decimal.TryParse(s, out var d) || decimal.TryParse(s.Replace(",", "."), out d);
//                number = d;
//                return result;
//            }

//            try
//            {
//                number = Obj.ChangeType<decimal>(value);
//                return true;
//            }
//            catch
//            {
//                return false;
//            }
//        }

//        /// <summary>
//        /// Проверяет, меньше ли первый объект второго.
//        /// </summary>
//        /// <typeparam name="T">Тип сравниваемых объектов.</typeparam>
//        /// <param name="obj1">Первый объект.</param>
//        /// <param name="obj2">Второй объект.</param>
//        /// <returns>True, если obj1 меньше obj2; иначе — false.</returns>
//        public static bool Less<T>(this T obj1, T obj2) where T : IComparable
//        {
//            return Comparer<T>.Default.Compare(obj1, obj2) < 0;
//        }

//        /// <summary>
//        /// Проверяет, меньше или равен ли первый объект второму.
//        /// </summary>
//        /// <typeparam name="T">Тип сравниваемых объектов.</typeparam>
//        /// <param name="obj1">Первый объект.</param>
//        /// <param name="obj2">Второй объект.</param>
//        /// <returns>True, если obj1 меньше или равен obj2; иначе — false.</returns>
//        public static bool LessOrEqual<T>(this T obj1, T obj2) where T : IComparable
//        {
//            return Comparer<T>.Default.Compare(obj1, obj2) <= 0;
//        }

//        /// <summary>
//        /// Проверяет, не находится ли значение между двумя границами.
//        /// </summary>
//        /// <typeparam name="T">Тип значения (структура).</typeparam>
//        /// <param name="obj">Проверяемое значение.</param>
//        /// <param name="min">Минимальная граница.</param>
//        /// <param name="max">Максимальная граница.</param>
//        /// <returns>True, если значение не между границами; иначе — false.</returns>
//        public static bool NotBetween<T>(this T obj, T min, T max) where T : struct
//        {
//            return Comparer<T>.Default.Compare(obj, min) <= 0 || Comparer<T>.Default.Compare(obj, max) >= 0;
//        }

//        /// <summary>
//        /// Проверяет, отсутствует ли объект в списке значений.
//        /// </summary>
//        /// <typeparam name="T">Тип сравниваемых объектов.</typeparam>
//        /// <param name="obj">Объект для поиска.</param>
//        /// <param name="values">Список значений.</param>
//        /// <returns>True, если объект не найден; иначе — false.</returns>
//        public static bool NotIn<T>(this T obj, params T[] values) where T : IComparable
//        {
//            return !In(obj, values);
//        }

//        /// <summary>
//        /// Выполняет действия, если выполняется хотя бы одно из условий.
//        /// </summary>
//        /// <typeparam name="TWith">Тип объекта.</typeparam>
//        /// <param name="obj">Объект.</param>
//        /// <param name="options">Массив пар (условие, действие).</param>
//        /// <returns>Объект obj.</returns>
//        public static TWith WithWhen<TWith>(this TWith obj, params (Func<bool> when, Action action)[] options)
//        {
//            foreach (var (when, action) in options)
//            {
//                if (!when())
//                    continue;
//                action();
//                return obj;
//            }

//            return obj;
//        }

//        /// <summary>
//        /// Выполняет действия, если выполняется хотя бы одно из условий, передавая объект в действие.
//        /// </summary>
//        /// <typeparam name="TWith">Тип объекта.</typeparam>
//        /// <param name="obj">Объект.</param>
//        /// <param name="options">Массив пар (условие, действие).</param>
//        /// <returns>Объект obj.</returns>
//        public static TWith WithWhen<TWith>(this TWith obj, params (Func<bool> when, Action<TWith> action)[] options)
//        {
//            return WithWhen(obj, null, options);
//        }

//        /// <summary>
//        /// Выполняет действия, если выполняется хотя бы одно из условий, иначе выполняет действие по умолчанию.
//        /// </summary>
//        /// <typeparam name="TWith">Тип объекта.</typeparam>
//        /// <param name="obj">Объект.</param>
//        /// <param name="defaultAction">Действие по умолчанию.</param>
//        /// <param name="options">Массив пар (условие, действие).</param>
//        /// <returns>Объект obj.</returns>
//        public static TWith WithWhen<TWith>(this TWith obj, Action<TWith> defaultAction, params (Func<bool> when, Action<TWith> action)[] options)
//        {
//            foreach (var (when, action) in options)
//            {
//                if (!when())
//                    continue;
//                action(obj);
//                return obj;
//            }

//            defaultAction?.Invoke(obj);
//            return obj;
//        }

//        /// <summary>
//        /// Выполняет действие, если условие истинно.
//        /// </summary>
//        /// <typeparam name="TWith">Тип объекта.</typeparam>
//        /// <param name="obj">Объект.</param>
//        /// <param name="when">Условие.</param>
//        /// <param name="action">Действие.</param>
//        /// <returns>Объект obj.</returns>
//        public static TWith WithWhen<TWith>(this TWith obj, Func<bool> when, Action<TWith> action)
//        {
//            return WithWhen(obj, (when, action));
//        }



//        /// <summary>
//        /// Возвращает значение, соответствующее первому совпавшему случаю, иначе значение по умолчанию.
//        /// </summary>
//        /// <typeparam name="TWhen">Тип значения для сравнения.</typeparam>
//        /// <typeparam name="TThen">Тип возвращаемого значения.</typeparam>
//        /// <param name="obj">Значение для сравнения.</param>
//        /// <param name="when">Значение для сравнения.</param>
//        /// <param name="then">Возвращаемое значение.</param>
//        /// <param name="andSoOnWhenThenParams">Дополнительные пары (when, then).</param>
//        /// <returns>Значение then для первого совпадения или defaultValue.</returns>
//        public static TThen Case<TWhen, TThen>(this TWhen obj, TWhen when, TThen then, params object[] andSoOnWhenThenParams)
//        {
//            return Case(obj, default, when, then, andSoOnWhenThenParams);
//        }

//        /// <summary>
//        /// Возвращает значение, соответствующее первому совпавшему случаю, иначе значение по умолчанию.
//        /// </summary>
//        /// <typeparam name="TWhen">Тип значения для сравнения.</typeparam>
//        /// <typeparam name="TThen">Тип возвращаемого значения.</typeparam>
//        /// <param name="obj">Значение для сравнения.</param>
//        /// <param name="defaultValue">Значение по умолчанию.</param>
//        /// <param name="when">Значение для сравнения.</param>
//        /// <param name="then">Возвращаемое значение.</param>
//        /// <param name="andSoOnWhenThenParams">Дополнительные пары (when, then).</param>
//        /// <returns>Значение then для первого совпадения или defaultValue.</returns>
//        public static TThen Case<TWhen, TThen>(this TWhen obj, TThen defaultValue, TWhen when, TThen then, params object[] andSoOnWhenThenParams)
//        {
//            var cases = new List<(TWhen, TThen)> { (when, then) };
//            for (var i = 0; i < andSoOnWhenThenParams.Length / 2; i += 2)
//            {
//                cases.Add(((TWhen)andSoOnWhenThenParams[i], (TThen)andSoOnWhenThenParams[i + 1]));
//            }

//            return Case(obj, defaultValue, cases.ToArray());
//        }

//        /// <summary>
//        /// Возвращает значение, соответствующее первому совпавшему случаю, иначе значение по умолчанию.
//        /// </summary>
//        /// <typeparam name="TWhen">Тип значения для сравнения.</typeparam>
//        /// <typeparam name="TThen">Тип возвращаемого значения.</typeparam>
//        /// <param name="obj">Значение для сравнения.</param>
//        /// <param name="cases">Массив пар (значение для сравнения, возвращаемое значение).</param>
//        /// <returns>Значение then для первого совпадения или default(TThen).</returns>
//        public static TThen Case<TWhen, TThen>(this TWhen obj, params (TWhen when, TThen then)[] cases)
//        {
//            return Case(obj, default(TThen), cases);
//        }

//        /// <summary>
//        /// Возвращает значение, соответствующее первому совпавшему условию, иначе значение по умолчанию.
//        /// </summary>
//        /// <typeparam name="TWhen">Тип значения для сравнения.</typeparam>
//        /// <typeparam name="TThen">Тип возвращаемого значения.</typeparam>
//        /// <param name="obj">Значение для сравнения.</param>
//        /// <param name="defaultValue">Значение по умолчанию.</param>
//        /// <param name="cases">Массив пар (условие, возвращаемое значение).</param>
//        /// <returns>Значение value для первого совпадения или defaultValue.</returns>
//        public static TThen Case<TWhen, TThen>(this TWhen obj, TThen defaultValue, params (Func<TWhen, bool> when, TThen value)[] cases)
//        {
//            foreach (var (when, value) in cases)
//            {
//                if (when(obj))
//                    return value;
//            }

//            return defaultValue;
//        }

//        /// <summary>
//        /// Возвращает значение, соответствующее первому совпавшему условию, иначе значение по умолчанию.
//        /// </summary>
//        /// <typeparam name="TWhen">Тип значения для сравнения.</typeparam>
//        /// <typeparam name="TThen">Тип возвращаемого значения.</typeparam>
//        /// <param name="obj">Значение для сравнения.</param>
//        /// <param name="cases">Массив пар (условие, возвращаемое значение).</param>
//        /// <returns>Значение value для первого совпадения или default(TThen).</returns>
//        public static TThen Case<TWhen, TThen>(this TWhen obj, params (Func<TWhen, bool> when, TThen value)[] cases)
//        {
//            return Case(obj, default(TThen), cases);
//        }
//    }
//}