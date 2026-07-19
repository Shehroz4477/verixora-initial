using Identity.Application;

namespace Identity.Infrastructure;

/// <summary>
/// Compatibility test double. The runtime composition always uses RedisOtpService.
/// </summary>
public sealed class MockOtpService : IOtpService
{
    private readonly Dictionary<string, (string Otp, DateTime Expiry)> _store = new();

    public Task SendRegistrationOtpAsync(string phoneNumber) => SendAsync("registration", phoneNumber);
    public Task<bool> ValidateRegistrationOtpAsync(string phoneNumber, string otp) => ValidateAsync("registration", phoneNumber, otp);
    public Task SendLoginOtpAsync(string phoneNumber) => SendAsync("login", phoneNumber);
    public Task<bool> ValidateLoginOtpAsync(string phoneNumber, string otp) => ValidateAsync("login", phoneNumber, otp);

    private Task SendAsync(string purpose, string phoneNumber)
    {
        _store[$"{purpose}:{phoneNumber}"] = ("123456", DateTime.UtcNow.AddMinutes(5));
        return Task.CompletedTask;
    }

    private Task<bool> ValidateAsync(string purpose, string phoneNumber, string otp)
    {
        var key = $"{purpose}:{phoneNumber}";
        if (_store.TryGetValue(key, out var entry) && entry.Expiry > DateTime.UtcNow && entry.Otp == otp)
        {
            _store.Remove(key);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }
}
