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
        var isPostgreSql = Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL";

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable(isPostgreSql ? "users" : "Users");
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Id).HasColumnName(isPostgreSql ? "id" : "Id");
            entity.Property(u => u.PhoneNumber).IsRequired().HasMaxLength(20);
            entity.Property(u => u.PhoneNumber).HasColumnName(isPostgreSql ? "phone_number" : "PhoneNumber");
            entity.HasIndex(u => u.PhoneNumber).IsUnique();
            entity.Property(u => u.PasswordHash).IsRequired();
            entity.Property(u => u.PasswordHash).HasColumnName(isPostgreSql ? "password_hash" : "PasswordHash");
            entity.Property(u => u.Email).HasMaxLength(256);
            entity.Property(u => u.Email).HasColumnName(isPostgreSql ? "email" : "Email");
            entity.HasIndex(u => u.Email).IsUnique().HasFilter(null); // filter varies by provider, set in migrations
            entity.Property(u => u.EmailVerified).IsRequired();
            entity.Property(u => u.EmailVerified).HasColumnName(isPostgreSql ? "email_verified" : "EmailVerified");
            entity.Property(u => u.Role).IsRequired().HasConversion<string>().HasMaxLength(20);
            entity.Property(u => u.Role).HasColumnName(isPostgreSql ? "role" : "Role");
            entity.Property(u => u.CreatedAt).IsRequired();
            entity.Property(u => u.CreatedAt).HasColumnName(isPostgreSql ? "created_at_utc" : "CreatedAtUtc");

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
            entity.ToTable(isPostgreSql ? "trusted_devices" : "TrustedDevices");
            entity.HasKey(d => d.Id);
            entity.Property(d => d.Id).HasColumnName(isPostgreSql ? "id" : "Id");
            entity.Property(d => d.UserId).HasColumnName(isPostgreSql ? "user_id" : "UserId");
            entity.Property(d => d.DeviceId).IsRequired().HasMaxLength(256);
            entity.Property(d => d.DeviceId).HasColumnName(isPostgreSql ? "device_id" : "DeviceId");
            entity.Property(d => d.DeviceFingerprint).IsRequired().HasMaxLength(512);
            entity.Property(d => d.DeviceFingerprint).HasColumnName(isPostgreSql ? "device_fingerprint" : "DeviceFingerprint");
            entity.Property(d => d.RegisteredAt).IsRequired();
            entity.Property(d => d.RegisteredAt).HasColumnName(isPostgreSql ? "registered_at_utc" : "RegisteredAtUtc");
            entity.Property(d => d.IsActive).IsRequired();
            entity.Property(d => d.IsActive).HasColumnName(isPostgreSql ? "is_active" : "IsActive");
        });

        modelBuilder.Entity<FaceEmbedding>(entity =>
        {
            entity.ToTable(isPostgreSql ? "face_embeddings" : "FaceEmbeddings");
            entity.HasKey(f => f.Id);
            entity.Property(f => f.Id).HasColumnName(isPostgreSql ? "id" : "Id");
            entity.Property(f => f.UserId).HasColumnName(isPostgreSql ? "user_id" : "UserId");
            entity.Property(f => f.EmbeddingCiphertext).IsRequired();
            entity.Property(f => f.EmbeddingCiphertext).HasColumnName(isPostgreSql ? "embedding_ciphertext" : "EmbeddingCiphertext");
            entity.Property(f => f.IV).IsRequired();
            entity.Property(f => f.IV).HasColumnName(isPostgreSql ? "iv" : "Iv");
            entity.Property(f => f.CreatedAt).IsRequired();
            entity.Property(f => f.CreatedAt).HasColumnName(isPostgreSql ? "created_at_utc" : "CreatedAtUtc");
        });
    }
}
