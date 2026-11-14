using Microsoft.EntityFrameworkCore;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Domain.Entities;
using ShieldReport.Persistence.Context;

namespace ShieldReport.Persistence.Repositories;

public sealed class ScanAuthorizationRepository : BaseRepository<ScanAuthorization>, IScanAuthorizationRepository
{
    public ScanAuthorizationRepository(ApplicationDbContext dbContext)
        : base(dbContext)
    {
    }

    public async Task<IReadOnlyList<ScanAuthorization>> GetByAssetAsync(long clientAssetId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(x => x.ClientAssetId == clientAssetId)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);
    }
}
