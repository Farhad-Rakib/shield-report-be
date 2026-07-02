namespace ShieldReport.Application.Engagements.Dtos;

public sealed record AssignEngagementUserRequestDto(long UserId, bool IsLead = false);
