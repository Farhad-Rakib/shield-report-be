using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.ClientAssets.Dtos;

public sealed record CreateClientAssetRequestDto(
    long ClientOrganizationId,
    string Name,
    AssetType AssetType,
    string Identifier,
    AssetEnvironment Environment,
    Criticality Criticality);
