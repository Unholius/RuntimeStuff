// ***********************************************************************
// Assembly         : RuntimeStuff
// Author           : RS
// Created          : 01-06-2026
//
// Last Modified By : RS
// Last Modified On : 01-07-2026
// ***********************************************************************
// <copyright file="EntityMapBuilder.cs" company="Rudnev Sergey">
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************
namespace RuntimeStuff.Builders
{
    using System;
    using System.Linq.Expressions;
    using System.Reflection;
    using RuntimeStuff;

    /// <summary>
    /// Class EntityMapBuilder. This class cannot be inherited.
    /// </summary>
    /// <typeparam name="T">Type.</typeparam>
    public sealed class EntityMapBuilder<T>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EntityMapBuilder{T}" /> class.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="tableName">Name of the table.</param>
        internal EntityMapBuilder(EntityMap map, string tableName)
            : this(map, new EntityMapping(typeof(T)))
        {
            this.Table(tableName);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EntityMapBuilder{T}" /> class.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="mapping">The mapping.</param>
        internal EntityMapBuilder(EntityMap map, EntityMapping mapping)
        {
            this.Map = map;
            this.EntityMapping = mapping;
            this.Map.EntityMapping[typeof(T)] = this.EntityMapping;
        }

        /// <summary>
        /// Gets or sets the entity mapping.
        /// </summary>
        internal EntityMapping EntityMapping { get; set; }

        /// <summary>
        /// Gets the map.
        /// </summary>
        internal EntityMap Map { get; }

        /// <summary>
        /// Gets the property.
        /// </summary>
        /// <typeparam name="TProperty">The type of the t property.</typeparam>
        /// <param name="selector">The selector.</param>
        /// <returns>PropertyInfo.</returns>
        /// <exception cref="System.ArgumentException">Expression must be a property.</exception>
        /// <exception cref="System.ArgumentException">Member is not a property.</exception>
        public static PropertyInfo GetProperty<TProperty>(Expression<Func<T, TProperty>> selector)
        {
            if (!(selector.Body is MemberExpression member))
            {
                throw new ArgumentException("Expression must be a property.");
            }

            if (!(member.Member is PropertyInfo property))
            {
                throw new ArgumentException("Member is not a property.");
            }

            return property;
        }

        /// <summary>
        /// Properties the specified selector.
        /// </summary>
        /// <typeparam name="TProperty">The type of the t property.</typeparam>
        /// <param name="selector">The selector.</param>
        /// <returns>PropertyMapBuilder&lt;T, TProperty&gt;.</returns>
        public PropertyMapBuilder<T, TProperty> Property<TProperty>(Expression<Func<T, TProperty>> selector)
        {
            var property = GetProperty(selector);
            return new PropertyMapBuilder<T, TProperty>(this, property);
        }

        /// <summary>
        /// Properties the specified selector.
        /// </summary>
        /// <typeparam name="TProperty">The type of the t property.</typeparam>
        /// <param name="selector">The selector.</param>
        /// <param name="columnName">Name of the column.</param>
        /// <returns>PropertyMapBuilder&lt;T, TProperty&gt;.</returns>
        public PropertyMapBuilder<T, TProperty> Property<TProperty>(Expression<Func<T, TProperty>> selector, string columnName)
        {
            var property = GetProperty(selector);
            var pb = new PropertyMapBuilder<T, TProperty>(this, property);
            pb.HasColumn(columnName);
            return pb;
        }

        /// <summary>
        /// Tables the specified table name.
        /// </summary>
        /// <param name="tableName">Name of the table.</param>
        /// <param name="schema">The schema.</param>
        /// <returns>EntityMapBuilder&lt;T&gt;.</returns>
        /// <exception cref="System.ArgumentException">tableName.</exception>
        public EntityMapBuilder<T> Table(string tableName, string schema = null)
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                throw new ArgumentException(nameof(tableName));
            }

            this.EntityMapping.TableName = tableName;
            this.EntityMapping.Schema = schema;
            return this;
        }

        /// <summary>
        /// Maps the name of the table.
        /// </summary>
        /// <param name="tableName">Name of the table.</param>
        internal void MapTableName(string tableName) => this.EntityMapping.TableName = tableName;
    }
}