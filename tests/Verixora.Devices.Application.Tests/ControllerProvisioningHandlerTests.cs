using System.Security.Cryptography;
using BuildingBlocks.Domain;
using Devices.Application;
using Devices.Domain;
using Xunit;

namespace Verixora.Devices.Application.Tests;

public sealed class ControllerProvisioningHandlerTests
{
    [Fact]
    public async Task Verified_p256_controller_consumes_pairing_token_once()
    {
        var device = new Device("Front Door ESP32", Guid.NewGuid(), "ESP32-PAIR-1");
        device.BeginProvisioning("hash:pairing-token", DateTime.UtcNow.AddMinutes(5));
        var repository = new InMemoryDeviceRepository(device) { CompletionResult = true };
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var handler = new CompleteControllerProvisioningCommandHandler(repository, new TestTokens(), new TestAttestation(true));

        var result = await handler.Handle(new CompleteControllerProvisioningCommand(device.Id, "pairing-token", key.ExportSubjectPublicKeyInfoPem(), "test-attestation"), TestContext.Current.CancellationToken);

        Assert.Equal("Active", result.Status);
        Assert.NotEmpty(result.ControllerPublicKeyThumbprint);
        Assert.Equal("hash:pairing-token", repository.ReceivedHash);
        Assert.Equal("local-test:ESP32-PAIR-1", repository.ReceivedSubject);
    }

    [Fact]
    public async Task Invalid_hardware_attestation_fails_before_consuming_pairing_token()
    {
        var device = new Device("Front Door ESP32", Guid.NewGuid(), "ESP32-PAIR-2");
        device.BeginProvisioning("hash:pairing-token", DateTime.UtcNow.AddMinutes(5));
        var repository = new InMemoryDeviceRepository(device);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var handler = new CompleteControllerProvisioningCommandHandler(repository, new TestTokens(), new TestAttestation(false));

        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(new CompleteControllerProvisioningCommand(device.Id, "pairing-token", key.ExportSubjectPublicKeyInfoPem(), "bad"), TestContext.Current.CancellationToken));
        Assert.Null(repository.ReceivedHash);
    }

    private sealed class TestTokens : IControllerProvisioningTokenService
    {
        public ControllerProvisioningToken Create() => throw new NotSupportedException();
        public string Hash(string token) => $"hash:{token}";
    }

    private sealed class TestAttestation(bool valid) : IControllerAttestationVerifier
    {
        public Task<ControllerAttestationResult> VerifyAsync(ControllerAttestationRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(valid ? new ControllerAttestationResult(true, $"local-test:{request.HardwareId}", null) : new ControllerAttestationResult(false, null, "Rejected"));
    }

    private sealed class InMemoryDeviceRepository(Device device) : IDeviceRepository
    {
        public bool CompletionResult { get; init; }
        public string? ReceivedHash { get; private set; }
        public string? ReceivedSubject { get; private set; }
        public Task<Device?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Device?>(id == device.Id ? device : null);
        public Task<Device?> GetByHardwareIdAsync(string hardwareId, CancellationToken cancellationToken = default) => Task.FromResult<Device?>(hardwareId == device.HardwareId ? device : null);
        public Task<List<Device>> GetByHomeIdAsync(Guid homeId, CancellationToken cancellationToken = default) => Task.FromResult(homeId == device.HomeId ? new List<Device> { device } : new List<Device>());
        public Task AddAsync(Device item, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateAsync(Device item, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> TryCompleteProvisioningAsync(Guid deviceId, string provisioningTokenHash, string publicKeyThumbprint, string attestationSubject, CancellationToken cancellationToken = default) { ReceivedHash = provisioningTokenHash; ReceivedSubject = attestationSubject; return Task.FromResult(CompletionResult); }
    }
}
