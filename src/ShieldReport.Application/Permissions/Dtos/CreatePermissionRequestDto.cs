namespace ShieldReport.Application.Permissions.Dtos;

public sealed record CreatePermissionRequestDto(
    string Name,
    string Description);