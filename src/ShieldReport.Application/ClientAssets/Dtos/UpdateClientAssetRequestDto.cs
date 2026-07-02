using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.ClientAssets.Dtos;

public sealed record UpdateClientAssetRequestDto(
    string Name,
    string Identifier,
    AssetEnvironment Environment,
    Criticality Criticality);
