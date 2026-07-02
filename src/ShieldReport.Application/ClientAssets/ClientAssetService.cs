using ShieldReport.Application.ClientAssets.Dtos;
using ShieldReport.Application.Common.Exceptions;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Application.Common.Models;
using ShieldReport.Domain.Entities;

namespace ShieldReport.Application.ClientAssets;

public sealed class ClientAssetService : IClientAssetService
{
    private readonly IClientAssetRepository _clientAssetRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ClientAssetService(IClientAssetRepository clientAssetRepository, IUnitOfWork unitOfWork)
    {
        _clientAssetRepository = clientAssetRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<ClientAssetDto>> ListAsync(PagedRequest request, long? clientOrganizationId, CancellationToken cancellationToken = default)
    {
        var (items, total) = await _clientAssetRepository.GetPagedAsync(request.Page, request.PageSize, request.Search, clientOrganizationId, cancellationToken);
        return PagedResult<ClientAssetDto>.Create(items.Select(ToDto).ToList(), total, request.Page, request.PageSize);
    }

    public async Task<ClientAssetDto> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var asset = await _clientAssetRepository.GetByIdWithDetailsAsync(id, cancellationToken)
            ?? throw new AppException("Client asset not found.", 404);

        return ToDto(asset);
    }

    public async Task<ClientAssetDto> CreateAsync(CreateClientAssetRequestDto request, CancellationToken cancellationToken = default)
    {
        var asset = new ClientAsset(request.ClientOrganizationId, request.Name, request.AssetType, request.Identifier, request.Environment, request.Criticality);
        await _clientAssetRepository.AddAsync(asset, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var created = await _clientAssetRepository.GetByIdWithDetailsAsync(asset.Id, cancellationToken)
            ?? throw new AppException("Client asset not found.", 404);

        return ToDto(created);
    }

    public async Task<ClientAssetDto> UpdateAsync(long id, UpdateClientAssetRequestDto request, CancellationToken cancellationToken = default)
    {
        var asset = await _clientAssetRepository.GetByIdWithDetailsAsync(id, cancellationToken)
            ?? throw new AppException("Client asset not found.", 404);

        asset.UpdateDetails(request.Name, request.Identifier, request.Environment, request.Criticality);
        _clientAssetRepository.Update(asset);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(asset);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var asset = await _clientAssetRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppException("Client asset not found.", 404);

        asset.Deactivate();
        _clientAssetRepository.Update(asset);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<ClientAssetDto> AuthorizeForScanningAsync(long id, long authorizedByUserId, CancellationToken cancellationToken = default)
    {
        var asset = await _clientAssetRepository.GetByIdWithDetailsAsync(id, cancellationToken)
            ?? throw new AppException("Client asset not found.", 404);

        asset.AuthorizeForScanning(authorizedByUserId);
        _clientAssetRepository.Update(asset);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(asset);
    }

    public async Task<ClientAssetDto> RevokeScanAuthorizationAsync(long id, CancellationToken cancellationToken = default)
    {
        var asset = await _clientAssetRepository.GetByIdWithDetailsAsync(id, cancellationToken)
            ?? throw new AppException("Client asset not found.", 404);

        asset.RevokeScanAuthorization();
        _clientAssetRepository.Update(asset);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(asset);
    }

    private static ClientAssetDto ToDto(ClientAsset asset)
    {
        return new ClientAssetDto(
            asset.Id,
            asset.PublicId,
            asset.ClientOrganizationId,
            asset.ClientOrganization?.Name ?? string.Empty,
            asset.Name,
            asset.AssetType.ToString(),
            asset.Identifier,
            asset.Environment.ToString(),
            asset.Criticality.ToString(),
            asset.IsAuthorizedForScanning,
            asset.AuthorizedAt,
            asset.AuthorizedByUserId,
            asset.IsActive);
    }
}
