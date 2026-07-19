using Devices.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Devices.Infrastructure;

public sealed class DevelopmentOnlyControllerAttestationVerifier(IHostEnvironment environment, IConfiguration configuration) : IControllerAttestationVerifier
{
    public Task<ControllerAttestationResult> VerifyAsync(ControllerAttestationRequest request, CancellationToken cancellationToken = default)
    {
        var enabled = bool.TryParse(configuration["ControllerProvisioning:AllowInsecureDevelopmentAttestation"], out var configured) && configured;
        if (!environment.IsDevelopment() || !enabled) return Task.FromResult(new ControllerAttestationResult(false, null, "Manufacturer hardware attestation is required."));
        return Task.FromResult(request.HardwareAttestation == "local-development-only" ? new ControllerAttestationResult(true, $"local-development:{request.HardwareId}", null) : new ControllerAttestationResult(false, null, "Development attestation marker is invalid."));
    }
}
