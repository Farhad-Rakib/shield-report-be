using ShieldReport.Domain.Entities;

namespace ShieldReport.Application.Common.Interfaces;

public interface IEngagementTaskRepository : IRepository<EngagementTask>
{
    Task<IReadOnlyList<EngagementTask>> GetByEngagementIdAsync(long engagementId, CancellationToken cancellationToken = default);

    Task<EngagementTask?> GetByIdWithDetailsAsync(long id, CancellationToken cancellationToken = default);
}
