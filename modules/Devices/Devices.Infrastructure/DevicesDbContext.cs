using Devices.Domain;
using Microsoft.EntityFrameworkCore;

namespace Devices.Infrastructure;

public class DevicesDbContext : DbContext
{
    public DbSet<Device> Devices => Set<Device>();

    public DevicesDbContext(DbContextOptions<DevicesDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("devices");

        modelBuilder.Entity<Device>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.Property(d => d.Name).IsRequired().HasMaxLength(100);
            entity.Property(d => d.HomeId).IsRequired();
            entity.Property(d => d.MqttTopic).IsRequired().HasMaxLength(256);
            entity.Property(d => d.Status)
                  .IsRequired()
                  .HasConversion<string>()
                  .HasMaxLength(20);
            entity.Property(d => d.CreatedAt).IsRequired();
        });
    }
}
