using ShieldReport.Domain.Entities;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.Common.Interfaces;

public interface IScanRepository : IRepository<Scan>
{
    Task<Scan?> GetByPublicIdAsync(Guid publicId, CancellationToken cancellationToken = default);
    Task<Scan?> GetByIdWithDetailsAsync(long id, CancellationToken cancellationToken = default);

    // Drives the 3-concurrent-scans-per-client cap (BUSINESS-FLOW-AutomatedScanning.md / AS-16).
    Task<int> CountActiveByClientAsync(long clientOrganizationId, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Scan> Items, int Total)> GetPagedAsync(
        int page,
        int pageSize,
        long? clientOrganizationId,
        long? clientAssetId,
        ScanStatus? status,
        CancellationToken cancellationToken = default);
}
