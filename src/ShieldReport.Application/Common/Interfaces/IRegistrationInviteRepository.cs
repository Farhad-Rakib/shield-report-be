using ShieldReport.Domain.Entities;

namespace ShieldReport.Application.Common.Interfaces;

public interface IRegistrationInviteRepository : IRepository<RegistrationInvite>
{
    Task<RegistrationInvite?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<RegistrationInvite?> GetByIdWithDetailsAsync(long id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<RegistrationInvite> Items, int Total)> GetPagedAsync(int page, int pageSize, string? search, CancellationToken cancellationToken = default);
}
