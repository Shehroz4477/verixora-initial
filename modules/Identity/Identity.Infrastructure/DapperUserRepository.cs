using System.Data;
using BuildingBlocks.Infrastructure;
using Dapper;
using Identity.Application;
using Identity.Domain;

namespace Identity.Infrastructure;

public sealed class DapperUserRepository(DbConnectionFactory connectionFactory) : IUserRepository
{
    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => (await QuerySingleAsync("identity.sp_GetUserById", "identity.fn_get_user_by_id", "Id", new { Id = id }, cancellationToken))?.ToDomain();

    public async Task<User?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default)
        => (await QuerySingleAsync("identity.sp_GetUserByPhoneNumber", "identity.fn_get_user_by_phone_number", "PhoneNumber", new { PhoneNumber = phoneNumber }, cancellationToken))?.ToDomain();

    public async Task<bool> PhoneNumberExistsAsync(string phoneNumber, CancellationToken cancellationToken = default)
        => await ExistsAsync("identity.sp_PhoneNumberExists", "identity.fn_phone_number_exists", "PhoneNumber", phoneNumber, cancellationToken);

    public async Task<bool> TrustedDeviceIdExistsAsync(string deviceId, CancellationToken cancellationToken = default)
        => await ExistsAsync("identity.sp_TrustedDeviceIdExists", "identity.fn_trusted_device_id_exists", "DeviceId", deviceId, cancellationToken);

    private async Task<bool> ExistsAsync(
        string sqlServerRoutine,
        string postgreSqlRoutine,
        string parameterName,
        string parameterValue,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        var parameters = new DynamicParameters();
        parameters.Add(parameterName, parameterValue);
        return connectionFactory.Provider switch
        {
            "SqlServer" => await connection.QuerySingleAsync<bool>(new CommandDefinition(
                sqlServerRoutine, parameters, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken)),
            "PostgreSql" => await connection.QuerySingleAsync<bool>(new CommandDefinition(
                $"select {postgreSqlRoutine}(@{parameterName})", parameters, cancellationToken: cancellationToken)),
            _ => throw UnsupportedProvider()
        };
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        => (await QuerySingleAsync("identity.sp_GetUserByEmail", "identity.fn_get_user_by_email", "Email", new { Email = email }, cancellationToken))?.ToDomain();

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        var parameters = PersistedUser.ToParameters(user);
        switch (connectionFactory.Provider)
        {
            case "SqlServer":
                await connection.ExecuteAsync(new CommandDefinition("identity.sp_CreateUser", parameters, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));
                return;
            case "PostgreSql":
                await connection.ExecuteAsync(new CommandDefinition(
                    "select identity.fn_create_user(@Id, @PhoneNumber, @PasswordHash, @Email, @EmailVerified, @Role, @CreatedAtUtc, @TrustedDeviceId, @TrustedDeviceDeviceId, @TrustedDeviceFingerprint, @TrustedDevicePublicKeySpkiBase64, @TrustedDevicePublicKeyThumbprint, @TrustedDeviceRegisteredAtUtc, @TrustedDeviceIsActive)",
                    parameters,
                    cancellationToken: cancellationToken));
                return;
            default:
                throw UnsupportedProvider();
        }
    }

    public async Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        var parameters = PersistedUser.ToParameters(user);
        switch (connectionFactory.Provider)
        {
            case "SqlServer":
                await connection.ExecuteAsync(new CommandDefinition("identity.sp_UpdateUser", parameters, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));
                return;
            case "PostgreSql":
                await connection.ExecuteAsync(new CommandDefinition(
                    "select identity.fn_update_user(@Id, @PasswordHash, @Email, @EmailVerified, @Role, @TrustedDeviceId, @TrustedDeviceDeviceId, @TrustedDeviceFingerprint, @TrustedDevicePublicKeySpkiBase64, @TrustedDevicePublicKeyThumbprint, @TrustedDeviceRegisteredAtUtc, @TrustedDeviceIsActive)",
                    parameters,
                    cancellationToken: cancellationToken));
                return;
            default:
                throw UnsupportedProvider();
        }
    }

    private async Task<PersistedUser?> QuerySingleAsync(
        string sqlServerRoutine,
        string postgreSqlRoutine,
        string parameterName,
        object parameters,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        return connectionFactory.Provider switch
        {
            "SqlServer" => await connection.QuerySingleOrDefaultAsync<PersistedUser>(new CommandDefinition(
                sqlServerRoutine, parameters, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken)),
            "PostgreSql" => await connection.QuerySingleOrDefaultAsync<PersistedUser>(new CommandDefinition(
                $"select id as \"Id\", phone_number as \"PhoneNumber\", password_hash as \"PasswordHash\", email as \"Email\", email_verified as \"EmailVerified\", role as \"Role\", created_at_utc as \"CreatedAtUtc\", trusted_device_id as \"TrustedDeviceId\", trusted_device_user_id as \"TrustedDeviceUserId\", trusted_device_device_id as \"TrustedDeviceDeviceId\", trusted_device_fingerprint as \"TrustedDeviceFingerprint\", trusted_device_public_key_spki_base64 as \"TrustedDevicePublicKeySpkiBase64\", trusted_device_public_key_thumbprint as \"TrustedDevicePublicKeyThumbprint\", trusted_device_registered_at_utc as \"TrustedDeviceRegisteredAtUtc\", trusted_device_is_active as \"TrustedDeviceIsActive\" from {postgreSqlRoutine}(@{parameterName})",
                parameters,
                cancellationToken: cancellationToken)),
            _ => throw UnsupportedProvider()
        };
    }

    private NotSupportedException UnsupportedProvider()
        => new($"Identity routines are not available for '{connectionFactory.Provider}'.");
}
