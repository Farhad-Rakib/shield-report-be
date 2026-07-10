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

    public async Task PushScanOutputAsync(Guid scanPublicId, string line, string stream, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.Group(NotificationHub.ScanGroupName(scanPublicId.ToString()))
            .SendAsync("ScanOutput", new { scanPublicId, line, stream }, cancellationToken);
    }

    public async Task PushScanStatusAsync(Guid scanPublicId, string status, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.Group(NotificationHub.ScanGroupName(scanPublicId.ToString()))
            .SendAsync("ScanStatusChanged", new { scanPublicId, status }, cancellationToken);
    }

    public async Task PushScanPhaseAsync(Guid scanPublicId, string message, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.Group(NotificationHub.ScanGroupName(scanPublicId.ToString()))
            .SendAsync("ScanPhase", new { scanPublicId, message }, cancellationToken);
    }

    public async Task PushScanFindingAsync(Guid scanPublicId, string title, string? severity, string endpoint, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.Group(NotificationHub.ScanGroupName(scanPublicId.ToString()))
            .SendAsync("ScanFinding", new { scanPublicId, title, severity, endpoint }, cancellationToken);
    }

    public async Task PushScanProgressAsync(Guid scanPublicId, int percent, long? requests, long? rps, long? matched, long? total, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.Group(NotificationHub.ScanGroupName(scanPublicId.ToString()))
            .SendAsync("ScanProgress", new { scanPublicId, percent, requests, rps, matched, total }, cancellationToken);
    }
}
