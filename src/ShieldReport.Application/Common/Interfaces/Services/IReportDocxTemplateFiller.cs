using ShieldReport.Domain.Entities;

namespace ShieldReport.Application.Common.Interfaces.Services;

public sealed record ReportNarrative(string ExecutiveSummary, string KeyFindings, string KeyRecommendations);

// Implemented in Infrastructure via DocumentFormat.OpenXml against the literal White Box Testing
// .docx (embedded resource) — fills only the sections the template's own Word reviewer comments
// mark as placeholders, leaving every other run/style/logo untouched. See
// src/Docs/REQUIREMENTS.md's "White Box Testing Report Content Mapping" for the field-by-field
// anchor mapping this was built from.
public interface IReportDocxTemplateFiller
{
    byte[] Fill(
        Engagement engagement,
        ClientOrganization clientOrganization,
        ReportTemplate template,
        IReadOnlyList<Vulnerability> includedFindings,
        ReportNarrative narrative,
        int version);
}
