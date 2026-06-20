using Identity.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var provider = configuration["DatabaseProvider"] ?? "Sqlite";
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=verixora.db";

        // Register EF Core DbContext with the correct provider
        services.AddDbContext<IdentityDbContext>(options =>
        {
            _ = provider switch
            {
                "SqlServer" => options.UseSqlServer(connectionString),
                "MySql" => options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)),
                "PostgreSql" => options.UseNpgsql(connectionString),
                "Sqlite" => options.UseSqlite(connectionString),
                _ => throw new NotSupportedException($"Database provider '{provider}' is not supported.")
            };

            // Log SQL in development
            bool.TryParse(configuration["LogSql"], out var logSql);
            if (logSql)
            {
                options.LogTo(Console.WriteLine, LogLevel.Information);
            }
        });

        // Register application dependencies
        services.AddScoped<IUserRepository, EfUserRepository>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();

        return services;
    }
}
