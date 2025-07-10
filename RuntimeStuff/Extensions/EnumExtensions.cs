using System;
using System.Collections.Generic;
using System.Text;

namespace RuntimeStuff.Extensions
{
    public static class EnumExtensions
    {
        /// <summary>
        /// Converts a StringComparison value to a corresponding StringComparer.
        /// </summary>
        /// <param name="comparison">The StringComparison value to convert.</param>
        /// <returns>The corresponding StringComparer.</returns>
        /// <exception cref="ArgumentException">Thrown when the StringComparison value is invalid.</exception>
        public static StringComparer ToStringComparer(this StringComparison comparison)
        {
            switch (comparison)
            {
                case StringComparison.CurrentCulture:
                    return StringComparer.CurrentCulture;
                case StringComparison.CurrentCultureIgnoreCase:
                    return StringComparer.CurrentCultureIgnoreCase;
                case StringComparison.InvariantCulture:
                    return StringComparer.InvariantCulture;
                case StringComparison.InvariantCultureIgnoreCase:
                    return StringComparer.InvariantCultureIgnoreCase;
                case StringComparison.Ordinal:
                    return StringComparer.Ordinal;
                case StringComparison.OrdinalIgnoreCase:
                    return StringComparer.OrdinalIgnoreCase;
                default:
                    throw new ArgumentException("Invalid StringComparison value", nameof(comparison));
            }
        }

        /// <summary>
        /// Конвертирует объект StringComparer в эквивалентное значение StringComparison.
        /// </summary>
        /// <param name="comparer">Объект StringComparer для конвертации.</param>
        /// <returns>Эквивалентное значение StringComparison.</returns>
        /// <exception cref="ArgumentException">Выбрасывается, если передан неизвестный StringComparer.</exception>
        public static StringComparison ToStringComparison(this StringComparer comparer)
        {
            if (comparer == StringComparer.Ordinal)
                return StringComparison.Ordinal;
            if (comparer == StringComparer.OrdinalIgnoreCase)
                return StringComparison.OrdinalIgnoreCase;
            if (comparer == StringComparer.CurrentCulture)
                return StringComparison.CurrentCulture;
            if (comparer == StringComparer.CurrentCultureIgnoreCase)
                return StringComparison.CurrentCultureIgnoreCase;
            if (comparer == StringComparer.InvariantCulture)
                return StringComparison.InvariantCulture;
            if (comparer == StringComparer.InvariantCultureIgnoreCase)
                return StringComparison.InvariantCultureIgnoreCase;

            throw new ArgumentException("Неизвестный StringComparer", nameof(comparer));
        }
    }
}
