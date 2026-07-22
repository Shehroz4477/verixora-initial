using BuildingBlocks.Domain;
using MediatR;

namespace Identity.Application;

public class SendOtpCommandHandler : IRequestHandler<SendOtpCommand, SendOtpResult>
{
    private readonly IOtpService _otpService;
    private readonly IUserRepository _userRepository;

    public SendOtpCommandHandler(IOtpService otpService, IUserRepository userRepository)
    {
        _otpService = otpService;
        _userRepository = userRepository;
    }

    public async Task<SendOtpResult> Handle(SendOtpCommand request, CancellationToken cancellationToken)
    {
        var phoneNumber = InternationalPhoneNumber.NormalizeE164(request.PhoneNumber);
        if (await _userRepository.PhoneNumberExistsAsync(phoneNumber, cancellationToken))
            throw new DomainException("This mobile number is already registered. Sign in from its registered device.");

        if (await _userRepository.TrustedDeviceIdExistsAsync(request.DeviceId.Trim(), cancellationToken))
            throw new DomainException("This mobile device is already registered. Sign in instead.");

        await _otpService.SendRegistrationOtpAsync(phoneNumber);
        return new SendOtpResult(true, "OTP sent successfully.");
    }
}
