using BuildingBlocks.Domain;
using Identity.Domain;
using MediatR;

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

        await _otpService.SendOtpAsync(request.PhoneNumber);
        return new SendLoginOtpResult(true, "OTP sent to phone.");
    }
}
