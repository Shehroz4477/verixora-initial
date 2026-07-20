using BuildingBlocks.Domain;
using Identity.Application;
using Identity.Domain;
using System.Security.Cryptography;
using Xunit;

namespace Verixora.Identity.Application.Tests;

public sealed class AuthenticationHandlerTests
{
    [Fact]
    public async Task Registration_persists_a_user_with_its_trusted_device()
    {
        var repository = new InMemoryUserRepository();
        var otpService = new TestOtpService { RegistrationOtpIsValid = true };
        var handler = new RegisterUserCommandHandler(repository, new TestPasswordHasher(), otpService);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        var thumbprint = TrustedDevicePublicKey.ValidateAndGetThumbprint(publicKey);

        var result = await handler.Handle(
            new RegisterUserCommand("+923001234567", "Password!1", "Password!1", "123456", "device-1", thumbprint, publicKey),
            TestContext.Current.CancellationToken);

        var user = Assert.Single(repository.Users);
        Assert.Equal(result.UserId, user.Id);
        Assert.Equal("device-1", user.TrustedDevice!.DeviceId);
        Assert.Equal(thumbprint, user.TrustedDevice.DeviceFingerprint);
        Assert.Equal(publicKey, user.TrustedDevice.DevicePublicKeySpkiBase64);
    }

    [Fact]
    public async Task Login_from_an_unregistered_device_is_rejected_before_otp_validation()
    {
        var repository = new InMemoryUserRepository();
        var user = new User("+923001234567", "hash:Password!1");
        user.RegisterTrustedDevice("device-1", "thumbprint-1");
        await repository.AddAsync(user, TestContext.Current.CancellationToken);
        var handler = new LoginCommandHandler(repository, new TestPasswordHasher(), new TestOtpService { LoginOtpIsValid = true }, new TestJwtGenerator());

        var exception = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(
            new LoginCommand("+923001234567", "Password!1", "123456", "device-2", "thumbprint-2"),
            TestContext.Current.CancellationToken));

        Assert.Equal("This account can only be used from its registered mobile device.", exception.Message);
    }

    [Fact]
    public async Task Registration_rejects_a_fingerprint_that_does_not_match_the_p256_device_key()
    {
        var handler = new RegisterUserCommandHandler(new InMemoryUserRepository(), new TestPasswordHasher(), new TestOtpService { RegistrationOtpIsValid = true });
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());

        var exception = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(
            new RegisterUserCommand("+923001234567", "Password!1", "Password!1", "123456", "device-1", "wrong-thumbprint", publicKey),
            TestContext.Current.CancellationToken));

        Assert.Equal("The mobile device fingerprint does not match its public key.", exception.Message);
    }

    [Fact]
    public async Task Login_recovers_the_key_after_reinstall_only_on_the_same_registered_device()
    {
        var repository = new InMemoryUserRepository();
        var user = new User("+923001234567", "hash:Password!1");
        user.RegisterTrustedDevice("device-1", "old-fingerprint");
        await repository.AddAsync(user, TestContext.Current.CancellationToken);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        var thumbprint = TrustedDevicePublicKey.ValidateAndGetThumbprint(publicKey);
        var handler = new LoginCommandHandler(repository, new TestPasswordHasher(), new TestOtpService { LoginOtpIsValid = true }, new TestJwtGenerator());

        await handler.Handle(new LoginCommand("+923001234567", "Password!1", "123456", "device-1", thumbprint, publicKey), TestContext.Current.CancellationToken);

        Assert.Equal(thumbprint, user.TrustedDevice!.DeviceFingerprint);
        Assert.Equal(publicKey, user.TrustedDevice.DevicePublicKeySpkiBase64);
    }

    private sealed class InMemoryUserRepository : IUserRepository
    {
        public List<User> Users { get; } = [];

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(Users.SingleOrDefault(user => user.Id == id));

        public Task<User?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default)
            => Task.FromResult(Users.SingleOrDefault(user => user.PhoneNumber == phoneNumber));

        public Task<bool> PhoneNumberExistsAsync(string phoneNumber, CancellationToken cancellationToken = default)
            => Task.FromResult(Users.Any(user => user.PhoneNumber == phoneNumber));

        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
            => Task.FromResult(Users.SingleOrDefault(user => user.Email == email.ToLowerInvariant()));

        public Task AddAsync(User user, CancellationToken cancellationToken = default)
        {
            Users.Add(user);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(User user, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class TestPasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => $"hash:{password}";
        public bool Verify(string password, string hash) => hash == $"hash:{password}";
    }

    private sealed class TestOtpService : IOtpService
    {
        public bool RegistrationOtpIsValid { get; init; }
        public bool LoginOtpIsValid { get; init; }
        public Task SendRegistrationOtpAsync(string phoneNumber) => Task.CompletedTask;
        public Task<bool> ValidateRegistrationOtpAsync(string phoneNumber, string otp) => Task.FromResult(RegistrationOtpIsValid);
        public Task SendLoginOtpAsync(string phoneNumber) => Task.CompletedTask;
        public Task<bool> ValidateLoginOtpAsync(string phoneNumber, string otp) => Task.FromResult(LoginOtpIsValid);
    }

    private sealed class TestJwtGenerator : IJwtTokenGenerator
    {
        public string GenerateToken(Guid userId, string phoneNumber, string role) => "test-jwt";
    }
}
