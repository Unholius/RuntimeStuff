// <copyright file="XmlHelper.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace RuntimeStuff.Helpers
{
    using System;
    using System.Linq;
    using System.Xml.Linq;

    /// <summary>
    /// Вспомогательный класс для работы с XML-данными.
    /// </summary>
    public static class XmlHelper
    {
        /// <summary>
        /// Извлекает значения всех XML-узлов с указанным именем.
        /// </summary>
        /// <param name="xml">
        /// Строка, содержащая XML-документ.
        /// </param>
        /// <param name="nodeName">
        /// Локальное имя XML-узла, значения которого необходимо получить.
        /// </param>
        /// <returns>
        /// Массив строк, содержащих значения найденных узлов.
        /// Если входные параметры некорректны или произошла ошибка разбора XML,
        /// возвращается пустой массив.
        /// </returns>
        public static string[] GetValues(string xml, string nodeName)
        {
            if (string.IsNullOrWhiteSpace(xml) || string.IsNullOrWhiteSpace(nodeName))
            {
                return Array.Empty<string>();
            }

            try
            {
                var doc = XDocument.Parse(xml);
                return doc.Descendants()
                    .Where(x => x.Name.LocalName == nodeName)
                    .Select(x => x.Value)
                    .ToArray();
            }
            catch (Exception)
            {
                return Array.Empty<string>();
            }
        }
    }
}