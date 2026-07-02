namespace ShieldReport.Application.Auth.Dtos;

public sealed record MfaVerifyRequestDto(string ChallengeId, string Code);
