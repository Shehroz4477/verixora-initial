using AuditLogs.Domain;
using Microsoft.EntityFrameworkCore;

namespace AuditLogs.Infrastructure;

public class AuditLogsDbContext : DbContext
{
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public AuditLogsDbContext(DbContextOptions<AuditLogsDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("auditlogs");
        var isPostgreSql = Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL";

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable(isPostgreSql ? "audit_logs" : "AuditLogs");
            entity.HasKey(l => l.Id);
            entity.Property(l => l.Id).HasColumnName(isPostgreSql ? "id" : "Id");
            entity.Property(l => l.HomeId).HasColumnName(isPostgreSql ? "home_id" : "HomeId").IsRequired();
            entity.Property(l => l.UserId).HasColumnName(isPostgreSql ? "user_id" : "UserId").IsRequired();
            entity.Property(l => l.DeviceId).HasColumnName(isPostgreSql ? "device_id" : "DeviceId").IsRequired();
            entity.Property(l => l.Action).HasColumnName(isPostgreSql ? "action" : "Action").IsRequired().HasMaxLength(100);
            entity.Property(l => l.Timestamp).HasColumnName(isPostgreSql ? "timestamp_utc" : "TimestampUtc").IsRequired();
            entity.Property(l => l.Result).HasColumnName(isPostgreSql ? "result" : "Result").IsRequired();
            entity.Property(l => l.Details).HasColumnName(isPostgreSql ? "details" : "Details").HasMaxLength(1000);
            entity.HasIndex(l => new { l.HomeId, l.Timestamp });
        });
    }
}
