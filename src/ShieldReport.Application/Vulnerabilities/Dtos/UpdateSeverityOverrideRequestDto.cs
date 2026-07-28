using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.Vulnerabilities.Dtos;

// SeverityOverride null means "clear the override, revert to the raw scanner-assigned severity".
public sealed record UpdateSeverityOverrideRequestDto(Severity? SeverityOverride);
