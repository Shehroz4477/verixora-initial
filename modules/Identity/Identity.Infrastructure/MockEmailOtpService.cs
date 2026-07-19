using Identity.Application;

namespace Identity.Infrastructure;

public class MockEmailOtpService : IEmailOtpService
{
    private readonly Dictionary<string, (string Otp, DateTime Expiry)> _store = new();

    public Task SendEmailOtpAsync(string email)
    {
        var otp = "654321"; // fixed for demo
        _store[email] = (otp, DateTime.UtcNow.AddMinutes(5));
        Console.WriteLine($"MOCK EMAIL OTP to {email}: {otp}");
        return Task.CompletedTask;
    }

    public Task<bool> ValidateEmailOtpAsync(string email, string otp)
    {
        if (_store.TryGetValue(email, out var entry))
        {
            if (entry.Expiry > DateTime.UtcNow && entry.Otp == otp)
            {
                _store.Remove(email);
                return Task.FromResult(true);
            }
        }
        return Task.FromResult(false);
    }

    public Task SendEmailVerificationOtpAsync(string email) => SendEmailOtpAsync(email);

    public Task<bool> ValidateEmailVerificationOtpAsync(string email, string otp) => ValidateEmailOtpAsync(email, otp);
}
