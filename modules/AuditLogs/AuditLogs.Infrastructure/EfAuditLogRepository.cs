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
    {
        await _context.AuditLogs.AddAsync(log, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<AuditLog>> GetByHomeIdAsync(Guid homeId, CancellationToken cancellationToken = default)
        => await _context.AuditLogs
            .Where(log => log.HomeId == homeId)
            .OrderByDescending(l => l.Timestamp)
            .Take(100)
            .ToListAsync(cancellationToken);
}
