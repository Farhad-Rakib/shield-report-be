using ShieldReport.Application.Common.Exceptions;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Application.Common.Models;
using ShieldReport.Application.Engagements.Dtos;
using ShieldReport.Application.Notifications;
using ShieldReport.Domain.Entities;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.Engagements;

public sealed class EngagementService : IEngagementService
{
    private readonly IEngagementRepository _engagementRepository;
    private readonly IUserRepository _userRepository;
    private readonly INotificationService _notificationService;
    private readonly IUnitOfWork _unitOfWork;

    public EngagementService(
        IEngagementRepository engagementRepository,
        IUserRepository userRepository,
        INotificationService notificationService,
        IUnitOfWork unitOfWork)
    {
        _engagementRepository = engagementRepository;
        _userRepository = userRepository;
        _notificationService = notificationService;
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<EngagementDto>> ListEngagementsAsync(PagedRequest request, long? clientOrganizationId, CancellationToken cancellationToken = default)
    {
        var (items, total) = await _engagementRepository.GetPagedAsync(request.Page, request.PageSize, request.Search, clientOrganizationId, cancellationToken);
        return PagedResult<EngagementDto>.Create(items.Select(ToDto).ToList(), total, request.Page, request.PageSize);
    }

    public async Task<EngagementDto> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var engagement = await _engagementRepository.GetByIdWithDetailsAsync(id, cancellationToken)
            ?? throw new AppException("Engagement not found.", 404);

        return ToDto(engagement);
    }

    public async Task<EngagementDto> CreateAsync(CreateEngagementRequestDto request, long createdByUserId, CancellationToken cancellationToken = default)
    {
        await EnsurePentesterAsync(request.LeadPentesterId, cancellationToken);

        var engagement = new Engagement(
            request.ClientOrganizationId,
            request.Title,
            request.LeadPentesterId,
            createdByUserId,
            request.Scope,
            request.StartDate,
            request.EndDate);

        await _engagementRepository.AddAsync(engagement, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var created = await _engagementRepository.GetByIdWithDetailsAsync(engagement.Id, cancellationToken)
            ?? throw new AppException("Engagement not found.", 404);

        return ToDto(created);
    }

    public async Task<EngagementDto> UpdateAsync(long id, UpdateEngagementRequestDto request, CancellationToken cancellationToken = default)
    {
        var engagement = await _engagementRepository.GetByIdWithDetailsAsync(id, cancellationToken)
            ?? throw new AppException("Engagement not found.", 404);

        engagement.UpdateDetails(request.Title, request.Scope, request.StartDate, request.EndDate);

        if (request.Status.HasValue && request.Status.Value != engagement.Status)
        {
            engagement.ChangeStatus(request.Status.Value);
            _engagementRepository.Update(engagement);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await NotifyClientAdminsOfStatusChangeAsync(engagement, cancellationToken);
        }
        else
        {
            _engagementRepository.Update(engagement);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return ToDto(engagement);
    }

    public async Task<EngagementDto> AssignUserAsync(long engagementId, AssignEngagementUserRequestDto request, CancellationToken cancellationToken = default)
    {
        var engagement = await _engagementRepository.GetByIdWithDetailsAsync(engagementId, cancellationToken)
            ?? throw new AppException("Engagement not found.", 404);

        await EnsurePentesterAsync(request.UserId, cancellationToken);

        if (request.IsLead)
        {
            engagement.ReassignLead(request.UserId);
        }
        else if (!engagement.Assignees.Any(a => a.UserId == request.UserId))
        {
            engagement.Assignees.Add(new EngagementAssignee { EngagementId = engagement.Id, UserId = request.UserId });
        }

        _engagementRepository.Update(engagement);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updated = await _engagementRepository.GetByIdWithDetailsAsync(engagementId, cancellationToken)
            ?? throw new AppException("Engagement not found.", 404);

        return ToDto(updated);
    }

    public async Task<EngagementDto> RemoveAssigneeAsync(long engagementId, long userId, CancellationToken cancellationToken = default)
    {
        var engagement = await _engagementRepository.GetByIdWithDetailsAsync(engagementId, cancellationToken)
            ?? throw new AppException("Engagement not found.", 404);

        if (engagement.LeadPentesterId == userId)
        {
            throw new AppException("Cannot remove the lead pentester — reassign the lead instead.", 400);
        }

        var assignee = engagement.Assignees.FirstOrDefault(a => a.UserId == userId)
            ?? throw new AppException("Assignee not found.", 404);

        engagement.Assignees.Remove(assignee);
        _engagementRepository.Update(engagement);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(engagement);
    }

    public async Task<Engagement> GetEngagementForAssignmentCheckAsync(long engagementId, CancellationToken cancellationToken = default)
    {
        return await _engagementRepository.GetByIdWithDetailsAsync(engagementId, cancellationToken)
            ?? throw new AppException("Engagement not found.", 404);
    }

    private async Task EnsurePentesterAsync(long userId, CancellationToken cancellationToken)
    {
        var isPentester = await _userRepository.HasRoleAsync(userId, SystemRoles.Pentester, cancellationToken);
        if (!isPentester)
        {
            throw new AppException("Target user must hold the PENTESTER role.", 400);
        }
    }

    private async Task NotifyClientAdminsOfStatusChangeAsync(Engagement engagement, CancellationToken cancellationToken)
    {
        var clientAdmins = await _userRepository.GetByClientOrganizationAndRoleAsync(engagement.ClientOrganizationId, SystemRoles.ClientAdmin, cancellationToken);
        foreach (var clientAdmin in clientAdmins)
        {
            await _notificationService.CreateNotificationAsync(
                clientAdmin.Id,
                "ENGAGEMENT_STATUS_CHANGE",
                $"Engagement \"{engagement.Title}\" status changed to {engagement.Status}.",
                cancellationToken);
        }
    }

    private static EngagementDto ToDto(Engagement engagement)
    {
        return new EngagementDto(
            engagement.Id,
            engagement.PublicId,
            engagement.ClientOrganizationId,
            engagement.ClientOrganization?.Name ?? string.Empty,
            engagement.Title,
            engagement.Scope,
            engagement.Status.ToString(),
            engagement.StartDate,
            engagement.EndDate,
            engagement.LeadPentesterId,
            engagement.LeadPentester?.FullName ?? string.Empty,
            engagement.CreatedByUserId,
            engagement.Assignees.Select(a => new EngagementAssigneeDto(a.UserId, a.User?.FullName ?? string.Empty, a.AssignedAt)).ToList());
    }
}
