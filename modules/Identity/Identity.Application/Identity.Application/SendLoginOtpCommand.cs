using MediatR;

namespace Identity.Application;

public record SendLoginOtpCommand(string PhoneNumber, string Password, string DeviceId, string DeviceFingerprint) : IRequest<SendLoginOtpResult>;

public record SendLoginOtpResult(bool Success, string Message);
