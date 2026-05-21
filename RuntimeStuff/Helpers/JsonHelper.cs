// <copyright file="JsonHelper.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace System.Helpers
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
        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertyCache = new();

        private static readonly Regex PropertyRegex =
                    new(
                        "\"(?<name>[^\"]+)\"\\s*:\\s*(?<value>\\{.*?\\}|\\[.*?\\]|\".*?\"|true|false|null|-?\\d+(\\.\\d+)?)",
                        RegexOptions.Singleline);

        private static ValueFormatter defaultValueFormatter = new()
        {
            StringPrefix = "\"",
            StringSuffix = "\"",
            DatePrefix = "\"",
            DateSuffix = "\"",
            DateFormat = "yyyy-MM-dd",
            DateTimeFormat = "yyyy-MM-ddTHH:mm:ssZ",
            EnumerablePrefix = "[",
            EnumerableSuffix = "]",
            ObjectPrefix = "{",
            ObjectSuffix = "}",
            EnumAsString = false,
        };

        static JsonHelper()
        {
            defaultValueFormatter.AddSerializer(t => t.IsDictionary(), (x, vf) => SerializeDictionary((IDictionary)x, vf));
            defaultValueFormatter.AddSerializer(t => !t.IsValueType && !t.IsBasic() && !t.IsCollection(), SerializeObject);
        }

        /// <summary>
        /// Преобразует JSON-строку в плоскую структуру словаря,
        /// где ключом является путь к узлу через точку,
        /// а значением — строковое представление простого значения.
        /// </summary>
        /// <param name="json">JSON-строка для обработки.</param>
        /// <returns>
        /// Словарь, в котором:
        /// <list type="bullet">
        /// <item>
        /// <description>
        /// ключ — путь к значению в формате <c>Parent.Child.Property</c>;
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// значение — строковое представление простого JSON-значения
        /// (строка, число или логическое значение).
        /// </description>
        /// </item>
        /// </list>
        /// Если входная строка пуста, содержит только пробелы
        /// или произошла ошибка разбора — возвращается пустой словарь.
        /// </returns>
        /// <remarks>
        /// Обрабатываются только простые типы значений (string, number, boolean).
        /// Сложные структуры (объекты и массивы) рекурсивно разворачиваются
        /// до достижения простых узлов.
        /// Исключения при разборе намеренно подавляются,
        /// что соответствует общей стратегии обработки ошибок.
        /// </remarks>
        public static Dictionary<string, string> GetAllValues(string json)
        {
            var result = new Dictionary<string, string>();

            if (string.IsNullOrWhiteSpace(json))
            {
                return result;
            }

            try
            {
                Flatten(json, null, result);
            }
            catch
            {
                // намеренно подавляем ошибки, поведение согласовано с остальными методами
            }

            return result;
        }

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
            {
                return Array.Empty<Dictionary<string, string>>();
            }

            try
            {
                return [.. FindNodes(json, nodeNameSelector)
                    .Where(x => searchInArrays ? IsObjectOrArray(x) : IsObject(x))
                    .Select(ParseObject)];
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
            return [.. contents.SelectMany(x => GetAttributes(x, attributesNodeNameSelector, searchInArrays))];
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
            {
                return Array.Empty<string>();
            }

            try
            {
                return [.. FindNodes(json, nodeNameSelector).Where(x => contentFilter == null || contentFilter(x))];
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
            {
                return Array.Empty<string>();
            }

            try
            {
                return [.. FindNodes(json, nodeNameSelector)
                    .Where(v => searchInArrays || !IsArray(v))
                    .Select(Unwrap)];
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
            return [.. contents.SelectMany(x => GetValues(x, valueNodeNameSelctor))];
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
        /// <param name="customTypeFormats">
        /// Дополнительные форматы сериализации для конкретных типов.
        /// </param>
        /// <returns>
        /// JSON-представление объекта.
        /// </returns>
        public static string Serialize(
            object obj,
            string dateFormat = "yyyy-MM-dd",
            bool enumAsStrings = false,
            Dictionary<Type, string> customTypeFormats = null)
        {
            if (obj == null)
            {
                return "null";
            }

            var vf = new ValueFormatter(
                defaultValueFormatter,
                dateFormat,
                enumAsStrings,
                true,
                StringHelper.EscapeMode.Json)
            {
                CustomTypeFormat = customTypeFormats ?? [],
            };

            return SerializeInternal(obj, vf);
        }

        /// <summary>
        /// Сериализует объект в строковое представление с использованием указанного <see cref="ValueFormatter"/>.
        /// </summary>
        /// <param name="obj">Объект, который нужно сериализовать. Если <c>null</c>, возвращается строка "null".</param>
        /// <param name="valueFormatter">Экземпляр <see cref="ValueFormatter"/>, задающий правила форматирования и сериализации объектов.</param>
        /// <returns>Строковое представление объекта согласно правилам <paramref name="valueFormatter"/>.</returns>
        /// <remarks>
        /// Если объект <paramref name="obj"/> равен <c>null</c>, возвращается строка "null".
        /// В противном случае используется внутренний метод <c>SerializeInternal</c> для выполнения сериализации.
        /// </remarks>
        public static string Serialize(
            object obj,
            ValueFormatter valueFormatter)
        {
            if (obj == null)
            {
                return "null";
            }

            return SerializeInternal(obj, valueFormatter);
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
            {
                yield return json;
            }

            foreach (Match match in PropertyRegex.Matches(json))
            {
                if (nameSelector != null && nameSelector(match.Groups["name"].Value))
                {
                    yield return match.Groups["value"].Value;
                }

                var value = match.Groups["value"].Value;
                if (IsObjectOrArray(value))
                {
                    foreach (var nested in FindNodes(value, nameSelector))
                    {
                        yield return nested;
                    }
                }
            }
        }

        private static bool IsNumeric(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            return Type.GetTypeCode(obj.GetType()) switch
            {
                TypeCode.Byte or TypeCode.SByte or TypeCode.UInt16 or TypeCode.UInt32 or TypeCode.UInt64 or TypeCode.Int16 or TypeCode.Int32 or TypeCode.Int64 or TypeCode.Decimal or TypeCode.Double or TypeCode.Single => true,
                _ => false,
            };
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

        private static string SerializeDictionary(IDictionary dict, ValueFormatter formatter)
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
                  .Append(SerializeInternal(entry.Value, formatter));

                first = false;
            }

            sb.Append('}');
            return sb.ToString();
        }

        private static string SerializeEnumerable(
            IEnumerable enumerable,
            ValueFormatter formatter)
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

                sb.Append(SerializeInternal(item, formatter));
                first = false;
            }

            sb.Append(']');
            return sb.ToString();
        }

        private static string SerializeInternal(object obj, ValueFormatter formatter)
        {
            if (formatter == null)
            {
                formatter = defaultValueFormatter;
            }

            return formatter.Format(obj);
        }

        private static string SerializeObject(object obj, ValueFormatter formatter)
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
                  .Append(SerializeInternal(value, formatter));

                first = false;
            }

            sb.Append('}');
            return sb.ToString();
        }

        private static string Unwrap(string value)
        {
            value = value.Trim();
            if (value.StartsWith("\"") && value.EndsWith("\""))
            {
                return value.Substring(1, value.Length - 2);
            }

            return value;
        }

        private static void Flatten(string json, string prefix, Dictionary<string, string> dict)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            json = json.Trim();

            if (IsObject(json))
            {
                foreach (Match match in PropertyRegex.Matches(json))
                {
                    var name = match.Groups["name"].Value;
                    var value = match.Groups["value"].Value.Trim();

                    var path = string.IsNullOrEmpty(prefix)
                        ? name
                        : prefix + "." + name;

                    if (IsObject(value) || IsArray(value))
                    {
                        Flatten(value, path, dict);
                    }
                    else if (IsSimpleValue(value))
                    {
                        dict[path] = Unwrap(value);
                    }
                }
            }
            else if (IsArray(json))
            {
                var index = 0;

                foreach (var element in SplitArray(json))
                {
                    var trimmed = element.Trim();

                    var path = string.IsNullOrEmpty(prefix)
                        ? "[" + index.ToString(CultureInfo.InvariantCulture) + "]"
                        : prefix + ".[" + index.ToString(CultureInfo.InvariantCulture) + "]";

                    if (IsObject(trimmed) || IsArray(trimmed))
                    {
                        Flatten(trimmed, path, dict);
                    }
                    else if (IsSimpleValue(trimmed))
                    {
                        dict[path] = Unwrap(trimmed);
                    }

                    index++;
                }
            }
        }

        private static bool IsSimpleValue(string value)
        {
            value = value.Trim();

            if (value == "true" || value == "false")
            {
                return true;
            }

            if (value.StartsWith("\"") && value.EndsWith("\""))
            {
                return true;
            }

            if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
            {
                return true;
            }

            return false;
        }

        private static IEnumerable<string> SplitArray(string json)
        {
            json = json.Trim();

            if (!json.StartsWith("[") || !json.EndsWith("]"))
            {
                yield break;
            }

            var content = json.Substring(1, json.Length - 2);

            var depth = 0;
            var inString = false;
            var sb = new StringBuilder();

            for (var i = 0; i < content.Length; i++)
            {
                var c = content[i];

                if (c == '"' && (i == 0 || content[i - 1] != '\\'))
                {
                    inString = !inString;
                }

                if (!inString)
                {
                    if (c == '{' || c == '[')
                    {
                        depth++;
                    }
                    else if (c == '}' || c == ']')
                    {
                        depth--;
                    }
                    else if (c == ',' && depth == 0)
                    {
                        yield return sb.ToString();
                        sb.Clear();
                        continue;
                    }
                }

                sb.Append(c);
            }

            if (sb.Length > 0)
            {
                yield return sb.ToString();
            }
        }
    }
}