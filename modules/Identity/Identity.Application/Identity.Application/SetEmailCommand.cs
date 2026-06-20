using MediatR;

namespace Identity.Application;

public record SetEmailCommand(Guid UserId, string Email) : IRequest<SetEmailResult>;

public record SetEmailResult(bool Success, string Message);
