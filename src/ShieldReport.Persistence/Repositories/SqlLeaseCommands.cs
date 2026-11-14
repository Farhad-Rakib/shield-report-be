using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ShieldReport.Persistence.Context;

namespace ShieldReport.Persistence.Repositories;

// Small ADO.NET helper for the queue's atomic UPDATE ... OUTPUT claim statements. These can't be
// expressed in LINQ, and EF's SqlQuery may wrap raw SQL in a sub-select, which an UPDATE can't be.
internal static class SqlLeaseCommands
{
    public static async Task<long?> ExecuteClaimAsync(ApplicationDbContext dbContext, string sql, IReadOnlyList<SqlParameter> parameters, CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await dbContext.Database.OpenConnectionAsync(cancellationToken);
        }

        try
        {
            await using DbCommand command = connection.CreateCommand();
            command.CommandText = sql;
            var currentTransaction = dbContext.Database.CurrentTransaction;
            if (currentTransaction is not null)
            {
                command.Transaction = currentTransaction.GetDbTransaction();
            }

            foreach (var parameter in parameters)
            {
                command.Parameters.Add(parameter);
            }

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is null or DBNull ? null : Convert.ToInt64(result);
        }
        finally
        {
            if (openedHere)
            {
                await dbContext.Database.CloseConnectionAsync();
            }
        }
    }
}
