namespace Devices.Application;

public interface IControllerProvisioningTokenService
{
    ControllerProvisioningToken Create();
    string Hash(string token);
}

public sealed record ControllerProvisioningToken(string Value, string Hash, DateTime ExpiresAtUtc);
