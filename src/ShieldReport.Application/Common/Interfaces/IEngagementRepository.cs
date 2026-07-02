using ShieldReport.Domain.Entities;

namespace ShieldReport.Application.Common.Interfaces;

public interface IEngagementRepository : IRepository<Engagement>
{
    Task<Engagement?> GetByIdWithDetailsAsync(long id, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Engagement> Items, int Total)> GetPagedAsync(
        int page,
        int pageSize,
        string? search,
        long? clientOrganizationId,
        CancellationToken cancellationToken = default);
}
