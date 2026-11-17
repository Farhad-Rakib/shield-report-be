namespace ShieldReport.Application.LlmProviderConfigurations.Dtos;

// ApiKey blank/null means "keep the existing encrypted key" — the read DTO never returns a
// decrypted value for this field to round-trip. The provider itself can't change: create a new
// configuration instead.
public sealed record UpdateLlmProviderConfigurationRequestDto(
    string? Name,
    string? ApiKey,
    string Model,
    string? BaseUrl,
    decimal Temperature,
    int MaxOutputTokens,
    string? PromptTemplate,
    string? Effort = null
);
