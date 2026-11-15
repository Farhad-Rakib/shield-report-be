using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShieldReport.Api.Common;
using FluentValidation;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Application.Security;
using ShieldReport.Domain.Enums;
using ShieldReport.Application.Roles.Dtos;
using ShieldReport.Application.Users;
using ShieldReport.Application.Users.Dtos;

namespace ShieldReport.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public sealed class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IValidator<CreateUserRequestDto> _createValidator;

    public UsersController(IUserService userService, ICurrentUserService currentUserService, IValidator<CreateUserRequestDto> createValidator)
    {
        _userService = userService;
        _currentUserService = currentUserService;
        _createValidator = createValidator;
    }

    /// <summary>
    /// Create a staff user (replaces the removed anonymous POST /Auth/register). Client-portal users
    /// are invited instead; only a SuperAdmin can create a SuperAdmin.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Permissions.UsersCreate)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] CreateUserRequestDto request, CancellationToken cancellationToken)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);
        var user = await _userService.CreateAsync(request, User.IsInRole(SystemRoles.SuperAdmin), cancellationToken);
        return Ok(ApiResponse<UserDto>.SuccessResponse(user, "User created successfully"));
    }

    /// <summary>
    /// Activate or deactivate a user. Users are never deleted; a deactivated user can't sign in or
    /// refresh a session (an access token already issued stays valid until it expires).
    /// </summary>
    [HttpPut("{id:long}/status")]
    [Authorize(Policy = Permissions.UsersUpdate)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetStatus(long id, [FromBody] SetUserStatusRequestDto request, CancellationToken cancellationToken)
    {
        var user = await _userService.SetActiveAsync(id, request.IsActive, _currentUserService.UserId, User.IsInRole(SystemRoles.SuperAdmin), cancellationToken);
        return Ok(ApiResponse<UserDto>.SuccessResponse(user, request.IsActive ? "User activated" : "User deactivated"));
    }

    [HttpGet]
    [Authorize(Policy = Permissions.UsersRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<UserDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var users = await _userService.GetAllAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<UserDto>>.SuccessResponse(users, "Users retrieved successfully"));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = Policies.UsersReadOwnOrAny)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
    {
        var user = await _userService.GetByIdAsync(id, cancellationToken);
        if (user is null)
            return NotFound(ApiResponse.FailureResponse("User not found", StatusCodes.Status404NotFound));

        return Ok(ApiResponse<UserDto>.SuccessResponse(user, "User retrieved successfully"));
    }

    /// <summary>
    /// Get current user's profile
    /// </summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiResponse<ProfileDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var profile = await _userService.GetProfileAsync(userId, cancellationToken);
        if (profile is null)
            return NotFound(ApiResponse.FailureResponse("Profile not found", StatusCodes.Status404NotFound));

        return Ok(ApiResponse<ProfileDto>.SuccessResponse(profile, "Profile retrieved successfully"));
    }

    /// <summary>
    /// Update current user's profile
    /// </summary>
    [HttpPut("me")]
    [ProducesResponseType(typeof(ApiResponse<ProfileDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequestDto request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var profile = await _userService.UpdateProfileAsync(userId, request, cancellationToken);
        return Ok(ApiResponse<ProfileDto>.SuccessResponse(profile, "Profile updated successfully"));
    }

    [HttpGet("{id:long}/roles")]
    [Authorize(Policy = Permissions.UserRolesRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<RoleDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserRoles(long id, CancellationToken cancellationToken)
    {
        var roles = await _userService.GetRolesAsync(id, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<RoleDto>>.SuccessResponse(roles, "User roles retrieved successfully"));
    }

    [HttpPut("{id:long}/roles")]
    [Authorize(Policy = Permissions.UserRolesUpdate)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReplaceUserRoles(long id, [FromBody] UpdateUserRolesRequestDto request, CancellationToken cancellationToken)
    {
        var user = await _userService.ReplaceRolesAsync(id, request, cancellationToken);
        return Ok(ApiResponse<UserDto>.SuccessResponse(user, "User roles updated successfully"));
    }

    [HttpPost("{id:long}/roles/{roleId:long}")]
    [Authorize(Policy = Permissions.UserRolesCreate)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddUserRole(long id, long roleId, CancellationToken cancellationToken)
    {
        var user = await _userService.AddRoleAsync(id, roleId, cancellationToken);
        return Ok(ApiResponse<UserDto>.SuccessResponse(user, "User role added successfully"));
    }

    [HttpDelete("{id:long}/roles/{roleId:long}")]
    [Authorize(Policy = Permissions.UserRolesDelete)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RemoveUserRole(long id, long roleId, CancellationToken cancellationToken)
    {
        var user = await _userService.RemoveRoleAsync(id, roleId, cancellationToken);
        return Ok(ApiResponse<UserDto>.SuccessResponse(user, "User role removed successfully"));
    }
 
    private long GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("sub") ?? User.FindFirst("id") ?? User.FindFirst("userId");
        if (userIdClaim == null || !long.TryParse(userIdClaim.Value, out var userId))
            throw new UnauthorizedAccessException("User ID claim not found.");
        return userId;
    }
}
