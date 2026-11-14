namespace ShieldReport.Application.Audit;

// Explicit security events that are not data changes (data changes are captured automatically by
// AuditSaveChangesInterceptor): sign-ins, MFA, file downloads. Writes in its own DI scope, so it
// never flushes the caller's half-finished unit of work, and never throws — a failed audit write
// must not break the action being audited.
public interface IAuditLogger
{
    Task LogAsync(string action, string entityName, string? entityId = null, object? details = null, long? userId = null, CancellationToken cancellationToken = default);
}

public static class AuditActions
{
    public const string LoginSucceeded = "LoginSucceeded";
    public const string LoginFailed = "LoginFailed";
    public const string MfaChallengeIssued = "MfaChallengeIssued";
    public const string MfaSucceeded = "MfaSucceeded";
    public const string MfaFailed = "MfaFailed";
    public const string Downloaded = "Downloaded";
}
