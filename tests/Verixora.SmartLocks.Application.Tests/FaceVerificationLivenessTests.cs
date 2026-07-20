using System.Net;
using System.Text;
using System.Text.Json;
using FaceVerification.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SmartLocks.Infrastructure;
using Xunit;

namespace Verixora.SmartLocks.Application.Tests;

public sealed class FaceVerificationLivenessTests
{
    [Fact]
    public async Task Passive_face_match_is_rejected_outside_explicit_local_development()
    {
        var userId = Guid.NewGuid();
        var configuration = BuildConfiguration(allowPassiveDevelopmentVerification: true);
        var protector = new AesGcmFaceTemplateProtector(configuration);
        var template = protector.Protect(userId, JsonSerializer.SerializeToUtf8Bytes(new float[128]));
        using var client = new HttpClient(new MatchingFaceHandler()) { BaseAddress = new Uri("http://face-service.test") };
        var provider = new PythonFaceVerificationProvider(client, ThreeTemplates(template), protector, new TestHostEnvironment("Production"), configuration);

        var isVerified = await provider.VerifyAsync(userId, new MemoryStream([1, 2, 3]), TestContext.Current.CancellationToken);

        Assert.False(isVerified);
    }

    [Fact]
    public async Task Passive_face_match_is_allowed_only_when_explicitly_enabled_in_development()
    {
        var userId = Guid.NewGuid();
        var configuration = BuildConfiguration(allowPassiveDevelopmentVerification: true);
        var protector = new AesGcmFaceTemplateProtector(configuration);
        var template = protector.Protect(userId, JsonSerializer.SerializeToUtf8Bytes(new float[128]));
        using var client = new HttpClient(new MatchingFaceHandler()) { BaseAddress = new Uri("http://face-service.test") };
        var provider = new PythonFaceVerificationProvider(client, ThreeTemplates(template), protector, new TestHostEnvironment("Development"), configuration);

        var isVerified = await provider.VerifyAsync(userId, new MemoryStream([1, 2, 3]), TestContext.Current.CancellationToken);

        Assert.True(isVerified);
    }

    private static IConfiguration BuildConfiguration(bool allowPassiveDevelopmentVerification)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FaceBiometrics:EncryptionKeyBase64"] = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)),
                ["FaceBiometrics:AllowPassiveDevelopmentVerification"] = allowPassiveDevelopmentVerification.ToString()
            })
            .Build();

    private static FixedTemplateRepository ThreeTemplates(EncryptedFaceTemplate template)
        => new(template, template with { Id = Guid.NewGuid() }, template with { Id = Guid.NewGuid() });

    private sealed class FixedTemplateRepository(params EncryptedFaceTemplate[] templates) : IFaceTemplateRepository
    {
        public Task<IReadOnlyList<EncryptedFaceTemplate>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<EncryptedFaceTemplate>>(templates);

        public Task ReplaceForUserAsync(Guid userId, IReadOnlyList<EncryptedFaceTemplate> templates, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class MatchingFaceHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"match\":true,\"confidence\":0.99,\"livenessPassed\":false}", Encoding.UTF8, "application/json")
            });
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Verixora.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
