using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.RegistrationInvites.Dtos;

public sealed record CreateRegistrationInviteRequestDto(
    string Email,
    long RoleId,
    long? ClientOrganizationId,
    InviteLifetime Lifetime);
