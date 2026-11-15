using ShieldReport.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Domain.Entities;
using ShieldReport.Persistence.Context;

namespace ShieldReport.Persistence.Repositories;

// Queries that load whole Report rows run synchronously on purpose: RenderedHtml is a large
// nvarchar(max) (~0.5 MB per report) and Microsoft.Data.SqlClient 5.x reads large values very
// slowly in async mode (dotnet/SqlClient#593; ~1.5 s per report here, vs milliseconds sync). See
// "Avoid async for large text/binary columns" in the EF Core performance docs.
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

    public Task<Report?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_dbContext.Reports.FirstOrDefault(x => x.Id == id));
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

    public Task<Report?> GetByIdWithDetailsAsync(long id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_dbContext.Reports
            .Include(x => x.Engagement)
                .ThenInclude(e => e.ClientOrganization)
            .Include(x => x.ReportTemplate)
            .FirstOrDefault(x => x.Id == id));
    }

    public Task<IReadOnlyList<Report>> GetByEngagementIdAsync(long engagementId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<Report> reports = _dbContext.Reports
            .AsNoTracking()
            .Include(x => x.ReportTemplate)
            .Where(x => x.EngagementId == engagementId)
            .OrderByDescending(x => x.Version)
            .ToList();
        return Task.FromResult(reports);
    }

    public async Task<int> GetLatestVersionAsync(long engagementId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Reports
            .Where(x => x.EngagementId == engagementId)
            .MaxAsync(x => (int?)x.Version, cancellationToken) ?? 0;
    }

    public async Task AddWithNextVersionAsync(Report report, CancellationToken cancellationToken = default)
    {
        // Serialize version allocation per engagement: lock the engagement row (UPDLOCK, held to
        // commit) so concurrent generations queue for a few milliseconds instead of racing for
        // max(Version)+1. The transaction covers only the read + insert, never the rendering.
        // Joins an ambient transaction if the caller already opened one.
        var ownsTransaction = _dbContext.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction ? await _dbContext.Database.BeginTransactionAsync(cancellationToken) : null;

        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT Id FROM engagements WITH (UPDLOCK, HOLDLOCK) WHERE Id = {report.EngagementId}", cancellationToken);
        report.AssignVersion(await GetLatestVersionAsync(report.EngagementId, cancellationToken) + 1);
        await _dbContext.Reports.AddAsync(report, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
    }

    public void Update(Report entity)
    {
        _dbContext.Reports.Update(entity);
    }

    public void Delete(Report entity)
    {
        _dbContext.Reports.Remove(entity);
    }

    public Task<IReadOnlyList<Report>> GetApprovedByClientOrganizationAsync(long clientOrganizationId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Explicit org + status filter on top of the Engagement tenant query filter (defense in depth).
        IReadOnlyList<Report> reports = _dbContext.Reports
            .AsNoTracking()
            .Include(r => r.Engagement)
            .Where(r => r.Status == ShieldReport.Domain.Enums.ReportStatus.Approved && r.Engagement.ClientOrganizationId == clientOrganizationId)
            .OrderByDescending(r => r.ApprovedAt)
            .ToList();
        return Task.FromResult(reports);
    }
}
