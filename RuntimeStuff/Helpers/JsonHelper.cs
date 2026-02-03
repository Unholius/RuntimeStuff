// <copyright file="JsonHelper.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace RuntimeStuff.Helpers
{
    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Вспомогательный класс для упрощённой работы с JSON-строками:
    /// извлечение значений, содержимого узлов, атрибутов
    /// и базовая сериализация объектов.
    /// </summary>
    /// <remarks>
    /// Реализация не является полноценным JSON-парсером и
    /// основана на регулярных выражениях.
    /// Подходит для простых и предсказуемых JSON-структур.
    /// </remarks>
    public static class JsonHelper
    {
        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertyCache = new ConcurrentDictionary<Type, PropertyInfo[]>();

        private static readonly Regex PropertyRegex =
                    new Regex(
                        "\"(?<name>[^\"]+)\"\\s*:\\s*(?<value>\\{.*?\\}|\\[.*?\\]|\".*?\"|true|false|null|-?\\d+(\\.\\d+)?)",
                        RegexOptions.Singleline);

        /// <summary>
        /// Извлекает атрибуты всех JSON-объектов с указанным именем узла.
        /// </summary>
        /// <param name="json">
        /// Строка, содержащая JSON-документ.
        /// </param>
        /// <param name="nodeNameSelector">
        /// Имя узла, значения которого должны быть JSON-объектами.
        /// </param>
        /// <param name="searchInArrays">Искать в массивах.</param>
        /// <returns>
        /// Массив словарей, где каждый словарь содержит пары
        /// «имя свойства — значение свойства».
        /// Если данные некорректны или объекты не найдены,
        /// возвращается пустой массив.
        /// </returns>
        public static Dictionary<string, string>[] GetAttributes(string json, Func<string, bool> nodeNameSelector, bool searchInArrays)
        {
            if (string.IsNullOrWhiteSpace(json))
                return Array.Empty<Dictionary<string, string>>();

            try
            {
                return FindNodes(json, nodeNameSelector)
                    .Where(x => searchInArrays ? IsObjectOrArray(x) : IsObject(x))
                    .Select(ParseObject)
                    .ToArray();
            }
            catch
            {
                return Array.Empty<Dictionary<string, string>>();
            }
        }

        /// <summary>
        /// Извлекает атрибуты всех JSON-объектов с указанным именем узла.
        /// </summary>
        /// <param name="json">
        /// Строка, содержащая JSON-документ.
        /// </param>
        /// <param name="nodeName">
        /// Имя узла, значения которого должны быть JSON-объектами.
        /// </param>
        /// <param name="searchInArrays">Искать в массивах.</param>
        /// <returns>
        /// Массив словарей, где каждый словарь содержит пары
        /// «имя свойства — значение свойства».
        /// Если данные некорректны или объекты не найдены,
        /// возвращается пустой массив.
        /// </returns>
        public static Dictionary<string, string>[] GetAttributes(string json, string nodeName, bool searchInArrays)
            => GetAttributes(json, x => x == nodeName, searchInArrays);

        /// <summary>
        /// Извлекает атрибуты JSON-объектов из предварительно
        /// отфильтрованных JSON-фрагментов.
        /// </summary>
        /// <param name="json">
        /// Строка, содержащая JSON-документ.
        /// </param>
        /// <param name="attributesNodeNameSelector">
        /// Имя узла, содержащего JSON-объекты,
        /// атрибуты которых необходимо извлечь.
        /// </param>
        /// <param name="contentNodeNameSelector">
        /// Имя узлов, используемых как источник JSON-фрагментов.
        /// </param>
        /// <param name="contentFilter">
        /// Фильтр, применяемый к JSON-содержимому узлов
        /// <paramref name="contentNodeNameSelector"/>.
        /// </param>
        /// <param name="searchInArrays">Искать в массивах.</param>
        /// <returns>
        /// Массив словарей атрибутов найденных JSON-объектов.
        /// </returns>
        public static Dictionary<string, string>[] GetAttributes(string json, Func<string, bool> attributesNodeNameSelector, Func<string, bool> contentNodeNameSelector, Func<string, bool> contentFilter, bool searchInArrays)
        {
            var contents = GetContents(json, contentNodeNameSelector, contentFilter);
            return contents.SelectMany(x => GetAttributes(x, attributesNodeNameSelector, searchInArrays)).ToArray();
        }

        /// <summary>
        /// Извлекает атрибуты JSON-объектов из предварительно
        /// отфильтрованных JSON-фрагментов.
        /// </summary>
        /// <param name="json">
        /// Строка, содержащая JSON-документ.
        /// </param>
        /// <param name="attributesNodeName">
        /// Имя узла, содержащего JSON-объекты,
        /// атрибуты которых необходимо извлечь.
        /// </param>
        /// <param name="contentNodeName">
        /// Имя узлов, используемых как источник JSON-фрагментов.
        /// </param>
        /// <param name="contentFilter">
        /// Фильтр, применяемый к JSON-содержимому узлов
        /// <paramref name="contentNodeName"/>.
        /// </param>
        /// <param name="searchInArrays">Искать в массивах.</param>
        /// <returns>
        /// Массив словарей атрибутов найденных JSON-объектов.
        /// </returns>
        public static Dictionary<string, string>[] GetAttributes(string json, string attributesNodeName, string contentNodeName, Func<string, bool> contentFilter, bool searchInArrays)
            => GetAttributes(json, x => x == attributesNodeName, x => x == contentNodeName, contentFilter, searchInArrays);

        /// <summary>
        /// Извлекает строковое содержимое узлов с указанным именем.
        /// </summary>
        /// <param name="json">
        /// Строка, содержащая JSON-документ.
        /// </param>
        /// <param name="nodeNameSelector">
        /// Имя узлов, содержимое которых необходимо получить.
        /// </param>
        /// <param name="contentFilter">
        /// Необязательный фильтр для JSON-фрагментов.
        /// </param>
        /// <returns>
        /// Массив строк с JSON-представлением найденных узлов.
        /// </returns>
        public static string[] GetContents(
                    string json,
                    Func<string, bool> nodeNameSelector,
                    Func<string, bool> contentFilter = null)
        {
            if (string.IsNullOrWhiteSpace(json))
                return Array.Empty<string>();

            try
            {
                return FindNodes(json, nodeNameSelector)
                    .Where(x => contentFilter == null || contentFilter(x))
                    .ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Извлекает строковое содержимое узлов с указанным именем.
        /// </summary>
        /// <param name="json">
        /// Строка, содержащая JSON-документ.
        /// </param>
        /// <param name="nodeName">
        /// Имя узлов, содержимое которых необходимо получить.
        /// </param>
        /// <param name="contentFilter">
        /// Необязательный фильтр для JSON-фрагментов.
        /// </param>
        /// <returns>
        /// Массив строк с JSON-представлением найденных узлов.
        /// </returns>
        public static string[] GetContents(
                    string json,
                    string nodeName,
                    Func<string, bool> contentFilter = null)
            => GetContents(json, x => x == nodeName, contentFilter);

        /// <summary>
        /// Извлекает значения простых JSON-узлов
        /// (строки, числа, логические значения).
        /// </summary>
        /// <param name="json">
        /// Строка, содержащая JSON-документ.
        /// </param>
        /// <param name="nodeNameSelector">
        /// Имя узлов, значения которых необходимо извлечь.
        /// </param>
        /// <param name="searchInArrays">Искать в массивах.</param>
        /// <returns>
        /// Массив строковых значений найденных узлов.
        /// Объекты и массивы игнорируются.
        /// </returns>
        public static string[] GetValues(string json, Func<string, bool> nodeNameSelector, bool searchInArrays = true)
        {
            if (string.IsNullOrWhiteSpace(json))
                return Array.Empty<string>();

            try
            {
                return FindNodes(json, nodeNameSelector)
                    .Where(v => searchInArrays ? !IsObjectOrArray(v) : IsObject(v))
                    .Select(Unwrap)
                    .ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Извлекает значения простых JSON-узлов
        /// (строки, числа, логические значения).
        /// </summary>
        /// <param name="json">
        /// Строка, содержащая JSON-документ.
        /// </param>
        /// <param name="nodeName">
        /// Имя узлов, значения которых необходимо извлечь.
        /// </param>
        /// <param name="searchInArrays">Искать в массивах.</param>
        /// <returns>
        /// Массив строковых значений найденных узлов.
        /// Объекты и массивы игнорируются.
        /// </returns>
        public static string[] GetValues(string json, string nodeName, bool searchInArrays = true)
            => GetValues(json, x => x == nodeName, searchInArrays);

        /// <summary>
        /// Извлекает значения узлов из предварительно
        /// отфильтрованных JSON-фрагментов.
        /// </summary>
        /// <param name="json">
        /// Строка, содержащая JSON-документ.
        /// </param>
        /// <param name="valueNodeNameSelctor">
        /// Имя узлов, значения которых необходимо получить.
        /// </param>
        /// <param name="contentNodeNameSelector">
        /// Имя узлов, используемых как источник JSON-фрагментов.
        /// </param>
        /// <param name="contentFilter">
        /// Фильтр для JSON-содержимого.
        /// </param>
        /// <returns>
        /// Массив строковых значений найденных узлов.
        /// </returns>
        public static string[] GetValues(string json, Func<string, bool> valueNodeNameSelctor, Func<string, bool> contentNodeNameSelector, Func<string, bool> contentFilter)
        {
            var contents = GetContents(json, contentNodeNameSelector, contentFilter);
            return contents.SelectMany(x => GetValues(x, valueNodeNameSelctor)).ToArray();
        }

        /// <summary>
        /// Извлекает значения узлов из предварительно
        /// отфильтрованных JSON-фрагментов.
        /// </summary>
        /// <param name="json">
        /// Строка, содержащая JSON-документ.
        /// </param>
        /// <param name="valueNodeName">
        /// Имя узлов, значения которых необходимо получить.
        /// </param>
        /// <param name="contentNodeName">
        /// Имя узлов, используемых как источник JSON-фрагментов.
        /// </param>
        /// <param name="contentFilter">
        /// Фильтр для JSON-содержимого.
        /// </param>
        /// <returns>
        /// Массив строковых значений найденных узлов.
        /// </returns>
        public static string[] GetValues(string json, string valueNodeName, string contentNodeName, Func<string, bool> contentFilter)
            => GetValues(json, x => x == valueNodeName, x => x == contentNodeName, contentFilter);

        /// <summary>
        /// Сериализует указанный объект в JSON-строку.
        /// </summary>
        /// <param name="obj">
        /// Объект для сериализации.
        /// Если равен <c>null</c>, возвращается строка <c>"null"</c>.
        /// </param>
        /// <param name="dateFormat">
        /// Формат даты для значений <see cref="DateTime"/> и <see cref="DateTimeOffset"/>.
        /// По умолчанию используется <c>yyyy-MM-dd</c>.
        /// </param>
        /// <param name="enumAsStrings">
        /// Если <c>true</c>, перечисления сериализуются как строки;
        /// если <c>false</c> — как числовые значения.
        /// </param>
        /// <param name="additionalFormats">
        /// Дополнительные форматы сериализации для конкретных типов.
        /// </param>
        /// <returns>
        /// JSON-представление объекта.
        /// </returns>
        public static string Serialize(
            object obj,
            string dateFormat = "yyyy-MM-dd",
            bool enumAsStrings = false,
            Dictionary<Type, string> additionalFormats = null)
        {
            if (obj == null)
            {
                return "null";
            }

            return SerializeInternal(obj, dateFormat, enumAsStrings, additionalFormats);
        }

        /// <summary>
        /// Определяет, является ли строка JSON-объектом.
        /// </summary>
        /// <param name="s">
        /// Строка, содержащая JSON-фрагмент.
        /// </param>
        /// <returns>
        /// <c>true</c>, если строка представляет собой JSON-объект
        /// (начинается с символа <c>{</c> после пропуска пробельных символов);
        /// иначе <c>false</c>.
        /// </returns>
        public static bool IsObject(string s) =>
            s.TrimStart().StartsWith("{");

        /// <summary>
        /// Определяет, является ли строка JSON-массивом.
        /// </summary>
        /// <param name="s">
        /// Строка, содержащая JSON-фрагмент.
        /// </param>
        /// <returns>
        /// <c>true</c>, если строка представляет собой JSON-массив
        /// (начинается с символа <c>[</c> после пропуска пробельных символов);
        /// иначе <c>false</c>.
        /// </returns>
        public static bool IsArray(string s) =>
            s.TrimStart().StartsWith("[");

        /// <summary>
        /// Определяет, является ли строка JSON-объектом или JSON-массивом.
        /// </summary>
        /// <param name="s">
        /// Строка, содержащая JSON-фрагмент.
        /// </param>
        /// <returns>
        /// <c>true</c>, если строка представляет собой JSON-объект
        /// или JSON-массив; иначе <c>false</c>.
        /// </returns>
        public static bool IsObjectOrArray(string s)
        {
            s = s.TrimStart();
            return s.StartsWith("{") || s.StartsWith("[");
        }

        private static string EscapeString(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            var sb = new StringBuilder(value.Length + 8);
            foreach (var c in value)
            {
                switch (c)
                {
                    case '\\':
                        sb.Append("\\\\");
                        break;

                    case '"':
                        sb.Append("\\\"");
                        break;

                    case '\b':
                        sb.Append("\\b");
                        break;

                    case '\f':
                        sb.Append("\\f");
                        break;

                    case '\n':
                        sb.Append("\\n");
                        break;

                    case '\r':
                        sb.Append("\\r");
                        break;

                    case '\t':
                        sb.Append("\\t");
                        break;

                    default:
                        sb.Append(c);
                        break;
                }
            }

            return sb.ToString();
        }

        private static IEnumerable<string> FindNodes(string json, Func<string, bool> nameSelector)
        {
            if (nameSelector == null)
                yield return json;

            foreach (Match match in PropertyRegex.Matches(json))
            {
                if (nameSelector(match.Groups["name"].Value))
                    yield return match.Groups["value"].Value;

                var value = match.Groups["value"].Value;
                if (IsObjectOrArray(value))
                {
                    foreach (var nested in FindNodes(value, nameSelector))
                        yield return nested;
                }
            }
        }

        private static bool IsNumeric(object obj)
        {
            if (obj == null)
                return false;

            switch (Type.GetTypeCode(obj.GetType()))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Single:
                    return true;

                default:
                    return false;
            }
        }

        private static Dictionary<string, string> ParseObject(string json)
        {
            var dict = new Dictionary<string, string>();
            foreach (Match match in PropertyRegex.Matches(json))
            {
                dict[match.Groups["name"].Value] = Unwrap(match.Groups["value"].Value);
            }

            return dict;
        }

        private static string Quote(string value)
        {
            return "\"" + value + "\"";
        }

        private static string SerializeDictionary(
            IDictionary dict,
            string dateFormat,
            bool enumAsStrings,
            Dictionary<Type, string> additionalFormats)
        {
            var sb = new StringBuilder(128);
            sb.Append('{');

            var first = true;
            foreach (DictionaryEntry entry in dict)
            {
                if (!first)
                {
                    sb.Append(',');
                }

                sb.Append(Quote(EscapeString(
                        Convert.ToString(entry.Key, CultureInfo.InvariantCulture))))
                  .Append(':')
                  .Append(SerializeInternal(entry.Value, dateFormat, enumAsStrings, additionalFormats));

                first = false;
            }

            sb.Append('}');
            return sb.ToString();
        }

        private static string SerializeEnumerable(
            IEnumerable enumerable,
            string dateFormat,
            bool enumAsStrings,
            Dictionary<Type, string> additionalFormats)
        {
            var sb = new StringBuilder(128);
            sb.Append('[');

            var first = true;
            foreach (var item in enumerable)
            {
                if (!first)
                {
                    sb.Append(',');
                }

                sb.Append(SerializeInternal(item, dateFormat, enumAsStrings, additionalFormats));
                first = false;
            }

            sb.Append(']');
            return sb.ToString();
        }

        private static string SerializeFormattable(
            object value,
            string defaultFormat,
            Dictionary<Type, string> additionalFormats,
            bool isNumeric)
        {
            var type = value.GetType();
            string format;

            if (additionalFormats != null &&
                additionalFormats.TryGetValue(type, out var customFormat))
            {
                format = customFormat;
            }
            else
            {
                format = defaultFormat;
            }

            var text = value is IFormattable formattable
                ? formattable.ToString(format, CultureInfo.InvariantCulture)
                : Convert.ToString(value, CultureInfo.InvariantCulture);

            return isNumeric ? text : Quote(text);
        }

        private static string SerializeInternal(
            object obj,
            string dateFormat,
            bool enumAsStrings,
            Dictionary<Type, string> additionalFormats)
        {
            switch (obj)
            {
                case string s:
                    return Quote(EscapeString(s));

                case bool b:
                    return b ? "true" : "false";

                case Enum e:
                    return enumAsStrings
                        ? Quote(e.ToString())
                        : Convert.ToInt64(e, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);

                case DateTime _:
                case DateTimeOffset _:
                    return SerializeFormattable(obj, dateFormat, additionalFormats, false);

                case TimeSpan _:
                    return SerializeFormattable(obj, null, additionalFormats, false);

                case IDictionary dict:
                    return SerializeDictionary(dict, dateFormat, enumAsStrings, additionalFormats);

                case IEnumerable enumerable:
                    return SerializeEnumerable(enumerable, dateFormat, enumAsStrings, additionalFormats);
            }

            if (IsNumeric(obj))
            {
                return SerializeFormattable(obj, null, additionalFormats, true);
            }

            return SerializeObject(obj, dateFormat, enumAsStrings, additionalFormats);
        }

        private static string SerializeObject(
            object obj,
            string dateFormat,
            bool enumAsStrings,
            Dictionary<Type, string> additionalFormats)
        {
            var type = obj.GetType();
            var properties = PropertyCache.GetOrAdd(
                type,
                t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance));

            var sb = new StringBuilder(256);
            sb.Append('{');

            var first = true;
            foreach (var prop in properties)
            {
                if (!prop.CanRead)
                {
                    continue;
                }

                var value = prop.GetValue(obj);

                if (value == null)
                {
                    continue;
                }

                if (!first)
                {
                    sb.Append(',');
                }

                sb.Append(Quote(EscapeString(prop.Name)))
                  .Append(':')
                  .Append(SerializeInternal(value, dateFormat, enumAsStrings, additionalFormats));

                first = false;
            }

            sb.Append('}');
            return sb.ToString();
        }

        private static string Unwrap(string value)
        {
            value = value.Trim();
            if (value.StartsWith("\"") && value.EndsWith("\""))
                return value.Substring(1, value.Length - 2);
            return value;
        }
    }
}