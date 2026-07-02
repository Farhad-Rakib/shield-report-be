using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShieldReport.Api.Common;
using ShieldReport.Application.Common.Exceptions;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Application.Common.Models;
using ShieldReport.Application.RegistrationInvites;
using ShieldReport.Application.RegistrationInvites.Dtos;
using ShieldReport.Application.Security;

namespace ShieldReport.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/registration-invites")]
[Authorize]
public sealed class RegistrationInvitesController : ControllerBase
{
    private readonly IRegistrationInviteService _registrationInviteService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IValidator<CreateRegistrationInviteRequestDto> _createValidator;
    private readonly IValidator<CompleteRegistrationRequestDto> _completeValidator;

    public RegistrationInvitesController(
        IRegistrationInviteService registrationInviteService,
        ICurrentUserService currentUserService,
        IValidator<CreateRegistrationInviteRequestDto> createValidator,
        IValidator<CompleteRegistrationRequestDto> completeValidator)
    {
        _registrationInviteService = registrationInviteService;
        _currentUserService = currentUserService;
        _createValidator = createValidator;
        _completeValidator = completeValidator;
    }

    [HttpPost]
    [Authorize(Policy = Permissions.RegistrationInvitesCreate)]
    [ProducesResponseType(typeof(ApiResponse<RegistrationInviteDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateRegistrationInviteRequestDto request, CancellationToken cancellationToken)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);
        var createdByUserId = _currentUserService.UserId ?? throw new AppException("User context is missing.", 401);

        var invite = await _registrationInviteService.CreateInviteAsync(request, createdByUserId, cancellationToken);
        return CreatedAtAction(nameof(Create), new { id = invite.Id }, ApiResponse<RegistrationInviteDto>.SuccessResponse(invite, "Invite created successfully", StatusCodes.Status201Created));
    }

    [HttpGet]
    [Authorize(Policy = Permissions.RegistrationInvitesRead)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<RegistrationInviteDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] PagedRequest request, CancellationToken cancellationToken)
    {
        var invites = await _registrationInviteService.ListInvitesAsync(request, cancellationToken);
        return Ok(ApiResponse<PagedResult<RegistrationInviteDto>>.SuccessResponse(invites, "Invites retrieved successfully"));
    }

    [HttpPost("{id:long}/revoke")]
    [Authorize(Policy = Permissions.RegistrationInvitesRevoke)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Revoke(long id, CancellationToken cancellationToken)
    {
        var revokedByUserId = _currentUserService.UserId ?? throw new AppException("User context is missing.", 401);
        await _registrationInviteService.RevokeInviteAsync(id, revokedByUserId, cancellationToken);
        return Ok(ApiResponse.SuccessResponse("Invite revoked successfully"));
    }

    [AllowAnonymous]
    [HttpGet("by-token/{token}")]
    [ProducesResponseType(typeof(ApiResponse<RegistrationInviteValidationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ValidateByToken(string token, CancellationToken cancellationToken)
    {
        var validation = await _registrationInviteService.ValidateTokenAsync(token, cancellationToken);
        return Ok(ApiResponse<RegistrationInviteValidationDto>.SuccessResponse(validation, "Invite is valid"));
    }

    [AllowAnonymous]
    [HttpPost("by-token/{token}/complete")]
    [ProducesResponseType(typeof(ApiResponse<dynamic>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CompleteByToken(string token, [FromBody] CompleteRegistrationByTokenRequestDto request, CancellationToken cancellationToken)
    {
        var completeRequest = new CompleteRegistrationRequestDto(token, request.FullName, request.Password);
        await _completeValidator.ValidateAndThrowAsync(completeRequest, cancellationToken);

        var result = await _registrationInviteService.CompleteRegistrationAsync(completeRequest, cancellationToken);
        return CreatedAtAction(nameof(CompleteByToken), new { token }, ApiResponse<dynamic>.SuccessResponse(result, "Registration completed successfully", StatusCodes.Status201Created));
    }
}
