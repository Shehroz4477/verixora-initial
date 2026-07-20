using BuildingBlocks.Domain;

namespace SmartLocks.Domain;

/// <summary>Durable, short-lived instruction for one controller action.</summary>
public sealed class LockCommand : Entity
{
    public Guid LockId { get; private set; }
    public Guid DeviceId { get; private set; }
    public Guid HomeId { get; private set; }
    public Guid RequestedBy { get; private set; }
    public string IdempotencyKey { get; private set; } = null!;
    public string CommandType { get; private set; } = null!;
    public LockCommandStatus Status { get; private set; }
    public DateTime RequestedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? PublishedAtUtc { get; private set; }
    public DateTime? AcknowledgedAtUtc { get; private set; }
    public string? Outcome { get; private set; }
    public string? AcknowledgementNonce { get; private set; }
    public string? Details { get; private set; }

    private LockCommand() { }

    public LockCommand(Guid lockId, Guid deviceId, Guid homeId, Guid requestedBy, string idempotencyKey, DateTime expiresAtUtc)
    {
        if (lockId == Guid.Empty || deviceId == Guid.Empty || homeId == Guid.Empty || requestedBy == Guid.Empty)
            throw new DomainException("A valid lock command context is required.");
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 128)
            throw new DomainException("A valid idempotency key is required.");
        if (expiresAtUtc <= DateTime.UtcNow)
            throw new DomainException("Lock command expiry must be in the future.");

        Id = Guid.NewGuid();
        LockId = lockId;
        DeviceId = deviceId;
        HomeId = homeId;
        RequestedBy = requestedBy;
        IdempotencyKey = idempotencyKey;
        CommandType = "Unlock";
        Status = LockCommandStatus.Queued;
        RequestedAtUtc = DateTime.UtcNow;
        ExpiresAtUtc = expiresAtUtc;
    }

    public static LockCommand Rehydrate(Guid id, Guid lockId, Guid deviceId, Guid homeId, Guid requestedBy, string idempotencyKey, string commandType, LockCommandStatus status, DateTime requestedAtUtc, DateTime expiresAtUtc, DateTime? publishedAtUtc, DateTime? acknowledgedAtUtc, string? outcome, string? acknowledgementNonce, string? details)
        => new()
        {
            Id = id,
            LockId = lockId,
            DeviceId = deviceId,
            HomeId = homeId,
            RequestedBy = requestedBy,
            IdempotencyKey = idempotencyKey,
            CommandType = commandType,
            Status = status,
            RequestedAtUtc = requestedAtUtc,
            ExpiresAtUtc = expiresAtUtc,
            PublishedAtUtc = publishedAtUtc,
            AcknowledgedAtUtc = acknowledgedAtUtc,
            Outcome = outcome,
            AcknowledgementNonce = acknowledgementNonce,
            Details = details
        };

    public bool TryMarkPublished(DateTime nowUtc)
    {
        if (Status != LockCommandStatus.Queued || ExpiresAtUtc <= nowUtc)
            return false;
        Status = LockCommandStatus.Published;
        PublishedAtUtc = nowUtc;
        return true;
    }

    public bool TryAcknowledge(string outcome, DateTime occurredAtUtc, string nonce, string details)
    {
        if (Status is not (LockCommandStatus.Queued or LockCommandStatus.Published) || ExpiresAtUtc < occurredAtUtc || string.IsNullOrWhiteSpace(nonce))
            return false;
        Status = string.Equals(outcome, "Unlocked", StringComparison.Ordinal) ? LockCommandStatus.Acknowledged : LockCommandStatus.Failed;
        AcknowledgedAtUtc = occurredAtUtc;
        Outcome = outcome;
        AcknowledgementNonce = nonce;
        Details = details;
        return true;
    }

    public bool TryExpire(DateTime nowUtc)
    {
        if (Status != LockCommandStatus.Queued || ExpiresAtUtc > nowUtc)
            return false;
        Status = LockCommandStatus.Expired;
        Details = "Command expired before delivery";
        return true;
    }
}
