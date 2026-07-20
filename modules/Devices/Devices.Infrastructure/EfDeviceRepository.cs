using Devices.Application;
using Devices.Domain;
using Microsoft.EntityFrameworkCore;

namespace Devices.Infrastructure;

public class EfDeviceRepository : IDeviceRepository
{
    private readonly DevicesDbContext _context;

    public EfDeviceRepository(DevicesDbContext context)
    {
        _context = context;
    }

    public async Task<Device?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Devices.FindAsync(new object[] { id }, cancellationToken);

    public async Task<Device?> GetByHardwareIdAsync(string hardwareId, CancellationToken cancellationToken = default)
        => await _context.Devices.SingleOrDefaultAsync(device => device.HardwareId == hardwareId.Trim().ToUpperInvariant(), cancellationToken);

    public async Task<List<Device>> GetByHomeIdAsync(Guid homeId, CancellationToken cancellationToken = default)
        => await _context.Devices.Where(d => d.HomeId == homeId).ToListAsync(cancellationToken);

    public async Task AddAsync(Device device, CancellationToken cancellationToken = default)
    {
        await _context.Devices.AddAsync(device, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task UpdateAsync(Device device, CancellationToken cancellationToken = default)
    {
        _context.Devices.Update(device);
        return _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> TryCompleteProvisioningAsync(Guid deviceId, string provisioningTokenHash, string publicKeyThumbprint, string publicKeySpkiBase64, string attestationSubject, CancellationToken cancellationToken = default)
    {
        var device = await _context.Devices.SingleOrDefaultAsync(item => item.Id == deviceId, cancellationToken);
        if (device is null)
            return false;
        try { device.CompleteProvisioning(provisioningTokenHash, publicKeyThumbprint, publicKeySpkiBase64, attestationSubject); }
        catch (BuildingBlocks.Domain.DomainException) { return false; }
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
