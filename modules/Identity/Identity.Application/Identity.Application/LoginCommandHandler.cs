using BuildingBlocks.Domain;
using Identity.Domain;
using MediatR;
using System.Security.Cryptography;
using System.Text;

namespace Identity.Application;

public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IOtpService _otpService;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public LoginCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IOtpService otpService,
        IJwtTokenGenerator jwtTokenGenerator)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _otpService = otpService;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public async Task<LoginResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByPhoneNumberAsync(request.PhoneNumber, cancellationToken);
        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
            throw new DomainException("Invalid phone number or password.");

        if (!MatchesTrustedDevice(user, request.DeviceId, request.DeviceFingerprint))
            throw new DomainException("This account can only be used from its registered mobile device.");

        // Validate OTP
        if (!await _otpService.ValidateLoginOtpAsync(request.PhoneNumber, request.Otp))
            throw new DomainException("Invalid or expired OTP.");

        // Generate JWT
        var token = _jwtTokenGenerator.GenerateToken(user.Id, user.PhoneNumber, user.Role.ToString());

        return new LoginResult(user.Id, token);
    }

    private static bool MatchesTrustedDevice(User user, string deviceId, string deviceFingerprint)
    {
        var registered = user.TrustedDevice;
        return registered is { IsActive: true } &&
               FixedTimeEquals(registered.DeviceId, deviceId) &&
               FixedTimeEquals(registered.DeviceFingerprint, deviceFingerprint);
    }

    private static bool FixedTimeEquals(string expected, string actual)
        => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual ?? string.Empty));
}
