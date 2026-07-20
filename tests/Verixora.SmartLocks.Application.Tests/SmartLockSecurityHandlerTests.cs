using Authorization.Domain;
using BuildingBlocks.Domain;
using Devices.Application;
using Devices.Domain;
using FaceVerification.Domain;
using Homes.Application;
using Identity.Application;
using Identity.Domain;
using SmartLocks.Application;
using SmartLocks.Domain;
using System.Security.Cryptography;
using System.Text;
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
            new FixedHomeRepository(new HomeSummary(homeId, "Main Home", ownerId, "Owner", 20, DateTime.UtcNow)),
            new InMemoryLockCommandRepository(),
            new FixedUserRepository(CreateTrustedUser(ownerId)));

        var result = await handler.Handle(
            new UnlockDoorCommand(smartLock.Id, ownerId, "Owner", null, "command-1"),
            TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Contains("queued", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(LockStatus.Locked, smartLock.Status);
        Assert.Equal($"{controller.MqttTopic}/commands", mqtt.Topic);
        Assert.Contains("commandId", mqtt.Payload);
        Assert.Contains("trustedMobilePublicKeySpkiBase64", mqtt.Payload);
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

    [Fact]
    public async Task Verified_controller_acknowledgement_marks_the_door_unlocked_once()
    {
        var ownerId = Guid.NewGuid();
        var homeId = Guid.NewGuid();
        using var controllerKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var spki = controllerKey.ExportSubjectPublicKeyInfo();
        var controller = Device.Rehydrate(Guid.NewGuid(), homeId, "ESP32-TEST-4", "Front Door ESP32", "verixora/controller-4", DeviceStatus.Online, DateTime.UtcNow, null, null, Convert.ToHexString(SHA256.HashData(spki)), Convert.ToBase64String(spki), "test-controller", DateTime.UtcNow);
        var smartLock = new SmartLock("Front Door", controller.Id, homeId);
        var command = new LockCommand(smartLock.Id, controller.Id, homeId, ownerId, "ack-command-1", DateTime.UtcNow.AddSeconds(30));
        var commandRepository = new InMemoryLockCommandRepository();
        await commandRepository.CreateOrGetAsync(command, TestContext.Current.CancellationToken);
        var lockRepository = new InMemorySmartLockRepository(smartLock);
        var audit = new CapturingAuditLogService();
        var occurredAt = DateTime.UtcNow;
        var nonce = "controller-nonce-1";
        var payload = Encoding.UTF8.GetBytes(AcknowledgeControllerCommandHandler.CanonicalPayload(controller.Id, command.Id, "Unlocked", occurredAt, nonce));
        var signature = Convert.ToBase64String(controllerKey.SignData(payload, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
        var handler = new AcknowledgeControllerCommandHandler(new InMemoryDeviceRepository(controller), commandRepository, lockRepository, audit);

        var result = await handler.Handle(new AcknowledgeControllerCommand(controller.Id, command.Id, "Unlocked", occurredAt, nonce, signature, null), TestContext.Current.CancellationToken);

        Assert.True(result.DoorUnlocked);
        Assert.Equal(LockStatus.Unlocked, smartLock.Status);
        Assert.Single(audit.Events, entry => entry.Action == "ControllerAcknowledgement" && entry.Result);
        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(new AcknowledgeControllerCommand(controller.Id, command.Id, "Unlocked", occurredAt, nonce, signature, null), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Controller_acknowledgement_accepts_standard_p256_der_signatures()
    {
        var ownerId = Guid.NewGuid();
        var homeId = Guid.NewGuid();
        using var controllerKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var spki = controllerKey.ExportSubjectPublicKeyInfo();
        var controller = Device.Rehydrate(Guid.NewGuid(), homeId, "ESP32-TEST-DER", "Front Door ESP32", "verixora/controller-der", DeviceStatus.Online, DateTime.UtcNow, null, null, Convert.ToHexString(SHA256.HashData(spki)), Convert.ToBase64String(spki), "test-controller", DateTime.UtcNow);
        var smartLock = new SmartLock("Front Door", controller.Id, homeId);
        var command = new LockCommand(smartLock.Id, controller.Id, homeId, ownerId, "ack-command-der", DateTime.UtcNow.AddSeconds(30));
        var commands = new InMemoryLockCommandRepository();
        await commands.CreateOrGetAsync(command, TestContext.Current.CancellationToken);
        var occurredAt = DateTime.UtcNow;
        var nonce = "controller-der-nonce";
        var payload = Encoding.UTF8.GetBytes(AcknowledgeControllerCommandHandler.CanonicalPayload(controller.Id, command.Id, "Unlocked", occurredAt, nonce));
        var signature = Convert.ToBase64String(controllerKey.SignData(payload, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence));
        var handler = new AcknowledgeControllerCommandHandler(new InMemoryDeviceRepository(controller), commands, new InMemorySmartLockRepository(smartLock), new CapturingAuditLogService());

        var result = await handler.Handle(new AcknowledgeControllerCommand(controller.Id, command.Id, "Unlocked", occurredAt, nonce, signature, null), TestContext.Current.CancellationToken);

        Assert.True(result.DoorUnlocked);
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
        public Task<bool> TryCompleteProvisioningAsync(Guid deviceId, string provisioningTokenHash, string publicKeyThumbprint, string publicKeySpkiBase64, string attestationSubject, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class FixedHomeRepository(params HomeSummary[] homes) : IHomeRepository
    {
        public Task<HomeSummary> AddAsync(Homes.Domain.Home home, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<HomeSummary>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<HomeSummary>>(homes.Where(home => home.OwnerId == userId).ToList());
        public Task<IReadOnlyList<HomeSummary>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<HomeSummary>>(homes);
    }

    private static User CreateTrustedUser(Guid userId)
        => User.Rehydrate(
            userId,
            "+923001234567",
            "test-hash",
            null,
            false,
            UserRole.Owner,
            DateTime.UtcNow,
            TrustedDevice.Rehydrate(Guid.NewGuid(), userId, "mobile-device", "mobile-thumbprint", DateTime.UtcNow, true, "test-mobile-public-key", "test-mobile-thumbprint"));

    private sealed class FixedUserRepository(params User[] users) : IUserRepository
    {
        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(users.SingleOrDefault(user => user.Id == id));
        public Task<User?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default) => Task.FromResult(users.SingleOrDefault(user => user.PhoneNumber == phoneNumber));
        public Task<bool> PhoneNumberExistsAsync(string phoneNumber, CancellationToken cancellationToken = default) => Task.FromResult(users.Any(user => user.PhoneNumber == phoneNumber));
        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) => Task.FromResult(users.SingleOrDefault(user => user.Email == email));
        public Task AddAsync(User user, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateAsync(User user, CancellationToken cancellationToken = default) => Task.CompletedTask;
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

    private sealed class InMemoryLockCommandRepository : ILockCommandRepository
    {
        private readonly List<LockCommand> _commands = [];
        public Task<LockCommand> CreateOrGetAsync(LockCommand command, CancellationToken cancellationToken = default)
        {
            var existing = _commands.SingleOrDefault(item => item.LockId == command.LockId && item.IdempotencyKey == command.IdempotencyKey);
            if (existing is not null) return Task.FromResult(existing);
            _commands.Add(command);
            return Task.FromResult(command);
        }
        public Task<LockCommand?> GetByIdAsync(Guid commandId, CancellationToken cancellationToken = default) => Task.FromResult(_commands.SingleOrDefault(item => item.Id == commandId));
        public Task<IReadOnlyList<LockCommand>> GetQueuedForDispatchAsync(int maximum, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<LockCommand>>(_commands.Where(item => item.Status == LockCommandStatus.Queued).Take(maximum).ToList());
        public Task<bool> MarkPublishedAsync(Guid commandId, CancellationToken cancellationToken = default) => Task.FromResult(_commands.Single(item => item.Id == commandId).TryMarkPublished(DateTime.UtcNow));
        public Task<bool> TryAcknowledgeAsync(Guid commandId, Guid deviceId, string outcome, DateTime occurredAtUtc, string nonce, string details, CancellationToken cancellationToken = default)
        {
            var command = _commands.SingleOrDefault(item => item.Id == commandId && item.DeviceId == deviceId);
            return Task.FromResult(command is not null && command.TryAcknowledge(outcome, occurredAtUtc, nonce, details));
        }
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
