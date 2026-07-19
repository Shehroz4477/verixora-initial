using AuditLogs.Application;
using BuildingBlocks.Infrastructure;
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
        var mode = Enum.TryParse<DataAccessMode>(configuration["DataAccess:Mode"], ignoreCase: true, out var configuredMode)
            ? configuredMode
            : DataAccessMode.DapperStoredProcedure;

        if (mode == DataAccessMode.EfCore)
        {
            var provider = configuration["DatabaseProvider"] ?? throw new InvalidOperationException("DatabaseProvider is required.");
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

            services.AddDbContext<AuditLogsDbContext>(options =>
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
            services.AddScoped<IAuditLogRepository, EfAuditLogRepository>();
        }
        else
        {
            services.AddSingleton<DbConnectionFactory>();
            _ = mode switch
            {
                DataAccessMode.DapperStoredProcedure => services.AddScoped<IAuditLogRepository, DapperAuditLogRepository>(),
                DataAccessMode.AdoNetStoredProcedure => services.AddScoped<IAuditLogRepository, AdoNetAuditLogRepository>(),
                _ => throw new NotSupportedException($"Data access mode '{mode}' is not supported.")
            };
        }

        return services;
    }
}
