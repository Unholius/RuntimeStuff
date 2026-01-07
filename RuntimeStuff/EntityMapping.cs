// ***********************************************************************
// Assembly         : RuntimeStuff
// Author           : RS
// Created          : 01-06-2026
//
// Last Modified By : RS
// Last Modified On : 01-07-2026
// ***********************************************************************
// <copyright file="EntityMapping.cs" company="Rudnev Sergey">
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************
namespace RuntimeStuff
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;

    /// <summary>
    /// Class EntityMapping. This class cannot be inherited.
    /// </summary>
    public sealed class EntityMapping
    {
        /// <summary>
        /// Gets the type of the entity.
        /// </summary>
        /// <value>The type of the entity.</value>
        public Type EntityType { get; }

        /// <summary>
        /// Gets the name of the table.
        /// </summary>
        /// <value>The name of the table.</value>
        public string TableName { get; internal set; }

        /// <summary>
        /// Gets the schema.
        /// </summary>
        /// <value>The schema.</value>
        public string Schema { get; internal set; }

        /// <summary>
        /// Gets the property columns.
        /// </summary>
        /// <value>The property columns.</value>
        public IDictionary<PropertyInfo, PropertyMapping> PropertyColumns => this._propertyColumns;

        /// <summary>
        /// The property columns.
        /// </summary>
        internal readonly Dictionary<PropertyInfo, PropertyMapping> _propertyColumns;

        /// <summary>
        /// Initializes a new instance of the <see cref="EntityMapping" /> class.
        /// </summary>
        /// <param name="entityType">Type of the entity.</param>
        internal EntityMapping(Type entityType)
        {
            this.EntityType = entityType;
            this._propertyColumns = new Dictionary<PropertyInfo, PropertyMapping>();
        }
    }

    /// <summary>
    /// Class PropertyMapping. This class cannot be inherited.
    /// </summary>
    public sealed class PropertyMapping
    {
        /// <summary>
        /// Gets the property.
        /// </summary>
        /// <value>The property.</value>
        public PropertyInfo Property { get; }

        /// <summary>
        /// Gets or sets the name of the column.
        /// </summary>
        /// <value>The name of the column.</value>
        public string ColumnName { get; set; }

        /// <summary>
        /// Gets or sets the alias.
        /// </summary>
        /// <value>The alias.</value>
        public string Alias { get; set; }

        /// <summary>
        /// Gets or sets the function.
        /// </summary>
        /// <value>The function.</value>
        public string Function { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PropertyMapping" /> class.
        /// </summary>
        /// <param name="property">The property.</param>
        public PropertyMapping(PropertyInfo property)
        {
            this.Property = property;
        }
    }
}