namespace ShieldReport.Domain.Entities;

// Outgoing mail (SMTP) configuration, edited on Administration → Email Configuration. A single
// row; when it is missing or has no host, the `Smtp:*` configuration (env vars) is used instead.
public sealed class EmailSetting : BaseEntity
{
    public string Host { get; private set; } = string.Empty;
    public int Port { get; private set; } = 587;
    public bool UseSsl { get; private set; } = true;
    public string Username { get; private set; } = string.Empty;
    // Encrypted with the DataProtection key ring (IApiKeyEncryptor); never returned by the API.
    public string PasswordEncrypted { get; private set; } = string.Empty;
    public string FromAddress { get; private set; } = string.Empty;
    public string FromName { get; private set; } = string.Empty;

    public void Update(string host, int port, bool useSsl, string username, string? passwordEncrypted, string fromAddress, string fromName, long updatedByUserId)
    {
        Host = host;
        Port = port;
        UseSsl = useSsl;
        Username = username;
        // null keeps the stored password; empty string clears it.
        if (passwordEncrypted is not null)
        {
            PasswordEncrypted = passwordEncrypted;
        }

        FromAddress = fromAddress;
        FromName = fromName;
        UpdatedBy = updatedByUserId;
        UpdatedAt = DateTime.UtcNow;
    }
}
