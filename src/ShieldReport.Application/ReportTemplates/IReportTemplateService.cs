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
}
