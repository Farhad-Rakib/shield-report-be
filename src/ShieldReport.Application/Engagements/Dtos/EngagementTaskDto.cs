namespace ShieldReport.Application.Engagements.Dtos;

public sealed record EngagementTaskDto(
    long Id,
    long EngagementId,
    string Title,
    string? Description,
    long AssignedToUserId,
    string AssignedToUserName,
    string Status,
    long CreatedByUserId,
    IReadOnlyList<long> ClientAssetIds);
