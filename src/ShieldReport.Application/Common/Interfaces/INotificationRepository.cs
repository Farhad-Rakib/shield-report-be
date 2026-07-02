using ShieldReport.Domain.Entities;

namespace ShieldReport.Application.Common.Interfaces;

public interface INotificationRepository : IRepository<Notification>
{
    Task<IReadOnlyList<Notification>> GetByUserIdAsync(long userId, CancellationToken cancellationToken = default);
}
