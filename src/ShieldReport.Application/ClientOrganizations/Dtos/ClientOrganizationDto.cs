namespace ShieldReport.Application.ClientOrganizations.Dtos;

public sealed record ClientOrganizationDto(
    long Id,
    Guid PublicId,
    string Name,
    string? PrimaryContactName,
    string? PrimaryContactEmail,
    bool IsActive,
    bool AllowSelfServiceScanning);
