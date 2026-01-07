// ***********************************************************************
// Assembly         : RuntimeStuff
// Author           : RS
// Created          : 01-07-2026
//
// Last Modified By : RS
// Last Modified On : 01-07-2026
// ***********************************************************************
// <copyright file="DbClient{T}.cs" company="Rudnev Sergey">
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************
namespace RuntimeStuff
{
    using System.Data;

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
        private static readonly Cache<IDbConnection, DbClient<T>> ClientCache =
            new Cache<IDbConnection, DbClient<T>>(con => new DbClient<T>((T)con));

        /// <summary>
        /// Initializes a new instance of the <see cref="DbClient{T}"/> class.
        /// Создаёт новый экземпляр клиента с автоматически созданным соединением.
        /// </summary>
        public DbClient()
            : base(new T())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DbClient{T}"/> class.
        /// Создаёт новый экземпляр клиента на основе переданного соединения.
        /// </summary>
        /// <param name="con">Открытое или закрытое соединение с БД.</param>
        public DbClient(T con)
            : base(con)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DbClient{T}"/> class.
        /// Создаёт новый экземпляр клиента и инициализирует строку подключения.
        /// </summary>
        /// <param name="connectionString">Строка подключения к базе данных.</param>
        public DbClient(string connectionString)
        {
            this.Connection = new T { ConnectionString = connectionString };
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

        /// <summary>
        /// Получает или создаёт кэшированный экземпляр клиента по строке подключения.
        /// </summary>
        /// <param name="connectionString">Строка подключения.</param>
        /// <returns>Экземпляр <see cref="DbClient{T}" />.</returns>
        public static DbClient<T> Create(string connectionString)
        {
            T con = new T { ConnectionString = connectionString };
            DbClient<T> dbClient = ClientCache.Get(con);
            return dbClient;
        }

        /// <summary>
        /// Получает или создаёт кэшированный экземпляр клиента по соединению.
        /// </summary>
        /// <param name="con">Соединение с базой данных.</param>
        /// <returns>Экземпляр <see cref="DbClient{T}" />.</returns>
        public static DbClient<T> Create(T con)
        {
            DbClient<T> dbClient = ClientCache.Get(con);
            return dbClient;
        }
    }
}