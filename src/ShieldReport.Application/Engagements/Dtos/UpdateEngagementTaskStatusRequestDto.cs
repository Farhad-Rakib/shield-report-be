using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.Engagements.Dtos;

public sealed record UpdateEngagementTaskStatusRequestDto(EngagementTaskStatus Status);
