using ShieldReport.Application.Roles.Dtos;
using ShieldReport.Application.Users.Dtos;

namespace ShieldReport.Application.Users;

public interface IUserService
{
    Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<UserDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    // Staff user created by an admin. actingIsSuperAdmin = the caller (only SuperAdmin may grant SuperAdmin).
    Task<UserDto> CreateAsync(CreateUserRequestDto request, bool actingIsSuperAdmin, CancellationToken cancellationToken = default);
    Task<ProfileDto?> GetProfileAsync(long userId, CancellationToken cancellationToken = default);
    Task<ProfileDto> UpdateProfileAsync(long userId, UpdateProfileRequestDto request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RoleDto>> GetRolesAsync(long userId, CancellationToken cancellationToken = default);
    Task<UserDto> ReplaceRolesAsync(long userId, UpdateUserRolesRequestDto request, CancellationToken cancellationToken = default);
    Task<UserDto> AddRoleAsync(long userId, long roleId, CancellationToken cancellationToken = default);
    Task<UserDto> RemoveRoleAsync(long userId, long roleId, CancellationToken cancellationToken = default);
    // Activate or deactivate (there is no user delete). actingUserId/actingIsSuperAdmin = the caller.
    Task<UserDto> SetActiveAsync(long userId, bool isActive, long? actingUserId, bool actingIsSuperAdmin, CancellationToken cancellationToken = default);
}
