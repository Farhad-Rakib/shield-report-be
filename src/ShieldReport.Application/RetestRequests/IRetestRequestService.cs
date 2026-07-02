using ShieldReport.Application.RetestRequests.Dtos;

namespace ShieldReport.Application.RetestRequests;

public interface IRetestRequestService
{
    Task<RetestRequestDto?> GetCurrentAsync(long vulnerabilityId, CancellationToken cancellationToken = default);

    Task<RetestRequestDto> CreateAsync(
        long vulnerabilityId,
        CreateRetestRequestDto request,
        long requestedByUserId,
        bool isPlatformAdmin,
        CancellationToken cancellationToken = default);

    Task<RetestRequestDto> ResolveAsync(
        long vulnerabilityId,
        ResolveRetestRequestDto request,
        long resolvedByUserId,
        CancellationToken cancellationToken = default);
}
