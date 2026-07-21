using Authorization.Domain;
using BuildingBlocks.Infrastructure;
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
        services.AddSingleton<DbConnectionFactory>();
        var mode = Enum.TryParse<DataAccessMode>(configuration["DataAccess:Mode"], ignoreCase: true, out var configuredMode)
            ? configuredMode
            : DataAccessMode.DapperStoredProcedure;

        if (mode == DataAccessMode.EfCore)
        {
            var provider = configuration["DatabaseProvider"] ?? throw new InvalidOperationException("DatabaseProvider is required.");
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

            services.AddDbContext<SmartLocksDbContext>(options =>
            {
                _ = provider switch
                {
                    "SqlServer" => options.UseSqlServer(connectionString),
                    "PostgreSql" => options.UseNpgsql(connectionString),
                    _ => throw new NotSupportedException($"Database provider '{provider}' is not supported for this module.")
                };

                if (bool.TryParse(configuration["LogSql"], out var logSql) && logSql)
                    options.LogTo(Console.WriteLine, LogLevel.Information);
            });
            services.AddScoped<ISmartLockRepository, EfSmartLockRepository>();
            services.AddScoped<ILockCommandRepository, EfLockCommandRepository>();
        }
        else
        {
            _ = mode switch
            {
                DataAccessMode.DapperStoredProcedure => services.AddScoped<ISmartLockRepository, DapperSmartLockRepository>().AddScoped<ILockCommandRepository, DapperLockCommandRepository>(),
                DataAccessMode.AdoNetStoredProcedure => services.AddScoped<ISmartLockRepository, AdoSmartLockRepository>().AddScoped<ILockCommandRepository, AdoLockCommandRepository>(),
                _ => throw new NotSupportedException($"Data access mode '{mode}' is not supported.")
            };
        }

        // MQTT Publisher (singleton – one connection per app)
        var brokerHost = configuration["Mqtt:Host"] ?? "localhost";
        var brokerPort = int.Parse(configuration["Mqtt:Port"] ?? "1883");
        services.AddSingleton<IMqttPublisher>(sp => new MqttPublisher(brokerHost, brokerPort));
        services.AddHostedService<LockCommandOutboxDispatcher>();

        services.AddSingleton<IAuthorizationService, ScheduleBasedAuthorizationService>();
        services.AddSingleton<IFaceTemplateProtector, AesGcmFaceTemplateProtector>();
        _ = mode switch
        {
            DataAccessMode.AdoNetStoredProcedure => services.AddScoped<IFaceTemplateRepository, AdoNetFaceTemplateRepository>(),
            _ => services.AddScoped<IFaceTemplateRepository, DapperFaceTemplateRepository>()
        };
        services.AddHttpClient<IFaceVerificationProvider, PythonFaceVerificationProvider>(client =>
        {
            client.BaseAddress = new Uri(configuration["FaceService:BaseUrl"] ?? "http://localhost:5001");
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        return services;
    }
}
