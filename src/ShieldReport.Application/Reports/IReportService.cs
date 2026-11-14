using ShieldReport.Application.Reports.Dtos;

namespace ShieldReport.Application.Reports;

public interface IReportService
{
    Task<ReportDto> GenerateAsync(long engagementId, long currentUserId, GenerateReportRequestDto request, CancellationToken cancellationToken = default);
    // Renders what Generate would produce (placeholder narrative), without saving anything.
    Task<string> PreviewNewAsync(long engagementId, long currentUserId, ReportPreviewRequestDto request, CancellationToken cancellationToken = default);
    // Queues (or re-queues) AI drafting of the narrative for a pending report.
    Task<IReadOnlyList<ReportTemplateOptionDto>> GetTemplateOptionsAsync(CancellationToken cancellationToken = default);
    Task<ReportDto> RequestNarrativeDraftAsync(long id, CancellationToken cancellationToken = default);
    // Background job body: drafts, applies (unless a person edited meanwhile) and re-renders.
    Task CompleteNarrativeDraftAsync(long id, CancellationToken cancellationToken = default);
    Task<ReportDto> RegenerateAsync(long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReportDto>> ListAsync(long engagementId, CancellationToken cancellationToken = default);
    Task<ReportDto> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<ReportDto> UpdateNarrativeAsync(long id, UpdateReportNarrativeRequestDto request, CancellationToken cancellationToken = default);
    Task<ReportDto> UpdateSectionsAsync(long id, UpdateReportSectionsRequestDto request, CancellationToken cancellationToken = default);

    // The exact HTML the report's PDF/Word were produced from (null for legacy .docx reports).
    Task<string?> GetPreviewHtmlAsync(long id, CancellationToken cancellationToken = default);
    Task<ReportDto> ApproveAsync(long id, long currentUserId, CancellationToken cancellationToken = default);
    Task<ReportDto> RejectAsync(long id, long currentUserId, string reason, CancellationToken cancellationToken = default);
    Task<ReportDto> ConvertToPdfAsync(long id, CancellationToken cancellationToken = default);
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
    Task<(Stream Content, string FileName)> GetDraftFileAsync(long id, CancellationToken cancellationToken = default);
    Task<(Stream Content, string FileName)> GetPdfFileAsync(long id, CancellationToken cancellationToken = default);
}
