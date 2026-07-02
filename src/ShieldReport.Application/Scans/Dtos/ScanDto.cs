namespace ShieldReport.Application.Scans.Dtos;

public sealed record ScanDto(
    long Id,
    Guid PublicId,
    long ClientOrganizationId,
    long ClientAssetId,
    string ClientAssetName,
    long? EngagementId,
    long? EngagementTaskId,
    string Tool,
    string Status,
    DateTime QueuedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    long RequestedByUserId,
    string? ErrorMessage,
    string? RawOutput);
