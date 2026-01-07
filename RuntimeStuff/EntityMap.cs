// ***********************************************************************
// Assembly         : RuntimeStuff
// Author           : RS
// Created          : 01-06-2026
//
// Last Modified By : RS
// Last Modified On : 01-07-2026
// ***********************************************************************
// <copyright file="EntityMap.cs" company="Rudnev Sergey">
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************
namespace RuntimeStuff
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using RuntimeStuff.Builders;

    /// <summary>
    /// Class EntityMap.
    /// </summary>
    public class EntityMap
    {
        /// <summary>
        /// Gets the entity mapping.
        /// </summary>
        internal Dictionary<Type, EntityMapping> EntityMapping { get; } = new Dictionary<Type, EntityMapping>();

        /// <summary>
        /// Gets the column to property map.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>IEnumerable&lt;System.ValueTuple&lt;System.String, System.String&gt;&gt;.</returns>
        public IEnumerable<(string ColumnName, string PropertyName)> GetColumnToPropertyMap(Type type)
        {
            if (type == null)
            {
                return null;
            }

            if (!this.EntityMapping.TryGetValue(type, out var typeMapping))
            {
                return null;
            }

            return typeMapping.PropertyColumns.Select(x => (x.Value.ColumnName, x.Value.Property.Name)).ToArray();
        }

        /// <summary>
        /// Gets the property to column map.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>IEnumerable&lt;System.ValueTuple&lt;System.String, System.String&gt;&gt;.</returns>
        public IEnumerable<(string PropertyName, string ColumnName)> GetPropertyToColumnMap(Type type)
        {
            if (type == null)
            {
                return null;
            }

            if (!this.EntityMapping.TryGetValue(type, out var typeMapping))
            {
                return null;
            }

            return typeMapping.PropertyColumns.Select(x => (x.Value.Property.Name, x.Value.ColumnName)).ToArray();
        }

        /// <summary>
        /// Resolves the name of the column.
        /// </summary>
        /// <param name="property">The property.</param>
        /// <param name="namePrefix">The name prefix.</param>
        /// <param name="nameSuffix">The name suffix.</param>
        /// <returns>System.String.</returns>
        public string ResolveColumnName(PropertyInfo property, string namePrefix, string nameSuffix)
        {
            if (property == null)
            {
                return null;
            }

            return this.EntityMapping.TryGetValue(property.DeclaringType, out var typeMapping) ? (typeMapping.PropertyColumns.TryGetValue(property, out var propertyMapping) ? $"{namePrefix}{propertyMapping.ColumnName}{nameSuffix}" : null) : null;
        }

        /// <summary>
        /// Resolves the property.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="columnName">Name of the column.</param>
        /// <returns>PropertyInfo.</returns>
        public PropertyInfo ResolveProperty(Type type, string columnName)
        {
            if (type == null)
            {
                return null;
            }

            if (!this.EntityMapping.TryGetValue(type, out var typeMapping))
            {
                return null;
            }

            return typeMapping.PropertyColumns.FirstOrDefault(x => x.Value.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase)).Key;
        }

        /// <summary>
        /// Resolves the name of the schema.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="namePrefix">The name prefix.</param>
        /// <param name="nameSuffix">The name suffix.</param>
        /// <returns>System.String.</returns>
        public string ResolveSchemaName(Type type, string namePrefix, string nameSuffix)
        {
            if (type == null)
            {
                return null;
            }

            return this.EntityMapping.TryGetValue(type, out var typeMapping) ? $"{namePrefix}{typeMapping.Schema}{nameSuffix}" : null;
        }

        /// <summary>
        /// Resolves the name of the table.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="namePrefix">The name prefix.</param>
        /// <param name="nameSuffix">The name suffix.</param>
        /// <returns>System.String.</returns>
        public string ResolveTableName(Type type, string namePrefix, string nameSuffix)
        {
            if (type == null)
            {
                return null;
            }

            return this.EntityMapping.TryGetValue(type, out var typeMapping) ? $"{namePrefix}{typeMapping.TableName}{nameSuffix}" : null;
        }

        /// <summary>
        /// Resolves the type.
        /// </summary>
        /// <param name="tableName">Name of the table.</param>
        /// <returns>Type.</returns>
        public Type ResolveType(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                return null;
            }

            return this.EntityMapping.FirstOrDefault(x => x.Value.TableName.Equals(tableName, StringComparison.OrdinalIgnoreCase)).Key;
        }

        /// <summary>
        /// Tables this instance.
        /// </summary>
        /// <typeparam name="T">Type.</typeparam>
        /// <returns>EntityMapBuilder&lt;T&gt;.</returns>
        public EntityMapBuilder<T> Table<T>()
            where T : class => new EntityMapBuilder<T>(this, this.GetOrAdd(typeof(T)));

        /// <summary>
        /// Tables the specified table name.
        /// </summary>
        /// <typeparam name="T">Type.</typeparam>
        /// <param name="tableName">Name of the table.</param>
        /// <returns>EntityMapBuilder&lt;T&gt;.</returns>
        public EntityMapBuilder<T> Table<T>(string tableName)
            where T : class
        {
            var entityMapping = this.GetOrAdd(typeof(T));
            var builder = new EntityMapBuilder<T>(this, entityMapping);
            builder.MapTableName(tableName);
            return builder;
        }

        /// <summary>
        /// Gets the or add.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>EntityMapping.</returns>
        private EntityMapping GetOrAdd(Type type)
        {
            if (!this.EntityMapping.TryGetValue(type, out var typeProps))
            {
                typeProps = new EntityMapping(type);
                this.EntityMapping.Add(type, typeProps);
            }

            return typeProps;
        }
    }
}