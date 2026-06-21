using AuditLogs.Domain;

namespace AuditLogs.Application;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog log, CancellationToken cancellationToken = default);
    Task<List<AuditLog>> GetByHomeIdAsync(Guid homeId, CancellationToken cancellationToken = default);
}
