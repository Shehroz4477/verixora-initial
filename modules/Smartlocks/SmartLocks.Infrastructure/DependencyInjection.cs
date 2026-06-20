using Authorization.Domain;
using FaceVerification.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartLocks.Application;

namespace SmartLocks.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSmartLocksInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var provider = configuration["DatabaseProvider"] ?? "Sqlite";
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=verixora.db";

        // Register EF Core DbContext
        services.AddDbContext<SmartLocksDbContext>(options =>
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
            {
                options.LogTo(Console.WriteLine, LogLevel.Information);
            }
        });

        // Choose EF or ADO.NET repository based on config
        bool.TryParse(configuration["UseEfCore"], out var useEf);
        if (useEf || string.IsNullOrWhiteSpace(configuration["UseEfCore"]))
        {
            services.AddScoped<ISmartLockRepository, EfSmartLockRepository>();
        }
        else
        {
            services.AddSingleton<BuildingBlocks.Infrastructure.DbConnectionFactory>();
            services.AddScoped<ISmartLockRepository, AdoSmartLockRepository>();
        }

        // MQTT Publisher (singleton – one connection per app)
        var brokerHost = configuration["Mqtt:Host"] ?? "localhost";
        var brokerPort = int.Parse(configuration["Mqtt:Port"] ?? "1883");
        services.AddSingleton<IMqttPublisher>(sp => new MqttPublisher(brokerHost, brokerPort));

        // Audit Log (mock for now; later replace with real AuditLogs module)
        services.AddSingleton<IAuditLogService, MockAuditLogService>();

        services.AddSingleton<IAuthorizationService, MockAuthorizationService>();
        services.AddSingleton<IFaceVerificationProvider, MockFaceVerificationProvider>();

        return services;
    }
}
