using MediatR;

namespace Identity.Application;

public record WebLoginSendOtpCommand(string Email, string Password) : IRequest<WebLoginSendOtpResult>;

public record WebLoginSendOtpResult(bool Success, string Message);
