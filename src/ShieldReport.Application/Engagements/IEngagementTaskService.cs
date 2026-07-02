using ShieldReport.Application.Engagements.Dtos;

namespace ShieldReport.Application.Engagements;

public interface IEngagementTaskService
{
    Task<IReadOnlyList<EngagementTaskDto>> ListByEngagementAsync(long engagementId, CancellationToken cancellationToken = default);

    Task<EngagementTaskDto> CreateAsync(long engagementId, CreateEngagementTaskRequestDto request, long createdByUserId, CancellationToken cancellationToken = default);

    Task<EngagementTaskDto> UpdateAsync(long engagementId, long taskId, UpdateEngagementTaskRequestDto request, CancellationToken cancellationToken = default);

    Task<EngagementTaskDto> UpdateStatusAsync(long engagementId, long taskId, UpdateEngagementTaskStatusRequestDto request, long currentUserId, bool callerHasAssignPermission, CancellationToken cancellationToken = default);
}
