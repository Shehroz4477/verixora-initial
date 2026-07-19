using BuildingBlocks.Infrastructure;
using Homes.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Homes.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddHomesInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var mode = Enum.TryParse<DataAccessMode>(configuration["DataAccess:Mode"], ignoreCase: true, out var configuredMode)
            ? configuredMode
            : DataAccessMode.DapperStoredProcedure;

        if (mode == DataAccessMode.EfCore)
        {
            var provider = configuration["DatabaseProvider"] ?? throw new InvalidOperationException("DatabaseProvider is required.");
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

            services.AddDbContext<HomesDbContext>(options => _ = provider switch
            {
                "SqlServer" => options.UseSqlServer(connectionString),
                "MySql" => options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)),
                "PostgreSql" => options.UseNpgsql(connectionString),
                "Sqlite" => options.UseSqlite(connectionString),
                _ => throw new NotSupportedException($"Database provider '{provider}' is not supported.")
            });
            services.AddScoped<IHomeRepository, EfHomeRepository>();
        }
        else
        {
            services.AddSingleton<DbConnectionFactory>();
            _ = mode switch
            {
                DataAccessMode.DapperStoredProcedure => services.AddScoped<IHomeRepository, DapperHomeRepository>(),
                DataAccessMode.AdoNetStoredProcedure => services.AddScoped<IHomeRepository, AdoNetHomeRepository>(),
                _ => throw new NotSupportedException($"Data access mode '{mode}' is not supported.")
            };
        }

        return services;
    }
}
