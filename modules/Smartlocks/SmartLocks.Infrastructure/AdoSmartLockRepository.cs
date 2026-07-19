using System.Data;
using System.Data.Common;
using BuildingBlocks.Infrastructure;
using SmartLocks.Application;
using SmartLocks.Domain;

namespace SmartLocks.Infrastructure;

public sealed class AdoSmartLockRepository(DbConnectionFactory connectionFactory) : ISmartLockRepository
{
    public async Task<SmartLock?> GetByIdAsync(Guid lockId, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = connectionFactory.Provider switch
        {
            "SqlServer" => "smartlocks.sp_GetSmartLockById",
            "PostgreSql" => PostgreSqlSelect("smartlocks.fn_get_smart_lock_by_id"),
            _ => throw UnsupportedProvider()
        };
        command.CommandType = connectionFactory.Provider == "SqlServer" ? CommandType.StoredProcedure : CommandType.Text;
        Add(command, "Id", lockId);
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Map(reader).ToDomain() : null;
    }

    public async Task<List<SmartLock>> GetByHomeIdAsync(Guid homeId, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = connectionFactory.Provider switch
        {
            "SqlServer" => "smartlocks.sp_GetSmartLocksForHome",
            "PostgreSql" => PostgreSqlSelectForHome(),
            _ => throw UnsupportedProvider()
        };
        command.CommandType = connectionFactory.Provider == "SqlServer" ? CommandType.StoredProcedure : CommandType.Text;
        Add(command, "HomeId", homeId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var locks = new List<SmartLock>();
        while (await reader.ReadAsync(cancellationToken)) locks.Add(Map(reader).ToDomain());
        return locks;
    }

    public Task AddAsync(SmartLock smartLock, CancellationToken cancellationToken = default)
        => SaveAsync("smartlocks.sp_CreateSmartLock", "select * from smartlocks.fn_create_smart_lock(@Id, @DeviceId, @HomeId, @Name, @Status, @RequiresFace)", smartLock, isUpdate: false, cancellationToken: cancellationToken);

    public Task UpdateAsync(SmartLock smartLock, CancellationToken cancellationToken = default)
        => SaveAsync("smartlocks.sp_UpdateSmartLockState", "select smartlocks.fn_update_smart_lock_state(@Id, @Status, @LastUnlockedAtUtc, @LastUnlockedBy)", smartLock, isUpdate: true, cancellationToken: cancellationToken);

    private async Task SaveAsync(string sqlServerRoutine, string postgreSqlStatement, SmartLock smartLock, bool isUpdate, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = connectionFactory.Provider switch
        {
            "SqlServer" => sqlServerRoutine,
            "PostgreSql" => postgreSqlStatement,
            _ => throw UnsupportedProvider()
        };
        command.CommandType = connectionFactory.Provider == "SqlServer" ? CommandType.StoredProcedure : CommandType.Text;
        Add(command, "Id", smartLock.Id);
        Add(command, "Status", smartLock.Status.ToString());
        if (isUpdate)
        {
            Add(command, "LastUnlockedAtUtc", smartLock.LastUnlockedAt);
            Add(command, "LastUnlockedBy", smartLock.LastUnlockedBy);
        }
        else
        {
            Add(command, "DeviceId", smartLock.DeviceId);
            Add(command, "HomeId", smartLock.HomeId);
            Add(command, "Name", smartLock.Name);
            Add(command, "RequiresFace", smartLock.RequiresFace);
        }
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static PersistedSmartLock Map(DbDataReader reader) => new()
    {
        Id = reader.GetGuid(reader.GetOrdinal("Id")),
        DeviceId = reader.GetGuid(reader.GetOrdinal("DeviceId")),
        HomeId = reader.GetGuid(reader.GetOrdinal("HomeId")),
        Name = reader.GetString(reader.GetOrdinal("Name")),
        Status = reader.GetString(reader.GetOrdinal("Status")),
        RequiresFace = reader.GetBoolean(reader.GetOrdinal("RequiresFace")),
        LastUnlockedAtUtc = NullableDateTime(reader, "LastUnlockedAtUtc"),
        LastUnlockedBy = NullableGuid(reader, "LastUnlockedBy")
    };

    private static string PostgreSqlSelect(string routine)
        => $"select id as \"Id\", device_id as \"DeviceId\", home_id as \"HomeId\", name as \"Name\", status as \"Status\", requires_face as \"RequiresFace\", last_unlocked_at_utc as \"LastUnlockedAtUtc\", last_unlocked_by as \"LastUnlockedBy\" from {routine}(@Id)";

    private static string PostgreSqlSelectForHome()
        => "select id as \"Id\", device_id as \"DeviceId\", home_id as \"HomeId\", name as \"Name\", status as \"Status\", requires_face as \"RequiresFace\", last_unlocked_at_utc as \"LastUnlockedAtUtc\", last_unlocked_by as \"LastUnlockedBy\" from smartlocks.fn_get_smart_locks_for_home(@HomeId)";

    private static DateTime? NullableDateTime(DbDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
    }

    private static Guid? NullableGuid(DbDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetGuid(ordinal);
    }

    private static void Add(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private NotSupportedException UnsupportedProvider() => new($"Smart-lock routines are not available for '{connectionFactory.Provider}'.");
}
