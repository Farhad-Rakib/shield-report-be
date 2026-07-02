namespace ShieldReport.Domain.Entities;

public sealed class AuditLog : BaseEntity
{
    public long? UserId { get; private set; }
    public string EntityName { get; private set; } = string.Empty;
    public string EntityId { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty;
    public string? Changes { get; private set; }
    public string? IpAddress { get; private set; }

    private AuditLog()
    {
    }

    public AuditLog(long? userId, string entityName, string entityId, string action, string? changes = null, string? ipAddress = null)
    {
        UserId = userId;
        EntityName = !string.IsNullOrWhiteSpace(entityName)
            ? entityName
            : throw new ArgumentException("Entity name is required.", nameof(entityName));
        EntityId = entityId ?? string.Empty;
        Action = !string.IsNullOrWhiteSpace(action)
            ? action
            : throw new ArgumentException("Action is required.", nameof(action));
        Changes = changes;
        IpAddress = ipAddress;
    }
}
