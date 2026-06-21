using AuditLogs.Domain;
using MediatR;

namespace AuditLogs.Application;

public record GetAuditLogsQuery(Guid HomeId) : IRequest<List<AuditLog>>;
