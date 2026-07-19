using Microsoft.EntityFrameworkCore;
using SmartLocks.Application;
using SmartLocks.Domain;

namespace SmartLocks.Infrastructure;

public class EfSmartLockRepository : ISmartLockRepository
{
    private readonly SmartLocksDbContext _context;

    public EfSmartLockRepository(SmartLocksDbContext context)
    {
        _context = context;
    }

    public async Task<SmartLock?> GetByIdAsync(Guid lockId, CancellationToken cancellationToken = default)
        => await _context.SmartLocks.FindAsync(new object[] { lockId }, cancellationToken);

    public async Task<List<SmartLock>> GetByHomeIdAsync(Guid homeId, CancellationToken cancellationToken = default)
        => await _context.SmartLocks.Where(item => item.HomeId == homeId).ToListAsync(cancellationToken);

    public async Task AddAsync(SmartLock smartLock, CancellationToken cancellationToken = default)
    {
        await _context.SmartLocks.AddAsync(smartLock, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task UpdateAsync(SmartLock smartLock, CancellationToken cancellationToken = default)
    {
        _context.SmartLocks.Update(smartLock);
        return _context.SaveChangesAsync(cancellationToken);
    }
}
