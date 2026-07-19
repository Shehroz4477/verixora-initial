using BuildingBlocks.Infrastructure;
using Homes.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Homes.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddHomesInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<DbConnectionFactory>();

        var mode = Enum.TryParse<DataAccessMode>(configuration["DataAccess:Mode"], ignoreCase: true, out var configuredMode)
            ? configuredMode
            : DataAccessMode.DapperStoredProcedure;

        _ = mode switch
        {
            DataAccessMode.DapperStoredProcedure => services.AddScoped<IHomeRepository, DapperHomeRepository>(),
            DataAccessMode.AdoNetStoredProcedure => services.AddScoped<IHomeRepository, AdoNetHomeRepository>(),
            DataAccessMode.EfCore => throw new NotSupportedException("Homes EF Core mode has not been enabled. Use DapperStoredProcedure or AdoNetStoredProcedure."),
            _ => throw new NotSupportedException($"Data access mode '{mode}' is not supported.")
        };

        return services;
    }
}
