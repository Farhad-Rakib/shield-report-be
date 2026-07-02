using ShieldReport.Application.Auth.Dtos;

namespace ShieldReport.Application.Auth;

public interface IMfaService
{
    Task<MfaEnrollResponseDto> EnrollAsync(long userId, CancellationToken cancellationToken = default);
    Task<MfaEnrollConfirmResponseDto> ConfirmEnrollAsync(long userId, MfaEnrollConfirmRequestDto request, CancellationToken cancellationToken = default);
    Task DisableAsync(long userId, MfaDisableRequestDto request, CancellationToken cancellationToken = default);
    Task<AuthTokensDto> VerifyLoginChallengeAsync(MfaVerifyRequestDto request, CancellationToken cancellationToken = default);
}
