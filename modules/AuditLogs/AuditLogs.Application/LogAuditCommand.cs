using MediatR;
using AuditLogs.Domain;

namespace AuditLogs.Application;

public record LogAuditCommand(
    Guid HomeId,
    Guid DeviceId,
    Guid UserId,
    string Action,
    bool Result,
    string? Details = null
) : IRequest<AuditLog>;
