using Authorization.Domain;

namespace SmartLocks.Infrastructure;

public class ScheduleBasedAuthorizationService : IAuthorizationService
{
    public Task<bool> CanUnlockAsync(Guid userId, Guid lockId, Guid homeId, string role, CancellationToken cancellationToken = default)
        => Task.FromResult(string.Equals(role, "Owner", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(role, "SystemAdmin", StringComparison.OrdinalIgnoreCase));

    public Task<bool> CanLockAsync(Guid userId, Guid lockId, Guid homeId, string role, CancellationToken cancellationToken = default)
        => Task.FromResult(string.Equals(role, "Owner", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(role, "SystemAdmin", StringComparison.OrdinalIgnoreCase));
}
