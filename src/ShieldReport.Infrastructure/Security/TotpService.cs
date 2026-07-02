using OtpNet;
using ShieldReport.Application.Common.Interfaces.Security;

namespace ShieldReport.Infrastructure.Security;

public sealed class TotpService : ITotpService
{
    private const int SecretKeyBytes = 20;

    public string GenerateSecretKey()
    {
        var key = KeyGeneration.GenerateRandomKey(SecretKeyBytes);
        return Base32Encoding.ToString(key);
    }

    public string GetOtpAuthUri(string secretKey, string accountEmail, string issuer)
    {
        var encodedIssuer = Uri.EscapeDataString(issuer);
        var encodedAccount = Uri.EscapeDataString(accountEmail);

        return $"otpauth://totp/{encodedIssuer}:{encodedAccount}?secret={secretKey}&issuer={encodedIssuer}&algorithm=SHA1&digits=6&period=30";
    }

    public bool ValidateCode(string secretKey, string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var keyBytes = Base32Encoding.ToBytes(secretKey);
        var totp = new Totp(keyBytes);

        return totp.VerifyTotp(code, out _, new VerificationWindow(previous: 1, future: 1));
    }
}
