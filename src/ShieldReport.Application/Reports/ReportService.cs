using System.Text.Json;
using Microsoft.Extensions.Logging;
using ShieldReport.Application.BackgroundJobs;
using ShieldReport.Application.Common.Exceptions;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Application.Common.Interfaces.Services;
using ShieldReport.Application.Reports.Dtos;
using ShieldReport.Application.Reports.Rendering;
using ShieldReport.Domain.Entities;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.Reports;

// Report generation. Two engines:
//   HTML (default): Handlebars template → HTML snapshot (Report.RenderedHtml) → PDF + Word, both
//                   produced at generate/regenerate time from the same snapshot.
//   Legacy .docx:   only for a ReportTemplate that has an uploaded custom .docx (OpenXML filling,
//                   PDF via LibreOffice) — kept so existing custom templates keep working.
public sealed class ReportService : IReportService
{
    private const int MaxScreenshotBytes = 5 * 1024 * 1024;
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".gif", ".webp" };

    private readonly IReportRepository _reportRepository;
    private readonly IEngagementRepository _engagementRepository;
    private readonly IVulnerabilityRepository _vulnerabilityRepository;
    private readonly IVulnerabilityAttachmentRepository _attachmentRepository;
    private readonly IReportTemplateRepository _reportTemplateRepository;
    private readonly IUserRepository _userRepository;
    private readonly IReportNarrativeDraftingService _narrativeDraftingService;
    private readonly IReportDocxTemplateFiller _docxTemplateFiller;
    private readonly IReportPdfConverter _pdfConverter;
    private readonly IHtmlReportRenderer _htmlRenderer;
    private readonly IFileStorageService _fileStorageService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBackgroundJobQueue _jobQueue;
    private readonly ILogger<ReportService> _logger;

    public ReportService(
        IReportRepository reportRepository,
        IEngagementRepository engagementRepository,
        IVulnerabilityRepository vulnerabilityRepository,
        IVulnerabilityAttachmentRepository attachmentRepository,
        IReportTemplateRepository reportTemplateRepository,
        IUserRepository userRepository,
        IReportNarrativeDraftingService narrativeDraftingService,
        IReportDocxTemplateFiller docxTemplateFiller,
        IReportPdfConverter pdfConverter,
        IHtmlReportRenderer htmlRenderer,
        IFileStorageService fileStorageService,
        IUnitOfWork unitOfWork,
        IBackgroundJobQueue jobQueue,
        ILogger<ReportService> logger)
    {
        _jobQueue = jobQueue;
        _logger = logger;
        _reportRepository = reportRepository;
        _engagementRepository = engagementRepository;
        _vulnerabilityRepository = vulnerabilityRepository;
        _attachmentRepository = attachmentRepository;
        _reportTemplateRepository = reportTemplateRepository;
        _userRepository = userRepository;
        _narrativeDraftingService = narrativeDraftingService;
        _docxTemplateFiller = docxTemplateFiller;
        _pdfConverter = pdfConverter;
        _htmlRenderer = htmlRenderer;
        _fileStorageService = fileStorageService;
        _unitOfWork = unitOfWork;
    }

    public async Task<ReportDto> GenerateAsync(long engagementId, long currentUserId, GenerateReportRequestDto request, CancellationToken cancellationToken = default)
    {
        var engagement = await _engagementRepository.GetByIdWithDetailsAsync(engagementId, cancellationToken)
            ?? throw new AppException("Engagement not found.", 404);
        var template = await ResolveTemplateAsync(request.TemplateId, cancellationToken);
        var customValuesJson = BuildCustomFieldValuesJson(template, request.CustomFieldValues, enforceRequired: true);

        if (request.TestingType is { } testingType && testingType != engagement.TestingType)
        {
            engagement.SetTestingType(testingType);   // tracked; saved with the report
        }

        var includedFindings = await _vulnerabilityRepository.GetReportIncludedByEngagementIdAsync(engagementId, cancellationToken);
        var nextVersion = await _reportRepository.GetLatestVersionAsync(engagementId, cancellationToken) + 1;

        // Never wait on the LLM here (a slow or failing provider used to time the request out):
        // render now with placeholder text and let a background job fill the narrative in.
        var placeholder = _narrativeDraftingService.PlaceholderText;
        var draftWithAi = request.DraftWithAi && await _narrativeDraftingService.IsAvailableAsync(cancellationToken);

        var report = new Report(engagementId, template.Id, nextVersion, currentUserId, placeholder, placeholder, placeholder);
        report.SetSections(
            ReportSectionDefaults.ProjectGoals(engagement.ClientOrganization?.Name ?? "the client"),
            ReportSectionDefaults.Limitations(engagement.TestingType),
            "{}",
            request.ShowToolNames);
        report.SetCustomFieldValues(customValuesJson);

        if (UsesLegacyDocxEngine(template))
        {
            var narrative = new ReportNarrative(placeholder, placeholder, placeholder);
            var docxBytes = _docxTemplateFiller.Fill(engagement, engagement.ClientOrganization, template, includedFindings, narrative, nextVersion);
            report.MarkDraftGenerated(await SaveFileAsync(engagementId, $"report-v{nextVersion}.docx", docxBytes, cancellationToken));
        }
        else
        {
            await RenderHtmlEngineAsync(report, engagement, template, includedFindings, cancellationToken);
        }

        await _reportRepository.AddAsync(report, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (draftWithAi)
        {
            // Needs the report id, so it is queued after the first save.
            report.StartNarrativeDrafting();
            _reportRepository.Update(report);
            await _jobQueue.EnqueueAsync(ReportNarrativeJobHandler.Type, new ReportNarrativeJobPayload(report.Id), maxAttempts: 1, cancellationToken: cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        report = await _reportRepository.GetByIdWithDetailsAsync(report.Id, cancellationToken) ?? report;
        return MapToDto(report);
    }

    public async Task<string> PreviewNewAsync(long engagementId, long currentUserId, ReportPreviewRequestDto request, CancellationToken cancellationToken = default)
    {
        var engagement = await _engagementRepository.GetByIdWithDetailsAsync(engagementId, cancellationToken)
            ?? throw new AppException("Engagement not found.", 404);
        var testingType = request.TestingType ?? engagement.TestingType;
        var template = await ResolveTemplateAsync(request.TemplateId, cancellationToken);
        if (UsesLegacyDocxEngine(template))
        {
            throw new AppException($"\"{template.Name}\" is a legacy Word template and has no HTML preview. Generate the report and download the Word file instead.", 400);
        }

        var includedFindings = await _vulnerabilityRepository.GetReportIncludedByEngagementIdAsync(engagementId, cancellationToken);
        var nextVersion = await _reportRepository.GetLatestVersionAsync(engagementId, cancellationToken) + 1;

        const string pending = "Drafted automatically after you generate the report (or written by you if no AI provider is active).";
        // Transient: never added to the DbContext, so nothing is saved.
        var report = new Report(engagementId, template.Id, nextVersion, currentUserId, pending, pending, pending);
        report.SetSections(
            ReportSectionDefaults.ProjectGoals(engagement.ClientOrganization?.Name ?? "the client"),
            ReportSectionDefaults.Limitations(testingType),
            "{}",
            request.ShowToolNames);
        report.SetCustomFieldValues(BuildCustomFieldValuesJson(template, request.CustomFieldValues, enforceRequired: false));

        var model = await BuildRenderModelAsync(report, engagement, template, includedFindings, cancellationToken, testingType);
        return await _htmlRenderer.RenderHtmlAsync(model, _htmlRenderer.ResolveTemplateHtml(template, testingType), cancellationToken);
    }

    public async Task<ReportDto> RequestNarrativeDraftAsync(long id, CancellationToken cancellationToken = default)
    {
        var report = await _reportRepository.GetByIdWithDetailsAsync(id, cancellationToken)
            ?? throw new AppException("Report not found.", 404);
        if (report.Status != ReportStatus.PendingApproval)
        {
            throw new AppException("Only a pending-approval report can be redrafted.", 409);
        }

        if (report.NarrativeStatus == ReportNarrativeStatus.Drafting)
        {
            return MapToDto(report);
        }

        if (!await _narrativeDraftingService.IsAvailableAsync(cancellationToken))
        {
            throw new AppException("No AI provider is active. Activate one under Settings → LLM Providers.", 400);
        }

        report.StartNarrativeDrafting();
        _reportRepository.Update(report);
        await _jobQueue.EnqueueAsync(ReportNarrativeJobHandler.Type, new ReportNarrativeJobPayload(report.Id), maxAttempts: 1, cancellationToken: cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapToDto(report);
    }

    public async Task CompleteNarrativeDraftAsync(long id, CancellationToken cancellationToken = default)
    {
        var report = await _reportRepository.GetByIdWithDetailsAsync(id, cancellationToken);
        if (report is null || report.Status != ReportStatus.PendingApproval || report.NarrativeStatus != ReportNarrativeStatus.Drafting)
        {
            return;   // deleted, approved, or a person edited the narrative meanwhile
        }

        var includedFindings = await _vulnerabilityRepository.GetReportIncludedByEngagementIdAsync(report.EngagementId, cancellationToken);
        var draftEngagement = await _engagementRepository.GetByIdWithDetailsAsync(report.EngagementId, cancellationToken);
        ReportNarrative? narrative;
        try
        {
            narrative = await _narrativeDraftingService.DraftAsync(includedFindings, draftEngagement, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "AI drafting of the narrative for report {ReportId} failed.", id);
            report.FailNarrativeDrafting(DescribeLlmError(ex));
            _reportRepository.Update(report);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        if (narrative is null)
        {
            report.FailNarrativeDrafting("No AI provider is active.");
            _reportRepository.Update(report);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        // Re-check against the database (not the tracked copy): a person may have edited the
        // narrative, approved or deleted the report while the LLM was working.
        var current = await _reportRepository.GetNarrativeStateAsync(id, cancellationToken);
        if (current is not { Status: ReportStatus.PendingApproval, NarrativeStatus: ReportNarrativeStatus.Drafting })
        {
            return;
        }

        report.ApplyDraftedNarrative(narrative.ExecutiveSummary, narrative.KeyFindings, narrative.KeyRecommendations);
        var engagement = draftEngagement ?? throw new InvalidOperationException("Engagement not found.");
        if (UsesLegacyDocxEngine(report.ReportTemplate) && report.RenderedHtml is null)
        {
            var docxBytes = _docxTemplateFiller.Fill(engagement, engagement.ClientOrganization, report.ReportTemplate, includedFindings, narrative, report.Version);
            report.MarkDraftGenerated(await SaveFileAsync(report.EngagementId, $"report-v{report.Version}.docx", docxBytes, cancellationToken));
        }
        else
        {
            await RenderHtmlEngineAsync(report, engagement, report.ReportTemplate, includedFindings, cancellationToken);
        }

        _reportRepository.Update(report);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static string DescribeLlmError(Exception ex) => ex switch
    {
        TaskCanceledException => "The AI provider did not answer within 60 seconds.",
        HttpRequestException { StatusCode: { } code } => $"The AI provider returned HTTP {(int)code} ({code}). Check the provider under Settings → LLM Providers and use Test.",
        _ => ex.Message,
    };

    // Engagement-kind templates only (Findings summary templates belong to the Findings Report),
    // with their dynamic fields so the Generate dialog can ask for the values.
    public async Task<IReadOnlyList<ReportTemplateOptionDto>> GetTemplateOptionsAsync(CancellationToken cancellationToken = default)
    {
        var templates = await _reportTemplateRepository.GetAllAsync(cancellationToken);
        return templates
            .Where(t => t.Kind == ReportTemplateKind.Engagement)
            .OrderBy(t => t.BuiltInTestingType is null).ThenBy(t => t.BuiltInTestingType).ThenBy(t => t.Name)
            .Select(t => new ReportTemplateOptionDto(
                t.Id, t.Name, UsesLegacyDocxEngine(t) ? "Word" : "Html", !string.IsNullOrWhiteSpace(t.HtmlContent), t.BuiltInTestingType,
                ShieldReport.Application.ReportTemplates.ReportTemplateFields.Parse(t.CustomFieldsJson)))
            .ToList();
    }

    // There is no default template: the caller must name an Engagement-kind template.
    private async Task<ReportTemplate> ResolveTemplateAsync(long? templateId, CancellationToken cancellationToken)
    {
        if (templateId is not { } id)
        {
            throw new AppException("Choose a report template.", 400);
        }

        var template = await _reportTemplateRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppException("Report template not found.", 404);
        if (template.Kind != ReportTemplateKind.Engagement)
        {
            throw new AppException($"\"{template.Name}\" is a Findings summary template. Use it from Reporting -> Findings Report.", 400);
        }

        return template;
    }

    // Keeps only the template's own field keys (10,000 chars max each). With enforceRequired, a
    // required field with neither a value nor a default is rejected.
    private static string BuildCustomFieldValuesJson(ReportTemplate template, IReadOnlyDictionary<string, string>? values, bool enforceRequired)
    {
        var fields = ShieldReport.Application.ReportTemplates.ReportTemplateFields.Parse(template.CustomFieldsJson);
        var clean = fields
            .Where(f => values is not null && values.TryGetValue(f.Key, out var v) && !string.IsNullOrWhiteSpace(v))
            .ToDictionary(f => f.Key, f => values![f.Key].Length > 10_000 ? values[f.Key][..10_000] : values[f.Key]);

        if (enforceRequired)
        {
            var missing = fields
                .Where(f => f.Required && !clean.ContainsKey(f.Key) && string.IsNullOrWhiteSpace(f.DefaultValue))
                .Select(f => f.Label)
                .ToList();
            if (missing.Count > 0)
            {
                throw new AppException($"Fill in the required field(s): {string.Join(", ", missing)}.", 400);
            }
        }

        return JsonSerializer.Serialize(clean);
    }

    public async Task<ReportDto> RegenerateAsync(long id, CancellationToken cancellationToken = default)
    {
        var report = await _reportRepository.GetByIdWithDetailsAsync(id, cancellationToken)
            ?? throw new AppException("Report not found.", 404);

        if (report.Status != ReportStatus.PendingApproval)
        {
            throw new AppException("Only a pending-approval report can be regenerated. Generate a new version instead.", 409);
        }

        var engagement = await _engagementRepository.GetByIdWithDetailsAsync(report.EngagementId, cancellationToken)
            ?? throw new AppException("Engagement not found.", 404);
        var includedFindings = await _vulnerabilityRepository.GetReportIncludedByEngagementIdAsync(report.EngagementId, cancellationToken);

        // Uses the report's current (possibly edited) sections — the LLM only drafts at Generate time.
        if (UsesLegacyDocxEngine(report.ReportTemplate) && report.RenderedHtml is null)
        {
            var narrative = new ReportNarrative(report.ExecutiveSummary, report.KeyFindings, report.KeyRecommendations);
            var docxBytes = _docxTemplateFiller.Fill(engagement, engagement.ClientOrganization, report.ReportTemplate, includedFindings, narrative, report.Version);
            report.MarkDraftGenerated(await SaveFileAsync(report.EngagementId, $"report-v{report.Version}.docx", docxBytes, cancellationToken));
        }
        else
        {
            await RenderHtmlEngineAsync(report, engagement, report.ReportTemplate, includedFindings, cancellationToken);
        }

        _reportRepository.Update(report);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapToDto(report);
    }

    public async Task<IReadOnlyList<ReportDto>> ListAsync(long engagementId, CancellationToken cancellationToken = default)
    {
        var reports = await _reportRepository.GetByEngagementIdAsync(engagementId, cancellationToken);
        return reports.Select(MapToDto).ToList();
    }

    public async Task<ReportDto> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var report = await _reportRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppException("Report not found.", 404);
        return MapToDto(report);
    }

    public async Task<ReportDto> UpdateNarrativeAsync(long id, UpdateReportNarrativeRequestDto request, CancellationToken cancellationToken = default)
    {
        var report = await _reportRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppException("Report not found.", 404);

        report.SetNarrative(request.ExecutiveSummary, request.KeyFindings, request.KeyRecommendations);
        _reportRepository.Update(report);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapToDto(report);
    }

    public async Task<ReportDto> UpdateSectionsAsync(long id, UpdateReportSectionsRequestDto request, CancellationToken cancellationToken = default)
    {
        var report = await _reportRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppException("Report not found.", 404);

        if (request.ProjectGoals.Length > 20000 || request.Limitations.Length > 20000)
        {
            throw new AppException("Sections must be 20,000 characters or fewer.", 400);
        }

        var statuses = (request.ScopeStatuses ?? new Dictionary<long, string>())
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
            .ToDictionary(kv => kv.Key.ToString(), kv => kv.Value.Trim().Length > 100 ? kv.Value.Trim()[..100] : kv.Value.Trim());

        try
        {
            report.SetSections(request.ProjectGoals.Trim(), request.Limitations.Trim(), JsonSerializer.Serialize(statuses), request.ShowToolNames);
            if (request.CustomFieldValues is not null)
            {
                var template = await _reportTemplateRepository.GetByIdAsync(report.ReportTemplateId, cancellationToken);
                var keys = ShieldReport.Application.ReportTemplates.ReportTemplateFields.Parse(template?.CustomFieldsJson).Select(f => f.Key).ToHashSet();
                var values = request.CustomFieldValues
                    .Where(kv => keys.Contains(kv.Key))
                    .ToDictionary(kv => kv.Key, kv => (kv.Value ?? string.Empty).Length > 10_000 ? kv.Value![..10_000] : kv.Value ?? string.Empty);
                report.SetCustomFieldValues(JsonSerializer.Serialize(values));
            }
        }
        catch (InvalidOperationException ex)
        {
            throw new AppException(ex.Message, 409);
        }

        _reportRepository.Update(report);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapToDto(report);
    }

    public async Task<string?> GetPreviewHtmlAsync(long id, CancellationToken cancellationToken = default)
    {
        var report = await _reportRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppException("Report not found.", 404);
        return report.RenderedHtml;
    }

    public async Task<ReportDto> ApproveAsync(long id, long currentUserId, CancellationToken cancellationToken = default)
    {
        var report = await _reportRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppException("Report not found.", 404);

        report.Approve(currentUserId);
        _reportRepository.Update(report);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapToDto(report);
    }

    public async Task<ReportDto> RejectAsync(long id, long currentUserId, string reason, CancellationToken cancellationToken = default)
    {
        var report = await _reportRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppException("Report not found.", 404);

        report.Reject(currentUserId, reason);
        _reportRepository.Update(report);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapToDto(report);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var report = await _reportRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppException("Report not found.", 404);

        if (!string.IsNullOrWhiteSpace(report.DraftDocxPath))
        {
            _fileStorageService.Delete(report.DraftDocxPath);
        }

        if (!string.IsNullOrWhiteSpace(report.FinalPdfPath))
        {
            _fileStorageService.Delete(report.FinalPdfPath);
        }

        _reportRepository.Delete(report);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    // HTML-engine reports already have their PDF; this re-prints it from the stored snapshot (e.g.
    // after a file was lost). Legacy .docx reports convert via LibreOffice as before.
    public async Task<ReportDto> ConvertToPdfAsync(long id, CancellationToken cancellationToken = default)
    {
        var report = await _reportRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppException("Report not found.", 404);

        byte[] pdfBytes;
        if (report.RenderedHtml is not null)
        {
            pdfBytes = await _htmlRenderer.RenderPdfAsync(report.RenderedHtml, cancellationToken);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(report.DraftDocxPath))
            {
                throw new AppException("This report has no draft document to convert yet.", 400);
            }

            pdfBytes = await _pdfConverter.ConvertAsync(await ReadAllBytesAsync(report.DraftDocxPath, cancellationToken), cancellationToken);
        }

        report.MarkPdfGenerated(await SaveFileAsync(report.EngagementId, $"report-v{report.Version}.pdf", pdfBytes, cancellationToken));
        _reportRepository.Update(report);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapToDto(report);
    }

    public async Task<(Stream Content, string FileName)> GetDraftFileAsync(long id, CancellationToken cancellationToken = default)
    {
        var report = await _reportRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppException("Report not found.", 404);

        if (string.IsNullOrWhiteSpace(report.DraftDocxPath))
        {
            throw new AppException("This report has no Word document.", 404);
        }

        var stream = _fileStorageService.OpenRead(report.DraftDocxPath)
            ?? throw new AppException("The Word document is missing from storage.", 404);

        return (stream, $"report-v{report.Version}.docx");
    }

    public async Task<(Stream Content, string FileName)> GetPdfFileAsync(long id, CancellationToken cancellationToken = default)
    {
        var report = await _reportRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppException("Report not found.", 404);

        if (string.IsNullOrWhiteSpace(report.FinalPdfPath))
        {
            throw new AppException("This report has no PDF yet.", 404);
        }

        var stream = _fileStorageService.OpenRead(report.FinalPdfPath)
            ?? throw new AppException("The PDF file is missing from storage.", 404);

        return (stream, $"report-v{report.Version}.pdf");
    }

    // ------------------------------------------------------------------ HTML engine

    private static bool UsesLegacyDocxEngine(ReportTemplate template) =>
        !string.IsNullOrWhiteSpace(template.DocxTemplatePath) && string.IsNullOrWhiteSpace(template.HtmlContent);

    private async Task RenderHtmlEngineAsync(Report report, Engagement engagement, ReportTemplate template, IReadOnlyList<Vulnerability> findings, CancellationToken cancellationToken)
    {
        var model = await BuildRenderModelAsync(report, engagement, template, findings, cancellationToken);
        var html = await _htmlRenderer.RenderHtmlAsync(model, _htmlRenderer.ResolveTemplateHtml(template, engagement.TestingType), cancellationToken);
        var pdf = await _htmlRenderer.RenderPdfAsync(html, cancellationToken);
        var docx = await _htmlRenderer.RenderDocxAsync(html, cancellationToken);

        var pdfPath = await SaveFileAsync(report.EngagementId, $"report-v{report.Version}.pdf", pdf, cancellationToken);
        var docxPath = await SaveFileAsync(report.EngagementId, $"report-v{report.Version}.docx", docx, cancellationToken);
        report.MarkRendered(html, docxPath, pdfPath);
    }

    private async Task<ReportRenderModel> BuildRenderModelAsync(Report report, Engagement engagement, ReportTemplate template, IReadOnlyList<Vulnerability> findings, CancellationToken cancellationToken, TestingType? testingTypeOverride = null)
    {
        var testingType = testingTypeOverride ?? engagement.TestingType;
        var nameCache = new Dictionary<long, string>();
        async Task<string> NameAsync(long? userId)
        {
            if (userId is null) return "—";
            if (nameCache.TryGetValue(userId.Value, out var cached)) return cached;
            var user = await _userRepository.GetByIdAsync(userId.Value, cancellationToken);
            return nameCache[userId.Value] = user?.FullName is { Length: > 0 } name ? name : user?.Email ?? $"user #{userId}";
        }

        // Version control table: every earlier version of this engagement's report, plus this one.
        var existing = await _reportRepository.GetByEngagementIdAsync(engagement.Id, cancellationToken);
        var versionRows = new List<ReportRenderVersion>();
        foreach (var r in existing.Where(r => r.Id != report.Id && r.Version < report.Version).Append(report).OrderBy(r => r.Version))
        {
            var comment = r.Status switch
            {
                ReportStatus.Approved => "Final report (approved)",
                ReportStatus.Rejected => $"Rejected{(string.IsNullOrWhiteSpace(r.RejectionReason) ? string.Empty : ": " + r.RejectionReason)}",
                _ => "Draft report"
            };
            versionRows.Add(new ReportRenderVersion(versionRows.Count + 1, $"{r.Version}.0", r.GeneratedAt, await NameAsync(r.GeneratedByUserId), await NameAsync(r.ApprovedByUserId), comment));
        }

        // Scope table: every asset with an included finding, with the per-report status override.
        var statuses = ParseScopeStatuses(report.ScopeStatusesJson);
        var scopeRows = findings
            .Where(f => f.ClientAsset is not null)
            .Select(f => f.ClientAsset!)
            .DistinctBy(a => a.Id)
            .Select(a => new ReportRenderScopeRow(
                a.Name,
                a.AssetType is AssetType.WebUrl or AssetType.Api ? "Network / Web" : "Network",
                a.Identifier,
                statuses.GetValueOrDefault(a.Id, ReportSectionDefaults.DefaultScopeStatus)))
            .ToList();

        var ordered = findings
            .OrderByDescending(f => (int)(f.SeverityOverride ?? f.Severity))
            .ThenByDescending(f => f.CvssScore ?? 0)
            .ThenBy(f => f.Title)
            .ToList();

        var rendered = new List<ReportRenderFinding>();
        for (var i = 0; i < ordered.Count; i++)
        {
            var f = ordered[i];
            var id = $"VULN-{i + 1:000}";
            var severity = (f.SeverityOverride ?? f.Severity).ToString();
            var tools = ParseJsonArray(f.ToolsUsed);
            var toolsText = tools.Count == 0
                ? "Manual testing"
                : string.Join(", ", tools.Select(t => report.ShowToolNames ? t : t.ToClientLabel()).Distinct());

            rendered.Add(new ReportRenderFinding(
                id,
                id.ToLowerInvariant(),
                f.Title,
                severity,
                f.CvssScore?.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture),
                f.CvssVector,
                f.ClientAsset?.Identifier ?? f.ClientAsset?.Name ?? "—",
                f.Endpoint,
                toolsText,
                ParseJsonArray(f.ReferenceLinks),
                f.Description,
                f.Impact,
                f.Recommendation,
                f.ProofOfConcept,
                f.StepsToReproduce,
                f.AiExplanation,
                f.PatchStatus switch
                {
                    PatchStatus.Patched => "Fixed",
                    PatchStatus.AcceptedRisk => "Accepted Risk",
                    PatchStatus.WontFix => "Won't Fix",
                    _ => "Not Fixed"
                },
                Math.Max(1, f.OccurrenceCount),
                await LoadScreenshotsAsync(f.Id, cancellationToken)));
        }

        var counts = new ReportRenderCounts(
            ordered.Count(f => (f.SeverityOverride ?? f.Severity) == Severity.Critical),
            ordered.Count(f => (f.SeverityOverride ?? f.Severity) == Severity.High),
            ordered.Count(f => (f.SeverityOverride ?? f.Severity) == Severity.Medium),
            ordered.Count(f => (f.SeverityOverride ?? f.Severity) == Severity.Low),
            ordered.Count(f => (f.SeverityOverride ?? f.Severity) == Severity.Informational));

        return new ReportRenderModel(
            testingType.ToString(),
            testingType == TestingType.White,
            testingType == TestingType.Gray,
            testingType == TestingType.Black,
            report.ShowToolNames,
            string.IsNullOrWhiteSpace(template.CompanyDisplayName) ? "Selise Digital" : template.CompanyDisplayName,
            new ReportRenderClient(engagement.ClientOrganization?.Name ?? "Client"),
            new ReportRenderEngagement(engagement.Title, engagement.Scope, engagement.StartDate, engagement.EndDate),
            new ReportRenderReport(report.Version, report.GeneratedAt, report.Status == ReportStatus.PendingApproval,
                report.ExecutiveSummary, report.KeyFindings, report.KeyRecommendations, report.ProjectGoals, report.Limitations),
            versionRows,
            scopeRows,
            counts,
            rendered,
            SeverityChart: string.Empty,
            Custom: ShieldReport.Application.ReportTemplates.ReportTemplateFields.Resolve(
                ShieldReport.Application.ReportTemplates.ReportTemplateFields.Parse(template.CustomFieldsJson),
                ShieldReport.Application.ReportTemplates.ReportTemplateFields.ParseValues(report.CustomFieldValuesJson)));
    }

    // Image attachments marked "include in report", embedded as data URIs (the renderer has no
    // network or file access). Oversized or missing files are skipped rather than failing the report.
    private async Task<IReadOnlyList<ReportRenderScreenshot>> LoadScreenshotsAsync(long vulnerabilityId, CancellationToken cancellationToken)
    {
        var attachments = await _attachmentRepository.GetByVulnerabilityIdAsync(vulnerabilityId, cancellationToken);
        var screenshots = new List<ReportRenderScreenshot>();
        foreach (var attachment in attachments.Where(a => a.IncludeInReport && ImageExtensions.Contains(Path.GetExtension(a.FileName))))
        {
            var stream = _fileStorageService.OpenRead(attachment.FileUrl);
            if (stream is null) continue;

            await using (stream)
            {
                using var buffer = new MemoryStream();
                await stream.CopyToAsync(buffer, cancellationToken);
                if (buffer.Length is 0 or > MaxScreenshotBytes) continue;

                var mime = Path.GetExtension(attachment.FileName).ToLowerInvariant() switch
                {
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".gif" => "image/gif",
                    ".webp" => "image/webp",
                    _ => "image/png"
                };
                screenshots.Add(new ReportRenderScreenshot($"data:{mime};base64,{Convert.ToBase64String(buffer.ToArray())}", attachment.Caption));
            }
        }

        return screenshots;
    }

    private static Dictionary<long, string> ParseScopeStatuses(string json)
    {
        try
        {
            return (JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new())
                .Where(kv => long.TryParse(kv.Key, out _))
                .ToDictionary(kv => long.Parse(kv.Key), kv => kv.Value);
        }
        catch (JsonException)
        {
            return new Dictionary<long, string>();
        }
    }

    private static IReadOnlyList<string> ParseJsonArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json)?.Where(s => !string.IsNullOrWhiteSpace(s)).ToList() ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private async Task<string> SaveFileAsync(long engagementId, string fileName, byte[] content, CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream(content);
        return await _fileStorageService.SaveAsync($"reports/{engagementId}", fileName, stream, cancellationToken);
    }

    private async Task<byte[]> ReadAllBytesAsync(string relativeUrl, CancellationToken cancellationToken)
    {
        var stream = _fileStorageService.OpenRead(relativeUrl)
            ?? throw new AppException("The draft document is missing from storage.", 404);

        await using (stream)
        {
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream, cancellationToken);
            return memoryStream.ToArray();
        }
    }

    private static ReportDto MapToDto(Report report) => new(
        report.Id,
        report.PublicId,
        report.EngagementId,
        report.ReportTemplateId,
        report.Version,
        report.Status.ToString(),
        report.ExecutiveSummary,
        report.KeyFindings,
        report.KeyRecommendations,
        !string.IsNullOrWhiteSpace(report.DraftDocxPath),
        !string.IsNullOrWhiteSpace(report.FinalPdfPath),
        report.GeneratedByUserId,
        report.GeneratedAt,
        report.ApprovedByUserId,
        report.ApprovedAt,
        report.RejectedByUserId,
        report.RejectedAt,
        report.RejectionReason,
        report.ProjectGoals,
        report.Limitations,
        ParseScopeStatuses(report.ScopeStatusesJson),
        report.ShowToolNames,
        report.RenderedHtml is not null,
        report.NarrativeStatus.ToString(),
        report.NarrativeError,
        report.ReportTemplate?.Name,
        report.ReportTemplate is null ? null : ShieldReport.Application.ReportTemplates.ReportTemplateFields.Parse(report.ReportTemplate.CustomFieldsJson),
        ShieldReport.Application.ReportTemplates.ReportTemplateFields.ParseValues(report.CustomFieldValuesJson));
}
