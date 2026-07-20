using System.Security.Cryptography;
using System.Text;
using BuildingBlocks.Domain;
using Devices.Application;
using Devices.Domain;
using MediatR;

namespace SmartLocks.Application;

public sealed record AcknowledgeControllerCommand(
    Guid DeviceId,
    Guid CommandId,
    string Outcome,
    DateTime OccurredAtUtc,
    string Nonce,
    string SignatureBase64,
    string? Details) : IRequest<ControllerAcknowledgementResult>;

public sealed record ControllerAcknowledgementResult(Guid CommandId, string Status, bool DoorUnlocked);

public sealed class AcknowledgeControllerCommandHandler(
    IDeviceRepository devices,
    ILockCommandRepository commands,
    ISmartLockRepository locks,
    IAuditLogService audits) : IRequestHandler<AcknowledgeControllerCommand, ControllerAcknowledgementResult>
{
    public async Task<ControllerAcknowledgementResult> Handle(AcknowledgeControllerCommand request, CancellationToken cancellationToken)
    {
        if (request.DeviceId == Guid.Empty || request.CommandId == Guid.Empty ||
            request.Outcome is not ("Unlocked" or "Failed") || string.IsNullOrWhiteSpace(request.Nonce) || request.Nonce.Length > 128 ||
            string.IsNullOrWhiteSpace(request.SignatureBase64) || request.SignatureBase64.Length > 256 || (request.Details?.Length ?? 0) > 500)
            throw new DomainException("Controller acknowledgement is invalid.");

        if (request.OccurredAtUtc.Kind == DateTimeKind.Unspecified || Math.Abs((DateTime.UtcNow - request.OccurredAtUtc.ToUniversalTime()).TotalMinutes) > 5)
            throw new DomainException("Controller acknowledgement timestamp is outside the allowed window.");

        var device = await devices.GetByIdAsync(request.DeviceId, cancellationToken);
        if (device is null || device.Status is not (DeviceStatus.Active or DeviceStatus.Online) || string.IsNullOrWhiteSpace(device.ControllerPublicKeySpkiBase64))
            throw new DomainException("Controller acknowledgement is not trusted.");

        var command = await commands.GetByIdAsync(request.CommandId, cancellationToken);
        if (command is null || command.DeviceId != device.Id)
            throw new DomainException("Controller command is not valid.");

        VerifySignature(device.ControllerPublicKeySpkiBase64, request);
        var details = request.Details?.Trim() ?? string.Empty;
        if (!await commands.TryAcknowledgeAsync(command.Id, device.Id, request.Outcome, request.OccurredAtUtc.ToUniversalTime(), request.Nonce.Trim(), details, cancellationToken))
            throw new DomainException("Controller acknowledgement was already processed, expired, or replayed.");

        if (request.Outcome == "Unlocked")
        {
            var smartLock = await locks.GetByIdAsync(command.LockId, cancellationToken);
            if (smartLock is null || smartLock.DeviceId != device.Id)
                throw new DomainException("Controller acknowledgement lock is invalid.");

            smartLock.Unlock(command.RequestedBy);
            await locks.UpdateAsync(smartLock, cancellationToken);
            await audits.LogAsync(command.HomeId, device.Id, command.RequestedBy, "ControllerAcknowledgement", true, "Controller signature verified; physical unlock acknowledged");
            return new ControllerAcknowledgementResult(command.Id, "Acknowledged", true);
        }

        await audits.LogAsync(command.HomeId, device.Id, command.RequestedBy, "ControllerAcknowledgement", false, details.Length == 0 ? "Controller reported unlock failure" : details);
        return new ControllerAcknowledgementResult(command.Id, "Failed", false);
    }

    public static string CanonicalPayload(Guid deviceId, Guid commandId, string outcome, DateTime occurredAtUtc, string nonce)
        => $"Verixora.ControllerAck.v1|{deviceId:N}|{commandId:N}|{outcome}|{occurredAtUtc.ToUniversalTime():O}|{nonce}";

    private static void VerifySignature(string controllerPublicKeySpkiBase64, AcknowledgeControllerCommand request)
    {
        try
        {
            var spki = Convert.FromBase64String(controllerPublicKeySpkiBase64);
            var signature = Convert.FromBase64String(request.SignatureBase64);
            if (signature.Length != 64)
                throw new DomainException("Controller acknowledgement signature is invalid.");
            using var key = ECDsa.Create();
            key.ImportSubjectPublicKeyInfo(spki, out _);
            var payload = Encoding.UTF8.GetBytes(CanonicalPayload(request.DeviceId, request.CommandId, request.Outcome, request.OccurredAtUtc, request.Nonce));
            if (!key.VerifyData(payload, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
                throw new DomainException("Controller acknowledgement signature is invalid.");
        }
        catch (FormatException) { throw new DomainException("Controller acknowledgement signature is invalid."); }
        catch (CryptographicException) { throw new DomainException("Controller acknowledgement signature is invalid."); }
    }
}
