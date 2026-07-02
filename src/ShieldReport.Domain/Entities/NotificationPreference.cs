namespace ShieldReport.Domain.Entities;

public sealed class NotificationPreference : BaseEntity
{
    public long? UserId { get; private set; }
    public long? ClientOrganizationId { get; private set; }
    public string NotificationType { get; private set; } = string.Empty;
    public bool IsEnabled { get; private set; } = true;

    private NotificationPreference()
    {
    }

    public NotificationPreference(long? userId, long? clientOrganizationId, string notificationType, bool isEnabled = true)
    {
        if (userId is null && clientOrganizationId is null)
        {
            throw new ArgumentException("Either UserId or ClientOrganizationId must be set.");
        }

        UserId = userId;
        ClientOrganizationId = clientOrganizationId;
        NotificationType = !string.IsNullOrWhiteSpace(notificationType)
            ? notificationType
            : throw new ArgumentException("Notification type is required.", nameof(notificationType));
        IsEnabled = isEnabled;
    }

    public void SetEnabled(bool isEnabled)
    {
        IsEnabled = isEnabled;
    }
}
