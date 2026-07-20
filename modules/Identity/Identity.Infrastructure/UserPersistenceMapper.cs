using Identity.Domain;

namespace Identity.Infrastructure;

internal sealed class PersistedUser
{
    public Guid Id { get; set; }
    public string PhoneNumber { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string? Email { get; set; }
    public bool EmailVerified { get; set; }
    public string Role { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
    public Guid? TrustedDeviceId { get; set; }
    public Guid? TrustedDeviceUserId { get; set; }
    public string? TrustedDeviceDeviceId { get; set; }
    public string? TrustedDeviceFingerprint { get; set; }
    public string? TrustedDevicePublicKeySpkiBase64 { get; set; }
    public string? TrustedDevicePublicKeyThumbprint { get; set; }
    public DateTime? TrustedDeviceRegisteredAtUtc { get; set; }
    public bool? TrustedDeviceIsActive { get; set; }

    public User ToDomain()
    {
        if (!Enum.TryParse<UserRole>(Role, ignoreCase: false, out var role))
            throw new InvalidOperationException($"Stored identity role '{Role}' is invalid.");

        TrustedDevice? device = null;
        if (TrustedDeviceId.HasValue)
        {
            if (!TrustedDeviceUserId.HasValue || string.IsNullOrWhiteSpace(TrustedDeviceDeviceId) ||
                string.IsNullOrWhiteSpace(TrustedDeviceFingerprint) || !TrustedDeviceRegisteredAtUtc.HasValue ||
                !TrustedDeviceIsActive.HasValue)
            {
                throw new InvalidOperationException("Stored trusted-device record is incomplete.");
            }

            device = TrustedDevice.Rehydrate(
                TrustedDeviceId.Value,
                TrustedDeviceUserId.Value,
                TrustedDeviceDeviceId,
                TrustedDeviceFingerprint,
                TrustedDeviceRegisteredAtUtc.Value,
                TrustedDeviceIsActive.Value,
                TrustedDevicePublicKeySpkiBase64,
                TrustedDevicePublicKeyThumbprint);
        }

        return User.Rehydrate(Id, PhoneNumber, PasswordHash, Email, EmailVerified, role, CreatedAtUtc, device);
    }

    public static object ToParameters(User user)
    {
        var device = user.TrustedDevice;
        return new
        {
            user.Id,
            user.PhoneNumber,
            user.PasswordHash,
            user.Email,
            user.EmailVerified,
            Role = user.Role.ToString(),
            CreatedAtUtc = user.CreatedAt,
            TrustedDeviceId = device?.Id,
            TrustedDeviceDeviceId = device?.DeviceId,
            TrustedDeviceFingerprint = device?.DeviceFingerprint,
            TrustedDevicePublicKeySpkiBase64 = device?.DevicePublicKeySpkiBase64,
            TrustedDevicePublicKeyThumbprint = device?.DevicePublicKeyThumbprint,
            TrustedDeviceRegisteredAtUtc = device?.RegisteredAt,
            TrustedDeviceIsActive = device?.IsActive
        };
    }
}
