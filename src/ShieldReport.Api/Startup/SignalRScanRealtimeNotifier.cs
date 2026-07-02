using Microsoft.AspNetCore.SignalR;
using ShieldReport.Api.Hubs;
using ShieldReport.Application.Common.Interfaces.Services;

namespace ShieldReport.Api.Startup;

public sealed class SignalRScanRealtimeNotifier : IScanRealtimeNotifier
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public SignalRScanRealtimeNotifier(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task PushScanOutputAsync(Guid scanPublicId, string line, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.Group(NotificationHub.ScanGroupName(scanPublicId.ToString()))
            .SendAsync("ScanOutput", new { scanPublicId, line }, cancellationToken);
    }

    public async Task PushScanStatusAsync(Guid scanPublicId, string status, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.Group(NotificationHub.ScanGroupName(scanPublicId.ToString()))
            .SendAsync("ScanStatusChanged", new { scanPublicId, status }, cancellationToken);
    }
}
