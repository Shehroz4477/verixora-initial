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

        modelBuilder.Entity<SmartLock>(entity =>
        {
            entity.HasKey(l => l.Id);
            entity.Property(l => l.Name).IsRequired().HasMaxLength(100);
            entity.Property(l => l.DeviceId).IsRequired();
            entity.Property(l => l.HomeId).IsRequired();
            entity.Property(l => l.Status)
                  .IsRequired()
                  .HasConversion<string>()
                  .HasMaxLength(20);
            entity.Property(l => l.RequiresFace).IsRequired();
            entity.Property(l => l.LastUnlockedAt);
            entity.Property(l => l.LastUnlockedBy);
        });
    }
}
