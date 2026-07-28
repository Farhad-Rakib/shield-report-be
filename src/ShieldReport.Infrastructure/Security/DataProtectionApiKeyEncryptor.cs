using Microsoft.AspNetCore.DataProtection;
using ShieldReport.Application.Common.Interfaces.Security;

namespace ShieldReport.Infrastructure.Security;

public sealed class DataProtectionApiKeyEncryptor : IApiKeyEncryptor
{
    private const string Purpose = "ShieldReport.LlmProviderConfiguration.ApiKey";

    private readonly IDataProtector _protector;

    public DataProtectionApiKeyEncryptor(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector(Purpose);
    }

    public string Encrypt(string plainText) => _protector.Protect(plainText);

    public string Decrypt(string cipherText) => _protector.Unprotect(cipherText);

    public string Mask(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return string.Empty;
        }

        const int visibleSuffixLength = 4;
        return plainText.Length <= visibleSuffixLength
            ? new string('*', plainText.Length)
            : $"{new string('*', 8)}{plainText[^visibleSuffixLength..]}";
    }
}
