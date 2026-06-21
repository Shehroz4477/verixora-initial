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

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(l => l.Id);
            entity.Property(l => l.UserId).IsRequired();
            entity.Property(l => l.DeviceId).IsRequired();
            entity.Property(l => l.Action).IsRequired().HasMaxLength(100);
            entity.Property(l => l.Timestamp).IsRequired();
            entity.Property(l => l.Result).IsRequired();
            entity.Property(l => l.Details).HasMaxLength(1000);
        });
    }
}
