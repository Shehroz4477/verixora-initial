using Identity.Application;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace Identity.Infrastructure;

/// <summary>
/// Reads the initial administrator phone from secret configuration. When the
/// setting is absent, no registration receives elevated access.
/// </summary>
public sealed class ConfigurationSystemAdministratorBootstrapPolicy : ISystemAdministratorBootstrapPolicy
{
    private readonly string? _bootstrapPhoneE164;

    public ConfigurationSystemAdministratorBootstrapPolicy(IConfiguration configuration)
    {
        _bootstrapPhoneE164 = configuration["SystemAdministration:BootstrapPhoneE164"]?.Trim();
    }

    public bool IsBootstrapSystemAdministratorPhone(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(_bootstrapPhoneE164) || string.IsNullOrWhiteSpace(phoneNumber))
            return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(_bootstrapPhoneE164),
            Encoding.UTF8.GetBytes(phoneNumber.Trim()));
    }
}
