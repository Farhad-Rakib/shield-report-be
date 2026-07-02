using System.Security.Cryptography;
using ShieldReport.Application.Common.Interfaces.Security;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Infrastructure.Authentication;

public sealed class RegistrationInviteTokenGenerator : IRegistrationInviteTokenGenerator
{
    public RegistrationInviteTokenGenerationResult Generate(InviteLifetime lifetime)
    {
        var randomBytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(randomBytes);
        var expiresAtUtc = DateTime.UtcNow.Add(lifetime.ToTimeSpan());

        return new RegistrationInviteTokenGenerationResult(token, expiresAtUtc);
    }
}
