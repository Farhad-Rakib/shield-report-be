using ShieldReport.Domain.Entities;

namespace ShieldReport.Application.Common.Interfaces;

public interface IReportRepository
{
    Task<Report> AddAsync(Report entity, CancellationToken cancellationToken = default);
    Task<Report?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<Report?> GetByIdWithDetailsAsync(long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Report>> GetByEngagementIdAsync(long engagementId, CancellationToken cancellationToken = default);
    Task<int> GetLatestVersionAsync(long engagementId, CancellationToken cancellationToken = default);
    // Inserts the report with its engagement's next free version and saves (this commits the unit
    // of work's other pending changes too). Allocation is serialized per engagement, so
    // simultaneous generations get consecutive versions instead of colliding.
    Task AddWithNextVersionAsync(Report report, CancellationToken cancellationToken = default);

    // Client portal: Approved reports of one client organization's engagements, with the engagement.
    Task<IReadOnlyList<Report>> GetApprovedByClientOrganizationAsync(long clientOrganizationId, CancellationToken cancellationToken = default);
    // Untracked read straight from the database.
    Task<(ShieldReport.Domain.Enums.ReportStatus Status, ShieldReport.Domain.Enums.ReportNarrativeStatus NarrativeStatus)?> GetNarrativeStateAsync(long id, CancellationToken cancellationToken = default);
    void Update(Report entity);
    void Delete(Report entity);
}
