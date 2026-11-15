
using ShieldReport.Application.Common.Exceptions;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Application.Common.Interfaces.Security;
using ShieldReport.Domain.Enums;
using ShieldReport.Application.Roles.Dtos;
using ShieldReport.Application.Users.Dtos;
using ShieldReport.Domain.Entities;

namespace ShieldReport.Application.Users;

public sealed class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;

    // Client-portal roles need an organization, which only the registration-invite flow sets.
    private static readonly HashSet<string> ClientRoles = [SystemRoles.ClientAdmin, SystemRoles.ClientUser];

    public UserService(IUserRepository userRepository, IRoleRepository roleRepository, IUnitOfWork unitOfWork, IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
    }

    public async Task<UserDto> CreateAsync(CreateUserRequestDto request, bool actingIsSuperAdmin, CancellationToken cancellationToken = default)
    {
        if (await _userRepository.GetByEmailAsync(request.Email, cancellationToken) is not null)
        {
            throw new AppException("A user with this email already exists.", 409);
        }

        var roles = new List<Role>();
        foreach (var roleId in request.RoleIds.Distinct())
        {
            roles.Add(await _roleRepository.GetByIdWithPermissionsAsync(roleId, cancellationToken)
                ?? throw new AppException($"Role {roleId} not found."));
        }

        if (roles.Any(r => ClientRoles.Contains(r.Name)))
        {
            throw new AppException("Client users must be invited (Administration -> Registration Invites) so they are linked to their organization.");
        }

        if (!actingIsSuperAdmin && roles.Any(r => r.Name == SystemRoles.SuperAdmin))
        {
            throw new AppException("Only a SuperAdmin can create another SuperAdmin.", 403);
        }

        var user = new User(request.FullName, request.Email, _passwordHasher.Hash(request.Password));
        user.SetRoles(roles.Select(role => new UserRole { UserId = user.Id, RoleId = role.Id, User = user, Role = role }));
        await _userRepository.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await MapUserAsync(user.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.GetAllAsync(cancellationToken);

        return users
            .Select(user => new UserDto(
                user.Id,
                user.FullName,
                user.Email,
                user.IsActive,
                user.UserRoles.Select(ur => ur.Role.Name).Distinct().ToList()))
            .ToList();
    }

    public async Task<UserDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdWithRolesAsync(id, cancellationToken);

        return user is null
            ? null
            : new UserDto(
                user.Id,
                user.FullName,
                user.Email,
                user.IsActive,
                user.UserRoles.Select(ur => ur.Role.Name).Distinct().ToList());
    }


    public async Task<ProfileDto?> GetProfileAsync(long userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdWithRolesAsync(userId, cancellationToken);
        if (user is null)
            return null;
        return new ProfileDto(
            user.Id,
            user.FullName,
            user.Email,
            user.IsActive,
            user.ProfileImageUrl,
            user.UserRoles.Select(ur => ur.Role.Name).Distinct().ToList()
        );
    }



    public async Task<ProfileDto> UpdateProfileAsync(long userId, UpdateProfileRequestDto request, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdWithRolesAsync(userId, cancellationToken);
        if (user is null)
            throw new InvalidOperationException("User not found.");

        user.UpdateProfile(request.FullName, request.Email, request.ProfileImageUrl);
        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new ProfileDto(
            user.Id,
            user.FullName,
            user.Email,
            user.IsActive,
            user.ProfileImageUrl,
            user.UserRoles.Select(ur => ur.Role.Name).Distinct().ToList()
        );
    }

    public async Task<IReadOnlyList<RoleDto>> GetRolesAsync(long userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdWithRolesAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        return user.UserRoles
            .Select(userRole => new RoleDto(
                userRole.Role.Id,
                userRole.Role.Name,
                userRole.Role.Description,
                userRole.Role.RolePermissions.Select(rolePermission => rolePermission.Permission.Name).Distinct().ToList()))
            .ToList();
    }

    public async Task<UserDto> ReplaceRolesAsync(long userId, UpdateUserRolesRequestDto request, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdWithRolesAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        var roles = await GetRolesByIdsAsync(request.RoleIds, cancellationToken);
        user.SetRoles(roles.Select(role => new UserRole
        {
            UserId = user.Id,
            RoleId = role.Id,
            User = user,
            Role = role
        }));

        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await MapUserAsync(user.Id, cancellationToken);
    }

    public async Task<UserDto> AddRoleAsync(long userId, long roleId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdWithRolesAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        var role = await GetRoleByIdAsync(roleId, cancellationToken);
        if (user.UserRoles.Any(userRole => userRole.RoleId == role.Id))
        {
            return await MapUserAsync(user.Id, cancellationToken);
        }

        var updatedRoles = user.UserRoles.ToList();
        updatedRoles.Add(new UserRole
        {
            UserId = user.Id,
            RoleId = role.Id,
            User = user,
            Role = role
        });

        user.SetRoles(updatedRoles);
        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await MapUserAsync(user.Id, cancellationToken);
    }

    public async Task<UserDto> RemoveRoleAsync(long userId, long roleId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdWithRolesAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        var userRole = user.UserRoles.FirstOrDefault(userRole => userRole.RoleId == roleId)
            ?? throw new InvalidOperationException("User role not found.");

        var updatedRoles = user.UserRoles.Where(userRole => userRole.RoleId != roleId).ToList();
        user.SetRoles(updatedRoles);
        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await MapUserAsync(user.Id, cancellationToken);
    }

    public async Task<UserDto> SetActiveAsync(long userId, bool isActive, long? actingUserId, bool actingIsSuperAdmin, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdWithRolesAsync(userId, cancellationToken)
            ?? throw new AppException("User not found.", 404);

        if (user.IsActive == isActive)
        {
            return await MapUserAsync(user.Id, cancellationToken);
        }

        if (!isActive && actingUserId == user.Id)
        {
            throw new AppException("You can't deactivate your own account.");
        }

        var targetIsSuperAdmin = user.UserRoles.Any(ur => ur.Role.Name == SystemRoles.SuperAdmin);
        if (targetIsSuperAdmin && !actingIsSuperAdmin)
        {
            throw new AppException("Only a SuperAdmin can change a SuperAdmin's status.", 403);
        }

        if (!isActive && targetIsSuperAdmin)
        {
            var activeSuperAdmins = (await _userRepository.GetAllAsync(cancellationToken))
                .Count(u => u.IsActive && u.UserRoles.Any(ur => ur.Role.Name == SystemRoles.SuperAdmin));
            if (activeSuperAdmins <= 1)
            {
                throw new AppException("The last active SuperAdmin can't be deactivated.");
            }
        }

        if (isActive)
        {
            user.Activate();
        }
        else
        {
            user.Disable();
        }

        user.UpdatedAt = DateTime.UtcNow;
        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await MapUserAsync(user.Id, cancellationToken);
    }

    private async Task<Role> GetRoleByIdAsync(long roleId, CancellationToken cancellationToken)
    {
        return await _roleRepository.GetByIdWithPermissionsAsync(roleId, cancellationToken)
            ?? throw new InvalidOperationException("Role not found.");
    }

    private async Task<IReadOnlyList<Role>> GetRolesByIdsAsync(IEnumerable<long> roleIds, CancellationToken cancellationToken)
    {
        var distinctIds = roleIds.Distinct().ToArray();
        var roles = new List<Role>(distinctIds.Length);

        foreach (var roleId in distinctIds)
        {
            roles.Add(await GetRoleByIdAsync(roleId, cancellationToken));
        }

        return roles;
    }

    private async Task<UserDto> MapUserAsync(long userId, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdWithRolesAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        return new UserDto(
            user.Id,
            user.FullName,
            user.Email,
            user.IsActive,
            user.UserRoles.Select(userRole => userRole.Role.Name).Distinct().ToList());
    }
}
