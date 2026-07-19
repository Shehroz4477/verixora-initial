using System.Data;
using System.Data.Common;
using BuildingBlocks.Infrastructure;
using Devices.Application;
using Devices.Domain;

namespace Devices.Infrastructure;

public sealed class AdoNetDeviceRepository(DbConnectionFactory connectionFactory) : IDeviceRepository
{
    public async Task<Device?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => (await ReadOneAsync("devices.sp_GetDeviceById", "devices.fn_get_device_by_id", "Id", id, cancellationToken))?.ToDomain();

    public async Task<Device?> GetByHardwareIdAsync(string hardwareId, CancellationToken cancellationToken = default)
        => (await ReadOneAsync("devices.sp_GetDeviceByHardwareId", "devices.fn_get_device_by_hardware_id", "HardwareId", hardwareId, cancellationToken))?.ToDomain();

    public async Task<List<Device>> GetByHomeIdAsync(Guid homeId, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = connectionFactory.Provider switch
        {
            "SqlServer" => "devices.sp_GetDevicesForHome",
            "PostgreSql" => PostgreSqlSelect("devices.fn_get_devices_for_home", "HomeId"),
            _ => throw UnsupportedProvider()
        };
        command.CommandType = connectionFactory.Provider == "SqlServer" ? CommandType.StoredProcedure : CommandType.Text;
        Add(command, "HomeId", homeId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var devices = new List<Device>();
        while (await reader.ReadAsync(cancellationToken)) devices.Add(Map(reader).ToDomain());
        return devices;
    }

    public Task AddAsync(Device device, CancellationToken cancellationToken = default)
        => SaveAsync("devices.sp_CreateDevice", "select * from devices.fn_create_device(@Id, @HomeId, @HardwareId, @Name, @MqttTopic, @Status, @CreatedAtUtc)", device, cancellationToken);

    public async Task UpdateAsync(Device device, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = connectionFactory.Provider switch
        {
            "SqlServer" => "devices.sp_UpdateDeviceStatus",
            "PostgreSql" => "select devices.fn_update_device_status(@Id, @Status)",
            _ => throw UnsupportedProvider()
        };
        command.CommandType = connectionFactory.Provider == "SqlServer" ? CommandType.StoredProcedure : CommandType.Text;
        Add(command, "Id", device.Id);
        Add(command, "Status", device.Status.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<PersistedDevice?> ReadOneAsync(string sqlServerRoutine, string postgreSqlRoutine, string parameterName, object value, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = connectionFactory.Provider switch
        {
            "SqlServer" => sqlServerRoutine,
            "PostgreSql" => PostgreSqlSelect(postgreSqlRoutine, parameterName),
            _ => throw UnsupportedProvider()
        };
        command.CommandType = connectionFactory.Provider == "SqlServer" ? CommandType.StoredProcedure : CommandType.Text;
        Add(command, parameterName, value);
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    private async Task SaveAsync(string sqlServerRoutine, string postgreSqlStatement, Device device, CancellationToken cancellationToken)
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
        Add(command, "Id", device.Id);
        Add(command, "HomeId", device.HomeId);
        Add(command, "HardwareId", device.HardwareId);
        Add(command, "Name", device.Name);
        Add(command, "MqttTopic", device.MqttTopic);
        Add(command, "Status", device.Status.ToString());
        Add(command, "CreatedAtUtc", device.CreatedAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static PersistedDevice Map(DbDataReader reader) => new()
    {
        Id = reader.GetGuid(reader.GetOrdinal("Id")),
        HomeId = reader.GetGuid(reader.GetOrdinal("HomeId")),
        HardwareId = reader.GetString(reader.GetOrdinal("HardwareId")),
        Name = reader.GetString(reader.GetOrdinal("Name")),
        MqttTopic = reader.GetString(reader.GetOrdinal("MqttTopic")),
        Status = reader.GetString(reader.GetOrdinal("Status")),
        CreatedAtUtc = reader.GetDateTime(reader.GetOrdinal("CreatedAtUtc"))
    };

    private static string PostgreSqlSelect(string routine, string parameterName)
        => $"select id as \"Id\", home_id as \"HomeId\", hardware_id as \"HardwareId\", name as \"Name\", mqtt_topic as \"MqttTopic\", status as \"Status\", created_at_utc as \"CreatedAtUtc\" from {routine}(@{parameterName})";

    private static void Add(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private NotSupportedException UnsupportedProvider() => new($"Device routines are not available for '{connectionFactory.Provider}'.");
}
