using ShieldReport.Domain.Entities;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.Common.Interfaces;

public interface IScanRepository : IRepository<Scan>
{
    Task<Scan?> GetByPublicIdAsync(Guid publicId, CancellationToken cancellationToken = default);
    Task<Scan?> GetByIdWithDetailsAsync(long id, CancellationToken cancellationToken = default);

    // Drives the 3-concurrent-scans-per-client cap (BUSINESS-FLOW-AutomatedScanning.md / AS-16).
    Task<int> CountActiveByClientAsync(long clientOrganizationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Scan>> GetByStatusesAsync(IReadOnlyCollection<ScanStatus> statuses, CancellationToken cancellationToken = default);

    // ---- Database-backed scan queue (see ScanWorkerBackgroundService) ----

    // Atomically leases the next scan this worker should handle for `tool` and returns its id, or
    // null when there is nothing to do. Eligible: a Queued scan with no Queued/Running chain
    // predecessor, or a Running scan whose lease expired (its previous owner died, so the new
    // owner re-attaches to the still-running container). UPDLOCK/READPAST make concurrent claims
    // skip each other's rows instead of double-claiming or blocking.
    Task<long?> TryClaimNextAsync(ScanTool tool, string leaseOwner, TimeSpan leaseDuration, CancellationToken cancellationToken = default);

    // Extends the lease if `leaseOwner` still holds it; false means ownership was lost.
    Task<bool> RenewLeaseAsync(long scanId, string leaseOwner, TimeSpan leaseDuration, CancellationToken cancellationToken = default);

    // Expires the lease immediately (used when detaching on shutdown) so the next process
    // re-attaches without waiting for it to time out.
    Task ReleaseLeaseAsync(long scanId, string leaseOwner, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Scan> Items, int Total)> GetPagedAsync(
        int page,
        int pageSize,
        long? clientOrganizationId,
        long? clientAssetId,
        long? engagementId,
        ScanStatus? status,
        CancellationToken cancellationToken = default);
}
