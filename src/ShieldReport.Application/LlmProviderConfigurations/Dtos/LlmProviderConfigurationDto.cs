using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.LlmProviderConfigurations.Dtos;

// Read model — ApiKeyMasked is a display-only suffix (e.g. "********ab12"), never the
// decrypted key. The plaintext key only ever travels one-way, in Create/UpdateLlmProviderConfigurationRequestDto.
public sealed record LlmProviderConfigurationDto(
    long Id,
    string Name,
    LlmProvider Provider,
    string ApiKeyMasked,
    string Model,
    string? BaseUrl,
    decimal Temperature,
    int MaxOutputTokens,
    string PromptTemplate,
    bool IsActive
);

// Result of a one-off connection test: never throws for provider errors, so the UI can show them.
public sealed record LlmProviderTestResultDto(bool Success, long LatencyMs, string? Reply, string? Error);
