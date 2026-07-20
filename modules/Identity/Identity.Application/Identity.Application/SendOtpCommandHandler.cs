using MediatR;

namespace Identity.Application;

public class SendOtpCommandHandler : IRequestHandler<SendOtpCommand, SendOtpResult>
{
    private readonly IOtpService _otpService;

    public SendOtpCommandHandler(IOtpService otpService)
    {
        _otpService = otpService;
    }

    public async Task<SendOtpResult> Handle(SendOtpCommand request, CancellationToken cancellationToken)
    {
        await _otpService.SendRegistrationOtpAsync(InternationalPhoneNumber.NormalizeE164(request.PhoneNumber));
        return new SendOtpResult(true, "OTP sent successfully.");
    }
}
