using ShieldReport.Application.FindingsReport.Dtos;

namespace ShieldReport.Application.Common.Interfaces;

public interface IFindingsReportRepository
{
    /// <summary>Assets with at least one scan (queued in range) or vulnerability (first seen in range).</summary>
    Task<IReadOnlyList<FindingsReportAssetRow>> GetTestedAssetsAsync(FindingsReportFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Exact vulnerability counts for the filter, grouped by client/asset/severity/open.</summary>
    Task<IReadOnlyList<FindingsReportCountRow>> GetVulnerabilityCountsAsync(FindingsReportFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Matching vulnerabilities, most severe first, at most <paramref name="limit"/> rows.</summary>
    Task<IReadOnlyList<FindingsReportVulnerabilityRow>> GetVulnerabilitiesAsync(FindingsReportFilter filter, int limit, CancellationToken cancellationToken = default);
}
