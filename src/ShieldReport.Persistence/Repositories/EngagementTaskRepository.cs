using Microsoft.EntityFrameworkCore;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Domain.Entities;
using ShieldReport.Persistence.Context;

namespace ShieldReport.Persistence.Repositories;

public sealed class EngagementTaskRepository : BaseRepository<EngagementTask>, IEngagementTaskRepository
{
    public EngagementTaskRepository(ApplicationDbContext dbContext)
        : base(dbContext)
    {
    }

    public async Task<IReadOnlyList<EngagementTask>> GetByEngagementIdAsync(long engagementId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(x => x.AssignedToUser)
            .Include(x => x.Assets)
            .Where(x => x.EngagementId == engagementId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<EngagementTask?> GetByIdWithDetailsAsync(long id, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(x => x.AssignedToUser)
            .Include(x => x.Assets)
            .Include(x => x.Engagement)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }
}
