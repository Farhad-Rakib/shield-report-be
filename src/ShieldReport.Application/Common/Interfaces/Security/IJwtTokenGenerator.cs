using ShieldReport.Domain.Entities;

namespace ShieldReport.Application.Common.Interfaces.Security;

public interface IJwtTokenGenerator
{
    string GenerateAccessToken(User user, IEnumerable<string> permissions, IEnumerable<string> amr);
    DateTime GetAccessTokenExpiryUtc();
}
