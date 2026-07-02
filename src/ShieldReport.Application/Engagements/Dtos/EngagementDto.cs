namespace ShieldReport.Application.Engagements.Dtos;

public sealed record EngagementDto(
    long Id,
    Guid PublicId,
    long ClientOrganizationId,
    string ClientOrganizationName,
    string Title,
    string? Scope,
    string Status,
    DateTime? StartDate,
    DateTime? EndDate,
    long LeadPentesterId,
    string LeadPentesterName,
    long CreatedByUserId,
    IReadOnlyList<EngagementAssigneeDto> Assignees);

public sealed record EngagementAssigneeDto(long UserId, string FullName, DateTime AssignedAt);
