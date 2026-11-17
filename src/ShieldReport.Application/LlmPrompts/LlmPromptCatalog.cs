using ShieldReport.Domain.Entities;

namespace ShieldReport.Application.LlmPrompts;

public sealed record LlmPromptPlaceholder(string Name, string Description, string SampleValue);

public sealed record LlmPromptDefinition(
    string Key,
    string Name,
    string Description,
    string DefaultTemplate,
    IReadOnlyList<LlmPromptPlaceholder> Placeholders,
    // Placeholders a template must keep, or the feature can't work.
    IReadOnlyList<string> RequiredPlaceholders,
    // JSON fields the caller parses out of the reply (the prompt must keep asking for them).
    IReadOnlyList<string> ExpectedJsonFields);

// The prompts the app sends to the LLM. Each can be overridden in the Prompt Library (LlmPrompt).
public static class LlmPromptCatalog
{
    public const string VulnerabilityExplanation = "vulnerability-explanation";
    public const string ReportNarrative = "report-narrative";

    public static readonly LlmPromptDefinition Explanation = new(
        VulnerabilityExplanation,
        "Vulnerability explanation",
        "Runs for every new scan finding: plain-language explanation, suggested severity and remediation. A person approves before anything is applied.",
        LlmProviderConfiguration.DefaultPromptTemplate,
        [
            new("{ToolName}", "Client-safe label of the tool that found it", "Vulnerability Scanning"),
            new("{FindingTitle}", "Finding title", "Missing Content-Security-Policy header"),
            new("{Endpoint}", "Affected URL or host:port", "https://app.example.com/login"),
            new("{CvssScore}", "CVSS score, or \"unknown\"", "5.3"),
            new("{RawFinding}", "Raw tool output for the finding (JSON or text)", "{\"template-id\":\"http-missing-security-headers\",\"matcher-name\":\"content-security-policy\"}"),
        ],
        ["{FindingTitle}", "{RawFinding}"],
        ["explanation", "suggestedSeverity", "recommendation", "confidence"]);

    public static readonly LlmPromptDefinition Narrative = new(
        ReportNarrative,
        "Report narrative",
        "Drafts the Executive Summary, Key Findings and Key Recommendations of a report from its included findings. Runs in the background after Generate or Draft with AI.",
        """
        You are a security analyst assistant drafting the narrative sections of a penetration
        test report. You are given a list of findings (title, severity, and explanation) that
        will appear in the report's detailed findings section.

        Findings:
        {FindingsList}

        Using ONLY the data above, respond with a single JSON object (no markdown, no prose
        outside the JSON) matching exactly this shape:
        {"executiveSummary": "<2-4 sentence summary of the overall security posture and engagement objective>", "keyFindings": "<3-6 points on the recurring themes across findings, one per line, each line starting with \"- \">", "keyRecommendations": "<3-6 points on how to remediate the recurring themes, one per line, each line starting with \"- \">"}

        If the findings list is empty or insufficient to draft meaningful narrative, use
        "Insufficient data to generate this section." for each field rather than speculating.
        """,
        [
            new("{FindingsList}", "One line per included finding: \"- Title [Severity]: explanation\"", "- Missing Content-Security-Policy header [Medium]: The site does not restrict script sources.\n- Outdated TLS 1.0 enabled [High]: Legacy protocol allows downgrade attacks."),
            new("{ClientName}", "Client organization name", "Acme Corp"),
            new("{EngagementTitle}", "Engagement title", "Q3 External Pentest"),
            new("{TestingType}", "White, Gray or Black", "Gray"),
        ],
        ["{FindingsList}"],
        ["executiveSummary", "keyFindings", "keyRecommendations"]);

    public static readonly IReadOnlyList<LlmPromptDefinition> All = [Explanation, Narrative];

    public static LlmPromptDefinition? Find(string key) => All.FirstOrDefault(p => p.Key == key);
}
