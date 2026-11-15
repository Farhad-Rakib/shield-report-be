using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.FindingsReport.Dtos;

/// <summary>
/// Filter for the Findings Report. Dates are calendar days: <see cref="From"/> is inclusive from
/// 00:00, <see cref="To"/> is inclusive to the end of that day. Severity (effective, i.e. the
/// override when set) and PatchStatus narrow the vulnerabilities only: which assets count as
/// "tested" depends on client, asset and date range alone.
/// </summary>
public sealed record FindingsReportFilter(
    long? ClientOrganizationId,
    long? ClientAssetId,
    DateTime? From,
    DateTime? To,
    Severity? Severity,
    PatchStatus? PatchStatus,
    // Only scans and vulnerabilities of this engagement (and so the assets tested in it).
    long? EngagementId = null);

public sealed record SeverityCountsDto(int Critical, int High, int Medium, int Low, int Informational)
{
    public int Total => Critical + High + Medium + Low + Informational;

    public static readonly SeverityCountsDto Empty = new(0, 0, 0, 0, 0);
}

public sealed record FindingsReportSummaryDto(
    int ClientCount,
    int AssetsTested,
    int TotalVulnerabilities,
    int OpenVulnerabilities,
    int ClosedVulnerabilities,
    SeverityCountsDto BySeverity);

public sealed record FindingsReportVulnerabilityDto(
    long Id,
    string Title,
    Severity Severity,
    decimal? CvssScore,
    PatchStatus PatchStatus,
    string Endpoint,
    long? EngagementId,
    string? EngagementTitle,
    DateTime DetectedAt,
    DateTime? ClosedAt);

/// <summary>An asset under a client. <see cref="Id"/> is null for the "Unassigned asset" group.</summary>
public sealed record FindingsReportAssetDto(
    long? Id,
    string Name,
    string? Identifier,
    AssetType? AssetType,
    DateTime? LastScannedAt,
    int ScanCount,
    int OpenVulnerabilities,
    SeverityCountsDto Counts,
    IReadOnlyList<FindingsReportVulnerabilityDto> Vulnerabilities);

public sealed record FindingsReportClientDto(
    long Id,
    string Name,
    int AssetsTested,
    int OpenVulnerabilities,
    SeverityCountsDto Counts,
    IReadOnlyList<FindingsReportAssetDto> Assets);

public sealed record FindingsReportDto(
    FindingsReportSummaryDto Summary,
    IReadOnlyList<FindingsReportClientDto> Clients,
    bool Truncated,
    int VulnerabilityLimit,
    DateTime GeneratedAt);

// ── Raw rows returned by IFindingsReportRepository ─────────────────────────

public sealed record FindingsReportAssetRow(
    long Id,
    long ClientOrganizationId,
    string ClientName,
    string Name,
    string Identifier,
    AssetType AssetType,
    int ScanCount,
    DateTime? LastScannedAt);

public sealed record FindingsReportVulnerabilityRow(
    long Id,
    long ClientOrganizationId,
    string ClientName,
    long? ClientAssetId,
    string Title,
    Severity Severity,
    decimal? CvssScore,
    PatchStatus PatchStatus,
    string Endpoint,
    long? EngagementId,
    string? EngagementTitle,
    DateTime DetectedAt,
    DateTime? ClosedAt);

/// <summary>Vulnerability counts per (client, asset, effective severity, open/closed), computed in SQL.</summary>
public sealed record FindingsReportCountRow(
    long ClientOrganizationId,
    string ClientName,
    long? ClientAssetId,
    Severity Severity,
    bool IsOpen,
    int Count);
