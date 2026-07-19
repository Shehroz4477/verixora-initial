namespace Identity.Application;

public interface IEmailOtpService
{
    Task SendEmailOtpAsync(string email);
    Task<bool> ValidateEmailOtpAsync(string email, string otp);
    Task SendEmailVerificationOtpAsync(string email);
    Task<bool> ValidateEmailVerificationOtpAsync(string email, string otp);
}
