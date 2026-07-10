using ShieldReport.Application.Common.Exceptions;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Application.Common.Interfaces.Services;
using ShieldReport.Application.Common.Models;
using ShieldReport.Application.Scans.Dtos;
using ShieldReport.Domain.Entities;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.Scans;

public sealed class ScanService : IScanService
{
    private readonly IScanRepository _scanRepository;
    private readonly IScanFindingRawRepository _scanFindingRawRepository;
    private readonly IClientAssetRepository _clientAssetRepository;
    private readonly IEngagementRepository _engagementRepository;
    private readonly IScanQueueService _scanQueueService;
    private readonly IScanCancellationRegistry _scanCancellationRegistry;
    private readonly IScanRealtimeNotifier _realtimeNotifier;
    private readonly IUnitOfWork _unitOfWork;

    public ScanService(
        IScanRepository scanRepository,
        IScanFindingRawRepository scanFindingRawRepository,
        IClientAssetRepository clientAssetRepository,
        IEngagementRepository engagementRepository,
        IScanQueueService scanQueueService,
        IScanCancellationRegistry scanCancellationRegistry,
        IScanRealtimeNotifier realtimeNotifier,
        IUnitOfWork unitOfWork)
    {
        _scanRepository = scanRepository;
        _scanFindingRawRepository = scanFindingRawRepository;
        _clientAssetRepository = clientAssetRepository;
        _engagementRepository = engagementRepository;
        _scanQueueService = scanQueueService;
        _scanCancellationRegistry = scanCancellationRegistry;
        _realtimeNotifier = realtimeNotifier;
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<ScanDto>> ListAsync(PagedRequest request, long? clientOrganizationId, long? clientAssetId, ScanStatus? status, CancellationToken cancellationToken = default)
    {
        var (items, total) = await _scanRepository.GetPagedAsync(request.Page, request.PageSize, clientOrganizationId, clientAssetId, status, cancellationToken);
        return PagedResult<ScanDto>.Create(items.Select(s => ToDto(s)).ToList(), total, request.Page, request.PageSize);
    }

    public async Task<ScanDto> GetByPublicIdAsync(Guid publicId, CancellationToken cancellationToken = default)
    {
        var scan = await _scanRepository.GetByPublicIdAsync(publicId, cancellationToken)
            ?? throw new AppException("Scan not found.", 404);

        return ToDto(scan);
    }

    public async Task<IReadOnlyList<ScanDto>> CreateAsync(long clientAssetId, CreateScanRequestDto request, long requestedByUserId, bool callerIsPentester, bool callerIsClientPortalUser, CancellationToken cancellationToken = default)
    {
        if (request.Tools is null || request.Tools.Length == 0)
        {
            throw new AppException("At least one tool must be selected.", 400);
        }

        var asset = await _clientAssetRepository.GetByIdWithDetailsAsync(clientAssetId, cancellationToken)
            ?? throw new AppException("Client asset not found.", 404);

        if (!asset.IsAuthorizedForScanning)
        {
            throw new AppException("This asset is not authorized for scanning yet.", 400);
        }

        if (callerIsClientPortalUser && !asset.ClientOrganization.AllowSelfServiceScanning)
        {
            throw new AppException("Self-service scanning is disabled for your organization. Contact your pentest team to request a scan.", 403);
        }

        long? engagementTaskId = null;
        if (request.EngagementId.HasValue)
        {
            var engagement = await _engagementRepository.GetByIdWithDetailsAsync(request.EngagementId.Value, cancellationToken)
                ?? throw new AppException("Engagement not found.", 404);

            if (engagement.ClientOrganizationId != asset.ClientOrganizationId)
            {
                throw new AppException("The engagement's client does not match this asset's client.", 400);
            }

            if (callerIsPentester && engagement.LeadPentesterId != requestedByUserId && !engagement.Assignees.Any(a => a.UserId == requestedByUserId))
            {
                throw new AppException("You are not the lead or an assignee of this engagement.", 403);
            }

            var matchingTasks = engagement.Tasks.Where(t => t.Assets.Any(a => a.ClientAssetId == clientAssetId)).ToList();
            engagementTaskId = matchingTasks.Count == 1 ? matchingTasks[0].Id : null;
        }

        var distinctTools = request.Tools.Distinct().ToArray();
        var activeCount = await _scanQueueService.GetActiveCountAsync(asset.ClientOrganizationId, cancellationToken);
        var freeSlots = _scanQueueService.MaxConcurrentScansPerClient - activeCount;

        if (distinctTools.Length > freeSlots)
        {
            throw new AppException(
                $"Cannot start {distinctTools.Length} scan(s) — only {Math.Max(freeSlots, 0)} concurrent scan slot(s) free for this client (cap: {_scanQueueService.MaxConcurrentScansPerClient}).",
                409);
        }

        // Auto-chain: run requested tools one at a time in a fixed Naabu -> Nuclei -> Reconftw
        // order (matches ScanTool's declared enum order) instead of all in parallel — each Scan
        // row links to the next via NextScanId, but only the first is enqueued now.
        // ScanWorkerBackgroundService enqueues NextScanId once a scan reaches a terminal state.
        var orderedTools = distinctTools.OrderBy(tool => (int)tool).ToArray();

        var scans = new List<Scan>();
        foreach (var tool in orderedTools)
        {
            var scan = new Scan(asset.ClientOrganizationId, clientAssetId, tool, requestedByUserId, request.EngagementId, engagementTaskId);
            await _scanRepository.AddAsync(scan, cancellationToken);
            scans.Add(scan);
        }

        // Insert all rows first so they get permanent Ids — SQL Server batches same-table
        // inserts into a single MERGE statement and rejects a self-referencing FK pointing at
        // a sibling row inserted in that same batch. Linking the chain via a second save (an
        // UPDATE against already-committed rows) avoids that conflict.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        for (var i = 0; i < scans.Count - 1; i++)
        {
            scans[i].SetNextScan(scans[i + 1].Id);
            _scanRepository.Update(scans[i]);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _scanQueueService.Enqueue(scans[0].Id, scans[0].Tool);

        return scans
            .Select((scan, i) => ToDto(scan, asset, i < scans.Count - 1 ? scans[i + 1].PublicId : null))
            .ToList();
    }

    public async Task<ScanDto> CancelAsync(Guid publicId, long cancelledByUserId, CancellationToken cancellationToken = default)
    {
        var scan = await _scanRepository.GetByPublicIdAsync(publicId, cancellationToken)
            ?? throw new AppException("Scan not found.", 404);

        var wasRunning = scan.Status == ScanStatus.Running;
        scan.Cancel(cancelledByUserId);
        _scanRepository.Update(scan);

        // Cancelling one tool mid-chain shouldn't leave the rest sitting Queued forever with
        // nothing left to enqueue them — cancel the whole downstream chain along with it.
        var cancelledScanIds = new List<Guid> { scan.PublicId };
        var nextScanId = scan.NextScanId;
        while (nextScanId.HasValue)
        {
            var nextScan = await _scanRepository.GetByIdAsync(nextScanId.Value, cancellationToken);
            if (nextScan is null || nextScan.Status != ScanStatus.Queued)
            {
                break;
            }

            nextScan.Cancel(cancelledByUserId);
            _scanRepository.Update(nextScan);
            cancelledScanIds.Add(nextScan.PublicId);
            nextScanId = nextScan.NextScanId;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Flipping the DB row alone doesn't stop an in-flight `docker run` for this scan — signal
        // whichever background task actually owns it (a no-op if it wasn't Running, e.g. still
        // Queued) so the tool container is genuinely killed instead of running to completion
        // unattended. See DockerScanRunner.RunAsync / ScanCancellationRegistry.
        if (wasRunning)
        {
            _scanCancellationRegistry.TryCancel(scan.Id);
        }

        // The background worker only pushes ScanStatusChanged once it notices a run finished —
        // for a scan that was still Queued (never started), nothing would otherwise tell
        // connected clients it's done. Push it here so the console updates immediately either way.
        foreach (var cancelledScanId in cancelledScanIds)
        {
            await _realtimeNotifier.PushScanStatusAsync(cancelledScanId, ScanStatus.Cancelled.ToString(), cancellationToken);
        }

        return ToDto(scan);
    }

    public async Task<IReadOnlyList<ScanFindingRawDto>> GetRawFindingsAsync(Guid publicId, CancellationToken cancellationToken = default)
    {
        var scan = await _scanRepository.GetByPublicIdAsync(publicId, cancellationToken)
            ?? throw new AppException("Scan not found.", 404);

        var findings = await _scanFindingRawRepository.GetByScanIdAsync(scan.Id, cancellationToken);
        return findings.Select(f => new ScanFindingRawDto(f.Id, f.ScanId, f.RawOutputJson, f.ParsedTitle, f.ParsedEndpoint, f.ParsedSeverityRaw, f.ProcessedAt, f.ResultingVulnerabilityId)).ToList();
    }

    private static ScanDto ToDto(Scan scan, ClientAsset? asset = null, Guid? nextScanPublicIdOverride = null)
    {
        var clientAsset = asset ?? scan.ClientAsset;
        return new ScanDto(
            scan.Id,
            scan.PublicId,
            scan.ClientOrganizationId,
            scan.ClientAssetId,
            clientAsset?.Name ?? string.Empty,
            scan.EngagementId,
            scan.EngagementTaskId,
            scan.Tool.ToString(),
            scan.Status.ToString(),
            scan.QueuedAt,
            scan.StartedAt,
            scan.CompletedAt,
            scan.RequestedByUserId,
            scan.ErrorMessage,
            scan.RawOutput,
            nextScanPublicIdOverride ?? scan.NextScan?.PublicId);
    }
}
