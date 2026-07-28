using ShieldReport.Application.Vulnerabilities;

namespace ShieldReport.Scanning.Workers;

// Minimal Hangfire job, same rationale as ScanDispatchJob — Hangfire gives persistence/retry on
// "did this fire" for what's otherwise a fire-and-forget call; IVulnerabilityAiExplanationService
// itself already no-ops cleanly when no LLM provider is active.
public sealed class LlmExplanationJob
{
    private readonly IVulnerabilityAiExplanationService _explanationService;

    public LlmExplanationJob(IVulnerabilityAiExplanationService explanationService)
    {
        _explanationService = explanationService;
    }

    public async Task RunAsync(long vulnerabilityId, string toolName, CancellationToken cancellationToken)
    {
        await _explanationService.ExplainAsync(vulnerabilityId, toolName, cancellationToken);
    }
}
