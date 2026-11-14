using ShieldReport.Domain.Entities;

namespace ShieldReport.Application.Common.Interfaces;

public interface IBackgroundJobRepository : IRepository<BackgroundJob>
{
    // Atomically leases the next due job (Pending with RunAfter <= now, or Running with an expired
    // lease) for `leaseOwner`, increments its Attempts, and returns its id — or null if none is due.
    Task<long?> TryClaimNextAsync(string leaseOwner, TimeSpan leaseDuration, CancellationToken cancellationToken = default);

    // Deletes Succeeded jobs completed before `completedBefore`. Failed jobs are kept for diagnosis.
    Task<int> PurgeSucceededAsync(DateTime completedBefore, CancellationToken cancellationToken = default);
}
