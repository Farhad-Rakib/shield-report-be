using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.Vulnerabilities;

public interface ICvssSeverityMappingService
{
    // Looks up CvssSeverityBand (a configurable lookup table, not a hardcoded enum mapping —
    // see DATABASE-DESIGN-PentestOps.md / PO-12) so an admin can retune band thresholds without
    // a code change. Used for manual vulnerability entry and NVD-sourced CVSS scores; scan
    // findings with their own raw severity string go through VulnerabilityDedupService instead.
    Task<Severity> MapAsync(decimal cvssScore, CancellationToken cancellationToken = default);
}
