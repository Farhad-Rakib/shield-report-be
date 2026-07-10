using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ShieldReport.Api.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    // Called by server to send notification to a user
    public async Task SendNotificationToUser(string userId, object notification)
    {
        await Clients.User(userId).SendAsync("ReceiveNotification", notification);
    }

    // Clients viewing a scan join its group to receive live console output (ScanOutput),
    // backend-narrated progress (ScanPhase), live parsed findings (ScanFinding), and status
    // changes (ScanStatusChanged) — see API-DESIGN-PentestOps.md §6.
    public async Task JoinScanGroup(string scanPublicId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, ScanGroupName(scanPublicId));
    }

    public async Task LeaveScanGroup(string scanPublicId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, ScanGroupName(scanPublicId));
    }

    public static string ScanGroupName(string scanPublicId) => $"scan:{scanPublicId}";
}
