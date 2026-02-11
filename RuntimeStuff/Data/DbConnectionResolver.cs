using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace RuntimeStuff.Data
{
    public abstract class DbConnectionResolver
    {
        public abstract IDbConnection Resolve(Type entityType);

        public abstract IDbConnection Resolve<T>()
            where T : class;

        public static DbConnectionResolver Instance { get; set; }

        public static Func<IDbConnection> DefaultConnection { get; set; }

        public static Func<Type, IDbConnection> DefaultEntityConnection { get; set; }
    }
}
