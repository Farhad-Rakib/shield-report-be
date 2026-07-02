using Microsoft.EntityFrameworkCore;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Domain.Entities;
using ShieldReport.Persistence.Context;

namespace ShieldReport.Persistence.Repositories;

public sealed class ScanFindingRawRepository : BaseRepository<ScanFindingRaw>, IScanFindingRawRepository
{
    public ScanFindingRawRepository(ApplicationDbContext dbContext)
        : base(dbContext)
    {
    }

    public async Task<IReadOnlyList<ScanFindingRaw>> GetByScanIdAsync(long scanId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(x => x.ScanId == scanId)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }
}
