using SmartLocks.Domain;

namespace SmartLocks.Application;

public interface ISmartLockRepository
{
    Task<SmartLock?> GetByIdAsync(Guid lockId, CancellationToken cancellationToken = default);
    Task AddAsync(SmartLock smartLock, CancellationToken cancellationToken = default);
    Task UpdateAsync(SmartLock smartLock, CancellationToken cancellationToken = default);
}
