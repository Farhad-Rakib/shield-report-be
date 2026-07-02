using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.Scans.Dtos;

public sealed record CreateScanRequestDto(ScanTool[] Tools, long? EngagementId);
