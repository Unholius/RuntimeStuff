// <copyright file="StringPool.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace System
{
    using System.Collections.Generic;

    /// <summary>
    /// Класс для управления пулом строк, который позволяет интернировать строки и уменьшать использование памяти за счет повторного использования одинаковых строк. Пул строк может быть глобальным или локальным, в зависимости от потребностей приложения.
    /// </summary>
    public sealed class StringPool
    {
        private readonly Dictionary<string, string> map =
            new(StringComparer.Ordinal);

        /// <summary>
        /// Глобальный пул строк, который может использоваться в любом месте приложения для интернирования строк. Это позволяет уменьшить использование памяти за счет повторного использования одинаковых строк.
        /// </summary>
        public static StringPool Global { get; } = new StringPool();

        /// <summary>
        /// Возвращает интернированную строку, которая эквивалентна заданной строке. Если заданная строка уже была интернирована, возвращается существующая строка из пула. Если заданная строка не была интернирована, она добавляется в пул и возвращается.
        /// </summary>
        /// <param name="value">Значение.</param>
        /// <returns>Интернированная строка.</returns>
        public string Intern(string value)
        {
            if (value == null)
            {
                return null;
            }

            if (this.map.TryGetValue(value, out var existing))
            {
                return existing;
            }

            this.map[value] = value;
            return value;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"StringPool ({this.map.Count})";
        }
    }
}