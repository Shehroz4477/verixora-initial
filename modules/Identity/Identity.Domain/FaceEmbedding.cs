using BuildingBlocks.Domain;

namespace Identity.Domain;

public class FaceEmbedding : Entity
{
    public Guid UserId { get; private set; }
    public byte[] EmbeddingCiphertext { get; private set; }
    public byte[] IV { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public User User { get; private set; } = null!;

    private FaceEmbedding()
    {
        EmbeddingCiphertext = null!;
        IV = null!;
    }

    public FaceEmbedding(Guid userId, byte[] ciphertext, byte[] iv)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        EmbeddingCiphertext = ciphertext ?? throw new ArgumentNullException(nameof(ciphertext));
        IV = iv ?? throw new ArgumentNullException(nameof(iv));
        CreatedAt = DateTime.UtcNow;
    }
}
