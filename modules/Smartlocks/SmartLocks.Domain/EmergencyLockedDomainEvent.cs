using BuildingBlocks.Domain;

namespace SmartLocks.Domain;

public sealed class EmergencyLockedDomainEvent : IDomainEvent
{
    public Guid LockId { get; }
    public DateTime OccurredAt { get; }
    public Guid EventId { get; }
    public DateTime OccurredOn { get; }

    public EmergencyLockedDomainEvent(Guid lockId, DateTime occurredAt)
    {
        LockId = lockId;
        OccurredAt = occurredAt;
        EventId = Guid.NewGuid();
        OccurredOn = DateTime.UtcNow;
    }
}
