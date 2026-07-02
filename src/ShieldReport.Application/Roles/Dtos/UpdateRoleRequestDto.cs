namespace ShieldReport.Application.Roles.Dtos;

public sealed record UpdateRoleRequestDto(
    string Name,
    string Description);