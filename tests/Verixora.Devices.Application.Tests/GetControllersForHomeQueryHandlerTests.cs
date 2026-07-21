using BuildingBlocks.Domain;
using Devices.Application;
using Devices.Domain;
using Homes.Application;
using Xunit;

namespace Verixora.Devices.Application.Tests;

public sealed class GetControllersForHomeQueryHandlerTests
{
    [Fact]
    public async Task Home_member_receives_safe_controller_summary_without_provisioning_secrets()
    {
        var userId = Guid.NewGuid();
        var homeId = Guid.NewGuid();
        var device = new Device("Front Door ESP32", homeId, "esp32-front-01");
        var handler = new GetControllersForHomeQueryHandler(
            new DevicesForHome(device),
            new HomesForUser(new HomeSummary(homeId, "Main Home", userId, "Owner", 20, DateTime.UtcNow)));

        var result = await handler.Handle(new GetControllersForHomeQuery(homeId, userId, false), TestContext.Current.CancellationToken);

        var controller = Assert.Single(result);
        Assert.Equal(device.Id, controller.Id);
        Assert.Equal("ESP32-FRONT-01", controller.HardwareId);
        Assert.Equal("Pending", controller.Status);
        Assert.DoesNotContain("Token", string.Join('|', typeof(ControllerSummary).GetProperties().Select(property => property.Name)), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Key", string.Join('|', typeof(ControllerSummary).GetProperties().Select(property => property.Name)), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Unrelated_user_cannot_list_home_controllers()
    {
        var handler = new GetControllersForHomeQueryHandler(new DevicesForHome(), new HomesForUser());

        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(
            new GetControllersForHomeQuery(Guid.NewGuid(), Guid.NewGuid(), false),
            TestContext.Current.CancellationToken));
    }

    private sealed class DevicesForHome(params Device[] items) : IDeviceRepository
    {
        public Task<Device?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(items.SingleOrDefault(item => item.Id == id));
        public Task<Device?> GetByHardwareIdAsync(string hardwareId, CancellationToken cancellationToken = default) => Task.FromResult(items.SingleOrDefault(item => item.HardwareId == hardwareId));
        public Task<List<Device>> GetByHomeIdAsync(Guid homeId, CancellationToken cancellationToken = default) => Task.FromResult(items.Where(item => item.HomeId == homeId).ToList());
        public Task AddAsync(Device device, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateAsync(Device device, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> TryCompleteProvisioningAsync(Guid deviceId, string provisioningTokenHash, string publicKeyThumbprint, string publicKeySpkiBase64, string attestationSubject, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class HomesForUser(params HomeSummary[] homes) : IHomeRepository
    {
        public Task<HomeSummary> AddAsync(Homes.Domain.Home home, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<HomeSummary>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<HomeSummary>>(homes);
        public Task<IReadOnlyList<HomeSummary>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<HomeSummary>>(homes);
    }
}
