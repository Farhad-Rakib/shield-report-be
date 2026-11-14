using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.Scans.Dtos;

// CustomToolIds: registered custom tools to run after the built-in ones (Tools may then be empty).
public sealed record CreateScanRequestDto(ScanTool[] Tools, long? EngagementId, long[]? CustomToolIds = null);
