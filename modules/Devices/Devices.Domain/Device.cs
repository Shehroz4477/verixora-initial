using BuildingBlocks.Domain;
using System.Security.Cryptography;
using System.Text;

namespace Devices.Domain;

public class Device : Entity, IAggregateRoot
{
    public string Name { get; private set; }
    public Guid HomeId { get; private set; }
    public string HardwareId { get; private set; }
    public string MqttTopic { get; private set; }
    public DeviceStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public string? ProvisioningTokenHash { get; private set; }
    public DateTime? ProvisioningExpiresAt { get; private set; }
    public string? ControllerPublicKeyThumbprint { get; private set; }
    public string? HardwareAttestationSubject { get; private set; }
    public DateTime? ProvisionedAt { get; private set; }

    // EF Core parameterless constructor
    private Device()
    {
        Name = null!;
        HardwareId = null!;
        MqttTopic = null!;
    }

    public Device(string name, Guid homeId, string hardwareId) : this()
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Device name is required.");
        if (string.IsNullOrWhiteSpace(hardwareId))
            throw new DomainException("Hardware ID is required.");

        Id = Guid.NewGuid();
        Name = name.Trim();
        HomeId = homeId;
        HardwareId = hardwareId.Trim().ToUpperInvariant();
        MqttTopic = $"verixora/{Id}";
        Status = DeviceStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    public static Device Rehydrate(
        Guid id,
        Guid homeId,
        string hardwareId,
        string name,
        string mqttTopic,
        DeviceStatus status,
        DateTime createdAt,
        string? provisioningTokenHash,
        DateTime? provisioningExpiresAt,
        string? controllerPublicKeyThumbprint,
        string? hardwareAttestationSubject,
        DateTime? provisionedAt)
    {
        return new Device
        {
            Id = id,
            HomeId = homeId,
            HardwareId = hardwareId,
            Name = name,
            MqttTopic = mqttTopic,
            Status = status,
            CreatedAt = createdAt,
            ProvisioningTokenHash = provisioningTokenHash,
            ProvisioningExpiresAt = provisioningExpiresAt,
            ControllerPublicKeyThumbprint = controllerPublicKeyThumbprint,
            HardwareAttestationSubject = hardwareAttestationSubject,
            ProvisionedAt = provisionedAt
        };
    }

    public void BeginProvisioning(string tokenHash, DateTime expiresAtUtc)
    {
        if (Status != DeviceStatus.Pending || !string.IsNullOrWhiteSpace(ProvisioningTokenHash))
            throw new DomainException("Controller provisioning has already started or completed.");
        if (string.IsNullOrWhiteSpace(tokenHash) || expiresAtUtc <= DateTime.UtcNow)
            throw new DomainException("A valid provisioning token and expiry are required.");

        ProvisioningTokenHash = tokenHash;
        ProvisioningExpiresAt = expiresAtUtc;
    }

    public void CompleteProvisioning(string suppliedTokenHash, string publicKeyThumbprint, string attestationSubject)
    {
        if (Status != DeviceStatus.Pending || string.IsNullOrWhiteSpace(ProvisioningTokenHash) ||
            ProvisioningExpiresAt is null || ProvisioningExpiresAt <= DateTime.UtcNow)
            throw new DomainException("The controller provisioning session is invalid or expired.");
        if (!FixedTimeEquals(ProvisioningTokenHash, suppliedTokenHash))
            throw new DomainException("The controller provisioning token is invalid.");
        if (string.IsNullOrWhiteSpace(publicKeyThumbprint) || string.IsNullOrWhiteSpace(attestationSubject))
            throw new DomainException("A verified controller key and hardware attestation are required.");

        ControllerPublicKeyThumbprint = publicKeyThumbprint;
        HardwareAttestationSubject = attestationSubject;
        ProvisionedAt = DateTime.UtcNow;
        ProvisioningTokenHash = null;
        ProvisioningExpiresAt = null;
        Status = DeviceStatus.Active;
    }

    public void Activate() => Status = DeviceStatus.Active;
    public void Deactivate() => Status = DeviceStatus.Decommissioned;
    public void MarkOnline() => Status = DeviceStatus.Online;
    public void MarkOffline() => Status = DeviceStatus.Offline;

    private static bool FixedTimeEquals(string expected, string actual)
        => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual));
}
