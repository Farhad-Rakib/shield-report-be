using ShieldReport.Domain.Entities;

namespace ShieldReport.Application.Common.Interfaces.Services;

public sealed record ScanRunResult(bool Success, string RawOutput, string? ErrorMessage);

// Shells out to `docker run` for the scan's tool image. Implemented in Infrastructure since it's
// pure Process orchestration — no ASP.NET Core dependency. The caller supplies onOutputLine so
// this stays decoupled from however the consumer chooses to stream it (SignalR, logging, etc).
// onOutputLine's second parameter is the stream the line came from ("stdout" or "stderr") — many
// tools (e.g. nuclei) only print progress/status logging to stderr, so callers that want to show
// "what's happening" rather than just parsed results need to distinguish the two.
public interface IScanRunner
{
    Task<ScanRunResult> RunAsync(Scan scan, ClientAsset asset, Func<string, string, Task> onOutputLine, CancellationToken cancellationToken = default);
}
