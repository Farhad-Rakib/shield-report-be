using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ShieldReport.Application.Dashboard;
using ShieldReport.Domain.Entities;
using ShieldReport.Persistence.Context;

namespace ShieldReport.Persistence.Repositories;

public sealed class DashboardLayoutRepository : IDashboardLayoutRepository
{
    private readonly ApplicationDbContext _db;

    public DashboardLayoutRepository(ApplicationDbContext db) => _db = db;

    public async Task<DashboardLayoutDto?> GetByUserIdAsync(long userId, CancellationToken ct = default)
    {
        var pref = await _db.UserDashboardPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        if (pref is null) return null;

        var order  = JsonSerializer.Deserialize<string[]>(pref.WidgetOrder)  ?? [];
        var hidden = JsonSerializer.Deserialize<string[]>(pref.HiddenWidgets) ?? [];

        return new DashboardLayoutDto(order, hidden);
    }

    public async Task UpsertAsync(long userId, string widgetOrder, string hiddenWidgets, CancellationToken ct = default)
    {
        var existing = await _db.UserDashboardPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        if (existing is null)
        {
            await _db.UserDashboardPreferences.AddAsync(new UserDashboardPreference
            {
                UserId        = userId,
                WidgetOrder   = widgetOrder,
                HiddenWidgets = hiddenWidgets,
                UpdatedAt     = DateTime.UtcNow,
            }, ct);
        }
        else
        {
            existing.WidgetOrder   = widgetOrder;
            existing.HiddenWidgets = hiddenWidgets;
            existing.UpdatedAt     = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
    }
}
