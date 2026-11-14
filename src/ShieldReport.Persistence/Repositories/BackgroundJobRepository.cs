using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Domain.Entities;
using ShieldReport.Domain.Enums;
using ShieldReport.Persistence.Context;

namespace ShieldReport.Persistence.Repositories;

public sealed class BackgroundJobRepository : BaseRepository<BackgroundJob>, IBackgroundJobRepository
{
    private const string ClaimSql = """
        WITH next AS (
            SELECT TOP (1) j.Id, j.Status, j.Attempts, j.LeaseOwner, j.LeaseExpiresAt
            FROM background_jobs j WITH (UPDLOCK, READPAST, ROWLOCK)
            WHERE (j.Status = @pending AND j.RunAfter <= SYSUTCDATETIME())
               OR (j.Status = @running AND (j.LeaseExpiresAt IS NULL OR j.LeaseExpiresAt < SYSUTCDATETIME()))
            ORDER BY j.RunAfter, j.Id
        )
        UPDATE next
        SET Status = @running,
            Attempts = Attempts + 1,
            LeaseOwner = @owner,
            LeaseExpiresAt = DATEADD(SECOND, @leaseSeconds, SYSUTCDATETIME())
        OUTPUT inserted.Id;
        """;

    public BackgroundJobRepository(ApplicationDbContext dbContext)
        : base(dbContext)
    {
    }

    public async Task<long?> TryClaimNextAsync(string leaseOwner, TimeSpan leaseDuration, CancellationToken cancellationToken = default)
    {
        return await SqlLeaseCommands.ExecuteClaimAsync(
            DbContext,
            ClaimSql,
            [
                new SqlParameter("@pending", SqlDbType.TinyInt) { Value = (byte)BackgroundJobStatus.Pending },
                new SqlParameter("@running", SqlDbType.TinyInt) { Value = (byte)BackgroundJobStatus.Running },
                new SqlParameter("@owner", SqlDbType.NVarChar, 200) { Value = leaseOwner },
                new SqlParameter("@leaseSeconds", SqlDbType.Int) { Value = (int)leaseDuration.TotalSeconds },
            ],
            cancellationToken);
    }

    public async Task<int> PurgeSucceededAsync(DateTime completedBefore, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(j => j.Status == BackgroundJobStatus.Succeeded && j.CompletedAt < completedBefore)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
