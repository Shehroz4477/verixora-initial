using MediatR;

namespace AuditLogs.Application;

public record LogAuditCommand(
    Guid DeviceId,
    Guid UserId,
    string Action,
    bool Result,
    string? Details = null
) : IRequest;
