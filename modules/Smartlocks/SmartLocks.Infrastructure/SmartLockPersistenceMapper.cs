using SmartLocks.Domain;

namespace SmartLocks.Infrastructure;

internal sealed class PersistedSmartLock
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public Guid HomeId { get; set; }
    public string Name { get; set; } = null!;
    public string Status { get; set; } = null!;
    public bool RequiresFace { get; set; }
    public DateTime? LastUnlockedAtUtc { get; set; }
    public Guid? LastUnlockedBy { get; set; }

    public SmartLock ToDomain()
    {
        if (!Enum.TryParse<LockStatus>(Status, ignoreCase: false, out var status))
            throw new InvalidOperationException($"Stored smart-lock status '{Status}' is invalid.");

        return SmartLock.Rehydrate(Id, Name, DeviceId, HomeId, status, RequiresFace, LastUnlockedAtUtc, LastUnlockedBy);
    }

    public static object ToCreateParameters(SmartLock smartLock) => new
    {
        smartLock.Id,
        smartLock.DeviceId,
        smartLock.HomeId,
        smartLock.Name,
        Status = smartLock.Status.ToString(),
        smartLock.RequiresFace
    };
}
