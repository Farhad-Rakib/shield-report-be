namespace ShieldReport.Application.Auth.Dtos;

public sealed record MfaEnrollConfirmResponseDto(IReadOnlyList<string> RecoveryCodes);
