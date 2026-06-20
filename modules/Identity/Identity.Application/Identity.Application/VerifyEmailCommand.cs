using MediatR;

namespace Identity.Application;

public record VerifyEmailCommand(Guid UserId, string Code) : IRequest<VerifyEmailResult>;

public record VerifyEmailResult(bool Success, string Message);
