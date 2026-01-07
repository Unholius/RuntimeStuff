// ***********************************************************************
// Assembly         : RuntimeStuff
// Author           : RS
// Created          : 01-06-2026
//
// Last Modified By : RS
// Last Modified On : 01-07-2026
// ***********************************************************************
// <copyright file="PropertyMapBuilder.cs" company="Rudnev Sergey">
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************
namespace RuntimeStuff
{
    using System;
    using System.Linq.Expressions;
    using System.Reflection;

    /// <summary>
    /// Class PropertyMapBuilder. This class cannot be inherited.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="TProperty">The type of the t property.</typeparam>
    public sealed class PropertyMapBuilder<T, TProperty>
    {
        /// <summary>
        /// The property mapping.
        /// </summary>
        private readonly PropertyMapping _propertyMapping;

        /// <summary>
        /// The entity map builder.
        /// </summary>
        private readonly EntityMapBuilder<T> _entityMapBuilder;

        /// <summary>
        /// Initializes a new instance of the <see cref="PropertyMapBuilder{T, TProperty}" /> class.
        /// </summary>
        /// <param name="entityMapBuilder">The entity map builder.</param>
        /// <param name="propertyInfo">The property information.</param>
        internal PropertyMapBuilder(EntityMapBuilder<T> entityMapBuilder, PropertyInfo propertyInfo)
        {
            this._entityMapBuilder = entityMapBuilder;
            this._propertyMapping = new PropertyMapping(propertyInfo);
        }

        /// <summary>
        /// Determines whether the specified column name has column.
        /// </summary>
        /// <param name="columnName">Name of the column.</param>
        /// <returns>PropertyMapBuilder&lt;T, TProperty&gt;.</returns>
        /// <exception cref="System.ArgumentException">columnName.</exception>
        public PropertyMapBuilder<T, TProperty> HasColumn(string columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName))
            {
                throw new ArgumentException(nameof(columnName));
            }

            this._propertyMapping.ColumnName = columnName;
            this.MapColumnName(this._propertyMapping.Property, columnName);
            return this;
        }

        /// <summary>
        /// Determines whether the specified alias has alias.
        /// </summary>
        /// <param name="alias">The alias.</param>
        /// <returns>PropertyMapBuilder&lt;T, TProperty&gt;.</returns>
        public PropertyMapBuilder<T, TProperty> HasAlias(string alias)
        {
            this._propertyMapping.Alias = alias;
            this.MapAlias(this._propertyMapping.Property, alias);
            return this;
        }

        /// <summary>
        /// Properties the specified selector.
        /// </summary>
        /// <typeparam name="TNewProperty">The type of the t new property.</typeparam>
        /// <param name="selector">The selector.</param>
        /// <param name="columnName">Name of the column.</param>
        /// <param name="alias">The alias.</param>
        /// <returns>PropertyMapBuilder&lt;T, TNewProperty&gt;.</returns>
        public PropertyMapBuilder<T, TNewProperty> Property<TNewProperty>(Expression<Func<T, TNewProperty>> selector, string columnName, string alias = null)
        {
            var property = EntityMapBuilder<T>.GetProperty(selector);
            var pb = new PropertyMapBuilder<T, TNewProperty>(this._entityMapBuilder, property);
            pb.HasColumn(columnName);
            pb.HasAlias(alias);
            return pb;
        }

        /// <summary>
        /// Tables the specified table name.
        /// </summary>
        /// <typeparam name="TTable">The type of the t table.</typeparam>
        /// <param name="tableName">Name of the table.</param>
        /// <returns>EntityMapBuilder&lt;TTable&gt;.</returns>
        public EntityMapBuilder<TTable> Table<TTable>(string tableName)
            where TTable : class => new EntityMapBuilder<TTable>(this._entityMapBuilder._map, tableName);

        /// <summary>
        /// Maps the name of the column.
        /// </summary>
        /// <param name="property">The property.</param>
        /// <param name="columnName">Name of the column.</param>
        internal void MapColumnName(PropertyInfo property, string columnName)
        {
            var propMapping = this.GetOrAdd(property);
            propMapping.ColumnName = columnName;
        }

        /// <summary>
        /// Maps the alias.
        /// </summary>
        /// <param name="property">The property.</param>
        /// <param name="alias">The alias.</param>
        internal void MapAlias(PropertyInfo property, string alias)
        {
            var propMapping = this.GetOrAdd(property);
            propMapping.Alias = alias;
        }

        /// <summary>
        /// Gets the or add.
        /// </summary>
        /// <param name="property">The property.</param>
        /// <returns>PropertyMapping.</returns>
        private PropertyMapping GetOrAdd(PropertyInfo property)
        {
            if (!this._entityMapBuilder._entityMapping.PropertyColumns.TryGetValue(property, out var propMapping))
            {
                propMapping = new PropertyMapping(property);
                this._entityMapBuilder._entityMapping.PropertyColumns[property] = propMapping;
            }

            return propMapping;
        }
    }
}