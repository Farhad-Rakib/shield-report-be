namespace ShieldReport.Application.RegistrationInvites.Dtos;

public sealed record RegistrationInviteValidationDto(
    string Email,
    string RoleName,
    string? ClientOrganizationName);
