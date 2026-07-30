using ShieldReport.Application.Reports.Dtos;

namespace ShieldReport.Application.Reports;

public interface IReportService
{
    Task<ReportDto> GenerateAsync(long engagementId, long currentUserId, CancellationToken cancellationToken = default);
    Task<ReportDto> RegenerateAsync(long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReportDto>> ListAsync(long engagementId, CancellationToken cancellationToken = default);
    Task<ReportDto> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<ReportDto> UpdateNarrativeAsync(long id, UpdateReportNarrativeRequestDto request, CancellationToken cancellationToken = default);
    Task<ReportDto> ApproveAsync(long id, long currentUserId, CancellationToken cancellationToken = default);
    Task<ReportDto> RejectAsync(long id, long currentUserId, string reason, CancellationToken cancellationToken = default);
    Task<ReportDto> ConvertToPdfAsync(long id, CancellationToken cancellationToken = default);
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
    Task OverwriteDraftAsync(long id, byte[] docxBytes, CancellationToken cancellationToken = default);
    Task<(Stream Content, string FileName)> GetDraftFileAsync(long id, CancellationToken cancellationToken = default);
    Task<(Stream Content, string FileName)> GetPdfFileAsync(long id, CancellationToken cancellationToken = default);
}
