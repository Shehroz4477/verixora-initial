using Devices.Domain;
using MediatR;

namespace Devices.Application;

public class RegisterDeviceCommandHandler : IRequestHandler<RegisterDeviceCommand, RegisterDeviceResult>
{
    private readonly IDeviceRepository _deviceRepository;

    public RegisterDeviceCommandHandler(IDeviceRepository deviceRepository)
    {
        _deviceRepository = deviceRepository;
    }

    public async Task<RegisterDeviceResult> Handle(RegisterDeviceCommand request, CancellationToken cancellationToken)
    {
        var device = new Device(request.Name, request.HomeId);
        device.Activate(); // mark as active immediately for demo

        await _deviceRepository.AddAsync(device, cancellationToken);

        return new RegisterDeviceResult(device.Id, device.MqttTopic, device.Status.ToString());
    }
}
