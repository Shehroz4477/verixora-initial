using System.Data;
using BuildingBlocks.Infrastructure;
using Dapper;
using FaceVerification.Domain;

namespace SmartLocks.Infrastructure;

public sealed class DapperFaceTemplateRepository(DbConnectionFactory connectionFactory) : IFaceTemplateRepository
{
    public async Task<IReadOnlyList<EncryptedFaceTemplate>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        var templates = connectionFactory.Provider switch
        {
            "SqlServer" => await connection.QueryAsync<FaceTemplateRow>(new CommandDefinition(
                "identity.sp_GetFaceEmbeddingsByUser", new { UserId = userId }, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken)),
            "PostgreSql" => await connection.QueryAsync<FaceTemplateRow>(new CommandDefinition(
                "select id as \"Id\", user_id as \"UserId\", embedding_ciphertext as \"Ciphertext\", iv as \"Iv\", created_at_utc as \"CreatedAtUtc\" from identity.fn_get_face_embeddings_by_user(@UserId)",
                new { UserId = userId }, cancellationToken: cancellationToken)),
            _ => throw UnsupportedProvider()
        };

        return templates.Select(row => new EncryptedFaceTemplate(row.Id, row.UserId, row.Ciphertext, row.Iv, row.CreatedAtUtc)).ToArray();
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
            await ExecuteDeleteAsync(connection, transaction, userId, cancellationToken);
            foreach (var template in templates)
                await ExecuteCreateAsync(connection, transaction, template, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private Task ExecuteDeleteAsync(System.Data.Common.DbConnection connection, System.Data.Common.DbTransaction transaction, Guid userId, CancellationToken cancellationToken)
        => connectionFactory.Provider switch
        {
            "SqlServer" => connection.ExecuteAsync(new CommandDefinition("identity.sp_DeleteFaceEmbeddingsForUser", new { UserId = userId }, transaction, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken)),
            "PostgreSql" => connection.ExecuteAsync(new CommandDefinition("select identity.fn_delete_face_embeddings_for_user(@UserId)", new { UserId = userId }, transaction, cancellationToken: cancellationToken)),
            _ => throw UnsupportedProvider()
        };

    private Task ExecuteCreateAsync(System.Data.Common.DbConnection connection, System.Data.Common.DbTransaction transaction, EncryptedFaceTemplate template, CancellationToken cancellationToken)
        => connectionFactory.Provider switch
        {
            "SqlServer" => connection.ExecuteAsync(new CommandDefinition("identity.sp_CreateFaceEmbedding", template, transaction, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken)),
            "PostgreSql" => connection.ExecuteAsync(new CommandDefinition(
                "select identity.fn_create_face_embedding(@Id, @UserId, @Ciphertext, @Iv, @CreatedAtUtc)", template, transaction, cancellationToken: cancellationToken)),
            _ => throw UnsupportedProvider()
        };

    private NotSupportedException UnsupportedProvider()
        => new($"Face-template routines are not available for '{connectionFactory.Provider}'.");

    private sealed class FaceTemplateRow
    {
        public Guid Id { get; init; }
        public Guid UserId { get; init; }
        public byte[] Ciphertext { get; init; } = [];
        public byte[] Iv { get; init; } = [];
        public DateTime CreatedAtUtc { get; init; }
    }
}
