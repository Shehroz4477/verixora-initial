using MediatR;

namespace Identity.Application;

public record WebLoginCommand(string Email, string Password, string Otp) : IRequest<LoginResult>;
