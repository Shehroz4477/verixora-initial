using System.Data;
using System.Data.Common;
using BuildingBlocks.Infrastructure;
using Identity.Application;
using Identity.Domain;

namespace Identity.Infrastructure;

public sealed class AdoNetUserRepository(DbConnectionFactory connectionFactory) : IUserRepository
{
    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => (await GetUserAsync("identity.sp_GetUserById", "identity.fn_get_user_by_id", "Id", id, cancellationToken))?.ToDomain();

    public async Task<User?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default)
        => (await GetUserAsync("identity.sp_GetUserByPhoneNumber", "identity.fn_get_user_by_phone_number", "PhoneNumber", phoneNumber, cancellationToken))?.ToDomain();

    public async Task<bool> PhoneNumberExistsAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = connectionFactory.Provider switch
        {
            "SqlServer" => "identity.sp_PhoneNumberExists",
            "PostgreSql" => "select identity.fn_phone_number_exists(@PhoneNumber)",
            _ => throw UnsupportedProvider()
        };
        command.CommandType = connectionFactory.Provider == "SqlServer" ? CommandType.StoredProcedure : CommandType.Text;
        AddParameter(command, "PhoneNumber", phoneNumber);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is not null && value is not DBNull && Convert.ToBoolean(value);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        => (await GetUserAsync("identity.sp_GetUserByEmail", "identity.fn_get_user_by_email", "Email", email, cancellationToken))?.ToDomain();

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
        => await SaveUserAsync("identity.sp_CreateUser", "select identity.fn_create_user(@Id, @PhoneNumber, @PasswordHash, @Email, @EmailVerified, @Role, @CreatedAtUtc, @TrustedDeviceId, @TrustedDeviceDeviceId, @TrustedDeviceFingerprint, @TrustedDeviceRegisteredAtUtc, @TrustedDeviceIsActive)", user, includeImmutableFields: true, cancellationToken);

    public async Task UpdateAsync(User user, CancellationToken cancellationToken = default)
        => await SaveUserAsync("identity.sp_UpdateUser", "select identity.fn_update_user(@Id, @PasswordHash, @Email, @EmailVerified, @Role, @TrustedDeviceId, @TrustedDeviceDeviceId, @TrustedDeviceFingerprint, @TrustedDeviceRegisteredAtUtc, @TrustedDeviceIsActive)", user, includeImmutableFields: false, cancellationToken);

    private async Task<PersistedUser?> GetUserAsync(
        string sqlServerRoutine,
        string postgreSqlRoutine,
        string parameterName,
        object parameterValue,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = connectionFactory.Provider switch
        {
            "SqlServer" => sqlServerRoutine,
            "PostgreSql" => $"select id as \"Id\", phone_number as \"PhoneNumber\", password_hash as \"PasswordHash\", email as \"Email\", email_verified as \"EmailVerified\", role as \"Role\", created_at_utc as \"CreatedAtUtc\", trusted_device_id as \"TrustedDeviceId\", trusted_device_user_id as \"TrustedDeviceUserId\", trusted_device_device_id as \"TrustedDeviceDeviceId\", trusted_device_fingerprint as \"TrustedDeviceFingerprint\", trusted_device_registered_at_utc as \"TrustedDeviceRegisteredAtUtc\", trusted_device_is_active as \"TrustedDeviceIsActive\" from {postgreSqlRoutine}(@{parameterName})",
            _ => throw UnsupportedProvider()
        };
        command.CommandType = connectionFactory.Provider == "SqlServer" ? CommandType.StoredProcedure : CommandType.Text;
        AddParameter(command, parameterName, parameterValue);

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapUser(reader) : null;
    }

    private async Task SaveUserAsync(
        string sqlServerRoutine,
        string postgreSqlStatement,
        User user,
        bool includeImmutableFields,
        CancellationToken cancellationToken)
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
        AddUserParameters(command, user, includeImmutableFields);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static PersistedUser MapUser(DbDataReader reader)
    {
        var deviceIdOrdinal = reader.GetOrdinal("TrustedDeviceId");
        return new PersistedUser
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            PhoneNumber = reader.GetString(reader.GetOrdinal("PhoneNumber")),
            PasswordHash = reader.GetString(reader.GetOrdinal("PasswordHash")),
            Email = GetNullableString(reader, "Email"),
            EmailVerified = reader.GetBoolean(reader.GetOrdinal("EmailVerified")),
            Role = reader.GetString(reader.GetOrdinal("Role")),
            CreatedAtUtc = reader.GetDateTime(reader.GetOrdinal("CreatedAtUtc")),
            TrustedDeviceId = reader.IsDBNull(deviceIdOrdinal) ? null : reader.GetGuid(deviceIdOrdinal),
            TrustedDeviceUserId = GetNullableGuid(reader, "TrustedDeviceUserId"),
            TrustedDeviceDeviceId = GetNullableString(reader, "TrustedDeviceDeviceId"),
            TrustedDeviceFingerprint = GetNullableString(reader, "TrustedDeviceFingerprint"),
            TrustedDeviceRegisteredAtUtc = GetNullableDateTime(reader, "TrustedDeviceRegisteredAtUtc"),
            TrustedDeviceIsActive = GetNullableBoolean(reader, "TrustedDeviceIsActive")
        };
    }

    private static void AddUserParameters(DbCommand command, User user, bool includeImmutableFields)
    {
        AddParameter(command, "Id", user.Id);
        if (includeImmutableFields)
        {
            AddParameter(command, "PhoneNumber", user.PhoneNumber);
            AddParameter(command, "CreatedAtUtc", user.CreatedAt);
        }

        AddParameter(command, "PasswordHash", user.PasswordHash);
        AddParameter(command, "Email", user.Email);
        AddParameter(command, "EmailVerified", user.EmailVerified);
        AddParameter(command, "Role", user.Role.ToString());
        AddParameter(command, "TrustedDeviceId", user.TrustedDevice?.Id);
        AddParameter(command, "TrustedDeviceDeviceId", user.TrustedDevice?.DeviceId);
        AddParameter(command, "TrustedDeviceFingerprint", user.TrustedDevice?.DeviceFingerprint);
        AddParameter(command, "TrustedDeviceRegisteredAtUtc", user.TrustedDevice?.RegisteredAt);
        AddParameter(command, "TrustedDeviceIsActive", user.TrustedDevice?.IsActive);
    }

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static string? GetNullableString(DbDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static Guid? GetNullableGuid(DbDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetGuid(ordinal);
    }

    private static DateTime? GetNullableDateTime(DbDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
    }

    private static bool? GetNullableBoolean(DbDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetBoolean(ordinal);
    }

    private NotSupportedException UnsupportedProvider()
        => new($"Identity routines are not available for '{connectionFactory.Provider}'.");
}
