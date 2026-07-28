using System.Security.Cryptography;
using ShieldReport.Application.Common.Interfaces.Security;
using Microsoft.Extensions.Options;

namespace ShieldReport.Infrastructure.Authentication;

public sealed class PasswordResetTokenGenerator : IPasswordResetTokenGenerator
{
    private const int TokenExpiryHours = 24;

    public PasswordResetTokenGenerationResult Generate()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(32);
        // Base64Url — this token gets embedded in a query string; standard Base64's '+' would
        // silently decode as a literal space by the time the SPA reads it back out of the URL.
        var token = ToBase64Url(randomBytes);
        var expiresAtUtc = DateTime.UtcNow.AddHours(TokenExpiryHours);

        return new PasswordResetTokenGenerationResult(token, expiresAtUtc);
    }

    private static string ToBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
