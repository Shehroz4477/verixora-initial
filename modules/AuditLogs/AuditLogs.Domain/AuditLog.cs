using BuildingBlocks.Domain;

namespace AuditLogs.Domain;

public class AuditLog : Entity
{
    public Guid UserId { get; private set; }
    public Guid DeviceId { get; private set; }
    public string Action { get; private set; }
    public DateTime Timestamp { get; private set; }
    public bool Result { get; private set; }
    public string? Details { get; private set; }

    // EF Core parameterless constructor
    private AuditLog()
    {
        Action = null!;
    }

    public AuditLog(Guid userId, Guid deviceId, string action, bool result, string? details = null)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        DeviceId = deviceId;
        Action = action ?? throw new ArgumentNullException(nameof(action));
        Timestamp = DateTime.UtcNow;
        Result = result;
        Details = details;
    }
}
