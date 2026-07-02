namespace ShieldReport.Application.Common.Interfaces.Security;

public sealed record RegistrationInviteTokenGenerationResult(string Token, DateTime ExpiresAtUtc);
