using System.Security.Cryptography;
using BuildingBlocks.Domain;
using FaceVerification.Domain;
using Microsoft.Extensions.Configuration;

namespace SmartLocks.Infrastructure;

public sealed class AesGcmFaceTemplateProtector : IFaceTemplateProtector
{
    private const int KeyLength = 32;
    private const int NonceLength = 12;
    private const int TagLength = 16;
    private readonly byte[] _key;

    public AesGcmFaceTemplateProtector(IConfiguration configuration)
    {
        var encodedKey = configuration["FaceBiometrics:EncryptionKeyBase64"];
        if (string.IsNullOrWhiteSpace(encodedKey))
            throw new InvalidOperationException("FaceBiometrics:EncryptionKeyBase64 must be provided by secret configuration before biometric enrollment is enabled.");

        try
        {
            _key = Convert.FromBase64String(encodedKey);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("FaceBiometrics:EncryptionKeyBase64 must be a Base64-encoded 256-bit key.");
        }

        if (_key.Length != KeyLength)
            throw new InvalidOperationException("FaceBiometrics:EncryptionKeyBase64 must decode to exactly 32 bytes.");
    }

    public EncryptedFaceTemplate Protect(Guid userId, ReadOnlySpan<byte> plaintext)
    {
        if (plaintext.IsEmpty)
            throw new DomainException("Face template cannot be empty.");

        var nonce = RandomNumberGenerator.GetBytes(NonceLength);
        var ciphertextWithTag = new byte[plaintext.Length + TagLength];
        var ciphertext = ciphertextWithTag.AsSpan(0, plaintext.Length);
        var tag = ciphertextWithTag.AsSpan(plaintext.Length, TagLength);

        using var aes = new AesGcm(_key, TagLength);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, userId.ToByteArray());

        return new EncryptedFaceTemplate(Guid.NewGuid(), userId, ciphertextWithTag, nonce, DateTime.UtcNow);
    }

    public byte[] Unprotect(EncryptedFaceTemplate template)
    {
        if (template.Iv.Length != NonceLength || template.Ciphertext.Length <= TagLength)
            throw new DomainException("Stored face template is invalid.");

        var plaintextLength = template.Ciphertext.Length - TagLength;
        var plaintext = new byte[plaintextLength];
        var ciphertext = template.Ciphertext.AsSpan(0, plaintextLength);
        var tag = template.Ciphertext.AsSpan(plaintextLength, TagLength);

        try
        {
            using var aes = new AesGcm(_key, TagLength);
            aes.Decrypt(template.Iv, ciphertext, tag, plaintext, template.UserId.ToByteArray());
            return plaintext;
        }
        catch (CryptographicException)
        {
            throw new DomainException("Stored face template integrity validation failed.");
        }
    }
}
