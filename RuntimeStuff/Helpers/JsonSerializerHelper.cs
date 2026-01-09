// <copyright file="JsonSerializerHelper.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace RuntimeStuff.Helpers
{
    using System;
    using System.Collections;
    using System.Reflection;
    using System.Text;

    /// <summary>
    /// Вспомогательный класс для сериализации объектов в JSON строку.
    /// Поддерживает базовые типы, коллекции, словари и объекты с публичными свойствами.
    /// </summary>
    /// <remarks>
    /// Этот класс предоставляет простую сериализацию объектов в JSON без использования сторонних библиотек.
    /// Не поддерживает все особенности стандарта JSON, но подходит для большинства базовых сценариев.
    /// </remarks>
    public static class JsonSerializerHelper
    {
        /// <summary>
        /// Сериализует объект в JSON строку.
        /// </summary>
        /// <param name="obj">Объект для сериализации.</param>
        /// <param name="dateFormat">Формат даты для сериализации объектов <see cref="DateTime"/>. По умолчанию "yyyy-MM-dd".</param>
        /// <returns>JSON строка, представляющая объект.</returns>
        /// <example>
        /// <code>
        /// // Сериализация простого объекта
        /// var person = new { Name = "John", Age = 30 };
        /// string json = JsonSerializerHelper.Serialize(person);
        /// // Результат: {"Name":"John","Age":30}
        /// // Сериализация с форматом даты
        /// var data = new { Date = new DateTime(2023, 12, 25) };
        /// string json = JsonSerializerHelper.Serialize(data, "dd.MM.yyyy");
        /// // Результат: {"Date":"25.12.2023"}
        /// </code>
        /// </example>
        public static string Serialize(object obj, string dateFormat = "yyyy-MM-dd")
        {
            if (obj == null)
            {
                return "null";
            }

            if (obj is string str)
            {
                return "\"" + EscapeString(str) + "\"";
            }

            if (obj is bool b)
            {
                return b ? "true" : "false";
            }

            if (IsNumeric(obj))
            {
                return Convert.ToString(obj, System.Globalization.CultureInfo.InvariantCulture);
            }

            if (obj is DateTime dt)
            {
                var formatted = dateFormat != null ? dt.ToString(dateFormat) : dt.ToString("yyyy-MM-dd");
                return "\"" + formatted + "\"";
            }

            if (obj is IDictionary dict)
            {
                return SerializeDictionary(dict);
            }

            if (obj is IEnumerable enumerable)
            {
                return SerializeEnumerable(enumerable);
            }

            // Для объектов с публичными свойствами
            return SerializeObject(obj);
        }

        /// <summary>
        /// Сериализует словарь в JSON объект.
        /// </summary>
        /// <param name="dict">Словарь для сериализации.</param>
        /// <returns>JSON строка, представляющая словарь.</returns>
        private static string SerializeDictionary(IDictionary dict)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            var first = true;
            foreach (DictionaryEntry kvp in dict)
            {
                if (!first)
                {
                    sb.Append(",");
                }

                sb.Append("\"").Append(kvp.Key).Append("\":").Append(Serialize(kvp.Value));
                first = false;
            }

            sb.Append("}");
            return sb.ToString();
        }

        /// <summary>
        /// Сериализует перечисление в JSON массив.
        /// </summary>
        /// <param name="enumerable">Перечисление для сериализации.</param>
        /// <returns>JSON строка, представляющая массив.</returns>
        private static string SerializeEnumerable(IEnumerable enumerable)
        {
            var sb = new StringBuilder();
            sb.Append("[");
            var first = true;
            foreach (var item in enumerable)
            {
                if (!first)
                {
                    sb.Append(",");
                }

                sb.Append(Serialize(item));
                first = false;
            }

            sb.Append("]");
            return sb.ToString();
        }

        /// <summary>
        /// Сериализует объект в JSON, используя его публичные свойства.
        /// </summary>
        /// <param name="obj">Объект для сериализации.</param>
        /// <returns>JSON строка, представляющая объект.</returns>
        private static string SerializeObject(object obj)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            var first = true;
            var props = obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in props)
            {
                if (!prop.CanRead)
                {
                    continue;
                }

                var value = prop.GetValue(obj);
                if (!first)
                {
                    sb.Append(",");
                }

                sb.Append("\"").Append(prop.Name).Append("\":").Append(Serialize(value));
                first = false;
            }

            sb.Append("}");
            return sb.ToString();
        }

        /// <summary>
        /// Проверяет, является ли объект числовым типом.
        /// </summary>
        /// <param name="obj">Объект для проверки.</param>
        /// <returns>true, если объект является числовым типом; иначе false.</returns>
        private static bool IsNumeric(object obj)
        {
            return obj is byte || obj is sbyte ||
                   obj is short || obj is ushort ||
                   obj is int || obj is uint ||
                   obj is long || obj is ulong ||
                   obj is float || obj is double ||
                   obj is decimal;
        }

        /// <summary>
        /// Экранирует специальные символы в строке для JSON.
        /// </summary>
        /// <param name="str">Исходная строка.</param>
        /// <returns>Экранированная строка.</returns>
        /// <remarks>
        /// Экранирует следующие символы:
        /// - \ (обратная косая черта)
        /// - " (двойная кавычка)
        /// - \b (backspace)
        /// - \f (form feed)
        /// - \n (new line)
        /// - \r (carriage return)
        /// - \t (tab).
        /// </remarks>
        private static string EscapeString(string str)
        {
            return str.Replace("\\", "\\\\")
                      .Replace("\"", "\\\"")
                      .Replace("\b", "\\b")
                      .Replace("\f", "\\f")
                      .Replace("\n", "\\n")
                      .Replace("\r", "\\r")
                      .Replace("\t", "\\t");
        }
    }
}
