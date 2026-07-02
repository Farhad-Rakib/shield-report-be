namespace ShieldReport.Application.Scans.Dtos;

public sealed record ScanFindingRawDto(
    long Id,
    long ScanId,
    string RawOutputJson,
    string? ParsedTitle,
    string? ParsedEndpoint,
    string? ParsedSeverityRaw,
    DateTime? ProcessedAt,
    long? ResultingVulnerabilityId);
