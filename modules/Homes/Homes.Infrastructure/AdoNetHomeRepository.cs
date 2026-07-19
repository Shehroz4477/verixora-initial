using System.Data;
using System.Data.Common;
using BuildingBlocks.Infrastructure;
using Homes.Application;
using Homes.Domain;

namespace Homes.Infrastructure;

public sealed class AdoNetHomeRepository(DbConnectionFactory connectionFactory) : IHomeRepository
{
    public async Task<HomeSummary> AddAsync(Home home, CancellationToken cancellationToken = default)
    {
        var owner = home.Members.Single(x => x.UserId == home.OwnerId && x.Role == HomeMemberRole.Owner);
        return await ReadSingleAsync("homes.sp_CreateHome", "select id as \"Id\", name as \"Name\", owner_id as \"OwnerId\", max_devices as \"MaxDevices\", created_at_utc as \"CreatedAtUtc\", 'Owner' as \"Role\" from homes.fn_create_home(@HomeId, @OwnerMemberId, @OwnerId, @Name)", new Dictionary<string, object?> { ["@HomeId"] = home.Id, ["@OwnerMemberId"] = owner.Id, ["@OwnerId"] = home.OwnerId, ["@Name"] = home.Name }, cancellationToken);
    }

    public async Task<IReadOnlyList<HomeSummary>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        using var connection = (DbConnection)connectionFactory.CreateConnection(); await connection.OpenAsync(cancellationToken);
        using var command = connection.CreateCommand(); command.CommandText = connectionFactory.Provider == "SqlServer" ? "homes.sp_GetHomesForUser" : "select id as \"Id\", name as \"Name\", owner_id as \"OwnerId\", role as \"Role\", max_devices as \"MaxDevices\", created_at_utc as \"CreatedAtUtc\" from homes.fn_get_homes_for_user(@UserId)"; command.CommandType = connectionFactory.Provider == "SqlServer" ? CommandType.StoredProcedure : CommandType.Text; Add(command, "@UserId", userId);
        using var reader = await command.ExecuteReaderAsync(cancellationToken); var homes = new List<HomeSummary>(); while (await reader.ReadAsync(cancellationToken)) homes.Add(Map(reader)); return homes;
    }

    private async Task<HomeSummary> ReadSingleAsync(string sqlServerRoutine, string postgresSql, IReadOnlyDictionary<string, object?> values, CancellationToken cancellationToken)
    { using var connection = (DbConnection)connectionFactory.CreateConnection(); await connection.OpenAsync(cancellationToken); using var command = connection.CreateCommand(); command.CommandText = connectionFactory.Provider == "SqlServer" ? sqlServerRoutine : postgresSql; command.CommandType = connectionFactory.Provider == "SqlServer" ? CommandType.StoredProcedure : CommandType.Text; foreach (var value in values) Add(command, value.Key, value.Value); using var reader = await command.ExecuteReaderAsync(cancellationToken); if (!await reader.ReadAsync(cancellationToken)) throw new InvalidOperationException("Database routine returned no home."); return Map(reader); }
    private static void Add(DbCommand command, string name, object? value) { var parameter = command.CreateParameter(); parameter.ParameterName = name; parameter.Value = value ?? DBNull.Value; command.Parameters.Add(parameter); }
    private static HomeSummary Map(DbDataReader reader) => new((Guid)reader["Id"], (string)reader["Name"], (Guid)reader["OwnerId"], reader["Role"].ToString()!, Convert.ToInt32(reader["MaxDevices"]), (DateTime)reader["CreatedAtUtc"]);
}
