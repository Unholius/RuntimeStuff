// ***********************************************************************
// Assembly         : RuntimeStuff
// Author           : RS
// Created          : 11-19-2025
//
// Last Modified By : RS
// Last Modified On : 01-07-2026
// ***********************************************************************
// <copyright file="ValueFormatter.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************

namespace RuntimeStuff
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using RuntimeStuff.Helpers;

    /// <summary>
    /// Выполняет форматирование значений различных типов с учетом настроек культуры,
    /// пользовательских форматов, правил экранирования и обработки специальных случаев
    /// (null, bool, enum, числовые типы и т.д.).
    /// </summary>
    public class ValueFormatter
    {
        /// <summary>
        /// Инициализирует новый экземпляр <see cref="ValueFormatter"/>.
        /// </summary>
        public ValueFormatter()
        {
        }

        /// <summary>
        /// Пользовательские правила форматирования для конкретных типов.
        /// Ключ — тип значения, значение — делегат, возвращающий форматную строку
        /// и <see cref="CultureInfo"/> для форматирования.
        /// </summary>
        public Dictionary<Type, Func<object, (string Format, CultureInfo Culture)>> CustomTypeFormat { get; set; } = new Dictionary<Type, Func<object, (string Format, CultureInfo Culture)>>();

        /// <summary>
        /// Пользовательские функции обработки отформатированной строки. Выполняются последовательно в последнюю очередь.
        /// </summary>
        public List<Func<string, string>> CustomPostFormatters { get; set; } = new List<Func<string, string>>();

        /// <summary>
        /// Формат даты (<see cref="DateTime"/> без времени).
        /// По умолчанию: <c>{0:yyyy-MM-dd}</c>.
        /// </summary>
        public string DateFormat { get; set; } = "{0:yyyy-MM-dd}";

        /// <summary>
        /// Формат даты и времени (<see cref="DateTime"/>).
        /// По умолчанию: <c>{0:yyyy-MM-dd HH:mm:ss}</c>.
        /// </summary>
        public string DateTimeFormat { get; set; } = "{0:yyyy-MM-dd HH:mm:ss}";

        /// <summary>
        /// Формат чисел с плавающей точкой и десятичных значений.
        /// Если не задан, используется стандартное форматирование культуры.
        /// </summary>
        public string DecimalNumberFormat { get; set; }

        /// <summary>
        /// Культура, используемая по умолчанию для форматирования значений.
        /// По умолчанию: <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public CultureInfo DefaultCultureInfo { get; set; } = CultureInfo.InvariantCulture;

        /// <summary>
        /// Определяет, следует ли форматировать значения перечислений как строки (имена),
        /// а не как числовые значения.
        /// </summary>
        public bool EnumAsString { get; set; }

        /// <summary>
        /// Режим экранирования результирующей строки.
        /// </summary>
        public StringHelper.EscapeMode EscapeMode { get; set; } = StringHelper.EscapeMode.None;

        /// <summary>
        /// Строковое представление логического значения <c>false</c>.
        /// Если не задано, используется стандартное преобразование.
        /// </summary>
        public string FalseValue { get; set; }

        /// <summary>
        /// Префикс, добавляемый к значениям, не являющимся числовыми.
        /// </summary>
        public string NonNumberValuePrefix { get; set; }

        /// <summary>
        /// Суффикс, добавляемый к значениям, не являющимся числовыми.
        /// </summary>
        public string NonNumberValueSuffix { get; set; }

        /// <summary>
        /// Строковое представление значения <c>null</c>.
        /// </summary>
        public string NullValue { get; set; }

        /// <summary>
        /// Список значений, которые следует трактовать как <c>null</c>.
        /// </summary>
        public List<object> NullValues { get; set; }

        /// <summary>
        /// Формат времени (<see cref="TimeSpan"/> или временной части <see cref="DateTime"/>).
        /// По умолчанию: <c>{0:HH:mm:ss}</c>.
        /// </summary>
        public string TimeFormat { get; set; } = "{0:HH:mm:ss}";

        /// <summary>
        /// Указывает, следует ли удалять незначащие нули в конце дробной части числовых значений.
        /// </summary>
        public bool TrimNumberZeroes { get; set; }

        /// <summary>
        /// Удалять пробелы с концов отформатированной строки.
        /// </summary>
        public bool TrimTrailingSpaces { get; set; }

        /// <summary>
        /// Удалять лишние пробелы, заменять табуляцию и переносы строк пробелом из отформатированнолй строки.
        /// </summary>
        public bool NormalizeWhitespaces { get; set; }

        /// <summary>
        /// Строковое представление логического значения <c>true</c>.
        /// Если не задано, используется стандартное преобразование.
        /// </summary>
        public string TrueValue { get; set; }

        /// <summary>
        /// Форматирует переданное значение в строковое представление
        /// с учетом текущих настроек форматирования.
        /// </summary>
        /// <param name="value">
        /// Значение для форматирования.
        /// </param>
        /// <returns>
        /// Отформатированная строка с учетом культуры, пользовательских форматов,
        /// обработки <c>null</c>, логических значений, перечислений,
        /// числовых типов, а также применения экранирования и префикс/суффикс логики.
        /// </returns>
        /// <remarks>
        /// Поведение метода зависит от:
        /// <list type="bullet">
        /// <item><description>Наличия пользовательского формата в <see cref="CustomTypeFormat"/>.</description></item>
        /// <item><description>Настроек форматов даты и времени.</description></item>
        /// <item><description>Настроек числового форматирования.</description></item>
        /// <item><description>Правил обработки логических значений и перечислений.</description></item>
        /// <item><description>Применения <see cref="EscapeMode"/>.</description></item>
        /// </list>
        /// </remarks>
        /// <exception cref="FormatException">
        /// Может быть выброшено при некорректной форматной строке.
        /// </exception>
        public virtual string Format(object value)
        {
            // 1. Null handling
            if (value == null)
            {
                return this.Finalize(this.NullValue);
            }

            if (this.NullValues != null && this.NullValues.Contains(value))
            {
                return this.Finalize(this.NullValue);
            }

            var culture = this.DefaultCultureInfo ?? CultureInfo.InvariantCulture;

            var type = value.GetType();
            var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

            string result;

            // 2. Custom type formatter (максимальный приоритет)
            if (this.CustomTypeFormat != null)
            {
                foreach (var kv in this.CustomTypeFormat)
                {
                    if (kv.Key.IsAssignableFrom(underlyingType))
                    {
                        var (format, customCulture) = kv.Value(value);

                        result = string.Format(
                            customCulture ?? culture,
                            format,
                            value);

                        return this.Finalize(this.FinalizeNonNumeric(result, underlyingType));
                    }
                }
            }

            // 3. Boolean
            if (underlyingType == typeof(bool))
            {
                var boolValue = (bool)value;

                result = boolValue
                    ? (this.TrueValue ?? true.ToString().ToLower())
                    : (this.FalseValue ?? false.ToString().ToLower());

                return this.Finalize(this.FinalizeNonNumeric(result, underlyingType));
            }

            // 4. Enum
            if (underlyingType.IsEnum)
            {
                if (this.EnumAsString)
                {
                    result = value.ToString();
                }
                else
                {
                    var numericValue = Convert.ChangeType(
                        value,
                        Enum.GetUnderlyingType(underlyingType),
                        culture);

                    result = Convert.ToString(numericValue, culture);
                }

                return this.Finalize(this.FinalizeNonNumeric(result, underlyingType));
            }

            // 5. DateTime
            if (underlyingType == typeof(DateTime))
            {
                result = string.Format(
                    culture,
                    this.DateTimeFormat ?? "{0:G}",
                    value);

                return this.Finalize(this.FinalizeNonNumeric(result, underlyingType));
            }

#if NET6_0_OR_GREATER
    // 6. DateOnly
    if (underlyingType == typeof(DateOnly))
    {
        result = string.Format(
            culture,
            DateFormat ?? "{0:yyyy-MM-dd}",
            value);

        return this.Finalize(FinalizeNonNumeric(result, underlyingType));
    }
#endif

            // 7. TimeSpan
            if (underlyingType == typeof(TimeSpan))
            {
                result = string.Format(
                    culture,
                    this.TimeFormat ?? "{0:c}",
                    value);

                return this.Finalize(this.FinalizeNonNumeric(result, underlyingType));
            }

            // 8. Numeric
            if (IsNumericType(underlyingType))
            {
                if (!string.IsNullOrWhiteSpace(this.DecimalNumberFormat))
                {
                    result = string.Format(
                        culture,
                        this.DecimalNumberFormat,
                        value);
                }
                else
                {
                    result = Convert.ToString(value, culture);
                }

                if (this.TrimNumberZeroes && result != null)
                {
                    var sep = culture.NumberFormat.NumberDecimalSeparator;

                    if (result.Contains(sep))
                    {
                        result = result.TrimEnd('0');

                        if (result.EndsWith(sep))
                        {
                            result = result.Substring(0, result.Length - sep.Length);
                        }
                    }
                }

                return this.Finalize(result); // numeric не проходит через prefix/suffix/escape
            }

            // 9. Fallback
            result = Convert.ToString(value, culture);

            return this.Finalize(this.FinalizeNonNumeric(result, underlyingType));
        }

        private static bool IsNumericType(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;

#if NET5_0_OR_GREATER
    if (type == typeof(Half))
        return true;
#endif

            return type == typeof(byte)
                   || type == typeof(sbyte)
                   || type == typeof(short)
                   || type == typeof(ushort)
                   || type == typeof(int)
                   || type == typeof(uint)
                   || type == typeof(long)
                   || type == typeof(ulong)
                   || type == typeof(float)
                   || type == typeof(double)
                   || type == typeof(decimal);
        }

        private string ApplyPrefix(string value)
        {
            if (string.IsNullOrEmpty(this.NonNumberValuePrefix))
            {
                return value;
            }

            if (this.IsAlreadyPrefixed(value))
            {
                return value;
            }

            return this.NonNumberValuePrefix + value;
        }

        private string ApplySuffix(string value)
        {
            if (string.IsNullOrEmpty(this.NonNumberValueSuffix))
            {
                return value;
            }

            if (this.IsAlreadySuffixed(value))
            {
                return value;
            }

            return value + this.NonNumberValueSuffix;
        }

        private string FinalizeNonNumeric(string value, Type type)
        {
            if (value == null)
            {
                return null;
            }

            if (IsNumericType(type))
            {
                return value;
            }

            if (decimal.TryParse(value, out _))
            {
                return value;
            }

            // 1. Escape содержимого
            if (this.EscapeMode != StringHelper.EscapeMode.None)
            {
                value = StringHelper.EscapeString(value, this.EscapeMode);
            }

            // 2. Prefix
            value = this.ApplyPrefix(value);

            // 3. Suffix
            value = this.ApplySuffix(value);

            return value;
        }

        private string Finalize(string value)
        {
            if (this.NormalizeWhitespaces)
            {
                value = StringHelper.NormalizeWhiteSpaces(value);
            }
            else
            {
                if (this.TrimTrailingSpaces)
                {
                    value = StringHelper.TrimWhitespaces(value);
                }
            }

            return this.CustomPostFormatters == null ? value : this.CustomPostFormatters.Where(f => f != null).Aggregate(value, (current, f) => f(current));
        }

        private bool IsAlreadyPrefixed(string value)
        {
            return value.StartsWith(this.NonNumberValuePrefix, StringComparison.Ordinal);
        }

        private bool IsAlreadySuffixed(string value)
        {
            return value.EndsWith(this.NonNumberValueSuffix, StringComparison.Ordinal);
        }
    }
}