using Microsoft.EntityFrameworkCore;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Application.FindingsReport.Dtos;
using ShieldReport.Domain.Entities;
using ShieldReport.Domain.Enums;
using ShieldReport.Persistence.Context;

namespace ShieldReport.Persistence.Repositories;

/// <summary>
/// Read-only projections for the Findings Report. Goes through the normal DbSets, so the
/// context's global query filters (tenant + per-user visibility) apply to every subquery.
/// </summary>
public sealed class FindingsReportRepository : IFindingsReportRepository
{
    private readonly ApplicationDbContext _db;

    public FindingsReportRepository(ApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<FindingsReportAssetRow>> GetTestedAssetsAsync(FindingsReportFilter filter, CancellationToken cancellationToken = default)
    {
        var scans = ScansInRange(filter);
        var vulnerabilities = VulnerabilitiesInRange(filter);

        var query = _db.ClientAssets.AsNoTracking();
        if (filter.ClientOrganizationId.HasValue)
        {
            query = query.Where(a => a.ClientOrganizationId == filter.ClientOrganizationId.Value);
        }

        if (filter.ClientAssetId.HasValue)
        {
            query = query.Where(a => a.Id == filter.ClientAssetId.Value);
        }

        return await query
            .Where(a => scans.Any(s => s.ClientAssetId == a.Id) || vulnerabilities.Any(v => v.ClientAssetId == a.Id))
            .Select(a => new FindingsReportAssetRow(
                a.Id,
                a.ClientOrganizationId,
                a.ClientOrganization.Name,
                a.Name,
                a.Identifier,
                a.AssetType,
                scans.Count(s => s.ClientAssetId == a.Id),
                scans.Where(s => s.ClientAssetId == a.Id).Max(s => (DateTime?)(s.CompletedAt ?? s.StartedAt ?? s.QueuedAt))))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FindingsReportCountRow>> GetVulnerabilityCountsAsync(FindingsReportFilter filter, CancellationToken cancellationToken = default)
    {
        var rows = await MatchingVulnerabilities(filter)
            .GroupBy(v => new
            {
                v.ClientOrganizationId,
                ClientName = v.ClientOrganization.Name,
                v.ClientAssetId,
                Severity = v.SeverityOverride ?? v.Severity,
                v.PatchStatus,
            })
            .Select(g => new { g.Key.ClientOrganizationId, g.Key.ClientName, g.Key.ClientAssetId, g.Key.Severity, g.Key.PatchStatus, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => new { r.ClientOrganizationId, r.ClientName, r.ClientAssetId, r.Severity, IsOpen = IsOpen(r.PatchStatus) })
            .Select(g => new FindingsReportCountRow(g.Key.ClientOrganizationId, g.Key.ClientName, g.Key.ClientAssetId, g.Key.Severity, g.Key.IsOpen, g.Sum(r => r.Count)))
            .ToList();
    }

    public async Task<IReadOnlyList<FindingsReportVulnerabilityRow>> GetVulnerabilitiesAsync(FindingsReportFilter filter, int limit, CancellationToken cancellationToken = default)
    {
        return await MatchingVulnerabilities(filter)
            .OrderByDescending(v => v.SeverityOverride ?? v.Severity)
            .ThenByDescending(v => v.CvssScore)
            .ThenByDescending(v => v.FirstSeenAt)
            .Take(limit)
            .Select(v => new FindingsReportVulnerabilityRow(
                v.Id,
                v.ClientOrganizationId,
                v.ClientOrganization.Name,
                v.ClientAssetId,
                v.Title,
                v.SeverityOverride ?? v.Severity,
                v.CvssScore,
                v.PatchStatus,
                v.Endpoint,
                v.EngagementId,
                v.Engagement != null ? v.Engagement.Title : null,
                v.FirstSeenAt,
                v.ClosedAt))
            .ToListAsync(cancellationToken);
    }

    private static bool IsOpen(PatchStatus status) => status is PatchStatus.Open or PatchStatus.InProgress;

    private IQueryable<Scan> ScansInRange(FindingsReportFilter filter)
    {
        var query = _db.Scans.AsNoTracking();
        if (filter.ClientOrganizationId.HasValue)
        {
            query = query.Where(s => s.ClientOrganizationId == filter.ClientOrganizationId.Value);
        }

        if (filter.ClientAssetId.HasValue)
        {
            query = query.Where(s => s.ClientAssetId == filter.ClientAssetId.Value);
        }

        if (filter.EngagementId.HasValue)
        {
            query = query.Where(s => s.EngagementId == filter.EngagementId.Value);
        }

        if (filter.From.HasValue)
        {
            var from = filter.From.Value.Date;
            query = query.Where(s => s.QueuedAt >= from);
        }

        if (filter.To.HasValue)
        {
            var toExclusive = filter.To.Value.Date.AddDays(1);
            query = query.Where(s => s.QueuedAt < toExclusive);
        }

        return query;
    }

    private IQueryable<Vulnerability> VulnerabilitiesInRange(FindingsReportFilter filter)
    {
        var query = _db.Vulnerabilities.AsNoTracking();
        if (filter.ClientOrganizationId.HasValue)
        {
            query = query.Where(v => v.ClientOrganizationId == filter.ClientOrganizationId.Value);
        }

        if (filter.ClientAssetId.HasValue)
        {
            query = query.Where(v => v.ClientAssetId == filter.ClientAssetId.Value);
        }

        if (filter.EngagementId.HasValue)
        {
            query = query.Where(v => v.EngagementId == filter.EngagementId.Value);
        }

        if (filter.From.HasValue)
        {
            var from = filter.From.Value.Date;
            query = query.Where(v => v.FirstSeenAt >= from);
        }

        if (filter.To.HasValue)
        {
            var toExclusive = filter.To.Value.Date.AddDays(1);
            query = query.Where(v => v.FirstSeenAt < toExclusive);
        }

        return query;
    }

    private IQueryable<Vulnerability> MatchingVulnerabilities(FindingsReportFilter filter)
    {
        var query = VulnerabilitiesInRange(filter);
        if (filter.Severity.HasValue)
        {
            var severity = filter.Severity.Value;
            query = query.Where(v => (v.SeverityOverride ?? v.Severity) == severity);
        }

        if (filter.PatchStatus.HasValue)
        {
            var patchStatus = filter.PatchStatus.Value;
            query = query.Where(v => v.PatchStatus == patchStatus);
        }

        return query;
    }
}
