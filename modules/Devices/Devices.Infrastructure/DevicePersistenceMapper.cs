using Devices.Domain;

namespace Devices.Infrastructure;

internal sealed class PersistedDevice
{
    public Guid Id { get; set; }
    public Guid HomeId { get; set; }
    public string HardwareId { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string MqttTopic { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
    public string? ProvisioningTokenHash { get; set; }
    public DateTime? ProvisioningExpiresAtUtc { get; set; }
    public string? ControllerPublicKeyThumbprint { get; set; }
    public string? HardwareAttestationSubject { get; set; }
    public DateTime? ProvisionedAtUtc { get; set; }

    public Device ToDomain()
    {
        if (!Enum.TryParse<DeviceStatus>(Status, ignoreCase: false, out var status))
            throw new InvalidOperationException($"Stored device status '{Status}' is invalid.");

        return Device.Rehydrate(Id, HomeId, HardwareId, Name, MqttTopic, status, CreatedAtUtc, ProvisioningTokenHash, ProvisioningExpiresAtUtc, ControllerPublicKeyThumbprint, HardwareAttestationSubject, ProvisionedAtUtc);
    }

    public static object ToParameters(Device device) => new
    {
        device.Id,
        device.HomeId,
        device.HardwareId,
        device.Name,
        device.MqttTopic,
        Status = device.Status.ToString(),
        CreatedAtUtc = device.CreatedAt,
        device.ProvisioningTokenHash,
        ProvisioningExpiresAtUtc = device.ProvisioningExpiresAt,
        device.ControllerPublicKeyThumbprint,
        device.HardwareAttestationSubject,
        ProvisionedAtUtc = device.ProvisionedAt
    };
}
