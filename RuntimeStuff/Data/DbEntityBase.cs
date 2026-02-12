// <copyright file="DbEntityBase.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace RuntimeStuff.Data
{
    using System;
    using System.Collections.Concurrent;
    using System.Data;

    public abstract class DbEntityBase : ObservableObjectEx
    {
        protected static readonly ConcurrentDictionary<IDbConnection, DbClient> ClientCache = new ConcurrentDictionary<IDbConnection, DbClient>();

        protected static IDbConnection GetConnection(Type entityType)
            => DbConnectionResolver.Instance?.Resolve(entityType) ?? DbConnectionResolver.DefaultEntityConnection?.Invoke(entityType) ?? DbConnectionResolver.DefaultConnection?.Invoke();

        public static DbEntityMap DefaultMap { get; set; }
    }
}