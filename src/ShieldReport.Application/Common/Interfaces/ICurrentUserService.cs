namespace ShieldReport.Application.Common.Interfaces;

public interface ICurrentUserService
{
    long? UserId { get; }
    string? Email { get; }
    long? ClientOrganizationId { get; }
    bool IsClientPortalUser { get; }
    IReadOnlyCollection<string> Roles { get; }

    // True for a signed-in staff user who is neither SuperAdmin nor granted "data.view-all":
    // they only see engagements they lead, are assigned to or created, and that data (EF query
    // filters in ApplicationDbContext). False for client-portal users (scoped by organization
    // instead) and for background work with no user.
    bool IsRestrictedToAssignedEngagements { get; }

    // Client IP for the audit log (the real client behind the reverse proxy — Program.cs applies
    // X-Forwarded-For via UseForwardedHeaders). Null outside an HTTP request.
    string? IpAddress { get; }
}
