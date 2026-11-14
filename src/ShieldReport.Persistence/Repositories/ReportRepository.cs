using ShieldReport.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Domain.Entities;
using ShieldReport.Persistence.Context;

namespace ShieldReport.Persistence.Repositories;

public sealed class ReportRepository : IReportRepository
{
    private readonly ApplicationDbContext _dbContext;

    public ReportRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Report> AddAsync(Report entity, CancellationToken cancellationToken = default)
    {
        await _dbContext.Reports.AddAsync(entity, cancellationToken);
        return entity;
    }

    public async Task<Report?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Reports.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<(ReportStatus Status, ReportNarrativeStatus NarrativeStatus)?> GetNarrativeStateAsync(long id, CancellationToken cancellationToken = default)
    {
        var row = await _dbContext.Reports
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new { x.Status, x.NarrativeStatus })
            .FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : (row.Status, row.NarrativeStatus);
    }

    public async Task<Report?> GetByIdWithDetailsAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Reports
            .Include(x => x.Engagement)
                .ThenInclude(e => e.ClientOrganization)
            .Include(x => x.ReportTemplate)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Report>> GetByEngagementIdAsync(long engagementId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Reports
            .AsNoTracking()
            .Include(x => x.ReportTemplate)
            .Where(x => x.EngagementId == engagementId)
            .OrderByDescending(x => x.Version)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetLatestVersionAsync(long engagementId, CancellationToken cancellationToken = default)
    {
        var versions = await _dbContext.Reports
            .Where(x => x.EngagementId == engagementId)
            .Select(x => x.Version)
            .ToListAsync(cancellationToken);

        return versions.Count > 0 ? versions.Max() : 0;
    }

    public void Update(Report entity)
    {
        _dbContext.Reports.Update(entity);
    }

    public void Delete(Report entity)
    {
        _dbContext.Reports.Remove(entity);
    }

    public async Task<IReadOnlyList<Report>> GetApprovedByClientOrganizationAsync(long clientOrganizationId, CancellationToken cancellationToken = default)
    {
        // Explicit org + status filter on top of the Engagement tenant query filter (defense in depth).
        return await _dbContext.Reports
            .AsNoTracking()
            .Include(r => r.Engagement)
            .Where(r => r.Status == ShieldReport.Domain.Enums.ReportStatus.Approved && r.Engagement.ClientOrganizationId == clientOrganizationId)
            .OrderByDescending(r => r.ApprovedAt)
            .ToListAsync(cancellationToken);
    }
}
