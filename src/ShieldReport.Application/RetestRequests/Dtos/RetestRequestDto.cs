using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.RetestRequests.Dtos;

public sealed record RetestRequestDto(
    long Id,
    long VulnerabilityId,
    long RequestedByUserId,
    string RequestedByUserName,
    DateTime RequestedAt,
    long? AssignedToUserId,
    string? AssignedToUserName,
    string? Instructions,
    string Status,
    long? ResolvedByUserId,
    string? ResolvedByUserName,
    DateTime? ResolvedAt,
    string? ResolutionNotes,
    IReadOnlyList<RetestRequestCaseDto> Cases);

public sealed record RetestRequestCaseDto(long Id, string CaseText, bool IsChecked);

public sealed record CreateRetestRequestDto(long? AssignedToUserId, string? Instructions, IReadOnlyList<string>? Cases);

public sealed record ResolveRetestRequestDto(RetestRequestStatus Outcome, string? ResolutionNotes, IReadOnlyList<RetestRequestCaseUpdateDto>? Cases);

public sealed record RetestRequestCaseUpdateDto(long Id, bool IsChecked);
