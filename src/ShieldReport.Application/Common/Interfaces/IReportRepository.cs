using ShieldReport.Domain.Entities;

namespace ShieldReport.Application.Common.Interfaces;

public interface IReportRepository
{
    Task<Report> AddAsync(Report entity, CancellationToken cancellationToken = default);
    Task<Report?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<Report?> GetByIdWithDetailsAsync(long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Report>> GetByEngagementIdAsync(long engagementId, CancellationToken cancellationToken = default);
    Task<int> GetLatestVersionAsync(long engagementId, CancellationToken cancellationToken = default);
    void Update(Report entity);
}
