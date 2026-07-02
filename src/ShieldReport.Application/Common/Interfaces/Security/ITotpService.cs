namespace ShieldReport.Application.Common.Interfaces.Security;

public interface ITotpService
{
    string GenerateSecretKey();
    string GetOtpAuthUri(string secretKey, string accountEmail, string issuer);
    bool ValidateCode(string secretKey, string code);
}
