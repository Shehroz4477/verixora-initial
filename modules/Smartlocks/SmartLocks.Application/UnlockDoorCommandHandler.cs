using Authorization.Domain;
using BuildingBlocks.Domain;
using FaceVerification.Domain;
using MediatR;

namespace SmartLocks.Application;

public class UnlockDoorCommandHandler : IRequestHandler<UnlockDoorCommand, UnlockDoorResult>
{
    private readonly ISmartLockRepository _lockRepository;
    private readonly IAuthorizationService _authService;
    private readonly IFaceVerificationProvider _faceProvider;
    private readonly IMqttPublisher _mqttPublisher;
    private readonly IAuditLogService _auditLogService;

    public UnlockDoorCommandHandler(
        ISmartLockRepository lockRepository,
        IAuthorizationService authService,
        IFaceVerificationProvider faceProvider,
        IMqttPublisher mqttPublisher,
        IAuditLogService auditLogService)
    {
        _lockRepository = lockRepository;
        _authService = authService;
        _faceProvider = faceProvider;
        _mqttPublisher = mqttPublisher;
        _auditLogService = auditLogService;
    }

    public async Task<UnlockDoorResult> Handle(UnlockDoorCommand request, CancellationToken cancellationToken)
    {
        var smartLock = await _lockRepository.GetByIdAsync(request.LockId, cancellationToken);
        if (smartLock is null)
            throw new DomainException("Smart lock not found.");

        // 1. Authorization check
        if (!await _authService.CanUnlockAsync(request.UserId, request.LockId, smartLock.HomeId, request.UserRole, cancellationToken))
        {
            await _auditLogService.LogAsync(request.LockId, request.UserId, "Unlock", false, "Authorization failed");
            throw new DomainException("Access denied by authorization policy.");
        }

        // 2. Face verification if required
        if (smartLock.RequiresFace)
        {
            if (request.FaceImageStream is null)
            {
                await _auditLogService.LogAsync(request.LockId, request.UserId, "Unlock", false, "Face image required");
                throw new DomainException("Face verification required but no image provided.");
            }

            var faceMatch = await _faceProvider.VerifyAsync(request.UserId, request.FaceImageStream, cancellationToken);
            if (!faceMatch)
            {
                await _auditLogService.LogAsync(request.LockId, request.UserId, "Unlock", false, "Face mismatch");
                throw new DomainException("Face verification failed.");
            }
        }

        // 3. Perform unlock
        smartLock.Unlock(request.UserId);
        await _lockRepository.UpdateAsync(smartLock, cancellationToken);

        // 4. Send MQTT command
        // Need device's MQTT topic – we'll get it via a new method or repository; for now assume we have DeviceRepository
        // We'll add a helper later; for now publish to a fixed topic based on lock.DeviceId
        await _mqttPublisher.PublishAsync($"verixora/{smartLock.DeviceId}", "{\"cmd\":\"unlock\"}");

        // 5. Audit log
        await _auditLogService.LogAsync(request.LockId, request.UserId, "Unlock", true, "Door unlocked successfully");

        return new UnlockDoorResult(true, "Door unlocked.");
    }
}
