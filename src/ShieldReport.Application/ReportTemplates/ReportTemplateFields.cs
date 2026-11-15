using System.Text.Json;
using System.Text.RegularExpressions;
using ShieldReport.Application.Common.Exceptions;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.ReportTemplates;

// One admin-defined dynamic field of a report template, available as {{Custom.<Key>}}.
public sealed record ReportTemplateField(string Key, string Label, string Type, string? DefaultValue, string? Help, bool Required);

public sealed record ReportTemplateVariable(string Expression, string Description, string Group);

public static partial class ReportTemplateFields
{
    public static readonly string[] Types = ["text", "multiline", "date", "number"];
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_]{0,39}$")]
    private static partial Regex KeyRegex();

    public static IReadOnlyList<ReportTemplateField> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<ReportTemplateField>>(json, Json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static string Serialize(IReadOnlyList<ReportTemplateField> fields) => JsonSerializer.Serialize(fields, Json);

    public static IReadOnlyList<ReportTemplateField> Validate(IReadOnlyList<ReportTemplateField>? fields)
    {
        var list = (fields ?? []).Select(f => f with
        {
            Key = (f.Key ?? string.Empty).Trim(),
            Label = (f.Label ?? string.Empty).Trim(),
            Type = string.IsNullOrWhiteSpace(f.Type) ? "text" : f.Type.Trim().ToLowerInvariant(),
            DefaultValue = string.IsNullOrEmpty(f.DefaultValue) ? null : f.DefaultValue,
            Help = string.IsNullOrWhiteSpace(f.Help) ? null : f.Help.Trim(),
        }).ToList();

        var errors = new List<string>();
        if (list.Count > 30) errors.Add("At most 30 fields per template.");
        foreach (var f in list)
        {
            if (!KeyRegex().IsMatch(f.Key)) errors.Add($"Key \"{f.Key}\" must start with a letter and use only letters, digits and _ (40 max).");
            if (f.Label.Length is 0 or > 100) errors.Add($"Field \"{f.Key}\" needs a label (100 characters max).");
            if (!Types.Contains(f.Type)) errors.Add($"Field \"{f.Key}\" type must be text, multiline, date or number.");
            if ((f.DefaultValue?.Length ?? 0) > 10_000) errors.Add($"Field \"{f.Key}\" default value is too long.");
        }

        var duplicates = list.GroupBy(f => f.Key, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicates.Count > 0) errors.Add($"Duplicate key(s): {string.Join(", ", duplicates)}.");
        if (errors.Count > 0) throw new AppException(string.Join(" ", errors), 400);
        return list;
    }

    // Values for rendering: the report's value, else the field default, else empty.
    public static Dictionary<string, string> Resolve(IReadOnlyList<ReportTemplateField> fields, IReadOnlyDictionary<string, string>? values) =>
        fields.ToDictionary(
            f => f.Key,
            f => values is not null && values.TryGetValue(f.Key, out var v) && !string.IsNullOrEmpty(v) ? v : f.DefaultValue ?? string.Empty);

    public static Dictionary<string, string> ParseValues(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    // The documented Handlebars variables (ReportRenderModel) shown in the template editor.
    public static readonly IReadOnlyList<ReportTemplateVariable> Variables =
    [
        new("{{CompanyName}}", "Your company name (template setting)", "General"),
        new("{{Client.Name}}", "Client organization", "General"),
        new("{{TestingType}}", "White, Gray or Black", "General"),
        new("{{#if IsWhite}}…{{/if}}", "Also IsGray, IsBlack", "General"),
        new("{{#if ShowToolNames}}…{{/if}}", "Report option: name the scan tools", "General"),
        new("{{Engagement.Title}}", "Engagement title", "Engagement"),
        new("{{Engagement.Scope}}", "Engagement scope text", "Engagement"),
        new("{{formatDate Engagement.StartDate}}", "Also Engagement.EndDate", "Engagement"),
        new("{{Report.Version}}", "Report version number", "Report"),
        new("{{formatDate Report.GeneratedAt}}", "Generation date", "Report"),
        new("{{#if Report.IsDraft}}…{{/if}}", "True until approved (e.g. DRAFT watermark)", "Report"),
        new("{{{text Report.ExecutiveSummary}}}", "Also KeyFindings, KeyRecommendations, ProjectGoals, Limitations", "Report"),
        new("{{#each Versions}}{{Version}} {{PreparedBy}}{{/each}}", "Version control rows (Number, Version, Date, PreparedBy, ReviewedBy, Comment)", "Report"),
        new("{{#each Scope}}{{Application}} {{Url}} {{Status}}{{/each}}", "Scope rows (Application, AttackVector, Url, Status)", "Scope"),
        new("{{Counts.Critical}}", "Also High, Medium, Low, Informational, Total", "Findings"),
        new("{{severityBreakdown Counts}}", "\"2 critical, 3 high …\" sentence", "Findings"),
        new("<img src=\"{{SeverityChart}}\">", "Severity chart image", "Findings"),
        new("{{#each Findings}}…{{/each}}", "Each finding: Id, Anchor, Title, Severity, CvssScore, CvssVector, AffectedSystem, Endpoint, ToolsUsed, Status, Occurrences", "Findings"),
        new("{{{text Description}}}", "Inside each finding: also Impact, Recommendation, ProofOfConcept, StepsToReproduce, AiExplanation", "Findings"),
        new("{{#each Screenshots}}<img src=\"{{DataUri}}\">{{Caption}}{{/each}}", "Inside each finding", "Findings"),
        new("{{upper Severity}}", "Helpers: upper, formatDate, numberWords, eq (e.g. {{#if (eq Severity \"High\")}})", "Helpers"),
        new("{{asset \"logo.png\"}}", "Built-in images: logo.png, cover.png, vapt-cycle.png, scope-white.png, scope-gray.png, assessment-flow.png, black-flow.png", "Helpers"),
        new("{{Custom.yourKey}}", "Your dynamic fields (defined on this template, filled per report), inline and escaped. Multiline fields: {{{text Custom.yourKey}}} (paragraphs and bullets, use outside <p>)", "Custom fields"),
    ];

    // Variables of a Findings summary template (FindingsSummaryRenderModel).
    public static readonly IReadOnlyList<ReportTemplateVariable> FindingsSummaryVariables =
    [
        new("{{CompanyName}}", "Your company name (template setting)", "General"),
        new("{{formatDate GeneratedAt}}", "Export date", "General"),
        new("{{GeneratedBy}}", "User who exported the report", "General"),
        new("{{FilterDescription}}", "All filters in one line", "Filters"),
        new("{{Filters.Client}}", "Also Filters.Asset, Filters.Period, Filters.Severity, Filters.Status (\"All clients\" … when not filtered)", "Filters"),
        new("{{Summary.ClientCount}}", "Also AssetsTested, TotalVulnerabilities, OpenVulnerabilities, ClosedVulnerabilities", "Summary"),
        new("{{Counts.Critical}}", "Also High, Medium, Low, Informational, Total", "Summary"),
        new("{{severityBreakdown Counts}}", "\"2 critical, 3 high …\" sentence", "Summary"),
        new("<img src=\"{{SeverityChart}}\">", "Severity chart image (empty when there are no findings)", "Summary"),
        new("{{#if Truncated}}…{{/if}}", "True when more than {{VulnerabilityLimit}} rows matched (counts stay complete)", "Summary"),
        new("{{#each Clients}}…{{/each}}", "Each client: Name, Anchor, AssetsTested, OpenVulnerabilities, Counts, Assets", "Clients"),
        new("{{#each Assets}}…{{/each}}", "Inside a client: Name, Anchor, Identifier, AssetType, LastScannedAt, ScanCount, OpenVulnerabilities, Counts, Vulnerabilities", "Clients"),
        new("{{#each Vulnerabilities}}…{{/each}}", "Inside an asset: Id, Title, Severity, CvssScore, Status, Endpoint, Engagement, FirstSeen, ClosedAt", "Clients"),
        new("{{upper Severity}}", "Helpers: upper, formatDate, numberWords, eq (e.g. {{#if (eq Severity \"High\")}})", "Helpers"),
        new("{{asset \"logo.png\"}}", "Built-in images: logo.png, cover.jpg", "Helpers"),
        new("{{Custom.yourKey}}", "Your dynamic fields (defined on this template, filled at export), inline and escaped. Multiline fields: {{{text Custom.yourKey}}}", "Custom fields"),
    ];

    public static IReadOnlyList<ReportTemplateVariable> VariablesFor(ReportTemplateKind kind) =>
        kind == ReportTemplateKind.FindingsSummary ? FindingsSummaryVariables : Variables;
}
