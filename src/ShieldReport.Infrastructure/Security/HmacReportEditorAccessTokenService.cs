using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using ShieldReport.Application.Common.Interfaces.Security;
using ShieldReport.Infrastructure.Authentication;

namespace ShieldReport.Infrastructure.Security;

public sealed class HmacReportEditorAccessTokenService : IReportEditorAccessTokenService
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(30);

    private readonly byte[] _key;

    public HmacReportEditorAccessTokenService(IOptions<JwtOptions> jwtOptions)
    {
        // Reuses the app's existing JWT signing key — same trust boundary as the real auth
        // tokens, just a narrower claim shape (report id + expiry only) and a much shorter
        // lifetime, since this token's only job is letting the OnlyOffice container fetch/save
        // one specific report's draft without a user session.
        _key = Encoding.UTF8.GetBytes(jwtOptions.Value.SecretKey);
    }

    public string Issue(long reportId)
    {
        var expiry = DateTimeOffset.UtcNow.Add(TokenLifetime).ToUnixTimeSeconds();
        var payload = $"{reportId}.{expiry}";
        var signature = Sign(payload);
        return $"{Base64UrlEncode(Encoding.UTF8.GetBytes(payload))}.{Base64UrlEncode(signature)}";
    }

    public bool TryValidate(string token, out long reportId)
    {
        reportId = 0;
        var parts = token.Split('.', 2);
        if (parts.Length != 2)
        {
            return false;
        }

        byte[] payloadBytes;
        byte[] providedSignature;
        try
        {
            payloadBytes = Base64UrlDecode(parts[0]);
            providedSignature = Base64UrlDecode(parts[1]);
        }
        catch (FormatException)
        {
            return false;
        }

        var payload = Encoding.UTF8.GetString(payloadBytes);
        var expectedSignature = Sign(payload);
        if (!CryptographicOperations.FixedTimeEquals(providedSignature, expectedSignature))
        {
            return false;
        }

        var payloadParts = payload.Split('.');
        if (payloadParts.Length != 2
            || !long.TryParse(payloadParts[0], out var parsedReportId)
            || !long.TryParse(payloadParts[1], out var expiryUnixSeconds))
        {
            return false;
        }

        if (DateTimeOffset.FromUnixTimeSeconds(expiryUnixSeconds) < DateTimeOffset.UtcNow)
        {
            return false;
        }

        reportId = parsedReportId;
        return true;
    }

    private byte[] Sign(string payload)
    {
        using var hmac = new HMACSHA256(_key);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - padded.Length % 4) % 4);
        return Convert.FromBase64String(padded);
    }
}
