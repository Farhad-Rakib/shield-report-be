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
    string? RawOutput,
    Guid? NextScanPublicId,
    // Client-safe tool name (built-in label or the custom tool's client label).
    string ToolLabel = "",
    long? CustomToolId = null);
