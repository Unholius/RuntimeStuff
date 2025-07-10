using RuntimeStuff.Extensions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RuntimeStuff
{
    public static partial class Converters
    {
        /// <summary>
        /// Универсальный конвертер строки в DateTime?, не зависящий от региональных настроек.
        /// Пытается распарсить дату из строки, используя набор фиксированных форматов. Если не получается, то пытается угадать формат.
        /// </summary>
        public static Converter<string, DateTime?> StringToDateTimeConverter = (s) =>
        {
            if (string.IsNullOrWhiteSpace(s))
                return null;

            string[] formats = new[]
            {
            "yyyy-MM-dd",
            "dd.MM.yyyy",
            "MM/dd/yyyy",
            "yyyy/MM/dd",
            "dd-MM-yyyy",
            "yyyyMMdd",
            "dd MMM yyyy",
            "dd MMMM yyyy",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-ddTHH:mm:ssZ",
            "yyyy-MM-ddTHH:mm:ss.fffZ",
            "o", // ISO 8601 Round-trip: 2024-06-30T14:57:32.0000000Z
            "s", // Sortable: 2024-06-30T14:57:32
        };

            DateTimeStyles styles = DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal;

            if (DateTime.TryParseExact(s.Trim(), formats, CultureInfo.InvariantCulture, styles, out var result))
                return result;

            // Попробуем общий парсинг с CultureInfo.InvariantCulture
            if (DateTime.TryParse(s.Trim(), CultureInfo.InvariantCulture, styles, out result))
                return result;

            // Пробуем угадать формат:

            DateTime d;
            if (DateTime.TryParse(s, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out d))
                return d;

            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out d))
                return d;

            var dateTimeParts = s.Split(new[] { ' ', 'T' }, StringSplitOptions.RemoveEmptyEntries);
            var dateParts = dateTimeParts[0].Split(new[] { '.', '\\', '/', '-' }, StringSplitOptions.RemoveEmptyEntries);
            var year = dateParts.IndexOf(x => x.Length == 4);
            var dayForSure = dateParts.IndexOf(x => x.Length <= 2 && x.ToInt() > 12 && x.ToInt() <= 31);
            var dayPossible = dateParts.IndexOf((x, i) => x.Length <= 2 && x.ToInt() > 0 && x.ToInt() <= 31 && i != dayForSure);
            var day = dayForSure >= 0 ? dayForSure : dayPossible;
            var month = dateParts.IndexOf((x, i) => x.Length <= 2 && x.ToInt() > 0 && x.ToInt() <= 12 && i != day);

            if (year >= 0 && month >= 0 && day >= 0 && year != month && month != day && day != year)
                return new DateTime((int)dateParts[year].ToInt(), (int)dateParts[month].ToInt(), (int)dateParts[day].ToInt());

            if (dateTimeParts[0].Length == 8)
                return new DateTime(Obj.ChangeType<int>(s.SubStr(0, 3)), Obj.ChangeType<int>(s.SubStr(4, 5)), Obj.ChangeType<int>(s.SubStr(6, 7)));

            return null;
        };
    }
}
