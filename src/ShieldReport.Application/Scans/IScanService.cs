using ShieldReport.Application.Common.Models;
using ShieldReport.Application.Scans.Dtos;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.Scans;

public interface IScanService
{
    Task<PagedResult<ScanDto>> ListAsync(PagedRequest request, long? clientOrganizationId, long? clientAssetId, long? engagementId, ScanStatus? status, DateTime? from, DateTime? to, bool callerIsClientPortalUser, long? callerClientOrganizationId, CancellationToken cancellationToken = default);

    Task<ScanDto> GetByPublicIdAsync(Guid publicId, bool callerIsClientPortalUser, long? callerClientOrganizationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ScanDto>> CreateAsync(long clientAssetId, CreateScanRequestDto request, long requestedByUserId, bool callerIsPentester, bool callerIsClientPortalUser, CancellationToken cancellationToken = default);

    Task<ScanDto> CancelAsync(Guid publicId, long cancelledByUserId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ScanFindingRawDto>> GetRawFindingsAsync(Guid publicId, bool callerIsClientPortalUser, long? callerClientOrganizationId, CancellationToken cancellationToken = default);
}
