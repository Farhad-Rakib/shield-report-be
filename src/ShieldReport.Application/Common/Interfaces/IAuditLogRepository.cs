using ShieldReport.Domain.Entities;

namespace ShieldReport.Application.Common.Interfaces;

public sealed record AuditLogQuery(
    int Page,
    int PageSize,
    long? UserId,
    string? EntityName,
    string? EntityId,
    string? Action,
    string? Search,
    DateTime? From,
    DateTime? To);

public sealed record AuditLogRow(
    long Id,
    DateTime CreatedAt,
    long? UserId,
    string? UserEmail,
    string EntityName,
    string EntityId,
    string Action,
    string? Changes,
    string? IpAddress);

public interface IAuditLogRepository : IRepository<AuditLog>
{
    Task<(IReadOnlyList<AuditLogRow> Items, int Total)> SearchAsync(AuditLogQuery query, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetDistinctEntityNamesAsync(CancellationToken cancellationToken = default);

    Task<int> PurgeOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken = default);
}
