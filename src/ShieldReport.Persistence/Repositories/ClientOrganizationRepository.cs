using Microsoft.EntityFrameworkCore;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Domain.Entities;
using ShieldReport.Persistence.Context;

namespace ShieldReport.Persistence.Repositories;

public sealed class ClientOrganizationRepository : BaseRepository<ClientOrganization>, IClientOrganizationRepository
{
    public ClientOrganizationRepository(ApplicationDbContext dbContext)
        : base(dbContext)
    {
    }

    public async Task<bool> NameExistsAsync(string name, long? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = DbSet.Where(x => x.Name == name);
        if (excludeId.HasValue)
        {
            query = query.Where(x => x.Id != excludeId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<ClientOrganization> Items, int Total)> GetPagedAsync(
        int page,
        int pageSize,
        string? search,
        long? clientOrganizationId,
        long? clientAssetId,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsQueryable();

        if (clientOrganizationId.HasValue)
        {
            query = query.Where(x => x.Id == clientOrganizationId.Value);
        }

        // The organization that owns the asset.
        if (clientAssetId.HasValue)
        {
            var assetId = clientAssetId.Value;
            query = query.Where(x => DbContext.ClientAssets.Any(a => a.Id == assetId && a.ClientOrganizationId == x.Id));
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
            query = query.Where(x => x.Name.Contains(search));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }
}
