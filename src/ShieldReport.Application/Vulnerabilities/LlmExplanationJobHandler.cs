using System.Text.Json;
using ShieldReport.Application.BackgroundJobs;

namespace ShieldReport.Application.Vulnerabilities;

public sealed record LlmExplanationJobPayload(long VulnerabilityId, string ToolName);

// Generates the AI explanation / suggested severity for one vulnerability. Queued by the scan
// worker together with the scan's findings. IVulnerabilityAiExplanationService already no-ops
// when no LLM provider is active and overwrites its own output, so re-running is safe.
public sealed class LlmExplanationJobHandler : IBackgroundJobHandler
{
    public const string Type = "vulnerability.llm-explanation";

    private readonly IVulnerabilityAiExplanationService _explanationService;

    public LlmExplanationJobHandler(IVulnerabilityAiExplanationService explanationService)
    {
        _explanationService = explanationService;
    }

    public string JobType => Type;

    public async Task HandleAsync(string payloadJson, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<LlmExplanationJobPayload>(payloadJson)
            ?? throw new InvalidOperationException("Invalid LLM explanation job payload.");

        await _explanationService.ExplainAsync(payload.VulnerabilityId, payload.ToolName, cancellationToken);
    }
}
