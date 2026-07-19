using MediatR;

namespace Identity.Application;

public record LoginCommand(string PhoneNumber, string Password, string Otp, string DeviceId, string DeviceFingerprint) : IRequest<LoginResult>;

public record LoginResult(Guid UserId, string Token);
