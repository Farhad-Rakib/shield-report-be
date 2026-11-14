using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Domain.Entities;
using ShieldReport.Domain.Enums;
using ShieldReport.Persistence.Context;

namespace ShieldReport.Persistence.Repositories;

public sealed class ScanRepository : BaseRepository<Scan>, IScanRepository
{
    private static readonly ScanStatus[] ActiveStatuses = [ScanStatus.Queued, ScanStatus.Running];

    public ScanRepository(ApplicationDbContext dbContext)
        : base(dbContext)
    {
    }

    public async Task<Scan?> GetByPublicIdAsync(Guid publicId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(x => x.ClientAsset)
            .Include(x => x.WorkerNode)
            .Include(x => x.NextScan)
            .FirstOrDefaultAsync(x => x.PublicId == publicId, cancellationToken);
    }

    public async Task<Scan?> GetByIdWithDetailsAsync(long id, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(x => x.ClientAsset)
            .Include(x => x.WorkerNode)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Scan>> GetByStatusesAsync(IReadOnlyCollection<ScanStatus> statuses, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(x => statuses.Contains(x.Status))
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    private const string ClaimSql = """
        WITH next AS (
            SELECT TOP (1) s.Id, s.LeaseOwner, s.LeaseExpiresAt
            FROM scans s WITH (UPDLOCK, READPAST, ROWLOCK)
            WHERE s.Tool = @tool
              AND (s.LeaseExpiresAt IS NULL OR s.LeaseExpiresAt < SYSUTCDATETIME())
              AND (
                    (s.Status = @queued AND NOT EXISTS (
                        SELECT 1 FROM scans p
                        WHERE p.NextScanId = s.Id AND p.Status IN (@queued, @running)))
                 OR s.Status = @running
              )
            ORDER BY CASE WHEN s.Status = @running THEN 0 ELSE 1 END, s.QueuedAt, s.Id
        )
        UPDATE next
        SET LeaseOwner = @owner,
            LeaseExpiresAt = DATEADD(SECOND, @leaseSeconds, SYSUTCDATETIME())
        OUTPUT inserted.Id;
        """;

    public async Task<long?> TryClaimNextAsync(ScanTool tool, string leaseOwner, TimeSpan leaseDuration, CancellationToken cancellationToken = default)
    {
        return await SqlLeaseCommands.ExecuteClaimAsync(
            DbContext,
            ClaimSql,
            [
                new SqlParameter("@tool", SqlDbType.TinyInt) { Value = (byte)tool },
                new SqlParameter("@queued", SqlDbType.TinyInt) { Value = (byte)ScanStatus.Queued },
                new SqlParameter("@running", SqlDbType.TinyInt) { Value = (byte)ScanStatus.Running },
                new SqlParameter("@owner", SqlDbType.NVarChar, 200) { Value = leaseOwner },
                new SqlParameter("@leaseSeconds", SqlDbType.Int) { Value = (int)leaseDuration.TotalSeconds },
            ],
            cancellationToken);
    }

    public async Task<bool> RenewLeaseAsync(long scanId, string leaseOwner, TimeSpan leaseDuration, CancellationToken cancellationToken = default)
    {
        var rows = await DbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE scans SET LeaseExpiresAt = DATEADD(SECOND, {(int)leaseDuration.TotalSeconds}, SYSUTCDATETIME()) WHERE Id = {scanId} AND LeaseOwner = {leaseOwner}",
            cancellationToken);
        return rows > 0;
    }

    public async Task ReleaseLeaseAsync(long scanId, string leaseOwner, CancellationToken cancellationToken = default)
    {
        await DbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE scans SET LeaseOwner = NULL, LeaseExpiresAt = NULL WHERE Id = {scanId} AND LeaseOwner = {leaseOwner}",
            cancellationToken);
    }

    public async Task<int> CountActiveByClientAsync(long clientOrganizationId, CancellationToken cancellationToken = default)
    {
        return await DbSet.CountAsync(
            x => x.ClientOrganizationId == clientOrganizationId && ActiveStatuses.Contains(x.Status),
            cancellationToken);
    }

    public async Task<(IReadOnlyList<Scan> Items, int Total)> GetPagedAsync(
        int page,
        int pageSize,
        long? clientOrganizationId,
        long? clientAssetId,
        long? engagementId,
        ScanStatus? status,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet.Include(x => x.ClientAsset).Include(x => x.NextScan).AsQueryable();

        if (clientOrganizationId.HasValue)
        {
            query = query.Where(x => x.ClientOrganizationId == clientOrganizationId.Value);
        }

        if (clientAssetId.HasValue)
        {
            query = query.Where(x => x.ClientAssetId == clientAssetId.Value);
        }

        if (engagementId.HasValue)
        {
            query = query.Where(x => x.EngagementId == engagementId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.QueuedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }
}
