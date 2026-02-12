// <copyright file="DbEntity.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace RuntimeStuff.Data
{
    using System;
    using System.Collections.Generic;
    using System.Linq.Expressions;

    /// <summary>
    /// Базовый абстрактный класс для сущностей базы данных с поддержкой CRUD-операций.
    /// </summary>
    /// <typeparam name="T">Тип сущности, наследуемый от класса.</typeparam>
    public abstract class DbEntity<T> : DbEntityBase
        where T : class
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DbEntity{T}"/> class.
        /// Инициализирует новый экземпляр <see cref="DbEntity{T}"/>.
        /// </summary>
        /// <remarks>
        /// При создании экземпляра инициализируется кэш членов через <see cref="MemberCache"/>.
        /// </remarks>
        protected DbEntity()
        {
            MemberCache.Create<T>();
        }

        /// <summary>
        /// Получает или задаёт карту сопоставлений сущности.
        /// Использует общий кэш в базовом типе, чтобы избежать статических полей в обобщённом типе.
        /// </summary>
        public static DbEntityMap Map
        {
            get => GetEntityMap(typeof(T));
            set => SetEntityMap(typeof(T), value);
        }

        /// <summary>
        /// Выбирает одну сущность из базы данных, удовлетворяющую условию.
        /// </summary>
        /// <param name="whereExpression">Выражение фильтрации.</param>
        /// <returns>Первый объект типа <typeparamref name="T"/>, соответствующий условию.</returns>
        public static T SelectOne(Expression<Func<T, bool>> whereExpression)
        {
            return GetClient().First<T>(whereExpression);
        }

        /// <summary>
        /// Выбирает все сущности из базы данных, удовлетворяющие условию.
        /// </summary>
        /// <param name="whereExpression">Выражение фильтрации.</param>
        /// <returns>Коллекция объектов типа <typeparamref name="T"/>.</returns>
        public static IEnumerable<T> Select(Expression<Func<T, bool>> whereExpression)
        {
            return GetClient().ToList<T>(whereExpression);
        }

        /// <summary>
        /// Загружает данные сущности из базы данных по ключу.
        /// </summary>
        /// <param name="id">Значения ключевых полей сущности.</param>
        /// <remarks>
        /// Использует <see cref="DbClient.Fill{T}"/> для заполнения текущего экземпляра.
        /// </remarks>
        public void Load(params object[] id)
        {
            GetClient().Fill<T>(this as T, id);
        }

        /// <summary>
        /// Сохраняет текущий экземпляр сущности в базу данных.
        /// </summary>
        /// <remarks>
        /// Использует <see cref="RuntimeStuff.Data.DbClient.Update{T}(T, System.Linq.Expressions.Expression{System.Func{T, object}}[])"/> для обновления данных.
        /// </remarks>
        public void Save()
        {
            GetClient().Update<T>(this as T);
        }

        /// <summary>
        /// Получает клиента базы данных для выполнения операций над сущностью.
        /// </summary>
        /// <returns>Экземпляр <see cref="DbClient"/>.</returns>
        private static DbClient GetClient()
        {
            return ClientCache.GetOrAdd(
                GetConnection(typeof(T)),
                c => new DbClient(c, Map ?? DefaultMap ?? DbContext.GlobalMap));
        }
    }
}
