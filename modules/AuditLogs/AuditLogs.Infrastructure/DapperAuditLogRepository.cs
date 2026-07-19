using System.Data;
using AuditLogs.Application;
using AuditLogs.Domain;
using BuildingBlocks.Infrastructure;
using Dapper;

namespace AuditLogs.Infrastructure;

public sealed class DapperAuditLogRepository(DbConnectionFactory connectionFactory) : IAuditLogRepository
{
    public async Task AddAsync(AuditLog log, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        var parameters = PersistedAuditLog.ToParameters(log);
        switch (connectionFactory.Provider)
        {
            case "SqlServer":
                await connection.ExecuteAsync(new CommandDefinition("auditlogs.sp_CreateAuditLog", parameters, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));
                return;
            case "PostgreSql":
                await connection.ExecuteAsync(new CommandDefinition("select auditlogs.fn_create_audit_log(@Id, @HomeId, @UserId, @DeviceId, @Action, @TimestampUtc, @Result, @Details)", parameters, cancellationToken: cancellationToken));
                return;
            default:
                throw UnsupportedProvider();
        }
    }

    public async Task<List<AuditLog>> GetByHomeIdAsync(Guid homeId, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        var rows = connectionFactory.Provider switch
        {
            "SqlServer" => await connection.QueryAsync<PersistedAuditLog>(new CommandDefinition("auditlogs.sp_GetAuditLogsByHome", new { HomeId = homeId, Limit = 100 }, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken)),
            "PostgreSql" => await connection.QueryAsync<PersistedAuditLog>(new CommandDefinition(PostgreSqlSelect(), new { HomeId = homeId, Limit = 100 }, cancellationToken: cancellationToken)),
            _ => throw UnsupportedProvider()
        };
        return rows.Select(row => row.ToDomain()).ToList();
    }

    private static string PostgreSqlSelect()
        => "select id as \"Id\", home_id as \"HomeId\", user_id as \"UserId\", device_id as \"DeviceId\", action as \"Action\", timestamp_utc as \"TimestampUtc\", result as \"Result\", details as \"Details\" from auditlogs.fn_get_audit_logs_by_home(@HomeId, @Limit)";

    private NotSupportedException UnsupportedProvider() => new($"Audit routines are not available for '{connectionFactory.Provider}'.");
}
