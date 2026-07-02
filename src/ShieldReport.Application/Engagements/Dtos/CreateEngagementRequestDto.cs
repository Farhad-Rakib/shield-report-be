namespace ShieldReport.Application.Engagements.Dtos;

public sealed record CreateEngagementRequestDto(
    long ClientOrganizationId,
    string Title,
    long LeadPentesterId,
    string? Scope,
    DateTime? StartDate,
    DateTime? EndDate);
