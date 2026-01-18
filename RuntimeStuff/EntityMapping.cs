// ***********************************************************************
// Assembly         : RuntimeStuff
// Author           : RS
// Created          : 01-06-2026
//
// Last Modified By : RS
// Last Modified On : 01-07-2026
// ***********************************************************************
// <copyright file="EntityMapping.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
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
        /// Initializes a new instance of the <see cref="EntityMapping" /> class.
        /// </summary>
        /// <param name="entityType">Type of the entity.</param>
        internal EntityMapping(Type entityType)
        {
            this.EntityType = entityType;
            this.PropertyColumnsValue = new Dictionary<PropertyInfo, PropertyMapping>();
        }

        /// <summary>
        /// Gets the type of the entity.
        /// </summary>
        /// <value>The type of the entity.</value>
        public Type EntityType { get; }

        /// <summary>
        /// Gets the property columns.
        /// </summary>
        /// <value>The property columns.</value>
        public IDictionary<PropertyInfo, PropertyMapping> PropertyColumns => this.PropertyColumnsValue;

        /// <summary>
        /// Gets the schema.
        /// </summary>
        /// <value>The schema.</value>
        public string Schema { get; internal set; }

        /// <summary>
        /// Gets the name of the table.
        /// </summary>
        /// <value>The name of the table.</value>
        public string TableName { get; internal set; }

        /// <summary>
        /// Gets the property columns.
        /// </summary>
        internal Dictionary<PropertyInfo, PropertyMapping> PropertyColumnsValue { get; }
    }
}