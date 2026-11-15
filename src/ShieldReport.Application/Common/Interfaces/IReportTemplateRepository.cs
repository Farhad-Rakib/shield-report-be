using ShieldReport.Domain.Entities;

namespace ShieldReport.Application.Common.Interfaces;

public interface IReportTemplateRepository
{
    Task<ReportTemplate> AddAsync(ReportTemplate entity, CancellationToken cancellationToken = default);
    Task<ReportTemplate?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReportTemplate>> GetAllAsync(CancellationToken cancellationToken = default);
    void Update(ReportTemplate entity);
    void Delete(ReportTemplate entity);
}
