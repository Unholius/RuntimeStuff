// ***********************************************************************
// Assembly         : RuntimeStuff
// Author           : RS
// Created          : 01-06-2026
//
// Last Modified By : RS
// Last Modified On : 01-07-2026
// ***********************************************************************
// <copyright file="ComparableExtensions.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************
namespace RuntimeStuff.Extensions
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Методы расширения для типов, поддерживающих сравнение.
    /// </summary>
    public static class ComparableExtensions
    {
        /// <summary>
        /// Проверяет, содержится ли элемент в указанном наборе значений.
        /// Поддерживается массив значений, ISet и кастомный компаратор.
        /// </summary>
        /// <typeparam name="T">Type.</typeparam>
        /// <param name="item">The item.</param>
        /// <param name="values">The values.</param>
        /// <param name="comparer">The comparer.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        public static bool In<T>(this T item, IEnumerable<T> values, IEqualityComparer<T> comparer = null)
        {
            if (values == null)
            {
                return false;
            }

            var set = values is ISet<T> s && (comparer == null || (s is HashSet<T> hs && hs.Comparer.Equals(comparer)))
                ? s
                : new HashSet<T>(values, comparer);

            return set.Contains(item);
        }

        /// <summary>
        /// Удобная перегрузка для массива значений (params).
        /// </summary>
        /// <typeparam name="T">Type.</typeparam>
        /// <param name="item">The item.</param>
        /// <param name="comparer">The comparer.</param>
        /// <param name="values">The values.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        public static bool In<T>(this T item, IEqualityComparer<T> comparer, params T[] values) => item.In(values, comparer);

        /// <summary>
        /// Удобная перегрузка для массива значений (params) для коллекций.
        /// </summary>
        /// <typeparam name="T">Type.</typeparam>
        /// <param name="item">The item.</param>
        /// <param name="values">The values.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        public static bool In<T>(this T item, params T[] values) => item.In(null, values);

        /// <summary>
        /// Проверяет, находится ли строка <paramref name="value" /> в диапазоне от <paramref name="from" /> до
        /// <paramref name="to" /> (включительно).
        /// Можно указать тип сравнения <see cref="StringComparison" /> или <see cref="StringComparer" />.
        /// </summary>
        /// <param name="value">Проверяемая строка.</param>
        /// <param name="from">Начало диапазона (включительно).</param>
        /// <param name="to">Конец диапазона (включительно).</param>
        /// <param name="comparison">Тип сравнения строк (по умолчанию — <see cref="StringComparison.Ordinal" />).</param>
        /// <returns><c>true</c>, если <paramref name="value" /> находится между <paramref name="from" /> и <paramref name="to" />;
        /// иначе <c>false</c>.</returns>
        /// <exception cref="System.ArgumentNullException">value.</exception>
        /// <exception cref="System.ArgumentNullException">from.</exception>
        /// <exception cref="System.ArgumentNullException">to.</exception>
        public static bool Between(this string value, string from, string to, StringComparison comparison)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (from == null)
            {
                throw new ArgumentNullException(nameof(from));
            }

            if (to == null)
            {
                throw new ArgumentNullException(nameof(to));
            }

            return string.Compare(value, from, comparison) >= 0
                   && string.Compare(value, to, comparison) <= 0;
        }

        /// <summary>
        /// Проверяет, находится ли строка <paramref name="value" /> в диапазоне от <paramref name="from" /> до
        /// <paramref name="to" /> (включительно),
        /// используя заданный <see cref="StringComparer" />.
        /// </summary>
        /// <param name="value">Проверяемая строка.</param>
        /// <param name="from">Начало диапазона (включительно).</param>
        /// <param name="to">Конец диапазона (включительно).</param>
        /// <param name="comparer">Сравниватель строк (по умолчанию <see cref="StringComparer.Ordinal" />).</param>
        /// <returns><c>true</c>, если <paramref name="value" /> находится между <paramref name="from" /> и <paramref name="to" />;
        /// иначе <c>false</c>.</returns>
        /// <exception cref="System.ArgumentNullException">value.</exception>
        /// <exception cref="System.ArgumentNullException">from.</exception>
        /// <exception cref="System.ArgumentNullException">to.</exception>
        /// <exception cref="System.ArgumentNullException">comparer.</exception>
        public static bool Between(this string value, string from, string to, StringComparer comparer)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (from == null)
            {
                throw new ArgumentNullException(nameof(from));
            }

            if (to == null)
            {
                throw new ArgumentNullException(nameof(to));
            }

            if (comparer == null)
            {
                throw new ArgumentNullException(nameof(comparer));
            }

            return comparer.Compare(value, from) >= 0
                   && comparer.Compare(value, to) <= 0;
        }

        /// <summary>
        /// Проверяет, находится ли значение x в диапазоне [from, to] включительно.
        /// </summary>
        /// <typeparam name="T">Тип сравниваемого значения (должен реализовывать IComparable&lt;T&gt;).</typeparam>
        /// <param name="x">Значение для проверки.</param>
        /// <param name="from">Нижняя граница диапазона (включительно).</param>
        /// <param name="to">Верхняя граница диапазона (включительно).</param>
        /// <returns>true, если x находится в диапазоне [from, to]; иначе false.</returns>
        /// <exception cref="System.ArgumentNullException">x.</exception>
        /// <exception cref="System.ArgumentNullException">from.</exception>
        /// <exception cref="System.ArgumentNullException">to.</exception>
        public static bool Between<T>(this T x, T from, T to)
            where T : IComparable<T>
        {
            if (object.Equals(x, default(T)))
            {
                throw new ArgumentNullException(nameof(x));
            }

            if (object.Equals(from, default(T)))
            {
                throw new ArgumentNullException(nameof(from));
            }

            if (object.Equals(to, default(T)))
            {
                throw new ArgumentNullException(nameof(to));
            }

            if (x is string s1 && from is string s2 && to is string s3)
            {
                return Between(s1, s2, s3, StringComparison.Ordinal);
            }

            return x.CompareTo(from) >= 0 && x.CompareTo(to) <= 0;
        }

        /// <summary>
        /// Возвращает значение, соответствующее первому совпавшему случаю, иначе значение по умолчанию.
        /// </summary>
        /// <typeparam name="TWhen">Тип значения для сравнения.</typeparam>
        /// <typeparam name="TThen">Тип возвращаемого значения.</typeparam>
        /// <param name="obj">Значение для сравнения.</param>
        /// <param name="defaultValue">Значение по умолчанию.</param>
        /// <param name="cases">Массив пар (значение для сравнения, возвращаемое значение).</param>
        /// <returns>Значение then для первого совпадения или defaultValue.</returns>
        public static TThen Case<TWhen, TThen>(
            this TWhen obj,
            Func<TWhen, TThen> defaultValue,
            params (TWhen when, TThen then)[] cases)
        {
            if (defaultValue == null)
                throw new ArgumentNullException(nameof(defaultValue));

            var comparer = EqualityComparer<TWhen>.Default;

            foreach (var (when, then) in cases)
            {
                if (comparer.Equals(obj, when))
                    return then;
            }

            return defaultValue(obj);
        }

        /// <summary>
        /// Возвращает значение, соответствующее первому совпавшему случаю, иначе значение по умолчанию.
        /// </summary>
        /// <typeparam name="TWhen">Тип значения для сравнения.</typeparam>
        /// <typeparam name="TThen">Тип возвращаемого значения.</typeparam>
        /// <param name="obj">Значение для сравнения.</param>
        /// <param name="defaultValue">Значение по умолчанию.</param>
        /// <param name="objParser">Парсер значения.</param>
        /// <param name="cases">Массив пар (значение для сравнения, возвращаемое значение).</param>
        /// <returns>Значение then для первого совпадения или defaultValue.</returns>
        public static TThen Case<TWhen, TThen>(
            this TWhen obj,
            Func<TWhen, TThen> defaultValue,
            Func<TWhen, TWhen> objParser,
            params (TWhen when, TThen then)[] cases)
        {
            if (defaultValue == null)
                throw new ArgumentNullException(nameof(defaultValue));
            if (cases == null)
                throw new ArgumentNullException(nameof(cases));

            var value = objParser != null ? objParser(obj) : obj;
            var comparer = EqualityComparer<TWhen>.Default;

            foreach (var (when, then) in cases)
            {
                if (comparer.Equals(value, when))
                    return then;
            }

            return defaultValue(obj);
        }

        /// <summary>
        /// Возвращает значение, соответствующее первому совпавшему случаю, иначе значение по умолчанию.
        /// </summary>
        /// <typeparam name="TWhen">Тип значения для сравнения.</typeparam>
        /// <typeparam name="TThen">Тип возвращаемого значения.</typeparam>
        /// <param name="obj">Значение для сравнения.</param>
        /// <param name="defaultValue">Значение по умолчанию.</param>
        /// <param name="cases">Массив пар (значение для сравнения, возвращаемое значение).</param>
        /// <returns>Значение then для первого совпадения или defaultValue.</returns>
        public static TThen Case<TWhen, TThen>(this TWhen obj, Func<TWhen, TThen> defaultValue, params (Func<TWhen, bool> when, TThen then)[] cases)
        {
            foreach (var (when, then) in cases)
            {
                if (when(obj))
                {
                    return then;
                }
            }

            return defaultValue(obj);
        }
    }
}