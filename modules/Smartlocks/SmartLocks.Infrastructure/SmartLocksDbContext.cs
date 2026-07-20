using Microsoft.EntityFrameworkCore;
using SmartLocks.Domain;

namespace SmartLocks.Infrastructure;

public class SmartLocksDbContext : DbContext
{
    public DbSet<SmartLock> SmartLocks => Set<SmartLock>();
    public DbSet<LockCommand> LockCommands => Set<LockCommand>();

    public SmartLocksDbContext(DbContextOptions<SmartLocksDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("smartlocks");
        var isPostgreSql = Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL";

        modelBuilder.Entity<SmartLock>(entity =>
        {
            entity.ToTable(isPostgreSql ? "smart_locks" : "SmartLocks");
            entity.HasKey(l => l.Id);
            entity.Property(l => l.Id).HasColumnName(isPostgreSql ? "id" : "Id");
            entity.Property(l => l.Name).HasColumnName(isPostgreSql ? "name" : "Name").IsRequired().HasMaxLength(100);
            entity.Property(l => l.DeviceId).HasColumnName(isPostgreSql ? "device_id" : "DeviceId").IsRequired();
            entity.HasIndex(l => l.DeviceId).IsUnique();
            entity.Property(l => l.HomeId).HasColumnName(isPostgreSql ? "home_id" : "HomeId").IsRequired();
            entity.Property(l => l.Status)
                  .HasColumnName(isPostgreSql ? "status" : "Status")
                  .IsRequired()
                  .HasConversion<string>()
                  .HasMaxLength(20);
            entity.Property(l => l.RequiresFace).HasColumnName(isPostgreSql ? "requires_face" : "RequiresFace").IsRequired();
            entity.Property(l => l.LastUnlockedAt).HasColumnName(isPostgreSql ? "last_unlocked_at_utc" : "LastUnlockedAtUtc");
            entity.Property(l => l.LastUnlockedBy).HasColumnName(isPostgreSql ? "last_unlocked_by" : "LastUnlockedBy");
        });

        modelBuilder.Entity<LockCommand>(entity =>
        {
            entity.ToTable(isPostgreSql ? "lock_commands" : "LockCommands");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName(isPostgreSql ? "id" : "Id");
            entity.Property(item => item.LockId).HasColumnName(isPostgreSql ? "lock_id" : "LockId");
            entity.Property(item => item.DeviceId).HasColumnName(isPostgreSql ? "device_id" : "DeviceId");
            entity.Property(item => item.HomeId).HasColumnName(isPostgreSql ? "home_id" : "HomeId");
            entity.Property(item => item.RequestedBy).HasColumnName(isPostgreSql ? "requested_by" : "RequestedBy");
            entity.Property(item => item.IdempotencyKey).HasColumnName(isPostgreSql ? "idempotency_key" : "IdempotencyKey").HasMaxLength(128);
            entity.Property(item => item.CommandType).HasColumnName(isPostgreSql ? "command_type" : "CommandType").HasMaxLength(20);
            entity.Property(item => item.Status).HasColumnName(isPostgreSql ? "status" : "Status").HasConversion<string>().HasMaxLength(20);
            entity.Property(item => item.RequestedAtUtc).HasColumnName(isPostgreSql ? "requested_at_utc" : "RequestedAtUtc");
            entity.Property(item => item.ExpiresAtUtc).HasColumnName(isPostgreSql ? "expires_at_utc" : "ExpiresAtUtc");
            entity.Property(item => item.PublishedAtUtc).HasColumnName(isPostgreSql ? "published_at_utc" : "PublishedAtUtc");
            entity.Property(item => item.AcknowledgedAtUtc).HasColumnName(isPostgreSql ? "acknowledged_at_utc" : "AcknowledgedAtUtc");
            entity.Property(item => item.Outcome).HasColumnName(isPostgreSql ? "outcome" : "Outcome").HasMaxLength(20);
            entity.Property(item => item.AcknowledgementNonce).HasColumnName(isPostgreSql ? "acknowledgement_nonce" : "AcknowledgementNonce").HasMaxLength(128);
            entity.Property(item => item.Details).HasColumnName(isPostgreSql ? "details" : "Details").HasMaxLength(500);
            entity.HasIndex(item => new { item.LockId, item.IdempotencyKey }).IsUnique();
        });
    }
}
