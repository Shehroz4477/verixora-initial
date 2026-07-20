using System.Data.Common;
using Microsoft.Data.SqlClient;
using Npgsql;
using Microsoft.Extensions.Configuration;

namespace BuildingBlocks.Infrastructure;

public class DbConnectionFactory
{
    private readonly string _provider;
    private readonly string _connectionString;

    public DbConnectionFactory(IConfiguration configuration)
    {
        _provider = configuration["DatabaseProvider"] ?? throw new ArgumentNullException("DatabaseProvider");
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new ArgumentNullException("ConnectionStrings:DefaultConnection");
    }

    public string Provider => _provider;

    public DbConnection CreateConnection() => _provider switch
    {
        "SqlServer" => new SqlConnection(_connectionString),
        "PostgreSql" => new NpgsqlConnection(_connectionString),
        _ => throw new NotSupportedException($"Database provider '{_provider}' is not supported.")
    };
}
