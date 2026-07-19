using Homes.Domain;

namespace Homes.Application;

public interface IHomeRepository
{
    Task<HomeSummary> AddAsync(Home home, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HomeSummary>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}

public sealed record HomeSummary(Guid Id, string Name, Guid OwnerId, string Role, int MaxDevices, DateTime CreatedAtUtc);
