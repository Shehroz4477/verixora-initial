using AuditLogs.Domain;

namespace AuditLogs.Infrastructure;

internal sealed class PersistedAuditLog
{
    public Guid Id { get; set; }
    public Guid HomeId { get; set; }
    public Guid UserId { get; set; }
    public Guid DeviceId { get; set; }
    public string Action { get; set; } = null!;
    public DateTime TimestampUtc { get; set; }
    public bool Result { get; set; }
    public string? Details { get; set; }

    public AuditLog ToDomain() => AuditLog.Rehydrate(Id, HomeId, UserId, DeviceId, Action, TimestampUtc, Result, Details);

    public static object ToParameters(AuditLog log) => new
    {
        log.Id,
        log.HomeId,
        log.UserId,
        log.DeviceId,
        log.Action,
        TimestampUtc = log.Timestamp,
        log.Result,
        log.Details
    };
}
