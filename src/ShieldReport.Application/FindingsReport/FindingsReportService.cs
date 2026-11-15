using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Application.FindingsReport.Dtos;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.FindingsReport;

/// <summary>
/// Builds the Client → Asset → Vulnerability tree for the Findings Report from three queries:
/// tested assets, exact grouped counts, and the (capped) vulnerability rows. Counts always come
/// from the grouped query, so totals stay correct when the row list is truncated.
/// Visibility (tenant / assigned engagements) is applied by the DbContext's global query filters.
/// </summary>
public sealed class FindingsReportService : IFindingsReportService
{
    public const int VulnerabilityLimit = 5000;
    private const string UnassignedAssetName = "Unassigned asset";

    private readonly IFindingsReportRepository _repository;

    public FindingsReportService(IFindingsReportRepository repository) => _repository = repository;

    public async Task<FindingsReportDto> GetReportAsync(FindingsReportFilter filter, CancellationToken cancellationToken = default)
    {
        filter = Normalize(filter);

        // Sequential on purpose: one DbContext per request can't run queries concurrently.
        var assets = await _repository.GetTestedAssetsAsync(filter, cancellationToken);
        var counts = await _repository.GetVulnerabilityCountsAsync(filter, cancellationToken);
        var vulnerabilities = await _repository.GetVulnerabilitiesAsync(filter, VulnerabilityLimit + 1, cancellationToken);

        var truncated = vulnerabilities.Count > VulnerabilityLimit;
        if (truncated)
        {
            vulnerabilities = vulnerabilities.Take(VulnerabilityLimit).ToList();
        }

        var clientNames = new Dictionary<long, string>();
        foreach (var a in assets) clientNames[a.ClientOrganizationId] = a.ClientName;
        foreach (var c in counts) clientNames.TryAdd(c.ClientOrganizationId, c.ClientName);

        var countsByGroup = counts.ToLookup(c => (c.ClientOrganizationId, c.ClientAssetId));
        var vulnsByGroup = vulnerabilities.ToLookup(v => (v.ClientOrganizationId, v.ClientAssetId));
        var assetsByClient = assets.ToLookup(a => a.ClientOrganizationId);

        var clients = new List<FindingsReportClientDto>();
        foreach (var (clientId, clientName) in clientNames.OrderBy(kv => kv.Value, StringComparer.OrdinalIgnoreCase))
        {
            var assetDtos = new List<FindingsReportAssetDto>();
            var knownAssetIds = new HashSet<long>();

            foreach (var asset in assetsByClient[clientId])
            {
                knownAssetIds.Add(asset.Id);
                assetDtos.Add(BuildAsset(asset.Id, asset.Name, asset.Identifier, asset.AssetType, asset.LastScannedAt, asset.ScanCount,
                    countsByGroup[(clientId, asset.Id)], vulnsByGroup[(clientId, asset.Id)]));
            }

            // Findings on an asset that isn't in the tested list (e.g. the asset is hidden from this
            // user, or belongs to another org) still get a group so the numbers add up.
            var orphanAssetIds = countsByGroup
                .Where(g => g.Key.ClientOrganizationId == clientId && g.Key.ClientAssetId is long id && !knownAssetIds.Contains(id))
                .Select(g => g.Key.ClientAssetId!.Value)
                .ToList();
            foreach (var assetId in orphanAssetIds)
            {
                assetDtos.Add(BuildAsset(assetId, $"Asset #{assetId}", null, null, null, 0,
                    countsByGroup[(clientId, assetId)], vulnsByGroup[(clientId, assetId)]));
            }

            assetDtos = assetDtos
                .OrderByDescending(a => a.Counts.Critical)
                .ThenByDescending(a => a.Counts.High)
                .ThenByDescending(a => a.Counts.Total)
                .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var unassignedCounts = countsByGroup[(clientId, (long?)null)].ToList();
            if (unassignedCounts.Count > 0)
            {
                assetDtos.Add(BuildAsset(null, UnassignedAssetName, null, null, null, 0,
                    unassignedCounts, vulnsByGroup[(clientId, (long?)null)]));
            }

            var clientCounts = counts.Where(c => c.ClientOrganizationId == clientId).ToList();
            clients.Add(new FindingsReportClientDto(
                clientId,
                clientName,
                assetDtos.Count(a => a.Id != null),
                clientCounts.Where(c => c.IsOpen).Sum(c => c.Count),
                ToSeverityCounts(clientCounts),
                assetDtos));
        }

        var open = counts.Where(c => c.IsOpen).Sum(c => c.Count);
        var bySeverity = ToSeverityCounts(counts);
        var summary = new FindingsReportSummaryDto(
            clients.Count,
            clients.Sum(c => c.AssetsTested),
            bySeverity.Total,
            open,
            bySeverity.Total - open,
            bySeverity);

        return new FindingsReportDto(summary, clients, truncated, VulnerabilityLimit, DateTime.UtcNow);
    }

    private static FindingsReportFilter Normalize(FindingsReportFilter filter)
    {
        var from = filter.From?.Date;
        var to = filter.To?.Date;
        if (from.HasValue && to.HasValue && from > to)
        {
            (from, to) = (to, from);
        }

        return filter with { From = from, To = to };
    }

    private static FindingsReportAssetDto BuildAsset(
        long? id,
        string name,
        string? identifier,
        AssetType? assetType,
        DateTime? lastScannedAt,
        int scanCount,
        IEnumerable<FindingsReportCountRow> counts,
        IEnumerable<FindingsReportVulnerabilityRow> vulnerabilities)
    {
        var countList = counts.ToList();
        var vulnDtos = vulnerabilities
            .OrderByDescending(v => v.Severity)
            .ThenByDescending(v => v.CvssScore ?? -1m)
            .ThenByDescending(v => v.DetectedAt)
            .Select(v => new FindingsReportVulnerabilityDto(
                v.Id, v.Title, v.Severity, v.CvssScore, v.PatchStatus, v.Endpoint,
                v.EngagementId, v.EngagementTitle, v.DetectedAt, v.ClosedAt))
            .ToList();

        return new FindingsReportAssetDto(
            id, name, identifier, assetType, lastScannedAt, scanCount,
            countList.Where(c => c.IsOpen).Sum(c => c.Count),
            ToSeverityCounts(countList),
            vulnDtos);
    }

    private static SeverityCountsDto ToSeverityCounts(IEnumerable<FindingsReportCountRow> rows)
    {
        int critical = 0, high = 0, medium = 0, low = 0, info = 0;
        foreach (var row in rows)
        {
            switch (row.Severity)
            {
                case Severity.Critical: critical += row.Count; break;
                case Severity.High: high += row.Count; break;
                case Severity.Medium: medium += row.Count; break;
                case Severity.Low: low += row.Count; break;
                default: info += row.Count; break;
            }
        }

        return new SeverityCountsDto(critical, high, medium, low, info);
    }
}
