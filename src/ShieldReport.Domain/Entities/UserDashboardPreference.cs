namespace ShieldReport.Domain.Entities;

// One row per user — stores their dashboard widget order and hidden widget list as JSON arrays.
public sealed class UserDashboardPreference
{
    public long UserId { get; set; }
    public User? User { get; set; }

    // JSON text: ordered array of widget IDs, e.g. ["kpi-total-users","widget-recent-users-table",...]
    public string WidgetOrder { get; set; } = "[]";

    // JSON text: array of widget IDs the user has dismissed, e.g. ["widget-recent-users-table"]
    public string HiddenWidgets { get; set; } = "[]";

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
