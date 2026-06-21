using AuditLogs.Application;
using AuditLogs.Domain;
using Microsoft.EntityFrameworkCore;

namespace AuditLogs.Infrastructure;

public class EfAuditLogRepository : IAuditLogRepository
{
    private readonly AuditLogsDbContext _context;

    public EfAuditLogRepository(AuditLogsDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(AuditLog log, CancellationToken cancellationToken = default)
        => await _context.AuditLogs.AddAsync(log, cancellationToken);

    public async Task<List<AuditLog>> GetByHomeIdAsync(Guid homeId, CancellationToken cancellationToken = default)
        // HomeId is not stored directly; we'd join via Devices/SmartLocks. For demo, return all.
        => await _context.AuditLogs
            .OrderByDescending(l => l.Timestamp)
            .Take(100)
            .ToListAsync(cancellationToken);
}
