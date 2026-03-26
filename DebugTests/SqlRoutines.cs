using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using RuntimeStuff.Extensions;

namespace DebugTests
{

    public interface ISqlRoutines
    {
        T xp_Genders_DA_NEW<T>(IDbConnection con, int docNum, int shop, int sendBoris);
    }

    public static class SqlRoutines
    {
        public static T xp_Genders_DA_NEW<T>(this IDbConnection con, int docNum, int shop, int sendBoris)
            where T : class
        {
            if (typeof(T) == typeof(DataTable))
            {
                return con.ToDataTable("EXEC xp_Genders_DA_NEW ") as T;
            }
            else
            {
                return con.ToList<T>("EXEC xp_Genders_DA_NEW ") as T;
            }
        }
    }
}
