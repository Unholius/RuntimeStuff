// <copyright file="XmlHelper.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace RuntimeStuff.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml.Linq;

    /// <summary>
    /// Вспомогательный класс для работы с XML-данными.
    /// </summary>
    public static class XmlHelper
    {
        /// <summary>
        /// Извлекает наборы атрибутов всех XML-узлов с указанным именем.
        /// </summary>
        /// <param name="xml">
        /// Строка, содержащая XML-документ.
        /// </param>
        /// <param name="nodeName">
        /// Локальное имя XML-узлов, атрибуты которых необходимо получить.
        /// </param>
        /// <returns>
        /// Массив словарей, где каждый словарь содержит атрибуты одного узла:
        /// ключ — локальное имя атрибута, значение — значение атрибута.
        /// Если входные параметры некорректны, XML не удалось разобрать
        /// или подходящие узлы отсутствуют, возвращается пустой массив.
        /// </returns>
        public static Dictionary<string, string>[] GetAttributes(string xml, string nodeName)
        {
            if (string.IsNullOrWhiteSpace(xml) || string.IsNullOrWhiteSpace(nodeName))
            {
                return Array.Empty<Dictionary<string, string>>();
            }

            try
            {
                var doc = XDocument.Parse(xml);
                return doc.Descendants()
                    .Where(x => x.Name.LocalName == nodeName)
                    .Select(x => x.Attributes().ToDictionary(k => k.Name.LocalName, v => v.Value))
                    .ToArray();
            }
            catch (Exception)
            {
                return Array.Empty<Dictionary<string, string>>();
            }
        }

        /// <summary>
        /// Извлекает наборы атрибутов узлов с указанным именем
        /// из предварительно отфильтрованных XML-фрагментов.
        /// </summary>
        /// <param name="xml">
        /// Строка, содержащая XML-документ.
        /// </param>
        /// <param name="attributesNodeName">
        /// Локальное имя XML-узлов, атрибуты которых необходимо извлечь.
        /// </param>
        /// <param name="contentNodeName">
        /// Локальное имя узлов, содержимое которых используется
        /// как источник для дальнейшего поиска атрибутов.
        /// </param>
        /// <param name="contentFilter">
        /// Фильтр, применяемый к XML-содержимому узлов
        /// с именем <paramref name="contentNodeName"/>.
        /// </param>
        /// <returns>
        /// Массив словарей атрибутов узлов с именем
        /// <paramref name="attributesNodeName"/>.
        /// Если подходящие элементы отсутствуют, возвращается пустой массив.
        /// </returns>
        public static Dictionary<string, string>[] GetAttributes(string xml, string attributesNodeName, string contentNodeName, Func<string, bool> contentFilter)
        {
            var contents = GetContents(xml, contentNodeName, contentFilter);
            return contents.SelectMany(x => GetAttributes(x, attributesNodeName)).ToArray();
        }

        /// <summary>
        /// Извлекает XML-представление узлов с указанным именем
        /// из XML-документа в виде строк.
        /// </summary>
        /// <param name="xml">
        /// Строка, содержащая XML-документ.
        /// </param>
        /// <param name="nodeName">
        /// Локальное имя XML-узлов, содержимое которых необходимо получить.
        /// </param>
        /// <param name="contentFilter">
        /// Необязательный фильтр, применяемый к строковому содержимому узла
        /// (включая сам тег и вложенные элементы).
        /// </param>
        /// <returns>
        /// Массив строк, содержащих полное XML-содержимое найденных узлов.
        /// Если входные параметры некорректны, XML не удалось разобрать
        /// или подходящие узлы отсутствуют, возвращается пустой массив.
        /// </returns>
        public static string[] GetContents(string xml, string nodeName, Func<string, bool> contentFilter = null)
        {
            if (string.IsNullOrWhiteSpace(xml) || string.IsNullOrWhiteSpace(nodeName))
            {
                return Array.Empty<string>();
            }

            try
            {
                var doc = XDocument.Parse(xml);
                return doc.Descendants()
                    .Where(x => x.Name.LocalName == nodeName && (contentFilter == null || contentFilter(x.ToString())))
                    .Select(x => x.ToString())
                    .ToArray();
            }
            catch (Exception)
            {
                return Array.Empty<string>();
            }
        }

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

        /// <summary>
        /// Извлекает значения узлов с указанным именем из дочерних XML-фрагментов,
        /// предварительно отфильтрованных по содержимому.
        /// </summary>
        /// <param name="xml">
        /// Строка, содержащая XML-документ.
        /// </param>
        /// <param name="valueNodeName">
        /// Локальное имя узлов, значения которых необходимо извлечь
        /// из найденных XML-фрагментов.
        /// </param>
        /// <param name="contentNodeName">
        /// Локальное имя узлов, содержимое которых используется
        /// как источник для дальнейшего поиска значений.
        /// </param>
        /// <param name="contentFilter">
        /// Фильтр, применяемый к XML-содержимому узлов
        /// с именем <paramref name="contentNodeName"/>.
        /// </param>
        /// <returns>
        /// Массив строк, содержащих значения найденных узлов
        /// с именем <paramref name="valueNodeName"/>.
        /// Если подходящие элементы отсутствуют, возвращается пустой массив.
        /// </returns>
        public static string[] GetValues(string xml, string valueNodeName, string contentNodeName, Func<string, bool> contentFilter)
        {
            var contents = GetContents(xml, contentNodeName, contentFilter);
            return contents.SelectMany(x => GetValues(x, valueNodeName)).ToArray();
        }
    }
}