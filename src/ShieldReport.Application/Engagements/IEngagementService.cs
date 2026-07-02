using ShieldReport.Application.Common.Models;
using ShieldReport.Application.Engagements.Dtos;
using ShieldReport.Domain.Entities;

namespace ShieldReport.Application.Engagements;

public interface IEngagementService
{
    Task<PagedResult<EngagementDto>> ListEngagementsAsync(PagedRequest request, long? clientOrganizationId, CancellationToken cancellationToken = default);
    Task<EngagementDto> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<EngagementDto> CreateAsync(CreateEngagementRequestDto request, long createdByUserId, CancellationToken cancellationToken = default);
    Task<EngagementDto> UpdateAsync(long id, UpdateEngagementRequestDto request, CancellationToken cancellationToken = default);
    Task<EngagementDto> AssignUserAsync(long engagementId, AssignEngagementUserRequestDto request, CancellationToken cancellationToken = default);
    Task<EngagementDto> RemoveAssigneeAsync(long engagementId, long userId, CancellationToken cancellationToken = default);

    // Used by Scan/Vulnerability creation flows to enforce the "is the caller the lead or
    // an assignee of this engagement" rule (TASK-GROUPS-EngagementManagement.md Group C #7/#8).
    Task<Engagement> GetEngagementForAssignmentCheckAsync(long engagementId, CancellationToken cancellationToken = default);
}
