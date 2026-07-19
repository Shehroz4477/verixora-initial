namespace Identity.Application;

public interface IOtpService
{
    Task SendRegistrationOtpAsync(string phoneNumber);
    Task<bool> ValidateRegistrationOtpAsync(string phoneNumber, string otp);
    Task SendLoginOtpAsync(string phoneNumber);
    Task<bool> ValidateLoginOtpAsync(string phoneNumber, string otp);
}
