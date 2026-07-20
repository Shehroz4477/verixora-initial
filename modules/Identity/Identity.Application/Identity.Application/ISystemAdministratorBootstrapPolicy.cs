namespace Identity.Application;

/// <summary>
/// Controls the one-time, deployment-configured creation of the first system
/// administrator. Public registration must never be able to select a role.
/// </summary>
public interface ISystemAdministratorBootstrapPolicy
{
    bool IsBootstrapSystemAdministratorPhone(string phoneNumber);
}
