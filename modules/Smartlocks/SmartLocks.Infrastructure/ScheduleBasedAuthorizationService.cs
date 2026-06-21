using Authorization.Domain;

namespace SmartLocks.Infrastructure;

public class ScheduleBasedAuthorizationService : IAuthorizationService
{
    public Task<bool> CanUnlockAsync(Guid userId, Guid lockId, Guid homeId, string role, CancellationToken cancellationToken = default)
    {
        if (string.Equals(role, "Owner", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(true);

        // Guest: only weekdays 9-17 UTC (for demo)
        var now = DateTime.UtcNow;
        if (now.DayOfWeek >= DayOfWeek.Monday && now.DayOfWeek <= DayOfWeek.Friday &&
            now.Hour >= 9 && now.Hour < 17)
            return Task.FromResult(true);

        return Task.FromResult(false);
    }

    public Task<bool> CanLockAsync(Guid userId, Guid lockId, Guid homeId, string role, CancellationToken cancellationToken = default)
        => Task.FromResult(true); // lock always allowed
}
