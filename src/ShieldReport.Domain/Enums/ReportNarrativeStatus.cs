namespace ShieldReport.Domain.Enums;

// Where the executive summary / key findings / key recommendations came from. Stored as a byte.
public enum ReportNarrativeStatus
{
    // Written (or edited) by a person, or no LLM provider was active at generation time.
    Manual,
    // A background job is drafting it; the report already renders with placeholder text.
    Drafting,
    Drafted,
    Failed
}
