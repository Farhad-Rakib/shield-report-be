using ShieldReport.Application.Dashboard.Dtos;

namespace ShieldReport.Application.Dashboard;

public sealed class DashboardService : IDashboardService
{
    private readonly IDashboardRepository _repo;

    public DashboardService(IDashboardRepository repo) => _repo = repo;

    public async Task<DashboardSummaryDto> GetSummaryAsync(DashboardFilter filter, CancellationToken cancellationToken = default)
    {
        var to = filter.To ?? DateTime.UtcNow;
        var from = filter.From ?? to.AddMonths(-6);
        var clientOrganizationId = filter.ClientOrganizationId;

        var totalUsers = await _repo.GetTotalUsersAsync(cancellationToken);
        var activeUsers = await _repo.GetActiveUsersAsync(cancellationToken);
        var totalRoles = await _repo.GetTotalRolesAsync(cancellationToken);
        var totalPermissions = await _repo.GetTotalPermissionsAsync(cancellationToken);
        var usersByRole = await _repo.GetUsersByRoleAsync(cancellationToken);
        var registrationsTrend = await _repo.GetRegistrationsTrendAsync(from, to, cancellationToken);
        var recentUsers = await _repo.GetRecentUsersAsync(8, cancellationToken);

        var totalClients = await _repo.GetTotalClientsAsync(clientOrganizationId, cancellationToken);
        var totalAssets = await _repo.GetTotalAssetsAsync(clientOrganizationId, cancellationToken);
        var totalEngagements = await _repo.GetTotalEngagementsAsync(clientOrganizationId, from, to, cancellationToken);
        var openVulnerabilities = await _repo.GetOpenVulnerabilitiesAsync(clientOrganizationId, from, to, cancellationToken);
        var criticalVulnerabilities = await _repo.GetCriticalVulnerabilitiesAsync(clientOrganizationId, from, to, cancellationToken);
        var activeScans = await _repo.GetActiveScansAsync(clientOrganizationId, cancellationToken);
        var vulnerabilitiesBySeverity = await _repo.GetVulnerabilitiesBySeverityAsync(clientOrganizationId, from, to, cancellationToken);
        var engagementsByStatus = await _repo.GetEngagementsByStatusAsync(clientOrganizationId, from, to, cancellationToken);
        var recentActivity = await _repo.GetRecentActivityAsync(clientOrganizationId, from, to, 10, cancellationToken);

        return new DashboardSummaryDto(
            totalUsers, activeUsers, totalRoles, totalPermissions, usersByRole, registrationsTrend, recentUsers,
            totalClients, totalEngagements, totalAssets,
            openVulnerabilities, criticalVulnerabilities, activeScans,
            vulnerabilitiesBySeverity, engagementsByStatus, recentActivity);
    }
}
