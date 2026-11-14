using ShieldReport.Application.ScanAuthorizations.Dtos;
using ShieldReport.Domain.Entities;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.ScanAuthorizations;

public interface IScanAuthorizationService
{
    Task<IReadOnlyList<ScanAuthorizationDto>> ListAsync(long clientAssetId, CancellationToken cancellationToken = default);

    Task<ScanAuthorizationDto> CreateAsync(long clientAssetId, CreateScanAuthorizationRequestDto request, long authorizedByUserId, CancellationToken cancellationToken = default);

    Task<ScanAuthorizationDto> UploadEvidenceAsync(long id, string fileName, string? contentType, Stream content, CancellationToken cancellationToken = default);

    Task<(Stream Content, string FileName, string ContentType)> GetEvidenceAsync(long id, CancellationToken cancellationToken = default);

    Task<ScanAuthorizationDto> RevokeAsync(long id, string reason, long revokedByUserId, CancellationToken cancellationToken = default);

    Task RevokeAllForAssetAsync(long clientAssetId, string reason, long revokedByUserId, CancellationToken cancellationToken = default);

    // Returns the authorization that permits scanning `asset` with every tool in `tools` right
    // now, or throws a 400 AppException saying exactly why not (none / expired / not started /
    // tool not allowed). Messages use client-safe tool labels (clients can trigger scans too).
    Task<ScanAuthorization> EnsureCoveredAsync(ClientAsset asset, IReadOnlyCollection<ScanTool> tools, CancellationToken cancellationToken = default);
}
