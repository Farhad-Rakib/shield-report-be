namespace ShieldReport.Application.ClientOrganizations.Dtos;

public sealed record UpdateClientOrganizationRequestDto(
    string Name,
    string? PrimaryContactName,
    string? PrimaryContactEmail,
    bool AllowSelfServiceScanning);
