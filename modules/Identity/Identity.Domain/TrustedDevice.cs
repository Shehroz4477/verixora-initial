using BuildingBlocks.Domain;

namespace Identity.Domain;

public class TrustedDevice : Entity
{
    public Guid UserId { get; private set; }
    public string DeviceId { get; private set; }
    public string DeviceFingerprint { get; private set; }
    public string? DevicePublicKeySpkiBase64 { get; private set; }
    public string? DevicePublicKeyThumbprint { get; private set; }
    public DateTime RegisteredAt { get; private set; }
    public bool IsActive { get; private set; }

    public User User { get; private set; } = null!;

    // EF Core parameterless constructor
    private TrustedDevice()
    {
        DeviceId = null!;
        DeviceFingerprint = null!;
    }

    public TrustedDevice(Guid userId, string deviceId, string deviceFingerprint, string? devicePublicKeySpkiBase64 = null)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        DeviceId = deviceId ?? throw new ArgumentNullException(nameof(deviceId));
        DeviceFingerprint = deviceFingerprint ?? throw new ArgumentNullException(nameof(deviceFingerprint));
        if (!string.IsNullOrWhiteSpace(devicePublicKeySpkiBase64))
        {
            DevicePublicKeySpkiBase64 = devicePublicKeySpkiBase64;
            DevicePublicKeyThumbprint = TrustedDevicePublicKey.ValidateAndGetThumbprint(devicePublicKeySpkiBase64);
        }
        RegisteredAt = DateTime.UtcNow;
        IsActive = true;
    }

    public static TrustedDevice Rehydrate(
        Guid id,
        Guid userId,
        string deviceId,
        string deviceFingerprint,
        DateTime registeredAt,
        bool isActive,
        string? devicePublicKeySpkiBase64 = null,
        string? devicePublicKeyThumbprint = null)
    {
        return new TrustedDevice
        {
            Id = id,
            UserId = userId,
            DeviceId = deviceId,
            DeviceFingerprint = deviceFingerprint,
            DevicePublicKeySpkiBase64 = devicePublicKeySpkiBase64,
            DevicePublicKeyThumbprint = devicePublicKeyThumbprint,
            RegisteredAt = registeredAt,
            IsActive = isActive
        };
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void RotatePublicKey(string deviceFingerprint, string devicePublicKeySpkiBase64)
    {
        var thumbprint = TrustedDevicePublicKey.ValidateAndGetThumbprint(devicePublicKeySpkiBase64);
        if (!string.Equals(thumbprint, deviceFingerprint, StringComparison.Ordinal))
            throw new BuildingBlocks.Domain.DomainException("The mobile device fingerprint does not match its public key.");

        DeviceFingerprint = deviceFingerprint;
        DevicePublicKeySpkiBase64 = devicePublicKeySpkiBase64;
        DevicePublicKeyThumbprint = thumbprint;
        RegisteredAt = DateTime.UtcNow;
    }
}
