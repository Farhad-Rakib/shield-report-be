using Microsoft.AspNetCore.SignalR;
using ShieldReport.Api.Hubs;
using ShieldReport.Application.Common.Interfaces.Services;

namespace ShieldReport.Api.Startup;

public sealed class SignalRRealtimeNotifier : IRealtimeNotifier
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public SignalRRealtimeNotifier(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyUserAsync(long userId, object payload, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.User(userId.ToString()).SendAsync("ReceiveNotification", payload, cancellationToken);
    }
}
