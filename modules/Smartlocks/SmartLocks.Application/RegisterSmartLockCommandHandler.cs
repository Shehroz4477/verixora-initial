using MediatR;
using SmartLocks.Domain;

namespace SmartLocks.Application;

public class RegisterSmartLockCommandHandler : IRequestHandler<RegisterSmartLockCommand, RegisterSmartLockResult>
{
    private readonly ISmartLockRepository _repository;

    public RegisterSmartLockCommandHandler(ISmartLockRepository repository)
    {
        _repository = repository;
    }

    public async Task<RegisterSmartLockResult> Handle(RegisterSmartLockCommand request, CancellationToken cancellationToken)
    {
        var smartLock = new SmartLock(request.Name, request.DeviceId, request.HomeId, request.RequiresFace);

        await _repository.AddAsync(smartLock, cancellationToken);

        return new RegisterSmartLockResult(smartLock.Id, smartLock.Status.ToString());
    }
}
