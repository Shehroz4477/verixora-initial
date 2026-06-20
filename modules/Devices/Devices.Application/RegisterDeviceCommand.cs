using MediatR;

namespace Devices.Application;

public record RegisterDeviceCommand(Guid HomeId, string Name) : IRequest<RegisterDeviceResult>;

public record RegisterDeviceResult(Guid DeviceId, string MqttTopic, string Status);
