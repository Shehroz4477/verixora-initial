using Devices.Domain;

namespace Devices.Application;

public interface IDeviceRepository
{
    Task<Device?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Device?> GetByHardwareIdAsync(string hardwareId, CancellationToken cancellationToken = default);
    Task<List<Device>> GetByHomeIdAsync(Guid homeId, CancellationToken cancellationToken = default);
    Task AddAsync(Device device, CancellationToken cancellationToken = default);
    Task UpdateAsync(Device device, CancellationToken cancellationToken = default);
    Task<bool> TryCompleteProvisioningAsync(Guid deviceId, string provisioningTokenHash, string publicKeyThumbprint, string publicKeySpkiBase64, string attestationSubject, CancellationToken cancellationToken = default);
}
