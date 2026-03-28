// ***********************************************************************
// Assembly         : RuntimeStuff
// Author           : RS
// Created          : 01-07-2026
//
// Last Modified By : RS
// Last Modified On : 01-07-2026
// ***********************************************************************
// <copyright file="DbClient{T}.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************

namespace System.Data
{
    using System.Collections.Concurrent;

    /// <summary>
    /// Универсальный клиент доступа к базе данных, типизированный по конкретному
    /// типу соединения (<typeparamref name="T" />).
    /// </summary>
    /// <typeparam name="T">Тип соединения с базой данных, реализующий <see cref="IDbConnection" />
    /// и имеющий конструктор без параметров.</typeparam>
    public class DbClient<T> : DbClient
        where T : IDbConnection, new()
    {
        /// <summary>
        /// The client cache.
        /// </summary>
        private static readonly ConcurrentDictionary<IDbConnection, DbClient<T>> ClientCache =
            new ConcurrentDictionary<IDbConnection, DbClient<T>>();

        /// <summary>
        /// Initializes a new instance of the <see cref="DbClient{T}"/> class.
        /// Создаёт новый экземпляр клиента с автоматически созданным соединением.
        /// </summary>
        /// <param name="map">Глобальная карта сопоставлений.</param>
        public DbClient(DbEntityMap map = null)
            : base(new T(), map)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DbClient{T}"/> class.
        /// Создаёт новый экземпляр клиента и инициализирует строку подключения.
        /// </summary>
        /// <param name="connectionString">Строка подключения к базе данных.</param>
        /// <param name="map">Сопоставление типов и имен сущностей в БД.</param>
        public DbClient(string connectionString, DbEntityMap map = null)
            : base(new T { ConnectionString = connectionString }, map)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DbClient{T}"/> class.
        /// Создаёт новый экземпляр клиента и инициализирует строку подключения.
        /// </summary>
        /// <param name="server">Имя сервера.</param>
        /// <param name="database">Имя базы данных.</param>
        /// <param name="map">Сопоставление типов и имен сущностей в БД.</param>
        public DbClient(string server, string database, DbEntityMap map = null)
            : base(new T(), map)
        {
            DbConnectionExtensions.Server(this.Connection, server);
            DbConnectionExtensions.Database(this.Connection, database);
            DbConnectionExtensions.IntegratedSecurity(this.Connection, true);
        }

        /// <summary>
        /// Gets or sets типизированное соединение с базой данных.
        /// </summary>
        /// <value>The connection.</value>
        public new T Connection
        {
            get => (T)base.Connection;
            set => base.Connection = value;
        }
    }
}