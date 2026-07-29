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

    // Per-client toggle for whether client-portal users (CLIENT_ADMIN/CLIENT_USER) can see the
    // Scans list/detail/live console at all. Unlike AllowSelfServiceScanning (which only gates
    // the client's own scan-trigger button), this gates read access to scan data entirely — so
    // it defaults false: a brand-new client org sees nothing about scans until staff explicitly
    // opts them in, rather than inheriting an always-on default with no prior behavior to preserve.
    public bool AllowScanConsoleAccess { get; private set; }

    private ClientOrganization()
    {
    }

    public ClientOrganization(string name, string? primaryContactName = null, string? primaryContactEmail = null, bool allowSelfServiceScanning = true, bool allowScanConsoleAccess = false)
    {
        Name = !string.IsNullOrWhiteSpace(name)
            ? name.Trim()
            : throw new ArgumentException("Name is required.", nameof(name));
        PrimaryContactName = primaryContactName;
        PrimaryContactEmail = primaryContactEmail;
        AllowSelfServiceScanning = allowSelfServiceScanning;
        AllowScanConsoleAccess = allowScanConsoleAccess;
    }

    public void UpdateDetails(string name, string? primaryContactName, string? primaryContactEmail, bool allowSelfServiceScanning, bool allowScanConsoleAccess)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            Name = name.Trim();
        }

        PrimaryContactName = primaryContactName;
        PrimaryContactEmail = primaryContactEmail;
        AllowSelfServiceScanning = allowSelfServiceScanning;
        AllowScanConsoleAccess = allowScanConsoleAccess;
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
