using MediatR;

namespace Identity.Application;

public record SendOtpCommand(string PhoneNumber) : IRequest<SendOtpResult>;

public record SendOtpResult(bool Success, string Message);
