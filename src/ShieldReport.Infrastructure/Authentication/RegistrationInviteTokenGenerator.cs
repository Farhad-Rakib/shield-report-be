using System.Security.Cryptography;
using ShieldReport.Application.Common.Interfaces.Security;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Infrastructure.Authentication;

public sealed class RegistrationInviteTokenGenerator : IRegistrationInviteTokenGenerator
{
    public RegistrationInviteTokenGenerationResult Generate(InviteLifetime lifetime)
    {
        var randomBytes = RandomNumberGenerator.GetBytes(32);
        // Base64Url, not raw Base64 — this token gets embedded directly into a URL *path*
        // segment (/register/{token}); a stray '/' from standard Base64 breaks routing outright
        // (splits into an extra path segment), which is not a hypothetical — it reproduced on
        // essentially the first token generated in manual testing.
        var token = ToBase64Url(randomBytes);
        var expiresAtUtc = DateTime.UtcNow.Add(lifetime.ToTimeSpan());

        return new RegistrationInviteTokenGenerationResult(token, expiresAtUtc);
    }

    private static string ToBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
