using Authorization.Domain;
using BuildingBlocks.Domain;
using Devices.Application;
using Devices.Domain;
using FaceVerification.Domain;
using Homes.Application;
using MediatR;
using SmartLocks.Domain;
using System.Text.Json;

namespace SmartLocks.Application;

public class UnlockDoorCommandHandler : IRequestHandler<UnlockDoorCommand, UnlockDoorResult>
{
    private readonly ISmartLockRepository _lockRepository;
    private readonly IAuthorizationService _authService;
    private readonly IFaceVerificationProvider _faceProvider;
    private readonly IMqttPublisher _mqttPublisher;
    private readonly IAuditLogService _auditLogService;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IHomeRepository _homeRepository;
    private readonly ILockCommandRepository _commandRepository;

    public UnlockDoorCommandHandler(
        ISmartLockRepository lockRepository,
        IAuthorizationService authService,
        IFaceVerificationProvider faceProvider,
        IMqttPublisher mqttPublisher,
        IAuditLogService auditLogService,
        IDeviceRepository deviceRepository,
        IHomeRepository homeRepository,
        ILockCommandRepository commandRepository)
    {
        _lockRepository = lockRepository;
        _authService = authService;
        _faceProvider = faceProvider;
        _mqttPublisher = mqttPublisher;
        _auditLogService = auditLogService;
        _deviceRepository = deviceRepository;
        _homeRepository = homeRepository;
        _commandRepository = commandRepository;
    }

    public async Task<UnlockDoorResult> Handle(UnlockDoorCommand request, CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty)
            throw new DomainException("An authenticated user is required.");
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Length > 128)
            throw new DomainException("A valid idempotency key is required.");

        var smartLock = await _lockRepository.GetByIdAsync(request.LockId, cancellationToken);
        if (smartLock is null)
            throw new DomainException("Smart lock not found.");
        if (smartLock.Status == LockStatus.EmergencyLocked)
            throw new DomainException("This door is emergency-locked and cannot be remotely unlocked.");

        var device = await _deviceRepository.GetByIdAsync(smartLock.DeviceId, cancellationToken);
        if (device is null || device.HomeId != smartLock.HomeId)
            throw new DomainException("The lock controller is not valid for this home.");
        if (device.Status != DeviceStatus.Online)
        {
            await _auditLogService.LogAsync(smartLock.HomeId, smartLock.DeviceId, request.UserId, "UnlockCommand", false, "Controller is offline or not securely provisioned");
            throw new DomainException("The controller is not online. Use the local mechanical override if immediate access is required.");
        }

        var isSystemAdmin = string.Equals(request.UserRole, "SystemAdmin", StringComparison.Ordinal);
        var homeMembership = isSystemAdmin
            ? null
            : (await _homeRepository.GetForUserAsync(request.UserId, cancellationToken))
                .SingleOrDefault(home => home.Id == smartLock.HomeId);
        if (!isSystemAdmin && homeMembership is null)
        {
            await _auditLogService.LogAsync(smartLock.HomeId, smartLock.DeviceId, request.UserId, "UnlockCommand", false, "User is not a home member");
            throw new DomainException("You do not have access to this home.");
        }

        var accessRole = isSystemAdmin ? "SystemAdmin" : homeMembership!.Role;
        if (!await _authService.CanUnlockAsync(request.UserId, request.LockId, smartLock.HomeId, accessRole, cancellationToken))
        {
            await _auditLogService.LogAsync(smartLock.HomeId, smartLock.DeviceId, request.UserId, "UnlockCommand", false, "Authorization failed");
            throw new DomainException("Access denied by authorization policy.");
        }

        if (smartLock.RequiresFace)
        {
            if (request.FaceImageStream is null)
            {
                await _auditLogService.LogAsync(smartLock.HomeId, smartLock.DeviceId, request.UserId, "UnlockCommand", false, "Face image required");
                throw new DomainException("Face verification required but no image provided.");
            }

            bool faceMatch;
            try
            {
                faceMatch = await _faceProvider.VerifyAsync(request.UserId, request.FaceImageStream, cancellationToken);
            }
            catch (Exception)
            {
                await _auditLogService.LogAsync(smartLock.HomeId, smartLock.DeviceId, request.UserId, "UnlockCommand", false, "Face verification service unavailable");
                throw new DomainException("Face verification is temporarily unavailable; the door remains locked.");
            }

            if (!faceMatch)
            {
                await _auditLogService.LogAsync(smartLock.HomeId, smartLock.DeviceId, request.UserId, "UnlockCommand", false, "Face mismatch");
                throw new DomainException("Face verification failed.");
            }
        }

        // Broker acceptance does not prove a physical unlock. Persist the command first; it expires
        // quickly so an outage can never turn an old user action into a later door opening.
        var command = await _commandRepository.CreateOrGetAsync(
            new LockCommand(smartLock.Id, smartLock.DeviceId, smartLock.HomeId, request.UserId, request.IdempotencyKey, DateTime.UtcNow.AddSeconds(30)),
            cancellationToken);

        if (command.Status is LockCommandStatus.Acknowledged or LockCommandStatus.Failed or LockCommandStatus.Expired)
            return new UnlockDoorResult(command.Status == LockCommandStatus.Acknowledged, $"This unlock request was already processed: {command.Status}.");
        if (command.Status == LockCommandStatus.Published)
            return new UnlockDoorResult(true, "Unlock command was already delivered and is awaiting controller acknowledgement.");

        var payload = JsonSerializer.Serialize(new
        {
            command = "unlock",
            lockId = smartLock.Id,
            commandId = command.Id,
            requestedAtUtc = command.RequestedAtUtc,
            expiresAtUtc = command.ExpiresAtUtc,
            expiresAtUnixTimeSeconds = new DateTimeOffset(command.ExpiresAtUtc).ToUnixTimeSeconds()
        });
        try
        {
            await _mqttPublisher.PublishAsync($"{device.MqttTopic}/commands", payload);
            await _commandRepository.MarkPublishedAsync(command.Id, cancellationToken);
        }
        catch (Exception)
        {
            await _auditLogService.LogAsync(smartLock.HomeId, smartLock.DeviceId, request.UserId, "UnlockCommand", true, "Command stored in durable outbox; broker delivery is pending");
            return new UnlockDoorResult(true, "Unlock command stored securely and is awaiting controller delivery. It expires automatically if not delivered.");
        }

        await _auditLogService.LogAsync(smartLock.HomeId, smartLock.DeviceId, request.UserId, "UnlockCommand", true, "Command accepted by MQTT broker; awaiting controller acknowledgement");
        return new UnlockDoorResult(true, "Unlock command queued. The controller acknowledgement is pending.");
    }
}
