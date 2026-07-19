using BuildingBlocks.Domain;
using Devices.Application;
using Homes.Application;
using MediatR;

namespace SmartLocks.Application;

public sealed record GetSmartLocksForHomeQuery(Guid HomeId, Guid UserId, bool IsSystemAdmin)
    : IRequest<IReadOnlyList<SmartLockSummary>>;

public sealed record SmartLockSummary(
    Guid Id,
    Guid DeviceId,
    Guid HomeId,
    string Name,
    string Status,
    bool RequiresFace,
    string ControllerStatus,
    DateTime? LastUnlockedAtUtc);

public sealed class GetSmartLocksForHomeQueryHandler(
    ISmartLockRepository locks,
    IDeviceRepository devices,
    IHomeRepository homes) : IRequestHandler<GetSmartLocksForHomeQuery, IReadOnlyList<SmartLockSummary>>
{
    public async Task<IReadOnlyList<SmartLockSummary>> Handle(GetSmartLocksForHomeQuery request, CancellationToken cancellationToken)
    {
        if (request.HomeId == Guid.Empty || request.UserId == Guid.Empty)
            throw new DomainException("A home and authenticated user are required.");

        if (!request.IsSystemAdmin)
        {
            var membership = (await homes.GetForUserAsync(request.UserId, cancellationToken))
                .Any(home => home.Id == request.HomeId);
            if (!membership)
                throw new DomainException("You do not have access to this home.");
        }

        var controllerStates = (await devices.GetByHomeIdAsync(request.HomeId, cancellationToken))
            .ToDictionary(device => device.Id, device => device.Status.ToString());

        return (await locks.GetByHomeIdAsync(request.HomeId, cancellationToken))
            .Select(lockItem => new SmartLockSummary(
                lockItem.Id,
                lockItem.DeviceId,
                lockItem.HomeId,
                lockItem.Name,
                lockItem.Status.ToString(),
                lockItem.RequiresFace,
                controllerStates.GetValueOrDefault(lockItem.DeviceId, "Unknown"),
                lockItem.LastUnlockedAt))
            .ToList();
    }
}
