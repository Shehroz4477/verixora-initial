using System.Data;
using BuildingBlocks.Infrastructure;
using Dapper;
using Devices.Application;
using Devices.Domain;

namespace Devices.Infrastructure;

public sealed class DapperDeviceRepository(DbConnectionFactory connectionFactory) : IDeviceRepository
{
    public async Task<Device?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => (await QuerySingleAsync("devices.sp_GetDeviceById", "devices.fn_get_device_by_id", "Id", new { Id = id }, cancellationToken))?.ToDomain();

    public async Task<Device?> GetByHardwareIdAsync(string hardwareId, CancellationToken cancellationToken = default)
        => (await QuerySingleAsync("devices.sp_GetDeviceByHardwareId", "devices.fn_get_device_by_hardware_id", "HardwareId", new { HardwareId = hardwareId }, cancellationToken))?.ToDomain();

    public async Task<List<Device>> GetByHomeIdAsync(Guid homeId, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        var rows = connectionFactory.Provider switch
        {
            "SqlServer" => await connection.QueryAsync<PersistedDevice>(new CommandDefinition("devices.sp_GetDevicesForHome", new { HomeId = homeId }, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken)),
            "PostgreSql" => await connection.QueryAsync<PersistedDevice>(new CommandDefinition(PostgreSqlSelect("devices.fn_get_devices_for_home", "HomeId"), new { HomeId = homeId }, cancellationToken: cancellationToken)),
            _ => throw UnsupportedProvider()
        };
        return rows.Select(row => row.ToDomain()).ToList();
    }

    public async Task AddAsync(Device device, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        var parameters = PersistedDevice.ToParameters(device);
        switch (connectionFactory.Provider)
        {
            case "SqlServer":
                await connection.ExecuteAsync(new CommandDefinition("devices.sp_CreateDevice", parameters, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));
                return;
            case "PostgreSql":
                await connection.ExecuteAsync(new CommandDefinition("select * from devices.fn_create_device(@Id, @HomeId, @HardwareId, @Name, @MqttTopic, @Status, @CreatedAtUtc)", parameters, cancellationToken: cancellationToken));
                return;
            default:
                throw UnsupportedProvider();
        }
    }

    public async Task UpdateAsync(Device device, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        var parameters = new { device.Id, Status = device.Status.ToString() };
        switch (connectionFactory.Provider)
        {
            case "SqlServer":
                await connection.ExecuteAsync(new CommandDefinition("devices.sp_UpdateDeviceStatus", parameters, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));
                return;
            case "PostgreSql":
                await connection.ExecuteAsync(new CommandDefinition("select devices.fn_update_device_status(@Id, @Status)", parameters, cancellationToken: cancellationToken));
                return;
            default:
                throw UnsupportedProvider();
        }
    }

    private async Task<PersistedDevice?> QuerySingleAsync(string sqlServerRoutine, string postgreSqlRoutine, string parameterName, object parameters, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        return connectionFactory.Provider switch
        {
            "SqlServer" => await connection.QuerySingleOrDefaultAsync<PersistedDevice>(new CommandDefinition(sqlServerRoutine, parameters, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken)),
            "PostgreSql" => await connection.QuerySingleOrDefaultAsync<PersistedDevice>(new CommandDefinition(PostgreSqlSelect(postgreSqlRoutine, parameterName), parameters, cancellationToken: cancellationToken)),
            _ => throw UnsupportedProvider()
        };
    }

    private static string PostgreSqlSelect(string routine, string parameterName)
        => $"select id as \"Id\", home_id as \"HomeId\", hardware_id as \"HardwareId\", name as \"Name\", mqtt_topic as \"MqttTopic\", status as \"Status\", created_at_utc as \"CreatedAtUtc\" from {routine}(@{parameterName})";

    private NotSupportedException UnsupportedProvider() => new($"Device routines are not available for '{connectionFactory.Provider}'.");
}
