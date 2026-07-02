namespace ShieldReport.Application.Auth.Dtos;

public sealed record LoginResponseDto(string AccessToken, DateTime ExpiresAtUtc);
