//// <copyright file="DbEntityBase.cs" company="Rudnev Sergey">
//// Copyright (c) Rudnev Sergey. All rights reserved.
//// </copyright>

//namespace RuntimeStuff.Data
//{
//    using System;
//    using System.Collections.Concurrent;
//    using System.Data;

//    /// <summary>
//    /// Базовый абстрактный класс для всех сущностей базы данных.
//    /// </summary>
//    /// <remarks>
//    /// Обеспечивает кэширование клиентов базы данных и получение подключений
//    /// для производных классов <see cref="DbEntity{T}"/>.
//    /// Наследуется от <see cref="ObservableObjectEx"/> для поддержки уведомлений об изменениях.
//    /// </remarks>
//    public abstract class DbEntityBase : ObservableObjectEx
//    {
//        /// <summary>
//        /// Кэш клиентов базы данных для каждого подключения <see cref="IDbConnection"/>.
//        /// </summary>
//        /// <remarks>
//        /// Позволяет повторно использовать экземпляры <see cref="DbClient"/> для одного соединения.
//        /// </remarks>
//        protected static readonly ConcurrentDictionary<IDbConnection, DbClient> ClientCache
//            = new ConcurrentDictionary<IDbConnection, DbClient>();

//        /// <summary>
//        /// Кэш карт сопоставлений сущностей по типу. Хранит отображения, но избегает объявления статического поля внутри обобщённого типа.
//        /// </summary>
//        private static readonly ConcurrentDictionary<Type, DbEntityMap> EntityMapCache
//            = new ConcurrentDictionary<Type, DbEntityMap>();

//        /// <summary>
//        /// Получает или задаёт карту сопоставлений сущностей по умолчанию.
//        /// </summary>
//        /// <remarks>
//        /// Используется при создании <see cref="DbClient"/> в методе <see cref="DbEntity{T}.GetClient"/>.
//        /// </remarks>
//        public static DbEntityMap DefaultMap { get; set; }

//        /// <summary>
//        /// Возвращает карту сопоставлений для указанного типа сущности или null, если не задана.
//        /// </summary>
//        /// <param name="type">Тип сущности.</param>
//        /// <returns>Соответствующая <see cref="DbEntityMap"/> или null.</returns>
//        protected static DbEntityMap? GetEntityMap(Type type)
//            => EntityMapCache.TryGetValue(type, out var map) ? map : null;

//        /// <summary>
//        /// Устанавливает карту сопоставлений для указанного типа сущности. Если map равен null, запись удаляется.
//        /// </summary>
//        /// <param name="type">Тип сущности.</param>
//        /// <param name="map">Карта сопоставлений или null для удаления.</param>
//        protected static void SetEntityMap(Type type, DbEntityMap map)
//        {
//            if (map == null)
//            {
//                EntityMapCache.TryRemove(type, out _);
//            }
//            else
//            {
//                EntityMapCache[type] = map;
//            }
//        }

//        /// <summary>
//        /// Получает подключение к базе данных для указанного типа сущности.
//        /// </summary>
//        /// <param name="entityType">Тип сущности, для которой требуется подключение.</param>
//        /// <returns>
//        /// Экземпляр <see cref="IDbConnection"/>, определённый через <see cref="DbContext"/>.
//        /// </returns>
//        /// <remarks>
//        /// Сначала используется глобальный резолвер <see cref="DbContext.Instance"/>,
//        /// затем <see cref="DbContext.DefaultEntityConnection"/> по типу сущности,
//        /// и наконец <see cref="DbContext.DefaultConnection"/> по умолчанию.
//        /// Может вернуть <c>null</c>, если ни один из источников подключения не определён.
//        /// </remarks>
//        protected static IDbConnection GetConnection(Type entityType)
//            => DbContext.Instance?.Resolve(entityType)
//               ?? DbContext.DefaultEntityConnection?.Invoke(entityType)
//               ?? DbContext.DefaultConnection?.Invoke();
//    }
//}