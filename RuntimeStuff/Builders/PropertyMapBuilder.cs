// <copyright file="PropertyMapBuilder.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace RuntimeStuff.Builders
{
    using System;
    using System.Linq.Expressions;
    using System.Reflection;
    using RuntimeStuff.Internal;

    /// <summary>
    /// Fluent-builder для настройки сопоставления свойства сущности
    /// с колонкой таблицы базы данных.
    /// </summary>
    /// <typeparam name="T">
    /// Тип сущности.
    /// </typeparam>
    /// <typeparam name="TProperty">
    /// Тип свойства сущности.
    /// </typeparam>
    public sealed class PropertyMapBuilder<T, TProperty>
    {
        private readonly PropertyMapping propertyMapping;
        private readonly EntityMapBuilder<T> entityMapBuilder;

        /// <summary>
        /// Initializes a new instance of the <see cref="PropertyMapBuilder{T, TProperty}"/> class.
        /// Инициализирует новый экземпляр билдера сопоставления свойства.
        /// </summary>
        /// <param name="entityMapBuilder">
        /// Билдер сопоставления сущности.
        /// </param>
        /// <param name="propertyInfo">
        /// Информация о свойстве, для которого настраивается сопоставление.
        /// </param>
        internal PropertyMapBuilder(EntityMapBuilder<T> entityMapBuilder, PropertyInfo propertyInfo)
        {
            this.entityMapBuilder = entityMapBuilder;
            this.propertyMapping = new PropertyMapping(propertyInfo);
        }

        /// <summary>
        /// Задаёт имя колонки таблицы, с которой сопоставляется свойство.
        /// </summary>
        /// <param name="columnName">
        /// Имя колонки в таблице базы данных.
        /// </param>
        /// <returns>
        /// Текущий экземпляр билдера для цепочного вызова.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Выбрасывается, если <paramref name="columnName"/> пуст или содержит только пробелы.
        /// </exception>
        public PropertyMapBuilder<T, TProperty> HasColumn(string columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName))
            {
                throw new ArgumentException(nameof(columnName));
            }

            this.propertyMapping.ColumnName = columnName;
            this.MapColumnName(this.propertyMapping.Property, columnName);
            return this;
        }

        /// <summary>
        /// Задаёт псевдоним (alias) колонки.
        /// </summary>
        /// <param name="alias">
        /// Псевдоним колонки, используемый, например, в SQL-запросах.
        /// </param>
        /// <returns>
        /// Текущий экземпляр билдера для цепочного вызова.
        /// </returns>
        public PropertyMapBuilder<T, TProperty> HasAlias(string alias)
        {
            this.propertyMapping.Alias = alias;
            this.MapAlias(this.propertyMapping.Property, alias);
            return this;
        }

        /// <summary>
        /// Настраивает сопоставление другого свойства сущности.
        /// </summary>
        /// <typeparam name="TNewProperty">
        /// Тип нового свойства.
        /// </typeparam>
        /// <param name="selector">
        /// Выражение, указывающее на свойство сущности.
        /// </param>
        /// <param name="columnName">
        /// Имя колонки таблицы.
        /// </param>
        /// <param name="alias">
        /// Псевдоним колонки (необязательно).
        /// </param>
        /// <returns>
        /// Билдер сопоставления нового свойства.
        /// </returns>
        public PropertyMapBuilder<T, TNewProperty> Property<TNewProperty>(
            Expression<Func<T, TNewProperty>> selector,
            string columnName,
            string alias = null)
        {
            var property = EntityMapBuilder<T>.GetProperty(selector);
            var pb = new PropertyMapBuilder<T, TNewProperty>(this.entityMapBuilder, property);
            pb.HasColumn(columnName);
            pb.HasAlias(alias);
            return pb;
        }

        /// <summary>
        /// Переходит к настройке сопоставления другой сущности (таблицы).
        /// </summary>
        /// <typeparam name="TTable">
        /// Тип сущности таблицы.
        /// </typeparam>
        /// <param name="tableName">
        /// Имя таблицы.
        /// </param>
        /// <returns>
        /// Билдер сопоставления сущности таблицы.
        /// </returns>
        public EntityMapBuilder<TTable> Table<TTable>(string tableName)
            where TTable : class
            => new EntityMapBuilder<TTable>(this.entityMapBuilder.Map, tableName);

        /// <summary>
        /// Привязывает имя колонки к указанному свойству.
        /// </summary>
        /// <param name="property">
        /// Свойство сущности.
        /// </param>
        /// <param name="columnName">
        /// Имя колонки.
        /// </param>
        internal void MapColumnName(PropertyInfo property, string columnName)
        {
            var propMapping = this.GetOrAdd(property);
            propMapping.ColumnName = columnName;
        }

        /// <summary>
        /// Привязывает псевдоним к указанному свойству.
        /// </summary>
        /// <param name="property">
        /// Свойство сущности.
        /// </param>
        /// <param name="alias">
        /// Псевдоним колонки.
        /// </param>
        internal void MapAlias(PropertyInfo property, string alias)
        {
            var propMapping = this.GetOrAdd(property);
            propMapping.Alias = alias;
        }

        /// <summary>
        /// Возвращает существующее сопоставление свойства
        /// либо создаёт новое при его отсутствии.
        /// </summary>
        /// <param name="property">
        /// Свойство сущности.
        /// </param>
        /// <returns>
        /// Экземпляр <see cref="PropertyMapping"/>.
        /// </returns>
        private PropertyMapping GetOrAdd(PropertyInfo property)
        {
            if (!this.entityMapBuilder.EntityMapping.PropertyColumns.TryGetValue(property, out var propMapping))
            {
                propMapping = new PropertyMapping(property);
                this.entityMapBuilder.EntityMapping.PropertyColumns[property] = propMapping;
            }

            return propMapping;
        }
    }
}
