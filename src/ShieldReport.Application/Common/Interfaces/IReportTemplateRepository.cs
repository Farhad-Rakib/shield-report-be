using ShieldReport.Domain.Entities;

namespace ShieldReport.Application.Common.Interfaces;

public interface IReportTemplateRepository
{
    Task<ReportTemplate> AddAsync(ReportTemplate entity, CancellationToken cancellationToken = default);
    Task<ReportTemplate?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<ReportTemplate?> GetDefaultAsync(CancellationToken cancellationToken = default);
    // Tracked (unlike GetDefaultAsync) — used when the current default needs to be mutated
    // (unmarked) as part of switching which template is the default.
    Task<ReportTemplate?> GetDefaultTrackedAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReportTemplate>> GetAllAsync(CancellationToken cancellationToken = default);
    void Update(ReportTemplate entity);
    void Delete(ReportTemplate entity);
}
