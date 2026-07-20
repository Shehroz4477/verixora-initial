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
        var phoneNumber = InternationalPhoneNumber.NormalizeE164(request.PhoneNumber);
        var user = await _userRepository.GetByPhoneNumberAsync(phoneNumber, cancellationToken);
        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
            throw new DomainException("Invalid phone number or password.");

        if (!MatchesTrustedDeviceId(user, request.DeviceId))
            throw new DomainException("This account can only be used from its registered mobile device.");

        // Validate OTP
        if (!await _otpService.ValidateLoginOtpAsync(phoneNumber, request.Otp))
            throw new DomainException("Invalid or expired OTP.");

        if (!MatchesTrustedDeviceFingerprint(user, request.DeviceFingerprint))
        {
            if (string.IsNullOrWhiteSpace(request.DevicePublicKeySpkiBase64))
                throw new DomainException("The trusted device security key changed. Reinstall recovery requires the new Android Keystore public key.");

            user.RefreshTrustedDevicePublicKey(request.DeviceId, request.DeviceFingerprint, request.DevicePublicKeySpkiBase64.Trim());
            await _userRepository.UpdateAsync(user, cancellationToken);
        }

        // Generate JWT
        var token = _jwtTokenGenerator.GenerateToken(user.Id, user.PhoneNumber, user.Role.ToString());

        return new LoginResult(user.Id, token);
    }

    private static bool MatchesTrustedDeviceId(User user, string deviceId)
    {
        var registered = user.TrustedDevice;
        return registered is { IsActive: true } &&
               FixedTimeEquals(registered.DeviceId, deviceId);
    }

    private static bool MatchesTrustedDeviceFingerprint(User user, string deviceFingerprint)
        => user.TrustedDevice is { IsActive: true } registered && FixedTimeEquals(registered.DeviceFingerprint, deviceFingerprint);

    private static bool FixedTimeEquals(string expected, string actual)
        => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual ?? string.Empty));
}
