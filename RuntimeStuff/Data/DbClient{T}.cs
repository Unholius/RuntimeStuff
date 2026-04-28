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
        /// <param name="commandTimeout">Максимальное время исполнения команды.</param>
        public DbClient(int commandTimeout = 5)
            : base(commandTimeout)
        {
            this.Connection = new T();
        }

        /// <summary>
        /// Создаёт новый экземпляр клиента и инициализирует строку подключения.
        /// </summary>
        /// <param name="con">Соединение с базой данных.</param>
        /// <param name="commandTimeout">Максимальное время исполнения команды.</param>
        public DbClient(T con, int commandTimeout = 5)
            : base(con, null, commandTimeout)
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
        /// <param name="commandTimeout">Максимальное время исполнения команды.</param>
        public DbClient(string server, string database, DbEntityMap map = null, int commandTimeout = 5)
            : base(new T(), map, commandTimeout)
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
        /// <param name="commandTimeout">Максимальное время исполнения команды.</param>
        public DbClient(string server, string database, string userName, string password, DbEntityMap map = null, int commandTimeout = 5)
            : base(new T(), map, commandTimeout)
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