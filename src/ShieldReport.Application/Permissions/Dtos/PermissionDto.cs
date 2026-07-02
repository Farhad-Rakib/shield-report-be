namespace ShieldReport.Application.Permissions.Dtos;

public sealed record PermissionDto(
    long Id,
    string Name,
    string Description);