using BuildingBlocks.Domain;
using Identity.Domain;
using MediatR;
using System.Security.Cryptography;
using System.Text;

namespace Identity.Application;

public class SendLoginOtpCommandHandler : IRequestHandler<SendLoginOtpCommand, SendLoginOtpResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IOtpService _otpService;

    public SendLoginOtpCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IOtpService otpService)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _otpService = otpService;
    }

    public async Task<SendLoginOtpResult> Handle(SendLoginOtpCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByPhoneNumberAsync(request.PhoneNumber, cancellationToken);
        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
            throw new DomainException("Invalid phone number or password.");

        var registered = user.TrustedDevice;
        if (registered is not { IsActive: true } ||
            !FixedTimeEquals(registered.DeviceId, request.DeviceId) ||
            !FixedTimeEquals(registered.DeviceFingerprint, request.DeviceFingerprint))
        {
            throw new DomainException("This account can only be used from its registered mobile device.");
        }

        await _otpService.SendLoginOtpAsync(request.PhoneNumber);
        return new SendLoginOtpResult(true, "OTP sent to phone.");
    }

    private static bool FixedTimeEquals(string expected, string actual)
        => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual ?? string.Empty));
}
