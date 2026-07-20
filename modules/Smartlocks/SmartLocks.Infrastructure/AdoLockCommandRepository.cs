using System.Data;
using System.Data.Common;
using BuildingBlocks.Infrastructure;
using SmartLocks.Application;
using SmartLocks.Domain;

namespace SmartLocks.Infrastructure;

public sealed class AdoLockCommandRepository(DbConnectionFactory connectionFactory) : ILockCommandRepository
{
    public Task<LockCommand> CreateOrGetAsync(LockCommand command, CancellationToken cancellationToken = default)
        => ReadOneRequiredAsync("smartlocks.sp_CreateOrGetLockCommand", "select * from smartlocks.fn_create_or_get_lock_command(@Id,@LockId,@DeviceId,@HomeId,@RequestedBy,@IdempotencyKey,@RequestedAtUtc,@ExpiresAtUtc)", PersistedLockCommand.ToParameters(command), cancellationToken);

    public async Task<LockCommand?> GetByIdAsync(Guid commandId, CancellationToken cancellationToken = default)
        => await ReadOneAsync("smartlocks.sp_GetLockCommand", "select * from smartlocks.fn_get_lock_command(@Id)", new { Id = commandId }, cancellationToken);

    public async Task<IReadOnlyList<LockCommand>> GetQueuedForDispatchAsync(int maximum, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateCommand(connection, "smartlocks.sp_GetQueuedLockCommands", "select * from smartlocks.fn_get_queued_lock_commands(@Maximum)", new { Maximum = maximum });
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var commands = new List<LockCommand>();
        while (await reader.ReadAsync(cancellationToken)) commands.Add(Map(reader).ToDomain());
        return commands;
    }

    public async Task<bool> MarkPublishedAsync(Guid commandId, CancellationToken cancellationToken = default)
        => await ExecuteBooleanAsync("smartlocks.sp_MarkLockCommandPublished", "select smartlocks.fn_mark_lock_command_published(@Id)", new { Id = commandId }, cancellationToken);

    public async Task<bool> TryAcknowledgeAsync(Guid commandId, Guid deviceId, string outcome, DateTime occurredAtUtc, string nonce, string details, CancellationToken cancellationToken = default)
        => await ExecuteBooleanAsync("smartlocks.sp_AcknowledgeLockCommand", "select smartlocks.fn_acknowledge_lock_command(@Id,@DeviceId,@Outcome,@OccurredAtUtc,@Nonce,@Details)", new { Id = commandId, DeviceId = deviceId, Outcome = outcome, OccurredAtUtc = occurredAtUtc, Nonce = nonce, Details = details }, cancellationToken);

    private async Task<LockCommand> ReadOneRequiredAsync(string sqlServerRoutine, string postgreSqlStatement, object parameters, CancellationToken cancellationToken)
        => await ReadOneAsync(sqlServerRoutine, postgreSqlStatement, parameters, cancellationToken) ?? throw new InvalidOperationException("Lock command routine returned no row.");

    private async Task<LockCommand?> ReadOneAsync(string sqlServerRoutine, string postgreSqlStatement, object parameters, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateCommand(connection, sqlServerRoutine, postgreSqlStatement, parameters);
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Map(reader).ToDomain() : null;
    }

    private async Task<bool> ExecuteBooleanAsync(string sqlServerRoutine, string postgreSqlStatement, object parameters, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateCommand(connection, sqlServerRoutine, postgreSqlStatement, parameters);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is not null && result is not DBNull && Convert.ToBoolean(result);
    }

    private DbCommand CreateCommand(DbConnection connection, string sqlServerRoutine, string postgreSqlStatement, object parameters)
    {
        var command = connection.CreateCommand();
        command.CommandText = connectionFactory.Provider switch
        {
            "SqlServer" => sqlServerRoutine,
            "PostgreSql" => postgreSqlStatement,
            _ => throw UnsupportedProvider()
        };
        command.CommandType = connectionFactory.Provider == "SqlServer" ? CommandType.StoredProcedure : CommandType.Text;
        foreach (var property in parameters.GetType().GetProperties()) Add(command, property.Name, property.GetValue(parameters));
        return command;
    }

    private static PersistedLockCommand Map(DbDataReader reader) => new()
    {
        Id = reader.GetGuid(reader.GetOrdinal("Id")), LockId = reader.GetGuid(reader.GetOrdinal("LockId")), DeviceId = reader.GetGuid(reader.GetOrdinal("DeviceId")), HomeId = reader.GetGuid(reader.GetOrdinal("HomeId")), RequestedBy = reader.GetGuid(reader.GetOrdinal("RequestedBy")),
        IdempotencyKey = reader.GetString(reader.GetOrdinal("IdempotencyKey")), CommandType = reader.GetString(reader.GetOrdinal("CommandType")), Status = reader.GetString(reader.GetOrdinal("Status")), RequestedAtUtc = reader.GetDateTime(reader.GetOrdinal("RequestedAtUtc")), ExpiresAtUtc = reader.GetDateTime(reader.GetOrdinal("ExpiresAtUtc")),
        PublishedAtUtc = NullableDateTime(reader, "PublishedAtUtc"), AcknowledgedAtUtc = NullableDateTime(reader, "AcknowledgedAtUtc"), Outcome = NullableString(reader, "Outcome"), AcknowledgementNonce = NullableString(reader, "AcknowledgementNonce"), Details = NullableString(reader, "Details")
    };

    private static DateTime? NullableDateTime(DbDataReader reader, string name) { var ordinal = reader.GetOrdinal(name); return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal); }
    private static string? NullableString(DbDataReader reader, string name) { var ordinal = reader.GetOrdinal(name); return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal); }
    private static void Add(DbCommand command, string name, object? value) { var parameter = command.CreateParameter(); parameter.ParameterName = name; parameter.Value = value ?? DBNull.Value; command.Parameters.Add(parameter); }
    private NotSupportedException UnsupportedProvider() => new($"Lock command routines are not available for '{connectionFactory.Provider}'.");
}
