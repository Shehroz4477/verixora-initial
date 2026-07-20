using AuditLogs.Application;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using SmartLocks.Application;

namespace ApiHost;

/// <summary>Persists the audit record before broadcasting a read-model notification.</summary>
public sealed class SignalRAuditLogService(IMediator mediator, IHubContext<MonitoringHub> hub) : IAuditLogService
{
    public async Task LogAsync(Guid homeId, Guid deviceId, Guid userId, string action, bool result, string? details = null)
    {
        var entry = await mediator.Send(new LogAuditCommand(homeId, deviceId, userId, action, result, details));
        var notification = new
        {
            entry.Id,
            entry.HomeId,
            entry.DeviceId,
            entry.UserId,
            entry.Action,
            entry.Result,
            entry.Details,
            timestampUtc = entry.Timestamp
        };
        await hub.Clients.Group($"home:{homeId:N}").SendAsync("auditLogged", notification);
        await hub.Clients.Group("system-admin").SendAsync("auditLogged", notification);
    }
}
