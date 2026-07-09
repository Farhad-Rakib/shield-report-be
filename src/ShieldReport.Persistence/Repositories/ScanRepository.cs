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
