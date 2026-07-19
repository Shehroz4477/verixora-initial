using AuditLogs.Domain;
using MediatR;

namespace AuditLogs.Application;

public record GetAuditLogsQuery(Guid HomeId, Guid RequestedBy, bool IsSystemAdmin) : IRequest<List<AuditLog>>;
