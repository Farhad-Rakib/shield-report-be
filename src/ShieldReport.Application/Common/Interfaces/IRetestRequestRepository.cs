using ShieldReport.Domain.Entities;

namespace ShieldReport.Application.Common.Interfaces;

public interface IRetestRequestRepository : IRepository<RetestRequest>
{
    Task<RetestRequest?> GetLatestByVulnerabilityIdAsync(long vulnerabilityId, CancellationToken cancellationToken = default);
}
