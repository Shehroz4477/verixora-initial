using MediatR;

namespace SmartLocks.Application;

public record UnlockDoorCommand(Guid LockId, Guid UserId, Stream? FaceImageStream, string IdempotencyKey)
    : IRequest<UnlockDoorResult>;

public record UnlockDoorResult(bool Success, string Message);
