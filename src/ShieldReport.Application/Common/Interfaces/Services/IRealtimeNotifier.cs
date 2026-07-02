namespace ShieldReport.Application.Common.Interfaces.Services;

// Abstraction over the SignalR NotificationHub, which lives in the Api layer — Application
// can't reference it directly without an inverted dependency.
public interface IRealtimeNotifier
{
    Task NotifyUserAsync(long userId, object payload, CancellationToken cancellationToken = default);
}
