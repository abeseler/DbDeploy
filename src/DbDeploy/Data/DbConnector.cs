using Npgsql;
using System.Data;
using System.Data.SqlClient;

namespace DbDeploy.Data;

internal sealed class DbConnector(IOptions<Settings> options)
{
    private readonly string? _connectionString = options.Value.DatabaseProvider switch
    {
        "postgres" => new NpgsqlConnectionStringBuilder(options.Value.ConnectionString).ConnectionString,
        "mssql" => new SqlConnectionStringBuilder(options.Value.ConnectionString).ConnectionString,
        _ => throw new NotSupportedException("Database provider not supported.")
    };

    public async Task<IDbConnection> ConnectAsync(CancellationToken stoppingToken = default) => options.Value.DatabaseProvider switch
    {
        "postgres" => await ConnectPostgresAsync(stoppingToken),
        "mssql" => await ConnectMssqlAsync(stoppingToken),
        _ => throw new NotSupportedException("Database provider not supported.")
    };

    private async Task<IDbConnection> ConnectMssqlAsync(CancellationToken stoppingToken)
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(stoppingToken);
        return connection;
    }

    private async Task<IDbConnection> ConnectPostgresAsync(CancellationToken stoppingToken)
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(stoppingToken);
        return connection;
    }
}
