using ShieldReport.Application.Common.Interfaces.Services;
using ShieldReport.Domain.Entities;

namespace ShieldReport.Application.Reports;

public interface IReportNarrativeDraftingService
{
    // Text shown in the three narrative sections until a person or the LLM fills them.
    string PlaceholderText { get; }

    // True when an LLM provider is active, i.e. drafting is worth queueing.
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    // Calls the active LLM provider. Returns null when none is active; throws when the call or the
    // response parsing fails (the background job records the error on the report).
    Task<ReportNarrative?> DraftAsync(IReadOnlyList<Vulnerability> includedFindings, Engagement? engagement = null, CancellationToken cancellationToken = default);
}
