using MediatR;

namespace SmartLocks.Application;

public record RegisterSmartLockCommand(
    string Name,
    Guid DeviceId,
    Guid HomeId,
    bool RequiresFace = false
) : IRequest<RegisterSmartLockResult>;

public record RegisterSmartLockResult(Guid SmartLockId, string Status);
