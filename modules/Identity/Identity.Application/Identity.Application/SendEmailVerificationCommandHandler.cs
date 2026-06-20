using BuildingBlocks.Domain;
using Identity.Domain;
using MediatR;

namespace Identity.Application;

public class SendEmailVerificationCommandHandler : IRequestHandler<SendEmailVerificationCommand, SendEmailVerificationResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IEmailService _emailService;

    public SendEmailVerificationCommandHandler(IUserRepository userRepository, IEmailService emailService)
    {
        _userRepository = userRepository;
        _emailService = emailService;
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

        // In a real system, store a code per user; mock will just use "123456"
        await _emailService.SendVerificationCodeAsync(user.Email, "123456");

        return new SendEmailVerificationResult(true, "Verification code sent to email.");
    }
}
