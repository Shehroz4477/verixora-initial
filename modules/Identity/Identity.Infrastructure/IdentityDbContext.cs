using Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure;

public class IdentityDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<TrustedDevice> TrustedDevices => Set<TrustedDevice>();
    public DbSet<FaceEmbedding> FaceEmbeddings => Set<FaceEmbedding>();

    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("identity");

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.Property(u => u.PhoneNumber).IsRequired().HasMaxLength(20);
            entity.HasIndex(u => u.PhoneNumber).IsUnique();
            entity.Property(u => u.PasswordHash).IsRequired();
            entity.Property(u => u.Email).HasMaxLength(256);
            entity.HasIndex(u => u.Email).IsUnique().HasFilter(null); // filter varies by provider, set in migrations
            entity.Property(u => u.EmailVerified).IsRequired();
            entity.Property(u => u.Role).IsRequired().HasConversion<string>().HasMaxLength(20);
            entity.Property(u => u.CreatedAt).IsRequired();

            entity.HasOne(u => u.TrustedDevice)
                  .WithOne(d => d.User)
                  .HasForeignKey<TrustedDevice>(d => d.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany<FaceEmbedding>()
                  .WithOne(f => f.User)
                  .HasForeignKey(f => f.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TrustedDevice>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.Property(d => d.DeviceId).IsRequired().HasMaxLength(256);
            entity.Property(d => d.DeviceFingerprint).IsRequired().HasMaxLength(512);
            entity.Property(d => d.RegisteredAt).IsRequired();
            entity.Property(d => d.IsActive).IsRequired();
        });

        modelBuilder.Entity<FaceEmbedding>(entity =>
        {
            entity.HasKey(f => f.Id);
            entity.Property(f => f.EmbeddingCiphertext).IsRequired();
            entity.Property(f => f.IV).IsRequired();
            entity.Property(f => f.CreatedAt).IsRequired();
        });
    }
}
