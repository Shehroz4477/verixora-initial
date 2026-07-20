using System.Data;
using System.Data.Common;
using BuildingBlocks.Infrastructure;
using FaceVerification.Domain;

namespace SmartLocks.Infrastructure;

public sealed class AdoNetFaceTemplateRepository(DbConnectionFactory connectionFactory) : IFaceTemplateRepository
{
    public async Task<IReadOnlyList<EncryptedFaceTemplate>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        Add(command, "UserId", userId, DbType.Guid);

        if (connectionFactory.Provider == "SqlServer")
        {
            command.CommandText = "identity.sp_GetFaceEmbeddingsByUser";
            command.CommandType = CommandType.StoredProcedure;
        }
        else if (connectionFactory.Provider == "PostgreSql")
        {
            command.CommandText = "select id as \"Id\", user_id as \"UserId\", embedding_ciphertext as \"Ciphertext\", iv as \"Iv\", created_at_utc as \"CreatedAtUtc\" from identity.fn_get_face_embeddings_by_user(@UserId)";
        }
        else
        {
            throw UnsupportedProvider();
        }

        var templates = new List<EncryptedFaceTemplate>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            templates.Add(new EncryptedFaceTemplate(
                reader.GetGuid(reader.GetOrdinal("Id")),
                reader.GetGuid(reader.GetOrdinal("UserId")),
                (byte[])reader.GetValue(reader.GetOrdinal("Ciphertext")),
                (byte[])reader.GetValue(reader.GetOrdinal("Iv")),
                reader.GetDateTime(reader.GetOrdinal("CreatedAtUtc"))));
        }

        return templates;
    }

    public async Task ReplaceForUserAsync(Guid userId, IReadOnlyList<EncryptedFaceTemplate> templates, CancellationToken cancellationToken = default)
    {
        if (templates.Count is < 3 or > 5 || templates.Any(template => template.UserId != userId))
            throw new InvalidOperationException("A face enrollment must contain three to five templates for its user.");

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await DeleteAsync(connection, transaction, userId, cancellationToken);
            foreach (var template in templates)
                await CreateAsync(connection, transaction, template, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task DeleteAsync(DbConnection connection, DbTransaction transaction, Guid userId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        Add(command, "UserId", userId, DbType.Guid);
        if (connectionFactory.Provider == "SqlServer")
        {
            command.CommandText = "identity.sp_DeleteFaceEmbeddingsForUser";
            command.CommandType = CommandType.StoredProcedure;
        }
        else if (connectionFactory.Provider == "PostgreSql")
        {
            command.CommandText = "select identity.fn_delete_face_embeddings_for_user(@UserId)";
        }
        else
        {
            throw UnsupportedProvider();
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task CreateAsync(DbConnection connection, DbTransaction transaction, EncryptedFaceTemplate template, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        Add(command, "Id", template.Id, DbType.Guid);
        Add(command, "UserId", template.UserId, DbType.Guid);
        Add(command, "Ciphertext", template.Ciphertext, DbType.Binary);
        Add(command, "Iv", template.Iv, DbType.Binary);
        Add(command, "CreatedAtUtc", template.CreatedAtUtc, DbType.DateTime2);
        if (connectionFactory.Provider == "SqlServer")
        {
            command.CommandText = "identity.sp_CreateFaceEmbedding";
            command.CommandType = CommandType.StoredProcedure;
        }
        else if (connectionFactory.Provider == "PostgreSql")
        {
            command.CommandText = "select identity.fn_create_face_embedding(@Id, @UserId, @Ciphertext, @Iv, @CreatedAtUtc)";
        }
        else
        {
            throw UnsupportedProvider();
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void Add(DbCommand command, string name, object value, DbType dbType)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = dbType;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private NotSupportedException UnsupportedProvider()
        => new($"Face-template routines are not available for '{connectionFactory.Provider}'.");
}
