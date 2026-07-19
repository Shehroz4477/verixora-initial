using BuildingBlocks.Infrastructure;
using Devices.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Devices.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddDevicesInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IControllerProvisioningTokenService, ControllerProvisioningTokenService>();
        services.AddSingleton<IControllerAttestationVerifier, DevelopmentOnlyControllerAttestationVerifier>();
        var mode = Enum.TryParse<DataAccessMode>(configuration["DataAccess:Mode"], ignoreCase: true, out var configuredMode)
            ? configuredMode
            : DataAccessMode.DapperStoredProcedure;

        if (mode == DataAccessMode.EfCore)
        {
            var provider = configuration["DatabaseProvider"] ?? throw new InvalidOperationException("DatabaseProvider is required.");
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

            services.AddDbContext<DevicesDbContext>(options => _ = provider switch
            {
                "SqlServer" => options.UseSqlServer(connectionString),
                "MySql" => options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)),
                "PostgreSql" => options.UseNpgsql(connectionString),
                "Sqlite" => options.UseSqlite(connectionString),
                _ => throw new NotSupportedException($"Database provider '{provider}' is not supported.")
            });
            services.AddScoped<IDeviceRepository, EfDeviceRepository>();
        }
        else
        {
            services.AddSingleton<DbConnectionFactory>();
            _ = mode switch
            {
                DataAccessMode.DapperStoredProcedure => services.AddScoped<IDeviceRepository, DapperDeviceRepository>(),
                DataAccessMode.AdoNetStoredProcedure => services.AddScoped<IDeviceRepository, AdoNetDeviceRepository>(),
                _ => throw new NotSupportedException($"Data access mode '{mode}' is not supported.")
            };
        }

        return services;
    }
}
