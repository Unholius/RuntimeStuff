// ***********************************************************************
// Assembly         : RuntimeStuff
// Author           : RS
// Created          : 01-06-2026
//
// Last Modified By : RS
// Last Modified On : 01-07-2026
// ***********************************************************************
// <copyright file="DbEntityMap.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************
namespace RuntimeStuff.Data
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using RuntimeStuff.Builders;
    using RuntimeStuff.Helpers;
    using RuntimeStuff.Internal;

    /// <summary>
    /// Class EntityMap.
    /// </summary>
    public class DbEntityMap
    {
        /// <summary>
        /// Gets the entity mapping.
        /// </summary>
        internal Dictionary<Type, TypeMappingInfo> TypeMap { get; } = new Dictionary<Type, TypeMappingInfo>();

        /// <summary>
        /// Автоматически сопоставляет таблицу и колонки сущности <typeparamref name="T"/>
        /// в формате <c>snake_case</c>.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <returns>Текущий экземпляр <see cref="DbEntityMap"/> для цепочного вызова.</returns>
        public DbEntityMap MapToSnakeCase<T>()
            where T : class
        {
            return AutoMap<T>(StringHelper.ToSnakeCase);
        }

        /// <summary>
        /// Автоматически сопоставляет таблицу и колонки сущности <typeparamref name="T"/>
        /// в формате <c>PascalCase / CamelCase</c>.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <returns>Текущий экземпляр <see cref="DbEntityMap"/> для цепочного вызова.</returns>
        public DbEntityMap MapToCamelCase<T>()
            where T : class
        {
            return AutoMap<T>(StringHelper.ToCamelCase);
        }

        /// <summary>
        /// Автоматически сопоставляет таблицу и колонки сущности <typeparamref name="T"/>
        /// в формате <c>kebab-case</c>.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <returns>Текущий экземпляр <see cref="DbEntityMap"/> для цепочного вызова.</returns>
        public DbEntityMap MapToKebabCase<T>()
            where T : class
        {
            return AutoMap<T>(StringHelper.ToKebabCase);
        }

        /// <summary>
        /// Автоматически сопоставляет таблицу и колонки сущности <typeparamref name="T"/>
        /// с помощью заданного делегата преобразования имён.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="nameMapper">
        /// Делегат для преобразования имён таблиц и колонок.
        /// Например, <c>StringHelper.ToSnakeCase</c>, <c>StringHelper.ToCamelCase</c>.
        /// </param>
        /// <returns>Текущий экземпляр <see cref="DbEntityMap"/> для цепочного вызова.</returns>
        /// <remarks>
        /// Создаёт объект <see cref="TypeMappingInfo"/> для типа <typeparamref name="T"/> и
        /// добавляет его в словарь <c>TypeMap</c>.
        /// Для каждого свойства создаётся <see cref="PropertyMappingInfo"/> с преобразованным именем колонки.
        /// </remarks>
        public DbEntityMap AutoMap<T>(Func<string, string> nameMapper)
        {
            return AutoMap(nameMapper, typeof(T));
        }

        /// <summary>
        /// Автоматически сопоставляет таблицу и колонки сущности
        /// с помощью заданного делегата преобразования имён.
        /// </summary>
        /// <param name="nameMapper">
        /// Делегат для преобразования имён таблиц и колонок.
        /// Например, <c>StringHelper.ToSnakeCase</c>, <c>StringHelper.ToCamelCase</c>.
        /// </param>
        /// <param name="entityTypes">Типы сущностей.</param>
        /// <returns>Текущий экземпляр <see cref="DbEntityMap"/> для цепочного вызова.</returns>
        /// <remarks>
        /// Создаёт объект <see cref="TypeMappingInfo"/> для указанных типов и
        /// добавляет его в словарь <c>TypeMap</c>.
        /// Для каждого свойства создаётся <see cref="PropertyMappingInfo"/> с преобразованным именем колонки.
        /// </remarks>
        public DbEntityMap AutoMap(Func<string, string> nameMapper, params Type[] entityTypes)
        {
            foreach (var entityType in entityTypes)
            {
                var tmi = new TypeMappingInfo(entityType);
                TypeMap[entityType] = tmi;

                // Преобразуем имя таблицы
                tmi.TableName = nameMapper(entityType.Name);

                // Преобразуем имена колонок для каждого свойства
                foreach (var p in Obj.GetProperties(entityType))
                {
                    var pmi = new PropertyMappingInfo(p)
                    {
                        ColumnName = nameMapper(p.Name),
                    };
                    tmi.PropertyMap[p] = pmi;
                }
            }

            return this;
        }

        /// <summary>
        /// Gets the column to property map.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>IEnumerable&lt;System.ValueTuple&lt;System.String, System.String&gt;&gt;.</returns>
        public IEnumerable<(string ColumnName, string PropertyName)> GetColumnToPropertyMap(Type type)
        {
            if (type == null || !this.TypeMap.TryGetValue(type, out var typeMapping))
            {
                return Array.Empty<(string, string)>();
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
                return Array.Empty<(string, string)>();
            }

            if (!this.TypeMap.TryGetValue(type, out var typeMapping))
            {
                return Array.Empty<(string, string)>();
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
            if (property?.DeclaringType == null)
            {
                return null;
            }

            if (this.TypeMap.TryGetValue(property.DeclaringType, out var typeMapping) && typeMapping.PropertyColumns.TryGetValue(property, out var propertyMapping))
            {
                return $"{namePrefix}{propertyMapping.ColumnName}{nameSuffix}";
            }

            return null;
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

            if (!this.TypeMap.TryGetValue(type, out var typeMapping))
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

            return this.TypeMap.TryGetValue(type, out var typeMapping) ? $"{namePrefix}{typeMapping.Schema}{nameSuffix}" : null;
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

            return this.TypeMap.TryGetValue(type, out var typeMapping) ? $"{namePrefix}{typeMapping.TableName}{nameSuffix}" : null;
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

            return this.TypeMap.FirstOrDefault(x => x.Value.TableName.Equals(tableName, StringComparison.OrdinalIgnoreCase)).Key;
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
        private TypeMappingInfo GetOrAdd(Type type)
        {
            if (!this.TypeMap.TryGetValue(type, out var typeProps))
            {
                typeProps = new TypeMappingInfo(type);
                this.TypeMap.Add(type, typeProps);
            }

            return typeProps;
        }
    }
}