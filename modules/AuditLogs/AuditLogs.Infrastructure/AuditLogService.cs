using MediatR;
using AuditLogs.Application;
using SmartLocks.Application;

namespace AuditLogs.Infrastructure;

public class AuditLogService : IAuditLogService
{
    private readonly IMediator _mediator;

    public AuditLogService(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task LogAsync(Guid deviceId, Guid userId, string action, bool result, string? details = null)
    {
        var command = new LogAuditCommand(deviceId, userId, action, result, details);
        await _mediator.Send(command);
    }
}
