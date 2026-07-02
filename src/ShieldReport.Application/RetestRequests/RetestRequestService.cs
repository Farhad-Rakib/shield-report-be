using ShieldReport.Application.Common.Exceptions;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Application.Notifications;
using ShieldReport.Application.RetestRequests.Dtos;
using ShieldReport.Domain.Entities;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.RetestRequests;

public sealed class RetestRequestService : IRetestRequestService
{
    private readonly IRetestRequestRepository _retestRequestRepository;
    private readonly IVulnerabilityRepository _vulnerabilityRepository;
    private readonly IUserRepository _userRepository;
    private readonly INotificationService _notificationService;
    private readonly IUnitOfWork _unitOfWork;

    public RetestRequestService(
        IRetestRequestRepository retestRequestRepository,
        IVulnerabilityRepository vulnerabilityRepository,
        IUserRepository userRepository,
        INotificationService notificationService,
        IUnitOfWork unitOfWork)
    {
        _retestRequestRepository = retestRequestRepository;
        _vulnerabilityRepository = vulnerabilityRepository;
        _userRepository = userRepository;
        _notificationService = notificationService;
        _unitOfWork = unitOfWork;
    }

    public async Task<RetestRequestDto?> GetCurrentAsync(long vulnerabilityId, CancellationToken cancellationToken = default)
    {
        await EnsureVulnerabilityExistsAsync(vulnerabilityId, cancellationToken);

        var retest = await _retestRequestRepository.GetLatestByVulnerabilityIdAsync(vulnerabilityId, cancellationToken);
        return retest is null ? null : ToDto(retest);
    }

    public async Task<RetestRequestDto> CreateAsync(
        long vulnerabilityId,
        CreateRetestRequestDto request,
        long requestedByUserId,
        bool isPlatformAdmin,
        CancellationToken cancellationToken = default)
    {
        var vulnerability = await _vulnerabilityRepository.GetByIdAsync(vulnerabilityId, cancellationToken)
            ?? throw new AppException("Vulnerability not found.", 404);

        // CLIENT_ADMIN can only raise a retest once a fix is in flight; PLATFORM_ADMIN is
        // ungated (covers QA-driven retests raised before the client even marks it patched).
        if (!isPlatformAdmin && vulnerability.PatchStatus is not (PatchStatus.Patched or PatchStatus.InProgress))
        {
            throw new AppException("A retest can only be requested once the vulnerability is marked Patched or In Progress.", 400);
        }

        var existing = await _retestRequestRepository.GetLatestByVulnerabilityIdAsync(vulnerabilityId, cancellationToken);
        if (existing?.Status == RetestRequestStatus.Pending)
        {
            throw new AppException("A retest request is already pending for this vulnerability.", 409);
        }

        if (request.AssignedToUserId.HasValue)
        {
            var isPentester = await _userRepository.HasRoleAsync(request.AssignedToUserId.Value, SystemRoles.Pentester, cancellationToken);
            if (!isPentester)
            {
                throw new AppException("The assigned user must hold the Pentester role.", 400);
            }
        }

        var retest = new RetestRequest(vulnerabilityId, requestedByUserId, request.AssignedToUserId, request.Instructions);
        if (request.Cases is { Count: > 0 })
        {
            foreach (var caseText in request.Cases)
            {
                retest.Cases.Add(new RetestRequestCase(caseText));
            }
        }

        await _retestRequestRepository.AddAsync(retest, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var created = await _retestRequestRepository.GetLatestByVulnerabilityIdAsync(vulnerabilityId, cancellationToken)
            ?? throw new AppException("Retest request not found.", 404);
        return ToDto(created);
    }

    public async Task<RetestRequestDto> ResolveAsync(
        long vulnerabilityId,
        ResolveRetestRequestDto request,
        long resolvedByUserId,
        CancellationToken cancellationToken = default)
    {
        var vulnerability = await _vulnerabilityRepository.GetByIdAsync(vulnerabilityId, cancellationToken)
            ?? throw new AppException("Vulnerability not found.", 404);

        var retest = await _retestRequestRepository.GetLatestByVulnerabilityIdAsync(vulnerabilityId, cancellationToken);
        if (retest is null || retest.Status != RetestRequestStatus.Pending)
        {
            throw new AppException("There is no pending retest request to resolve.", 409);
        }

        if (request.Outcome is not (RetestRequestStatus.VerifiedClosed or RetestRequestStatus.Reopened))
        {
            throw new AppException("Outcome must be VerifiedClosed or Reopened.", 400);
        }

        if (request.Cases is { Count: > 0 })
        {
            foreach (var update in request.Cases)
            {
                var matchingCase = retest.Cases.FirstOrDefault(c => c.Id == update.Id);
                matchingCase?.SetChecked(update.IsChecked);
            }
        }

        if (request.Outcome == RetestRequestStatus.VerifiedClosed)
        {
            retest.VerifyClosed(resolvedByUserId, request.ResolutionNotes);

            // Freeze DaysOpen at resolution time — see BUSINESS-FLOW-PentestOps.md §7.
            var frozenDaysOpen = (int)(DateTime.UtcNow - vulnerability.FirstSeenAt).TotalDays;
            vulnerability.Close(resolvedByUserId, frozenDaysOpen);
        }
        else
        {
            retest.Reopen(resolvedByUserId, request.ResolutionNotes);
            vulnerability.SetPatchStatus(PatchStatus.Open);
        }

        _retestRequestRepository.Update(retest);
        _vulnerabilityRepository.Update(vulnerability);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _notificationService.CreateNotificationAsync(
            retest.RequestedByUserId,
            "RETEST_COMPLETE",
            $"Your retest request for vulnerability #{vulnerabilityId} was resolved: {retest.Status}.",
            cancellationToken);

        return ToDto(retest);
    }

    private async Task EnsureVulnerabilityExistsAsync(long vulnerabilityId, CancellationToken cancellationToken)
    {
        _ = await _vulnerabilityRepository.GetByIdAsync(vulnerabilityId, cancellationToken)
            ?? throw new AppException("Vulnerability not found.", 404);
    }

    private static RetestRequestDto ToDto(RetestRequest retest)
    {
        return new RetestRequestDto(
            retest.Id,
            retest.VulnerabilityId,
            retest.RequestedByUserId,
            retest.RequestedByUser?.FullName ?? string.Empty,
            retest.RequestedAt,
            retest.AssignedToUserId,
            retest.AssignedToUser?.FullName,
            retest.Instructions,
            retest.Status.ToString(),
            retest.ResolvedByUserId,
            retest.ResolvedByUser?.FullName,
            retest.ResolvedAt,
            retest.ResolutionNotes,
            retest.Cases.Select(c => new RetestRequestCaseDto(c.Id, c.CaseText, c.IsChecked)).ToList());
    }
}
