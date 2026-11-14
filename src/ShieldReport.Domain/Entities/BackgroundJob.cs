using ShieldReport.Domain.Enums;

namespace ShieldReport.Domain.Entities;

// Durable, database-backed background job (replaces Hangfire). Rows are added through
// IBackgroundJobQueue in the same unit of work as the data they relate to, so a job is never
// queued for a change that didn't commit (and vice versa). BackgroundJobWorkerService claims
// Pending rows with a lease, runs the handler registered for Type, and retries with back-off.
public sealed class BackgroundJob : BaseEntity
{
    public string Type { get; private set; } = string.Empty;
    public string PayloadJson { get; private set; } = "{}";
    public BackgroundJobStatus Status { get; private set; } = BackgroundJobStatus.Pending;
    public int Attempts { get; private set; }
    public int MaxAttempts { get; private set; }
    public DateTime RunAfter { get; private set; } = DateTime.UtcNow;
    public string? LeaseOwner { get; private set; }
    public DateTime? LeaseExpiresAt { get; private set; }
    public string? LastError { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    private BackgroundJob()
    {
    }

    public BackgroundJob(string type, string payloadJson, int maxAttempts, DateTime? runAfter = null)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new ArgumentException("Job type is required.", nameof(type));
        }

        Type = type;
        PayloadJson = payloadJson;
        MaxAttempts = Math.Max(1, maxAttempts);
        RunAfter = runAfter ?? DateTime.UtcNow;
    }

    public void MarkSucceeded()
    {
        Status = BackgroundJobStatus.Succeeded;
        CompletedAt = DateTime.UtcNow;
        LastError = null;
        ReleaseLease();
    }

    // Attempts is incremented by the atomic claim, so it already counts the run that just failed.
    public void MarkAttemptFailed(string error)
    {
        LastError = error.Length > 2000 ? error[..2000] : error;
        ReleaseLease();

        if (Attempts >= MaxAttempts)
        {
            Status = BackgroundJobStatus.Failed;
            CompletedAt = DateTime.UtcNow;
            return;
        }

        // Exponential back-off: 30s, 60s, 120s, ... capped at 30 minutes.
        var delaySeconds = Math.Min(30 * Math.Pow(2, Math.Max(0, Attempts - 1)), 1800);
        Status = BackgroundJobStatus.Pending;
        RunAfter = DateTime.UtcNow.AddSeconds(delaySeconds);
    }

    private void ReleaseLease()
    {
        LeaseOwner = null;
        LeaseExpiresAt = null;
    }
}
