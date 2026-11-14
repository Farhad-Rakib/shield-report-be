namespace ShieldReport.Domain.Entities;

public enum ReportDeliveryStatus
{
    Pending,
    Sent,
    PartiallySent,
    Failed
}

// One "send to client" action for an approved report. The email itself goes out from a durable
// background job (ReportEmailJobHandler), which records per-recipient progress so a retry never
// re-sends to someone who already received it.
public sealed class ReportDelivery : BaseEntity
{
    public long ReportId { get; private set; }
    public long SentByUserId { get; private set; }
    public string Recipients { get; private set; } = string.Empty;        // comma-separated
    public string DeliveredTo { get; private set; } = string.Empty;       // comma-separated, grows as emails go out
    public string? Message { get; private set; }
    public bool AttachWord { get; private set; }
    public ReportDeliveryStatus Status { get; private set; } = ReportDeliveryStatus.Pending;
    public string? LastError { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    public Report Report { get; private set; } = null!;

    private ReportDelivery()
    {
    }

    public ReportDelivery(long reportId, long sentByUserId, IReadOnlyCollection<string> recipients, string? message, bool attachWord)
    {
        if (recipients.Count == 0)
        {
            throw new ArgumentException("At least one recipient is required.", nameof(recipients));
        }

        ReportId = reportId;
        SentByUserId = sentByUserId;
        Recipients = string.Join(',', recipients);
        Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim();
        AttachWord = attachWord;
    }

    public IReadOnlyList<string> GetRecipients() => Split(Recipients);

    public IReadOnlyList<string> GetDeliveredTo() => Split(DeliveredTo);

    public void MarkDeliveredTo(string recipient)
    {
        var delivered = GetDeliveredTo().ToList();
        if (!delivered.Contains(recipient, StringComparer.OrdinalIgnoreCase))
        {
            delivered.Add(recipient);
            DeliveredTo = string.Join(',', delivered);
        }

        if (GetRecipients().All(r => delivered.Contains(r, StringComparer.OrdinalIgnoreCase)))
        {
            Status = ReportDeliveryStatus.Sent;
            LastError = null;
            CompletedAt = DateTime.UtcNow;
        }
    }

    public void MarkAttemptFailed(string error)
    {
        LastError = error.Length > 1000 ? error[..1000] : error;
        Status = GetDeliveredTo().Count > 0 ? ReportDeliveryStatus.PartiallySent : ReportDeliveryStatus.Failed;
    }

    private static IReadOnlyList<string> Split(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
