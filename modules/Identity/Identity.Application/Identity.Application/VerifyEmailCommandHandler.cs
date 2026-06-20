using BuildingBlocks.Domain;
using Identity.Domain;
using MediatR;

namespace Identity.Application;

public class VerifyEmailCommandHandler : IRequestHandler<VerifyEmailCommand, VerifyEmailResult>
{
    private readonly IUserRepository _userRepository;

    public VerifyEmailCommandHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<VerifyEmailResult> Handle(VerifyEmailCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
            throw new DomainException("User not found.");

        if (string.IsNullOrWhiteSpace(user.Email))
            throw new DomainException("No email set.");

        // Mock code check
        if (request.Code != "123456")
            throw new DomainException("Invalid verification code.");

        user.VerifyEmail();
        await _userRepository.UpdateAsync(user, cancellationToken);

        return new VerifyEmailResult(true, "Email verified successfully.");
    }
}
