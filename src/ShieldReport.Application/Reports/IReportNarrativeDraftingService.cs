using ShieldReport.Application.Common.Interfaces.Services;
using ShieldReport.Domain.Entities;

namespace ShieldReport.Application.Reports;

public interface IReportNarrativeDraftingService
{
    // Never throws — falls back to placeholder text if no LLM provider is active or the call
    // fails, same "never break the caller's workflow" contract as VulnerabilityAiExplanationService.
    Task<ReportNarrative> DraftAsync(IReadOnlyList<Vulnerability> includedFindings, CancellationToken cancellationToken = default);
}
