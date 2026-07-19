using BuildingBlocks.Domain;

namespace AuditLogs.Domain;

public class AuditLog : Entity
{
    public Guid HomeId { get; private set; }
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

    public AuditLog(Guid homeId, Guid userId, Guid deviceId, string action, bool result, string? details = null)
    {
        if (homeId == Guid.Empty || userId == Guid.Empty || deviceId == Guid.Empty)
            throw new DomainException("Home, user, and device identifiers are required for an audit event.");
        if (string.IsNullOrWhiteSpace(action))
            throw new DomainException("Audit action is required.");

        Id = Guid.NewGuid();
        HomeId = homeId;
        UserId = userId;
        DeviceId = deviceId;
        Action = action ?? throw new ArgumentNullException(nameof(action));
        Timestamp = DateTime.UtcNow;
        Result = result;
        Details = details;
    }

    public static AuditLog Rehydrate(
        Guid id,
        Guid homeId,
        Guid userId,
        Guid deviceId,
        string action,
        DateTime timestamp,
        bool result,
        string? details)
        => new()
        {
            Id = id,
            HomeId = homeId,
            UserId = userId,
            DeviceId = deviceId,
            Action = action,
            Timestamp = timestamp,
            Result = result,
            Details = details
        };
}
