﻿using System.Data;
using System.Data.SqlClient;

namespace DbDeploy.Data;

internal sealed class DbConnector(IOptions<Settings> options)
{
    private readonly string? _connectionString = new SqlConnectionStringBuilder(options.Value.ConnectionString).ConnectionString;

    public async Task<IDbConnection> ConnectAsync(CancellationToken stoppingToken = default)
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(stoppingToken);
        return connection;
    }
}
