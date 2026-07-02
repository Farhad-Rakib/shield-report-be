using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Application.Common.Interfaces.Services;
using ShieldReport.Application.Notifications.Dtos;
using ShieldReport.Domain.Entities;

namespace ShieldReport.Application.Notifications;

public sealed class NotificationService : INotificationService
{
    private readonly INotificationRepository _notificationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRealtimeNotifier _realtimeNotifier;

    public NotificationService(INotificationRepository notificationRepository, IUnitOfWork unitOfWork, IRealtimeNotifier realtimeNotifier)
    {
        _notificationRepository = notificationRepository;
        _unitOfWork = unitOfWork;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<IReadOnlyList<NotificationDto>> GetNotificationsAsync(long userId, CancellationToken cancellationToken = default)
    {
        var notifications = await _notificationRepository.GetByUserIdAsync(userId, cancellationToken);
        return notifications.Select(ToDto).ToList();
    }

    public async Task MarkAsReadAsync(long notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await _notificationRepository.GetByIdAsync(notificationId, cancellationToken);
        if (notification is null)
        {
            return;
        }

        notification.MarkAsRead();
        _notificationRepository.Update(notification);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<NotificationDto> CreateNotificationAsync(long userId, string type, string message, CancellationToken cancellationToken = default)
    {
        var notification = new Notification(userId, type, message);
        await _notificationRepository.AddAsync(notification, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = ToDto(notification);
        await _realtimeNotifier.NotifyUserAsync(userId, dto, cancellationToken);

        return dto;
    }

    public async Task DeleteNotificationAsync(long notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await _notificationRepository.GetByIdAsync(notificationId, cancellationToken);
        if (notification is null)
        {
            return;
        }

        _notificationRepository.Delete(notification);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static NotificationDto ToDto(Notification notification) =>
        new(notification.Id, notification.Type, notification.Message, notification.IsRead, notification.CreatedAt);
}
