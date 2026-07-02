namespace ShieldReport.Application.Vulnerabilities.Dtos;

public sealed record FetchIntelligenceResponseDto(string CveId, string? Description, decimal? CvssScore, string? CvssVector, string? Severity);
