using BuildingBlocks.Domain;

namespace Devices.Domain;

public class Device : Entity, IAggregateRoot
{
    public string Name { get; private set; }
    public Guid HomeId { get; private set; }
    public string MqttTopic { get; private set; }
    public DeviceStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // EF Core parameterless constructor
    private Device()
    {
        Name = null!;
        MqttTopic = null!;
    }

    public Device(string name, Guid homeId) : this()
    {
        Id = Guid.NewGuid();
        Name = name ?? throw new ArgumentNullException(nameof(name));
        HomeId = homeId;
        MqttTopic = $"verixora/{Id}";
        Status = DeviceStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    public void Activate() => Status = DeviceStatus.Active;
    public void Deactivate() => Status = DeviceStatus.Decommissioned;
    public void MarkOnline() => Status = DeviceStatus.Online;
    public void MarkOffline() => Status = DeviceStatus.Offline;
}
