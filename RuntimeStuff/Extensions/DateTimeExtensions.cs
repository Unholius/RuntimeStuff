// ***********************************************************************
// Assembly         : RuntimeStuff
// Author           : RS
// Created          : 01-06-2026
//
// Last Modified By : RS
// Last Modified On : 01-07-2026
// ***********************************************************************
// <copyright file="DateTimeExtensions.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
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
        public static IEnumerable<DateTime> GetDates(
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
        public static IEnumerable<DateTime> EachDay(this DateTime startDate, DateTime endDate) => DateTimeHelper.EachDay(startDate, endDate);

        /// <summary>
        /// Возвращает максимальную дату из текущей и переданных дат.
        /// </summary>
        /// <param name="date">Первая дата для сравнения.</param>
        /// <param name="dates">Дополнительные даты для сравнения.</param>
        /// <returns>Максимальная дата.</returns>
        public static DateTime Max(this DateTime date, params DateTime[] dates)
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
        public static DateTime Min(this DateTime date, params DateTime[] dates)
        {
            var allDates = new List<DateTime> { date };
            if (dates != null)
            {
                allDates.AddRange(dates);
            }

            return DateTimeHelper.Min(allDates.ToArray());
        }
    }
}