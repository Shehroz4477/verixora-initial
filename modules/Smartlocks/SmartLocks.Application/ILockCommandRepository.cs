using SmartLocks.Domain;

namespace SmartLocks.Application;

public interface ILockCommandRepository
{
    Task<LockCommand> CreateOrGetAsync(LockCommand command, CancellationToken cancellationToken = default);
    Task<LockCommand?> GetByIdAsync(Guid commandId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LockCommand>> GetQueuedForDispatchAsync(int maximum, CancellationToken cancellationToken = default);
    Task<bool> MarkPublishedAsync(Guid commandId, CancellationToken cancellationToken = default);
    Task<bool> TryAcknowledgeAsync(Guid commandId, Guid deviceId, string outcome, DateTime occurredAtUtc, string nonce, string details, CancellationToken cancellationToken = default);
}
