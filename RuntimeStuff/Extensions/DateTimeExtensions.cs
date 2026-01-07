// ***********************************************************************
// Assembly         : RuntimeStuff
// Author           : RS
// Created          : 01-06-2026
//
// Last Modified By : RS
// Last Modified On : 01-07-2026
// ***********************************************************************
// <copyright file="DateTimeExtensions.cs" company="Rudnev Sergey">
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************
namespace RuntimeStuff.Extensions
{
    using System;
    using System.Collections.Generic;
    using RuntimeStuff.Helpers;

    /// <summary>
    /// Предоставляет методы расширения для работы с типом <see cref="DateTime" />.
    /// </summary>
    public static class DateTimeExtensions
    {
        /// <summary>
        /// Определяет, содержит ли дата компонент времени (не равно 00:00:00).
        /// </summary>
        /// <param name="date">Дата для проверки.</param>
        /// <returns>true, если время не равно 00:00:00; в противном случае — false.</returns>
        public static bool HasTime(this DateTime date) => DateTimeHelper.HasTime(date);

        /// <summary>
        /// Добавляет к дате временной интервал, заданный строкой.
        /// </summary>
        /// <param name="date">Исходная дата.</param>
        /// <param name="timeSpan">Строка с временным интервалом.</param>
        /// <returns>Новая дата после добавления интервала.</returns>
        public static DateTime AddInterval(this DateTime date, string timeSpan) => DateTimeHelper.Add(date, timeSpan);

        /// <summary>
        /// Возвращает начало дня (00:00:00) для указанной даты.
        /// </summary>
        /// <param name="date">Исходная дата.</param>
        /// <returns>Дата с временем 00:00:00.</returns>
        public static DateTime BeginDay(this DateTime date) => DateTimeHelper.BeginDay(date);

        /// <summary>
        /// Возвращает конец дня (23:59:59.999) для указанной даты.
        /// </summary>
        /// <param name="date">Исходная дата.</param>
        /// <returns>Дата с временем 23:59:59.999.</returns>
        public static DateTime EndDay(this DateTime date) => DateTimeHelper.EndDay(date);

        /// <summary>
        /// Возвращает начало месяца (первый день, 00:00:00) для указанной даты.
        /// </summary>
        /// <param name="date">Исходная дата.</param>
        /// <returns>Дата с первым днем месяца и временем 00:00:00.</returns>
        public static DateTime BeginMonth(this DateTime date) => DateTimeHelper.BeginMonth(date);

        /// <summary>
        /// Возвращает конец месяца (последний день, 23:59:59.999) для указанной даты.
        /// </summary>
        /// <param name="date">Исходная дата.</param>
        /// <returns>Дата с последним днем месяца и временем 23:59:59.999.</returns>
        public static DateTime EndMonth(this DateTime date) => DateTimeHelper.EndMonth(date);

        /// <summary>
        /// Возвращает начало года (первый день, 00:00:00) для указанной даты.
        /// </summary>
        /// <param name="date">Исходная дата.</param>
        /// <returns>Дата с первым днем года и временем 00:00:00.</returns>
        public static DateTime BeginYear(this DateTime date) => DateTimeHelper.BeginYear(date);

        /// <summary>
        /// Возвращает конец года (последний день, 23:59:59.999) для указанной даты.
        /// </summary>
        /// <param name="date">Исходная дата.</param>
        /// <returns>Дата с последним днем года и временем 23:59:59.999.</returns>
        public static DateTime EndYear(this DateTime date) => DateTimeHelper.EndYear(date);

        /// <summary>
        /// Возвращает вчерашнюю дату (начало дня).
        /// </summary>
        /// <param name="date">Исходная дата.</param>
        /// <returns>Дата предыдущего дня с временем 00:00:00.</returns>
        public static DateTime Yesterday(this DateTime date) => DateTimeHelper.Yesterday(date);

        /// <summary>
        /// Возвращает уникальные тики для текущего момента времени.
        /// </summary>
        /// <param name="date">Исходная дата (не используется).</param>
        /// <returns>Уникальные тики текущего момента времени.</returns>
        public static long ExactTicks(this DateTime date) => DateTimeHelper.ExactTicks();

        /// <summary>
        /// Возвращает текущую дату и время с гарантией уникальности тиков.
        /// </summary>
        /// <param name="date">Исходная дата (не используется).</param>
        /// <returns>Текущая дата и время с уникальными тиками.</returns>
        public static DateTime ExactNow(this DateTime date) => DateTimeHelper.ExactNow();

        /// <summary>
        /// Возвращает последовательность дат от текущей даты до указанной даты с заданным шагом.
        /// </summary>
        /// <param name="startDate">Начальная дата.</param>
        /// <param name="endDate">Конечная дата.</param>
        /// <param name="step">Шаг изменения даты (по умолчанию 1).</param>
        /// <param name="interval">Тип временного интервала (по умолчанию день).</param>
        /// <param name="includeDate">Функция-фильтр для включения дат.</param>
        /// <param name="excludeDate">Функция-фильтр для исключения дат.</param>
        /// <returns>Последовательность дат.</returns>
        public static IEnumerable<DateTime> GetDatesTo(
            this DateTime startDate,
            DateTime endDate,
            int step = 1,
            DateTimeHelper.DateTimeInterval interval = DateTimeHelper.DateTimeInterval.Day,
            Func<DateTime, int, bool> includeDate = null,
            Func<DateTime, int, bool> excludeDate = null) => DateTimeHelper.GetDates(startDate, endDate, step, interval, includeDate, excludeDate);

        /// <summary>
        /// Возвращает последовательность дней от текущей даты до указанной даты.
        /// </summary>
        /// <param name="startDate">Начальная дата.</param>
        /// <param name="endDate">Конечная дата.</param>
        /// <returns>Последовательность дней.</returns>
        public static IEnumerable<DateTime> EachDayTo(this DateTime startDate, DateTime endDate) => DateTimeHelper.EachDay(startDate, endDate);

        /// <summary>
        /// Возвращает максимальную дату из текущей и переданных дат.
        /// </summary>
        /// <param name="date">Первая дата для сравнения.</param>
        /// <param name="dates">Дополнительные даты для сравнения.</param>
        /// <returns>Максимальная дата.</returns>
        public static DateTime MaxWith(this DateTime date, params DateTime[] dates)
        {
            var allDates = new List<DateTime> { date };
            if (dates != null)
            {
                allDates.AddRange(dates);
            }

            return DateTimeHelper.Max(allDates.ToArray());
        }

        /// <summary>
        /// Возвращает минимальную дату из текущей и переданных дат.
        /// </summary>
        /// <param name="date">Первая дата для сравнения.</param>
        /// <param name="dates">Дополнительные даты для сравнения.</param>
        /// <returns>Минимальная дата.</returns>
        public static DateTime MinWith(this DateTime date, params DateTime[] dates)
        {
            var allDates = new List<DateTime> { date };
            if (dates != null)
            {
                allDates.AddRange(dates);
            }

            return DateTimeHelper.Min(allDates.ToArray());
        }

        /// <summary>
        /// Возвращает временной интервал между текущей и указанной датой.
        /// </summary>
        /// <param name="startDate">Начальная дата.</param>
        /// <param name="endDate">Конечная дата.</param>
        /// <returns>Интервал времени между датами.</returns>
        public static TimeSpan ElapsedTo(this DateTime startDate, DateTime endDate) => endDate - startDate;

        /// <summary>
        /// Возвращает временной интервал между текущей датой и текущим моментом.
        /// </summary>
        /// <param name="date">Начальная дата.</param>
        /// <returns>Интервал времени от указанной даты до текущего момента.</returns>
        public static TimeSpan ElapsedFromNow(this DateTime date) => DateTime.Now - date;

        /// <summary>
        /// Проверяет, находится ли дата в указанном диапазоне (включительно).
        /// </summary>
        /// <param name="date">Проверяемая дата.</param>
        /// <param name="startDate">Начало диапазона.</param>
        /// <param name="endDate">Конец диапазона.</param>
        /// <returns>true, если дата находится в диапазоне; в противном случае — false.</returns>
        public static bool IsBetween(this DateTime date, DateTime startDate, DateTime endDate) => date >= startDate && date <= endDate;

        /// <summary>
        /// Проверяет, является ли дата сегодняшним днем (без учета времени).
        /// </summary>
        /// <param name="date">Проверяемая дата.</param>
        /// <returns>true, если дата сегодняшняя; в противном случае — false.</returns>
        public static bool IsToday(this DateTime date) => date.Date == DateTime.Today;

        /// <summary>
        /// Проверяет, является ли дата вчерашним днем (без учета времени).
        /// </summary>
        /// <param name="date">Проверяемая дата.</param>
        /// <returns>true, если дата вчерашняя; в противном случае — false.</returns>
        public static bool IsYesterday(this DateTime date) => date.Date == DateTime.Today.AddDays(-1);

        /// <summary>
        /// Проверяет, является ли дата завтрашним днем (без учета времени).
        /// </summary>
        /// <param name="date">Проверяемая дата.</param>
        /// <returns>true, если дата завтрашняя; в противном случае — false.</returns>
        public static bool IsTomorrow(this DateTime date) => date.Date == DateTime.Today.AddDays(1);

        /// <summary>
        /// Проверяет, является ли дата выходным днем.
        /// </summary>
        /// <param name="date">Проверяемая дата.</param>
        /// <returns>true, если это суббота или воскресенье; в противном случае — false.</returns>
        public static bool IsWeekend(this DateTime date) => date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;

        /// <summary>
        /// Проверяет, является ли дата рабочим днем.
        /// </summary>
        /// <param name="date">Проверяемая дата.</param>
        /// <returns>true, если это понедельник-пятница; в противном случае — false.</returns>
        public static bool IsWeekday(this DateTime date) => !date.IsWeekend();

        /// <summary>
        /// Возвращает первый день недели для указанной даты.
        /// </summary>
        /// <param name="date">Исходная дата.</param>
        /// <param name="startOfWeek">День, считающийся началом недели (по умолчанию понедельник).</param>
        /// <returns>Первый день недели.</returns>
        public static DateTime StartOfWeek(this DateTime date, DayOfWeek startOfWeek = DayOfWeek.Monday)
        {
            int diff = (7 + (date.DayOfWeek - startOfWeek)) % 7;
            return date.AddDays(-1 * diff).Date;
        }

        /// <summary>
        /// Возвращает последний день недели для указанной даты.
        /// </summary>
        /// <param name="date">Исходная дата.</param>
        /// <param name="startOfWeek">День, считающийся началом недели (по умолчанию понедельник).</param>
        /// <returns>Последний день недели.</returns>
        public static DateTime EndOfWeek(this DateTime date, DayOfWeek startOfWeek = DayOfWeek.Monday)
        {
            var start = date.StartOfWeek(startOfWeek);
            return start.AddDays(6).EndDay();
        }
    }
}