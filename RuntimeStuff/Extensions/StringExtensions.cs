using System;




/// <summary>
///     Предоставляет набор методов-расширений для работы со строками, включая замену, удаление и обрезку подстрок, а
///     также удаление суффикса.
/// </summary>
/// <remarks>
///     Класс содержит статические методы-расширения для типа <see cref="string" />, позволяющие
///     выполнять типовые операции над строками с использованием диапазонов индексов и сравнения суффиксов. Методы
///     предназначены для упрощения манипуляций со строками в пользовательском коде. Все методы не изменяют исходную
///     строку, а возвращают новую строку с применёнными изменениями.
/// </remarks>
public static class StringExtensions
{
    /// <summary>
    ///     Заменяет часть строки в диапазоне [startIndex..endIndex] на указанную строку.
    /// </summary>
    /// <param name="s">Исходная строка.</param>
    /// <param name="startIndex">Начальная позиция (включительно).</param>
    /// <param name="endIndex">Конечная позиция (включительно).</param>
    /// <param name="replaceString">Строка для замены.</param>
    /// <returns>Новая строка с заменой.</returns>
    public static string Replace(this string s, int startIndex, int endIndex, string replaceString)
    {
        return StringHelper.Replace(s, startIndex, endIndex, replaceString);
    }

    /// <summary>
    ///     Удаляет часть строки в диапазоне [startIndex..endIndex]. Работает как s.Substring(0, startIndex) +
    ///     s.Substring(endIndex + 1);
    /// </summary>
    /// <param name="s">Исходная строка.</param>
    /// <param name="startIndex">Начальная позиция (включительно).</param>
    /// <param name="endIndex">Конечная позиция (включительно).</param>
    public static string Cut(this string s, int startIndex, int endIndex)
    {
        return StringHelper.Cut(s, startIndex, endIndex);
    }

    /// <summary>
    ///     Возвращает часть строки в диапазоне [startIndex..endIndex]. Работает как string.Substring(s, startIndex, endIndex -
    ///     startIndex + 1)
    /// </summary>
    /// <param name="s">Исходная строка.</param>
    /// <param name="startIndex">Начальная позиция (включительно).</param>
    /// <param name="endIndex">Конечная позиция (включительно).</param>
    public static string Crop(this string s, int startIndex, int endIndex)
    {
        return StringHelper.Crop(s, startIndex, endIndex);
    }

    /// <summary>
    ///     Метод удаляет указанный суффикс с конца строки, если он существует
    /// </summary>
    /// <param name="s">Исходная строка, из которой нужно удалить суффикс</param>
    /// <param name="subStr">Строка-суффикс, которую нужно удалить с конца</param>
    /// <param name="comparison">Тип сравнения строк при проверке суффикса</param>
    /// <returns>Строка без указанного суффикса в конце, если он был найден</returns>
    /// <remarks>
    ///     Метод проверяет заканчивается ли исходная строка указанным суффиксом.
    ///     Если суффикс найден, возвращается строка без этого суффикса.
    ///     Если суффикс не найден или параметры пустые, возвращается исходная строка.
    /// </remarks>
    public static string TrimEnd(this string s, string subStr, StringComparison comparison = StringComparison.Ordinal)
    {
        return StringHelper.TrimEnd(s, subStr, comparison);
    }
}