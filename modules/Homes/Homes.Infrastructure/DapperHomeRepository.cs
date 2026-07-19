using System.Data;
using BuildingBlocks.Infrastructure;
using Dapper;
using Homes.Application;
using Homes.Domain;

namespace Homes.Infrastructure;

public sealed class DapperHomeRepository(DbConnectionFactory connectionFactory) : IHomeRepository
{
    public async Task<HomeSummary> AddAsync(Home home, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        var ownerMember = home.Members.Single(m => m.UserId == home.OwnerId && m.Role == HomeMemberRole.Owner);

        return connectionFactory.Provider switch
        {
            "SqlServer" => await connection.QuerySingleAsync<HomeSummary>(new CommandDefinition(
                "homes.sp_CreateHome",
                new { HomeId = home.Id, OwnerMemberId = ownerMember.Id, OwnerId = home.OwnerId, home.Name },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken)),
            "PostgreSql" => await connection.QuerySingleAsync<HomeSummary>(new CommandDefinition(
                "select id as \"Id\", name as \"Name\", owner_id as \"OwnerId\", max_devices as \"MaxDevices\", created_at_utc as \"CreatedAtUtc\", 'Owner' as \"Role\" from homes.fn_create_home(@HomeId, @OwnerMemberId, @OwnerId, @Name)",
                new { HomeId = home.Id, OwnerMemberId = ownerMember.Id, OwnerId = home.OwnerId, home.Name },
                cancellationToken: cancellationToken)),
            _ => throw new NotSupportedException($"Homes routines are not available for '{connectionFactory.Provider}'.")
        };
    }

    public async Task<IReadOnlyList<HomeSummary>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        var results = connectionFactory.Provider switch
        {
            "SqlServer" => await connection.QueryAsync<HomeSummary>(new CommandDefinition(
                "homes.sp_GetHomesForUser", new { UserId = userId }, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken)),
            "PostgreSql" => await connection.QueryAsync<HomeSummary>(new CommandDefinition(
                "select id as \"Id\", name as \"Name\", owner_id as \"OwnerId\", role as \"Role\", max_devices as \"MaxDevices\", created_at_utc as \"CreatedAtUtc\" from homes.fn_get_homes_for_user(@UserId)",
                new { UserId = userId }, cancellationToken: cancellationToken)),
            _ => throw new NotSupportedException($"Homes routines are not available for '{connectionFactory.Provider}'.")
        };
        return results.AsList();
    }
}
