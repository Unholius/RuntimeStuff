// <copyright file="DbClient{T}.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace System.Data
{
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
        /// Создаёт новый экземпляр клиента и инициализирует строку подключения.
        /// </summary>
        public DbClient()
            : base()
        {
            this.Connection = new T();
        }

        /// <summary>
        /// Создаёт новый экземпляр клиента и инициализирует строку подключения.
        /// </summary>
        /// <param name="con">Соединение с базой данных.</param>
        public DbClient(T con)
            : base(con)
        {
            this.Connection = con;
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
        /// Initializes a new instance of the <see cref="DbClient{T}"/> class.
        /// Создаёт новый экземпляр клиента и инициализирует строку подключения.
        /// </summary>
        /// <param name="server">Имя сервера.</param>
        /// <param name="database">Имя базы данных.</param>
        /// <param name="userName">Логин.</param>
        /// <param name="password">Пароль.</param>
        /// <param name="map">Сопоставление типов и имен сущностей в БД.</param>
        public DbClient(string server, string database, string userName, string password, DbEntityMap map = null)
            : base(new T(), map)
        {
            DbConnectionExtensions.Server(this.Connection, server);
            DbConnectionExtensions.Database(this.Connection, database);
            DbConnectionExtensions.IntegratedSecurity(this.Connection, false);
            DbConnectionExtensions.User(this.Connection, userName);
            DbConnectionExtensions.Password(this.Connection, password);
        }

        /// <summary>
        /// типизированное соединение с базой данных.
        /// </summary>
        /// <value>The connection.</value>
        public new T Connection
        {
            get => (T)base.Connection;
            private set => base.Connection = value;
        }
    }
}