namespace ShieldReport.Application.Common.Interfaces.Services;

// Abstraction over the SignalR NotificationHub's scan group, which lives in the Api layer —
// mirrors IRealtimeNotifier so ShieldReport.Scanning can push live scan output/status without
// referencing Api or SignalR directly.
public interface IScanRealtimeNotifier
{
    Task PushScanOutputAsync(Guid scanPublicId, string line, string stream, CancellationToken cancellationToken = default);

    Task PushScanStatusAsync(Guid scanPublicId, string status, CancellationToken cancellationToken = default);

    // Backend-narrated progress ("Starting Nuclei scan against...", "Nuclei finished — 2
    // findings") — independent of whatever the underlying CLI tool does or doesn't print, so the
    // console always shows real activity even on a run with no raw output at all.
    Task PushScanPhaseAsync(Guid scanPublicId, string message, CancellationToken cancellationToken = default);

    // Emitted as soon as a single streamed output line parses into a finding, ahead of the
    // scan's final completion — lets the console show "what we found" as it happens.
    Task PushScanFindingAsync(Guid scanPublicId, string title, string? severity, string endpoint, CancellationToken cancellationToken = default);
}
