using System.Security.Cryptography;

namespace Identity.Domain;

public static class TrustedDevicePublicKey
{
    public static string ValidateAndGetThumbprint(string publicKeySpkiBase64)
    {
        if (string.IsNullOrWhiteSpace(publicKeySpkiBase64) || publicKeySpkiBase64.Length > 2048)
            throw new BuildingBlocks.Domain.DomainException("A valid mobile device public key is required.");

        try
        {
            var subjectPublicKeyInfo = Convert.FromBase64String(publicKeySpkiBase64);
            using var key = ECDsa.Create();
            key.ImportSubjectPublicKeyInfo(subjectPublicKeyInfo, out var consumed);
            if (consumed != subjectPublicKeyInfo.Length || key.KeySize != 256)
                throw new BuildingBlocks.Domain.DomainException("The mobile device key must be a P-256 public key.");

            return Base64Url(SHA256.HashData(subjectPublicKeyInfo));
        }
        catch (FormatException)
        {
            throw new BuildingBlocks.Domain.DomainException("The mobile device public key is invalid.");
        }
        catch (CryptographicException)
        {
            throw new BuildingBlocks.Domain.DomainException("The mobile device key must be a valid P-256 public key.");
        }
    }

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
