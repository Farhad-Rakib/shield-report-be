using ShieldReport.Application.ClientOrganizations.Dtos;
using ShieldReport.Application.Common.Exceptions;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Application.Common.Models;
using ShieldReport.Domain.Entities;

namespace ShieldReport.Application.ClientOrganizations;

public sealed class ClientOrganizationService : IClientOrganizationService
{
    private readonly IClientOrganizationRepository _clientOrganizationRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ClientOrganizationService(IClientOrganizationRepository clientOrganizationRepository, IUnitOfWork unitOfWork)
    {
        _clientOrganizationRepository = clientOrganizationRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<ClientOrganizationDto>> ListAsync(PagedRequest request, CancellationToken cancellationToken = default)
    {
        var (items, total) = await _clientOrganizationRepository.GetPagedAsync(request.Page, request.PageSize, request.Search, cancellationToken);
        return PagedResult<ClientOrganizationDto>.Create(items.Select(ToDto).ToList(), total, request.Page, request.PageSize);
    }

    public async Task<ClientOrganizationDto> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var organization = await _clientOrganizationRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppException("Client organization not found.", 404);

        return ToDto(organization);
    }

    public async Task<ClientOrganizationDto> CreateAsync(CreateClientOrganizationRequestDto request, CancellationToken cancellationToken = default)
    {
        if (await _clientOrganizationRepository.NameExistsAsync(request.Name, excludeId: null, cancellationToken))
        {
            throw new AppException("A client organization with this name already exists.", 409);
        }

        var organization = new ClientOrganization(request.Name, request.PrimaryContactName, request.PrimaryContactEmail, request.AllowSelfServiceScanning);
        await _clientOrganizationRepository.AddAsync(organization, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(organization);
    }

    public async Task<ClientOrganizationDto> UpdateAsync(long id, UpdateClientOrganizationRequestDto request, CancellationToken cancellationToken = default)
    {
        var organization = await _clientOrganizationRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppException("Client organization not found.", 404);

        if (await _clientOrganizationRepository.NameExistsAsync(request.Name, excludeId: id, cancellationToken))
        {
            throw new AppException("A client organization with this name already exists.", 409);
        }

        organization.UpdateDetails(request.Name, request.PrimaryContactName, request.PrimaryContactEmail, request.AllowSelfServiceScanning);
        _clientOrganizationRepository.Update(organization);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(organization);
    }

    public async Task DeactivateAsync(long id, CancellationToken cancellationToken = default)
    {
        var organization = await _clientOrganizationRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppException("Client organization not found.", 404);

        organization.Disable();
        _clientOrganizationRepository.Update(organization);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static ClientOrganizationDto ToDto(ClientOrganization organization)
    {
        return new ClientOrganizationDto(
            organization.Id,
            organization.PublicId,
            organization.Name,
            organization.PrimaryContactName,
            organization.PrimaryContactEmail,
            organization.IsActive,
            organization.AllowSelfServiceScanning);
    }
}
