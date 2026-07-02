namespace ShieldReport.Application.Common.Interfaces;

public interface ICurrentUserService
{
    long? UserId { get; }
    string? Email { get; }
    long? ClientOrganizationId { get; }
    bool IsClientPortalUser { get; }
    IReadOnlyCollection<string> Roles { get; }
}
