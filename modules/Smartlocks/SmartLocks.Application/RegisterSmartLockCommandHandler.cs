using BuildingBlocks.Domain;
using Devices.Application;
using Devices.Domain;
using Homes.Application;
using MediatR;
using SmartLocks.Domain;

namespace SmartLocks.Application;

public class RegisterSmartLockCommandHandler : IRequestHandler<RegisterSmartLockCommand, RegisterSmartLockResult>
{
    private readonly ISmartLockRepository _repository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IHomeRepository _homeRepository;

    public RegisterSmartLockCommandHandler(
        ISmartLockRepository repository,
        IDeviceRepository deviceRepository,
        IHomeRepository homeRepository)
    {
        _repository = repository;
        _deviceRepository = deviceRepository;
        _homeRepository = homeRepository;
    }

    public async Task<RegisterSmartLockResult> Handle(RegisterSmartLockCommand request, CancellationToken cancellationToken)
    {
        if (request.RequestedBy == Guid.Empty)
            throw new DomainException("An authenticated user is required.");

        var homes = await _homeRepository.GetForUserAsync(request.RequestedBy, cancellationToken);
        if (!homes.Any(home => home.Id == request.HomeId && string.Equals(home.Role, "Owner", StringComparison.Ordinal)))
            throw new DomainException("Only the home owner can register a door lock.");

        var device = await _deviceRepository.GetByIdAsync(request.DeviceId, cancellationToken);
        if (device is null || device.HomeId != request.HomeId)
            throw new DomainException("The controller does not belong to this home.");
        if (device.Status is not (DeviceStatus.Active or DeviceStatus.Online))
            throw new DomainException("The controller must complete secure provisioning before a lock can be registered.");

        var smartLock = new SmartLock(request.Name, request.DeviceId, request.HomeId, request.RequiresFace);

        await _repository.AddAsync(smartLock, cancellationToken);

        return new RegisterSmartLockResult(smartLock.Id, smartLock.Status.ToString());
    }
}
