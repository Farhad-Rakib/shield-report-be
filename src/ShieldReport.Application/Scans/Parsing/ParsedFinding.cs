namespace ShieldReport.Application.Scans.Parsing;

public sealed record ParsedFinding(string Title, string Endpoint, string? SeverityRaw);
