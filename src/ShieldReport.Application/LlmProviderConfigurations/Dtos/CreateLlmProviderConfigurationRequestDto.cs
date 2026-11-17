using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.LlmProviderConfigurations.Dtos;

public sealed record CreateLlmProviderConfigurationRequestDto(
    string? Name,
    LlmProvider Provider,
    string ApiKey,
    string Model,
    string? BaseUrl,
    decimal Temperature,
    int MaxOutputTokens,
    string? PromptTemplate,
    string? Effort = null
);
