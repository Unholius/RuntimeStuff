using System;

namespace RuntimeStuff.Extensions
{
    /// <summary>
    /// Полезные расширения для работы со строками
    /// </summary>
    public static class StringExtensions
    {
        /// <summary>
        /// Метод удаляет указанный суффикс с конца строки, если он существует
        /// </summary>
        /// <param name="s">Исходная строка, из которой нужно удалить суффикс</param>
        /// <param name="subStr">Строка-суффикс, которую нужно удалить с конца</param>
        /// <param name="comparison">Тип сравнения строк при проверке суффикса</param>
        /// <returns>Строка без указанного суффикса в конце, если он был найден</returns>
        /// <remarks>
        /// Метод проверяет заканчивается ли исходная строка указанным суффиксом.
        /// Если суффикс найден, возвращается строка без этого суффикса.
        /// Если суффикс не найден или параметры пустые, возвращается исходная строка.
        /// </remarks>
        public static string TrimEnd(this string s, string subStr, StringComparison comparison = StringComparison.Ordinal)
        {
            // Проверка на пустые входные данные
            if (string.IsNullOrEmpty(s) || string.IsNullOrEmpty(subStr))
                return s;

            // Проверка наличия суффикса в конце строки
            if (s.EndsWith(subStr, comparison))
                // Возвращаем строку без суффикса
                return s.Substring(0, s.Length - subStr.Length);

            // Если суффикс не найден, возвращаем исходную строку
            return s;
        }


        /// <summary>
        /// Попытка преобразовать строку в целое число
        /// </summary>
        /// <param name="s">Строка</param>
        /// <returns></returns>
        public static int? ToInt(this string s)
        {
            return Obj.TryChangeType<int?>(s, out var i) ? i : null;
        }

        /// <summary>
        /// Возвращает подстроку от startIndex до endIndex включительно.
        /// Если индексы выходят за границы строки — они корректируются.
        /// Если startIndex > endIndex — возвращается инвертированная подстрока.
        /// </summary>
        /// <param name="s">Исходная строка.</param>
        /// <param name="startIndex">Начальный индекс (включительно).</param>
        /// <param name="endIndex">Конечный индекс (включительно).</param>
        /// <returns>Подстрока в указанном диапазоне, либо инвертированная подстрока, если startIndex > endIndex.</returns>
        public static string SubStr(this string s, int startIndex, int endIndex)
        {
            if (string.IsNullOrEmpty(s))
                return string.Empty;

            // Приведение индексов в допустимые границы
            int minIndex = 0;
            int maxIndex = s.Length - 1;

            startIndex = Math.Max(minIndex, Math.Min(startIndex, maxIndex));
            endIndex = Math.Max(minIndex, Math.Min(endIndex, maxIndex));

            if (startIndex <= endIndex)
            {
                int length = endIndex - startIndex + 1;
                return s.Substring(startIndex, length);
            }
            else
            {
                // startIndex > endIndex → берём подстроку в обратном порядке
                int length = startIndex - endIndex + 1;
                var slice = s.Substring(endIndex, length);
                // Инвертируем
                char[] chars = slice.ToCharArray();
                Array.Reverse(chars);
                return new string(chars);
            }
        }

        /// <summary>
        /// Генерирует уникальное имя на основе исходного имени, добавляя числовой суффикс,
        /// если имя уже существует в системе.
        /// </summary>
        /// <param name="name">Исходное имя, для которого нужно сгенерировать уникальную версию</param>
        /// <param name="nameExists">Делегат, проверяющий существование имени в системе</param>
        /// <param name="nameTemplate">Шаблон форматирования для генерации нового имени. 
        /// {0} - исходное имя, {1} - числовой суффикс</param>
        /// <returns>Уникальное имя, которое не существует в системе</returns>
        /// <remarks>
        /// Метод использует простой алгоритм генерации уникальных имен:
        /// 1. Начинает с исходного имени
        /// 2. Если имя уже существует, добавляет числовой суффикс, начиная с 1
        /// 3. Увеличивает суффикс до тех пор, пока не найдет уникальное имя
        /// Пример использования:
        /// var uniqueName = "file".GetUniqueName(CheckIfNameExists);
        /// где CheckIfNameExists - метод, проверяющий существование имени
        /// </remarks>
        public static string GetUniqueName(this string name, Func<string, bool> nameExists, string nameTemplate = "{0}_{1}")
        {
            int idx = 1;                         // Счетчик для генерации суффикса
            string tmp = name;                   // Временная переменная для хранения текущего имени
            bool exists = nameExists(tmp);       // Проверка существования текущего имени

            while (exists)
            {
                // Формируем новое имя по шаблону с увеличением счетчика
                tmp = string.Format(nameTemplate, name, idx);
                exists = nameExists(tmp);        // Проверяем существование нового имени
                idx++;                          // Увеличиваем счетчик
            }

            return tmp;                         // Возвращаем уникальное имя
        }

    }
}
