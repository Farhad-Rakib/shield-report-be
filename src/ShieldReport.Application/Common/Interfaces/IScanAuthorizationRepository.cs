using ShieldReport.Domain.Entities;

namespace ShieldReport.Application.Common.Interfaces;

public interface IScanAuthorizationRepository : IRepository<ScanAuthorization>
{
    // Full history for an asset, newest first.
    Task<IReadOnlyList<ScanAuthorization>> GetByAssetAsync(long clientAssetId, CancellationToken cancellationToken = default);
}
