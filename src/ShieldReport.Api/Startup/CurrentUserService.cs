using System.Security.Claims;
using ShieldReport.Application.Common.Interfaces;

namespace ShieldReport.Api.Startup;

public sealed class CurrentUserService : ICurrentUserService
{
    private readonly ClaimsPrincipal? _user;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _user = httpContextAccessor.HttpContext?.User;
    }

    public long? UserId
    {
        get
        {
            var value = _user?.FindFirst("sub")?.Value;
            return long.TryParse(value, out var id) ? id : null;
        }
    }

    public string? Email => _user?.FindFirst("email")?.Value;

    public long? ClientOrganizationId
    {
        get
        {
            var value = _user?.FindFirst("clientOrganizationId")?.Value;
            return long.TryParse(value, out var id) ? id : null;
        }
    }

    public bool IsClientPortalUser => _user?.FindFirst("isClientPortalUser")?.Value == "true";

    public IReadOnlyCollection<string> Roles =>
        _user?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray() ?? Array.Empty<string>();
}
