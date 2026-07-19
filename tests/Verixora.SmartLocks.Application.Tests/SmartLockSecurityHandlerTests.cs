using Authorization.Domain;
using BuildingBlocks.Domain;
using Devices.Application;
using Devices.Domain;
using FaceVerification.Domain;
using Homes.Application;
using SmartLocks.Application;
using SmartLocks.Domain;
using Xunit;

namespace Verixora.SmartLocks.Application.Tests;

public sealed class SmartLockSecurityHandlerTests
{
    [Fact]
    public async Task Pending_controller_cannot_be_assigned_to_a_door()
    {
        var ownerId = Guid.NewGuid();
        var homeId = Guid.NewGuid();
        var controller = new Device("Front Door ESP32", homeId, "ESP32-TEST-1");
        var handler = new RegisterSmartLockCommandHandler(
            new InMemorySmartLockRepository(),
            new InMemoryDeviceRepository(controller),
            new FixedHomeRepository(new HomeSummary(homeId, "Main Home", ownerId, "Owner", 20, DateTime.UtcNow)));

        var exception = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(
            new RegisterSmartLockCommand("Front Door", controller.Id, homeId, ownerId, true),
            TestContext.Current.CancellationToken));

        Assert.Equal("The controller must complete secure provisioning before a lock can be registered.", exception.Message);
    }

    [Fact]
    public async Task Online_owner_unlock_queues_a_command_without_claiming_physical_success()
    {
        var ownerId = Guid.NewGuid();
        var homeId = Guid.NewGuid();
        var controller = new Device("Front Door ESP32", homeId, "ESP32-TEST-2");
        controller.Activate();
        controller.MarkOnline();
        var smartLock = new SmartLock("Front Door", controller.Id, homeId);
        var lockRepository = new InMemorySmartLockRepository(smartLock);
        var mqtt = new CapturingMqttPublisher();
        var audit = new CapturingAuditLogService();
        var handler = new UnlockDoorCommandHandler(
            lockRepository,
            new AllowOwnerAuthorizationService(),
            new PassingFaceProvider(),
            mqtt,
            audit,
            new InMemoryDeviceRepository(controller),
            new FixedHomeRepository(new HomeSummary(homeId, "Main Home", ownerId, "Owner", 20, DateTime.UtcNow)));

        var result = await handler.Handle(
            new UnlockDoorCommand(smartLock.Id, ownerId, "Owner", null, "command-1"),
            TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Contains("queued", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(LockStatus.Locked, smartLock.Status);
        Assert.Equal($"{controller.MqttTopic}/commands", mqtt.Topic);
        Assert.Contains("command-1", mqtt.Payload);
        var eventEntry = Assert.Single(audit.Events);
        Assert.True(eventEntry.Result);
        Assert.Equal("UnlockCommand", eventEntry.Action);
    }

    [Fact]
    public async Task Owner_can_read_locks_only_for_an_authorized_home()
    {
        var ownerId = Guid.NewGuid();
        var homeId = Guid.NewGuid();
        var controller = new Device("Front Door ESP32", homeId, "ESP32-TEST-3");
        controller.Activate();
        var smartLock = new SmartLock("Front Door", controller.Id, homeId, requiresFace: true);
        var handler = new GetSmartLocksForHomeQueryHandler(
            new InMemorySmartLockRepository(smartLock),
            new InMemoryDeviceRepository(controller),
            new FixedHomeRepository(new HomeSummary(homeId, "Main Home", ownerId, "Owner", 20, DateTime.UtcNow)));

        var result = await handler.Handle(
            new GetSmartLocksForHomeQuery(homeId, ownerId, false),
            TestContext.Current.CancellationToken);

        var summary = Assert.Single(result);
        Assert.Equal(smartLock.Id, summary.Id);
        Assert.Equal("Active", summary.ControllerStatus);
        Assert.True(summary.RequiresFace);
    }

    private sealed class InMemorySmartLockRepository(params SmartLock[] locks) : ISmartLockRepository
    {
        private readonly List<SmartLock> _locks = locks.ToList();
        public Task<SmartLock?> GetByIdAsync(Guid lockId, CancellationToken cancellationToken = default) => Task.FromResult(_locks.SingleOrDefault(lockItem => lockItem.Id == lockId));
        public Task<List<SmartLock>> GetByHomeIdAsync(Guid homeId, CancellationToken cancellationToken = default) => Task.FromResult(_locks.Where(lockItem => lockItem.HomeId == homeId).ToList());
        public Task AddAsync(SmartLock smartLock, CancellationToken cancellationToken = default) { _locks.Add(smartLock); return Task.CompletedTask; }
        public Task UpdateAsync(SmartLock smartLock, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class InMemoryDeviceRepository(params Device[] devices) : IDeviceRepository
    {
        private readonly List<Device> _devices = devices.ToList();
        public Task<Device?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(_devices.SingleOrDefault(device => device.Id == id));
        public Task<Device?> GetByHardwareIdAsync(string hardwareId, CancellationToken cancellationToken = default) => Task.FromResult(_devices.SingleOrDefault(device => device.HardwareId == hardwareId));
        public Task<List<Device>> GetByHomeIdAsync(Guid homeId, CancellationToken cancellationToken = default) => Task.FromResult(_devices.Where(device => device.HomeId == homeId).ToList());
        public Task AddAsync(Device device, CancellationToken cancellationToken = default) { _devices.Add(device); return Task.CompletedTask; }
        public Task UpdateAsync(Device device, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> TryCompleteProvisioningAsync(Guid deviceId, string provisioningTokenHash, string publicKeyThumbprint, string attestationSubject, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class FixedHomeRepository(params HomeSummary[] homes) : IHomeRepository
    {
        public Task<HomeSummary> AddAsync(Homes.Domain.Home home, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<HomeSummary>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<HomeSummary>>(homes.Where(home => home.OwnerId == userId).ToList());
    }

    private sealed class AllowOwnerAuthorizationService : IAuthorizationService
    {
        public Task<bool> CanUnlockAsync(Guid userId, Guid lockId, Guid homeId, string role, CancellationToken cancellationToken = default) => Task.FromResult(role == "Owner");
        public Task<bool> CanLockAsync(Guid userId, Guid lockId, Guid homeId, string role, CancellationToken cancellationToken = default) => Task.FromResult(role == "Owner");
    }

    private sealed class PassingFaceProvider : IFaceVerificationProvider
    {
        public Task<bool> VerifyAsync(Guid userId, Stream imageStream, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task EnrollAsync(Guid userId, List<Stream> imageStreams, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class CapturingMqttPublisher : IMqttPublisher
    {
        public string Topic { get; private set; } = string.Empty;
        public string Payload { get; private set; } = string.Empty;
        public Task PublishAsync(string topic, string payload) { Topic = topic; Payload = payload; return Task.CompletedTask; }
    }

    private sealed class CapturingAuditLogService : IAuditLogService
    {
        public List<(Guid HomeId, Guid DeviceId, Guid UserId, string Action, bool Result, string? Details)> Events { get; } = [];
        public Task LogAsync(Guid homeId, Guid deviceId, Guid userId, string action, bool result, string? details = null)
        {
            Events.Add((homeId, deviceId, userId, action, result, details));
            return Task.CompletedTask;
        }
    }
}
