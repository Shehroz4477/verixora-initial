using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using MySql.Data.MySqlClient;
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

    public IDbConnection CreateConnection() => _provider switch
    {
        "SqlServer" => new SqlConnection(_connectionString),
        "MySql" => new MySqlConnection(_connectionString),
        "PostgreSql" => new NpgsqlConnection(_connectionString),
        "Sqlite" => new SqliteConnection(_connectionString),
        _ => throw new NotSupportedException($"Database provider '{_provider}' is not supported.")
    };
}