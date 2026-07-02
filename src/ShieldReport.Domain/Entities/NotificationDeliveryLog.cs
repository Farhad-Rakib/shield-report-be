namespace ShieldReport.Domain.Entities;

public sealed class NotificationDeliveryLog : BaseEntity
{
    public long UserId { get; private set; }
    public string NotificationType { get; private set; } = string.Empty;
    public string Channel { get; private set; } = string.Empty;
    public bool Success { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime SentAtUtc { get; private set; }

    private NotificationDeliveryLog()
    {
    }

    public NotificationDeliveryLog(long userId, string notificationType, string channel, bool success, string? errorMessage = null)
    {
        UserId = userId;
        NotificationType = !string.IsNullOrWhiteSpace(notificationType)
            ? notificationType
            : throw new ArgumentException("Notification type is required.", nameof(notificationType));
        Channel = !string.IsNullOrWhiteSpace(channel)
            ? channel
            : throw new ArgumentException("Channel is required.", nameof(channel));
        Success = success;
        ErrorMessage = errorMessage;
        SentAtUtc = DateTime.UtcNow;
    }
}
