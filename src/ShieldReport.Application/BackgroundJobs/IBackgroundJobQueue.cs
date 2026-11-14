namespace ShieldReport.Application.BackgroundJobs;

// Durable replacement for Hangfire's BackgroundJob.Enqueue. Enqueue only *adds* the job row to the
// current unit of work — it is persisted by the caller's next IUnitOfWork.SaveChangesAsync, in the
// same transaction as the caller's own changes (transactional outbox). Nothing runs until then.
public interface IBackgroundJobQueue
{
    Task EnqueueAsync<TPayload>(string jobType, TPayload payload, int maxAttempts = 3, DateTime? runAfter = null, CancellationToken cancellationToken = default);
}

// One implementation per job type, registered in DI; BackgroundJobWorkerService dispatches by JobType.
// Handlers must be idempotent: a job can run more than once (a retry after an error, or a re-run
// after the worker process died mid-job and its lease expired).
public interface IBackgroundJobHandler
{
    string JobType { get; }

    Task HandleAsync(string payloadJson, CancellationToken cancellationToken);
}
