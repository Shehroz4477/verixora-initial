using MediatR;

namespace Identity.Application;

public record LoginCommand(string PhoneNumber, string Password, string Otp) : IRequest<LoginResult>;

public record LoginResult(Guid UserId, string Token);
