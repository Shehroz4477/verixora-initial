namespace Identity.Application;

/// <summary>
/// Delivers an OTP without exposing it to an API response or application log
/// outside the local-development adapter.
/// </summary>
public interface ISmsService
{
    Task SendOtpAsync(string phoneNumber, string code, CancellationToken cancellationToken = default);
}
