using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ShieldReport.Application.Common.Interfaces.Security;
using ShieldReport.Domain.Entities;

namespace ShieldReport.Infrastructure.Authentication;

public sealed class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtOptions _jwtOptions;

    public JwtTokenGenerator(IOptions<JwtOptions> jwtOptions)
    {
        _jwtOptions = jwtOptions.Value;
    }

    public string GenerateAccessToken(User user, IEnumerable<string> permissions, IEnumerable<string> amr)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, user.FullName),
            new(ClaimTypes.NameIdentifier, user.Id.ToString())
        };

        foreach (var role in user.UserRoles.Select(x => x.Role.Name).Distinct())
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        if (user.ClientOrganizationId.HasValue)
        {
            claims.Add(new Claim("clientOrganizationId", user.ClientOrganizationId.Value.ToString()));
        }

        if (user.IsClientPortalUser)
        {
            claims.Add(new Claim("isClientPortalUser", "true"));
        }

        foreach (var permission in permissions.Distinct())
        {
            claims.Add(new Claim("permission", permission));
        }

        foreach (var method in amr.Distinct())
        {
            claims.Add(new Claim("amr", method));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpiryMinutes);

        var tokenDescriptor = new JwtSecurityToken(
            _jwtOptions.Issuer,
            _jwtOptions.Audience,
            claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);
    }

    public DateTime GetAccessTokenExpiryUtc()
    {
        return DateTime.UtcNow.AddMinutes(_jwtOptions.ExpiryMinutes);
    }
}
