namespace ShieldReport.Application.Permissions.Dtos;

public sealed record UpdatePermissionRequestDto(
    string Name,
    string Description);