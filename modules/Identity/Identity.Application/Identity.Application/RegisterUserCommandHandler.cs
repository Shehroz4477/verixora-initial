using BuildingBlocks.Domain;
using Identity.Domain;
using MediatR;

namespace Identity.Application;

public class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, RegisterUserResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IOtpService _otpService;

    public RegisterUserCommandHandler(IUserRepository userRepository, IPasswordHasher passwordHasher, IOtpService otpService)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _otpService = otpService;
    }

    public async Task<RegisterUserResult> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        // Validate OTP
        if (!await _otpService.ValidateRegistrationOtpAsync(request.PhoneNumber, request.Otp))
            throw new DomainException("Invalid or expired OTP.");

        // Validate password match
        if (request.Password != request.ConfirmPassword)
            throw new DomainException("Passwords do not match.");

        if (string.IsNullOrWhiteSpace(request.DeviceId) || string.IsNullOrWhiteSpace(request.DeviceFingerprint))
            throw new DomainException("A device binding is required to register an account.");

        // Check uniqueness of phone number
        if (await _userRepository.PhoneNumberExistsAsync(request.PhoneNumber, cancellationToken))
            throw new DomainException("Phone number already registered.");

        // Hash password
        var passwordHash = _passwordHasher.Hash(request.Password);

        // Create user
        var user = new User(request.PhoneNumber, passwordHash);

        // Set email if provided
        if (!string.IsNullOrWhiteSpace(request.Email))
            user.SetEmail(request.Email);

        user.RegisterTrustedDevice(request.DeviceId.Trim(), request.DeviceFingerprint.Trim());

        // Save
        await _userRepository.AddAsync(user, cancellationToken);

        return new RegisterUserResult(user.Id, "User registered successfully.");
    }
}
