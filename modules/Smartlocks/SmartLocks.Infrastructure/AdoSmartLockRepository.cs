using System.Data;
using System.Data.Common;
using BuildingBlocks.Domain;
using BuildingBlocks.Infrastructure;
using SmartLocks.Application;
using SmartLocks.Domain;

namespace SmartLocks.Infrastructure;

public class AdoSmartLockRepository : ISmartLockRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public AdoSmartLockRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<SmartLock?> GetByIdAsync(Guid lockId, CancellationToken cancellationToken = default)
    {
        var conn = (DbConnection)_connectionFactory.CreateConnection();
        await conn.OpenAsync(cancellationToken);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = SqlDialectHelper.GetSelectLockById(_connectionFactory.Provider);
        cmd.Parameters.Add(CreateParameter(cmd, "@Id", lockId));

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        var smartLock = new SmartLock(
            reader["Name"].ToString()!,
            Guid.Parse(reader["DeviceId"].ToString()!),
            Guid.Parse(reader["HomeId"].ToString()!),
            Convert.ToBoolean(reader["RequiresFace"])
        );
        typeof(Entity).GetProperty("Id")?.SetValue(smartLock, Guid.Parse(reader["Id"].ToString()!));
        return smartLock;
    }

    public async Task AddAsync(SmartLock smartLock, CancellationToken cancellationToken = default)
    {
        var conn = (DbConnection)_connectionFactory.CreateConnection();
        await conn.OpenAsync(cancellationToken);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = GetInsertLockSql();
        cmd.Parameters.Add(CreateParameter(cmd, "@Id", smartLock.Id));
        cmd.Parameters.Add(CreateParameter(cmd, "@Name", smartLock.Name));
        cmd.Parameters.Add(CreateParameter(cmd, "@DeviceId", smartLock.DeviceId));
        cmd.Parameters.Add(CreateParameter(cmd, "@HomeId", smartLock.HomeId));
        cmd.Parameters.Add(CreateParameter(cmd, "@Status", smartLock.Status.ToString()));
        cmd.Parameters.Add(CreateParameter(cmd, "@RequiresFace", smartLock.RequiresFace));
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(SmartLock smartLock, CancellationToken cancellationToken = default)
    {
        var conn = (DbConnection)_connectionFactory.CreateConnection();
        await conn.OpenAsync(cancellationToken);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = SqlDialectHelper.GetUnlockProcedure(_connectionFactory.Provider);
        cmd.CommandType = _connectionFactory.Provider == "Sqlite" ? CommandType.Text : CommandType.StoredProcedure;
        cmd.Parameters.Add(CreateParameter(cmd, "@LockId", smartLock.Id));
        cmd.Parameters.Add(CreateParameter(cmd, "@UserId", smartLock.LastUnlockedBy ?? Guid.Empty));
        cmd.Parameters.Add(CreateParameter(cmd, "@UnlockedAt", smartLock.LastUnlockedAt ?? DateTime.UtcNow));
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private string GetInsertLockSql() => _connectionFactory.Provider switch
    {
        "SqlServer" => "INSERT INTO smartlocks.SmartLocks (Id, Name, DeviceId, HomeId, Status, RequiresFace) VALUES (@Id, @Name, @DeviceId, @HomeId, @Status, @RequiresFace)",
        "MySql" => "INSERT INTO smartlocks.SmartLocks (Id, Name, DeviceId, HomeId, Status, RequiresFace) VALUES (@Id, @Name, @DeviceId, @HomeId, @Status, @RequiresFace)",
        "PostgreSql" => @"INSERT INTO smartlocks.""SmartLocks"" (""Id"", ""Name"", ""DeviceId"", ""HomeId"", ""Status"", ""RequiresFace"") VALUES (@Id, @Name, @DeviceId, @HomeId, @Status, @RequiresFace)",
        "Sqlite" => "INSERT INTO SmartLocks (Id, Name, DeviceId, HomeId, Status, RequiresFace) VALUES (@Id, @Name, @DeviceId, @HomeId, @Status, @RequiresFace)",
        _ => throw new NotSupportedException()
    };

    private DbParameter CreateParameter(DbCommand cmd, string name, object value)
    {
        var param = cmd.CreateParameter();
        param.ParameterName = name;
        param.Value = value ?? DBNull.Value;
        return param;
    }
}
