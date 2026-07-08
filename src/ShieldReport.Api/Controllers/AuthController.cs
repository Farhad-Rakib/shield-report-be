using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using ShieldReport.Api.Common;
using ShieldReport.Application.Auth;
using ShieldReport.Application.Auth.Dtos;
using ShieldReport.Application.Common.Exceptions;

namespace ShieldReport.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly IValidator<LoginRequestDto> _loginValidator;
    private readonly IValidator<RegisterUserRequestDto> _registerValidator;
    private readonly IValidator<RefreshTokenRequestDto> _refreshTokenValidator;
    private readonly IValidator<RevokeRefreshTokenRequestDto> _revokeRefreshTokenValidator;
    private readonly IValidator<ForgotPasswordRequestDto> _forgotPasswordValidator;
    private readonly IValidator<ResetPasswordRequestDto> _resetPasswordValidator;
    private readonly IValidator<ChangePasswordRequestDto> _changePasswordValidator;

    public AuthController(
        IAuthService authService,
        IWebHostEnvironment environment,
        IConfiguration configuration,
        IValidator<LoginRequestDto> loginValidator,
        IValidator<RegisterUserRequestDto> registerValidator,
        IValidator<RefreshTokenRequestDto> refreshTokenValidator,
        IValidator<RevokeRefreshTokenRequestDto> revokeRefreshTokenValidator,
        IValidator<ForgotPasswordRequestDto> forgotPasswordValidator,
        IValidator<ResetPasswordRequestDto> resetPasswordValidator,
        IValidator<ChangePasswordRequestDto> changePasswordValidator)
    {
        _authService = authService;
        _environment = environment;
        _configuration = configuration;
        _loginValidator = loginValidator;
        _registerValidator = registerValidator;
        _refreshTokenValidator = refreshTokenValidator;
        _revokeRefreshTokenValidator = revokeRefreshTokenValidator;
        _forgotPasswordValidator = forgotPasswordValidator;
        _resetPasswordValidator = resetPasswordValidator;
        _changePasswordValidator = changePasswordValidator;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<AuthTokenResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<MfaChallengeResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request, CancellationToken cancellationToken)
    {
        await _loginValidator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await _authService.LoginAsync(request, cancellationToken);

        if (result.MfaRequired)
        {
            var challenge = new MfaChallengeResponseDto(result.MfaChallengeId!);
            return Ok(ApiResponse<MfaChallengeResponseDto>.SuccessResponse(challenge, "Multi-factor authentication required.", StatusCodes.Status200OK));
        }

        var tokens = result.Tokens!;
        RefreshTokenCookie.Set(Response, _environment, _configuration, tokens.RefreshToken, tokens.RefreshTokenExpiresAtUtc);

        var response = new AuthTokenResponseDto(tokens.AccessToken, tokens.AccessTokenExpiresAtUtc);
        return Ok(ApiResponse<AuthTokenResponseDto>.SuccessResponse(response, "Login successful", StatusCodes.Status200OK));
    }

    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse<dynamic>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Register([FromBody] RegisterUserRequestDto request, CancellationToken cancellationToken)
    {
        await _registerValidator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await _authService.RegisterAsync(request, cancellationToken);

        return CreatedAtAction(nameof(Register), new { id = result.User.Id }, ApiResponse<dynamic>.SuccessResponse(result, "User registered successfully", StatusCodes.Status201Created));
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(ApiResponse<AuthTokenResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[RefreshTokenCookie.CookieName];
        if (string.IsNullOrEmpty(refreshToken))
        {
            throw new AppException("Refresh token is missing.", 401);
        }

        var request = new RefreshTokenRequestDto(refreshToken);
        await _refreshTokenValidator.ValidateAndThrowAsync(request, cancellationToken);
        var tokens = await _authService.RefreshTokenAsync(request, cancellationToken);
        RefreshTokenCookie.Set(Response, _environment, _configuration, tokens.RefreshToken, tokens.RefreshTokenExpiresAtUtc);

        var response = new AuthTokenResponseDto(tokens.AccessToken, tokens.AccessTokenExpiresAtUtc);
        return Ok(ApiResponse<AuthTokenResponseDto>.SuccessResponse(response, "Token refreshed successfully", StatusCodes.Status200OK));
    }

    [AllowAnonymous]
    [HttpPost("revoke-refresh")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RevokeRefresh(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[RefreshTokenCookie.CookieName];
        if (!string.IsNullOrEmpty(refreshToken))
        {
            var request = new RevokeRefreshTokenRequestDto(refreshToken);
            await _revokeRefreshTokenValidator.ValidateAndThrowAsync(request, cancellationToken);
            await _authService.RevokeRefreshTokenAsync(request, cancellationToken);
        }

        RefreshTokenCookie.Delete(Response, _environment, _configuration);
        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    [ProducesResponseType(typeof(ApiResponse<ForgotPasswordResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto request, CancellationToken cancellationToken)
    {
        await _forgotPasswordValidator.ValidateAndThrowAsync(request, cancellationToken);
        var response = await _authService.ForgotPasswordAsync(request, cancellationToken);

        return Ok(ApiResponse<ForgotPasswordResponseDto>.SuccessResponse(response, "Password reset email sent successfully", StatusCodes.Status200OK));
    }

    [AllowAnonymous]
    [HttpPost("reset-password")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto request, CancellationToken cancellationToken)
    {
        await _resetPasswordValidator.ValidateAndThrowAsync(request, cancellationToken);
        await _authService.ResetPasswordAsync(request, cancellationToken);

        return Ok(ApiResponse.SuccessResponse("Password reset successfully", StatusCodes.Status200OK));
    }

    [HttpPost("change-password")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto request, CancellationToken cancellationToken)
    {
        await _changePasswordValidator.ValidateAndThrowAsync(request, cancellationToken);
        await _authService.ChangePasswordAsync(request, cancellationToken);

        return Ok(ApiResponse.SuccessResponse("Password changed successfully", StatusCodes.Status200OK));
    }
}
