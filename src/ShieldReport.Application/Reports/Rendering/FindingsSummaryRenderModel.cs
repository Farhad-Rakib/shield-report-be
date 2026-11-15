namespace ShieldReport.Application.Reports.Rendering;

// Variables of a "Findings summary" template (ReportTemplateKind.FindingsSummary): the Findings
// Report (clients → assets → vulnerabilities) for the filters chosen on that page. This IS the
// documented variable list for such templates (see ReportTemplateFields.FindingsSummaryVariables).
// Never contains scan-tool names. User text (e.g. Custom fields) must go through {{{text X}}}.
public sealed record FindingsSummaryRenderModel(
    string CompanyName,
    DateTime GeneratedAt,
    string GeneratedBy,
    string FilterDescription,                 // "All clients · All assets · Any date · All severities · All statuses"
    FindingsSummaryFilters Filters,
    FindingsSummaryTotals Summary,
    ReportRenderCounts Counts,                // all matching vulnerabilities by severity
    IReadOnlyList<FindingsSummaryClient> Clients,
    bool Truncated,                           // true when more than VulnerabilityLimit rows matched
    int VulnerabilityLimit,
    string SeverityChart,                     // PNG data URI (filled in by the renderer; empty when no findings)
    IReadOnlyDictionary<string, string>? Custom = null);

// Human-readable filter values ("All clients", "01/01/2026 – 31/03/2026", "High", "Open" …).
public sealed record FindingsSummaryFilters(string Client, string Asset, string Period, string Severity, string Status, string Engagement = "All engagements");

public sealed record FindingsSummaryTotals(int ClientCount, int AssetsTested, int TotalVulnerabilities, int OpenVulnerabilities, int ClosedVulnerabilities);

public sealed record FindingsSummaryClient(
    string Name,
    string Anchor,                            // client-1 (for the table of contents)
    int AssetsTested,
    int OpenVulnerabilities,
    ReportRenderCounts Counts,
    IReadOnlyList<FindingsSummaryAsset> Assets);

public sealed record FindingsSummaryAsset(
    string Name,
    string Anchor,                            // client-1-asset-2
    string? Identifier,
    string? AssetType,                        // "Web URL", "IP range" …
    DateTime? LastScannedAt,
    int ScanCount,
    int OpenVulnerabilities,
    ReportRenderCounts Counts,
    IReadOnlyList<FindingsSummaryVulnerability> Vulnerabilities);

public sealed record FindingsSummaryVulnerability(
    string Id,                                // VULN-123
    string Title,
    string Severity,                          // Critical | High | Medium | Low | Informational
    string? CvssScore,
    string Status,                            // Open | In progress | Patched | Accepted risk | Won't fix (as on the Findings Report page)
    string Endpoint,
    string? Engagement,
    DateTime FirstSeen,
    DateTime? ClosedAt);
