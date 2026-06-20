using BuildingBlocks.Domain;
using Identity.Domain;
using MediatR;

namespace Identity.Application;

public class WebLoginCommandHandler : IRequestHandler<WebLoginCommand, LoginResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmailOtpService _emailOtpService;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public WebLoginCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IEmailOtpService emailOtpService,
        IJwtTokenGenerator jwtTokenGenerator)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _emailOtpService = emailOtpService;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public async Task<LoginResult> Handle(WebLoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
            throw new DomainException("Invalid email or password.");

        if (!user.EmailVerified)
            throw new DomainException("Email not verified.");

        if (user.Role == UserRole.Guest)
            throw new DomainException("Guests cannot access the web portal.");

        if (!await _emailOtpService.ValidateEmailOtpAsync(request.Email, request.Otp))
            throw new DomainException("Invalid or expired OTP.");

        var token = _jwtTokenGenerator.GenerateToken(user.Id, user.PhoneNumber, user.Role.ToString());
        return new LoginResult(user.Id, token);
    }
}
