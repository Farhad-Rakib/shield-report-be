namespace ShieldReport.Application.ClientAssets.Dtos;

public sealed record ClientAssetDto(
    long Id,
    Guid PublicId,
    long ClientOrganizationId,
    string ClientOrganizationName,
    string Name,
    string AssetType,
    string Identifier,
    string Environment,
    string Criticality,
    bool IsAuthorizedForScanning,
    DateTime? AuthorizedAt,
    long? AuthorizedByUserId,
    bool IsActive);
