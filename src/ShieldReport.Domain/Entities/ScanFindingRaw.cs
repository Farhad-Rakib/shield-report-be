namespace ShieldReport.Domain.Entities;

public sealed class ScanFindingRaw : BaseEntity
{
    public long ScanId { get; private set; }
    public string RawOutputJson { get; private set; } = string.Empty;
    public string? ParsedTitle { get; private set; }
    public string? ParsedEndpoint { get; private set; }
    public string? ParsedSeverityRaw { get; private set; }
    public DateTime? ProcessedAt { get; private set; }

    // Set regardless of whether this row created a new Vulnerability or matched/incremented
    // an existing one, so every raw row stays traceable to an outcome.
    public long? ResultingVulnerabilityId { get; private set; }

    public Scan Scan { get; private set; } = null!;

    private ScanFindingRaw()
    {
    }

    public ScanFindingRaw(long scanId, string rawOutputJson, string? parsedTitle = null, string? parsedEndpoint = null, string? parsedSeverityRaw = null)
    {
        ScanId = scanId;
        RawOutputJson = rawOutputJson ?? string.Empty;
        ParsedTitle = parsedTitle;
        ParsedEndpoint = parsedEndpoint;
        ParsedSeverityRaw = parsedSeverityRaw;
    }

    public void MarkProcessed(long? resultingVulnerabilityId)
    {
        ProcessedAt = DateTime.UtcNow;
        ResultingVulnerabilityId = resultingVulnerabilityId;
    }
}
