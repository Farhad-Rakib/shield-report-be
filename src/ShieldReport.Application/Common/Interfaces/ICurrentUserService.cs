namespace ShieldReport.Application.Common.Interfaces;

public interface ICurrentUserService
{
    long? UserId { get; }
    string? Email { get; }
    long? ClientOrganizationId { get; }
    bool IsClientPortalUser { get; }
    IReadOnlyCollection<string> Roles { get; }

    // Client IP for the audit log (the real client behind the reverse proxy — Program.cs applies
    // X-Forwarded-For via UseForwardedHeaders). Null outside an HTTP request.
    string? IpAddress { get; }
}
