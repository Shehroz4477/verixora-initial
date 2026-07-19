using MediatR;

namespace Devices.Application;

public record RegisterDeviceCommand(Guid HomeId, string Name, string HardwareId, Guid RequestedBy) : IRequest<RegisterDeviceResult>;

public record RegisterDeviceResult(Guid DeviceId, string MqttTopic, string Status, string HardwareId);
