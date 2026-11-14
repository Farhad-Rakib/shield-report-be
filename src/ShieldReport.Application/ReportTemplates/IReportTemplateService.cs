using ShieldReport.Application.ReportTemplates.Dtos;

namespace ShieldReport.Application.ReportTemplates;

public interface IReportTemplateService
{
    Task<IReadOnlyList<ReportTemplateDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ReportTemplateDto> CreateAsync(CreateReportTemplateRequestDto request, CancellationToken cancellationToken = default);
    Task<ReportTemplateDto> UpdateAsync(long id, UpdateReportTemplateRequestDto request, CancellationToken cancellationToken = default);
    Task<ReportTemplateDto> UploadDocxTemplateAsync(long id, Stream fileContent, string fileName, CancellationToken cancellationToken = default);
    Task<ReportTemplateDto> ClearDocxTemplateAsync(long id, CancellationToken cancellationToken = default);
    Task<ReportTemplateDto> SetDefaultAsync(long id, CancellationToken cancellationToken = default);
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
    Task<ReportTemplateHtmlDto> GetHtmlAsync(long id, CancellationToken cancellationToken = default);
    Task<ReportTemplateHtmlDto> UpdateHtmlAsync(long id, string htmlContent, CancellationToken cancellationToken = default);
    Task<ReportTemplateDto> UpdateFieldsAsync(long id, IReadOnlyList<ReportTemplateField> fields, CancellationToken cancellationToken = default);
    Task<ReportTemplateHtmlDto> UploadHtmlAsync(long id, Stream content, string fileName, CancellationToken cancellationToken = default);
    Task<string> PreviewAsync(long id, string? htmlContent, IReadOnlyList<ReportTemplateField>? fields, CancellationToken cancellationToken = default);
    Task<ReportTemplateHtmlDto> ResetHtmlAsync(long id, CancellationToken cancellationToken = default);
}
