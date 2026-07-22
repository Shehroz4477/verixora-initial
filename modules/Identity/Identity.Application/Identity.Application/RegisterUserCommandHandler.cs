using BuildingBlocks.Domain;
using Identity.Domain;
using MediatR;
using System.Security.Cryptography;
using System.Text;

namespace Identity.Application;

public class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, RegisterUserResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IOtpService _otpService;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly ISystemAdministratorBootstrapPolicy? _systemAdministratorBootstrapPolicy;

    public RegisterUserCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IOtpService otpService,
        IJwtTokenGenerator jwtTokenGenerator,
        ISystemAdministratorBootstrapPolicy? systemAdministratorBootstrapPolicy = null)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _otpService = otpService;
        _jwtTokenGenerator = jwtTokenGenerator;
        _systemAdministratorBootstrapPolicy = systemAdministratorBootstrapPolicy;
    }

    public async Task<RegisterUserResult> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        var phoneNumber = InternationalPhoneNumber.NormalizeE164(request.PhoneNumber);

        // Validate OTP
        if (!await _otpService.ValidateRegistrationOtpAsync(phoneNumber, request.Otp))
            throw new DomainException("Invalid or expired OTP.");

        // Validate password match
        if (request.Password != request.ConfirmPassword)
            throw new DomainException("Passwords do not match.");

        if (string.IsNullOrWhiteSpace(request.DeviceId) || string.IsNullOrWhiteSpace(request.DeviceFingerprint) || string.IsNullOrWhiteSpace(request.DevicePublicKeySpkiBase64))
            throw new DomainException("A cryptographic device binding is required to register an account.");

        var publicKeyThumbprint = TrustedDevicePublicKey.ValidateAndGetThumbprint(request.DevicePublicKeySpkiBase64);
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(publicKeyThumbprint), Encoding.UTF8.GetBytes(request.DeviceFingerprint)))
            throw new DomainException("The mobile device fingerprint does not match its public key.");

        // Check uniqueness of phone number
        if (await _userRepository.PhoneNumberExistsAsync(phoneNumber, cancellationToken))
            throw new DomainException("Phone number already registered.");

        if (await _userRepository.TrustedDeviceIdExistsAsync(request.DeviceId.Trim(), cancellationToken))
            throw new DomainException("This mobile device is already bound to an account.");

        // Hash password
        var passwordHash = _passwordHasher.Hash(request.Password);

        // Create user
        var role = _systemAdministratorBootstrapPolicy?.IsBootstrapSystemAdministratorPhone(phoneNumber) == true
            ? UserRole.SystemAdmin
            : UserRole.Owner;
        var user = new User(phoneNumber, passwordHash, role);

        // Set email if provided
        if (!string.IsNullOrWhiteSpace(request.Email))
            user.SetEmail(request.Email);

        user.RegisterTrustedDevice(request.DeviceId.Trim(), request.DeviceFingerprint.Trim(), request.DevicePublicKeySpkiBase64.Trim());

        // Save
        await _userRepository.AddAsync(user, cancellationToken);

        var token = _jwtTokenGenerator.GenerateToken(user.Id, user.PhoneNumber, user.Role.ToString());
        return new RegisterUserResult(user.Id, token, "User registered successfully.");
    }
}
