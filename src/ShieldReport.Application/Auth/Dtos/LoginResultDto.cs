namespace ShieldReport.Application.Auth.Dtos;

public sealed record LoginResultDto(
    bool MfaRequired,
    string? MfaChallengeId,
    AuthTokensDto? Tokens);
