using BuildingBlocks.Domain;
using Microsoft.Extensions.Configuration;
using SmartLocks.Infrastructure;
using Xunit;

namespace Verixora.SmartLocks.Application.Tests;

public sealed class FaceTemplateProtectorTests
{
    [Fact]
    public void Encrypts_templates_with_user_bound_authenticated_encryption()
    {
        var key = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FaceBiometrics:EncryptionKeyBase64"] = key
            })
            .Build();
        var protector = new AesGcmFaceTemplateProtector(configuration);
        var userId = Guid.NewGuid();
        var original = new byte[] { 1, 2, 3, 4 };

        var template = protector.Protect(userId, original);

        Assert.NotEqual(original, template.Ciphertext);
        Assert.Equal(12, template.Iv.Length);
        Assert.Equal(original, protector.Unprotect(template));
        Assert.Throws<DomainException>(() => protector.Unprotect(template with { UserId = Guid.NewGuid() }));
    }
}
