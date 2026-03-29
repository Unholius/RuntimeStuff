// <copyright file="ObjectExtensions.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace System
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Helpers;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    /// Дополнительные методы для объектов.
    /// </summary>
    public static class ObjectExtensions
    {
        /// <summary>
        /// Сериализует объект в JSON-строку с помощью вспомогательного класса <c>JsonHelper</c>.
        /// </summary>
        /// <typeparam name="T">Тип объекта для сериализации. Должен быть ссылочным типом.</typeparam>
        /// <param name="obj">Объект для сериализации.</param>
        /// <param name="valueFormatter">Сериализатор значений.</param>
        /// <returns>Строка в формате json.</returns>
        public static string ToJson<T>(this T obj, ValueFormatter valueFormatter = null)
            where T : class
                => JsonHelper.Serialize(obj, valueFormatter);

        /// <summary>
        /// Сериализует объект в XML-строку с помощью вспомогательного класса <c>XmlHelper</c>.
        /// </summary>
        /// <typeparam name="T">Тип объекта для сериализации. Должен быть ссылочным типом.</typeparam>
        /// <param name="obj">Объект для сериализации.</param>
        /// <returns>Строка в формате xml.</returns>
        public static string ToXml<T>(this T obj)
            where T : class
            => XmlHelper.Serialize(obj);

        /// <summary>
        /// Импортирует значения из JSON-строки в указанный объект.
        /// </summary>
        /// <typeparam name="T">
        /// Тип объекта, в который будут записаны значения. Должен быть ссылочным типом.
        /// </typeparam>
        /// <param name="obj">
        /// Экземпляр объекта, в который производится импорт данных.
        /// </param>
        /// <param name="json">
        /// JSON-строка, содержащая данные для импорта.
        /// </param>
        /// <remarks>
        /// Метод извлекает все значения из JSON и устанавливает их в объект
        /// по соответствующим ключам с помощью вспомогательных методов.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// Может возникнуть, если <paramref name="obj"/> или <paramref name="json"/> равны null.
        /// </exception>
        public static void FromJson<T>(this T obj, string json)
            where T : class
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj));
            }

            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            var jsonValues = JsonHelper.GetAllValues(json);
            foreach (var value in jsonValues)
            {
                Obj.Set(obj, value.Key, value.Value);
            }
        }

        /// <summary>
        /// Копирует значения указанных членов из исходного объекта в целевой объект. Поддерживает копирование как между
        /// отдельными объектами, так и между коллекциями объектов.
        /// </summary>
        /// <typeparam name="TSource">Тип исходного объекта, из которого копируются значения. Должен быть ссылочным типом.</typeparam>
        /// <typeparam name="TTarget">Тип целевого объекта, в который копируются значения. Должен быть ссылочным типом.</typeparam>
        /// <param name="source">Исходный объект, значения членов которого будут скопированы. Не может быть равен null.</param>
        /// <param name="target">Целевой объект, в который будут скопированы значения членов. Не может быть равен null.</param>
        /// <param name="memberNames">Массив имен членов, которые необходимо скопировать. Если не указан или пуст, копируются все доступные
        /// свойства исходного объекта.</param>
        /// <remarks>Если оба параметра <paramref name="source" /> и <paramref name="target" />
        /// являются коллекциями (кроме строк), метод копирует значения для каждого соответствующего элемента коллекции.
        /// При необходимости новые элементы добавляются в целевую коллекцию. Копирование выполняется только по
        /// указанным именам членов или по всем свойствам, если имена не заданы.</remarks>
        public static void Copy<TSource, TTarget>(this TSource source, TTarget target, params string[] memberNames)
            where TSource : class
            where TTarget : class
                => Obj.Copy(source, target, memberNames);

        /// <summary>
        /// Получает значения указанных свойств объекта.
        /// </summary>
        /// <typeparam name="TObject">Тип исходного объекта.</typeparam>
        /// <param name="source">Объект, из которого извлекаются значения.</param>
        /// <param name="memberNames">
        /// Имена свойств, значения которых необходимо получить.
        /// Если не указаны, будут использованы все публичные свойства.
        /// </param>
        /// <returns>
        /// Массив значений свойств в порядке их выбора.
        /// </returns>
        public static object[] GetValues<TObject>(this TObject source, params string[] memberNames)
            where TObject : class
                => Obj.GetValues(source, memberNames);

        /// <summary>
        /// Получает значения указанных свойств объекта с приведением к заданному типу.
        /// </summary>
        /// <typeparam name="TObject">Тип исходного объекта.</typeparam>
        /// <typeparam name="TValue">Тип, к которому будут приведены значения.</typeparam>
        /// <param name="source">Объект, из которого извлекаются значения.</param>
        /// <param name="memberNames">
        /// Имена свойств, значения которых необходимо получить.
        /// Если не указаны, будут использованы все публичные свойства.
        /// </param>
        /// <returns>
        /// Массив значений свойств, приведённых к типу <typeparamref name="TValue"/>.
        /// </returns>
        /// <remarks>
        /// Для преобразования используется вспомогательный метод <c>Obj.ChangeType&lt;T&gt;</c>.
        /// Если преобразование невозможно, может возникнуть исключение.
        /// </remarks>
        public static TValue[] GetValues<TObject, TValue>(this TObject source, params string[] memberNames)
            where TObject : class
                => Obj.GetValues<TObject, TValue>(source, memberNames);
    }
}