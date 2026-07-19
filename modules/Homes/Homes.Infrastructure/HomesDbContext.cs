using Homes.Domain;
using Microsoft.EntityFrameworkCore;

namespace Homes.Infrastructure;

public sealed class HomesDbContext(DbContextOptions<HomesDbContext> options) : DbContext(options)
{
    public DbSet<Home> Homes => Set<Home>();
    public DbSet<HomeMember> HomeMembers => Set<HomeMember>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("homes");
        var isPostgreSql = Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL";

        modelBuilder.Entity<Home>(entity =>
        {
            entity.ToTable(isPostgreSql ? "homes" : "Homes");
            entity.HasKey(home => home.Id);
            entity.Property(home => home.Id).HasColumnName(isPostgreSql ? "id" : "Id");
            entity.Property(home => home.Name).HasMaxLength(150).IsRequired();
            entity.Property(home => home.Name).HasColumnName(isPostgreSql ? "name" : "Name");
            entity.Property(home => home.OwnerId).HasColumnName(isPostgreSql ? "owner_id" : "OwnerId").IsRequired();
            entity.Property(home => home.CreatedAt).HasColumnName(isPostgreSql ? "created_at_utc" : "CreatedAtUtc").IsRequired();
            entity.Property(home => home.MaxDevices).HasColumnName(isPostgreSql ? "max_devices" : "MaxDevices").IsRequired();

            entity.HasMany(home => home.Members)
                .WithOne(member => member.Home)
                .HasForeignKey(member => member.HomeId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(home => home.Members)
                .HasField("_members")
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<HomeMember>(entity =>
        {
            entity.ToTable(isPostgreSql ? "home_members" : "HomeMembers");
            entity.HasKey(member => member.Id);
            entity.Property(member => member.Id).HasColumnName(isPostgreSql ? "id" : "Id");
            entity.Property(member => member.HomeId).HasColumnName(isPostgreSql ? "home_id" : "HomeId").IsRequired();
            entity.Property(member => member.UserId).HasColumnName(isPostgreSql ? "user_id" : "UserId").IsRequired();
            entity.Property(member => member.Role).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(member => member.Role).HasColumnName(isPostgreSql ? "role" : "Role");
            entity.Property(member => member.JoinedAt).HasColumnName(isPostgreSql ? "joined_at_utc" : "JoinedAtUtc").IsRequired();
            entity.HasIndex(member => new { member.HomeId, member.UserId }).IsUnique();
        });
    }
}
