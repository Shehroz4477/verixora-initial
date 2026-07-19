using BuildingBlocks.Domain;
using Identity.Domain;
using MediatR;

namespace Identity.Application;

public class SendEmailVerificationCommandHandler : IRequestHandler<SendEmailVerificationCommand, SendEmailVerificationResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IEmailOtpService _emailOtpService;

    public SendEmailVerificationCommandHandler(IUserRepository userRepository, IEmailOtpService emailOtpService)
    {
        _userRepository = userRepository;
        _emailOtpService = emailOtpService;
    }

    public async Task<SendEmailVerificationResult> Handle(SendEmailVerificationCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
            throw new DomainException("User not found.");

        if (string.IsNullOrWhiteSpace(user.Email))
            throw new DomainException("No email set. Please set an email first.");

        if (user.EmailVerified)
            return new SendEmailVerificationResult(false, "Email already verified.");

        await _emailOtpService.SendEmailVerificationOtpAsync(user.Email);

        return new SendEmailVerificationResult(true, "Verification code sent to email.");
    }
}
