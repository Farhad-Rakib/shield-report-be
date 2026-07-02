using ShieldReport.Domain.Entities;

namespace ShieldReport.Application.Common.Interfaces.Services;

public sealed record ScanRunResult(bool Success, string RawOutput, string? ErrorMessage);

// Shells out to `docker run` for the scan's tool image. Implemented in Infrastructure since it's
// pure Process orchestration — no ASP.NET Core dependency. The caller supplies onOutputLine so
// this stays decoupled from however the consumer chooses to stream it (SignalR, logging, etc).
public interface IScanRunner
{
    Task<ScanRunResult> RunAsync(Scan scan, ClientAsset asset, Func<string, Task> onOutputLine, CancellationToken cancellationToken = default);
}
