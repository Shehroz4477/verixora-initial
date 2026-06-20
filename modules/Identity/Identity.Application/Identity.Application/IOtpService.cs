namespace Identity.Application;

public interface IOtpService
{
    Task<string> SendOtpAsync(string phoneNumber); // returns OTP for demo, normally void
    Task<bool> ValidateOtpAsync(string phoneNumber, string otp);
}
