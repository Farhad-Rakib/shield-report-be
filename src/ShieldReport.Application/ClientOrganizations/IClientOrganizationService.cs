using ShieldReport.Application.ClientOrganizations.Dtos;
using ShieldReport.Application.Common.Models;

namespace ShieldReport.Application.ClientOrganizations;

public interface IClientOrganizationService
{
    Task<PagedResult<ClientOrganizationDto>> ListAsync(PagedRequest request, CancellationToken cancellationToken = default);
    Task<ClientOrganizationDto> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<ClientOrganizationDto> CreateAsync(CreateClientOrganizationRequestDto request, CancellationToken cancellationToken = default);
    Task<ClientOrganizationDto> UpdateAsync(long id, UpdateClientOrganizationRequestDto request, CancellationToken cancellationToken = default);
    Task DeactivateAsync(long id, CancellationToken cancellationToken = default);
}
