// <copyright file="DbEntityMap.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace System.Data
{
    using System;
    using System.Collections.Generic;
    using System.Helpers;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    /// Представляет конфигурацию сопоставления CLR-типов (сущностей)
    /// с объектами базы данных (таблицами, схемами и колонками).
    /// </summary>
    /// <remarks>
    /// Класс хранит информацию о соответствии типов, их свойств и имен таблиц/колонок.
    /// Позволяет выполнять автоматическое сопоставление на основе стратегии преобразования имён,
    /// а также получать обратные соответствия (колонка → свойство и наоборот).
    /// </remarks>
    public class DbEntityMap
    {
        /// <summary>
        /// Хранилище сопоставлений типов сущностей с их метаданными отображения.
        /// </summary>
        /// <remarks>
        /// Ключ словаря — CLR-тип сущности (<see cref="Type"/>),
        /// значение — объект <see cref="TypeMappingInfo"/>, содержащий информацию
        /// о таблице, схеме и сопоставлении свойств с колонками.
        /// Используется для быстрого разрешения соответствий при построении запросов
        /// и выполнении операций маппинга.
        /// </remarks>
        internal Dictionary<Type, TypeMappingInfo> TypeMap { get; } = new Dictionary<Type, TypeMappingInfo>();

        /// <summary>
        /// Выполняет автоматическое сопоставление типа <typeparamref name="T"/>
        /// с преобразованием имён таблицы и колонок в формат <c>snake_case</c>.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <returns>Текущий экземпляр <see cref="DbEntityMap"/> для цепочного вызова.</returns>
        public DbEntityMap MapToSnakeCase<T>()
            where T : class
        {
            return this.AutoMap<T>(StringHelper.ToSnakeCase);
        }

        /// <summary>
        /// Выполняет автоматическое сопоставление типа <typeparamref name="T"/>
        /// с преобразованием имён таблицы и колонок в формат <c>camelCase</c>.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <returns>Текущий экземпляр <see cref="DbEntityMap"/> для цепочного вызова.</returns>
        public DbEntityMap MapToCamelCase<T>()
            where T : class
        {
            return this.AutoMap<T>(StringHelper.ToPascalCase);
        }

        /// <summary>
        /// Выполняет автоматическое сопоставление типа <typeparamref name="T"/>
        /// с преобразованием имён таблицы и колонок в формат <c>kebab-case</c>.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <returns>Текущий экземпляр <see cref="DbEntityMap"/> для цепочного вызова.</returns>
        public DbEntityMap MapToKebabCase<T>()
            where T : class
        {
            return this.AutoMap<T>(StringHelper.ToKebabCase);
        }

        /// <summary>
        /// Выполняет автоматическое сопоставление типа <typeparamref name="T"/>
        /// с использованием пользовательской функции преобразования имён.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="nameMapper">
        /// Функция преобразования имени типа и свойств в имя таблицы и колонок.
        /// </param>
        /// <returns>Текущий экземпляр <see cref="DbEntityMap"/> для цепочного вызова.</returns>
        public DbEntityMap AutoMap<T>(Func<string, string> nameMapper)
        {
            return this.AutoMap(nameMapper, typeof(T));
        }

        /// <summary>
        /// Выполняет автоматическое сопоставление указанных типов
        /// с использованием пользовательской функции преобразования имён.
        /// </summary>
        /// <param name="nameMapper">
        /// Функция преобразования имени типа и свойств в имя таблицы и колонок.
        /// </param>
        /// <param name="entityTypes">Массив типов сущностей для сопоставления.</param>
        /// <returns>Текущий экземпляр <see cref="DbEntityMap"/> для цепочного вызова.</returns>
        public DbEntityMap AutoMap(Func<string, string> nameMapper, params Type[] entityTypes)
        {
            foreach (var entityType in entityTypes)
            {
                var tmi = new TypeMappingInfo(entityType);
                this.TypeMap[entityType] = tmi;

                tmi.TableName = nameMapper(entityType.Name);

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
        /// Возвращает сопоставление колонок и свойств для указанного типа
        /// в формате (ИмяКолонки, ИмяСвойства).
        /// </summary>
        /// <param name="type">Тип сущности.</param>
        /// <returns>
        /// Последовательность кортежей (ColumnName, PropertyName).
        /// Если тип не найден — возвращается пустая последовательность.
        /// </returns>
        public IEnumerable<(string ColumnName, string PropertyName)> GetColumnToPropertyMap(Type type)
        {
            if (type == null || !this.TypeMap.TryGetValue(type, out var typeMapping))
            {
                return Array.Empty<(string, string)>();
            }

            return typeMapping.PropertyColumns.Select(x => (x.Value.ColumnName, x.Value.Property.Name)).ToArray();
        }

        /// <summary>
        /// Возвращает сопоставление свойств и колонок для указанного типа
        /// в формате (ИмяСвойства, ИмяКолонки).
        /// </summary>
        /// <param name="type">Тип сущности.</param>
        /// <returns>
        /// Последовательность кортежей (PropertyName, ColumnName).
        /// Если тип не найден — возвращается пустая последовательность.
        /// </returns>
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
        /// Определяет имя колонки, соответствующей указанному свойству,
        /// с учётом заданного префикса и суффикса.
        /// </summary>
        /// <param name="property">Информация о свойстве.</param>
        /// <param name="namePrefix">Префикс имени.</param>
        /// <param name="nameSuffix">Суффикс имени.</param>
        /// <param name="fullName">Возвращать полное имя колонки с именем таблицы и схемы.</param>
        /// <returns>
        /// Полное имя колонки или <c>null</c>, если сопоставление не найдено.
        /// </returns>
        public string ResolveColumnName(PropertyInfo property, string namePrefix, string nameSuffix, bool fullName)
        {
            if (property?.DeclaringType == null)
            {
                return null;
            }

            if (this.TypeMap.TryGetValue(property.DeclaringType, out var typeMapping) && typeMapping.PropertyColumns.TryGetValue(property, out var propertyMapping))
            {
                var mappedColumnName = $"{namePrefix}{propertyMapping.ColumnName}{nameSuffix}";
                if (fullName)
                {
                    mappedColumnName = this.ResolveTableName(property.DeclaringType, namePrefix, nameSuffix) + "." + mappedColumnName;
                    return mappedColumnName;
                }
            }

            return null;
        }

        /// <summary>
        /// Определяет свойство типа, соответствующее указанному имени колонки.
        /// </summary>
        /// <param name="type">Тип сущности.</param>
        /// <param name="columnName">Имя колонки.</param>
        /// <returns>
        /// Объект <see cref="PropertyInfo"/>, если найдено соответствие; иначе <c>null</c>.
        /// </returns>
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
        /// Определяет имя схемы для указанного типа
        /// с учётом заданного префикса и суффикса.
        /// </summary>
        /// <param name="type">Тип сущности.</param>
        /// <param name="namePrefix">Префикс имени.</param>
        /// <param name="nameSuffix">Суффикс имени.</param>
        /// <returns>
        /// Полное имя схемы или <c>null</c>, если тип не сопоставлен.
        /// </returns>
        public string ResolveSchemaName(Type type, string namePrefix, string nameSuffix)
        {
            if (type == null)
            {
                return null;
            }

            return this.TypeMap.TryGetValue(type, out var typeMapping) ? $"{namePrefix}{typeMapping.Schema}{nameSuffix}" : null;
        }

        /// <summary>
        /// Возвращает сопоставленное имя таблицы для указанного типа в формате имя_схемы.имя_таблицы. null, если сопоставления нет.
        /// </summary>
        /// <param name="type">Тип сущности.</param>
        /// <param name="namePrefix">Префикс имени.</param>
        /// <param name="nameSuffix">Суффикс имени.</param>
        /// <returns>
        /// Полное имя таблицы или <c>null</c>, если тип не сопоставлен.
        /// </returns>
        public string ResolveTableName(Type type, string namePrefix, string nameSuffix)
        {
            if (type == null)
            {
                return null;
            }

            if (this.TypeMap.TryGetValue(type, out var typeMapping))
            {
                var tableName = $"{namePrefix}{typeMapping.TableName}{nameSuffix}";
                if (!string.IsNullOrEmpty(typeMapping.Schema))
                {
                    tableName = $"{namePrefix}{typeMapping.Schema}{nameSuffix}." + tableName;
                }

                return tableName;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Определяет тип сущности по имени таблицы.
        /// </summary>
        /// <param name="tableName">Имя таблицы.</param>
        /// <returns>
        /// Тип сущности, соответствующий таблице, либо <c>null</c>, если соответствие не найдено.
        /// </returns>
        public Type ResolveType(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                return null;
            }

            return this.TypeMap.FirstOrDefault(x => x.Value.TableName.Equals(tableName, StringComparison.OrdinalIgnoreCase)).Key;
        }

        /// <summary>
        /// Возвращает построитель сопоставления для типа <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <returns>Экземпляр <see cref="EntityMapBuilder{T}"/>.</returns>
        public EntityMapBuilder<T> Table<T>()
            where T : class => new EntityMapBuilder<T>(this, this.GetOrAdd(typeof(T)));

        /// <summary>
        /// Возвращает построитель сопоставления для типа <typeparamref name="T"/>
        /// с явной установкой имени таблицы.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <param name="tableName">Имя таблицы.</param>
        /// <returns>Экземпляр <see cref="EntityMapBuilder{T}"/>.</returns>
        public EntityMapBuilder<T> Table<T>(string tableName)
            where T : class
        {
            var entityMapping = this.GetOrAdd(typeof(T));
            var builder = new EntityMapBuilder<T>(this, entityMapping);
            builder.MapTableName(tableName);
            return builder;
        }

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