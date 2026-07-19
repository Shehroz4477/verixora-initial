using System.Data;
using System.Data.Common;
using AuditLogs.Application;
using AuditLogs.Domain;
using BuildingBlocks.Infrastructure;

namespace AuditLogs.Infrastructure;

public sealed class AdoNetAuditLogRepository(DbConnectionFactory connectionFactory) : IAuditLogRepository
{
    public async Task AddAsync(AuditLog log, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = connectionFactory.Provider switch
        {
            "SqlServer" => "auditlogs.sp_CreateAuditLog",
            "PostgreSql" => "select auditlogs.fn_create_audit_log(@Id, @HomeId, @UserId, @DeviceId, @Action, @TimestampUtc, @Result, @Details)",
            _ => throw UnsupportedProvider()
        };
        command.CommandType = connectionFactory.Provider == "SqlServer" ? CommandType.StoredProcedure : CommandType.Text;
        foreach (var (name, value) in Parameters(log)) Add(command, name, value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<List<AuditLog>> GetByHomeIdAsync(Guid homeId, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = connectionFactory.Provider switch
        {
            "SqlServer" => "auditlogs.sp_GetAuditLogsByHome",
            "PostgreSql" => "select id as \"Id\", home_id as \"HomeId\", user_id as \"UserId\", device_id as \"DeviceId\", action as \"Action\", timestamp_utc as \"TimestampUtc\", result as \"Result\", details as \"Details\" from auditlogs.fn_get_audit_logs_by_home(@HomeId, @Limit)",
            _ => throw UnsupportedProvider()
        };
        command.CommandType = connectionFactory.Provider == "SqlServer" ? CommandType.StoredProcedure : CommandType.Text;
        Add(command, "HomeId", homeId);
        Add(command, "Limit", 100);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var logs = new List<AuditLog>();
        while (await reader.ReadAsync(cancellationToken)) logs.Add(Map(reader).ToDomain());
        return logs;
    }

    private static IEnumerable<(string Name, object? Value)> Parameters(AuditLog log)
    {
        yield return ("Id", log.Id);
        yield return ("HomeId", log.HomeId);
        yield return ("UserId", log.UserId);
        yield return ("DeviceId", log.DeviceId);
        yield return ("Action", log.Action);
        yield return ("TimestampUtc", log.Timestamp);
        yield return ("Result", log.Result);
        yield return ("Details", log.Details);
    }

    private static PersistedAuditLog Map(DbDataReader reader) => new()
    {
        Id = reader.GetGuid(reader.GetOrdinal("Id")),
        HomeId = reader.GetGuid(reader.GetOrdinal("HomeId")),
        UserId = reader.GetGuid(reader.GetOrdinal("UserId")),
        DeviceId = reader.GetGuid(reader.GetOrdinal("DeviceId")),
        Action = reader.GetString(reader.GetOrdinal("Action")),
        TimestampUtc = reader.GetDateTime(reader.GetOrdinal("TimestampUtc")),
        Result = reader.GetBoolean(reader.GetOrdinal("Result")),
        Details = NullableString(reader, "Details")
    };

    private static string? NullableString(DbDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static void Add(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private NotSupportedException UnsupportedProvider() => new($"Audit routines are not available for '{connectionFactory.Provider}'.");
}
