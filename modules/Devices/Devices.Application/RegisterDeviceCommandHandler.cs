using BuildingBlocks.Domain;
using Devices.Domain;
using Homes.Application;
using MediatR;

namespace Devices.Application;

public class RegisterDeviceCommandHandler : IRequestHandler<RegisterDeviceCommand, RegisterDeviceResult>
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly IHomeRepository _homeRepository;
    private readonly IControllerProvisioningTokenService _provisioningTokenService;

    public RegisterDeviceCommandHandler(IDeviceRepository deviceRepository, IHomeRepository homeRepository, IControllerProvisioningTokenService provisioningTokenService)
    {
        _deviceRepository = deviceRepository;
        _homeRepository = homeRepository;
        _provisioningTokenService = provisioningTokenService;
    }

    public async Task<RegisterDeviceResult> Handle(RegisterDeviceCommand request, CancellationToken cancellationToken)
    {
        if (request.RequestedBy == Guid.Empty)
            throw new DomainException("An authenticated user is required.");

        var homes = await _homeRepository.GetForUserAsync(request.RequestedBy, cancellationToken);
        if (!homes.Any(home => home.Id == request.HomeId && string.Equals(home.Role, "Owner", StringComparison.Ordinal)))
            throw new DomainException("Only the home owner can register a controller.");

        if (await _deviceRepository.GetByHardwareIdAsync(request.HardwareId, cancellationToken) is not null)
            throw new DomainException("This controller is already registered.");

        var device = new Device(request.Name, request.HomeId, request.HardwareId);
        var provisioningToken = _provisioningTokenService.Create();
        device.BeginProvisioning(provisioningToken.Hash, provisioningToken.ExpiresAtUtc);

        await _deviceRepository.AddAsync(device, cancellationToken);

        return new RegisterDeviceResult(device.Id, device.MqttTopic, device.Status.ToString(), device.HardwareId, provisioningToken.Value, provisioningToken.ExpiresAtUtc);
    }
}
