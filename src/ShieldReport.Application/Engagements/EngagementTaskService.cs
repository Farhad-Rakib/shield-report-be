using ShieldReport.Application.Common.Exceptions;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Application.Engagements.Dtos;
using ShieldReport.Domain.Entities;

namespace ShieldReport.Application.Engagements;

public sealed class EngagementTaskService : IEngagementTaskService
{
    private readonly IEngagementTaskRepository _engagementTaskRepository;
    private readonly IEngagementRepository _engagementRepository;
    private readonly IRepository<ClientAsset> _clientAssetRepository;
    private readonly IUnitOfWork _unitOfWork;

    public EngagementTaskService(
        IEngagementTaskRepository engagementTaskRepository,
        IEngagementRepository engagementRepository,
        IRepository<ClientAsset> clientAssetRepository,
        IUnitOfWork unitOfWork)
    {
        _engagementTaskRepository = engagementTaskRepository;
        _engagementRepository = engagementRepository;
        _clientAssetRepository = clientAssetRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<EngagementTaskDto>> ListByEngagementAsync(long engagementId, CancellationToken cancellationToken = default)
    {
        var tasks = await _engagementTaskRepository.GetByEngagementIdAsync(engagementId, cancellationToken);
        return tasks.Select(ToDto).ToList();
    }

    public async Task<EngagementTaskDto> CreateAsync(long engagementId, CreateEngagementTaskRequestDto request, long createdByUserId, CancellationToken cancellationToken = default)
    {
        var engagement = await _engagementRepository.GetByIdWithDetailsAsync(engagementId, cancellationToken)
            ?? throw new AppException("Engagement not found.", 404);

        foreach (var clientAssetId in request.ClientAssetIds)
        {
            var asset = await _clientAssetRepository.GetByIdAsync(clientAssetId, cancellationToken)
                ?? throw new AppException($"Client asset {clientAssetId} not found.", 404);

            if (asset.ClientOrganizationId != engagement.ClientOrganizationId)
            {
                throw new AppException($"Client asset {clientAssetId} does not belong to this engagement's client organization.", 400);
            }
        }

        var task = new EngagementTask(engagementId, request.Title, request.AssignedToUserId, createdByUserId, request.Description);
        foreach (var clientAssetId in request.ClientAssetIds)
        {
            task.Assets.Add(new EngagementTaskAsset { ClientAssetId = clientAssetId });
        }

        await _engagementTaskRepository.AddAsync(task, cancellationToken);

        // Creating a sub-task for a non-assignee auto-adds them — no separate two-step
        // assignment needed (TASK-GROUPS-EngagementManagement.md Group B2 #4c).
        if (engagement.LeadPentesterId != request.AssignedToUserId && !engagement.Assignees.Any(a => a.UserId == request.AssignedToUserId))
        {
            engagement.Assignees.Add(new EngagementAssignee { EngagementId = engagementId, UserId = request.AssignedToUserId });
            _engagementRepository.Update(engagement);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var created = await _engagementTaskRepository.GetByIdWithDetailsAsync(task.Id, cancellationToken)
            ?? throw new AppException("Engagement task not found.", 404);

        return ToDto(created);
    }

    public async Task<EngagementTaskDto> UpdateAsync(long engagementId, long taskId, UpdateEngagementTaskRequestDto request, CancellationToken cancellationToken = default)
    {
        var task = await GetTaskInEngagementAsync(engagementId, taskId, cancellationToken);

        task.UpdateDetails(request.Title, request.Description);
        _engagementTaskRepository.Update(task);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(task);
    }

    public async Task<EngagementTaskDto> UpdateStatusAsync(long engagementId, long taskId, UpdateEngagementTaskStatusRequestDto request, long currentUserId, bool callerHasAssignPermission, CancellationToken cancellationToken = default)
    {
        var task = await GetTaskInEngagementAsync(engagementId, taskId, cancellationToken);

        var isSelfAssigned = task.AssignedToUserId == currentUserId;
        if (!isSelfAssigned && !callerHasAssignPermission)
        {
            throw new AppException("Only the assigned pentester or a user with engagements.assign can update this sub-task's status.", 403);
        }

        task.SetStatus(request.Status);
        _engagementTaskRepository.Update(task);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(task);
    }

    private async Task<EngagementTask> GetTaskInEngagementAsync(long engagementId, long taskId, CancellationToken cancellationToken)
    {
        var task = await _engagementTaskRepository.GetByIdWithDetailsAsync(taskId, cancellationToken)
            ?? throw new AppException("Engagement task not found.", 404);

        if (task.EngagementId != engagementId)
        {
            throw new AppException("Engagement task not found.", 404);
        }

        return task;
    }

    private static EngagementTaskDto ToDto(EngagementTask task)
    {
        return new EngagementTaskDto(
            task.Id,
            task.EngagementId,
            task.Title,
            task.Description,
            task.AssignedToUserId,
            task.AssignedToUser?.FullName ?? string.Empty,
            task.Status.ToString(),
            task.CreatedByUserId,
            task.Assets.Select(a => a.ClientAssetId).ToList());
    }
}
