using ShieldReport.Application.ScanTools;
using ShieldReport.Domain.Entities;

namespace ShieldReport.Application.Common.Interfaces.Services;

public enum ScanRunOutcome
{
    // Tool exited 0; results are in RawOutput / StructuredOutput.
    Completed,
    // Tool or docker failed; ErrorMessage says why.
    Failed,
    // stopToken fired (user Stop or timeout); the container was stopped and removed.
    Stopped,
    // detachToken fired (shutdown / lost lease); the container is still running and will be
    // re-attached by whichever process claims the scan next.
    Detached,
    // Re-attach found no container: the run is lost (e.g. the host itself rebooted).
    ContainerMissing
}

// StructuredOutput: result data read from a tool's output files (currently reconftw only). When
// set, the worker parses it instead of RawOutput, which for such tools is just progress logging.
public sealed record ScanRunResult(ScanRunOutcome Outcome, string RawOutput, string? ErrorMessage, string? StructuredOutput = null)
{
    public static ScanRunResult Failed(string rawOutput, string errorMessage) => new(ScanRunOutcome.Failed, rawOutput, errorMessage);
}

// Runs a scan's tool in a detached Docker container and follows its output. Implemented in
// ShieldReport.Scanning (DockerScanRunner). The caller supplies onOutputLine so this stays
// decoupled from however the consumer streams it (SignalR, logging, etc); its second parameter is
// the stream the line came from ("stdout" or "stderr") — many tools (e.g. nuclei) only print
// progress/status logging to stderr.
public interface IScanRunner
{
    Task<ScanRunResult> RunAsync(Scan scan, ClientAsset asset, ScanToolRuntimeSettings settings, bool reattach, Func<string, string, Task> onOutputLine, CancellationToken stopToken, CancellationToken detachToken);

    // Removes the finished scan's container and any tool output files. Called by the worker only
    // after the scan's outcome has been saved.
    Task CleanupAsync(Scan scan);
}
