using ShieldReport.Application.Common.Exceptions;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Application.Common.Interfaces.Services;
using ShieldReport.Application.Reports.Dtos;
using ShieldReport.Domain.Entities;

namespace ShieldReport.Application.Reports;

public sealed class ReportService : IReportService
{
    private readonly IReportRepository _reportRepository;
    private readonly IEngagementRepository _engagementRepository;
    private readonly IVulnerabilityRepository _vulnerabilityRepository;
    private readonly IReportTemplateRepository _reportTemplateRepository;
    private readonly IReportNarrativeDraftingService _narrativeDraftingService;
    private readonly IReportDocxTemplateFiller _docxTemplateFiller;
    private readonly IReportPdfConverter _pdfConverter;
    private readonly IFileStorageService _fileStorageService;
    private readonly IUnitOfWork _unitOfWork;

    public ReportService(
        IReportRepository reportRepository,
        IEngagementRepository engagementRepository,
        IVulnerabilityRepository vulnerabilityRepository,
        IReportTemplateRepository reportTemplateRepository,
        IReportNarrativeDraftingService narrativeDraftingService,
        IReportDocxTemplateFiller docxTemplateFiller,
        IReportPdfConverter pdfConverter,
        IFileStorageService fileStorageService,
        IUnitOfWork unitOfWork)
    {
        _reportRepository = reportRepository;
        _engagementRepository = engagementRepository;
        _vulnerabilityRepository = vulnerabilityRepository;
        _reportTemplateRepository = reportTemplateRepository;
        _narrativeDraftingService = narrativeDraftingService;
        _docxTemplateFiller = docxTemplateFiller;
        _pdfConverter = pdfConverter;
        _fileStorageService = fileStorageService;
        _unitOfWork = unitOfWork;
    }

    public async Task<ReportDto> GenerateAsync(long engagementId, long currentUserId, CancellationToken cancellationToken = default)
    {
        var engagement = await _engagementRepository.GetByIdWithDetailsAsync(engagementId, cancellationToken)
            ?? throw new AppException("Engagement not found.", 404);

        var template = await _reportTemplateRepository.GetDefaultAsync(cancellationToken)
            ?? throw new AppException("No report template is configured.", 400);

        var includedFindings = await _vulnerabilityRepository.GetReportIncludedByEngagementIdAsync(engagementId, cancellationToken);
        var narrative = await _narrativeDraftingService.DraftAsync(includedFindings, cancellationToken);
        var nextVersion = await _reportRepository.GetLatestVersionAsync(engagementId, cancellationToken) + 1;

        var report = new Report(engagementId, template.Id, nextVersion, currentUserId, narrative.ExecutiveSummary, narrative.KeyFindings, narrative.KeyRecommendations);

        var docxBytes = _docxTemplateFiller.Fill(engagement, engagement.ClientOrganization, template, includedFindings, narrative, nextVersion);
        var relativePath = await SaveDraftAsync(engagementId, nextVersion, docxBytes, cancellationToken);
        report.MarkDraftGenerated(relativePath);

        await _reportRepository.AddAsync(report, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(report);
    }

    public async Task<ReportDto> RegenerateAsync(long id, CancellationToken cancellationToken = default)
    {
        var report = await _reportRepository.GetByIdWithDetailsAsync(id, cancellationToken)
            ?? throw new AppException("Report not found.", 404);

        var includedFindings = await _vulnerabilityRepository.GetReportIncludedByEngagementIdAsync(report.EngagementId, cancellationToken);
        // Re-fills using the report's current (possibly just pentester-edited) narrative fields —
        // does not re-draft via the LLM again, that only happens once at Generate time.
        var narrative = new ReportNarrative(report.ExecutiveSummary, report.KeyFindings, report.KeyRecommendations);

        var docxBytes = _docxTemplateFiller.Fill(report.Engagement, report.Engagement.ClientOrganization, report.ReportTemplate, includedFindings, narrative, report.Version);
        var relativePath = await SaveDraftAsync(report.EngagementId, report.Version, docxBytes, cancellationToken);
        report.MarkDraftGenerated(relativePath);

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

    public async Task<ReportDto> ConvertToPdfAsync(long id, CancellationToken cancellationToken = default)
    {
        var report = await _reportRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppException("Report not found.", 404);

        if (string.IsNullOrWhiteSpace(report.DraftDocxPath))
        {
            throw new AppException("This report has no draft document to convert yet.", 400);
        }

        var docxBytes = await ReadAllBytesAsync(report.DraftDocxPath, cancellationToken);
        var pdfBytes = await _pdfConverter.ConvertAsync(docxBytes, cancellationToken);

        await using var pdfStream = new MemoryStream(pdfBytes);
        var relativePath = await _fileStorageService.SaveAsync($"reports/{report.EngagementId}", $"report-v{report.Version}.pdf", pdfStream, cancellationToken);
        report.MarkPdfGenerated(relativePath);

        _reportRepository.Update(report);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapToDto(report);
    }

    public async Task OverwriteDraftAsync(long id, byte[] docxBytes, CancellationToken cancellationToken = default)
    {
        var report = await _reportRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppException("Report not found.", 404);

        // Same "new path, clear any stale PDF" semantics as a fresh Generate/Regenerate — an
        // in-browser edit is just another way the draft's content changed.
        var relativePath = await SaveDraftAsync(report.EngagementId, report.Version, docxBytes, cancellationToken);
        report.MarkDraftGenerated(relativePath);

        _reportRepository.Update(report);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<(Stream Content, string FileName)> GetDraftFileAsync(long id, CancellationToken cancellationToken = default)
    {
        var report = await _reportRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppException("Report not found.", 404);

        if (string.IsNullOrWhiteSpace(report.DraftDocxPath))
        {
            throw new AppException("This report has no draft document.", 404);
        }

        var stream = _fileStorageService.OpenRead(report.DraftDocxPath)
            ?? throw new AppException("The draft document is missing from storage.", 404);

        return (stream, $"report-v{report.Version}.docx");
    }

    public async Task<(Stream Content, string FileName)> GetPdfFileAsync(long id, CancellationToken cancellationToken = default)
    {
        var report = await _reportRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppException("Report not found.", 404);

        if (string.IsNullOrWhiteSpace(report.FinalPdfPath))
        {
            throw new AppException("This report has not been converted to PDF yet.", 404);
        }

        var stream = _fileStorageService.OpenRead(report.FinalPdfPath)
            ?? throw new AppException("The PDF file is missing from storage.", 404);

        return (stream, $"report-v{report.Version}.pdf");
    }

    private async Task<string> SaveDraftAsync(long engagementId, int version, byte[] docxBytes, CancellationToken cancellationToken)
    {
        await using var docxStream = new MemoryStream(docxBytes);
        return await _fileStorageService.SaveAsync($"reports/{engagementId}", $"report-v{version}.docx", docxStream, cancellationToken);
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
        report.RejectionReason);
}
