using ShieldReport.Application.ClientAssets.Dtos;
using ShieldReport.Application.Common.Models;

namespace ShieldReport.Application.ClientAssets;

public interface IClientAssetService
{
    Task<PagedResult<ClientAssetDto>> ListAsync(PagedRequest request, long? clientOrganizationId, long? clientAssetId, DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
    Task<ClientAssetDto> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<ClientAssetDto> CreateAsync(CreateClientAssetRequestDto request, CancellationToken cancellationToken = default);
    Task<ClientAssetDto> UpdateAsync(long id, UpdateClientAssetRequestDto request, CancellationToken cancellationToken = default);
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
}
