using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace RuntimeStuff.Extensions
{
    /// <summary>
    /// Расширения для работы с датой и временем
    /// </summary>
    public static class DateTimeExtensions
    {
        /// <summary>
        /// Проверяет, содержит ли DateTime компонент времени (не равно 00:00:00)
        /// </summary>
        /// <param name="d">Проверяемая дата</param>
        /// <returns>True, если время не равно 00:00:00</returns>
        public static bool HasTime(this DateTime d)
        {
            return d.TimeOfDay != TimeSpan.Zero;
        }

        /// <summary>
        /// Проверяет, содержит ли nullable DateTime компонент времени (не равно 00:00:00)
        /// </summary>
        /// <param name="d">Проверяемая nullable дата</param>
        /// <returns>True, если дата не null и время не равно 00:00:00</returns>
        public static bool HasTime(this DateTime? d)
        {
            return d.HasValue && d.Value.TimeOfDay != TimeSpan.Zero;
        }

        /// <summary>
        /// Парсит строку в массив TimeSpan. Пример: "1d -12m +3M -100s +6y"
        /// </summary>
        /// <param name="s">Строка для парсинга. Поддерживаемые форматы: 
        /// Yy - год (365 дней), M - месяц (30 дней), Ww - недели, Dd - день, 
        /// Hh - час, m - минуты, Ss - секунды, Ff - миллисекунды</param>
        /// <returns>Массив TimeSpan</returns>
        public static TimeSpan[] ParseTimeSpan(this string s)
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
        /// Добавляет к дате временной интервал, заданный строкой
        /// </summary>
        /// <param name="date">Исходная дата</param>
        /// <param name="timeSpan">Строка с временным интервалом</param>
        /// <returns>Новая дата после добавления интервала</returns>
        public static DateTime Add(this DateTime date, string timeSpan)
        {
            var timeIntervals = timeSpan.ParseTimeSpan();
            var d = date;
            foreach (var ti in timeIntervals)
            {
                d = d.Add(ti);
            }
            return d;
        }

        /// <summary>
        /// Возвращает начало дня (00:00:00) для указанной даты
        /// </summary>
        /// <param name="dt">Исходная дата</param>
        /// <returns>Дата с временем 00:00:00</returns>
        public static DateTime BeginDay(this DateTime dt)
        {
            return new DateTime(dt.Year, dt.Month, dt.Day);
        }

        /// <summary>
        /// Возвращает начало дня (00:00:00) для nullable даты
        /// </summary>
        /// <param name="date">Исходная nullable дата</param>
        /// <returns>Дата с временем 00:00:00 или DateTime.MinValue если date равно null</returns>
        public static DateTime BeginDay(this DateTime? date)
        {
            if (date == null) return DateTime.MinValue;
            DateTime dt = (DateTime)date;
            return new DateTime(dt.Year, dt.Month, dt.Day);
        }

        /// <summary>
        /// Возвращает конец дня (23:59:59.999) для указанной даты
        /// </summary>
        /// <param name="dt">Исходная дата</param>
        /// <returns>Дата с временем 23:59:59.999</returns>
        public static DateTime EndDay(this DateTime dt)
        {
            return new DateTime(dt.Year, dt.Month, dt.Day, 23, 59, 59, 999);
        }

        /// <summary>
        /// Возвращает конец дня (23:59:59.999) для nullable даты
        /// </summary>
        /// <param name="date">Исходная nullable дата</param>
        /// <returns>Дата с временем 23:59:59.999 или DateTime.MaxValue если date равно null</returns>
        public static DateTime EndDay(this DateTime? date)
        {
            if (date == null) return DateTime.MaxValue;
            DateTime dt = (DateTime)date;
            return new DateTime(dt.Year, dt.Month, dt.Day, 23, 59, 59, 999);
        }

        /// <summary>
        /// Возвращает начало месяца (первый день, 00:00:00) для указанной даты
        /// </summary>
        /// <param name="dt">Исходная дата</param>
        /// <returns>Дата с первым днем месяца и временем 00:00:00</returns>
        public static DateTime BeginMonth(this DateTime dt)
        {
            return new DateTime(dt.Year, dt.Month, 1);
        }

        /// <summary>
        /// Возвращает начало месяца (первый день, 00:00:00) для nullable даты
        /// </summary>
        /// <param name="date">Исходная nullable дата</param>
        /// <returns>Дата с первым днем месяца и временем 00:00:00 или DateTime.MinValue если date равно null</returns>
        public static DateTime BeginMonth(this DateTime? date)
        {
            if (date == null) return DateTime.MinValue;
            DateTime dt = (DateTime)date;
            return new DateTime(dt.Year, dt.Month, 1);
        }

        /// <summary>
        /// Возвращает конец месяца (последний день, 23:59:59.999) для указанной даты
        /// </summary>
        /// <param name="dt">Исходная дата</param>
        /// <returns>Дата с последним днем месяца и временем 23:59:59.999</returns>
        public static DateTime EndMonth(this DateTime dt)
        {
            return new DateTime(dt.Year, dt.Month, DateTime.DaysInMonth(dt.Year, dt.Month), 23, 59, 59, 999);
        }

        /// <summary>
        /// Возвращает конец месяца (последний день, 23:59:59.999) для nullable даты
        /// </summary>
        /// <param name="date">Исходная nullable дата</param>
        /// <returns>Дата с последним днем месяца и временем 23:59:59.999 или DateTime.MaxValue если date равно null</returns>
        public static DateTime EndMonth(this DateTime? date)
        {
            if (date == null) return DateTime.MaxValue;
            DateTime dt = (DateTime)date;
            return new DateTime(dt.Year, dt.Month, DateTime.DaysInMonth(dt.Year, dt.Month), 23, 59, 59, 999);
        }

        /// <summary>
        /// Возвращает начало года (первый день, 00:00:00) для указанной даты
        /// </summary>
        /// <param name="dt">Исходная дата</param>
        /// <returns>Дата с первым днем года и временем 00:00:00</returns>
        public static DateTime BeginYear(this DateTime dt)
        {
            return new DateTime(dt.Year, 1, 1);
        }

        /// <summary>
        /// Возвращает начало года (первый день, 00:00:00) для nullable даты
        /// </summary>
        /// <param name="date">Исходная nullable дата</param>
        /// <returns>Дата с первым днем года и временем 00:00:00 или DateTime.MinValue если date равно null</returns>
        public static DateTime BeginYear(this DateTime? date)
        {
            if (date == null) return DateTime.MinValue;
            DateTime dt = (DateTime)date;
            return new DateTime(dt.Year, 1, 1);
        }

        /// <summary>
        /// Возвращает конец года (последний день, 23:59:59.999) для указанной даты
        /// </summary>
        /// <param name="dt">Исходная дата</param>
        /// <returns>Дата с последним днем года и временем 23:59:59.999</returns>
        public static DateTime EndYear(this DateTime dt)
        {
            return new DateTime(dt.Year, 12, DateTime.DaysInMonth(dt.Year, 12), 23, 59, 59, 999);
        }

        /// <summary>
        /// Возвращает конец года (последний день, 23:59:59.999) для nullable даты
        /// </summary>
        /// <param name="date">Исходная nullable дата</param>
        /// <returns>Дата с последним днем года и временем 23:59:59.999 или DateTime.MaxValue если date равно null</returns>
        public static DateTime EndYear(this DateTime? date)
        {
            if (date == null) return DateTime.MaxValue;
            DateTime dt = (DateTime)date;
            return new DateTime(dt.Year, 12, DateTime.DaysInMonth(dt.Year, 12), 23, 59, 59, 999);
        }

        /// <summary>
        /// Возвращает вчерашнюю дату (начало дня)
        /// </summary>
        /// <param name="date">Исходная дата</param>
        /// <returns>Дата предыдущего дня с временем 00:00:00</returns>
        public static DateTime Yesterday(this DateTime date)
        {
            return date.AddDays(-1).BeginDay();
        }

        /// <summary>
        /// Возвращает вчерашнюю дату (начало дня) для nullable даты
        /// </summary>
        /// <param name="date">Исходная nullable дата</param>
        /// <returns>Дата предыдущего дня с временем 00:00:00</returns>
        /// <exception cref="NullReferenceException">Если date равно null</exception>
        public static DateTime Yesterday(this DateTime? date)
        {
            return date?.AddDays(-1).BeginDay() ?? throw new NullReferenceException("DateTimeExtensions.Yesterday: Дата не должна быть <null>!");
        }

        private static long _lastTimeStamp = DateTime.Now.Ticks;

        /// <summary>
        /// Возвращает уникальные тики для текущего момента времени (гарантирует уникальность даже при быстрых последовательных вызовах)
        /// </summary>
        public static long NowTicks
        {
            get
            {
                long original, newValue;
                do
                {
                    original = _lastTimeStamp;
                    long now = DateTime.Now.Ticks;
                    newValue = Math.Max(now, original + 1);
                } while (Interlocked.CompareExchange(ref _lastTimeStamp, newValue, original) != original);
                return newValue;
            }
        }

        /// <summary>
        /// Возвращает текущую дату и время с гарантией уникальности тиков
        /// </summary>
        /// <param name="_">Экземпляр DateTime (не используется)</param>
        /// <returns>Текущая дата и время с уникальными тиками</returns>
        public static DateTime ExactNow(this DateTime _)
        {
            return new DateTime(NowTicks);
        }

        /// <summary>
        /// Возвращает уникальные тики для текущего момента времени
        /// </summary>
        /// <param name="_">Экземпляр DateTime (не используется)</param>
        /// <returns>Уникальные тики текущего момента времени</returns>
        public static long ExactTicks(this DateTime _)
        {
            return NowTicks;
        }
    }
}