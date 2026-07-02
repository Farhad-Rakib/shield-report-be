using ShieldReport.Domain.Entities;

namespace ShieldReport.Application.Common.Interfaces;

public interface IClientOrganizationRepository : IRepository<ClientOrganization>
{
    Task<bool> NameExistsAsync(string name, long? excludeId = null, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<ClientOrganization> Items, int Total)> GetPagedAsync(
        int page,
        int pageSize,
        string? search,
        CancellationToken cancellationToken = default);
}
