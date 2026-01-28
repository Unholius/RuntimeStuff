// ***********************************************************************
// Assembly         : RuntimeStuff
// Author           : RS
// Created          : 01-06-2026
//
// Last Modified By : RS
// Last Modified On : 01-07-2026
// ***********************************************************************
// <copyright file="EnumExtensions.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************

namespace RuntimeStuff.Extensions
{
    using System;
    using System.ComponentModel;
    using System.Globalization;
    using System.Reflection;

    /// <summary>
    /// Предоставляет расширения для работы с перечислениями и компараторами строк в .NET.
    /// <para>
    /// Класс содержит методы расширения для преобразования между значениями <see cref="StringComparison" /> и объектами <see cref="StringComparer" />.
    /// Это позволяет удобно и безопасно согласовывать режимы сравнения строк между различными API .NET, а также использовать стандартные компараторы
    /// в коллекциях, LINQ-запросах и других сценариях, где требуется явное указание способа сравнения строк.
    /// </para><b>Основные возможности:</b><list type="bullet"><item>— Преобразование <see cref="StringComparison" /> в соответствующий <see cref="StringComparer" /> для использования в коллекциях и алгоритмах.</item><item>— Обратное преобразование <see cref="StringComparer" /> в <see cref="StringComparison" /> для унификации логики сравнения строк.</item></list><b>Типовые сценарии использования:</b><list type="bullet"><item>— Унификация сравнения строк в пользовательских коллекциях и при работе с LINQ.</item><item>— Передача режима сравнения между различными библиотеками и компонентами .NET.</item><item>— Упрощение кода при необходимости динамического выбора способа сравнения строк.</item></list><b>Потокобезопасность:</b> Методы класса являются статическими и не содержат состояния, что делает их безопасными для многопоточного использования.
    /// </summary>
    public static class EnumExtensions
    {
        /// <summary>
        /// The current culture.
        /// </summary>
        private static readonly StringComparer CurrentCulture = StringComparer.CurrentCulture;

        /// <summary>
        /// The current culture ignore case.
        /// </summary>
        private static readonly StringComparer CurrentCultureIgnoreCase = StringComparer.CurrentCultureIgnoreCase;

        /// <summary>
        /// The invariant culture.
        /// </summary>
        private static readonly StringComparer InvariantCulture = StringComparer.InvariantCulture;

        /// <summary>
        /// The invariant culture ignore case.
        /// </summary>
        private static readonly StringComparer InvariantCultureIgnoreCase = StringComparer.InvariantCultureIgnoreCase;

        /// <summary>
        /// The ordinal.
        /// </summary>
        private static readonly StringComparer Ordinal = StringComparer.Ordinal;

        /// <summary>
        /// The ordinal ignore case.
        /// </summary>
        private static readonly StringComparer OrdinalIgnoreCase = StringComparer.OrdinalIgnoreCase;

        /// <summary>
        /// Возвращает текстовое описание значения перечисления.
        /// </summary>
        /// <param name="enumValue">
        /// Значение перечисления, для которого требуется получить описание.
        /// </param>
        /// <returns>
        /// Значение атрибута <c>Description</c>, связанного с элементом перечисления,
        /// либо имя элемента перечисления, если описание отсутствует.
        /// Если имя элемента определить невозможно, возвращается результат
        /// вызова <see cref="Enum.ToString()"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Выбрасывается, если <paramref name="enumValue"/> равен <c>null</c>.
        /// </exception>
        /// <remarks>
        /// Метод использует кэш метаданных для получения описания,
        /// что позволяет избежать повторного отражения (reflection).
        /// </remarks>
        public static string GetDescription(this Enum enumValue)
        {
            if (enumValue == null)
                throw new ArgumentNullException(nameof(enumValue));

            var name = Enum.GetName(enumValue.GetType(), enumValue);
            if (name == null)
                return enumValue.ToString();

            var cache = MemberCache.Create(enumValue.GetType()).GetField(name);
            return string.IsNullOrEmpty(cache.Description) ? name : cache.Description;
        }

        /// <summary>
        /// Возвращает отображаемое имя значения перечисления.
        /// </summary>
        /// <param name="enumValue">
        /// Значение перечисления, для которого требуется получить отображаемое имя.
        /// </param>
        /// <returns>
        /// Значение атрибута <c>DisplayName</c>, связанного с элементом перечисления,
        /// либо имя элемента перечисления, если отображаемое имя отсутствует.
        /// Если имя элемента определить невозможно, возвращается результат
        /// вызова <see cref="Enum.ToString()"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Выбрасывается, если <paramref name="enumValue"/> равен <c>null</c>.
        /// </exception>
        /// <remarks>
        /// Метод предназначен для использования в UI-слоях,
        /// где требуется человекочитаемое представление значений перечислений.
        /// </remarks>
        public static string GetDisplayName(this Enum enumValue)
        {
            if (enumValue == null)
                throw new ArgumentNullException(nameof(enumValue));

            var name = Enum.GetName(enumValue.GetType(), enumValue);
            if (name == null)
                return enumValue.ToString();

            var cache = MemberCache.Create(enumValue.GetType()).GetField(name);
            return string.IsNullOrEmpty(cache.DisplayName) ? name : cache.DisplayName;
        }

        /// <summary>
        /// Преобразует значение <see cref="StringComparison" /> в эквивалентный объект <see cref="StringComparer" />.
        /// <para>
        /// Позволяет использовать стандартные режимы сравнения строк .NET для получения соответствующего компаратора строк,
        /// который можно применять, например, в LINQ, коллекциях или при сравнении строк вручную.
        /// </para>
        /// </summary>
        /// <param name="comparison">Значение <see cref="StringComparison" />, определяющее способ сравнения строк.</param>
        /// <returns>Эквивалентный объект <see cref="StringComparer" />.</returns>
        /// <exception cref="System.ArgumentException">Invalid StringComparison value - comparison.</exception>
        public static StringComparer ToStringComparer(this StringComparison comparison)
        {
            switch (comparison)
            {
                case StringComparison.CurrentCulture:
                    return CurrentCulture;

                case StringComparison.CurrentCultureIgnoreCase:
                    return CurrentCultureIgnoreCase;

                case StringComparison.InvariantCulture:
                    return InvariantCulture;

                case StringComparison.InvariantCultureIgnoreCase:
                    return InvariantCultureIgnoreCase;

                case StringComparison.Ordinal:
                    return Ordinal;

                case StringComparison.OrdinalIgnoreCase:
                    return OrdinalIgnoreCase;

                default:
                    throw new ArgumentException(@"Invalid StringComparison value", nameof(comparison));
            }
        }

        /// <summary>
        /// Преобразует объект <see cref="StringComparer" /> в эквивалентное значение <see cref="StringComparison" />.
        /// <para>
        /// Используется для получения режима сравнения строк, соответствующего заданному компаратору.
        /// Это полезно при необходимости согласовать поведение сравнения строк между различными API .NET.
        /// </para>
        /// </summary>
        /// <param name="comparer">Объект <see cref="StringComparer" />, который требуется преобразовать.</param>
        /// <returns>Эквивалентное значение <see cref="StringComparison" />.</returns>
        /// <exception cref="System.ArgumentException">Неизвестный StringComparer - comparer.</exception>
        public static StringComparison ToStringComparison(this StringComparer comparer)
        {
            if (comparer == null)
                throw new ArgumentNullException(nameof(comparer));

            if (ReferenceEquals(comparer, StringComparer.Ordinal))
                return StringComparison.Ordinal;

            if (ReferenceEquals(comparer, StringComparer.OrdinalIgnoreCase))
                return StringComparison.OrdinalIgnoreCase;

            if (comparer.Equals(StringComparer.CurrentCulture))
                return StringComparison.CurrentCulture;

            if (comparer.Equals(StringComparer.CurrentCultureIgnoreCase))
                return StringComparison.CurrentCultureIgnoreCase;

            if (comparer.Equals(StringComparer.InvariantCulture))
                return StringComparison.InvariantCulture;

            if (comparer.Equals(StringComparer.InvariantCultureIgnoreCase))
                return StringComparison.InvariantCultureIgnoreCase;

            throw new NotSupportedException(
                $"StringComparer '{comparer.GetType().FullName}' cannot be converted to StringComparison.");
        }
    }
}