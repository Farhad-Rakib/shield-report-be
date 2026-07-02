using ShieldReport.Domain.Entities;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace ShieldReport.Application.Menu
{
    public interface IMenuRepository
    {
        Task<List<ShieldReport.Domain.Entities.Menu>> GetRootMenusWithChildrenAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<ShieldReport.Domain.Entities.Menu>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<ShieldReport.Domain.Entities.Menu?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
        Task<ShieldReport.Domain.Entities.Menu> CreateAsync(ShieldReport.Domain.Entities.Menu menu, CancellationToken cancellationToken = default);
        Task UpdateAsync(ShieldReport.Domain.Entities.Menu menu, CancellationToken cancellationToken = default);
        Task DeleteAsync(ShieldReport.Domain.Entities.Menu menu, CancellationToken cancellationToken = default);
    }
}
