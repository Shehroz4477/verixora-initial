using System.Security.Cryptography;
using BuildingBlocks.Domain;
using Devices.Domain;
using MediatR;

namespace Devices.Application;

public sealed record ControllerAttestationRequest(Guid DeviceId, string HardwareId, string ControllerPublicKeyPem, string? HardwareAttestation);
public sealed record ControllerAttestationResult(bool IsValid, string? Subject, string? FailureReason);
public interface IControllerAttestationVerifier { Task<ControllerAttestationResult> VerifyAsync(ControllerAttestationRequest request, CancellationToken cancellationToken = default); }
public sealed record CompleteControllerProvisioningCommand(Guid DeviceId, string ProvisioningToken, string ControllerPublicKeyPem, string? HardwareAttestation) : IRequest<CompleteControllerProvisioningResult>;
public sealed record CompleteControllerProvisioningResult(Guid DeviceId, string Status, string ControllerPublicKeyThumbprint);

public sealed class CompleteControllerProvisioningCommandHandler(IDeviceRepository devices, IControllerProvisioningTokenService tokens, IControllerAttestationVerifier attestations) : IRequestHandler<CompleteControllerProvisioningCommand, CompleteControllerProvisioningResult>
{
    public async Task<CompleteControllerProvisioningResult> Handle(CompleteControllerProvisioningCommand request, CancellationToken cancellationToken)
    {
        var device = await devices.GetByIdAsync(request.DeviceId, cancellationToken) ?? throw new DomainException("Controller provisioning session is invalid.");
        if (device.Status != DeviceStatus.Pending || string.IsNullOrWhiteSpace(request.ProvisioningToken)) throw new DomainException("Controller provisioning session is invalid.");
        var thumbprint = PublicKeyThumbprint(request.ControllerPublicKeyPem);
        var attestation = await attestations.VerifyAsync(new ControllerAttestationRequest(device.Id, device.HardwareId, request.ControllerPublicKeyPem, request.HardwareAttestation), cancellationToken);
        if (!attestation.IsValid || string.IsNullOrWhiteSpace(attestation.Subject)) throw new DomainException("Controller hardware attestation was rejected.");
        if (!await devices.TryCompleteProvisioningAsync(device.Id, tokens.Hash(request.ProvisioningToken), thumbprint, attestation.Subject, cancellationToken)) throw new DomainException("Controller provisioning session is invalid or expired.");
        return new CompleteControllerProvisioningResult(device.Id, "Active", thumbprint);
    }
    private static string PublicKeyThumbprint(string pem)
    {
        try { using var key = ECDsa.Create(); key.ImportFromPem(pem); if (key.KeySize != 256) throw new DomainException("Controller key must use P-256."); return Convert.ToHexString(SHA256.HashData(key.ExportSubjectPublicKeyInfo())); }
        catch (CryptographicException) { throw new DomainException("Controller public key is invalid."); }
    }
}
