using Identity.Application;

namespace Identity.Infrastructure;

public class MockOtpService : IOtpService
{
    // In-memory store for demo – phone -> (otp, expiry)
    private readonly Dictionary<string, (string Otp, DateTime Expiry)> _store = new();

    public Task<string> SendOtpAsync(string phoneNumber)
    {
        var otp = "123456"; // fixed for demo
        _store[phoneNumber] = (otp, DateTime.UtcNow.AddMinutes(5));
        Console.WriteLine($"MOCK SMS to {phoneNumber}: Your OTP is {otp}");
        return Task.FromResult(otp);
    }

    public Task<bool> ValidateOtpAsync(string phoneNumber, string otp)
    {
        if (_store.TryGetValue(phoneNumber, out var entry))
        {
            if (entry.Expiry > DateTime.UtcNow && entry.Otp == otp)
            {
                _store.Remove(phoneNumber);
                return Task.FromResult(true);
            }
        }
        return Task.FromResult(false);
    }
}
