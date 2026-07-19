using Microsoft.EntityFrameworkCore;
using SmartLocks.Domain;

namespace SmartLocks.Infrastructure;

public class SmartLocksDbContext : DbContext
{
    public DbSet<SmartLock> SmartLocks => Set<SmartLock>();

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
    }
}
