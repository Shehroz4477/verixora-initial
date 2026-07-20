using Microsoft.EntityFrameworkCore;
using SmartLocks.Application;
using SmartLocks.Domain;

namespace SmartLocks.Infrastructure;

public sealed class EfLockCommandRepository(SmartLocksDbContext context) : ILockCommandRepository
{
    public async Task<LockCommand> CreateOrGetAsync(LockCommand command, CancellationToken cancellationToken = default)
    {
        var existing = await context.LockCommands.SingleOrDefaultAsync(item => item.LockId == command.LockId && item.IdempotencyKey == command.IdempotencyKey, cancellationToken);
        if (existing is not null) return existing;
        await context.LockCommands.AddAsync(command, cancellationToken);
        try { await context.SaveChangesAsync(cancellationToken); return command; }
        catch (DbUpdateException)
        {
            context.Entry(command).State = EntityState.Detached;
            return await context.LockCommands.SingleAsync(item => item.LockId == command.LockId && item.IdempotencyKey == command.IdempotencyKey, cancellationToken);
        }
    }

    public Task<LockCommand?> GetByIdAsync(Guid commandId, CancellationToken cancellationToken = default)
        => context.LockCommands.SingleOrDefaultAsync(item => item.Id == commandId, cancellationToken);

    public async Task<IReadOnlyList<LockCommand>> GetQueuedForDispatchAsync(int maximum, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var expired = await context.LockCommands.Where(item => item.Status == LockCommandStatus.Queued && item.ExpiresAtUtc <= now).ToListAsync(cancellationToken);
        foreach (var item in expired) item.TryExpire(now);
        if (expired.Count > 0) await context.SaveChangesAsync(cancellationToken);
        return await context.LockCommands.Where(item => item.Status == LockCommandStatus.Queued && item.ExpiresAtUtc > now).OrderBy(item => item.RequestedAtUtc).Take(Math.Clamp(maximum, 1, 100)).ToListAsync(cancellationToken);
    }

    public async Task<bool> MarkPublishedAsync(Guid commandId, CancellationToken cancellationToken = default)
    {
        var command = await GetByIdAsync(commandId, cancellationToken);
        if (command is null || !command.TryMarkPublished(DateTime.UtcNow)) return false;
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> TryAcknowledgeAsync(Guid commandId, Guid deviceId, string outcome, DateTime occurredAtUtc, string nonce, string details, CancellationToken cancellationToken = default)
    {
        var command = await context.LockCommands.SingleOrDefaultAsync(item => item.Id == commandId && item.DeviceId == deviceId && item.AcknowledgementNonce == null, cancellationToken);
        if (command is null || !command.TryAcknowledge(outcome, occurredAtUtc, nonce, details)) return false;
        try { await context.SaveChangesAsync(cancellationToken); return true; }
        catch (DbUpdateException) { return false; }
    }
}
