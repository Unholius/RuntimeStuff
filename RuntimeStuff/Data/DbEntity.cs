// <copyright file="DbEntity.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace RuntimeStuff.Data
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq.Expressions;

    public class DbEntity<T> : ObservableObjectEx
        where T : class
    {
        private static ConcurrentDictionary<IDbConnection, DbClient> clientCache = new ConcurrentDictionary<IDbConnection, DbClient>();

        public static T SelectOne(Expression<Func<T, bool>> whereExpression)
        {
            var db = GetClient();
            return db.First<T>(whereExpression);
        }

        public static IEnumerable<T> Where(Expression<Func<T, bool>> whereExpression)
        {
            var db = GetClient();
            return db.ToList<T>(whereExpression);
        }

        private static DbClient GetClient()
        {
            return clientCache.GetOrAdd(GetConnection(), (c) => new DbClient(c));
        }

        private static IDbConnection GetConnection()
            => DbConnectionResolver.Instance?.Resolve<T>() ?? DbConnectionResolver.DefaultEntityConnection(typeof(T)) ?? DbConnectionResolver.DefaultConnection();
    }
}