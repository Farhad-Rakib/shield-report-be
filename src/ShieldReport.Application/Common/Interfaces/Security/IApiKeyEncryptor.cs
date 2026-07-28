namespace ShieldReport.Application.Common.Interfaces.Security;

public interface IApiKeyEncryptor
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);

    // Never exposes the decrypted value — used to render something like "sk-...ab12" in a
    // read DTO so the UI can confirm which key is configured without ever round-tripping it.
    string Mask(string plainText);
}
