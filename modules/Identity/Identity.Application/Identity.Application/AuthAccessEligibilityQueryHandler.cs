using System.Security.Cryptography;
using System.Text;
using Identity.Domain;
using MediatR;

namespace Identity.Application;

public sealed class AuthAccessEligibilityQueryHandler : IRequestHandler<AuthAccessEligibilityQuery, AuthAccessEligibilityResult>
{
    private readonly IUserRepository _userRepository;

    public AuthAccessEligibilityQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<AuthAccessEligibilityResult> Handle(AuthAccessEligibilityQuery request, CancellationToken cancellationToken)
    {
        var deviceId = request.DeviceId.Trim();
        var deviceIsRegistered = await _userRepository.TrustedDeviceIdExistsAsync(deviceId, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            return deviceIsRegistered
                ? new(false, false, "Registered", "NotChecked", "This device is already registered. Enter its mobile number to sign in.")
                : new(true, false, "New", "NotChecked", "This is a new device. Create an account to continue.");
        }

        var phoneNumber = InternationalPhoneNumber.NormalizeE164(request.PhoneNumber);
        var user = await _userRepository.GetByPhoneNumberAsync(phoneNumber, cancellationToken);
        if (user is null)
        {
            return deviceIsRegistered
                ? new(false, false, "Registered", "NotRegistered", "This number is not registered on this device.")
                : new(true, false, "New", "Available", "This number and device can be registered.");
        }

        if (MatchesTrustedDevice(user, deviceId))
            return new(false, true, "Registered", "RegisteredToThisDevice", "This number is registered on this device. Sign in to continue.");

        return new(false, false,
            deviceIsRegistered ? "Registered" : "New",
            "RegisteredToAnotherDevice",
            "This number is registered to a different mobile device.");
    }

    private static bool MatchesTrustedDevice(User user, string deviceId)
        => user.TrustedDevice is { IsActive: true } trusted &&
           CryptographicOperations.FixedTimeEquals(
               Encoding.UTF8.GetBytes(trusted.DeviceId),
               Encoding.UTF8.GetBytes(deviceId));
}
