using ShieldReport.Domain.Entities;

namespace ShieldReport.Application.Common.Interfaces;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<User?> GetByIdWithRolesAsync(long id, CancellationToken cancellationToken = default);
    Task<bool> HasRoleAsync(long userId, string roleName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> GetByClientOrganizationAndRoleAsync(long clientOrganizationId, string roleName, CancellationToken cancellationToken = default);
}
