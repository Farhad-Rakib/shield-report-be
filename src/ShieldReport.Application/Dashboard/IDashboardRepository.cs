using ShieldReport.Application.Dashboard.Dtos;

namespace ShieldReport.Application.Dashboard;

public interface IDashboardRepository
{
    // Original RBAC-admin widgets
    Task<int> GetTotalUsersAsync(CancellationToken ct = default);
    Task<int> GetActiveUsersAsync(CancellationToken ct = default);
    Task<int> GetTotalRolesAsync(CancellationToken ct = default);
    Task<int> GetTotalPermissionsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<UsersByRolePoint>> GetUsersByRoleAsync(CancellationToken ct = default);
    Task<IReadOnlyList<RegistrationTrendPoint>> GetRegistrationsTrendAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<IReadOnlyList<RecentUserDto>> GetRecentUsersAsync(int count, CancellationToken ct = default);

    // PentestOps widgets
    Task<int> GetTotalClientsAsync(long? clientOrganizationId, CancellationToken ct = default);
    Task<int> GetTotalAssetsAsync(long? clientOrganizationId, CancellationToken ct = default);
    Task<int> GetTotalEngagementsAsync(long? clientOrganizationId, DateTime from, DateTime to, CancellationToken ct = default);
    Task<int> GetOpenVulnerabilitiesAsync(long? clientOrganizationId, DateTime from, DateTime to, CancellationToken ct = default);
    Task<int> GetCriticalVulnerabilitiesAsync(long? clientOrganizationId, DateTime from, DateTime to, CancellationToken ct = default);
    Task<int> GetActiveScansAsync(long? clientOrganizationId, CancellationToken ct = default);
    Task<IReadOnlyList<SeverityBreakdownPoint>> GetVulnerabilitiesBySeverityAsync(long? clientOrganizationId, DateTime from, DateTime to, CancellationToken ct = default);
    Task<IReadOnlyList<EngagementStatusPoint>> GetEngagementsByStatusAsync(long? clientOrganizationId, DateTime from, DateTime to, CancellationToken ct = default);
    Task<IReadOnlyList<RecentActivityDto>> GetRecentActivityAsync(long? clientOrganizationId, DateTime from, DateTime to, int count, CancellationToken ct = default);
}
