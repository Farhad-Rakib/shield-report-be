using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Domain.Entities;

namespace ShieldReport.Persistence.Interceptors;

// Writes an audit_logs row for every create/update/delete of a business entity, from one central
// place so no endpoint can forget. Captured before the save (original values are still known),
// written right after it (generated ids are known by then) in a second, non-audited save.
//
// Recorded: who (ICurrentUserService, null = system/background work), from where (client IP),
// entity type + id, action, and the changed properties as {"Prop": {"old": ..., "new": ...}}.
// Secrets are masked and bulky/noisy columns skipped. Raw SQL statements (queue lease claims,
// heartbeats) bypass the change tracker and are intentionally not audited.
public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    // High-churn or purely technical tables — auditing them adds noise, not accountability.
    private static readonly HashSet<string> IgnoredEntities =
    [
        nameof(AuditLog), nameof(BackgroundJob), nameof(RefreshToken), nameof(Notification),
        nameof(NotificationDeliveryLog), nameof(UserDashboardPreference), nameof(ScanFindingRaw),
        nameof(MfaRecoveryCode), nameof(PasswordResetToken),
    ];

    // Never written to the audit log in any form.
    private static readonly HashSet<string> SkippedProperties =
    [
        nameof(BaseEntity.CreatedAt), nameof(BaseEntity.UpdatedAt), nameof(BaseEntity.CreatedBy), nameof(BaseEntity.UpdatedBy),
        "RawOutput", "LeaseOwner", "LeaseExpiresAt", "PayloadJson", "ConcurrencyStamp", "SecurityStamp",
    ];

    // Recorded as changed, value masked.
    private static readonly string[] SensitiveNameParts = ["Password", "Secret", "Token", "ApiKey", "Hash", "Mfa", "Otp"];

    private const int MaxValueLength = 2000;

    private readonly ICurrentUserService _currentUserService;
    private List<PendingAudit>? _pending;
    private bool _writingAudit;

    public AuditSaveChangesInterceptor(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        WriteAsync(eventData.Context, CancellationToken.None).GetAwaiter().GetResult();
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        await WriteAsync(eventData.Context, cancellationToken);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        _pending = null;
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        _pending = null;
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private void Capture(DbContext? context)
    {
        if (context is null || _writingAudit)
        {
            return;
        }

        context.ChangeTracker.DetectChanges();
        var pending = new List<PendingAudit>();

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)
                || IgnoredEntities.Contains(entry.Metadata.ClrType.Name))
            {
                continue;
            }

            var changes = CollectChanges(entry);
            if (entry.State == EntityState.Modified && changes.Count == 0)
            {
                continue; // Update() on an unchanged entity, or only skipped columns changed
            }

            pending.Add(new PendingAudit(entry, entry.State switch
            {
                EntityState.Added => "Created",
                EntityState.Deleted => "Deleted",
                _ => "Updated"
            }, changes));
        }

        _pending = pending.Count > 0 ? pending : null;
    }

    private static Dictionary<string, object?> CollectChanges(EntityEntry entry)
    {
        var changes = new Dictionary<string, object?>();

        foreach (var property in entry.Properties)
        {
            var name = property.Metadata.Name;
            if (SkippedProperties.Contains(name) || property.Metadata.IsShadowProperty())
            {
                continue;
            }

            var sensitive = SensitiveNameParts.Any(part => name.Contains(part, StringComparison.OrdinalIgnoreCase));

            switch (entry.State)
            {
                case EntityState.Added when property.CurrentValue is not null && !property.Metadata.IsPrimaryKey():
                    changes[name] = new { @new = sensitive ? "***" : Truncate(property.CurrentValue) };
                    break;
                case EntityState.Deleted when property.OriginalValue is not null && !property.Metadata.IsPrimaryKey():
                    changes[name] = new { old = sensitive ? "***" : Truncate(property.OriginalValue) };
                    break;
                case EntityState.Modified when property.IsModified && !Equals(property.OriginalValue, property.CurrentValue):
                    changes[name] = sensitive
                        ? new { old = "***", @new = "***" }
                        : new { old = Truncate(property.OriginalValue), @new = Truncate(property.CurrentValue) };
                    break;
            }
        }

        return changes;
    }

    private static object? Truncate(object? value) =>
        value is string text && text.Length > MaxValueLength ? text[..MaxValueLength] + "…" : value;

    private async Task WriteAsync(DbContext? context, CancellationToken cancellationToken)
    {
        if (context is null || _writingAudit || _pending is null)
        {
            return;
        }

        var pending = _pending;
        _pending = null;

        var userId = _currentUserService.UserId;
        var ipAddress = _currentUserService.IpAddress;
        foreach (var item in pending)
        {
            var entityId = DescribeKey(item.Entry);
            var changesJson = item.Changes.Count > 0 ? JsonSerializer.Serialize(item.Changes) : null;
            context.Set<AuditLog>().Add(new AuditLog(userId, item.Entry.Metadata.ClrType.Name, entityId, item.Action, changesJson, ipAddress));
        }

        // Second save only contains the AuditLog rows; the flag stops it auditing itself. A
        // failure here must not undo or fail the business save that already committed.
        _writingAudit = true;
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            foreach (var entry in context.ChangeTracker.Entries<AuditLog>().Where(e => e.State == EntityState.Added).ToList())
            {
                entry.State = EntityState.Detached;
            }
        }
        finally
        {
            _writingAudit = false;
        }
    }

    // Single key → "42"; composite key (UserRole, RolePermission, EngagementAssignee, ...) →
    // "UserId=1,RoleId=4", so grants and assignments are identifiable in the log.
    private static string DescribeKey(EntityEntry entry)
    {
        var keyProperties = entry.Metadata.FindPrimaryKey()?.Properties;
        if (keyProperties is null || keyProperties.Count == 0)
        {
            return "?";
        }

        var value = keyProperties.Count == 1
            ? entry.Property(keyProperties[0].Name).CurrentValue?.ToString() ?? "?"
            : string.Join(",", keyProperties.Select(p => $"{p.Name}={entry.Property(p.Name).CurrentValue}"));
        return value.Length > 64 ? value[..64] : value;
    }

    private sealed record PendingAudit(EntityEntry Entry, string Action, Dictionary<string, object?> Changes);
}
