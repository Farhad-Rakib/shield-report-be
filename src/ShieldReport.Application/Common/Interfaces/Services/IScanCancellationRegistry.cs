namespace ShieldReport.Application.Common.Interfaces.Services;

// Bridges the "Cancel scan" API call (Application/Api layer) to whichever in-flight background
// task is actually running that scan's `docker run` process (ShieldReport.Scanning) — without
// this, ScanService.CancelAsync only flips the DB row to Cancelled while the underlying tool
// container keeps running to completion untouched, oblivious to the cancellation.
public interface IScanCancellationRegistry
{
    // Returns a token that's cancelled either when the given scan is cancelled via TryCancel, or
    // when the supplied parent token fires (e.g. app shutdown) — whichever comes first. Caller
    // owns disposal.
    CancellationTokenSource Register(long scanId, CancellationToken linkedTo);

    // Signals cancellation to the scan's in-flight run, if one is currently registered. Returns
    // false if no run is currently tracked for that scan (e.g. it hadn't started yet, or already
    // finished) — the caller should still treat the scan as cancelled either way, since the DB
    // update is authoritative regardless of whether a live run was actually interrupted.
    bool TryCancel(long scanId);

    void Unregister(long scanId);
}
