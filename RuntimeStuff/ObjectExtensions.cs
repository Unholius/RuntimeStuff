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
        public static void ImportFromJson<T>(this T obj, string json)
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
        /// <remarks>
        /// Использует кэш метаданных (<c>MemberCache</c>) для повышения производительности.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// Может возникнуть, если <paramref name="source"/> равен null.
        /// </exception>
        public static object[] GetValues<TObject>(this TObject source, params string[] memberNames)
            where TObject : class
        {
            var values = new List<object>();
            var sourceTypeCache = MemberCache.Create(typeof(TObject));
            var props = memberNames.Length != 0 ? sourceTypeCache.Properties.Where(x => memberNames.Contains(x.Name)).ToArray() : sourceTypeCache.PublicProperties;
            foreach (var p in props)
            {
                values.Add(p.Getter?.Invoke(source));
            }

            return values.ToArray();
        }

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
            where TObject : class => GetValues(source, memberNames).Select(x => Obj.ChangeType<TValue>(x)).ToArray();

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
        /// <exception cref="ArgumentNullException">source.</exception>
        /// <exception cref="ArgumentNullException">targetination.</exception>
        /// <exception cref="InvalidOperationException">Targetination collection is not IList and cannot add new items.</exception>
        /// <remarks>Если оба параметра <paramref name="source" /> и <paramref name="target" />
        /// являются коллекциями (кроме строк), метод копирует значения для каждого соответствующего элемента коллекции.
        /// При необходимости новые элементы добавляются в целевую коллекцию. Копирование выполняется только по
        /// указанным именам членов или по всем свойствам, если имена не заданы.</remarks>
        public static void Copy<TSource, TTarget>(this TSource source, TTarget target, params string[] memberNames)
            where TSource : class
            where TTarget : class
        {
            if (source == null || typeof(TSource) == typeof(string))
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (target == null || typeof(TTarget) == typeof(string))
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (memberNames == null || memberNames.Length == 0)
            {
                memberNames = Obj.GetPropertyNames(source.GetType());
            }

            var sourceTypeCache = MemberCache.Create(source.GetType());
            if (sourceTypeCache.IsCollection)
            {
                sourceTypeCache = MemberCache.Create(sourceTypeCache.ElementType);
            }

            var targetTypeCache = MemberCache.Create(target.GetType());
            if (targetTypeCache.IsCollection)
            {
                targetTypeCache = MemberCache.Create(targetTypeCache.ElementType);
            }

            if (source is IEnumerable srcList && !(source is string) && target is IEnumerable dstList && !(target is string))
            {
                var srcEnumerator = srcList.GetEnumerator();
                var dstEnumerator = dstList.GetEnumerator();
                var dstListChanged = false;
                while (srcEnumerator.MoveNext())
                {
                    var srcItem = srcEnumerator.Current;
                    object dstItem;

                    if (!dstListChanged && dstEnumerator.MoveNext())
                    {
                        dstItem = dstEnumerator.Current;
                    }
                    else
                    {
                        dstItem = sourceTypeCache.DefaultConstructor();
                        if (dstList is IList dstIList)
                        {
                            dstListChanged = true;
                            dstIList.Add(dstItem);
                        }
                        else
                        {
                            throw new InvalidOperationException("Targetination collection is not IList and cannot add new items.");
                        }
                    }

                    Copy(srcItem, dstItem);
                }

                if (srcEnumerator is IDisposable disposableSrc)
                {
                    disposableSrc.Dispose();
                }

                if (dstEnumerator is IDisposable disposableDst)
                {
                    disposableDst.Dispose();
                }
            }
            else
            {
                foreach (var memberName in memberNames)
                {
                    var get = sourceTypeCache[memberName]?.Getter;
                    if (get == null)
                    {
                        continue;
                    }

                    var set = targetTypeCache[memberName]?.Setter;
                    if (set == null)
                    {
                        continue;
                    }

                    var value = get(source);
                    set(target, value);
                }
            }
        }
    }
}