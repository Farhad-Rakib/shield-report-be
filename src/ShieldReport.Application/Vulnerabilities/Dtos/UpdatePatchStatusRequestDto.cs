using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.Vulnerabilities.Dtos;

public sealed record UpdatePatchStatusRequestDto(PatchStatus PatchStatus);
