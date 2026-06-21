namespace Authorization.Domain;

public interface IAuthorizationService
{
    Task<bool> CanUnlockAsync(Guid userId, Guid lockId, Guid homeId, string role, CancellationToken cancellationToken = default);
    Task<bool> CanLockAsync(Guid userId, Guid lockId, Guid homeId, string role, CancellationToken cancellationToken = default);
}
