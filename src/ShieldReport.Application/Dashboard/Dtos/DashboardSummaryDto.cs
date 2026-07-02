namespace ShieldReport.Application.Dashboard.Dtos;

public sealed record DashboardSummaryDto(
    // Original RBAC-admin widgets — kept alongside the PentestOps widgets below so both
    // widget sets remain available/draggable on the dashboard.
    int TotalUsers,
    int ActiveUsers,
    int TotalRoles,
    int TotalPermissions,
    IReadOnlyList<UsersByRolePoint> UsersByRole,
    IReadOnlyList<RegistrationTrendPoint> RegistrationsTrend,
    IReadOnlyList<RecentUserDto> RecentUsers,
    // PentestOps widgets
    int TotalClients,
    int TotalEngagements,
    int TotalAssets,
    int OpenVulnerabilities,
    int CriticalVulnerabilities,
    int ActiveScans,
    IReadOnlyList<SeverityBreakdownPoint> VulnerabilitiesBySeverity,
    IReadOnlyList<EngagementStatusPoint> EngagementsByStatus,
    IReadOnlyList<RecentActivityDto> RecentActivity);

public sealed record UsersByRolePoint(string RoleName, int UserCount);

public sealed record RegistrationTrendPoint(string Month, int Count);

public sealed record RecentUserDto(
    long Id,
    string FullName,
    string Email,
    IReadOnlyList<string> Roles,
    bool IsActive,
    DateTime CreatedAt);

public sealed record SeverityBreakdownPoint(string Severity, int Count);

public sealed record EngagementStatusPoint(string Status, int Count);

public sealed record RecentActivityDto(
    string Type,
    string Title,
    string? ClientOrganizationName,
    string? Status,
    DateTime OccurredAt);

public sealed record DashboardFilter(long? ClientOrganizationId, DateTime? From, DateTime? To);
