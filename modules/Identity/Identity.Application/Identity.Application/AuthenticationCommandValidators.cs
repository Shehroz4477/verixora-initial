using FluentValidation;

namespace Identity.Application;

public sealed class SendOtpCommandValidator : AbstractValidator<SendOtpCommand>
{
    public SendOtpCommandValidator()
    {
        RuleFor(command => command.PhoneNumber)
            .Must(value => InternationalPhoneNumber.TryNormalizeE164(value, out _))
            .WithMessage("Phone number must be a valid international E.164 number.");
        RuleFor(command => command.DeviceId).NotEmpty().MaximumLength(256);
    }
}

public sealed class AuthAccessEligibilityQueryValidator : AbstractValidator<AuthAccessEligibilityQuery>
{
    public AuthAccessEligibilityQueryValidator()
    {
        RuleFor(query => query.DeviceId).NotEmpty().MaximumLength(256);
        When(query => !string.IsNullOrWhiteSpace(query.PhoneNumber), () =>
            RuleFor(query => query.PhoneNumber!)
                .Must(value => InternationalPhoneNumber.TryNormalizeE164(value, out _))
                .WithMessage("Phone number must be a valid international E.164 number."));
    }
}

public sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(command => command.PhoneNumber)
            .Must(value => InternationalPhoneNumber.TryNormalizeE164(value, out _))
            .WithMessage("Phone number must be a valid international E.164 number.");
        RuleFor(command => command.Password)
            .MinimumLength(8)
            .Matches("[A-Z]").WithMessage("Password must include an uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must include a lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must include a number.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must include a special character.");
        RuleFor(command => command.ConfirmPassword)
            .Equal(command => command.Password).WithMessage("Passwords do not match.");
        RuleFor(command => command.Otp).Matches("^[0-9]{6}$");
        RuleFor(command => command.DeviceId).NotEmpty().MaximumLength(256);
        RuleFor(command => command.DeviceFingerprint).NotEmpty().MaximumLength(512);
        RuleFor(command => command.DevicePublicKeySpkiBase64).NotEmpty().MaximumLength(4096);
        When(command => !string.IsNullOrWhiteSpace(command.Email), () =>
            RuleFor(command => command.Email!).EmailAddress().MaximumLength(256));
    }
}

public sealed class SendLoginOtpCommandValidator : AbstractValidator<SendLoginOtpCommand>
{
    public SendLoginOtpCommandValidator()
    {
        RuleFor(command => command.PhoneNumber)
            .Must(value => InternationalPhoneNumber.TryNormalizeE164(value, out _))
            .WithMessage("Phone number must be a valid international E.164 number.");
        RuleFor(command => command.Password).NotEmpty();
        RuleFor(command => command.DeviceId).NotEmpty().MaximumLength(256);
        RuleFor(command => command.DeviceFingerprint).NotEmpty().MaximumLength(512);
    }
}

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(command => command.PhoneNumber)
            .Must(value => InternationalPhoneNumber.TryNormalizeE164(value, out _))
            .WithMessage("Phone number must be a valid international E.164 number.");
        RuleFor(command => command.Password).NotEmpty();
        RuleFor(command => command.Otp).Matches("^[0-9]{6}$");
        RuleFor(command => command.DeviceId).NotEmpty().MaximumLength(256);
        RuleFor(command => command.DeviceFingerprint).NotEmpty().MaximumLength(512);
        When(command => !string.IsNullOrWhiteSpace(command.DevicePublicKeySpkiBase64), () =>
            RuleFor(command => command.DevicePublicKeySpkiBase64!).MaximumLength(4096));
    }
}
