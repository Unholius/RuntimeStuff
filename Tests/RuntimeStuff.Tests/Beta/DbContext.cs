// <copyright file="DbContext.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace RuntimeStuff.MSTests.Beta
{
    using System;
    using System.Data;

    /// <summary>
    /// Предоставляет механизм разрешения (<c>resolve</c>) подключений к базе данных
    /// для различных типов сущностей.
    /// </summary>
    /// <remarks>
    /// Используется как точка расширения для определения стратегии выбора
    /// подключения (например, по типу сущности, по схеме, по шарду и т.д.).
    /// Поддерживает как глобальное подключение по умолчанию,
    /// так и подключение, специфичное для конкретной сущности.
    /// </remarks>
    public abstract class DbContext
    {
        /// <summary>
        /// Получает или задаёт фабрику подключения по умолчанию.
        /// </summary>
        /// <remarks>
        /// Используется, если не определён более специфичный механизм разрешения.
        /// Делегат должен возвращать новый или валидный экземпляр <see cref="IDbConnection"/>.
        /// </remarks>
        public static Func<IDbConnection> DefaultConnection { get; set; }

        /// <summary>
        /// Получает или задаёт фабрику подключения,
        /// основанную на типе сущности.
        /// </summary>
        /// <remarks>
        /// Позволяет реализовать стратегию разделения подключений
        /// по типам сущностей (например, разные базы данных или шарды).
        /// </remarks>
        public static Func<Type, IDbConnection> DefaultEntityConnection { get; set; }

        /// <summary>
        /// Получает глобальную карту сопоставлений сущностей.
        /// </summary>
        /// <remarks>
        /// Используется для хранения метаданных mapping-конфигурации
        /// (таблицы, схемы, колонки и т.д.).
        /// </remarks>
        public static DbEntityMap GlobalMap { get; set; } = new DbEntityMap();

        /// <summary>
        /// Получает или задаёт текущий экземпляр резолвера подключений.
        /// </summary>
        /// <remarks>
        /// Позволяет централизованно заменить стратегию разрешения
        /// подключений в приложении.
        /// </remarks>
        public static DbContext Instance { get; set; }

        /// <summary>
        /// Разрешает подключение к базе данных для указанного типа сущности.
        /// </summary>
        /// <param name="entityType">Тип сущности.</param>
        /// <returns>Экземпляр <see cref="IDbConnection"/>.</returns>
        public abstract IDbConnection Resolve(Type entityType);

        /// <summary>
        /// Разрешает подключение к базе данных для сущности типа <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Тип сущности.</typeparam>
        /// <returns>Экземпляр <see cref="IDbConnection"/>.</returns>
        public abstract IDbConnection Resolve<T>()
            where T : class;
    }
}