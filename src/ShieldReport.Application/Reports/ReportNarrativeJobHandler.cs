using System.Text.Json;
using ShieldReport.Application.BackgroundJobs;

namespace ShieldReport.Application.Reports;

public sealed record ReportNarrativeJobPayload(long ReportId);

// Drafts a report's narrative with the active LLM, then re-renders the report. Idempotent: it only
// acts while the report is still pending approval with NarrativeStatus = Drafting. Failures are
// recorded on the report (NarrativeError) instead of retried; the user can press "Draft with AI".
public sealed class ReportNarrativeJobHandler : IBackgroundJobHandler
{
    public const string Type = "report.draft-narrative";

    private readonly IReportService _reportService;

    public ReportNarrativeJobHandler(IReportService reportService)
    {
        _reportService = reportService;
    }

    public string JobType => Type;

    public async Task HandleAsync(string payloadJson, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<ReportNarrativeJobPayload>(payloadJson)
            ?? throw new InvalidOperationException("Invalid report narrative job payload.");

        await _reportService.CompleteNarrativeDraftAsync(payload.ReportId, cancellationToken);
    }
}
