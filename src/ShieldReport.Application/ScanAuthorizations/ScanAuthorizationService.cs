using ShieldReport.Application.Common.Exceptions;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Application.Common.Interfaces.Services;
using ShieldReport.Application.ScanAuthorizations.Dtos;
using ShieldReport.Domain.Entities;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.ScanAuthorizations;

public sealed class ScanAuthorizationService : IScanAuthorizationService
{
    private static readonly TimeSpan DefaultValidity = TimeSpan.FromDays(30);

    // Evidence is a scope letter / approval email / screenshot — nothing executable.
    private static readonly HashSet<string> AllowedEvidenceExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".pdf", ".png", ".jpg", ".jpeg", ".eml", ".msg", ".txt", ".docx" };

    private readonly IScanAuthorizationRepository _repository;
    private readonly IClientAssetRepository _clientAssetRepository;
    private readonly IEngagementRepository _engagementRepository;
    private readonly IUserRepository _userRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly IUnitOfWork _unitOfWork;

    public ScanAuthorizationService(
        IScanAuthorizationRepository repository,
        IClientAssetRepository clientAssetRepository,
        IEngagementRepository engagementRepository,
        IUserRepository userRepository,
        IFileStorageService fileStorageService,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _clientAssetRepository = clientAssetRepository;
        _engagementRepository = engagementRepository;
        _userRepository = userRepository;
        _fileStorageService = fileStorageService;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<ScanAuthorizationDto>> ListAsync(long clientAssetId, CancellationToken cancellationToken = default)
    {
        _ = await _clientAssetRepository.GetByIdAsync(clientAssetId, cancellationToken)
            ?? throw new AppException("Client asset not found.", 404);

        var authorizations = await _repository.GetByAssetAsync(clientAssetId, cancellationToken);
        var emails = await LoadEmailsAsync(authorizations, cancellationToken);
        return authorizations.Select(a => ToDto(a, emails)).ToList();
    }

    public async Task<ScanAuthorizationDto> CreateAsync(long clientAssetId, CreateScanAuthorizationRequestDto request, long authorizedByUserId, CancellationToken cancellationToken = default)
    {
        var asset = await _clientAssetRepository.GetByIdWithDetailsAsync(clientAssetId, cancellationToken)
            ?? throw new AppException("Client asset not found.", 404);

        DateTime? engagementStart = null, engagementEnd = null;
        if (request.EngagementId.HasValue)
        {
            var engagement = await _engagementRepository.GetByIdAsync(request.EngagementId.Value, cancellationToken)
                ?? throw new AppException("Engagement not found.", 404);
            if (engagement.ClientOrganizationId != asset.ClientOrganizationId)
            {
                throw new AppException("The engagement's client does not match this asset's client.", 400);
            }

            engagementStart = engagement.StartDate;
            // Engagement end dates are calendar days — cover the whole last day.
            engagementEnd = engagement.EndDate?.Date.AddDays(1);
        }

        var now = DateTime.UtcNow;
        var validFrom = ToUtc(request.ValidFrom) ?? (engagementStart.HasValue && engagementStart > now ? engagementStart.Value : now);
        var validUntil = ToUtc(request.ValidUntil) ?? (engagementEnd > validFrom ? engagementEnd : validFrom + DefaultValidity);
        var tools = request.AllowedTools is { Length: > 0 } ? request.AllowedTools : Enum.GetValues<ScanTool>();

        if (validUntil <= now)
        {
            throw new AppException("The authorization would already be expired. Choose an end date in the future.", 400);
        }

        ScanAuthorization authorization;
        try
        {
            authorization = new ScanAuthorization(
                clientAssetId, request.EngagementId, authorizedByUserId, validFrom, validUntil, tools,
                Clean(request.ClientContactName), Clean(request.ClientContactEmail), Clean(request.Notes));
        }
        catch (ArgumentException ex)
        {
            throw new AppException(ex.Message, 400);
        }

        await _repository.AddAsync(authorization, cancellationToken);

        // Keep the legacy flag (shown in asset lists) in sync: an asset has an authorization on file.
        asset.AuthorizeForScanning(authorizedByUserId);
        _clientAssetRepository.Update(asset);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var emails = await LoadEmailsAsync([authorization], cancellationToken);
        return ToDto(authorization, emails);
    }

    public async Task<ScanAuthorizationDto> UploadEvidenceAsync(long id, string fileName, string? contentType, Stream content, CancellationToken cancellationToken = default)
    {
        var authorization = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppException("Scan authorization not found.", 404);

        if (authorization.IsRevoked)
        {
            throw new AppException("Evidence can't be added to a revoked authorization.", 400);
        }

        var safeName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeName) || !AllowedEvidenceExtensions.Contains(Path.GetExtension(safeName)))
        {
            throw new AppException($"Evidence must be one of: {string.Join(", ", AllowedEvidenceExtensions.Order())}.", 400);
        }

        var previousPath = authorization.EvidenceFilePath;
        var storedPath = await _fileStorageService.SaveAsync($"scan-authorizations/{id}", $"{Guid.NewGuid():N}_{safeName}", content, cancellationToken);
        authorization.AttachEvidence(storedPath, safeName, contentType);
        _repository.Update(authorization);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(previousPath))
        {
            _fileStorageService.Delete(previousPath);
        }

        var emails = await LoadEmailsAsync([authorization], cancellationToken);
        return ToDto(authorization, emails);
    }

    public async Task<(Stream Content, string FileName, string ContentType)> GetEvidenceAsync(long id, CancellationToken cancellationToken = default)
    {
        var authorization = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppException("Scan authorization not found.", 404);

        if (string.IsNullOrWhiteSpace(authorization.EvidenceFilePath))
        {
            throw new AppException("No evidence has been uploaded for this authorization.", 404);
        }

        var stream = _fileStorageService.OpenRead(authorization.EvidenceFilePath)
            ?? throw new AppException("The evidence file is missing from storage.", 404);

        return (stream, authorization.EvidenceFileName ?? "evidence", authorization.EvidenceContentType ?? "application/octet-stream");
    }

    public async Task<ScanAuthorizationDto> RevokeAsync(long id, string reason, long revokedByUserId, CancellationToken cancellationToken = default)
    {
        var authorization = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppException("Scan authorization not found.", 404);

        authorization.Revoke(revokedByUserId, reason.Trim());
        _repository.Update(authorization);
        await SyncAssetFlagAsync(authorization.ClientAssetId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var emails = await LoadEmailsAsync([authorization], cancellationToken);
        return ToDto(authorization, emails);
    }

    public async Task RevokeAllForAssetAsync(long clientAssetId, string reason, long revokedByUserId, CancellationToken cancellationToken = default)
    {
        foreach (var authorization in (await _repository.GetByAssetAsync(clientAssetId, cancellationToken)).Where(a => !a.IsRevoked))
        {
            authorization.Revoke(revokedByUserId, reason);
            _repository.Update(authorization);
        }

        await SyncAssetFlagAsync(clientAssetId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<ScanAuthorization> EnsureCoveredAsync(ClientAsset asset, IReadOnlyCollection<ScanTool> tools, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var candidates = (await _repository.GetByAssetAsync(asset.Id, cancellationToken)).Where(a => !a.IsRevoked).ToList();

        if (candidates.Count == 0)
        {
            throw new AppException("This asset has no scan authorization. An admin must record the client's approval before it can be scanned.", 400);
        }

        var active = candidates.Where(a => a.IsActiveAt(now)).ToList();
        if (active.Count == 0)
        {
            var upcoming = candidates.Where(a => a.ValidFrom > now).OrderBy(a => a.ValidFrom).FirstOrDefault();
            if (upcoming is not null)
            {
                throw new AppException($"Scanning this asset is authorized from {upcoming.ValidFrom:yyyy-MM-dd HH:mm} UTC, not yet.", 400);
            }

            var lastEnd = candidates.Max(a => a.ValidUntil);
            throw new AppException($"The scan authorization for this asset expired on {lastEnd:yyyy-MM-dd}. An admin must renew it before scanning.", 400);
        }

        var covering = active
            .Where(a => tools.All(a.Allows))
            .OrderByDescending(a => a.ValidUntil ?? DateTime.MaxValue)
            .FirstOrDefault();
        if (covering is not null)
        {
            return covering;
        }

        var allowedNow = active.SelectMany(a => a.GetAllowedTools()).ToHashSet();
        var missing = tools.Where(t => !allowedNow.Contains(t)).Select(t => t.ToClientLabel());
        throw new AppException($"The current scan authorization doesn't cover: {string.Join(", ", missing)}.", 400);
    }

    // The legacy IsAuthorizedForScanning flag mirrors "has a non-revoked authorization on file".
    private async Task SyncAssetFlagAsync(long clientAssetId, CancellationToken cancellationToken)
    {
        var asset = await _clientAssetRepository.GetByIdAsync(clientAssetId, cancellationToken);
        if (asset is null)
        {
            return;
        }

        var remaining = (await _repository.GetByAssetAsync(clientAssetId, cancellationToken))
            .Any(a => !a.IsRevoked && (a.ValidUntil is null || a.ValidUntil > DateTime.UtcNow));
        if (!remaining && asset.IsAuthorizedForScanning)
        {
            asset.RevokeScanAuthorization();
            _clientAssetRepository.Update(asset);
        }
    }

    private async Task<Dictionary<long, string>> LoadEmailsAsync(IEnumerable<ScanAuthorization> authorizations, CancellationToken cancellationToken)
    {
        var emails = new Dictionary<long, string>();
        foreach (var userId in authorizations.SelectMany(a => new[] { (long?)a.AuthorizedByUserId, a.RevokedByUserId }).Where(id => id.HasValue).Select(id => id!.Value).Distinct())
        {
            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user is not null)
            {
                emails[userId] = user.Email;
            }
        }

        return emails;
    }

    private static ScanAuthorizationDto ToDto(ScanAuthorization a, IReadOnlyDictionary<long, string> emails)
    {
        var now = DateTime.UtcNow;
        var status = a.IsRevoked ? "Revoked" : a.ValidFrom > now ? "Scheduled" : a.IsActiveAt(now) ? "Active" : "Expired";
        return new ScanAuthorizationDto(
            a.Id, a.ClientAssetId, a.EngagementId, status, a.ValidFrom, a.ValidUntil,
            a.GetAllowedTools().Select(t => t.ToString()).ToList(),
            a.ClientContactName, a.ClientContactEmail, a.Notes,
            a.AuthorizedByUserId, emails.GetValueOrDefault(a.AuthorizedByUserId), a.CreatedAt,
            !string.IsNullOrWhiteSpace(a.EvidenceFilePath), a.EvidenceFileName, a.EvidenceUploadedAt,
            a.RevokedAt, a.RevokedByUserId, a.RevokedByUserId.HasValue ? emails.GetValueOrDefault(a.RevokedByUserId.Value) : null, a.RevocationReason);
    }

    private static DateTime? ToUtc(DateTime? value) => value?.Kind switch
    {
        null => null,
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.Value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value!.Value, DateTimeKind.Utc)
    };

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
