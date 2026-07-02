namespace ShieldReport.Application.Roles.Dtos;

public sealed record CreateRoleRequestDto(
    string Name,
    string Description);