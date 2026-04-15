// <copyright file="TypeMappingInfo.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace System.Data
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;

    /// <summary>
    /// Представляет метаданные сопоставления (mapping) сущности
    /// с таблицей базы данных.
    /// </summary>
    internal sealed class TypeMappingInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TypeMappingInfo"/> class.
        /// </summary>
        /// <param name="entityType">
        /// Тип сущности, для которой создаётся описание сопоставления.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Выбрасывается, если <paramref name="entityType"/> равен <c>null</c>.
        /// </exception>
        internal TypeMappingInfo(Type entityType)
        {
            this.EntityType = entityType ?? throw new ArgumentNullException(nameof(entityType));
            this.PropertyMap = [];
        }

        /// <summary>
        /// Получает тип сущности, для которой описано сопоставление.
        /// </summary>
        public Type EntityType { get; }

        /// <summary>
        /// Получает коллекцию сопоставлений свойств сущности с колонками таблицы.
        /// </summary>
        /// <remarks>
        /// Ключом словаря является <see cref="PropertyInfo"/> свойства сущности,
        /// значением — объект <see cref="PropertyMappingInfo"/>, описывающий
        /// параметры сопоставления с колонкой.
        /// </remarks>
        public IDictionary<PropertyInfo, PropertyMappingInfo> PropertyColumns => this.PropertyMap;

        /// <summary>
        /// Получает или задаёт имя схемы базы данных,
        /// в которой расположена таблица сущности.
        /// </summary>
        public string Schema { get; internal set; }

        /// <summary>
        /// Получает или задаёт имя таблицы базы данных,
        /// соответствующей данной сущности.
        /// </summary>
        public string TableName { get; internal set; }

        /// <summary>
        /// Внутреннее хранилище сопоставлений свойств с колонками.
        /// </summary>
        internal Dictionary<PropertyInfo, PropertyMappingInfo> PropertyMap { get; }
    }
}