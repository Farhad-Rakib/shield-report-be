using ShieldReport.Domain.Entities;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.Reports.Rendering;

// Everything a report template can use — this IS the documented variable list for custom
// templates (PascalCase, e.g. {{Client.Name}}, {{#each Findings}}{{Title}}{{/each}}).
// Text fields that come from users or the LLM must be rendered with {{{text X}}} (HTML-escaped,
// line breaks → paragraphs, "- " lines → bullet lists).
public sealed record ReportRenderModel(
    string TestingType,             // "White" | "Gray" | "Black"
    bool IsWhite,
    bool IsGray,
    bool IsBlack,
    bool ShowToolNames,
    string CompanyName,
    ReportRenderClient Client,
    ReportRenderEngagement Engagement,
    ReportRenderReport Report,
    IReadOnlyList<ReportRenderVersion> Versions,
    IReadOnlyList<ReportRenderScopeRow> Scope,
    ReportRenderCounts Counts,
    IReadOnlyList<ReportRenderFinding> Findings,
    string SeverityChart,           // PNG data URI (filled in by the renderer)
    IReadOnlyDictionary<string, string>? Custom = null);   // template's custom fields: {{Custom.key}}

public sealed record ReportRenderClient(string Name);

public sealed record ReportRenderEngagement(string Title, string? Scope, DateTime? StartDate, DateTime? EndDate);

public sealed record ReportRenderReport(
    int Version,
    DateTime GeneratedAt,
    bool IsDraft,
    string ExecutiveSummary,
    string KeyFindings,
    string KeyRecommendations,
    string ProjectGoals,
    string Limitations);

public sealed record ReportRenderVersion(int Number, string Version, DateTime Date, string PreparedBy, string ReviewedBy, string Comment);

public sealed record ReportRenderScopeRow(string Application, string AttackVector, string Url, string Status);

public sealed record ReportRenderCounts(int Critical, int High, int Medium, int Low, int Informational)
{
    public int Total => Critical + High + Medium + Low + Informational;
}

public sealed record ReportRenderFinding(
    string Id,                      // VULN-001
    string Anchor,                  // vuln-001 (for the table of contents)
    string Title,
    string Severity,                // Critical | High | Medium | Low | Informational
    string? CvssScore,
    string? CvssVector,
    string AffectedSystem,
    string Endpoint,
    string ToolsUsed,               // real names or client-safe labels, per ShowToolNames
    IReadOnlyList<string> References,
    string? Description,
    string? Impact,
    string? Recommendation,
    string? ProofOfConcept,
    string? StepsToReproduce,
    string? AiExplanation,
    string Status,                  // Fixed | Not Fixed | Accepted Risk | Won't Fix
    int Occurrences,
    IReadOnlyList<ReportRenderScreenshot> Screenshots);

public sealed record ReportRenderScreenshot(string DataUri, string? Caption);

// Renders reports: Handlebars HTML → PDF (headless Chromium + paged.js) and Word (HtmlToOpenXml on
// the template .docx). Implemented in Infrastructure.
public interface IHtmlReportRenderer
{
    // customTemplate null = the built-in template for the model's testing type. Callers normally
    // pass the result of ResolveTemplateHtml (which honours the row's BuiltInTestingType).
    Task<string> RenderHtmlAsync(ReportRenderModel model, string? customTemplate, CancellationToken cancellationToken = default);
    Task<byte[]> RenderPdfAsync(string html, CancellationToken cancellationToken = default);
    Task<byte[]> RenderDocxAsync(string html, CancellationToken cancellationToken = default);

    // Validates a custom template (syntax) by rendering it against sample data; returns the error or null.
    Task<string?> ValidateTemplateAsync(string template, CancellationToken cancellationToken = default);

    // Renders a template (null = built-in for testingType) against sample data of that testing
    // type, with the given custom field values.
    Task<string> RenderSampleAsync(string? template, IReadOnlyDictionary<string, string> custom, TestingType testingType = TestingType.White, CancellationToken cancellationToken = default);

    // The built-in White / Gray / Black Box Handlebars template (converted from the SELISE samples).
    string GetBuiltInTemplate(TestingType testingType);

    // Built-in template of a kind: Engagement → per testing type (as above); FindingsSummary → the
    // SELISE-styled findings summary (testingType ignored).
    string GetBuiltInTemplate(ReportTemplateKind kind, TestingType testingType);

    // ---- Findings summary templates (ReportTemplateKind.FindingsSummary) ----

    // customTemplate null = the built-in findings summary template.
    Task<string> RenderFindingsSummaryHtmlAsync(FindingsSummaryRenderModel model, string? customTemplate, CancellationToken cancellationToken = default);

    // Validates a findings summary template against sample data (with and without findings); returns the error or null.
    Task<string?> ValidateFindingsSummaryTemplateAsync(string template, CancellationToken cancellationToken = default);

    // Renders a findings summary template (null = built-in) against sample data with the given custom field values.
    Task<string> RenderFindingsSummarySampleAsync(string? template, IReadOnlyDictionary<string, string> custom, CancellationToken cancellationToken = default);
}

public static class HtmlReportRendererExtensions
{
    // The HTML a template row renders with: its own HtmlContent, else the built-in template of its
    // kind: FindingsSummary → the findings summary built-in; Engagement → the built-in of its
    // BuiltInTestingType, else (user-created rows) the built-in matching the engagement's type.
    public static string ResolveTemplateHtml(this IHtmlReportRenderer renderer, ReportTemplate template, TestingType engagementTestingType) =>
        string.IsNullOrWhiteSpace(template.HtmlContent)
            ? renderer.GetBuiltInTemplate(template.Kind, template.BuiltInTestingType ?? engagementTestingType)
            : template.HtmlContent;
}
