// <copyright file="EntityMapBuilder.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace RuntimeStuff.Builders
{
    using System;
    using System.Linq.Expressions;
    using System.Reflection;
    using RuntimeStuff.Data;
    using RuntimeStuff.Helpers;

    /// <summary>
    /// Предоставляет fluent-интерфейс для конфигурации сопоставления
    /// сущности <typeparamref name="T"/> с таблицей базы данных.
    /// </summary>
    /// <typeparam name="T">Тип сущности.</typeparam>
    public sealed class EntityMapBuilder<T>
        where T : class
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EntityMapBuilder{T}"/> class.
        /// </summary>
        /// <param name="map">Глобальная карта сопоставлений.</param>
        /// <param name="tableName">Имя таблицы базы данных.</param>
        internal EntityMapBuilder(DbEntityMap map, string tableName)
            : this(map, new TypeMappingInfo(typeof(T)))
        {
            this.Table(tableName);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EntityMapBuilder{T}"/> class.
        /// </summary>
        /// <param name="map">Глобальная карта сопоставлений.</param>
        /// <param name="mapping">Метаданные сопоставления сущности.</param>
        internal EntityMapBuilder(DbEntityMap map, TypeMappingInfo mapping)
        {
            this.Map = map ?? throw new ArgumentNullException(nameof(map));
            this.EntityMapping = mapping ?? throw new ArgumentNullException(nameof(mapping));
            this.Map.TypeMap[typeof(T)] = this.EntityMapping;
        }

        /// <summary>
        /// Получает или задаёт метаданные сопоставления сущности.
        /// </summary>
        internal TypeMappingInfo EntityMapping { get; set; }

        /// <summary>
        /// Получает глобальную карту сопоставлений.
        /// </summary>
        internal DbEntityMap Map { get; }

        /// <summary>
        /// Извлекает <see cref="PropertyInfo"/> из лямбда-выражения,
        /// указывающего на свойство сущности.
        /// </summary>
        /// <typeparam name="TProperty">Тип свойства.</typeparam>
        /// <param name="selector">Выражение вида <c>x => x.Property</c>.</param>
        /// <returns>Метаданные свойства.</returns>
        /// <exception cref="ArgumentException">
        /// Выбрасывается, если выражение не указывает на свойство.
        /// </exception>
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
        /// Начинает конфигурацию сопоставления указанного свойства сущности.
        /// </summary>
        /// <typeparam name="TProperty">Тип свойства.</typeparam>
        /// <param name="selector">Выражение вида <c>x => x.Property</c>.</param>
        /// <returns>Билдер конфигурации свойства.</returns>
        public PropertyMapBuilder<T, TProperty> Property<TProperty>(Expression<Func<T, TProperty>> selector)
        {
            var property = GetProperty(selector);
            return new PropertyMapBuilder<T, TProperty>(this, property);
        }

        /// <summary>
        /// Начинает конфигурацию сопоставления указанного свойства сущности
        /// с одновременной установкой имени колонки.
        /// </summary>
        /// <typeparam name="TProperty">Тип свойства.</typeparam>
        /// <param name="selector">Выражение вида <c>x => x.Property</c>.</param>
        /// <param name="columnName">Имя колонки в таблице.</param>
        /// <returns>Билдер конфигурации свойства.</returns>
        public PropertyMapBuilder<T, TProperty> Property<TProperty>(
            Expression<Func<T, TProperty>> selector,
            string columnName)
        {
            var property = GetProperty(selector);
            var pb = new PropertyMapBuilder<T, TProperty>(this, property);
            pb.HasColumn(columnName);
            return pb;
        }

        /// <summary>
        /// Задаёт имя таблицы и схему базы данных для сущности.
        /// </summary>
        /// <param name="tableName">Имя таблицы.</param>
        /// <param name="schema">
        /// Имя схемы базы данных. Может быть <c>null</c>.
        /// </param>
        /// <returns>Текущий экземпляр билдера для продолжения конфигурации.</returns>
        /// <exception cref="ArgumentException">
        /// Выбрасывается, если <paramref name="tableName"/> пустое или состоит только из пробелов.
        /// </exception>
        public EntityMapBuilder<T> Table(string tableName, string schema = null)
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                throw new ArgumentException(null, nameof(tableName));
            }

            this.EntityMapping.TableName = tableName;
            this.EntityMapping.Schema = schema;
            return this;
        }

        /// <summary>
        /// Устанавливает имя таблицы без изменения схемы.
        /// </summary>
        /// <param name="tableName">Имя таблицы.</param>
        internal void MapTableName(string tableName) =>
            this.EntityMapping.TableName = tableName;
    }
}