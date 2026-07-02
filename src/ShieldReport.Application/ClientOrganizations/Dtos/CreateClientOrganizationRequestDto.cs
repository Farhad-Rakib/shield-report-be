namespace ShieldReport.Application.ClientOrganizations.Dtos;

public sealed record CreateClientOrganizationRequestDto(
    string Name,
    string? PrimaryContactName,
    string? PrimaryContactEmail,
    bool AllowSelfServiceScanning = true);
