using BuildingBlocks.Domain;
using Identity.Domain;
using MediatR;

namespace Identity.Application;

public class WebLoginSendOtpCommandHandler : IRequestHandler<WebLoginSendOtpCommand, WebLoginSendOtpResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmailOtpService _emailOtpService;

    public WebLoginSendOtpCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IEmailOtpService emailOtpService)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _emailOtpService = emailOtpService;
    }

    public async Task<WebLoginSendOtpResult> Handle(WebLoginSendOtpCommand request, CancellationToken cancellationToken)
    {
        // Find user by email (case-insensitive)
        var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
            throw new DomainException("Invalid email or password.");

        if (!user.EmailVerified)
            throw new DomainException("Email not verified. Please verify your email first.");

        if (user.Role == UserRole.Guest)
            throw new DomainException("Guests cannot access the web portal.");

        await _emailOtpService.SendEmailOtpAsync(request.Email);
        return new WebLoginSendOtpResult(true, "OTP sent to email.");
    }
}
