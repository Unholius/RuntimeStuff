// <copyright file="JsonSerializerHelper.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace RuntimeStuff.Helpers
{
    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Reflection;
    using System.Text;

    /// <summary>
    /// Вспомогательный класс для сериализации объектов в JSON строку.
    /// </summary>
    public static class JsonSerializerHelper
    {
        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertyCache = new ConcurrentDictionary<Type, PropertyInfo[]>();

        /// <summary>
        /// Сериализует указанный объект в строковое представление.
        /// </summary>
        /// <param name="obj">Объект для сериализации. Если равен <c>null</c>, возвращается строка "null".</param>
        /// <param name="dateFormat">
        /// Формат даты для объектов типа <see cref="DateTime"/>.
        /// По умолчанию используется "yyyy-MM-dd".
        /// </param>
        /// <param name="enumAsStrings">
        /// Если <c>true</c>, значения перечислений (<see cref="Enum"/>) будут сериализованы как строки;
        /// если <c>false</c>, как числовые значения.
        /// По умолчанию <c>false</c>.
        /// </param>
        /// <param name="additionalFormats">
        /// Дополнительные форматы для конкретных типов. Ключ — тип, значение — строковый формат.
        /// Можно использовать для переопределения стандартной сериализации отдельных типов.
        /// </param>
        /// <returns>Строковое представление объекта.</returns>
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

        private static bool IsNumeric(object obj)
        {
            return obj != null && Obj.IsNumeric(obj.GetType(), true);
        }

        private static string Quote(string value)
        {
            return "\"" + value + "\"";
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
    }
}
