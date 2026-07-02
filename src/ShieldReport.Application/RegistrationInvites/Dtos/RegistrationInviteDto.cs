namespace ShieldReport.Application.RegistrationInvites.Dtos;

public sealed record RegistrationInviteDto(
    long Id,
    string Email,
    string RoleName,
    long? ClientOrganizationId,
    string? ClientOrganizationName,
    DateTime ExpiresAtUtc,
    DateTime? ConsumedAtUtc,
    DateTime? RevokedAtUtc,
    string Status,
    string? Token);
