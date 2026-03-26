//using System.Data;

//public static class SqlRoutines<T>
//    where T : IDbConnection, new()
//{
//    /// <summary>  
//    /// SQL_STORED_PROCEDURE [dbo].[xp_UpdateStatusTM] @p_Table TableInt2CLMN  
//    /// </summary>  
//    // public static string XpUpdateStatusTM => "[dbo].[xp_UpdateStatusTM]";

//    public static IDbCommand XpUpdateStatusTMCommand(int p_Table) => CreateCommand("[dbo].[xp_UpdateStatusTM]", ("@p_Table", p_Table));
//    {
//    }

//    private static IDbCommand CreateCommand(string routineName, params (string pName, object pValue)[] parameters)
//    {
//        var cmd = new T().CreateCommand();
//        cmd.CommandText = "[dbo].[xp_UpdateStatusTM]";
//        cmd.CommandType = CommandType.StoredProcedure;
//        var p = cmd.CreateParameter();
//        p.ParameterName = "@p_Table";
//        p.DbType = DbType.Int32;
//        return cmd;
//    }
//}

//public class Test
//{
//    public void Run()
//    {
//        SqlRoutines<SqlConnection>.XpUpdateStatusTM(1).Ex;
//    }
//}