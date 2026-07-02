using Microsoft.EntityFrameworkCore;
using ShieldReport.Application.Dashboard;
using ShieldReport.Application.Dashboard.Dtos;
using ShieldReport.Domain.Enums;
using ShieldReport.Persistence.Context;

namespace ShieldReport.Persistence.Repositories;

public sealed class DashboardRepository : IDashboardRepository
{
    private readonly ApplicationDbContext _db;

    public DashboardRepository(ApplicationDbContext db) => _db = db;

    // ── Original RBAC-admin widgets ─────────────────────────────────────────
    public Task<int> GetTotalUsersAsync(CancellationToken ct = default)
        => _db.Users.AsNoTracking().CountAsync(ct);

    public Task<int> GetActiveUsersAsync(CancellationToken ct = default)
        => _db.Users.AsNoTracking().CountAsync(u => u.IsActive, ct);

    public Task<int> GetTotalRolesAsync(CancellationToken ct = default)
        => _db.Roles.AsNoTracking().CountAsync(ct);

    public Task<int> GetTotalPermissionsAsync(CancellationToken ct = default)
        => _db.Permissions.AsNoTracking().CountAsync(ct);

    public async Task<IReadOnlyList<UsersByRolePoint>> GetUsersByRoleAsync(CancellationToken ct = default)
    {
        return await _db.Roles
            .AsNoTracking()
            .OrderByDescending(role => role.UserRoles.Count)
            .Select(role => new UsersByRolePoint(role.Name, role.UserRoles.Count))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<RegistrationTrendPoint>> GetRegistrationsTrendAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var registrations = await _db.Users
            .AsNoTracking()
            .Where(u => u.CreatedAt >= from && u.CreatedAt <= to)
            .Select(u => u.CreatedAt)
            .ToListAsync(ct);

        var buckets = new List<RegistrationTrendPoint>();
        for (var cursor = new DateTime(from.Year, from.Month, 1); cursor <= to; cursor = cursor.AddMonths(1))
        {
            var count = registrations.Count(c => c.Year == cursor.Year && c.Month == cursor.Month);
            buckets.Add(new RegistrationTrendPoint(cursor.ToString("MMM yyyy"), count));
        }

        return buckets;
    }

    public async Task<IReadOnlyList<RecentUserDto>> GetRecentUsersAsync(int count, CancellationToken ct = default)
    {
        return await _db.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .OrderByDescending(u => u.CreatedAt)
            .Take(count)
            .Select(u => new RecentUserDto(
                u.Id,
                u.FullName,
                u.Email,
                u.UserRoles.Select(ur => ur.Role.Name).ToList(),
                u.IsActive,
                u.CreatedAt))
            .ToListAsync(ct);
    }

    // ── PentestOps widgets ───────────────────────────────────────────────────
    public Task<int> GetTotalClientsAsync(long? clientOrganizationId, CancellationToken ct = default)
        => _db.ClientOrganizations.AsNoTracking()
            .Where(c => c.IsActive)
            .Where(c => clientOrganizationId == null || c.Id == clientOrganizationId)
            .CountAsync(ct);

    public Task<int> GetTotalAssetsAsync(long? clientOrganizationId, CancellationToken ct = default)
        => _db.ClientAssets.AsNoTracking()
            .Where(a => a.IsActive)
            .Where(a => clientOrganizationId == null || a.ClientOrganizationId == clientOrganizationId)
            .CountAsync(ct);

    public Task<int> GetTotalEngagementsAsync(long? clientOrganizationId, DateTime from, DateTime to, CancellationToken ct = default)
        => _db.Engagements.AsNoTracking()
            .Where(e => e.CreatedAt >= from && e.CreatedAt <= to)
            .Where(e => clientOrganizationId == null || e.ClientOrganizationId == clientOrganizationId)
            .CountAsync(ct);

    public Task<int> GetOpenVulnerabilitiesAsync(long? clientOrganizationId, DateTime from, DateTime to, CancellationToken ct = default)
        => _db.Vulnerabilities.AsNoTracking()
            .Where(v => v.ClosedAt == null)
            .Where(v => v.FirstSeenAt >= from && v.FirstSeenAt <= to)
            .Where(v => clientOrganizationId == null || v.ClientOrganizationId == clientOrganizationId)
            .CountAsync(ct);

    public Task<int> GetCriticalVulnerabilitiesAsync(long? clientOrganizationId, DateTime from, DateTime to, CancellationToken ct = default)
        => _db.Vulnerabilities.AsNoTracking()
            .Where(v => v.ClosedAt == null)
            .Where(v => (v.SeverityOverride ?? v.Severity) == Severity.Critical)
            .Where(v => v.FirstSeenAt >= from && v.FirstSeenAt <= to)
            .Where(v => clientOrganizationId == null || v.ClientOrganizationId == clientOrganizationId)
            .CountAsync(ct);

    public Task<int> GetActiveScansAsync(long? clientOrganizationId, CancellationToken ct = default)
        => _db.Scans.AsNoTracking()
            .Where(s => s.Status == ScanStatus.Queued || s.Status == ScanStatus.Running)
            .Where(s => clientOrganizationId == null || s.ClientOrganizationId == clientOrganizationId)
            .CountAsync(ct);

    public async Task<IReadOnlyList<SeverityBreakdownPoint>> GetVulnerabilitiesBySeverityAsync(long? clientOrganizationId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var rows = await _db.Vulnerabilities.AsNoTracking()
            .Where(v => v.ClosedAt == null)
            .Where(v => v.FirstSeenAt >= from && v.FirstSeenAt <= to)
            .Where(v => clientOrganizationId == null || v.ClientOrganizationId == clientOrganizationId)
            .GroupBy(v => v.SeverityOverride ?? v.Severity)
            .Select(g => new SeverityBreakdownPoint(g.Key.ToString(), g.Count()))
            .ToListAsync(ct);

        return rows;
    }

    public async Task<IReadOnlyList<EngagementStatusPoint>> GetEngagementsByStatusAsync(long? clientOrganizationId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var rows = await _db.Engagements.AsNoTracking()
            .Where(e => e.CreatedAt >= from && e.CreatedAt <= to)
            .Where(e => clientOrganizationId == null || e.ClientOrganizationId == clientOrganizationId)
            .GroupBy(e => e.Status)
            .Select(g => new EngagementStatusPoint(g.Key.ToString(), g.Count()))
            .ToListAsync(ct);

        return rows;
    }

    public async Task<IReadOnlyList<RecentActivityDto>> GetRecentActivityAsync(long? clientOrganizationId, DateTime from, DateTime to, int count, CancellationToken ct = default)
    {
        var engagements = await _db.Engagements.AsNoTracking()
            .Include(e => e.ClientOrganization)
            .Where(e => e.CreatedAt >= from && e.CreatedAt <= to)
            .Where(e => clientOrganizationId == null || e.ClientOrganizationId == clientOrganizationId)
            .OrderByDescending(e => e.CreatedAt)
            .Take(count)
            .Select(e => new RecentActivityDto("Engagement", e.Title, e.ClientOrganization.Name, e.Status.ToString(), e.CreatedAt))
            .ToListAsync(ct);

        var scans = await _db.Scans.AsNoTracking()
            .Include(s => s.ClientOrganization)
            .Include(s => s.ClientAsset)
            .Where(s => s.QueuedAt >= from && s.QueuedAt <= to)
            .Where(s => clientOrganizationId == null || s.ClientOrganizationId == clientOrganizationId)
            .OrderByDescending(s => s.QueuedAt)
            .Take(count)
            .Select(s => new RecentActivityDto("Scan", $"{s.Tool} scan — {s.ClientAsset.Name}", s.ClientOrganization.Name, s.Status.ToString(), s.QueuedAt))
            .ToListAsync(ct);

        var vulnerabilities = await _db.Vulnerabilities.AsNoTracking()
            .Include(v => v.ClientOrganization)
            .Where(v => v.FirstSeenAt >= from && v.FirstSeenAt <= to)
            .Where(v => clientOrganizationId == null || v.ClientOrganizationId == clientOrganizationId)
            .OrderByDescending(v => v.FirstSeenAt)
            .Take(count)
            .Select(v => new RecentActivityDto("Vulnerability", v.Title, v.ClientOrganization.Name, (v.SeverityOverride ?? v.Severity).ToString(), v.FirstSeenAt))
            .ToListAsync(ct);

        return engagements.Concat(scans).Concat(vulnerabilities)
            .OrderByDescending(a => a.OccurredAt)
            .Take(count)
            .ToList();
    }
}
