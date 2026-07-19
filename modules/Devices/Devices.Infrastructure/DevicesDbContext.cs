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
        var isPostgreSql = Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL";

        modelBuilder.Entity<Device>(entity =>
        {
            entity.ToTable(isPostgreSql ? "devices" : "Devices");
            entity.HasKey(device => device.Id);
            entity.Property(device => device.Id).HasColumnName(isPostgreSql ? "id" : "Id");
            entity.Property(device => device.Name).HasColumnName(isPostgreSql ? "name" : "Name").IsRequired().HasMaxLength(100);
            entity.Property(device => device.HomeId).HasColumnName(isPostgreSql ? "home_id" : "HomeId").IsRequired();
            entity.Property(device => device.HardwareId).HasColumnName(isPostgreSql ? "hardware_id" : "HardwareId").IsRequired().HasMaxLength(128);
            entity.HasIndex(device => device.HardwareId).IsUnique();
            entity.Property(device => device.MqttTopic).HasColumnName(isPostgreSql ? "mqtt_topic" : "MqttTopic").IsRequired().HasMaxLength(256);
            entity.Property(device => device.Status).HasColumnName(isPostgreSql ? "status" : "Status").IsRequired().HasConversion<string>().HasMaxLength(20);
            entity.Property(device => device.CreatedAt).HasColumnName(isPostgreSql ? "created_at_utc" : "CreatedAtUtc").IsRequired();
            entity.Property(device => device.ProvisioningTokenHash).HasColumnName(isPostgreSql ? "provisioning_token_hash" : "ProvisioningTokenHash").HasMaxLength(128);
            entity.Property(device => device.ProvisioningExpiresAt).HasColumnName(isPostgreSql ? "provisioning_expires_at_utc" : "ProvisioningExpiresAtUtc");
            entity.Property(device => device.ControllerPublicKeyThumbprint).HasColumnName(isPostgreSql ? "controller_public_key_thumbprint" : "ControllerPublicKeyThumbprint").HasMaxLength(128);
            entity.Property(device => device.HardwareAttestationSubject).HasColumnName(isPostgreSql ? "hardware_attestation_subject" : "HardwareAttestationSubject").HasMaxLength(256);
            entity.Property(device => device.ProvisionedAt).HasColumnName(isPostgreSql ? "provisioned_at_utc" : "ProvisionedAtUtc");
        });
    }
}
