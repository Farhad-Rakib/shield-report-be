using Microsoft.EntityFrameworkCore;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Domain.Entities;
using ShieldReport.Persistence.Context;

namespace ShieldReport.Persistence.Repositories;

public sealed class AuditLogRepository : BaseRepository<AuditLog>, IAuditLogRepository
{
    public AuditLogRepository(ApplicationDbContext dbContext)
        : base(dbContext)
    {
    }

    public async Task<(IReadOnlyList<AuditLogRow> Items, int Total)> SearchAsync(AuditLogQuery query, CancellationToken cancellationToken = default)
    {
        var logs = DbSet.AsNoTracking().AsQueryable();

        if (query.UserId.HasValue) logs = logs.Where(l => l.UserId == query.UserId);
        if (!string.IsNullOrWhiteSpace(query.EntityName)) logs = logs.Where(l => l.EntityName == query.EntityName);
        if (!string.IsNullOrWhiteSpace(query.EntityId)) logs = logs.Where(l => l.EntityId == query.EntityId);
        if (!string.IsNullOrWhiteSpace(query.Action)) logs = logs.Where(l => l.Action == query.Action);
        if (query.From.HasValue) logs = logs.Where(l => l.CreatedAt >= query.From);
        if (query.To.HasValue) logs = logs.Where(l => l.CreatedAt <= query.To);

        var rows =
            from log in logs
            join user in DbContext.Users.AsNoTracking() on log.UserId equals (long?)user.Id into users
            from user in users.DefaultIfEmpty()
            select new { log, Email = user != null ? user.Email : null };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            rows = rows.Where(r => r.log.EntityName.Contains(term) || r.log.Action.Contains(term) || r.log.EntityId == term
                || (r.Email != null && r.Email.Contains(term)) || (r.log.IpAddress != null && r.log.IpAddress.Contains(term)));
        }

        var total = await rows.CountAsync(cancellationToken);
        var page = await rows
            .OrderByDescending(r => r.log.CreatedAt).ThenByDescending(r => r.log.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(r => new AuditLogRow(r.log.Id, r.log.CreatedAt, r.log.UserId, r.Email, r.log.EntityName, r.log.EntityId, r.log.Action, r.log.Changes, r.log.IpAddress))
            .ToListAsync(cancellationToken);

        return (page, total);
    }

    public async Task<IReadOnlyList<string>> GetDistinctEntityNamesAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet.AsNoTracking().Select(l => l.EntityName).Distinct().OrderBy(n => n).ToListAsync(cancellationToken);
    }

    public async Task<int> PurgeOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken = default)
    {
        return await DbSet.Where(l => l.CreatedAt < cutoff).ExecuteDeleteAsync(cancellationToken);
    }
}
