using BuildingBlocks.Domain;

namespace Identity.Domain;

public class User : Entity, IAggregateRoot
{
    public string PhoneNumber { get; private set; }
    public string PasswordHash { get; private set; }
    public string? Email { get; private set; }
    public bool EmailVerified { get; private set; }
    public UserRole Role { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public TrustedDevice? TrustedDevice { get; private set; }
    public ICollection<FaceEmbedding> FaceEmbeddings { get; private set; } = new List<FaceEmbedding>();

    // EF Core parameterless constructor
    private User()
    {
        PhoneNumber = null!;
        PasswordHash = null!;
    }

    public User(string phoneNumber, string passwordHash, UserRole role = UserRole.Owner)
    {
        Id = Guid.NewGuid();
        PhoneNumber = phoneNumber ?? throw new ArgumentNullException(nameof(phoneNumber));
        PasswordHash = passwordHash ?? throw new ArgumentNullException(nameof(passwordHash));
        Role = role;
        CreatedAt = DateTime.UtcNow;
        EmailVerified = false;
        Email = null;
    }

    /// <summary>
    /// Rebuilds an aggregate read from a persistence boundary without allowing
    /// infrastructure code to mutate individual domain properties.
    /// </summary>
    public static User Rehydrate(
        Guid id,
        string phoneNumber,
        string passwordHash,
        string? email,
        bool emailVerified,
        UserRole role,
        DateTime createdAt,
        TrustedDevice? trustedDevice = null)
    {
        var user = new User
        {
            Id = id,
            PhoneNumber = phoneNumber,
            PasswordHash = passwordHash,
            Email = email,
            EmailVerified = emailVerified,
            Role = role,
            CreatedAt = createdAt,
            TrustedDevice = trustedDevice
        };

        return user;
    }

    public void SetEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new DomainException("Email cannot be empty.");
        Email = email.ToLowerInvariant();
    }

    public void VerifyEmail()
    {
        if (string.IsNullOrWhiteSpace(Email))
            throw new DomainException("Email must be set before verification.");
        EmailVerified = true;
        AddDomainEvent(new EmailVerifiedDomainEvent(Id, Email));
    }

    public void RegisterTrustedDevice(string deviceId, string fingerprint, string? devicePublicKeySpkiBase64 = null)
    {
        if (TrustedDevice is not null && TrustedDevice.IsActive)
            throw new DomainException("A trusted device is already registered. Contact support to switch devices.");

        var device = new TrustedDevice(Id, deviceId, fingerprint, devicePublicKeySpkiBase64);
        TrustedDevice = device;
        AddDomainEvent(new TrustedDeviceRegisteredDomainEvent(Id, deviceId));
    }

    public void DeactivateTrustedDevice()
    {
        TrustedDevice?.Deactivate();
    }

    public void RefreshTrustedDevicePublicKey(string deviceId, string deviceFingerprint, string devicePublicKeySpkiBase64)
    {
        if (TrustedDevice is not { IsActive: true } || !string.Equals(TrustedDevice.DeviceId, deviceId, StringComparison.Ordinal))
            throw new DomainException("Only the already registered mobile device can refresh its security key.");

        TrustedDevice.RotatePublicKey(deviceFingerprint, devicePublicKeySpkiBase64);
    }
}
