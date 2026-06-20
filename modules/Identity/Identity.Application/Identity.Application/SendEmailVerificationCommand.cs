using MediatR;

namespace Identity.Application;

public record SendEmailVerificationCommand(Guid UserId) : IRequest<SendEmailVerificationResult>;

public record SendEmailVerificationResult(bool Success, string Message);
