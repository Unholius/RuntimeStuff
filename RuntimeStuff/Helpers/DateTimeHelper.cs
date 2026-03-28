// ***********************************************************************
// Assembly         : RuntimeStuff
// Author           : RS
// Created          : 10-13-2025
//
// Last Modified By : RS
// Last Modified On : 01-07-2026
// ***********************************************************************
// <copyright file="DateTimeHelper.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************

namespace System.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;

    /// <summary>
    /// Предоставляет набор статических методов для работы с датами и временем, включая получение уникальных тиков, парсинг
    /// временных интервалов из строк, а также вычисление начала и конца дня, месяца и года.
    /// </summary>
    /// <remarks>Класс предназначен для упрощения типовых операций с датой и временем, таких как определение наличия
    /// компонента времени, преобразование строковых интервалов в TimeSpan, а также безопасное получение уникальных значений
    /// времени для последовательных вызовов. Все методы реализованы как статические и потокобезопасны, что позволяет
    /// использовать их без создания экземпляра класса.</remarks>
    public static class DateTimeHelper
    {
        private static readonly Random Rnd = new Random();

        /// <summary>
        /// The last time stamp.
        /// </summary>
        private static long lastTimeStamp = DateTime.Now.Ticks;

        static DateTimeHelper()
        {
            LocalUtcOffset = TimeZoneInfo.Local.GetUtcOffset(DateTime.Now);
        }

        /// <summary>
        /// Enum DateTimeInterval.
        /// </summary>
        public enum DateTimeInterval
        {
            /// <summary>
            /// The tick
            /// </summary>
            Tick,

            /// <summary>
            /// The millisecond
            /// </summary>
            Millisecond,

            /// <summary>
            /// The second
            /// </summary>
            Second,

            /// <summary>
            /// The minute
            /// </summary>
            Minute,

            /// <summary>
            /// The hour
            /// </summary>
            Hour,

            /// <summary>
            /// The day
            /// </summary>
            Day,

            /// <summary>
            /// The week
            /// </summary>
            Week,

            /// <summary>
            /// The month
            /// </summary>
            Month,

            /// <summary>
            /// The year
            /// </summary>
            Year,
        }

        /// <summary>
        /// Gets возвращает уникальные тики для текущего момента времени (гарантирует уникальность даже при быстрых последовательных
        /// вызовах).
        /// </summary>
        /// <value>The now ticks.</value>
        public static long NowTicks
        {
            get
            {
                long original, newValue;
                do
                {
                    original = Volatile.Read(ref lastTimeStamp);

                    var now = DateTime.UtcNow.Ticks + LocalUtcOffset.Ticks;
                    var next = original + 1;

                    newValue = now > next ? now : next;
                }
                while (Interlocked.CompareExchange(
                           ref lastTimeStamp, newValue, original) != original);

                return newValue;
            }
        }

        private static TimeSpan LocalUtcOffset { get; set; }

        /// <summary>
        /// Gets универсальный конвертер строки в DateTime?, не зависящий от региональных настроек.
        /// Пытается распарсить дату из строки, используя набор фиксированных форматов. Если не получается, то пытается угадать
        /// формат.
        /// </summary>
        private static Converter<string, DateTime?> StringToDateTimeConverter { get; } = s =>
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                return null;
            }

            if (DateTime.TryParse(s, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out var d))
            {
                return d;
            }

            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out d))
            {
                return d;
            }

            // Пробуем угадать формат:
            var dateTimeParts = s.Split(new[] { ' ', 'T' }, StringSplitOptions.RemoveEmptyEntries);
            var dateParts = dateTimeParts[0]
                .Split(new[] { '.', '\\', '/', '-' }, StringSplitOptions.RemoveEmptyEntries);
            var yearIndex = IndexOf(dateParts, (x, _) => x.Length == 4);
            var dayForSureIndex = IndexOf(dateParts, (x, _) =>
                x.Length <= 2 && (int)Convert.ChangeType(x, typeof(int)) > 12 &&
                (int)Convert.ChangeType(x, typeof(int)) <= 31);
            var dayPossibleIndex = IndexOf(dateParts, (x, i) =>
                x.Length <= 2 && (int)Convert.ChangeType(x, typeof(int)) > 0 &&
                (int)Convert.ChangeType(x, typeof(int)) <= 31 && i != dayForSureIndex);
            var dayIndex = dayForSureIndex >= 0 ? dayForSureIndex : dayPossibleIndex;
            var monthIndex = IndexOf(dateParts, (x, i) =>
                x.Length <= 2 && (int)Convert.ChangeType(x, typeof(int)) > 0 &&
                (int)Convert.ChangeType(x, typeof(int)) <= 12 && i != dayIndex);

            var year = yearIndex >= 0 && yearIndex < dateParts.Length
                ? Convert.ChangeType(dateParts[yearIndex], typeof(int))
                : null;
            var month = monthIndex >= 0 && monthIndex < dateParts.Length
                ? Convert.ChangeType(dateParts[monthIndex], typeof(int))
                : null;
            var day = dayIndex >= 0 && dayIndex < dateParts.Length
                ? Convert.ChangeType(dateParts[dayIndex], typeof(int))
                : null;

            if (year != null && month != null && day != null)
            {
                return new DateTime((int)year, (int)month, (int)day, 0, 0, 0, DateTimeKind.Unspecified);
            }

            if (dateTimeParts[0].Length == 8)
            {
                return new DateTime(
                    (int)Convert.ChangeType(s.Substring(0, 4), typeof(int)),
                    (int)Convert.ChangeType(s.Substring(4, 2), typeof(int)),
                    (int)Convert.ChangeType(s.Substring(6, 2), typeof(int)),
                    0,
                    0,
                    0,
                    DateTimeKind.Unspecified);
            }

            return null;
        };

        /// <summary>
        /// Добавляет к дате временной интервал, заданный строкой.
        /// </summary>
        /// <param name="date">Исходная дата.</param>
        /// <param name="timeSpan">Строка с временным интервалом <see cref="ParseTimeSpan(string)" />.</param>
        /// <returns>Новая дата после добавления интервала.</returns>
        public static DateTime Add(DateTime date, string timeSpan)
        {
            var timeIntervals = ParseTimeSpan(timeSpan);
            var d = date;
            foreach (var ti in timeIntervals)
            {
                d = d.Add(ti);
            }

            return d;
        }

        /// <summary>
        /// Возвращает начало дня (00:00:00) для указанной даты.
        /// </summary>
        /// <param name="dt">Исходная дата.</param>
        /// <param name="addDays">Добавить дни.</param>
        /// <returns>Дата с временем 00:00:00.</returns>
        public static DateTime BeginDay(DateTime dt, int addDays = 0) => new DateTime(dt.Year, dt.Month, dt.Day, 0, 0, 0, dt.Kind).AddDays(addDays);

        /// <summary>
        /// Возвращает начало дня (00:00:00) для nullable даты.
        /// </summary>
        /// <param name="date">Исходная nullable дата.</param>
        /// <param name="addDays">Добавить дни.</param>
        /// <returns>Дата с временем 00:00:00 или DateTime.MinValue если date равно null.</returns>
        public static DateTime BeginDay(DateTime? date, int addDays = 0)
        {
            if (date == null)
            {
                return DateTime.MinValue;
            }

            var dt = (DateTime)date;
            return new DateTime(dt.Year, dt.Month, dt.Day, 0, 0, 0, dt.Kind).AddDays(addDays);
        }

        /// <summary>
        /// Возвращает начало месяца (первый день, 00:00:00) для указанной даты.
        /// </summary>
        /// <param name="dt">Исходная дата.</param>
        /// <param name="addMonths">Добавить месяцы.</param>
        /// <returns>Дата с первым днем месяца и временем 00:00:00.</returns>
        public static DateTime BeginMonth(DateTime dt, int addMonths = 0) => new DateTime(dt.Year, dt.Month, 1, 0, 0, 0, dt.Kind).AddMonths(addMonths);

        /// <summary>
        /// Возвращает начало месяца (первый день, 00:00:00) для nullable даты.
        /// </summary>
        /// <param name="date">Исходная nullable дата.</param>
        /// <param name="addMonths">Добавить месяцы.</param>
        /// <returns>Дата с первым днем месяца и временем 00:00:00 или DateTime.MinValue если date равно null.</returns>
        public static DateTime BeginMonth(DateTime? date, int addMonths = 0)
        {
            if (date == null)
            {
                return DateTime.MinValue;
            }

            var dt = (DateTime)date;
            return new DateTime(dt.Year, dt.Month, 1, 0, 0, 0, dt.Kind).AddMonths(addMonths);
        }

        /// <summary>
        /// Возвращает начало года (первый день, 00:00:00) для указанной даты.
        /// </summary>
        /// <param name="dt">Исходная дата.</param>
        /// <param name="addYears">Добавить года.</param>
        /// <returns>Дата с первым днем года и временем 00:00:00.</returns>
        public static DateTime BeginYear(DateTime dt, int addYears = 0) => new DateTime(dt.Year, 1, 1, 0, 0, 0, dt.Kind).AddYears(addYears);

        /// <summary>
        /// Возвращает начало года (первый день, 00:00:00) для nullable даты.
        /// </summary>
        /// <param name="date">Исходная nullable дата.</param>
        /// <param name="addYears">Добавить года.</param>
        /// <returns>Дата с первым днем года и временем 00:00:00 или DateTime.MinValue если date равно null.</returns>
        public static DateTime BeginYear(DateTime? date, int addYears = 0)
        {
            if (date == null)
            {
                return DateTime.MinValue;
            }

            var dt = (DateTime)date;
            return new DateTime(dt.Year, 1, 1, 0, 0, 0, dt.Kind).AddYears(addYears);
        }

        /// <summary>
        /// Возвращает диапазон дат, соответствующий указанному дню.
        /// </summary>
        /// <param name="year">Год.</param>
        /// <param name="month">Месяц (от 1 до 12).</param>
        /// <param name="day">День месяца.</param>
        /// <returns>
        /// Объект <see cref="DateRange"/>, представляющий диапазон от начала до конца указанного дня.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Выбрасывается, если переданы некорректные значения даты.
        /// </exception>
        /// <remarks>
        /// Границы диапазона включают:
        /// <list type="bullet">
        /// <item><description>Начало дня — 00:00:00.000.</description></item>
        /// <item><description>Конец дня — 23:59:59.999 (определяется методом <c>EndDay</c>).</description></item>
        /// </list>
        /// </remarks>
        /// <example>
        /// <code>
        /// DayRange(2024, 5, 10)
        /// // Результат:
        /// // 2024-05-10 00:00:00.000 — 2024-05-10 23:59:59.999
        /// </code>
        /// </example>
        public static DateRange DayRange(int year, int month, int day) => new DateRange(BeginDay(new DateTime(year, month, day)), EndDay(new DateTime(year, month, day)));

        /// <summary>
        /// Возвращает последовательность дат с шагом в один день в заданном диапазоне (включительно).
        /// </summary>
        /// <param name="startDate">Начальная дата диапазона.</param>
        /// <param name="endDate">Конечная дата диапазона.</param>
        /// <returns>
        /// Последовательность объектов <see cref="DateTime"/>, представляющих каждую календарную дату
        /// от <paramref name="startDate"/> до <paramref name="endDate"/> включительно.
        /// </returns>
        /// <remarks>
        /// Время (часы, минуты, секунды) игнорируется — используется только дата (<see cref="DateTime.Date"/>).
        /// Если <paramref name="startDate"/> больше <paramref name="endDate"/>, последовательность будет пустой.
        /// </remarks>
        /// <example>
        /// <code>
        /// EachDay(new DateTime(2024, 1, 1), new DateTime(2024, 1, 3))
        /// // Результат:
        /// // 2024-01-01
        /// // 2024-01-02
        /// // 2024-01-03
        /// </code>
        /// </example>
        public static IEnumerable<DateTime> EachDay(DateTime startDate, DateTime endDate)
        {
            for (var day = startDate.Date; day.Date <= endDate.Date; day = day.AddDays(1))
            {
                yield return day;
            }
        }

        /// <summary>
        /// Возвращает конец дня (23:59:59.999) для указанной даты.
        /// </summary>
        /// <param name="dt">Исходная дата.</param>
        /// <param name="addDays">Добавить дни.</param>
        /// <returns>Дата с временем 23:59:59.999.</returns>
        public static DateTime EndDay(DateTime dt, int addDays = 0) => new DateTime(dt.Year, dt.Month, dt.Day, 23, 59, 59, 999, dt.Kind).AddDays(addDays);

        /// <summary>
        /// Возвращает конец дня (23:59:59.999) для nullable даты.
        /// </summary>
        /// <param name="date">Исходная nullable дата.</param>
        /// <param name="addDays">Добавить дни.</param>
        /// <returns>Дата с временем 23:59:59.999 или DateTime.MaxValue если date равно null.</returns>
        public static DateTime EndDay(DateTime? date, int addDays = 0)
        {
            if (date == null)
            {
                return DateTime.MaxValue;
            }

            var dt = (DateTime)date;
            return new DateTime(dt.Year, dt.Month, dt.Day, 23, 59, 59, 999, dt.Kind).AddDays(addDays);
        }

        /// <summary>
        /// Возвращает конец месяца (последний день, 23:59:59.999) для указанной даты.
        /// </summary>
        /// <param name="dt">Исходная дата.</param>
        /// <param name="addMonths">Добавить месяцы.</param>
        /// <returns>Дата с последним днем месяца и временем 23:59:59.999.</returns>
        public static DateTime EndMonth(DateTime dt, int addMonths = 0) => new DateTime(dt.Year, dt.Month, DateTime.DaysInMonth(dt.Year, dt.Month), 23, 59, 59, 999, dt.Kind).AddMonths(addMonths);

        /// <summary>
        /// Возвращает конец месяца (последний день, 23:59:59.999) для nullable даты.
        /// </summary>
        /// <param name="date">Исходная nullable дата.</param>
        /// <param name="addMonths">Добавить месяцы.</param>
        /// <returns>Дата с последним днем месяца и временем 23:59:59.999 или DateTime.MaxValue если date равно null.</returns>
        public static DateTime EndMonth(DateTime? date, int addMonths = 0)
        {
            if (date == null)
            {
                return DateTime.MaxValue;
            }

            var dt = (DateTime)date;
            return new DateTime(dt.Year, dt.Month, DateTime.DaysInMonth(dt.Year, dt.Month), 23, 59, 59, 999, dt.Kind).AddMonths(addMonths);
        }

        /// <summary>
        /// Возвращает конец года (последний день, 23:59:59.999) для указанной даты.
        /// </summary>
        /// <param name="dt">Исходная дата.</param>
        /// <param name="addYears">Добавить года.</param>
        /// <returns>Дата с последним днем года и временем 23:59:59.999.</returns>
        public static DateTime EndYear(DateTime dt, int addYears = 0) => new DateTime(dt.Year, 12, DateTime.DaysInMonth(dt.Year, 12), 23, 59, 59, 999, dt.Kind).AddYears(addYears);

        /// <summary>
        /// Возвращает конец года (последний день, 23:59:59.999) для nullable даты.
        /// </summary>
        /// <param name="date">Исходная nullable дата.</param>
        /// <param name="addYears">Добавить года.</param>
        /// <returns>Дата с последним днем года и временем 23:59:59.999 или DateTime.MaxValue если date равно null.</returns>
        public static DateTime EndYear(DateTime? date, int addYears = 0)
        {
            if (date == null)
            {
                return DateTime.MaxValue;
            }

            var dt = (DateTime)date;
            return new DateTime(dt.Year, 12, DateTime.DaysInMonth(dt.Year, 12), 23, 59, 59, 999, dt.Kind).AddYears(addYears);
        }

        /// <summary>
        /// Возвращает текущую дату и время с гарантией уникальности тиков.
        /// </summary>
        /// <returns>Текущая дата и время с уникальными тиками.</returns>
        public static DateTime ExactNow() => new DateTime(NowTicks, DateTimeKind.Local);

        /// <summary>
        /// Возвращает уникальные тики для текущего момента времени.
        /// </summary>
        /// <returns>Уникальные тики текущего момента времени.</returns>
        public static long ExactTicks() => NowTicks;

        /// <summary>
        /// Возвращает последовательность дат и времени в заданном диапазоне
        /// с произвольным шагом и интервалом, с возможностью фильтрации.
        /// </summary>
        /// <param name="startDate">
        /// Начальная дата диапазона (включительно).
        /// </param>
        /// <param name="endDate">
        /// Конечная дата диапазона (включительно).
        /// </param>
        /// <param name="step">
        /// Шаг изменения даты. Может быть положительным (движение вперёд)
        /// или отрицательным (движение назад), но не может быть равен нулю.
        /// </param>
        /// <param name="interval">
        /// Тип временного интервала, на который изменяется дата при каждом шаге
        /// (день, неделя, месяц, год и т.п.).
        /// </param>
        /// <param name="includeDate">
        /// Необязательная функция-фильтр, определяющая, должна ли текущая дата
        /// быть включена в результирующую последовательность.
        /// Параметры функции:
        /// <list type="bullet">
        /// <item><description>первый — текущая дата;</description></item>
        /// <item><description>второй — порядковый индекс шага (начиная с 0).</description></item>
        /// </list>
        /// Если функция возвращает <c>true</c>, дата включается.
        /// </param>
        /// <param name="excludeDate">
        /// Необязательная функция-фильтр, определяющая, должна ли текущая дата
        /// быть исключена из результирующей последовательности.
        /// Параметры функции аналогичны <paramref name="includeDate"/>.
        /// Если функция возвращает <c>true</c>, дата исключается.
        /// </param>
        /// <returns>
        /// Последовательность значений <see cref="DateTime"/>, удовлетворяющих
        /// заданному диапазону, шагу, интервалу и условиям фильтрации.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Выбрасывается, если <paramref name="step"/> равен нулю.
        /// </exception>
        /// <remarks>
        /// <para>
        /// Направление перебора определяется знаком параметра <paramref name="step"/>:
        /// положительное значение — от <paramref name="startDate"/> к <paramref name="endDate"/>,
        /// отрицательное — в обратном направлении.
        /// </para>
        /// <para>
        /// Если направление перебора не соответствует границам диапазона
        /// (например, положительный шаг при <paramref name="startDate"/> больше
        /// <paramref name="endDate"/>), метод возвращает пустую последовательность.
        /// </para>
        /// <para>
        /// Для перехода к следующей дате используется вспомогательный метод
        /// <c>AddInterval</c>, который инкапсулирует логику добавления временного интервала.
        /// </para>
        /// <para>
        /// Сначала применяется <paramref name="includeDate"/>, затем
        /// <paramref name="excludeDate"/>. Дата включается в результат
        /// только если она удовлетворяет обоим условиям.
        /// </para>
        /// </remarks>
        public static IEnumerable<DateTime> GetDates(
            DateTime startDate,
            DateTime endDate,
            int step = 1,
            DateTimeInterval interval = DateTimeInterval.Day,
            Func<DateTime, int, bool> includeDate = null,
            Func<DateTime, int, bool> excludeDate = null)
        {
            if (step == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(step), "Step cannot be zero.");
            }

            return GetDatesIterator(
                startDate,
                endDate,
                step,
                interval,
                includeDate,
                excludeDate);
        }

        /// <summary>
        /// Преобразует величину времени в виде числа <paramref name="elapsed" /> и интервала
        /// <paramref name="timeInterval" /> в объект <see cref="TimeSpan" />.
        /// </summary>
        /// <param name="elapsed">Величина времени в единицах, заданных <paramref name="timeInterval" />.</param>
        /// <param name="timeInterval">Единица измерения для <paramref name="elapsed" />.</param>
        /// <returns>Эквивалентное значение <see cref="TimeSpan" />.
        /// Для месяцев и лет используется приближённое вычисление:
        /// <list type="bullet"><item><description>1 месяц ? 30 дней</description></item><item><description>1 год ? 365 дней</description></item></list></returns>
        /// <exception cref="System.ArgumentOutOfRangeException">timeInterval.</exception>
        /// <example>
        ///   <code>
        /// // 2.5 часа
        /// TimeSpan ts1 = GetElapsedTime(2.5, DateTimeInterval.Hour);
        /// // 3 месяца ? 90 дней
        /// TimeSpan ts2 = GetElapsedTime(3, DateTimeInterval.Month);
        /// </code>
        /// </example>
        /// <remarks>Метод использует стандартные функции <see cref="TimeSpan.FromMilliseconds" />,
        /// <see cref="TimeSpan.FromSeconds" />, <see cref="TimeSpan.FromMinutes" />,
        /// <see cref="TimeSpan.FromHours" /> и <see cref="TimeSpan.FromDays" /> для преобразования.
        /// Для интервалов <see cref="DateTimeInterval.Month" /> и <see cref="DateTimeInterval.Year" />
        /// используется приближённое преобразование через дни (30 и 365 соответственно),
        /// поэтому результат является ориентировочным и не учитывает разные длины месяцев и високосные годы.</remarks>
        public static TimeSpan GetElapsedTime(double elapsed, DateTimeInterval timeInterval)
        {
            // конвертируем elapsed в TimeSpan
            TimeSpan ts;
            switch (timeInterval)
            {
                case DateTimeInterval.Millisecond:
                    ts = TimeSpan.FromMilliseconds(elapsed);
                    break;

                case DateTimeInterval.Second:
                    ts = TimeSpan.FromSeconds(elapsed);
                    break;

                case DateTimeInterval.Minute:
                    ts = TimeSpan.FromMinutes(elapsed);
                    break;

                case DateTimeInterval.Hour:
                    ts = TimeSpan.FromHours(elapsed);
                    break;

                case DateTimeInterval.Day:
                    ts = TimeSpan.FromDays(elapsed);
                    break;

                case DateTimeInterval.Week:
                    ts = TimeSpan.FromDays(elapsed * 7);
                    break;

                case DateTimeInterval.Month:
                    ts = TimeSpan.FromDays(elapsed * 30); // приближенно
                    break;

                case DateTimeInterval.Year:
                    ts = TimeSpan.FromDays(elapsed * 365); // приближенно
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(timeInterval));
            }

            return ts;
        }

        /// <summary>
        /// Преобразует величину времени в человекочитаемую строку с указанным форматом.
        /// </summary>
        /// <param name="elapsed">Величина времени в единицах, заданных <paramref name="timeInterval" />.</param>
        /// <param name="timeInterval">Единица измерения для <paramref name="elapsed" />.</param>
        /// <param name="format">Формат строки с токенами для замены на составные части времени.
        /// По умолчанию: <c>"[{Year} лет] [{Month} мес.] [{Day} дн.] [{Hour} час.] [{Minute} мин.] [{Second} с.]"</c>.
        /// Токены:
        /// <list type="bullet"><item><description>{Year} — количество лет;</description></item><item><description>{Month} — количество месяцев;</description></item><item><description>{Week} — количество недель;</description></item><item><description>{Day} — количество дней;</description></item><item><description>{Hour} — количество часов;</description></item><item><description>{Minute} — количество минут;</description></item><item><description>{Second} — количество секунд;</description></item><item><description>{Millisecond} — количество миллисекунд.</description></item></list>
        /// Части с нулевыми значениями автоматически удаляются вместе с квадратными скобками.</param>
        /// <returns>Человекочитаемая строка, представляющая временной промежуток,
        /// с пропуском нулевых единиц времени.</returns>
        /// <example>
        ///   <code>
        /// // 400 дней
        /// string s = GetElapsedTimeString(400, DateTimeInterval.Day);
        /// // Результат: "1 лет 1 мес. 5 дн."
        /// // 2 часа, 15 минут
        /// string s2 = GetElapsedTimeString(2.25, DateTimeInterval.Hour);
        /// // Результат: "2 час. 15 мин."
        /// </code>
        /// </example>
        /// <remarks>Метод использует:
        /// <list type="bullet"><item><description><see cref="GetElapsedTime(double, DateTimeInterval)" /> — для конвертации числа <paramref name="elapsed" /> в <see cref="TimeSpan" />;</description></item><item><description>вычисление лет, месяцев, недель и дней с приближением (1 год = 365 дней, 1 месяц = 30 дней, 1 неделя = 7 дней);</description></item><item><description>замену токенов в формате на соответствующие значения и удаление частей с нулевыми значениями.</description></item></list>
        /// Результат учитывает приближения для месяцев и лет, поэтому для точных вычислений по календарю следует использовать методы работы с <see cref="DateTime" />.</remarks>
        public static string GetElapsedTimeString(
            double elapsed,
            DateTimeInterval timeInterval,
            string format = "[{Year} лет] [{Month} мес.] [{Day} дн.] [{Hour} час.] [{Minute} мин.] [{Second} с.] [{Millisecond} мс.]")
        {
            var ts = GetElapsedTime(elapsed, timeInterval);

            // вычисляем составные части
            var totalDays = (int)ts.TotalDays;
            var years = totalDays / 365;
            var months = (totalDays % 365) / 30;
            var days = totalDays - (years * 365) - (months * 30);
            var hours = ts.Hours;
            var minutes = ts.Minutes;
            var seconds = ts.Seconds;
            var ms = ts.Milliseconds;

            // заменяем токены, удаляя нулевые части вместе с квадратными скобками
            var result = format;
            var maskPrefix = "\\[[^\\]]*?{";
            var maskSuffix = "}[^\\]]*?\\]";

            result = years > 0 ? result.Replace($"{nameof(DateTimeInterval.Year)}", $"{years}") : Regex.Replace(result, maskPrefix + nameof(DateTimeInterval.Year) + maskSuffix, string.Empty);
            result = months > 0 ? result.Replace($"{nameof(DateTimeInterval.Month)}", $"{months}") : Regex.Replace(result, maskPrefix + nameof(DateTimeInterval.Month) + maskSuffix, string.Empty);
            var weeks = ((totalDays % 365) % 30) / 7;
            result = weeks > 0 ? result.Replace($"{nameof(DateTimeInterval.Week)}", $"{weeks}") : Regex.Replace(result, maskPrefix + nameof(DateTimeInterval.Week) + maskSuffix, string.Empty);
            result = days > 0 ? result.Replace($"{nameof(DateTimeInterval.Day)}", $"{days}") : Regex.Replace(result, maskPrefix + nameof(DateTimeInterval.Day) + maskSuffix, string.Empty);
            result = hours > 0 ? result.Replace($"{nameof(DateTimeInterval.Hour)}", $"{hours}") : Regex.Replace(result, maskPrefix + nameof(DateTimeInterval.Hour) + maskSuffix, string.Empty);
            result = minutes > 0 ? result.Replace($"{nameof(DateTimeInterval.Minute)}", $"{minutes}") : Regex.Replace(result, maskPrefix + nameof(DateTimeInterval.Minute) + maskSuffix, string.Empty);
            result = seconds > 0 ? result.Replace($"{nameof(DateTimeInterval.Second)}", $"{seconds}") : Regex.Replace(result, maskPrefix + nameof(DateTimeInterval.Second) + maskSuffix, string.Empty);
            result = ms > 0 ? result.Replace($"{nameof(DateTimeInterval.Millisecond)}", $"{ms}") : Regex.Replace(result, maskPrefix + nameof(DateTimeInterval.Millisecond) + maskSuffix, string.Empty);

            result = result.Replace("[", string.Empty).Replace("]", string.Empty).Replace("{", string.Empty).Replace("}", string.Empty);

            return result.Trim();
        }

        /// <summary>
        /// Возвращает полный временной период, охватывающий все переданные даты,
        /// с приведением начала к началу дня и конца — к концу дня.
        /// </summary>
        /// <param name="dates">Набор значений <see cref="DateTime" />, для которых необходимо определить общий период.</param>
        /// <returns>Кортеж, содержащий:
        /// <list type="bullet"><item><description><c>From</c> — начало дня минимальной даты;</description></item><item><description><c>To</c> — конец дня максимальной даты.</description></item></list></returns>
        /// <exception cref="ArgumentNullException">Выбрасывается, если <paramref name="dates" /> равен <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Выбрасывается, если <paramref name="dates" /> не содержит ни одного элемента.</exception>
        /// <remarks>Метод использует функции <see cref="Min(DateTime[])" /> и <see cref="Max(DateTime[])" />,
        /// а также вспомогательные методы <c>BeginDay</c> и <c>EndDay</c>,
        /// которые приводят дату ко времени 00:00:00 и 23:59:59.999… соответственно.</remarks>
        public static (DateTime From, DateTime to) GetFullPeriod(params DateTime[] dates) => (BeginDay(Min(dates)), EndDay(Max(dates)));

        /// <summary>
        /// Возвращает временной период, охватывающий все переданные даты,
        /// без изменения времени начала и окончания.
        /// </summary>
        /// <param name="dates">Набор значений <see cref="DateTime" />, для которых необходимо определить период.</param>
        /// <returns>Кортеж, содержащий:
        /// <list type="bullet"><item><description><c>From</c> — минимальное значение из набора дат;</description></item><item><description><c>To</c> — максимальное значение из набора дат.</description></item></list></returns>
        /// <exception cref="ArgumentNullException">Выбрасывается, если <paramref name="dates" /> равен <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Выбрасывается, если <paramref name="dates" /> не содержит ни одного элемента.</exception>
        /// <remarks>В отличие от <see cref="GetFullPeriod(DateTime[])" />, метод не выполняет
        /// нормализацию времени и возвращает фактические минимальное и максимальное значения.</remarks>
        public static (DateTime From, DateTime to) GetPeriod(params DateTime[] dates) => (Min(dates), Max(dates));

        /// <summary>
        /// Проверяет, содержит ли DateTime компонент времени (не равно 00:00:00).
        /// </summary>
        /// <param name="d">Проверяемая дата.</param>
        /// <returns>True, если время не равно 00:00:00.</returns>
        public static bool HasTime(DateTime d) => d.TimeOfDay != TimeSpan.Zero;

        /// <summary>
        /// Проверяет, содержит ли nullable DateTime компонент времени (не равно 00:00:00).
        /// </summary>
        /// <param name="d">Проверяемая nullable дата.</param>
        /// <returns>True, если дата не null и время не равно 00:00:00.</returns>
        public static bool HasTime(DateTime? d) => d.HasValue && d.Value.TimeOfDay != TimeSpan.Zero;

        /// <summary>
        /// Возвращает максимальное значение <see cref="DateTime" /> из переданного набора.
        /// </summary>
        /// <param name="dates">Набор значений <see cref="DateTime" />, среди которых необходимо определить максимальное.</param>
        /// <returns>Максимальное значение <see cref="DateTime" /> из массива <paramref name="dates" />.</returns>
        /// <exception cref="System.ArgumentNullException">dates.</exception>
        /// <exception cref="System.ArgumentException">At least one DateTime value is required. - dates.</exception>
        /// <remarks>Сравнение выполняется с использованием стандартных операторов сравнения
        /// <see cref="DateTime" />, учитывающих тики и тип времени (<see cref="DateTime.Kind" />).</remarks>
        public static DateTime Max(params DateTime[] dates)
        {
            if (dates == null)
            {
                throw new ArgumentNullException(nameof(dates));
            }

            if (dates.Length == 0)
            {
                throw new ArgumentException("At least one DateTime value is required.", nameof(dates));
            }

            var max = dates[0];

            for (var i = 1; i < dates.Length; i++)
            {
                if (dates[i] > max)
                {
                    max = dates[i];
                }
            }

            return max;
        }

        /// <summary>
        /// Возвращает минимальное значение <see cref="DateTime" /> из переданного набора.
        /// </summary>
        /// <param name="dates">Набор значений <see cref="DateTime" />, среди которых необходимо определить минимальное.</param>
        /// <returns>Минимальное значение <see cref="DateTime" /> из массива <paramref name="dates" />.</returns>
        /// <exception cref="System.ArgumentNullException">dates.</exception>
        /// <exception cref="System.ArgumentException">At least one DateTime value is required. - dates.</exception>
        /// <remarks>Сравнение выполняется с использованием стандартных операторов сравнения
        /// <see cref="DateTime" />, учитывающих тики и тип времени (<see cref="DateTime.Kind" />).</remarks>
        public static DateTime Min(params DateTime[] dates)
        {
            if (dates == null)
            {
                throw new ArgumentNullException(nameof(dates));
            }

            if (dates.Length == 0)
            {
                throw new ArgumentException("At least one DateTime value is required.", nameof(dates));
            }

            var min = dates[0];

            for (var i = 1; i < dates.Length; i++)
            {
                if (dates[i] < min)
                {
                    min = dates[i];
                }
            }

            return min;
        }

        /// <summary>
        /// Возвращает диапазон дат для указанного месяца текущего года.
        /// </summary>
        /// <param name="month">Месяц (от 1 до 12).</param>
        /// <returns>
        /// Объект <see cref="DateRange"/>, представляющий диапазон от начала до конца указанного месяца.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Выбрасывается, если <paramref name="month"/> не входит в диапазон от 1 до 12.
        /// </exception>
        /// <remarks>
        /// Диапазон включает:
        /// <list type="bullet">
        /// <item><description>Начало месяца — первый день месяца (00:00:00.000), определяется методом <c>BeginMonth</c>.</description></item>
        /// <item><description>Конец месяца — последний день месяца (23:59:59.999), определяется методом <c>EndMonth</c>.</description></item>
        /// </list>
        /// В качестве года используется <see cref="DateTime.Now"/>.
        /// </remarks>
        /// <example>
        /// <code>
        /// MonthRange(5)
        /// // Например, если текущий год 2024:
        /// // 2024-05-01 00:00:00.000 — 2024-05-31 23:59:59.999
        /// </code>
        /// </example>
        public static DateRange MonthRange(int month) => new DateRange(BeginMonth(new DateTime(DateTime.Now.Year, month, 1)), EndMonth(new DateTime(DateTime.Now.Year, month, 1)));

        /// <summary>
        /// Возвращает диапазон дат для текущего месяца.
        /// </summary>
        /// <returns>
        /// Объект <see cref="DateRange"/>, представляющий диапазон от начала до конца текущего месяца.
        /// </returns>
        /// <remarks>
        /// Диапазон включает:
        /// <list type="bullet">
        /// <item><description>Начало месяца — первый день текущего месяца (00:00:00.000), определяется методом <c>BeginMonth</c>.</description></item>
        /// <item><description>Конец месяца — последний день текущего месяца (23:59:59.999), определяется методом <c>EndMonth</c>.</description></item>
        /// </list>
        /// В качестве месяца и года используется <see cref="DateTime.Now"/>.
        /// </remarks>
        /// <example>
        /// <code>
        /// MonthRange()
        /// // Например, если текущий месяц май 2024:
        /// // 2024-05-01 00:00:00.000 — 2024-05-31 23:59:59.999
        /// </code>
        /// </example>
        public static DateRange MonthRange() => new DateRange(BeginMonth(DateTime.Now), EndMonth(DateTime.Now));

        /// <summary>
        /// Возвращает диапазон дат для указанного месяца указанного года.
        /// </summary>
        /// <param name="year">Год.</param>
        /// <param name="month">Месяц (от 1 до 12).</param>
        /// <returns>
        /// Объект <see cref="DateRange"/>, представляющий диапазон от начала до конца указанного месяца.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Выбрасывается, если <paramref name="month"/> не входит в диапазон от 1 до 12
        /// или если <paramref name="year"/> находится вне допустимого диапазона <see cref="DateTime"/>.
        /// </exception>
        /// <remarks>
        /// Диапазон включает:
        /// <list type="bullet">
        /// <item><description>Начало месяца — первый день месяца (00:00:00.000).</description></item>
        /// <item><description>Конец месяца — последний день месяца (23:59:59.999), вычисляется с помощью <see cref="DateTime.DaysInMonth"/>.</description></item>
        /// </list>
        /// </remarks>
        /// <example>
        /// <code>
        /// MonthRange(2024, 5)
        /// // Результат:
        /// // 2024-05-01 00:00:00.000 — 2024-05-31 23:59:59.999
        /// </code>
        /// </example>
        public static DateRange MonthRange(int year, int month) => new DateRange(new DateTime(year, month, 1), new DateTime(year, month, DateTime.DaysInMonth(year, month), 23, 59, 59, 999));

        /// <summary>
        /// Преобразует строковое представление даты и времени в значение <see cref="DateTime" />.
        /// </summary>
        /// <param name="dateTimeString">Строковое представление даты и времени, подлежащее преобразованию.</param>
        /// <returns>Значение <see cref="DateTime" />, полученное в результате преобразования,
        /// либо <c>null</c>, если строка не может быть интерпретирована как дата и время.</returns>
        /// <remarks>Метод использует внутренний конвертер <c>StringToDateTimeConverter</c>,
        /// который инкапсулирует логику разбора строки и обработки ошибок.
        /// В отличие от стандартных методов <see cref="DateTime.Parse(string)" /> и
        /// <see cref="DateTime.TryParse(string, out DateTime)" />,
        /// данный метод не выбрасывает исключения при некорректном формате входных данных.</remarks>
        public static DateTime? ParseDate(string dateTimeString) => StringToDateTimeConverter(dateTimeString);

        /// <summary>
        /// Парсит строку в массив TimeSpan. Пример: "1d -12m +3M -100s +6y".
        /// </summary>
        /// <param name="s">Строка для парсинга. Поддерживаемые форматы:
        /// Yy - год (365 дней), M - месяц (30 дней), Ww - недели, Dd - день,
        /// Hh - час, m - минуты, Ss - секунды, Ff - миллисекунды.</param>
        /// <returns>Массив TimeSpan.</returns>
        public static TimeSpan[] ParseTimeSpan(string s)
        {
            var result = new List<TimeSpan>();
            s = Regex.Replace(s, "[^0-9dDmMsSyYwWhfF\\-\\+]", string.Empty);
            var matches = Regex.Matches(s, "[+-]?\\d*[dDmMsSyYwWhfF]");
            foreach (Match m in matches)
            {
                var n = int.Parse(m.Value.Substring(0, m.Value.Length - 1));
                switch (m.Value.Last())
                {
                    case 'y':
                    case 'Y':
                        result.Add(TimeSpan.FromDays(365 * n));
                        break;

                    case 'M':
                        result.Add(TimeSpan.FromDays(30 * n));
                        break;

                    case 'W':
                    case 'w':
                        result.Add(TimeSpan.FromDays(7 * n));
                        break;

                    case 'd':
                    case 'D':
                        result.Add(TimeSpan.FromDays(n));
                        break;

                    case 'h':
                    case 'H':
                        result.Add(TimeSpan.FromHours(n));
                        break;

                    case 'm':
                        result.Add(TimeSpan.FromMinutes(n));
                        break;

                    case 's':
                    case 'S':
                        result.Add(TimeSpan.FromSeconds(n));
                        break;

                    case 'f':
                    case 'F':
                        result.Add(TimeSpan.FromMilliseconds(n));
                        break;
                }
            }

            return result.ToArray();
        }

        /// <summary>
        /// Возвращает диапазон дат для указанного квартала текущего года.
        /// </summary>
        /// <param name="number">Номер квартала (от 1 до 4).</param>
        /// <returns>
        /// Объект <see cref="DateRange"/>, представляющий диапазон дат квартала.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Выбрасывается, если <paramref name="number"/> не входит в диапазон от 1 до 4.
        /// </exception>
        /// <remarks>
        /// Границы диапазона включают:
        /// <list type="bullet">
        /// <item><description>Начало — первый день квартала (00:00:00.000).</description></item>
        /// <item><description>Конец — последний день квартала (23:59:59.999).</description></item>
        /// </list>
        /// Используется текущий год (<see cref="DateTime.Now"/>).
        /// </remarks>
        public static DateRange QuarterRange(int number) => QuarterRange(DateTime.Now.Year, number);

        /// <summary>
        /// Возвращает диапазон дат для указанного квартала заданного года.
        /// </summary>
        /// <param name="year">Год, для которого определяется квартал.</param>
        /// <param name="number">Номер квартала (от 1 до 4).</param>
        /// <returns>
        /// Объект <see cref="DateRange"/>, представляющий диапазон дат квартала.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Выбрасывается, если <paramref name="number"/> не входит в диапазон от 1 до 4.
        /// </exception>
        /// <remarks>
        /// Границы диапазона включают:
        /// <list type="bullet">
        /// <item><description>Начало — первый день квартала (00:00:00.000).</description></item>
        /// <item><description>Конец — последний день квартала (23:59:59.999).</description></item>
        /// </list>
        /// </remarks>
        /// <example>
        /// <code>
        /// QuarterRange(2024, 2)
        /// // Результат:
        /// // 2024-04-01 00:00:00.000 — 2024-06-30 23:59:59.999
        /// </code>
        /// </example>
        public static DateRange QuarterRange(int year, int number)
        {
            switch (number)
            {
                case 1: return new DateRange(new DateTime(year, 1, 1), new DateTime(year, 3, 31, 23, 59, 59, 999));
                case 2: return new DateRange(new DateTime(year, 4, 1), new DateTime(year, 6, 30, 23, 59, 59, 999));
                case 3: return new DateRange(new DateTime(year, 7, 1), new DateTime(year, 9, 30, 23, 59, 59, 999));
                case 4: return new DateRange(new DateTime(year, 10, 1), new DateTime(year, 12, 31, 23, 59, 59, 999));
                default:
                    throw new ArgumentOutOfRangeException(nameof(number), "Quarter number must be between 1 and 4.");
            }
        }

        /// <summary>
        /// Генерирует случайное значение <see cref="DateTime"/> в заданном диапазоне дат и (опционально) времени.
        /// </summary>
        /// <param name="fromDate">
        /// Начальная дата диапазона. Если больше <paramref name="toDate"/>, значения будут автоматически поменяны местами.
        /// Учитывается только дата (время отбрасывается).
        /// </param>
        /// <param name="toDate">
        /// Конечная дата диапазона. Если меньше <paramref name="fromDate"/>, значения будут автоматически поменяны местами.
        /// Учитывается только дата (время отбрасывается).
        /// </param>
        /// <param name="fromTime">
        /// (Необязательно) Начальное время суток. Если не задано, возвращается только случайная дата без времени.
        /// </param>
        /// <param name="toTime">
        /// (Необязательно) Конечное время суток. Если не задано, возвращается только случайная дата без времени.
        /// </param>
        /// <returns>
        /// Случайное значение <see cref="DateTime"/>:
        /// <list type="bullet">
        /// <item>
        /// <description>Если <paramref name="fromTime"/> и <paramref name="toTime"/> не заданы — случайная дата в диапазоне.</description>
        /// </item>
        /// <item>
        /// <description>Если время задано — случайная дата и время в пределах указанных диапазонов.</description>
        /// </item>
        /// </list>
        /// </returns>
        /// <remarks>
        /// <para>
        /// Диапазон дат включает обе границы.
        /// </para>
        /// <para>
        /// Диапазон времени также включает обе границы и рассчитывается с точностью до миллисекунд.
        /// </para>
        /// <para>
        /// Если начальные значения больше конечных (как для дат, так и для времени),
        /// они автоматически меняются местами.
        /// </para>
        /// </remarks>
        public static DateTime Random(
            DateTime fromDate,
            DateTime toDate,
            TimeSpan? fromTime = null,
            TimeSpan? toTime = null)
        {
            if (fromDate > toDate)
            {
                (fromDate, toDate) = (toDate, fromDate);
            }

            fromDate = fromDate.Date;
            toDate = toDate.Date;

            // Случайный день
            int dayRange = (toDate - fromDate).Days + 1;
            int randDays = Rnd.Next(dayRange);
            var date = fromDate.AddDays(randDays);

            // Если время не задано — возвращаем только дату
            if (fromTime == null || toTime == null)
            {
                return date;
            }

            var startTime = fromTime.Value;
            var endTime = toTime.Value;

            if (startTime > endTime)
            {
                (startTime, endTime) = (endTime, startTime);
            }

            // Случайное время (до миллисекунд)
            long timeRange = (long)(endTime - startTime).TotalMilliseconds;
            long randMs = (long)(Rnd.NextDouble() * (timeRange + 1));

            var time = startTime.Add(TimeSpan.FromMilliseconds(randMs));

            return date + time;
        }

        /// <summary>
        /// Возвращает диапазон дат для текущего дня.
        /// </summary>
        /// <returns>
        /// Объект <see cref="DateRange"/>, представляющий диапазон от начала до конца текущего дня.
        /// </returns>
        /// <remarks>
        /// Границы диапазона включают:
        /// <list type="bullet">
        /// <item><description>Начало дня — 00:00:00.000.</description></item>
        /// <item><description>Конец дня — 23:59:59.999 (определяется методом <c>EndDay</c>).</description></item>
        /// </list>
        /// В качестве текущей даты используется <see cref="DateTime.Now"/>.
        /// </remarks>
        /// <example>
        /// <code>
        /// TodayRange()
        /// // Результат:
        /// // [сегодня] 00:00:00.000 — [сегодня] 23:59:59.999
        /// </code>
        /// </example>
        public static DateRange TodayRange() => new DateRange(BeginDay(DateTime.Now), EndDay(DateTime.Now));

        /// <summary>
        /// Возвращает завтрашнюю дату (начало дня).
        /// </summary>
        /// <param name="date">Исходная дата.</param>
        /// <returns>Дата завтрашнего дня с временем 00:00:00.</returns>
        public static DateTime Tomorrow(DateTime date) => BeginDay(date, 1);

        /// <summary>
        /// Возвращает завтрашнюю дату (начало дня) для nullable даты.
        /// </summary>
        /// <param name="date">Исходная nullable дата.</param>
        /// <returns>Дата завтрашнего дня с временем 00:00:00.</returns>
        public static DateTime Tomorrow(DateTime? date) => date != null
            ? BeginDay(date, 1)
            : throw new ArgumentNullException(nameof(date));

        /// <summary>
        /// Возвращает диапазон дат за последние 7 дней, включая текущий день.
        /// </summary>
        /// <returns>
        /// Объект <see cref="DateRange"/>, представляющий диапазон от начала дня,
        /// который был 6 дней назад, до конца текущего дня.
        /// </returns>
        /// <remarks>
        /// Диапазон включает:
        /// <list type="bullet">
        /// <item><description>Начало — 00:00:00.000 дня, отстоящего на 6 дней назад от текущего.</description></item>
        /// <item><description>Конец — 23:59:59.999 текущего дня (определяется методом <c>EndDay</c>).</description></item>
        /// </list>
        /// Таким образом, общий диапазон составляет 7 календарных дней (включительно).
        /// В качестве текущей даты используется <see cref="DateTime.Now"/>.
        /// </remarks>
        /// <example>
        /// <code>
        /// WeekRange()
        /// // Например, если сегодня 10 мая:
        /// // 2024-05-04 00:00:00.000 — 2024-05-10 23:59:59.999
        /// </code>
        /// </example>
        public static DateRange WeekRange() => new DateRange(BeginDay(DateTime.Now, -6), EndDay(DateTime.Now));

        /// <summary>
        /// Возвращает диапазон дат для указанной недели года (отсчёт ведётся от 1 января).
        /// </summary>
        /// <param name="year">Год.</param>
        /// <param name="weekNumber">Номер недели (начиная с 1).</param>
        /// <returns>
        /// Объект <see cref="DateRange"/>, представляющий диапазон из 7 дней для указанной недели.
        /// </returns>
        /// <remarks>
        /// Недели рассчитываются как последовательные интервалы по 7 дней,
        /// начиная с 1 января указанного года.
        /// <para/>
        /// Важно: данный метод <b>не соответствует ISO 8601</b> (где неделя начинается с понедельника
        /// и первая неделя определяется иначе).
        /// <para/>
        /// Границы диапазона включают:
        /// <list type="bullet">
        /// <item><description>Начало — 00:00:00.000 первого дня недели.</description></item>
        /// <item><description>Конец — 23:59:59.999 седьмого дня недели (определяется методом <c>EndDay</c>).</description></item>
        /// </list>
        /// </remarks>
        /// <example>
        /// <code>
        /// WeekRange(2024, 1)
        /// // Результат:
        /// // 2024-01-01 — 2024-01-07
        ///
        /// WeekRange(2024, 2)
        /// // Результат:
        /// // 2024-01-08 — 2024-01-14
        /// </code>
        /// </example>
        public static DateRange WeekRange(int year, int weekNumber) => new DateRange(BeginDay(new DateTime(year, 1, 1).AddDays((weekNumber - 1) * 7)), EndDay(new DateTime(year, 1, 1).AddDays(((weekNumber - 1) * 7) + 6)));

        /// <summary>
        /// Возвращает диапазон дат для текущего года.
        /// </summary>
        /// <returns>
        /// Объект <see cref="DateRange"/>, представляющий диапазон от начала до конца текущего года.
        /// </returns>
        /// <remarks>
        /// Диапазон включает:
        /// <list type="bullet">
        /// <item><description>Начало — 1 января текущего года (00:00:00.000).</description></item>
        /// <item><description>Конец — 31 декабря текущего года (23:59:59.999).</description></item>
        /// </list>
        /// В качестве текущего года используется <see cref="DateTime.Now"/>.
        /// </remarks>
        /// <example>
        /// <code>
        /// YearRange()
        /// // Результат:
        /// // 2024-01-01 00:00:00.000 — 2024-12-31 23:59:59.999
        /// </code>
        /// </example>
        public static DateRange YearRange() => YearRange(DateTime.Now.Year);

        /// <summary>
        /// Возвращает диапазон дат для указанного года.
        /// </summary>
        /// <param name="year">Год.</param>
        /// <returns>
        /// Объект <see cref="DateRange"/>, представляющий диапазон от начала до конца указанного года.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Выбрасывается, если значение <paramref name="year"/> находится вне допустимого диапазона для <see cref="DateTime"/>.
        /// </exception>
        /// <remarks>
        /// Диапазон включает:
        /// <list type="bullet">
        /// <item><description>Начало — 1 января указанного года (00:00:00.000).</description></item>
        /// <item><description>Конец — 31 декабря указанного года (23:59:59.999).</description></item>
        /// </list>
        /// </remarks>
        /// <example>
        /// <code>
        /// YearRange(2024)
        /// // Результат:
        /// // 2024-01-01 00:00:00.000 — 2024-12-31 23:59:59.999
        /// </code>
        /// </example>
        public static DateRange YearRange(int year) => new DateRange(new DateTime(year, 1, 1), new DateTime(year, 12, 31, 23, 59, 59, 999));

        /// <summary>
        /// Возвращает вчерашнюю дату (начало дня).
        /// </summary>
        /// <param name="date">Исходная дата.</param>
        /// <returns>Дата предыдущего дня с временем 00:00:00.</returns>
        public static DateTime Yesterday(DateTime date) => BeginDay(date, -1);

        /// <summary>
        /// Возвращает вчерашнюю дату (начало дня) для nullable даты.
        /// </summary>
        /// <param name="date">Исходная nullable дата.</param>
        /// <returns>Дата предыдущего дня с временем 00:00:00.</returns>
        public static DateTime Yesterday(DateTime? date) => date != null
                ? BeginDay(date, -1)
                : throw new ArgumentNullException(nameof(date));

        /// <summary>
        /// Возвращает дату и время, смещённые на заданный шаг по указанному интервалу.
        /// </summary>
        /// <param name="value">Исходная дата и время.</param>
        /// <param name="step">Значение смещения. Может быть положительным (движение вперёд) или отрицательным (движение назад).</param>
        /// <param name="interval">Тип временного интервала, на который необходимо сместить дату.</param>
        /// <returns>Новое значение <see cref="DateTime" />, полученное после смещения на <paramref name="step" />
        /// единиц <paramref name="interval" /> от исходной даты <paramref name="value" />.</returns>
        /// <exception cref="System.ArgumentOutOfRangeException">interval - null.</exception>
        /// <remarks>Поддерживаются следующие интервалы:
        /// <list type="bullet"><item><description><see cref="DateTimeInterval.Millisecond" /> — миллисекунды;</description></item><item><description><see cref="DateTimeInterval.Second" /> — секунды;</description></item><item><description><see cref="DateTimeInterval.Minute" /> — минуты;</description></item><item><description><see cref="DateTimeInterval.Hour" /> — часы;</description></item><item><description><see cref="DateTimeInterval.Day" /> — дни;</description></item><item><description><see cref="DateTimeInterval.Month" /> — месяцы;</description></item><item><description><see cref="DateTimeInterval.Year" /> — годы.</description></item></list>
        /// Метод учитывает особенности календаря .NET (например, разную длину месяцев и високосные годы).</remarks>
        private static DateTime AddInterval(DateTime value, int step, DateTimeInterval interval)
        {
            switch (interval)
            {
                case DateTimeInterval.Millisecond:
                    return value.AddMilliseconds(step);

                case DateTimeInterval.Second:
                    return value.AddSeconds(step);

                case DateTimeInterval.Minute:
                    return value.AddMinutes(step);

                case DateTimeInterval.Hour:
                    return value.AddHours(step);

                case DateTimeInterval.Day:
                    return value.AddDays(step);

                case DateTimeInterval.Month:
                    return value.AddMonths(step);

                case DateTimeInterval.Year:
                    return value.AddYears(step);

                default:
                    throw new ArgumentOutOfRangeException(nameof(interval), interval, null);
            }
        }

        private static IEnumerable<DateTime> GetDatesIterator(
            DateTime startDate,
            DateTime endDate,
            int step,
            DateTimeInterval interval,
            Func<DateTime, int, bool> includeDate,
            Func<DateTime, int, bool> excludeDate)
        {
            var forward = step > 0;

            // проверка направления
            if (forward && startDate > endDate)
            {
                yield break;
            }

            if (!forward && startDate < endDate)
            {
                yield break;
            }

            var current = startDate;
            var index = 0;

            while (forward ? current <= endDate : current >= endDate)
            {
                var include =
                    (includeDate == null || includeDate(current, index)) &&
                    (excludeDate == null || !excludeDate(current, index));

                if (include)
                {
                    yield return current;
                }

                current = AddInterval(current, step, interval);
                index++;
            }
        }

        /// <summary>
        /// Indexes the of.
        /// </summary>
        /// <typeparam name="T">The type of elements in the collection to search.</typeparam>
        /// <param name="e">The collection to search.</param>
        /// <param name="match">The predicate function to match elements.</param>
        /// <param name="reverseSearch">if set to <c>true</c> [reverse search].</param>
        /// <returns>System.Int32.</returns>
        private static int IndexOf<T>(IEnumerable<T> e, Func<T, int, bool> match, bool reverseSearch = false)
        {
            if (e == null)
            {
                return -1;
            }

            // Если исходная коллекция - массив или IList<T>, используем индексацию
            if (e is IList<T> list)
            {
                if (!reverseSearch)
                {
                    for (var i = 0; i < list.Count; i++)
                    {
                        if (match(list[i], i))
                        {
                            return i;
                        }
                    }
                }
                else
                {
                    for (var i = list.Count - 1; i >= 0; i--)
                    {
                        if (match(list[i], i))
                        {
                            return i;
                        }
                    }
                }

                return -1;
            }

            // Для остальных IEnumerable<T>
            if (!reverseSearch)
            {
                var i = 0;
                foreach (var item in e)
                {
                    if (match(item, i))
                    {
                        return i;
                    }

                    i++;
                }
            }
            else
            {
                // К сожалению, для IEnumerable<T> без индексации придётся материализовать в список
                var arr = e.ToArray();
                for (var i = arr.Length - 1; i >= 0; i--)
                {
                    if (match(arr[i], i))
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        /// <summary>
        /// Представляет диапазон дат с начальной и конечной границей.
        /// </summary>
        public struct DateRange
        {
            /// <summary>
            /// Инициализирует новый экземпляр <see cref="DateRange"/> с указанными границами.
            /// Если <paramref name="from"/> позже <paramref name="to"/>, значения автоматически меняются местами.
            /// </summary>
            /// <param name="from">Начальная дата диапазона.</param>
            /// <param name="to">Конечная дата диапазона.</param>
            public DateRange(DateTime from, DateTime to)
            {
                if (from > to)
                {
                    (from, to) = (to, from);
                }

                this.From = from;
                this.To = to;
            }

            /// <summary>
            /// Начальная дата диапазона.
            /// </summary>
            public DateTime From { get; set; }

            /// <summary>
            /// Конечная дата диапазона.
            /// </summary>
            public DateTime To { get; set; }

            /// <summary>
            /// Неявное преобразование <see cref="DateRange"/> в кортеж <c>(DateTime From, DateTime To)</c>.
            /// </summary>
            /// <param name="range">Диапазон дат.</param>
            public static implicit operator (DateTime From, DateTime To)(DateRange range)
            {
                return (range.From, range.To);
            }

            /// <summary>
            /// Неявное преобразование кортежа <c>(DateTime From, DateTime To)</c> в <see cref="DateRange"/>.
            /// </summary>
            /// <param name="tuple">Кортеж с границами диапазона.</param>
            public static implicit operator DateRange((DateTime From, DateTime To) tuple)
            {
                return new DateRange(tuple.From, tuple.To);
            }

            /// <summary>
            /// Определяет, содержится ли указанная дата в диапазоне.
            /// </summary>
            /// <param name="date">Проверяемая дата.</param>
            /// <returns>
            /// <c>true</c>, если <paramref name="date"/> находится между <see cref="From"/> и <see cref="To"/> включительно; иначе <c>false</c>.
            /// </returns>
            public bool Contains(DateTime date) => date >= this.From && date <= this.To;

            /// <summary>
            /// Определяет, полностью ли другой диапазон содержится в данном диапазоне.
            /// </summary>
            /// <param name="other">Другой диапазон дат.</param>
            /// <returns>
            /// <c>true</c>, если <paramref name="other"/> полностью находится внутри данного диапазона; иначе <c>false</c>.
            /// </returns>
            public bool Contains(DateRange other) => this.Contains(other.From) && this.Contains(other.To);

            /// <summary>
            /// Возвращает строковое представление диапазона.
            /// </summary>
            /// <returns>
            /// Строка в формате "dd.MM.yyyy" или "dd.MM.yyyy HH:mm:ss" для каждой границы,
            /// в зависимости от наличия времени.
            /// </returns>
            public override string ToString()
            {
                return (HasTime(this.From) ? $"{this.From:dd.MM.yyyy HH:mm:ss}" : $"{this.From:dd.MM.yyyy}")
                       + " - "
                       + (HasTime(this.To) ? $"{this.To:dd.MM.yyyy HH:mm:ss}" : $"{this.To:dd.MM.yyyy}");
            }
        }
    }
}