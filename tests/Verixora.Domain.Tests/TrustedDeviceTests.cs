using BuildingBlocks.Domain;
using Identity.Domain;
using Xunit;

namespace Verixora.Domain.Tests;

public sealed class TrustedDeviceTests
{
    [Fact]
    public void User_can_register_only_one_active_trusted_device()
    {
        var user = new User("+923001234567", "not-a-real-password-hash");
        user.RegisterTrustedDevice("android-device-1", "key-thumbprint-1");

        var exception = Assert.Throws<DomainException>(() => user.RegisterTrustedDevice("android-device-2", "key-thumbprint-2"));

        Assert.Equal("A trusted device is already registered. Contact support to switch devices.", exception.Message);
        Assert.Equal("android-device-1", user.TrustedDevice!.DeviceId);
    }

    [Fact]
    public void Deactivated_device_can_be_replaced()
    {
        var user = new User("+923001234567", "not-a-real-password-hash");
        user.RegisterTrustedDevice("android-device-1", "key-thumbprint-1");
        user.DeactivateTrustedDevice();

        user.RegisterTrustedDevice("android-device-2", "key-thumbprint-2");

        Assert.Equal("android-device-2", user.TrustedDevice!.DeviceId);
        Assert.True(user.TrustedDevice.IsActive);
    }
}
