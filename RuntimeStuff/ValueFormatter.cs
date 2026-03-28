namespace System
{
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;

    public class ValueFormatter
    {
        private string dateFormat = "{0:yyyy-MM-dd}";
        private string dateTimeFormat = "{0:yyyy-MM-dd HH:mm:ss}";
        private string decimalNumberFormat;
        private string timeFormat = "{0:HH:mm:ss}";

        public ValueFormatter()
        {
        }

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
            this.EnumerableSeperator = baseFormatter.EnumerableSeperator;
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

        public ValueFormatter(string dateFormat, bool enumAsString = false, bool trimSpaces = true, StringHelper.EscapeMode escapeMode = StringHelper.EscapeMode.None)
        {
            this.DateFormat = dateFormat;
            this.DateTimeFormat = dateFormat;
            this.EnumAsString = enumAsString;
            this.TrimSpaces = trimSpaces;
            this.EscapeMode = escapeMode;
        }

        public string BoolPrefix { get; set; }

        public string BoolSuffix { get; set; }

        public CultureInfo CultureInfo { get; set; } = CultureInfo.InvariantCulture;

        public Dictionary<Type, string> CustomTypeFormat { get; set; } = new Dictionary<Type, string>();

        public string DateFormat
        {
            get => this.dateFormat;
            set => this.dateFormat = StringHelper.NormalizeFormat(value);
        }

        public string DatePrefix { get; set; }

        public string DateSuffix { get; set; }

        public string DateTimeFormat
        {
            get => this.dateTimeFormat;
            set => this.dateTimeFormat = StringHelper.NormalizeFormat(value);
        }

        public string DecimalNumberFormat
        {
            get => this.decimalNumberFormat;
            set => this.decimalNumberFormat = StringHelper.NormalizeFormat(value);
        }

        public bool EnumAsString { get; set; }

        public string EnumerablePrefix { get; set; }

        public string EnumerableSeperator { get; set; }

        public string EnumerableSuffix { get; set; }

        public StringHelper.EscapeMode EscapeMode { get; set; } = StringHelper.EscapeMode.None;

        public string FalseValue { get; set; }

        public bool NormalizeWhitespaces { get; set; }

        public string NullPrefix { get; set; }

        public string NullSuffix { get; set; }

        public string NullValue { get; set; }

        private HashSet<object> nullValuesSet;
        public List<object> NullValues
        {
            get => this.nullValuesSet?.ToList();
            set => this.nullValuesSet = value != null ? new HashSet<object>(value) : null;
        }

        public string NumberPrefix { get; set; }

        public string NumberSuffix { get; set; }

        public string ObjectPrefix { get; set; }

        public string ObjectSuffix { get; set; }

        public List<Func<string, string>> PostFormatters { get; set; } = new List<Func<string, string>>();

        public List<(Func<Type, bool> Condition, Func<object, ValueFormatter, string> Serializer)> Serializers { get; set; } = new List<(Func<Type, bool> Condition, Func<object, ValueFormatter, string> Serializer)>();
        private readonly Dictionary<Type, Func<object, ValueFormatter, string>> serializerCache = new Dictionary<Type, Func<object, ValueFormatter, string>>();

        public string StringPrefix { get; set; }

        public string StringSuffix { get; set; }

        public string TimeFormat
        {
            get => this.timeFormat;
            set => this.timeFormat = StringHelper.NormalizeFormat(value);
        }

        public bool TrimNumberZeroes { get; set; }

        public bool TrimSpaces { get; set; }

        public string TrueValue { get; set; }

        public void AddSerializer(Func<Type, bool> typeCondition, Func<object, ValueFormatter, string> serializer)
        {
            this.Serializers.Add((typeCondition, serializer));
        }

        public string Format(object value)
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
            if (customTypeFormat != null)
            {
                customTypeFormat = StringHelper.NormalizeFormat(customTypeFormat);
            }

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

                result = StringHelper.ApplyAffixes(str, this.StringPrefix, this.StringSuffix);
                return this.ApplyPost(result);
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
                var enumText = this.EnumAsString ? StringHelper.ApplyAffixes(value.ToString(), this.StringPrefix, this.StringSuffix) : StringHelper.ApplyAffixes(Convert.ToInt64(value).ToString(this.CultureInfo), this.NullPrefix, this.NumberSuffix);
                return this.ApplyPost(enumText);
            }

            // 6. DateTime
            if (value is DateTime dt)
            {
                var format = customTypeFormat ?? (dt.HasTime() && !string.IsNullOrWhiteSpace(this.DateTimeFormat) ? this.DateTimeFormat : this.DateFormat ?? "{0:yyyy-MM-dd}");
                var text = string.Format(this.CultureInfo, format, dt);
                result = StringHelper.ApplyAffixes(text, this.DatePrefix, this.DateSuffix);
                return this.ApplyPost(result);
            }

            if (value is DateTimeOffset dto)
            {
                var format = customTypeFormat ?? (dto.TimeOfDay != TimeSpan.Zero && !string.IsNullOrWhiteSpace(this.DateTimeFormat) ? this.DateTimeFormat : this.DateFormat ?? "{0:yyyy-MM-dd HH:mm:ss}");
                var text = string.Format(this.CultureInfo, format, dto);
                result = StringHelper.ApplyAffixes(text, this.DatePrefix, this.DateSuffix);
                return this.ApplyPost(result);
            }

            // #if NET6_0_OR_GREATER
            //                        if (value is DateOnly d)
            //                        {
            //                            var format = DateFormat ?? "{0:yyyy-MM-dd}";
            //                            var text = string.Format(CultureInfo, format, d);
            //                            result = ApplyAffixes(text, DatePrefix, DateSuffix);
            //                            return ApplyPost(result);
            //                        }
            //                        if (value is TimeOnly tOnly)
            //                        {
            //                            var format = TimeFormat ?? "{0:HH:mm:ss}";
            //                            var text = string.Format(CultureInfo, format, tOnly);
            //                            return ApplyPost(text);
            //                        }
            // #endif
            if (value is TimeSpan ts)
            {
                var format = customTypeFormat ?? "{0:c}";
                var text = StringHelper.ApplyAffixes(string.Format(this.CultureInfo, format, ts), this.DatePrefix, this.DateSuffix);
                return this.ApplyPost(text);
            }

            // 9. IEnumerable (кроме string)
            if (value is System.Collections.IEnumerable enumerable)
            {
                var items = new List<string>();

                foreach (var item in enumerable)
                {
                    items.Add(this.Format(item)); // рекурсивно
                }

                var separator = this.EnumerableSeperator ?? ", ";
                var joined = string.Join(separator, items);

                result = StringHelper.ApplyAffixes(joined, this.EnumerablePrefix, this.EnumerableSuffix);
                return this.ApplyPost(result);
            }

            // 10. Numeric
            if (type.IsNumeric())
            {
                string text = string.Format(this.CultureInfo, customTypeFormat ?? this.DecimalNumberFormat ?? "{0}", value);

                if (this.TrimNumberZeroes && text.Contains('.'))
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

        public bool TryGetSerializer(Type type, out Func<object, ValueFormatter, string> serializer)
        {
            if (this.serializerCache.TryGetValue(type, out serializer))
            {
                return serializer != null;
            }

            for (int i = 0; i < this.Serializers.Count; i++)
            {
                var entry = this.Serializers[i];

                if (entry.Condition != null && entry.Condition(type))
                {
                    serializer = entry.Serializer;
                    this.serializerCache[type] = serializer;
                    return true;
                }
            }

            this.serializerCache[type] = null;
            serializer = null;
            return false;
        }

        private static string NormalizeWs(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            return string.Join(" ", input.Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
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