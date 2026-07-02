namespace ShieldReport.Application.Engagements.Dtos;

public sealed record CreateEngagementTaskRequestDto(
    string Title,
    string? Description,
    long AssignedToUserId,
    long[] ClientAssetIds);
