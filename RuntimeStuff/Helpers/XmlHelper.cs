// <copyright file="XmlHelper.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace RuntimeStuff.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Xml;
    using System.Xml.Linq;
    using System.Xml.Serialization;

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
        public static Dictionary<string, string>[] GetAttributes(string xml, string nodeName) => GetAttributes(xml, x => x == nodeName);

        /// <summary>
        /// Извлекает наборы атрибутов всех XML-узлов с указанным именем.
        /// </summary>
        /// <param name="xml">
        /// Строка, содержащая XML-документ.
        /// </param>
        /// <param name="nodeNameSelector">
        /// Локальное имя XML-узлов, атрибуты которых необходимо получить.
        /// </param>
        /// <returns>
        /// Массив словарей, где каждый словарь содержит атрибуты одного узла:
        /// ключ — локальное имя атрибута, значение — значение атрибута.
        /// Если входные параметры некорректны, XML не удалось разобрать
        /// или подходящие узлы отсутствуют, возвращается пустой массив.
        /// </returns>
        public static Dictionary<string, string>[] GetAttributes(string xml, Func<string, bool> nodeNameSelector)
        {
            if (string.IsNullOrWhiteSpace(xml) || nodeNameSelector == null)
            {
                return Array.Empty<Dictionary<string, string>>();
            }

            try
            {
                var doc = XDocument.Parse(xml);
                return doc.Descendants()
                    .Where(x => nodeNameSelector(x.Name.LocalName))
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
        public static Dictionary<string, string>[] GetAttributes(string xml, string attributesNodeName, string contentNodeName, Func<string, bool> contentFilter) => GetAttributes(xml, x => x == attributesNodeName, x => x == contentNodeName, contentFilter);

        /// <summary>
        /// Извлекает наборы атрибутов узлов с указанным именем
        /// из предварительно отфильтрованных XML-фрагментов.
        /// </summary>
        /// <param name="xml">
        /// Строка, содержащая XML-документ.
        /// </param>
        /// <param name="attributesNodeNameSelector">
        /// Локальное имя XML-узлов, атрибуты которых необходимо извлечь.
        /// </param>
        /// <param name="contentNodeNameSelector">
        /// Локальное имя узлов, содержимое которых используется
        /// как источник для дальнейшего поиска атрибутов.
        /// </param>
        /// <param name="contentFilter">
        /// Фильтр, применяемый к XML-содержимому узлов
        /// с именем <paramref name="contentNodeNameSelector"/>.
        /// </param>
        /// <returns>
        /// Массив словарей атрибутов узлов с именем
        /// <paramref name="attributesNodeNameSelector"/>.
        /// Если подходящие элементы отсутствуют, возвращается пустой массив.
        /// </returns>
        public static Dictionary<string, string>[] GetAttributes(string xml, Func<string, bool> attributesNodeNameSelector, Func<string, bool> contentNodeNameSelector, Func<string, bool> contentFilter)
        {
            var contents = GetContents(xml, contentNodeNameSelector, contentFilter);
            return contents.SelectMany(x => GetAttributes(x, attributesNodeNameSelector)).ToArray();
        }

        /// <summary>
        /// Извлекает XML-представление узлов с указанным именем
        /// из XML-документа в виде строк.
        /// </summary>
        /// <param name="xml">
        /// Строка, содержащая XML-документ.
        /// </param>
        /// <param name="nodeNameSelector">
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
        public static string[] GetContents(string xml, Func<string, bool> nodeNameSelector, Func<string, bool> contentFilter = null)
        {
            if (string.IsNullOrWhiteSpace(xml) || nodeNameSelector == null)
            {
                return Array.Empty<string>();
            }

            try
            {
                var doc = XDocument.Parse(xml);
                return doc.Descendants()
                    .Where(x => nodeNameSelector(x.Name.LocalName) && (contentFilter == null || contentFilter(x.ToString())))
                    .Select(x => x.ToString())
                    .ToArray();
            }
            catch (Exception)
            {
                return Array.Empty<string>();
            }
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
        public static string[] GetContents(string xml, string nodeName, Func<string, bool> contentFilter = null) => GetContents(xml, x => x == nodeName, contentFilter);

        /// <summary>
        /// Извлекает значения всех XML-узлов с указанным именем.
        /// </summary>
        /// <param name="xml">
        /// Строка, содержащая XML-документ.
        /// </param>
        /// <param name="nodeNameSelector">
        /// Локальное имя XML-узла, значения которого необходимо получить.
        /// </param>
        /// <returns>
        /// Массив строк, содержащих значения найденных узлов.
        /// Если входные параметры некорректны или произошла ошибка разбора XML,
        /// возвращается пустой массив.
        /// </returns>
        public static string[] GetValues(string xml, Func<string, bool> nodeNameSelector)
        {
            if (string.IsNullOrWhiteSpace(xml) || nodeNameSelector == null)
            {
                return Array.Empty<string>();
            }

            try
            {
                var doc = XDocument.Parse(xml);
                return doc.Descendants()
                    .Where(x => nodeNameSelector(x.Name.LocalName))
                    .Select(x => x.Value)
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
        public static string[] GetValues(string xml, string nodeName) => GetValues(xml, x => x == nodeName);

        /// <summary>
        /// Извлекает значения узлов с указанным именем из дочерних XML-фрагментов,
        /// предварительно отфильтрованных по содержимому.
        /// </summary>
        /// <param name="xml">
        /// Строка, содержащая XML-документ.
        /// </param>
        /// <param name="valueNodeNameSelector">
        /// Локальное имя узлов, значения которых необходимо извлечь
        /// из найденных XML-фрагментов.
        /// </param>
        /// <param name="contentNodeNameSelector">
        /// Локальное имя узлов, содержимое которых используется
        /// как источник для дальнейшего поиска значений.
        /// </param>
        /// <param name="contentFilter">
        /// Фильтр, применяемый к XML-содержимому узлов
        /// с именем <paramref name="contentNodeNameSelector"/>.
        /// </param>
        /// <returns>
        /// Массив строк, содержащих значения найденных узлов
        /// с именем <paramref name="valueNodeNameSelector"/>.
        /// Если подходящие элементы отсутствуют, возвращается пустой массив.
        /// </returns>
        public static string[] GetValues(string xml, Func<string, bool> valueNodeNameSelector, Func<string, bool> contentNodeNameSelector, Func<string, bool> contentFilter)
        {
            var contents = GetContents(xml, contentNodeNameSelector, contentFilter);
            return contents.SelectMany(x => GetValues(x, valueNodeNameSelector)).ToArray();
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
            => GetValues(xml, x => x == valueNodeName, x => x == contentNodeName, contentFilter);

        /// <summary>
        /// Сериализует указанный объект в его XML-представление.
        /// </summary>
        /// <remarks>Для успешной сериализации все публичные свойства и поля объекта должны быть доступны
        /// для XmlSerializer. Если объект содержит вложенные объекты, они также должны быть сериализуемыми.</remarks>
        /// <param name="obj">Объект для сериализации. Не может быть равен null. Тип объекта должен поддерживать сериализацию с помощью
        /// XmlSerializer.</param>
        /// <param name="includeNamespace">Включать пространство имен.</param>
        /// <param name="propertiesAsAttributes">Сериализовать простые свойства как атрибуты.</param>
        /// <param name="writeIndent">Использовать форматирование строк.</param>
        /// <returns>Строка, содержащая XML-представление указанного объекта.</returns>
        public static string Serialize(
            object obj,
            bool includeNamespace = false,
            bool propertiesAsAttributes = true,
            bool writeIndent = true)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));

            var type = obj.GetType();

            XmlSerializer serializer;

            if (propertiesAsAttributes)
            {
                var overrides = new XmlAttributeOverrides();

                foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    // Атрибутами имеет смысл делать только простые типы
                    if (!prop.CanRead || !IsSimpleType(prop.PropertyType))
                        continue;

                    var attrs = new XmlAttributes
                    {
                        XmlAttribute = new XmlAttributeAttribute(prop.Name),
                    };

                    overrides.Add(type, prop.Name, attrs);
                }

                serializer = new XmlSerializer(type, overrides);
            }
            else
            {
                serializer = new XmlSerializer(type);
            }

            var namespaces = new XmlSerializerNamespaces();
            if (!includeNamespace)
            {
                namespaces.Add(string.Empty, string.Empty);
            }

            var xDoc = new XDocument();
            using (var writer = xDoc.CreateWriter())
            {
                serializer.Serialize(writer, obj, namespaces);
            }

            return xDoc.ToString(writeIndent ? SaveOptions.None : SaveOptions.DisableFormatting);
        }

        private static bool IsSimpleType(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;

            return type.IsPrimitive
                || type.IsEnum
                || type == typeof(string)
                || type == typeof(decimal)
                || type == typeof(DateTime)
                || type == typeof(Guid);
        }
    }
}