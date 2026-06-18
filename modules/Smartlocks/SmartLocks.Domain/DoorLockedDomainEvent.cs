using BuildingBlocks.Domain;

namespace SmartLocks.Domain;

public sealed class DoorLockedDomainEvent : IDomainEvent
{
    public Guid LockId { get; }
    public Guid UserId { get; }
    public DateTime OccurredAt { get; }
    public Guid EventId { get; }
    public DateTime OccurredOn { get; }

    public DoorLockedDomainEvent(Guid lockId, Guid userId, DateTime occurredAt)
    {
        LockId = lockId;
        UserId = userId;
        OccurredAt = occurredAt;
        EventId = Guid.NewGuid();
        OccurredOn = DateTime.UtcNow;
    }
}
