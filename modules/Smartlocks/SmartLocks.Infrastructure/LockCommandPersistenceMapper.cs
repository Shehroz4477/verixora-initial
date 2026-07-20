using SmartLocks.Domain;

namespace SmartLocks.Infrastructure;

internal sealed class PersistedLockCommand
{
    public Guid Id { get; set; }
    public Guid LockId { get; set; }
    public Guid DeviceId { get; set; }
    public Guid HomeId { get; set; }
    public Guid RequestedBy { get; set; }
    public string IdempotencyKey { get; set; } = null!;
    public string CommandType { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTime RequestedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public DateTime? AcknowledgedAtUtc { get; set; }
    public string? Outcome { get; set; }
    public string? AcknowledgementNonce { get; set; }
    public string? Details { get; set; }

    public LockCommand ToDomain()
    {
        if (!Enum.TryParse<LockCommandStatus>(Status, false, out var status))
            throw new InvalidOperationException($"Stored lock command status '{Status}' is invalid.");
        return LockCommand.Rehydrate(Id, LockId, DeviceId, HomeId, RequestedBy, IdempotencyKey, CommandType, status, RequestedAtUtc, ExpiresAtUtc, PublishedAtUtc, AcknowledgedAtUtc, Outcome, AcknowledgementNonce, Details);
    }

    public static object ToParameters(LockCommand command) => new
    {
        command.Id,
        command.LockId,
        command.DeviceId,
        command.HomeId,
        command.RequestedBy,
        command.IdempotencyKey,
        command.RequestedAtUtc,
        command.ExpiresAtUtc
    };
}
