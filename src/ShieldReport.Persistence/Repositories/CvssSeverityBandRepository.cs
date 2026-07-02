using Microsoft.EntityFrameworkCore;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Domain.Entities;
using ShieldReport.Persistence.Context;

namespace ShieldReport.Persistence.Repositories;

public sealed class CvssSeverityBandRepository : BaseRepository<CvssSeverityBand>, ICvssSeverityBandRepository
{
    public CvssSeverityBandRepository(ApplicationDbContext dbContext)
        : base(dbContext)
    {
    }

    public async Task<CvssSeverityBand?> FindBandForScoreAsync(decimal score, CancellationToken cancellationToken = default)
    {
        return await DbSet.FirstOrDefaultAsync(b => score >= b.MinScore && score <= b.MaxScore, cancellationToken);
    }
}
