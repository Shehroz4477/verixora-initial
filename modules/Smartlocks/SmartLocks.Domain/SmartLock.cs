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
        Id = Guid.NewGuid();
        Name = name ?? throw new ArgumentNullException(nameof(name));
        DeviceId = deviceId;
        HomeId = homeId;
        RequiresFace = requiresFace;
        Status = LockStatus.Locked;
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
