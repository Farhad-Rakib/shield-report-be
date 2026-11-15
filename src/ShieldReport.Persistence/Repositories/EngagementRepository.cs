using Microsoft.EntityFrameworkCore;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Domain.Entities;
using ShieldReport.Persistence.Context;

namespace ShieldReport.Persistence.Repositories;

public sealed class EngagementRepository : BaseRepository<Engagement>, IEngagementRepository
{
    public EngagementRepository(ApplicationDbContext dbContext)
        : base(dbContext)
    {
    }

    public async Task<Engagement?> GetByIdWithDetailsAsync(long id, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(x => x.ClientOrganization)
            .Include(x => x.LeadPentester)
            .Include(x => x.Assignees).ThenInclude(a => a.User)
            .Include(x => x.Tasks).ThenInclude(t => t.Assets)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<Engagement> Items, int Total)> GetPagedAsync(
        int page,
        int pageSize,
        string? search,
        long? clientOrganizationId,
        long? clientAssetId,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet
            .Include(x => x.ClientOrganization)
            .Include(x => x.LeadPentester)
            .AsQueryable();

        if (clientOrganizationId.HasValue)
        {
            query = query.Where(x => x.ClientOrganizationId == clientOrganizationId.Value);
        }

        // An engagement "covers" an asset if it has a scan, a finding or a task on that asset.
        if (clientAssetId.HasValue)
        {
            var assetId = clientAssetId.Value;
            query = query.Where(x =>
                DbContext.Scans.Any(s => s.EngagementId == x.Id && s.ClientAssetId == assetId)
                || DbContext.Vulnerabilities.Any(v => v.EngagementId == x.Id && v.ClientAssetId == assetId)
                || x.Tasks.Any(t => t.Assets.Any(a => a.ClientAssetId == assetId)));
        }

        // Date-only range; `to` is inclusive (whole end day).
        var fromDate = from?.Date;
        var toExclusive = to?.Date.AddDays(1);

        // Overlap with the engagement window; missing Start/End fall back to CreatedAt.
        if (fromDate.HasValue)
        {
            query = query.Where(x => (x.EndDate ?? x.StartDate ?? x.CreatedAt) >= fromDate.Value);
        }

        if (toExclusive.HasValue)
        {
            query = query.Where(x => (x.StartDate ?? x.EndDate ?? x.CreatedAt) < toExclusive.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x => x.Title.Contains(search));
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
