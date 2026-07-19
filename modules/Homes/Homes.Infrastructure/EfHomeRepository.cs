using Homes.Application;
using Homes.Domain;
using Microsoft.EntityFrameworkCore;

namespace Homes.Infrastructure;

public sealed class EfHomeRepository(HomesDbContext context) : IHomeRepository
{
    public async Task<HomeSummary> AddAsync(Home home, CancellationToken cancellationToken = default)
    {
        await context.Homes.AddAsync(home, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return new HomeSummary(home.Id, home.Name, home.OwnerId, HomeMemberRole.Owner.ToString(), home.MaxDevices, home.CreatedAt);
    }

    public async Task<IReadOnlyList<HomeSummary>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var rows = await context.HomeMembers
            .AsNoTracking()
            .Where(member => member.UserId == userId)
            .Join(
                context.Homes.AsNoTracking(),
                member => member.HomeId,
                home => home.Id,
                (member, home) => new
                {
                    home.Id,
                    home.Name,
                    home.OwnerId,
                    member.Role,
                    home.MaxDevices,
                    CreatedAtUtc = home.CreatedAt
                })
            .OrderBy(home => home.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return rows
            .Select(home => new HomeSummary(
                    home.Id,
                    home.Name,
                    home.OwnerId,
                    home.Role.ToString(),
                    home.MaxDevices,
                    home.CreatedAtUtc))
            .ToList();
    }
}
