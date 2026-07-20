using BuildingBlocks.Infrastructure;
using Identity.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var provider = configuration["DatabaseProvider"] ?? throw new InvalidOperationException("DatabaseProvider is required.");
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");
        var mode = Enum.TryParse<DataAccessMode>(configuration["DataAccess:Mode"], ignoreCase: true, out var configuredMode)
            ? configuredMode
            : DataAccessMode.DapperStoredProcedure;

        if (mode == DataAccessMode.EfCore)
        {
            services.AddDbContext<IdentityDbContext>(options =>
            {
                _ = provider switch
                {
                    "SqlServer" => options.UseSqlServer(connectionString),
                    "PostgreSql" => options.UseNpgsql(connectionString),
                    _ => throw new NotSupportedException($"Database provider '{provider}' is not supported.")
                };

                if (bool.TryParse(configuration["LogSql"], out var logSql) && logSql)
                    options.LogTo(Console.WriteLine, LogLevel.Information);
            });
            services.AddScoped<IUserRepository, EfUserRepository>();
        }
        else
        {
            services.AddSingleton<DbConnectionFactory>();
            _ = mode switch
            {
                DataAccessMode.DapperStoredProcedure => services.AddScoped<IUserRepository, DapperUserRepository>(),
                DataAccessMode.AdoNetStoredProcedure => services.AddScoped<IUserRepository, AdoNetUserRepository>(),
                _ => throw new NotSupportedException($"Data access mode '{mode}' is not supported.")
            };
        }

        var redisConfiguration = configuration["Redis:Configuration"];
        if (string.IsNullOrWhiteSpace(redisConfiguration))
            throw new InvalidOperationException("Redis:Configuration is required. Redis is mandatory for OTPs and security controls.");

        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConfiguration));
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ISmsService, LocalDevelopmentSmsService>();
        services.AddSingleton<IEmailService, LocalDevelopmentEmailService>();
        services.AddSingleton<RedisOtpService>();
        services.AddSingleton<IOtpService>(serviceProvider => serviceProvider.GetRequiredService<RedisOtpService>());
        services.AddSingleton<IEmailOtpService>(serviceProvider => serviceProvider.GetRequiredService<RedisOtpService>());
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddSingleton<ISystemAdministratorBootstrapPolicy, ConfigurationSystemAdministratorBootstrapPolicy>();

        return services;
    }
}
