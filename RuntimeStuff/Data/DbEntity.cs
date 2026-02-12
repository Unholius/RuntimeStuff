// <copyright file="DbEntity.cs" company="Rudnev Sergey">
// Copyright (c) Rudnev Sergey. All rights reserved.
// </copyright>

namespace RuntimeStuff.Data
{
    using System;
    using System.Collections.Generic;
    using System.Linq.Expressions;

    public abstract class DbEntity<T> : DbEntityBase
        where T : class
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DbEntity{T}"/> class.
        /// </summary>
        protected DbEntity()
        {
            MemberCache.Create<T>();
        }

        public static DbEntityMap Map { get; set; }

        public static T SelectOne(Expression<Func<T, bool>> whereExpression)
        {
            return GetClient().First<T>(whereExpression);
        }

        public static IEnumerable<T> Select(Expression<Func<T, bool>> whereExpression)
        {
            return GetClient().ToList<T>(whereExpression);
        }

        public void Load(params object[] id)
        {
            GetClient().Fill<T>(this as T, id);
        }

        public void Save()
        {
            GetClient().Update<T>(this as T);
        }

        private static DbClient GetClient()
        {
            return ClientCache.GetOrAdd(GetConnection(typeof(T)), (c) => new DbClient(c, Map ?? DefaultMap ?? DbConnectionResolver.GlobalMap));
        }
    }
}