using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Domain.Entities;

namespace ShieldReport.Application.Audit;

public sealed class AuditLogger : IAuditLogger
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<AuditLogger> _logger;

    public AuditLogger(IServiceScopeFactory scopeFactory, ICurrentUserService currentUserService, ILogger<AuditLogger> logger)
    {
        _scopeFactory = scopeFactory;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task LogAsync(string action, string entityName, string? entityId = null, object? details = null, long? userId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // Captured from the *request* scope before switching to a fresh one.
            var effectiveUserId = userId ?? _currentUserService.UserId;
            var ipAddress = _currentUserService.IpAddress;

            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IAuditLogRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            await repository.AddAsync(
                new AuditLog(effectiveUserId, entityName, entityId ?? "-", action, details is null ? null : JsonSerializer.Serialize(details), ipAddress),
                CancellationToken.None);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not write audit event {Action} for {Entity} {EntityId}.", action, entityName, entityId);
        }
    }
}
