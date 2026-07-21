using BuildingBlocks.Domain;
using Homes.Application;
using MediatR;

namespace Devices.Application;

public sealed record GetControllersForHomeQuery(Guid HomeId, Guid UserId, bool IsSystemAdmin)
    : IRequest<IReadOnlyList<ControllerSummary>>;

/// <summary>
/// Safe controller view for an authorised home member. Provisioning token hashes,
/// controller public keys, and hardware-attestation subjects must never leave the backend.
/// </summary>
public sealed record ControllerSummary(
    Guid Id,
    Guid HomeId,
    string Name,
    string HardwareId,
    string MqttTopic,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? ProvisionedAtUtc);

public sealed class GetControllersForHomeQueryHandler(
    IDeviceRepository devices,
    IHomeRepository homes) : IRequestHandler<GetControllersForHomeQuery, IReadOnlyList<ControllerSummary>>
{
    public async Task<IReadOnlyList<ControllerSummary>> Handle(GetControllersForHomeQuery request, CancellationToken cancellationToken)
    {
        if (request.HomeId == Guid.Empty || request.UserId == Guid.Empty)
            throw new DomainException("A home and authenticated user are required.");

        if (!request.IsSystemAdmin)
        {
            var canAccessHome = (await homes.GetForUserAsync(request.UserId, cancellationToken))
                .Any(home => home.Id == request.HomeId);
            if (!canAccessHome)
                throw new DomainException("You do not have access to this home.");
        }

        return (await devices.GetByHomeIdAsync(request.HomeId, cancellationToken))
            .Select(device => new ControllerSummary(
                device.Id,
                device.HomeId,
                device.Name,
                device.HardwareId,
                device.MqttTopic,
                device.Status.ToString(),
                device.CreatedAt,
                device.ProvisionedAt))
            .ToList();
    }
}
