using Microsoft.EntityFrameworkCore;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Domain.Entities;
using ShieldReport.Persistence.Context;

namespace ShieldReport.Persistence.Repositories;

public sealed class ClientAssetRepository : BaseRepository<ClientAsset>, IClientAssetRepository
{
    public ClientAssetRepository(ApplicationDbContext dbContext)
        : base(dbContext)
    {
    }

    public async Task<ClientAsset?> GetByIdWithDetailsAsync(long id, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(x => x.ClientOrganization)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<ClientAsset> Items, int Total)> GetPagedAsync(
        int page,
        int pageSize,
        string? search,
        long? clientOrganizationId,
        long? clientAssetId,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet.Include(x => x.ClientOrganization).AsQueryable();

        if (clientOrganizationId.HasValue)
        {
            query = query.Where(x => x.ClientOrganizationId == clientOrganizationId.Value);
        }

        if (clientAssetId.HasValue)
        {
            query = query.Where(x => x.Id == clientAssetId.Value);
        }

        // Date-only range; `to` is inclusive (whole end day).
        var fromDate = from?.Date;
        var toExclusive = to?.Date.AddDays(1);

        if (fromDate.HasValue)
        {
            query = query.Where(x => x.CreatedAt >= fromDate.Value);
        }

        if (toExclusive.HasValue)
        {
            query = query.Where(x => x.CreatedAt < toExclusive.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x => x.Name.Contains(search) || x.Identifier.Contains(search));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }
}
