using System.Security.Cryptography;
using System.Text;
using Devices.Application;

namespace Devices.Infrastructure;

public sealed class ControllerProvisioningTokenService : IControllerProvisioningTokenService
{
    public ControllerProvisioningToken Create()
    {
        var value = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return new ControllerProvisioningToken(value, Hash(value), DateTime.UtcNow.AddMinutes(10));
    }

    public string Hash(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token ?? string.Empty)));
}
