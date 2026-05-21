// <copyright file="ValueFormatter.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace System
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Helpers;
    using System.Linq;

    /// <summary>
    /// Универсальный форматтер значений различных типов с поддержкой кастомных правил,
    /// культуры, форматирования дат/чисел и постобработки.
    /// </summary>
    /// <remarks>
    /// Поддерживает:
    /// <list type="bullet">
    /// <item><description>Кастомные сериализаторы по типу.</description></item>
    /// <item><description>Форматирование строк, чисел, дат, перечислений и коллекций.</description></item>
    /// <item><description>Настройку префиксов/суффиксов для различных типов.</description></item>
    /// <item><description>Обработку <c>null</c> и пользовательских "null-значений".</description></item>
    /// <item><description>Постобработку результата через цепочку функций.</description></item>
    /// </list>
    /// </remarks>
    public class ValueFormatter : ICloneable
    {
        private readonly ConcurrentDictionary<Type, Func<object, ValueFormatter, string>> serializerCache = new();
        private HashSet<object> nullValuesSet = new(Obj.NullValues);

        /// <summary>
        /// Инициализирует новый экземпляр <see cref="ValueFormatter"/> с настройками по умолчанию.
        /// </summary>
        public ValueFormatter()
        {
        }

        /// <summary>
        /// Инициализирует новый экземпляр <see cref="ValueFormatter"/> на основе другого форматтера
        /// с возможностью переопределения отдельных параметров.
        /// </summary>
        /// <param name="baseFormatter">Базовый форматтер, из которого копируются настройки.</param>
        /// <param name="dateFormat">Формат даты и времени.</param>
        /// <param name="enumAsString">Флаг, указывающий, форматировать ли перечисления как строки.</param>
        /// <param name="trimSpaces">Удалять ли пробелы по краям строк.</param>
        /// <param name="escapeMode">Режим экранирования строк.</param>
        public ValueFormatter(ValueFormatter baseFormatter, string dateFormat, bool enumAsString = false, bool trimSpaces = true, StringHelper.EscapeMode escapeMode = StringHelper.EscapeMode.None)
        {
            this.Serializers = baseFormatter.Serializers;
            this.PostFormatters = new List<Func<string, string>>(baseFormatter.PostFormatters);
            this.DecimalNumberFormat = baseFormatter.DecimalNumberFormat;
            this.CultureInfo = baseFormatter.CultureInfo;
            this.FalseValue = baseFormatter.FalseValue;
            this.TrueValue = baseFormatter.TrueValue;
            this.NullValue = baseFormatter.NullValue;
            this.NullPrefix = baseFormatter.NullPrefix;
            this.NullSuffix = baseFormatter.NullSuffix;
            this.EnumerablePrefix = baseFormatter.EnumerablePrefix;
            this.EnumerableSuffix = baseFormatter.EnumerableSuffix;
            this.EnumerableSeparator = baseFormatter.EnumerableSeparator;
            this.StringPrefix = baseFormatter.StringPrefix;
            this.StringSuffix = baseFormatter.StringSuffix;
            this.NumberPrefix = baseFormatter.NumberPrefix;
            this.NumberSuffix = baseFormatter.NumberSuffix;
            this.ObjectPrefix = baseFormatter.ObjectPrefix;
            this.ObjectSuffix = baseFormatter.ObjectSuffix;
            this.BoolPrefix = baseFormatter.BoolPrefix;
            this.BoolSuffix = baseFormatter.BoolSuffix;
            this.DatePrefix = baseFormatter.DatePrefix;
            this.DateSuffix = baseFormatter.DateSuffix;
            this.TimeFormat = baseFormatter.TimeFormat;
            this.TrimNumberZeroes = baseFormatter.TrimNumberZeroes;
            this.NormalizeWhitespaces = baseFormatter.NormalizeWhitespaces;
            this.DateFormat = dateFormat;
            this.DateTimeFormat = dateFormat;
            this.EnumAsString = enumAsString;
            this.TrimSpaces = trimSpaces;
            this.EscapeMode = escapeMode;
        }

        /// <summary>
        /// Инициализирует новый экземпляр <see cref="ValueFormatter"/> с заданным форматом даты.
        /// </summary>
        /// <param name="dateFormat">Формат даты и времени.</param>
        /// <param name="enumAsString">Флаг, указывающий, форматировать ли перечисления как строки.</param>
        /// <param name="trimSpaces">Удалять ли пробелы по краям строк.</param>
        /// <param name="escapeMode">Режим экранирования строк.</param>
        public ValueFormatter(string dateFormat, bool enumAsString = false, bool trimSpaces = true, StringHelper.EscapeMode escapeMode = StringHelper.EscapeMode.None)
        {
            this.DateFormat = dateFormat;
            this.DateTimeFormat = dateFormat;
            this.EnumAsString = enumAsString;
            this.TrimSpaces = trimSpaces;
            this.EscapeMode = escapeMode;
        }

        /// <summary>
        /// Сериализатор значений в Json формате.
        /// </summary>
        public static ValueFormatter JsonValueFormatter { get; } = new()
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

        /// <summary>
        /// Сериализатор значений в SQL формате.
        /// </summary>
        public static ValueFormatter SqlValueFormatter { get; } = new()
        {
            StringPrefix = "'",
            StringSuffix = "'",
            DatePrefix = "'",
            DateSuffix = "'",
            EscapeMode = StringHelper.EscapeMode.Sql,
            TrueValue = "1",
            FalseValue = "0",
            DateFormat = "yyyy-MM-dd",
            DateTimeFormat = "yyyy-MM-ddTHH:mm:ss.fff",
        };

        /// <summary>
        /// Префикс, добавляемый перед представлением логического значения (<c>true</c> / <c>false</c>) при сериализации.
        /// Например, можно использовать "(" для обрамления значения.
        /// </summary>
        public string BoolPrefix { get; set; }

        /// <summary>
        /// Суффикс, добавляемый после представления логического значения (<c>true</c> / <c>false</c>) при сериализации.
        /// Например, можно использовать ")" для обрамления значения.
        /// </summary>
        public string BoolSuffix { get; set; }

        /// <summary>
        /// Культура, используемая для форматирования чисел и дат.
        /// По умолчанию — <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public CultureInfo CultureInfo { get; set; } = CultureInfo.InvariantCulture;

        /// <summary>
        /// Словарь пользовательских форматов для конкретных типов.
        /// </summary>
        public Dictionary<Type, string> CustomTypeFormat { get; set; } = [];

        /// <summary>
        /// Формат даты (без времени).
        /// </summary>
        public string DateFormat { get; set; } = "yyyy-MM-dd";

        /// <summary>
        /// Префикс, добавляемый перед представлением даты при сериализации.
        /// Например, можно использовать "(" для обрамления значения даты.
        /// </summary>
        public string DatePrefix { get; set; }

        /// <summary>
        /// Суффикс, добавляемый после представления даты при сериализации.
        /// Например, можно использовать ")" для обрамления значения даты.
        /// </summary>
        public string DateSuffix { get; set; }

        /// <summary>
        /// Формат даты и времени.
        /// </summary>
        public string DateTimeFormat { get; set; } = "yyyy-MM-dd HH:mm:ss";

        /// <summary>
        /// Формат чисел с плавающей точкой.
        /// </summary>
        public string DecimalNumberFormat { get; set; }

        /// <summary>
        /// Определяет, выводить ли значения перечислений как строки.
        /// </summary>
        public bool EnumAsString { get; set; }

        /// <summary>
        /// Префикс, добавляемый перед сериализацией коллекции (например, "[" для списков).
        /// </summary>
        public string EnumerablePrefix { get; set; }

        /// <summary>
        /// Разделитель элементов в сериализуемой коллекции (например, ", ").
        /// </summary>
        public string EnumerableSeparator { get; set; }

        /// <summary>
        /// Суффикс, добавляемый после сериализации коллекции (например, "]" для списков).
        /// </summary>
        public string EnumerableSuffix { get; set; }

        /// <summary>
        /// Режим экранирования строк.
        /// </summary>
        public StringHelper.EscapeMode EscapeMode { get; set; } = StringHelper.EscapeMode.None;

        /// <summary>
        /// Значение, используемое для <c>false</c>.
        /// </summary>
        public string FalseValue { get; set; }

        /// <summary>
        /// Определяет, удалять ли лишние пробелы в строках.
        /// </summary>
        public bool NormalizeWhitespaces { get; set; }

        /// <summary>
        /// Префикс, добавляемый перед представлением <c>null</c> при сериализации.
        /// Например, можно использовать "(" для обрамления значения null.
        /// </summary>
        public string NullPrefix { get; set; }

        /// <summary>
        /// Суффикс, добавляемый после представления <c>null</c> при сериализации.
        /// Например, можно использовать ")" для обрамления значения null.
        /// </summary>
        public string NullSuffix { get; set; }

        /// <summary>
        /// Строковое представление <c>null</c>.
        /// </summary>
        public string NullValue { get; set; }

        /// <summary>
        /// Дополнительные значения, которые следует интерпретировать как <c>null</c>.
        /// </summary>
        public HashSet<object> NullValues
        {
            get => this.nullValuesSet;
        }

        /// <summary>
        /// Префикс, добавляемый перед числовым значением при сериализации.
        /// Например, можно использовать "(" для обрамления числа.
        /// </summary>
        public string NumberPrefix { get; set; }

        /// <summary>
        /// Суффикс, добавляемый после числового значения при сериализации.
        /// Например, можно использовать ")" для обрамления числа.
        /// </summary>
        public string NumberSuffix { get; set; }

        /// <summary>
        /// Префикс, добавляемый перед сериализацией объекта.
        /// Например, можно использовать "{" для обрамления объекта.
        /// </summary>
        public string ObjectPrefix { get; set; }

        /// <summary>
        /// Суффикс, добавляемый после сериализации объекта.
        /// Например, можно использовать "}" для обрамления объекта.
        /// </summary>
        public string ObjectSuffix { get; set; }

        /// <summary>
        /// Список функций постобработки результата.
        /// </summary>
        public List<Func<string, string>> PostFormatters { get; set; } = [];

        /// <summary>
        /// Список пользовательских сериализаторов.
        /// </summary>
        public List<(Func<Type, bool> Condition, Func<object, ValueFormatter, string> Serializer)> Serializers { get; set; } = [];

        /// <summary>
        /// Префикс, добавляемый перед строковым значением при сериализации.
        /// Например, можно использовать кавычку '"' или другую обертку.
        /// </summary>
        public string StringPrefix { get; set; }

        /// <summary>
        /// Суффикс, добавляемый после строкового значения при сериализации.
        /// Например, можно использовать кавычку '"' или другую обертку.
        /// </summary>
        public string StringSuffix { get; set; }

        /// <summary>
        /// Формат времени.
        /// </summary>
        public string TimeFormat { get; set; } = "HH:mm:ss";

        /// <summary>
        /// Определяет, удалять ли незначащие нули в числах.
        /// </summary>
        public bool TrimNumberZeroes { get; set; }

        /// <summary>
        /// Определяет, обрезать ли пробелы по краям строк.
        /// </summary>
        public bool TrimSpaces { get; set; }

        /// <summary>
        /// Значение, используемое для <c>true</c>.
        /// </summary>
        public string TrueValue { get; set; }

        /// <summary>
        /// Добавляет пользовательский сериализатор для типов, удовлетворяющих условию.
        /// </summary>
        /// <param name="typeCondition">Условие для типа.</param>
        /// <param name="serializer">Функция сериализации.</param>
        public void AddSerializer(Func<Type, bool> typeCondition, Func<object, ValueFormatter, string> serializer)
        {
            this.Serializers.Add((typeCondition, serializer));
        }

        /// <summary>
        /// Creates a new ValueFormatter instance that is a copy of the current instance.
        /// </summary>
        /// <remarks>The returned clone copies all value and collection properties, ensuring that mutable
        /// collections are not shared between the original and the clone. Changes to the collections in the cloned
        /// instance do not affect the original instance.</remarks>
        /// <returns>A new object that is a deep copy of this ValueFormatter instance.</returns>
        public object Clone()
        {
            var clone = new ValueFormatter
            {
                // простые значения
                BoolPrefix = this.BoolPrefix,
                BoolSuffix = this.BoolSuffix,
                CultureInfo = this.CultureInfo,
                DateFormat = this.DateFormat,
                DatePrefix = this.DatePrefix,
                DateSuffix = this.DateSuffix,
                DateTimeFormat = this.DateTimeFormat,
                DecimalNumberFormat = this.DecimalNumberFormat,
                EnumAsString = this.EnumAsString,
                EnumerablePrefix = this.EnumerablePrefix,
                EnumerableSeparator = this.EnumerableSeparator,
                EnumerableSuffix = this.EnumerableSuffix,
                EscapeMode = this.EscapeMode,
                FalseValue = this.FalseValue,
                NormalizeWhitespaces = this.NormalizeWhitespaces,
                NullPrefix = this.NullPrefix,
                NullSuffix = this.NullSuffix,
                NullValue = this.NullValue,
                NumberPrefix = this.NumberPrefix,
                NumberSuffix = this.NumberSuffix,
                ObjectPrefix = this.ObjectPrefix,
                ObjectSuffix = this.ObjectSuffix,
                StringPrefix = this.StringPrefix,
                StringSuffix = this.StringSuffix,
                TimeFormat = this.TimeFormat,
                TrimNumberZeroes = this.TrimNumberZeroes,
                TrimSpaces = this.TrimSpaces,
                TrueValue = this.TrueValue,

                // коллекции — новые инстансы
                CustomTypeFormat = this.CustomTypeFormat != null
                    ? new Dictionary<Type, string>(this.CustomTypeFormat)
                    : null,

                PostFormatters = this.PostFormatters != null
                    ? new List<Func<string, string>>(this.PostFormatters)
                    : null,

                Serializers = this.Serializers != null
                    ? new List<(Func<Type, bool>, Func<object, ValueFormatter, string>)>(this.Serializers)
                    : null,
            };

            // HashSet отдельно (важно не шарить ссылку)
            if (this.nullValuesSet != null)
            {
                clone.nullValuesSet = new HashSet<object>(this.nullValuesSet);
            }

            return clone;
        }

        /// <summary>
        /// Форматирует значение в строку в соответствии с текущими настройками.
        /// </summary>
        /// <param name="value">Значение для форматирования.</param>
        /// <returns>Отформатированная строка.</returns>
        /// <remarks>
        /// Порядок обработки:
        /// <list type="number">
        /// <item><description>Обработка <c>null</c> и пользовательских null-значений.</description></item>
        /// <item><description>Пользовательские сериализаторы.</description></item>
        /// <item><description>Строки, bool, enum, даты, коллекции, числа.</description></item>
        /// <item><description>Fallback через <see cref="object.ToString"/>.</description></item>
        /// <item><description>Постобработка через <see cref="PostFormatters"/>.</description></item>
        /// </list>
        /// </remarks>
        public virtual string Format(object value)
        {
            // 1. NULL handling
            if (value == null || (this.NullValues != null && this.NullValues.Contains(value)))
            {
                var nullText = this.NullValue ?? string.Empty;
                return this.ApplyPost(StringHelper.ApplyAffixes(nullText, this.NullPrefix, this.NullSuffix));
            }

            var type = value.GetType();
            var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
            this.CustomTypeFormat.TryGetValue(underlyingType, out var customTypeFormat);

            if (this.TryGetSerializer(underlyingType, out var customSerializer))
            {
                var serialized = customSerializer(value, this);
                return this.ApplyPost(serialized);
            }

            string result;

            // 3. String
            if (value is string str)
            {
                if (this.TrimSpaces)
                {
                    str = str.Trim();
                }

                if (this.NormalizeWhitespaces)
                {
                    str = NormalizeWs(str);
                }

                if (this.EscapeMode != StringHelper.EscapeMode.None)
                {
                    str = StringHelper.EscapeString(str, this.EscapeMode);
                }

                return this.ApplyPost(StringHelper.ApplyAffixes(str, this.StringPrefix, this.StringSuffix));
            }

            // 4. Boolean
            if (value is bool b)
            {
                var text = b
                    ? (this.TrueValue ?? "true")
                    : (this.FalseValue ?? "false");

                result = StringHelper.ApplyAffixes(text, this.BoolPrefix, this.BoolSuffix);
                return this.ApplyPost(result);
            }

            // 5. Enum
            if (type.IsEnum)
            {
                var enumText = this.EnumAsString ? StringHelper.ApplyAffixes(value.ToString(), this.StringPrefix, this.StringSuffix) : StringHelper.ApplyAffixes(Convert.ToInt64(value).ToString(this.CultureInfo), this.NumberPrefix, this.NumberSuffix);
                return this.ApplyPost(enumText);
            }

            // 6. DateTime
            if (value is DateTime dt)
            {
                var format = customTypeFormat ?? (dt.TimeOfDay != TimeSpan.Zero && !string.IsNullOrWhiteSpace(this.DateTimeFormat) ? this.DateTimeFormat : this.DateFormat ?? "yyyy-MM-dd");
                var text = dt.ToString(format, this.CultureInfo);
                result = StringHelper.ApplyAffixes(text, this.DatePrefix, this.DateSuffix);
                return this.ApplyPost(result);
            }

            if (value is DateTimeOffset dto)
            {
                var format = customTypeFormat ?? (dto.TimeOfDay != TimeSpan.Zero && !string.IsNullOrWhiteSpace(this.DateTimeFormat) ? this.DateTimeFormat : this.DateFormat ?? "yyyy-MM-dd HH:mm:ss");
                var text = dto.ToString(format, this.CultureInfo);
                result = StringHelper.ApplyAffixes(text, this.DatePrefix, this.DateSuffix);
                return this.ApplyPost(result);
            }

#if NET6_0_OR_GREATER
            if (value is DateOnly d)
            {
                var format = customTypeFormat ?? this.DateFormat ?? "yyyy-MM-dd";
                var text = d.ToString(format, this.CultureInfo);
                result = StringHelper.ApplyAffixes(text, this.DatePrefix, this.DateSuffix);
                return this.ApplyPost(result);
            }

            if (value is TimeOnly tOnly)
            {
                var format = customTypeFormat ?? this.TimeFormat ?? "HH:mm:ss";
                var text = tOnly.ToString(format, this.CultureInfo);
                return this.ApplyPost(text);
            }
#endif
            if (value is TimeSpan ts)
            {
                var format = customTypeFormat ?? "c";
                var text = StringHelper.ApplyAffixes(ts.ToString(format, this.CultureInfo), this.DatePrefix, this.DateSuffix);
                return this.ApplyPost(text);
            }

            // 9. IEnumerable (кроме string)
            if (value is Collections.IEnumerable enumerable)
            {
                var items = new List<string>();

                foreach (var item in enumerable)
                {
                    items.Add(this.Format(item)); // рекурсивно
                }

                var separator = this.EnumerableSeparator ?? ", ";
                var joined = string.Join(separator, items);

                result = StringHelper.ApplyAffixes(joined, this.EnumerablePrefix, this.EnumerableSuffix);
                return this.ApplyPost(result);
            }

            // 10. Numeric
            if (Obj.NumberTypes.Contains(type))
            {
                var format = customTypeFormat ?? this.DecimalNumberFormat ?? "G";
                var text = (value as IFormattable)?.ToString(format, this.CultureInfo);

                if (this.TrimNumberZeroes && text?.Contains('.') == true)
                {
                    text = TrimZeros(text);
                }

                result = StringHelper.ApplyAffixes(text, this.NumberPrefix, this.NumberSuffix);
                return this.ApplyPost(result);
            }

            // 11. Fallback object
            result = StringHelper.ApplyAffixes(value.ToString(), this.ObjectPrefix, this.ObjectSuffix);
            return this.ApplyPost(result);
        }

        /// <summary>
        /// Пытается получить сериализатор для указанного типа.
        /// </summary>
        /// <param name="type">Тип объекта, для которого требуется сериализатор.</param>
        /// <param name="serializer">
        /// При успешном выполнении метода содержит делегат сериализации <see cref="Func{Object, ValueFormatter, String}"/>.
        /// В противном случае равен <c>null</c>.
        /// </param>
        /// <returns>
        /// <c>true</c>, если сериализатор найден; иначе <c>false</c>.
        /// </returns>
        /// <remarks>
        /// Метод сначала ищет сериализатор в кэше <see cref="serializerCache"/>.
        /// Если сериализатор не найден, перебирает список <see cref="Serializers"/> и проверяет условие <c>Condition</c>.
        /// Найденный сериализатор добавляется в кэш для последующего быстрого доступа.
        /// Если подходящий сериализатор не найден, в кэше сохраняется <c>null</c>.
        /// </remarks>
        public bool TryGetSerializer(Type type, out Func<object, ValueFormatter, string> serializer)
        {
            if (type == null)
            {
                serializer = null;
                return false;
            }

            if (this.serializerCache.TryGetValue(type, out serializer))
            {
                return serializer != null;
            }

            foreach (var (condition, serializer1) in this.Serializers)
            {
                if (condition == null || !condition(type))
                {
                    continue;
                }

                serializer = serializer1;
                this.serializerCache[type] = serializer;
                return true;
            }

            this.serializerCache[type] = null;
            return false;
        }

        private static string NormalizeWs(string input)
        {
            return string.IsNullOrEmpty(input) ? input : string.Join(" ", input.Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
        }

        private static string TrimZeros(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            // remove trailing zeros after decimal point
            text = text.TrimEnd('0');

            // remove trailing dot
            if (text.EndsWith("."))
            {
                text = text.Substring(0, text.Length - 1);
            }

            return text;
        }

        private string ApplyPost(string value)
        {
            if (this.PostFormatters != null)
            {
                foreach (var f in this.PostFormatters)
                {
                    value = f(value);
                }
            }

            return value;
        }
    }
}