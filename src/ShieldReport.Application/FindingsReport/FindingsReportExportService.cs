using System.Globalization;
using ShieldReport.Application.Common.Exceptions;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Application.FindingsReport.Dtos;
using ShieldReport.Application.ReportTemplates;
using ShieldReport.Application.Reports.Rendering;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.FindingsReport;

public interface IFindingsReportExportService
{
    Task<IReadOnlyList<FindingsReportTemplateOptionDto>> GetTemplateOptionsAsync(CancellationToken cancellationToken = default);
    Task<FindingsReportPdfFile> ExportPdfAsync(ExportFindingsReportPdfRequestDto request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Renders the Findings Report for the given filters with a "Findings summary" report template to
/// PDF. Data comes from <see cref="IFindingsReportService"/>, so the caller's visibility (EF query
/// filters) applies exactly as on the page.
/// </summary>
public sealed class FindingsReportExportService : IFindingsReportExportService
{
    private const int MaxCustomValueLength = 10_000;

    private readonly IFindingsReportService _findingsReportService;
    private readonly IReportTemplateRepository _templateRepository;
    private readonly IClientOrganizationRepository _clientRepository;
    private readonly IClientAssetRepository _assetRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly IHtmlReportRenderer _renderer;

    public FindingsReportExportService(
        IFindingsReportService findingsReportService,
        IReportTemplateRepository templateRepository,
        IClientOrganizationRepository clientRepository,
        IClientAssetRepository assetRepository,
        ICurrentUserService currentUser,
        IHtmlReportRenderer renderer)
    {
        _findingsReportService = findingsReportService;
        _templateRepository = templateRepository;
        _clientRepository = clientRepository;
        _assetRepository = assetRepository;
        _currentUser = currentUser;
        _renderer = renderer;
    }

    public async Task<IReadOnlyList<FindingsReportTemplateOptionDto>> GetTemplateOptionsAsync(CancellationToken cancellationToken = default)
    {
        var templates = await _templateRepository.GetAllAsync(cancellationToken);
        return templates
            .Where(t => t.Kind == ReportTemplateKind.FindingsSummary)
            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .Select(t => new FindingsReportTemplateOptionDto(t.Id, t.Name, t.CompanyDisplayName, ReportTemplateFields.Parse(t.CustomFieldsJson)))
            .ToList();
    }

    public async Task<FindingsReportPdfFile> ExportPdfAsync(ExportFindingsReportPdfRequestDto request, CancellationToken cancellationToken = default)
    {
        var template = await _templateRepository.GetByIdAsync(request.TemplateId, cancellationToken)
            ?? throw new AppException("Report template not found.", 404);
        if (template.Kind != ReportTemplateKind.FindingsSummary)
        {
            throw new AppException("Choose a Findings summary template to export the Findings Report.", 400);
        }

        var values = request.CustomFieldValues ?? [];
        var tooLong = values.Where(kv => (kv.Value?.Length ?? 0) > MaxCustomValueLength).Select(kv => kv.Key).ToList();
        if (tooLong.Count > 0)
        {
            throw new AppException($"Value too long for: {string.Join(", ", tooLong)}.", 400);
        }

        var fields = ReportTemplateFields.Parse(template.CustomFieldsJson);
        var custom = ReportTemplateFields.Resolve(fields, values!);
        var missing = fields.Where(f => f.Required && string.IsNullOrWhiteSpace(custom[f.Key])).Select(f => f.Label).ToList();
        if (missing.Count > 0)
        {
            throw new AppException($"Fill in the required field(s): {string.Join(", ", missing)}.", 400);
        }

        var filter = request.Filters ?? new FindingsReportFilter(null, null, null, null, null, null);
        var report = await _findingsReportService.GetReportAsync(filter, cancellationToken);
        var filters = await DescribeFiltersAsync(filter, report, cancellationToken);
        var model = BuildModel(report, filters, template.CompanyDisplayName, custom);

        var html = await _renderer.RenderFindingsSummaryHtmlAsync(model, _renderer.ResolveTemplateHtml(template, TestingType.White), cancellationToken);
        var pdf = await _renderer.RenderPdfAsync(html, cancellationToken);
        var fileName = $"findings-report-{report.GeneratedAt:yyyyMMdd}.pdf";
        return new FindingsReportPdfFile(pdf, fileName, template.Id, template.Name);
    }

    private FindingsSummaryRenderModel BuildModel(FindingsReportDto report, FindingsSummaryFilters filters, string companyDisplayName, IReadOnlyDictionary<string, string> custom)
    {
        var clients = report.Clients.Select((client, ci) => new FindingsSummaryClient(
            client.Name,
            $"client-{ci + 1}",
            client.AssetsTested,
            client.OpenVulnerabilities,
            ToCounts(client.Counts),
            client.Assets.Select((asset, ai) => new FindingsSummaryAsset(
                asset.Name,
                $"client-{ci + 1}-asset-{ai + 1}",
                asset.Identifier,
                asset.AssetType is { } type ? AssetTypeLabel(type) : null,
                asset.LastScannedAt,
                asset.ScanCount,
                asset.OpenVulnerabilities,
                ToCounts(asset.Counts),
                asset.Vulnerabilities.Select(v => new FindingsSummaryVulnerability(
                    $"VULN-{v.Id:000}",
                    v.Title,
                    v.Severity.ToString(),
                    v.CvssScore?.ToString("0.0", CultureInfo.InvariantCulture),
                    StatusLabel(v.PatchStatus),
                    v.Endpoint,
                    v.EngagementTitle,
                    v.DetectedAt,
                    v.ClosedAt)).ToList())).ToList())).ToList();

        var summary = report.Summary;
        return new FindingsSummaryRenderModel(
            string.IsNullOrWhiteSpace(companyDisplayName) ? "Selise Digital" : companyDisplayName,
            report.GeneratedAt,
            _currentUser.Email ?? string.Empty,
            string.Join(" · ", filters.Client, filters.Asset, filters.Period, filters.Severity, filters.Status),
            filters,
            new FindingsSummaryTotals(summary.ClientCount, summary.AssetsTested, summary.TotalVulnerabilities, summary.OpenVulnerabilities, summary.ClosedVulnerabilities),
            ToCounts(summary.BySeverity),
            clients,
            report.Truncated,
            report.VulnerabilityLimit,
            string.Empty,
            custom);
    }

    // Names come from the report when it contains the client/asset; otherwise a lookup through the
    // query-filtered repositories (a record the user can't see falls back to "#id").
    private async Task<FindingsSummaryFilters> DescribeFiltersAsync(FindingsReportFilter filter, FindingsReportDto report, CancellationToken cancellationToken)
    {
        var client = "All clients";
        if (filter.ClientOrganizationId is long clientId)
        {
            client = report.Clients.FirstOrDefault(c => c.Id == clientId)?.Name
                ?? (await _clientRepository.GetByIdAsync(clientId, cancellationToken))?.Name
                ?? $"Client #{clientId}";
        }

        var asset = "All assets";
        if (filter.ClientAssetId is long assetId)
        {
            asset = report.Clients.SelectMany(c => c.Assets).FirstOrDefault(a => a.Id == assetId)?.Name
                ?? (await _assetRepository.GetByIdAsync(assetId, cancellationToken))?.Name
                ?? $"Asset #{assetId}";
        }

        DateTime? from = filter.From?.Date, to = filter.To?.Date;
        if (from > to) (from, to) = (to, from);
        static string D(DateTime d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var period = (from, to) switch
        {
            ({ } f, { } t) => $"{D(f)} – {D(t)}",
            ({ } f, null) => $"From {D(f)}",
            (null, { } t) => $"Until {D(t)}",
            _ => "Any date",
        };

        var severity = filter.Severity?.ToString() ?? "All severities";
        var status = filter.PatchStatus is { } ps ? StatusLabel(ps) : "All statuses";
        return new FindingsSummaryFilters(client, asset, period, severity, status);
    }

    private static ReportRenderCounts ToCounts(SeverityCountsDto c) => new(c.Critical, c.High, c.Medium, c.Low, c.Informational);

    // Same wording as the Findings Report page.
    private static string StatusLabel(PatchStatus status) => status switch
    {
        PatchStatus.Open => "Open",
        PatchStatus.InProgress => "In progress",
        PatchStatus.Patched => "Patched",
        PatchStatus.AcceptedRisk => "Accepted risk",
        PatchStatus.WontFix => "Won't fix",
        _ => status.ToString(),
    };

    private static string AssetTypeLabel(AssetType type) => type switch
    {
        AssetType.WebUrl => "Web URL",
        AssetType.IpRange => "IP range",
        AssetType.MobileApp => "Mobile app",
        AssetType.Api => "API",
        AssetType.CloudEnvironment => "Cloud",
        _ => "Other",
    };
}
