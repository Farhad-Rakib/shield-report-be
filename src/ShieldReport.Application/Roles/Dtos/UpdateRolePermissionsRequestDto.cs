namespace ShieldReport.Application.Roles.Dtos;

public sealed record UpdateRolePermissionsRequestDto(
    IReadOnlyList<long> PermissionIds);