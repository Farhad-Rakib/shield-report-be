using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.Engagements.Dtos;

public sealed record UpdateEngagementRequestDto(
    string Title,
    string? Scope,
    DateTime? StartDate,
    DateTime? EndDate,
    EngagementStatus? Status);
