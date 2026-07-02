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
