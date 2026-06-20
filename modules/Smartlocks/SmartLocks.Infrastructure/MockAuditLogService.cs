using SmartLocks.Application;

namespace SmartLocks.Infrastructure;

public class MockAuditLogService : IAuditLogService
{
    public Task LogAsync(Guid deviceId, Guid userId, string action, bool result, string? details = null)
    {
        Console.WriteLine($"AUDIT LOG: Device={deviceId} User={userId} Action={action} Result={result} Details={details}");
        return Task.CompletedTask;
    }
}
