// ***********************************************************************
// Assembly         : RuntimeStuff
// Author           : RS
// Created          : 01-06-2026
//
// Last Modified By : RS
// Last Modified On : 01-07-2026
// ***********************************************************************
// <copyright file="StringExtensions.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************

namespace RuntimeStuff.Extensions
{
    using System;
    using System.Globalization;
    using RuntimeStuff.Helpers;

    /// <summary>
    /// Предоставляет набор методов-расширений для работы со строками, включая замену, удаление и обрезку подстрок, а
    /// также удаление суффикса.
    /// </summary>
    /// <remarks>Класс содержит статические методы-расширения для типа <see cref="string" />, позволяющие
    /// выполнять типовые операции над строками с использованием диапазонов индексов и сравнения суффиксов. Методы
    /// предназначены для упрощения манипуляций со строками в пользовательском коде. Все методы не изменяют исходную
    /// строку, а возвращают новую строку с применёнными изменениями.</remarks>
    public static class StringExtensions
    {
        /// <summary>
        /// Trimes the white chars.
        /// </summary>
        /// <param name="s">The s.</param>
        /// <returns>System.String.</returns>
        public static string TrimWhiteChars(this string s) => StringHelper.TrimWhiteChars(s);

        /// <summary>
        /// Расширение для string.IsNullOrWhiteSpace(s).
        /// </summary>
        /// <param name="s">Строка.</param>
        /// <returns>Возвращает значение string.IsNullOrWhiteSpace(s).</returns>
        public static bool IsEmpty(this string s) => string.IsNullOrWhiteSpace(s);

        /// <summary>
        /// Проверяет, является ли строка числовым значением и преобразует её в <see cref="decimal"/>.
        /// </summary>
        /// <param name="s">Строка для проверки.</param>
        /// <param name="d">Выходной параметр, содержащий значение <see cref="decimal"/>, если строка является числом.</param>
        /// <returns><c>true</c>, если строка успешно распознана как число; иначе <c>false</c>.</returns>
        /// <remarks>
        /// Используется <see cref="NumberStyles.Any"/> и <see cref="NumberFormatInfo.InvariantInfo"/>
        /// для корректного парсинга чисел в стандартном формате.
        /// </remarks>
        public static bool IsNumber(this string s, out decimal d)
        {
            return decimal.TryParse(s, NumberStyles.Any, NumberFormatInfo.InvariantInfo, out d);
        }

        /// <summary>
        /// Проверяет, является ли строка числовым значением.
        /// </summary>
        /// <param name="s">Строка для проверки.</param>
        /// <returns><c>true</c>, если строка является числом; иначе <c>false</c>.</returns>
        /// <remarks>
        /// Метод является перегрузкой для удобства и игнорирует само значение числа.
        /// </remarks>
        public static bool IsNumber(this string s)
        {
            return s.IsNumber(out _);
        }

        /// <summary>
        /// Возвращает первую непустую строку, не состоящую только из пробельных символов.
        /// </summary>
        /// <param name="str">
        /// Исходная строка, проверяемая в первую очередь.
        /// </param>
        /// <param name="strings">
        /// Дополнительные строки для проверки, используемые в случае,
        /// если <paramref name="str"/> равна <c>null</c>, пуста или содержит только пробельные символы.
        /// </param>
        /// <returns>
        /// Первую строку, которая не равна <c>null</c>, не пуста и не состоит только из пробельных символов;
        /// либо <c>null</c>, если все переданные строки не удовлетворяют этому условию.
        /// </returns>
        /// <remarks>
        /// Метод является строковым аналогом оператора <c>COALESCE</c>
        /// и удобен для выбора значения по умолчанию из набора строк.
        /// </remarks>
        public static string Coalesce(this string str, params string[] strings) => StringHelper.Coalesce(str, strings);

        /// <summary>
        /// Проверяет, содержит ли исходная строка указанную подстроку,
        /// используя заданный способ сравнения строк.
        /// </summary>
        /// <param name="source">Исходная строка, в которой выполняется поиск.</param>
        /// <param name="value">Подстрока, которую необходимо найти.</param>
        /// <param name="comparison">Параметр, определяющий способ сравнения строк
        /// (<see cref="StringComparison" />), например <see cref="StringComparison.OrdinalIgnoreCase" />.</param>
        /// <returns>Значение <c>true</c>, если подстрока найдена в исходной строке;
        /// в противном случае — <c>false</c>.
        /// Также возвращает <c>false</c>, если <paramref name="source" /> или <paramref name="value" /> равны <c>null</c>.</returns>
        public static bool Contains(this string source, string value, StringComparison comparison) => StringHelper.Contains(source, value, comparison);

        /// <summary>
        /// Заменяет часть строки в диапазоне [startIndex..endIndex] на указанную строку.
        /// </summary>
        /// <param name="s">Исходная строка.</param>
        /// <param name="startIndex">Начальная позиция (включительно).</param>
        /// <param name="endIndex">Конечная позиция (включительно).</param>
        /// <param name="replaceString">Строка для замены.</param>
        /// <returns>Новая строка с заменой.</returns>
        public static string Replace(this string s, int startIndex, int endIndex, string replaceString) => StringHelper.Replace(s, startIndex, endIndex, replaceString);

        /// <summary>
        /// Возвращает строку, повторенную указанное количество раз.
        /// </summary>
        /// <param name="s">Исходная строка.</param>
        /// <param name="count">Количество повторений.</param>
        /// <returns>Новая строка, состоящая из повторений исходной строки.</returns>
        public static string RepeatString(this string s, int count) => StringHelper.RepeatString(s, count);

        /// <summary>
        /// Удаляет часть строки в диапазоне [startIndex..endIndex]. Работает как s.Substring(0, startIndex) +
        /// s.Substring(endIndex + 1);.
        /// </summary>
        /// <param name="s">Исходная строка.</param>
        /// <param name="startIndex">Начальная позиция (включительно).</param>
        /// <param name="endIndex">Конечная позиция (включительно).</param>
        /// <returns>System.String.</returns>
        public static string Cut(this string s, int startIndex, int endIndex) => StringHelper.Cut(s, startIndex, endIndex);

        /// <summary>
        /// Возвращает часть строки в диапазоне [startIndex..endIndex]. Работает как string.Substring(s, startIndex, endIndex -
        /// startIndex + 1).
        /// </summary>
        /// <param name="s">Исходная строка.</param>
        /// <param name="startIndex">Начальная позиция (включительно).</param>
        /// <param name="endIndex">Конечная позиция (включительно).</param>
        /// <returns>System.String.</returns>
        public static string Crop(this string s, int startIndex, int endIndex) => StringHelper.Crop(s, startIndex, endIndex);

        /// <summary>
        /// Метод удаляет указанный суффикс с конца строки, если он существует.
        /// </summary>
        /// <param name="s">Исходная строка, из которой нужно удалить суффикс.</param>
        /// <param name="subStr">Строка-суффикс, которую нужно удалить с конца.</param>
        /// <param name="comparison">Тип сравнения строк при проверке суффикса.</param>
        /// <returns>Строка без указанного суффикса в конце, если он был найден.</returns>
        /// <remarks>Метод проверяет заканчивается ли исходная строка указанным суффиксом.
        /// Если суффикс найден, возвращается строка без этого суффикса.
        /// Если суффикс не найден или параметры пустые, возвращается исходная строка.</remarks>
        public static string TrimEnd(this string s, string subStr, StringComparison comparison = StringComparison.Ordinal) => StringHelper.TrimEnd(s, subStr, comparison);
    }
}