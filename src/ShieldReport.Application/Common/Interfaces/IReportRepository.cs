using ShieldReport.Domain.Entities;

namespace ShieldReport.Application.Common.Interfaces;

public interface IReportRepository
{
    Task<Report> AddAsync(Report entity, CancellationToken cancellationToken = default);
    Task<Report?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<Report?> GetByIdWithDetailsAsync(long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Report>> GetByEngagementIdAsync(long engagementId, CancellationToken cancellationToken = default);
    Task<int> GetLatestVersionAsync(long engagementId, CancellationToken cancellationToken = default);

    // Client portal: Approved reports of one client organization's engagements, with the engagement.
    Task<IReadOnlyList<Report>> GetApprovedByClientOrganizationAsync(long clientOrganizationId, CancellationToken cancellationToken = default);
    // Untracked read straight from the database.
    Task<(ShieldReport.Domain.Enums.ReportStatus Status, ShieldReport.Domain.Enums.ReportNarrativeStatus NarrativeStatus)?> GetNarrativeStateAsync(long id, CancellationToken cancellationToken = default);
    void Update(Report entity);
    void Delete(Report entity);
}
