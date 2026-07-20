using System.Data;
using BuildingBlocks.Infrastructure;
using Dapper;
using SmartLocks.Application;
using SmartLocks.Domain;

namespace SmartLocks.Infrastructure;

public sealed class DapperLockCommandRepository(DbConnectionFactory connectionFactory) : ILockCommandRepository
{
    public async Task<LockCommand> CreateOrGetAsync(LockCommand command, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        var row = connectionFactory.Provider switch
        {
            "SqlServer" => await connection.QuerySingleAsync<PersistedLockCommand>(new CommandDefinition("smartlocks.sp_CreateOrGetLockCommand", PersistedLockCommand.ToParameters(command), commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken)),
            "PostgreSql" => await connection.QuerySingleAsync<PersistedLockCommand>(new CommandDefinition("select * from smartlocks.fn_create_or_get_lock_command(@Id,@LockId,@DeviceId,@HomeId,@RequestedBy,@IdempotencyKey,@RequestedAtUtc,@ExpiresAtUtc)", PersistedLockCommand.ToParameters(command), cancellationToken: cancellationToken)),
            _ => throw UnsupportedProvider()
        };
        return row.ToDomain();
    }

    public async Task<LockCommand?> GetByIdAsync(Guid commandId, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        var row = connectionFactory.Provider switch
        {
            "SqlServer" => await connection.QuerySingleOrDefaultAsync<PersistedLockCommand>(new CommandDefinition("smartlocks.sp_GetLockCommand", new { Id = commandId }, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken)),
            "PostgreSql" => await connection.QuerySingleOrDefaultAsync<PersistedLockCommand>(new CommandDefinition("select * from smartlocks.fn_get_lock_command(@Id)", new { Id = commandId }, cancellationToken: cancellationToken)),
            _ => throw UnsupportedProvider()
        };
        return row?.ToDomain();
    }

    public async Task<IReadOnlyList<LockCommand>> GetQueuedForDispatchAsync(int maximum, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        var rows = connectionFactory.Provider switch
        {
            "SqlServer" => await connection.QueryAsync<PersistedLockCommand>(new CommandDefinition("smartlocks.sp_GetQueuedLockCommands", new { Maximum = maximum }, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken)),
            "PostgreSql" => await connection.QueryAsync<PersistedLockCommand>(new CommandDefinition("select * from smartlocks.fn_get_queued_lock_commands(@Maximum)", new { Maximum = maximum }, cancellationToken: cancellationToken)),
            _ => throw UnsupportedProvider()
        };
        return rows.Select(row => row.ToDomain()).ToList();
    }

    public async Task<bool> MarkPublishedAsync(Guid commandId, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        return connectionFactory.Provider switch
        {
            "SqlServer" => await connection.QuerySingleAsync<bool>(new CommandDefinition("smartlocks.sp_MarkLockCommandPublished", new { Id = commandId }, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken)),
            "PostgreSql" => await connection.QuerySingleAsync<bool>(new CommandDefinition("select smartlocks.fn_mark_lock_command_published(@Id)", new { Id = commandId }, cancellationToken: cancellationToken)),
            _ => throw UnsupportedProvider()
        };
    }

    public async Task<bool> TryAcknowledgeAsync(Guid commandId, Guid deviceId, string outcome, DateTime occurredAtUtc, string nonce, string details, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        var parameters = new { Id = commandId, DeviceId = deviceId, Outcome = outcome, OccurredAtUtc = occurredAtUtc, Nonce = nonce, Details = details };
        return connectionFactory.Provider switch
        {
            "SqlServer" => await connection.QuerySingleAsync<bool>(new CommandDefinition("smartlocks.sp_AcknowledgeLockCommand", parameters, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken)),
            "PostgreSql" => await connection.QuerySingleAsync<bool>(new CommandDefinition("select smartlocks.fn_acknowledge_lock_command(@Id,@DeviceId,@Outcome,@OccurredAtUtc,@Nonce,@Details)", parameters, cancellationToken: cancellationToken)),
            _ => throw UnsupportedProvider()
        };
    }

    private NotSupportedException UnsupportedProvider() => new($"Lock command routines are not available for '{connectionFactory.Provider}'.");
}
