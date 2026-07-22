using MediatR;

namespace Identity.Application;

/// <summary>
/// Gives the native application a safe, explicit UI state before it renders
/// registration or login inputs. The registration and login commands still
/// repeat every rule to protect against a modified client.
/// </summary>
public record AuthAccessEligibilityQuery(string DeviceId, string? PhoneNumber) : IRequest<AuthAccessEligibilityResult>;

public record AuthAccessEligibilityResult(
    bool CanRegister,
    bool CanLogin,
    string DeviceStatus,
    string PhoneStatus,
    string Message);
