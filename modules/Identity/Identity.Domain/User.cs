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

    public void RegisterTrustedDevice(string deviceId, string fingerprint)
    {
        if (TrustedDevice is not null && TrustedDevice.IsActive)
            throw new DomainException("A trusted device is already registered. Contact support to switch devices.");

        var device = new TrustedDevice(Id, deviceId, fingerprint);
        TrustedDevice = device;
        AddDomainEvent(new TrustedDeviceRegisteredDomainEvent(Id, deviceId));
    }

    public void DeactivateTrustedDevice()
    {
        TrustedDevice?.Deactivate();
    }
}
