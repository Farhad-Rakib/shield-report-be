using ShieldReport.Domain.Entities;

namespace ShieldReport.Application.Common.Interfaces;

public interface IClientAssetRepository : IRepository<ClientAsset>
{
    Task<ClientAsset?> GetByIdWithDetailsAsync(long id, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<ClientAsset> Items, int Total)> GetPagedAsync(
        int page,
        int pageSize,
        string? search,
        long? clientOrganizationId,
        long? clientAssetId,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default);
}
