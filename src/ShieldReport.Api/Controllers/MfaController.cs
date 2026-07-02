using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShieldReport.Api.Common;
using ShieldReport.Application.Auth;
using ShieldReport.Application.Auth.Dtos;

namespace ShieldReport.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public sealed class MfaController : ControllerBase
{
    private readonly IMfaService _mfaService;
    private readonly IWebHostEnvironment _environment;
    private readonly IValidator<MfaEnrollConfirmRequestDto> _enrollConfirmValidator;
    private readonly IValidator<MfaDisableRequestDto> _disableValidator;
    private readonly IValidator<MfaVerifyRequestDto> _verifyValidator;

    public MfaController(
        IMfaService mfaService,
        IWebHostEnvironment environment,
        IValidator<MfaEnrollConfirmRequestDto> enrollConfirmValidator,
        IValidator<MfaDisableRequestDto> disableValidator,
        IValidator<MfaVerifyRequestDto> verifyValidator)
    {
        _mfaService = mfaService;
        _environment = environment;
        _enrollConfirmValidator = enrollConfirmValidator;
        _disableValidator = disableValidator;
        _verifyValidator = verifyValidator;
    }

    [HttpPost("enroll")]
    [ProducesResponseType(typeof(ApiResponse<MfaEnrollResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Enroll(CancellationToken cancellationToken)
    {
        var response = await _mfaService.EnrollAsync(GetCurrentUserId(), cancellationToken);
        return Ok(ApiResponse<MfaEnrollResponseDto>.SuccessResponse(response, "Scan the QR code with your authenticator app, then confirm with a code.", StatusCodes.Status200OK));
    }

    [HttpPost("enroll/confirm")]
    [ProducesResponseType(typeof(ApiResponse<MfaEnrollConfirmResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ConfirmEnroll([FromBody] MfaEnrollConfirmRequestDto request, CancellationToken cancellationToken)
    {
        await _enrollConfirmValidator.ValidateAndThrowAsync(request, cancellationToken);
        var response = await _mfaService.ConfirmEnrollAsync(GetCurrentUserId(), request, cancellationToken);
        return Ok(ApiResponse<MfaEnrollConfirmResponseDto>.SuccessResponse(response, "MFA enabled. Store these recovery codes securely — they will not be shown again.", StatusCodes.Status200OK));
    }

    [HttpPost("disable")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Disable([FromBody] MfaDisableRequestDto request, CancellationToken cancellationToken)
    {
        await _disableValidator.ValidateAndThrowAsync(request, cancellationToken);
        await _mfaService.DisableAsync(GetCurrentUserId(), request, cancellationToken);
        return Ok(ApiResponse.SuccessResponse("MFA disabled successfully", StatusCodes.Status200OK));
    }

    [AllowAnonymous]
    [HttpPost("verify")]
    [ProducesResponseType(typeof(ApiResponse<AuthTokenResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Verify([FromBody] MfaVerifyRequestDto request, CancellationToken cancellationToken)
    {
        await _verifyValidator.ValidateAndThrowAsync(request, cancellationToken);
        var tokens = await _mfaService.VerifyLoginChallengeAsync(request, cancellationToken);
        RefreshTokenCookie.Set(Response, _environment, tokens.RefreshToken, tokens.RefreshTokenExpiresAtUtc);

        var response = new AuthTokenResponseDto(tokens.AccessToken, tokens.AccessTokenExpiresAtUtc);
        return Ok(ApiResponse<AuthTokenResponseDto>.SuccessResponse(response, "Login successful", StatusCodes.Status200OK));
    }

    private long GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("sub") ?? User.FindFirst("id") ?? User.FindFirst("userId");
        if (userIdClaim == null || !long.TryParse(userIdClaim.Value, out var userId))
            throw new UnauthorizedAccessException("User ID claim not found.");
        return userId;
    }
}
