using ShieldReport.Domain.Enums;

namespace ShieldReport.Domain.Entities;

public sealed class LlmProviderConfiguration : BaseEntity
{
    // Rendered against a finding via {ToolName}/{FindingTitle}/{Endpoint}/{RawFinding}/{CvssScore}
    // placeholders — kept as one saved/reusable template (per provider row) instead of being
    // rebuilt per call, both to bound input-token cost and to pin down the strict-JSON,
    // don't-speculate contract that keeps the model from hallucinating beyond the supplied data.
    public const string DefaultPromptTemplate =
        """
        You are a security analyst assistant helping triage automated scan findings.

        Tool: {ToolName}
        Finding: {FindingTitle}
        Endpoint: {Endpoint}
        CVSS Score: {CvssScore}
        Raw finding data: {RawFinding}

        Using ONLY the data above, respond with a single JSON object (no markdown, no prose
        outside the JSON) matching exactly this shape:
        {"explanation": "<plain-language explanation of the vulnerability and its risk, 2-4 sentences>", "suggestedSeverity": "<one of Informational, Low, Medium, High, Critical>", "recommendation": "<concrete remediation guidance for the client, 1-3 sentences>", "confidence": <number 0-1>}

        If the provided data is insufficient to explain or score the finding, set "explanation" to
        "Insufficient data to generate an explanation.", "suggestedSeverity" to null, and
        "recommendation" to null rather than guessing.
        """;

    // Admin-chosen label so several configurations of the same provider (e.g. two Gemini models)
    // can coexist and be told apart.
    public string Name { get; private set; } = string.Empty;
    public LlmProvider Provider { get; private set; }
    public string ApiKeyEncrypted { get; private set; } = string.Empty;
    public string Model { get; private set; } = string.Empty;
    public string? BaseUrl { get; private set; }
    public decimal Temperature { get; private set; } = 0.2m;
    public int MaxOutputTokens { get; private set; } = 1024;
    public string PromptTemplate { get; private set; } = DefaultPromptTemplate;
    public bool IsActive { get; private set; }
    public long? UpdatedByUserId { get; private set; }

    private LlmProviderConfiguration()
    {
    }

    public LlmProviderConfiguration(string name, LlmProvider provider, string apiKeyEncrypted, string model, string? baseUrl, decimal temperature, int maxOutputTokens, string promptTemplate)
    {
        Name = name;
        Provider = provider;
        ApiKeyEncrypted = apiKeyEncrypted;
        Model = model;
        BaseUrl = baseUrl;
        Temperature = temperature;
        MaxOutputTokens = maxOutputTokens;
        PromptTemplate = !string.IsNullOrWhiteSpace(promptTemplate) ? promptTemplate : DefaultPromptTemplate;
    }

    public void UpdateDetails(string name, string? apiKeyEncrypted, string model, string? baseUrl, decimal temperature, int maxOutputTokens, string promptTemplate, long? updatedByUserId)
    {
        // A blank incoming key means "leave the existing one unchanged" (the API never returns
        // the decrypted key for a form to round-trip), so only overwrite when one was supplied.
        if (!string.IsNullOrWhiteSpace(apiKeyEncrypted))
        {
            ApiKeyEncrypted = apiKeyEncrypted;
        }

        Name = name;
        Model = model;
        BaseUrl = baseUrl;
        Temperature = temperature;
        MaxOutputTokens = maxOutputTokens;
        PromptTemplate = !string.IsNullOrWhiteSpace(promptTemplate) ? promptTemplate : DefaultPromptTemplate;
        UpdatedByUserId = updatedByUserId;
    }

    public void Activate(long? updatedByUserId)
    {
        IsActive = true;
        UpdatedByUserId = updatedByUserId;
    }

    public void Deactivate(long? updatedByUserId)
    {
        IsActive = false;
        UpdatedByUserId = updatedByUserId;
    }
}
