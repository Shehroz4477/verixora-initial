using Authorization.Domain;

namespace SmartLocks.Infrastructure;

public class MockAuthorizationService : IAuthorizationService
{
    public Task<bool> CanUnlockAsync(Guid userId, Guid lockId, Guid homeId, CancellationToken cancellationToken = default)
    {
        // In a real system, check role, schedule, device restrictions
        // For demo, allow all unlock attempts
        Console.WriteLine($"AUTHZ: Allow unlock – User={userId} Lock={lockId} Home={homeId}");
        return Task.FromResult(true);
    }

    public Task<bool> CanLockAsync(Guid userId, Guid lockId, Guid homeId, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"AUTHZ: Allow lock – User={userId} Lock={lockId} Home={homeId}");
        return Task.FromResult(true);
    }
}
