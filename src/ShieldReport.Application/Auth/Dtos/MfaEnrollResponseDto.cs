namespace ShieldReport.Application.Auth.Dtos;

public sealed record MfaEnrollResponseDto(string SecretKey, string OtpAuthUri);
