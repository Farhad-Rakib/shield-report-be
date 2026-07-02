using System.Security.Cryptography;
using System.Text;
using ShieldReport.Application.Auth.Dtos;
using ShieldReport.Application.Common.Exceptions;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Application.Common.Interfaces.Security;
using ShieldReport.Domain.Entities;

namespace ShieldReport.Application.Auth;

public sealed class MfaService : IMfaService
{
    private const string Issuer = "ShieldReport";
    private const int RecoveryCodeCount = 8;

    private static readonly string[] PasswordAndMfaAmr = ["pwd", "otp"];
    private static readonly string[] PasswordAndRecoveryAmr = ["pwd", "recovery"];

    private readonly IUserRepository _userRepository;
    private readonly ITotpService _totpService;
    private readonly IMfaRecoveryCodeRepository _mfaRecoveryCodeRepository;
    private readonly IAppCache _appCache;
    private readonly IAuthService _authService;
    private readonly IUnitOfWork _unitOfWork;

    public MfaService(
        IUserRepository userRepository,
        ITotpService totpService,
        IMfaRecoveryCodeRepository mfaRecoveryCodeRepository,
        IAppCache appCache,
        IAuthService authService,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _totpService = totpService;
        _mfaRecoveryCodeRepository = mfaRecoveryCodeRepository;
        _appCache = appCache;
        _authService = authService;
        _unitOfWork = unitOfWork;
    }

    public async Task<MfaEnrollResponseDto> EnrollAsync(long userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            throw new AppException("User not found or is inactive.", 404);
        }

        if (user.MfaEnabled)
        {
            throw new AppException("MFA is already enabled for this account.", 400);
        }

        var secretKey = _totpService.GenerateSecretKey();
        await _appCache.SetAsync(PendingSecretCacheKey(userId), secretKey, TimeSpan.FromMinutes(10), cancellationToken);

        var otpAuthUri = _totpService.GetOtpAuthUri(secretKey, user.Email, Issuer);
        return new MfaEnrollResponseDto(secretKey, otpAuthUri);
    }

    public async Task<MfaEnrollConfirmResponseDto> ConfirmEnrollAsync(long userId, MfaEnrollConfirmRequestDto request, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            throw new AppException("User not found or is inactive.", 404);
        }

        var pendingSecret = await _appCache.GetAsync<string>(PendingSecretCacheKey(userId), cancellationToken);
        if (string.IsNullOrEmpty(pendingSecret))
        {
            throw new AppException("MFA enrollment session has expired. Please restart enrollment.", 400);
        }

        if (!_totpService.ValidateCode(pendingSecret, request.Code))
        {
            throw new AppException("Invalid verification code.", 400);
        }

        user.EnableMfa(pendingSecret);
        _userRepository.Update(user);

        var existingCodes = await _mfaRecoveryCodeRepository.GetActiveByUserIdAsync(userId, cancellationToken);
        if (existingCodes.Count > 0)
        {
            _mfaRecoveryCodeRepository.RemoveRange(existingCodes);
        }

        var recoveryCodes = GenerateRecoveryCodes();
        var recoveryCodeEntities = recoveryCodes.Select(code => new MfaRecoveryCode(userId, ComputeSha256(code)));
        await _mfaRecoveryCodeRepository.AddRangeAsync(recoveryCodeEntities, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _appCache.RemoveAsync(PendingSecretCacheKey(userId), cancellationToken);

        return new MfaEnrollConfirmResponseDto(recoveryCodes);
    }

    public async Task DisableAsync(long userId, MfaDisableRequestDto request, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            throw new AppException("User not found or is inactive.", 404);
        }

        if (!user.MfaEnabled || string.IsNullOrEmpty(user.MfaSecretKey))
        {
            throw new AppException("MFA is not enabled for this account.", 400);
        }

        var verified = _totpService.ValidateCode(user.MfaSecretKey, request.Code)
            || await TryConsumeRecoveryCodeAsync(userId, request.Code, cancellationToken);

        if (!verified)
        {
            throw new AppException("Invalid verification code.", 400);
        }

        user.DisableMfa();
        _userRepository.Update(user);

        var activeCodes = await _mfaRecoveryCodeRepository.GetActiveByUserIdAsync(userId, cancellationToken);
        if (activeCodes.Count > 0)
        {
            _mfaRecoveryCodeRepository.RemoveRange(activeCodes);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<AuthTokensDto> VerifyLoginChallengeAsync(MfaVerifyRequestDto request, CancellationToken cancellationToken = default)
    {
        var challengeKey = AuthService.MfaChallengeCacheKey(request.ChallengeId);
        var cachedUserId = await _appCache.GetAsync<string>(challengeKey, cancellationToken);
        if (string.IsNullOrEmpty(cachedUserId) || !long.TryParse(cachedUserId, out var userId))
        {
            throw new AppException("MFA challenge has expired or is invalid.", 401);
        }

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive || !user.MfaEnabled || string.IsNullOrEmpty(user.MfaSecretKey))
        {
            throw new AppException("MFA challenge has expired or is invalid.", 401);
        }

        var usedRecoveryCode = false;
        var verified = _totpService.ValidateCode(user.MfaSecretKey, request.Code);
        if (!verified)
        {
            usedRecoveryCode = await TryConsumeRecoveryCodeAsync(user.Id, request.Code, cancellationToken);
            verified = usedRecoveryCode;
        }

        if (!verified)
        {
            throw new AppException("Invalid verification code.", 401);
        }

        await _appCache.RemoveAsync(challengeKey, cancellationToken);

        var amr = usedRecoveryCode ? PasswordAndRecoveryAmr : PasswordAndMfaAmr;
        return await _authService.IssueTokensAsync(user.Id, amr, cancellationToken);
    }

    private async Task<bool> TryConsumeRecoveryCodeAsync(long userId, string code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var codeHash = ComputeSha256(code.Trim());
        var recoveryCode = await _mfaRecoveryCodeRepository.GetByCodeHashAsync(codeHash, cancellationToken);
        if (recoveryCode is null || recoveryCode.UserId != userId || !recoveryCode.IsActive)
        {
            return false;
        }

        recoveryCode.MarkAsUsed();
        _mfaRecoveryCodeRepository.Update(recoveryCode);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static IReadOnlyList<string> GenerateRecoveryCodes()
    {
        var codes = new List<string>(RecoveryCodeCount);
        for (var i = 0; i < RecoveryCodeCount; i++)
        {
            var bytes = RandomNumberGenerator.GetBytes(5);
            var code = Convert.ToHexString(bytes).ToLowerInvariant();
            codes.Add($"{code[..5]}-{code[5..]}");
        }

        return codes;
    }

    private static string PendingSecretCacheKey(long userId) => $"mfa:enroll:{userId}";

    private static string ComputeSha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }
}
