namespace ShieldReport.Application.Auth.Dtos;

public sealed record AuthTokenResponseDto(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc);
