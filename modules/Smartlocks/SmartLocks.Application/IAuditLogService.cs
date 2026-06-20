namespace SmartLocks.Application;

public interface IAuditLogService
{
    Task LogAsync(Guid deviceId, Guid userId, string action, bool result, string? details = null);
}
