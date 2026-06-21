using AuditLogs.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AuditLogs.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAuditLogsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var provider = configuration["DatabaseProvider"] ?? "Sqlite";
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=verixora.db";

        services.AddDbContext<AuditLogsDbContext>(options =>
        {
            _ = provider switch
            {
                "SqlServer" => options.UseSqlServer(connectionString),
                "MySql" => options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)),
                "PostgreSql" => options.UseNpgsql(connectionString),
                "Sqlite" => options.UseSqlite(connectionString),
                _ => throw new NotSupportedException($"Database provider '{provider}' is not supported.")
            };

            bool.TryParse(configuration["LogSql"], out var logSql);
            if (logSql)
                options.LogTo(Console.WriteLine, LogLevel.Information);
        });

        services.AddScoped<IAuditLogRepository, EfAuditLogRepository>();

        return services;
    }
}
