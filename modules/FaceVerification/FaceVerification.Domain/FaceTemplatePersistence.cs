namespace FaceVerification.Domain;

/// <summary>
/// An encrypted biometric template. Raw face images and plaintext embeddings
/// must never be persisted by the API or the recognition service.
/// </summary>
public sealed record EncryptedFaceTemplate(
    Guid Id,
    Guid UserId,
    byte[] Ciphertext,
    byte[] Iv,
    DateTime CreatedAtUtc);

public interface IFaceTemplateRepository
{
    Task<IReadOnlyList<EncryptedFaceTemplate>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task ReplaceForUserAsync(Guid userId, IReadOnlyList<EncryptedFaceTemplate> templates, CancellationToken cancellationToken = default);
    Task DeleteForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}

public interface IFaceTemplateProtector
{
    EncryptedFaceTemplate Protect(Guid userId, ReadOnlySpan<byte> plaintext);
    byte[] Unprotect(EncryptedFaceTemplate template);
}
