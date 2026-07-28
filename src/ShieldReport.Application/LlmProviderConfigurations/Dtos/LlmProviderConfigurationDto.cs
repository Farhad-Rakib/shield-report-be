using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.LlmProviderConfigurations.Dtos;

// Read model — ApiKeyMasked is a display-only suffix (e.g. "********ab12"), never the
// decrypted key. The plaintext key only ever travels one-way, in Create/UpdateLlmProviderConfigurationRequestDto.
public sealed record LlmProviderConfigurationDto(
    long Id,
    LlmProvider Provider,
    string ApiKeyMasked,
    string Model,
    string? BaseUrl,
    decimal Temperature,
    int MaxOutputTokens,
    string PromptTemplate,
    bool IsActive
);
