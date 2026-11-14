using Microsoft.EntityFrameworkCore;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Domain.Entities;
using ShieldReport.Persistence.Context;

namespace ShieldReport.Persistence.Repositories;

public sealed class ReportDeliveryRepository : BaseRepository<ReportDelivery>, IReportDeliveryRepository
{
    public ReportDeliveryRepository(ApplicationDbContext dbContext)
        : base(dbContext)
    {
    }

    public async Task<IReadOnlyList<ReportDelivery>> GetByReportIdAsync(long reportId, CancellationToken cancellationToken = default)
    {
        return await DbSet.AsNoTracking().Where(d => d.ReportId == reportId).OrderByDescending(d => d.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task<ReportDelivery?> GetByIdWithReportAsync(long id, CancellationToken cancellationToken = default)
    {
        // Runs in a background job (no user), so the tenant query filters don't restrict it.
        return await DbSet
            .Include(d => d.Report).ThenInclude(r => r.Engagement).ThenInclude(e => e.ClientOrganization)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }
}
