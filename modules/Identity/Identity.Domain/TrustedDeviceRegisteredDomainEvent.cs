using BuildingBlocks.Domain;

namespace Identity.Domain;

public sealed class TrustedDeviceRegisteredDomainEvent : IDomainEvent
{
    public Guid UserId { get; }
    public string DeviceId { get; }
    public Guid EventId { get; }
    public DateTime OccurredOn { get; }

    public TrustedDeviceRegisteredDomainEvent(Guid userId, string deviceId)
    {
        UserId = userId;
        DeviceId = deviceId;
        EventId = Guid.NewGuid();
        OccurredOn = DateTime.UtcNow;
    }
}
