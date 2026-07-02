using ShieldReport.Domain.Enums;

namespace ShieldReport.Domain.Entities;

public sealed class ClientAsset : BaseEntity
{
    public long ClientOrganizationId { get; private set; }
    public Guid PublicId { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = string.Empty;
    public AssetType AssetType { get; private set; }
    public string Identifier { get; private set; } = string.Empty;
    public AssetEnvironment Environment { get; private set; }
    public Criticality Criticality { get; private set; }
    public bool IsAuthorizedForScanning { get; private set; }
    public DateTime? AuthorizedAt { get; private set; }
    public long? AuthorizedByUserId { get; private set; }
    public bool IsActive { get; private set; } = true;

    public ClientOrganization ClientOrganization { get; private set; } = null!;

    private ClientAsset()
    {
    }

    public ClientAsset(long clientOrganizationId, string name, AssetType assetType, string identifier, AssetEnvironment environment, Criticality criticality)
    {
        ClientOrganizationId = clientOrganizationId;
        Name = !string.IsNullOrWhiteSpace(name)
            ? name.Trim()
            : throw new ArgumentException("Name is required.", nameof(name));
        AssetType = assetType;
        Identifier = !string.IsNullOrWhiteSpace(identifier)
            ? identifier.Trim()
            : throw new ArgumentException("Identifier is required.", nameof(identifier));
        Environment = environment;
        Criticality = criticality;
    }

    public void UpdateDetails(string name, string identifier, AssetEnvironment environment, Criticality criticality)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            Name = name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(identifier))
        {
            Identifier = identifier.Trim();
        }

        Environment = environment;
        Criticality = criticality;
    }

    // Registration and scan-authorization are two separate, deliberate steps — never automatic
    // (see BUSINESS-FLOW-PentestOps.md §4) — so this is the only way IsAuthorizedForScanning flips on.
    public void AuthorizeForScanning(long authorizedByUserId)
    {
        IsAuthorizedForScanning = true;
        AuthorizedAt = DateTime.UtcNow;
        AuthorizedByUserId = authorizedByUserId;
    }

    public void RevokeScanAuthorization()
    {
        IsAuthorizedForScanning = false;
        AuthorizedAt = null;
        AuthorizedByUserId = null;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }
}
