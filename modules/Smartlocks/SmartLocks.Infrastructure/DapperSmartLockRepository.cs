using System.Data;
using BuildingBlocks.Infrastructure;
using Dapper;
using SmartLocks.Application;
using SmartLocks.Domain;

namespace SmartLocks.Infrastructure;

public sealed class DapperSmartLockRepository(DbConnectionFactory connectionFactory) : ISmartLockRepository
{
    public async Task<SmartLock?> GetByIdAsync(Guid lockId, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        var lockRow = connectionFactory.Provider switch
        {
            "SqlServer" => await connection.QuerySingleOrDefaultAsync<PersistedSmartLock>(new CommandDefinition("smartlocks.sp_GetSmartLockById", new { Id = lockId }, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken)),
            "PostgreSql" => await connection.QuerySingleOrDefaultAsync<PersistedSmartLock>(new CommandDefinition(PostgreSqlSelect("smartlocks.fn_get_smart_lock_by_id"), new { Id = lockId }, cancellationToken: cancellationToken)),
            _ => throw UnsupportedProvider()
        };
        return lockRow?.ToDomain();
    }

    public async Task AddAsync(SmartLock smartLock, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        var parameters = PersistedSmartLock.ToParameters(smartLock);
        switch (connectionFactory.Provider)
        {
            case "SqlServer":
                await connection.ExecuteAsync(new CommandDefinition("smartlocks.sp_CreateSmartLock", parameters, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));
                return;
            case "PostgreSql":
                await connection.ExecuteAsync(new CommandDefinition("select * from smartlocks.fn_create_smart_lock(@Id, @DeviceId, @HomeId, @Name, @Status, @RequiresFace)", parameters, cancellationToken: cancellationToken));
                return;
            default:
                throw UnsupportedProvider();
        }
    }

    public async Task UpdateAsync(SmartLock smartLock, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        var parameters = new
        {
            smartLock.Id,
            Status = smartLock.Status.ToString(),
            LastUnlockedAtUtc = smartLock.LastUnlockedAt,
            smartLock.LastUnlockedBy
        };
        switch (connectionFactory.Provider)
        {
            case "SqlServer":
                await connection.ExecuteAsync(new CommandDefinition("smartlocks.sp_UpdateSmartLockState", parameters, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));
                return;
            case "PostgreSql":
                await connection.ExecuteAsync(new CommandDefinition("select smartlocks.fn_update_smart_lock_state(@Id, @Status, @LastUnlockedAtUtc, @LastUnlockedBy)", parameters, cancellationToken: cancellationToken));
                return;
            default:
                throw UnsupportedProvider();
        }
    }

    private static string PostgreSqlSelect(string routine)
        => $"select id as \"Id\", device_id as \"DeviceId\", home_id as \"HomeId\", name as \"Name\", status as \"Status\", requires_face as \"RequiresFace\", last_unlocked_at_utc as \"LastUnlockedAtUtc\", last_unlocked_by as \"LastUnlockedBy\" from {routine}(@Id)";

    private NotSupportedException UnsupportedProvider() => new($"Smart-lock routines are not available for '{connectionFactory.Provider}'.");
}
