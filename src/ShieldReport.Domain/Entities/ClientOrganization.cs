namespace ShieldReport.Domain.Entities;

public sealed class ClientOrganization : BaseEntity
{
    public Guid PublicId { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = string.Empty;
    public string? PrimaryContactName { get; private set; }
    public string? PrimaryContactEmail { get; private set; }
    public bool IsActive { get; private set; } = true;

    // Per-client toggle for self-service scanning (Assets page "Run Scan", outside any
    // engagement). Staff-triggered scans (Pentester/Admin, incl. Engagement "Run Scoped Scan")
    // are never affected by this — it only gates the client's own self-service trigger.
    // Defaults true so existing clients' current (always-on) behavior doesn't change until
    // staff explicitly turns it off for a specific org.
    public bool AllowSelfServiceScanning { get; private set; } = true;

    private ClientOrganization()
    {
    }

    public ClientOrganization(string name, string? primaryContactName = null, string? primaryContactEmail = null, bool allowSelfServiceScanning = true)
    {
        Name = !string.IsNullOrWhiteSpace(name)
            ? name.Trim()
            : throw new ArgumentException("Name is required.", nameof(name));
        PrimaryContactName = primaryContactName;
        PrimaryContactEmail = primaryContactEmail;
        AllowSelfServiceScanning = allowSelfServiceScanning;
    }

    public void UpdateDetails(string name, string? primaryContactName, string? primaryContactEmail, bool allowSelfServiceScanning)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            Name = name.Trim();
        }

        PrimaryContactName = primaryContactName;
        PrimaryContactEmail = primaryContactEmail;
        AllowSelfServiceScanning = allowSelfServiceScanning;
    }

    public void Disable()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }
}
