using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShieldReport.Api.Common;
using ShieldReport.Application.Common.Exceptions;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Application.EmailSettings;
using ShieldReport.Application.Security;

namespace ShieldReport.Api.Controllers;

// Outgoing mail configuration (Administration -> Email Configuration). Used by every email the
// app sends: invites, password resets, report delivery.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/email-settings")]
[Authorize(Policy = Permissions.EmailSettingsManage)]
public sealed class EmailSettingsController : ControllerBase
{
    private readonly IEmailSettingsService _service;
    private readonly ICurrentUserService _currentUserService;
    private readonly IValidator<UpdateEmailSettingsRequestDto> _updateValidator;
    private readonly IValidator<SendTestEmailRequestDto> _testValidator;

    public EmailSettingsController(IEmailSettingsService service, ICurrentUserService currentUserService, IValidator<UpdateEmailSettingsRequestDto> updateValidator, IValidator<SendTestEmailRequestDto> testValidator)
    {
        _service = service;
        _currentUserService = currentUserService;
        _updateValidator = updateValidator;
        _testValidator = testValidator;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<EmailSettingsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var settings = await _service.GetAsync(cancellationToken);
        return Ok(ApiResponse<EmailSettingsDto>.SuccessResponse(settings, "Email settings retrieved successfully"));
    }

    [HttpPut]
    [ProducesResponseType(typeof(ApiResponse<EmailSettingsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update([FromBody] UpdateEmailSettingsRequestDto request, CancellationToken cancellationToken)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);
        var userId = _currentUserService.UserId ?? throw new AppException("User context is missing.", 401);
        var settings = await _service.UpdateAsync(request, userId, cancellationToken);
        return Ok(ApiResponse<EmailSettingsDto>.SuccessResponse(settings, "Email settings saved"));
    }

    [HttpPost("test")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SendTest([FromBody] SendTestEmailRequestDto request, CancellationToken cancellationToken)
    {
        await _testValidator.ValidateAndThrowAsync(request, cancellationToken);
        await _service.SendTestAsync(request.To, cancellationToken);
        return Ok(ApiResponse.SuccessResponse($"Test email sent to {request.To}"));
    }
}
