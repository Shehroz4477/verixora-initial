using BuildingBlocks.Domain;

namespace Identity.Domain;

public class TrustedDevice : Entity
{
    public Guid UserId { get; private set; }
    public string DeviceId { get; private set; }
    public string DeviceFingerprint { get; private set; }
    public DateTime RegisteredAt { get; private set; }
    public bool IsActive { get; private set; }

    public User User { get; private set; } = null!;

    // EF Core parameterless constructor
    private TrustedDevice()
    {
        DeviceId = null!;
        DeviceFingerprint = null!;
    }

    public TrustedDevice(Guid userId, string deviceId, string deviceFingerprint)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        DeviceId = deviceId ?? throw new ArgumentNullException(nameof(deviceId));
        DeviceFingerprint = deviceFingerprint ?? throw new ArgumentNullException(nameof(deviceFingerprint));
        RegisteredAt = DateTime.UtcNow;
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }
}
