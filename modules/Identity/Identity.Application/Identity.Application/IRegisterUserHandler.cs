using MediatR;

namespace Identity.Application;

public record RegisterUserCommand(
    string PhoneNumber,
    string Password,
    string ConfirmPassword,
    string Otp,
    string DeviceId,
    string DeviceFingerprint,
    string DevicePublicKeySpkiBase64,
    string? Email = null
) : IRequest<RegisterUserResult>;

public record RegisterUserResult(Guid UserId, string Token, string Message);
