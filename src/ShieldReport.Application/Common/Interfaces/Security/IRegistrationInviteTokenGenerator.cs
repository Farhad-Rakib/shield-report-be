using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.Common.Interfaces.Security;

public interface IRegistrationInviteTokenGenerator
{
    RegistrationInviteTokenGenerationResult Generate(InviteLifetime lifetime);
}
