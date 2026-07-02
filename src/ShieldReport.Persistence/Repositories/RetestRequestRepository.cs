using Microsoft.EntityFrameworkCore;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Domain.Entities;
using ShieldReport.Persistence.Context;

namespace ShieldReport.Persistence.Repositories;

public sealed class RetestRequestRepository : BaseRepository<RetestRequest>, IRetestRequestRepository
{
    public RetestRequestRepository(ApplicationDbContext dbContext)
        : base(dbContext)
    {
    }

    public async Task<RetestRequest?> GetLatestByVulnerabilityIdAsync(long vulnerabilityId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(x => x.RequestedByUser)
            .Include(x => x.AssignedToUser)
            .Include(x => x.ResolvedByUser)
            .Include(x => x.Cases)
            .Where(x => x.VulnerabilityId == vulnerabilityId)
            .OrderByDescending(x => x.RequestedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
