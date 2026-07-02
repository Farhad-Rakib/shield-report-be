namespace ShieldReport.Application.Common.Interfaces.Services;

// Abstraction over the SignalR NotificationHub's scan group, which lives in the Api layer —
// mirrors IRealtimeNotifier so ShieldReport.Scanning can push live scan output/status without
// referencing Api or SignalR directly.
public interface IScanRealtimeNotifier
{
    Task PushScanOutputAsync(Guid scanPublicId, string line, CancellationToken cancellationToken = default);

    Task PushScanStatusAsync(Guid scanPublicId, string status, CancellationToken cancellationToken = default);
}
