using BuildingBlocks.Domain;

namespace SmartLocks.Domain;

public class SmartLock : Entity, IAggregateRoot
{
    public string Name { get; private set; }
    public Guid DeviceId { get; private set; }   // links to Devices.Domain.Device
    public Guid HomeId { get; private set; }
    public LockStatus Status { get; private set; }
    public bool RequiresFace { get; private set; }
    public DateTime? LastUnlockedAt { get; private set; }
    public Guid? LastUnlockedBy { get; private set; }

    // EF Core parameterless constructor
    private SmartLock()
    {
        Name = null!;
    }

    public SmartLock(string name, Guid deviceId, Guid homeId, bool requiresFace = false) : this()
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Lock name is required.");
        if (deviceId == Guid.Empty || homeId == Guid.Empty)
            throw new DomainException("A controller and home are required.");

        Id = Guid.NewGuid();
        Name = name.Trim();
        DeviceId = deviceId;
        HomeId = homeId;
        RequiresFace = requiresFace;
        Status = LockStatus.Locked;
    }

    public static SmartLock Rehydrate(
        Guid id,
        string name,
        Guid deviceId,
        Guid homeId,
        LockStatus status,
        bool requiresFace,
        DateTime? lastUnlockedAt,
        Guid? lastUnlockedBy)
    {
        return new SmartLock
        {
            Id = id,
            Name = name,
            DeviceId = deviceId,
            HomeId = homeId,
            Status = status,
            RequiresFace = requiresFace,
            LastUnlockedAt = lastUnlockedAt,
            LastUnlockedBy = lastUnlockedBy
        };
    }

    public void Unlock(Guid userId)
    {
        Status = LockStatus.Unlocked;
        LastUnlockedAt = DateTime.UtcNow;
        LastUnlockedBy = userId;
        AddDomainEvent(new DoorUnlockedDomainEvent(Id, userId, DateTime.UtcNow));
    }

    public void Lock(Guid userId)
    {
        Status = LockStatus.Locked;
        AddDomainEvent(new DoorLockedDomainEvent(Id, userId, DateTime.UtcNow));
    }

    public void EmergencyLock()
    {
        Status = LockStatus.EmergencyLocked;
        AddDomainEvent(new EmergencyLockedDomainEvent(Id, DateTime.UtcNow));
    }

    public void SetRequiresFace(bool requiresFace) => RequiresFace = requiresFace;
}
